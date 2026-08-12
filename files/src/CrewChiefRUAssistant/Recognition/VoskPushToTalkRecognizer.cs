using System.IO;
using System.Text.Json;
using CrewChiefRUAssistant.Input;
using NAudio.Wave;
using Vosk;

namespace CrewChiefRUAssistant.Recognition;

public sealed class VoskPushToTalkRecognizer : IDisposable
{
    private const int SampleRate = 16000;
    private const int BytesPerSample = 2;

    private readonly Model _model;
    private readonly WaveInEvent _waveIn;
    private readonly InputBinding _binding;
    private readonly InputBindingReader _inputReader = new();
    private readonly object _sync = new();
    private readonly CancellationTokenSource _cts = new();

    private readonly int _maxAlternatives;
    private readonly int _postRollMilliseconds;
    private readonly int _minimumSpeechMilliseconds;
    private readonly bool _commandGrammarPass;

    private readonly byte[] _preRollBuffer;
    private int _preRollWriteIndex;
    private int _preRollCount;

    private VoskRecognizer? _recognizer;
    private MemoryStream? _utteranceAudio;
    private CancellationTokenSource? _releaseDelay;
    private Task? _buttonMonitorTask;

    private volatile bool _buttonWasDown;
    private bool _listening;
    private int _activeSpeechBytes;
    private bool _disposed;
    private int _recordingRestartPending;

    public event EventHandler<SpeechRecognitionResult>? RecognitionCompleted;
    public event EventHandler<string>? RecognitionFailed;
    public event EventHandler<bool>? ListeningStateChanged;

    public VoskPushToTalkRecognizer(
        string modelPath,
        int microphoneDevice,
        InputBinding binding,
        int maxAlternatives,
        int preRollMilliseconds,
        int postRollMilliseconds,
        int minimumSpeechMilliseconds,
        bool commandGrammarPass)
    {
        if (!Directory.Exists(modelPath))
        {
            throw new DirectoryNotFoundException(
                $"Не найдена модель Vosk: {modelPath}.");
        }

        Vosk.Vosk.SetLogLevel(-1);

        _model = new Model(modelPath);
        _binding = binding.Clone();
        _maxAlternatives = Math.Clamp(maxAlternatives, 1, 10);
        _postRollMilliseconds = Math.Clamp(
            postRollMilliseconds,
            0,
            600);
        _minimumSpeechMilliseconds = Math.Clamp(
            minimumSpeechMilliseconds,
            80,
            1000);
        _commandGrammarPass = commandGrammarPass;

        var preRollBytes =
            SampleRate *
            BytesPerSample *
            Math.Clamp(preRollMilliseconds, 0, 600) /
            1000;

        _preRollBuffer = new byte[Math.Max(
            BytesPerSample,
            preRollBytes)];

        if (WaveIn.DeviceCount == 0)
        {
            throw new InvalidOperationException(
                "Windows не обнаружила ни одного устройства записи.");
        }

        if (microphoneDevice < 0 ||
            microphoneDevice >= WaveIn.DeviceCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(microphoneDevice),
                $"Устройство #{microphoneDevice} отсутствует. Доступно: {WaveIn.DeviceCount}.");
        }

        _waveIn = new WaveInEvent
        {
            DeviceNumber = microphoneDevice,
            WaveFormat = new WaveFormat(
                SampleRate,
                16,
                1),
            BufferMilliseconds = 30,
            NumberOfBuffers = 4
        };

        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.RecordingStopped += OnRecordingStopped;
    }

    public void Start()
    {
        ThrowIfDisposed();

        _waveIn.StartRecording();

        _buttonMonitorTask =
            Task.Run(() => MonitorButtonAsync(_cts.Token));
    }

    public void Stop()
    {
        if (_disposed)
            return;

        _cts.Cancel();
        _buttonWasDown = false;

        CancelReleaseDelay();

        if (_listening)
            EndUtterance();

        _waveIn.StopRecording();
    }

    private async Task MonitorButtonAsync(
        CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var buttonDown = _inputReader.IsPressed(_binding);

                if (buttonDown && !_buttonWasDown)
                    BeginUtterance();
                else if (!buttonDown && _buttonWasDown)
                    ScheduleUtteranceEnd();

                _buttonWasDown = buttonDown;
            }
            catch (Exception ex)
            {
                // A transient input error can otherwise leave _listening true
                // after the key was released. The next press would then be
                // ignored until the whole assistant was restarted.
                _buttonWasDown = false;

                if (_listening)
                {
                    try
                    {
                        EndUtterance();
                    }
                    catch
                    {
                        // State recovery is more important than preserving a
                        // partially recorded phrase.
                    }
                }

                RecognitionFailed?.Invoke(this, $"ошибка кнопки разговора: {ex.Message}");
            }

            try
            {
                await Task.Delay(10, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void BeginUtterance()
    {
        CancelReleaseDelay();

        lock (_sync)
        {
            if (_listening)
                return;

            _recognizer?.Dispose();
            _recognizer = CreateFreeRecognizer();

            _utteranceAudio?.Dispose();
            _utteranceAudio = new MemoryStream(
                capacity: SampleRate * BytesPerSample * 4);

            _activeSpeechBytes = 0;

            var preRoll = SnapshotPreRoll();

            if (preRoll.Length > 0)
            {
                _recognizer.AcceptWaveform(
                    preRoll,
                    preRoll.Length);

                _utteranceAudio.Write(
                    preRoll,
                    0,
                    preRoll.Length);

                UpdateSpeechActivity(
                    preRoll,
                    preRoll.Length);
            }

            _listening = true;
        }

        ListeningStateChanged?.Invoke(this, true);
    }

    private void ScheduleUtteranceEnd()
    {
        CancellationToken token;

        lock (_sync)
        {
            if (!_listening)
                return;

            _releaseDelay?.Cancel();
            _releaseDelay?.Dispose();
            _releaseDelay = new CancellationTokenSource();
            token = _releaseDelay.Token;
        }

        _ = Task.Run(
            async () =>
            {
                try
                {
                    await Task.Delay(
                        _postRollMilliseconds,
                        token);

                    if (!token.IsCancellationRequested &&
                        !_buttonWasDown)
                    {
                        EndUtterance();
                    }
                }
                catch (OperationCanceledException)
                {
                    // The button was pressed again.
                }
            },
            CancellationToken.None);
    }

    private void EndUtterance()
    {
        VoskRecognizer? recognizer;
        byte[] audio;
        int activeSpeechBytes;

        lock (_sync)
        {
            if (!_listening ||
                _recognizer is null ||
                _utteranceAudio is null)
            {
                return;
            }

            _listening = false;

            _releaseDelay?.Cancel();
            _releaseDelay?.Dispose();
            _releaseDelay = null;

            recognizer = _recognizer;
            _recognizer = null;

            audio = _utteranceAudio.ToArray();
            _utteranceAudio.Dispose();
            _utteranceAudio = null;

            activeSpeechBytes = _activeSpeechBytes;
            _activeSpeechBytes = 0;
        }

        ListeningStateChanged?.Invoke(this, false);

        var activeMilliseconds =
            activeSpeechBytes *
            1000 /
            (SampleRate * BytesPerSample);

        if (activeMilliseconds < _minimumSpeechMilliseconds)
        {
            recognizer.Dispose();
            RecognitionFailed?.Invoke(
                this,
                "фраза слишком короткая или микрофон слишком тихий");
            return;
        }

        try
        {
            var candidates = new List<SpeechCandidate>();

            AddCandidates(
                candidates,
                recognizer.FinalResult(),
                "свободный");

            if (_commandGrammarPass && audio.Length > 0)
            {
                AddCandidates(
                    candidates,
                    RunCommandGrammarPass(audio),
                    "командный");
            }

            var merged = MergeCandidates(candidates);

            if (merged.Count == 0)
            {
                RecognitionFailed?.Invoke(
                    this,
                    "речь не распознана");
                return;
            }

            RecognitionCompleted?.Invoke(
                this,
                new SpeechRecognitionResult(merged));
        }
        catch (Exception ex)
        {
            RecognitionFailed?.Invoke(
                this,
                $"ошибка Vosk: {ex.Message}");
        }
        finally
        {
            recognizer.Dispose();
        }
    }

    private VoskRecognizer CreateFreeRecognizer()
    {
        var recognizer =
            new VoskRecognizer(_model, SampleRate);

        recognizer.SetMaxAlternatives(
            _maxAlternatives);

        recognizer.SetWords(false);
        return recognizer;
    }

    private string RunCommandGrammarPass(
        byte[] audio)
    {
        using var recognizer =
            new VoskRecognizer(
                _model,
                SampleRate,
                RecognitionGrammar.CommandJson);

        recognizer.SetMaxAlternatives(
            Math.Min(4, _maxAlternatives));

        recognizer.SetWords(false);

        const int blockSize = 8192;

        for (var offset = 0;
             offset < audio.Length;
             offset += blockSize)
        {
            var count = Math.Min(
                blockSize,
                audio.Length - offset);

            if (offset == 0 && count == audio.Length)
            {
                recognizer.AcceptWaveform(
                    audio,
                    count);
                continue;
            }

            var block = new byte[count];

            Buffer.BlockCopy(
                audio,
                offset,
                block,
                0,
                count);

            recognizer.AcceptWaveform(
                block,
                count);
        }

        return recognizer.FinalResult();
    }

    private void OnDataAvailable(
        object? sender,
        WaveInEventArgs args)
    {
        lock (_sync)
        {
            if (!_listening ||
                _recognizer is null ||
                _utteranceAudio is null)
            {
                AddToPreRoll(
                    args.Buffer,
                    args.BytesRecorded);
                return;
            }

            _recognizer.AcceptWaveform(
                args.Buffer,
                args.BytesRecorded);

            _utteranceAudio.Write(
                args.Buffer,
                0,
                args.BytesRecorded);

            UpdateSpeechActivity(
                args.Buffer,
                args.BytesRecorded);
        }
    }

    private void UpdateSpeechActivity(
        byte[] buffer,
        int count)
    {
        if (count < BytesPerSample)
            return;

        double sumSquares = 0;
        var samples = count / BytesPerSample;

        for (var offset = 0;
             offset + 1 < count;
             offset += 2)
        {
            var sample =
                (short)(
                    buffer[offset] |
                    buffer[offset + 1] << 8);

            var normalized = sample / 32768.0;
            sumSquares += normalized * normalized;
        }

        var rms = Math.Sqrt(
            sumSquares /
            Math.Max(1, samples));

        // Low enough to retain quiet speech, high enough to ignore pure
        // digital silence and most button-release gaps.
        if (rms >= 0.0025)
            _activeSpeechBytes += count;
    }

    private void AddToPreRoll(
        byte[] source,
        int count)
    {
        if (_preRollBuffer.Length == 0 ||
            count <= 0)
        {
            return;
        }

        if (count >= _preRollBuffer.Length)
        {
            Buffer.BlockCopy(
                source,
                count - _preRollBuffer.Length,
                _preRollBuffer,
                0,
                _preRollBuffer.Length);

            _preRollWriteIndex = 0;
            _preRollCount = _preRollBuffer.Length;
            return;
        }

        var firstPart = Math.Min(
            count,
            _preRollBuffer.Length - _preRollWriteIndex);

        Buffer.BlockCopy(
            source,
            0,
            _preRollBuffer,
            _preRollWriteIndex,
            firstPart);

        var remaining = count - firstPart;

        if (remaining > 0)
        {
            Buffer.BlockCopy(
                source,
                firstPart,
                _preRollBuffer,
                0,
                remaining);
        }

        _preRollWriteIndex =
            (_preRollWriteIndex + count) %
            _preRollBuffer.Length;

        _preRollCount = Math.Min(
            _preRollBuffer.Length,
            _preRollCount + count);
    }

    private byte[] SnapshotPreRoll()
    {
        if (_preRollCount <= 0)
            return [];

        var result = new byte[_preRollCount];

        var start =
            (_preRollWriteIndex -
             _preRollCount +
             _preRollBuffer.Length) %
            _preRollBuffer.Length;

        var firstPart = Math.Min(
            _preRollCount,
            _preRollBuffer.Length - start);

        Buffer.BlockCopy(
            _preRollBuffer,
            start,
            result,
            0,
            firstPart);

        var remaining =
            _preRollCount - firstPart;

        if (remaining > 0)
        {
            Buffer.BlockCopy(
                _preRollBuffer,
                0,
                result,
                firstPart,
                remaining);
        }

        return result;
    }

    private static void AddCandidates(
        ICollection<SpeechCandidate> destination,
        string json,
        string source)
    {
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            using var document =
                JsonDocument.Parse(json);

            var root = document.RootElement;

            if (root.TryGetProperty(
                    "alternatives",
                    out var alternatives) &&
                alternatives.ValueKind ==
                JsonValueKind.Array)
            {
                foreach (var alternative in
                         alternatives.EnumerateArray())
                {
                    var text = ReadText(alternative);

                    if (string.IsNullOrWhiteSpace(text) ||
                        text.Contains(
                            "[unk]",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var confidence =
                        alternative.TryGetProperty(
                            "confidence",
                            out var value) &&
                        value.TryGetDouble(
                            out var parsed)
                            ? parsed
                            : 0.5;

                    destination.Add(
                        new SpeechCandidate(
                            text,
                            Math.Clamp(confidence, 0, 1),
                            source));
                }

                return;
            }

            var singleText = ReadText(root);

            if (!string.IsNullOrWhiteSpace(singleText) &&
                !singleText.Contains(
                    "[unk]",
                    StringComparison.OrdinalIgnoreCase))
            {
                destination.Add(
                    new SpeechCandidate(
                        singleText,
                        0.5,
                        source));
            }
        }
        catch (JsonException)
        {
            // Ignore malformed recognizer output.
        }
    }

    private static string ReadText(
        JsonElement element)
    {
        if (!element.TryGetProperty(
                "text",
                out var textElement))
        {
            return string.Empty;
        }

        return string.Join(
            ' ',
            (textElement.GetString() ?? string.Empty)
                .Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries));
    }

    private static IReadOnlyList<SpeechCandidate> MergeCandidates(
        IEnumerable<SpeechCandidate> candidates)
    {
        var merged =
            new Dictionary<string, SpeechCandidate>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in candidates)
        {
            if (!merged.TryGetValue(
                    candidate.Text,
                    out var existing) ||
                candidate.Confidence >
                existing.Confidence)
            {
                merged[candidate.Text] =
                    candidate;
            }
        }

        return merged.Values
            .OrderByDescending(candidate =>
                candidate.Confidence)
            .ThenBy(candidate =>
                candidate.Source ==
                "командный"
                    ? 0
                    : 1)
            .Take(10)
            .ToArray();
    }

    private void CancelReleaseDelay()
    {
        lock (_sync)
        {
            _releaseDelay?.Cancel();
            _releaseDelay?.Dispose();
            _releaseDelay = null;
        }
    }

    private void OnRecordingStopped(
        object? sender,
        StoppedEventArgs args)
    {
        if (_disposed || _cts.IsCancellationRequested)
            return;

        var reason = args.Exception?.Message ?? "устройство записи остановилось";
        RecognitionFailed?.Invoke(this, $"{reason}; микрофон будет перезапущен");

        if (Interlocked.Exchange(ref _recordingRestartPending, 1) != 0)
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(300, _cts.Token);
                if (!_disposed && !_cts.IsCancellationRequested)
                    _waveIn.StartRecording();
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown.
            }
            catch (Exception ex)
            {
                RecognitionFailed?.Invoke(this, $"не удалось перезапустить микрофон: {ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref _recordingRestartPending, 0);
            }
        });
    }

    public static IReadOnlyList<string> GetRecordingDevices()
    {
        var devices = new List<string>();

        for (var index = 0;
             index < WaveIn.DeviceCount;
             index++)
        {
            var capabilities =
                WaveIn.GetCapabilities(index);

            devices.Add(
                $"{index}: {capabilities.ProductName}");
        }

        return devices;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cts.Cancel();

        CancelReleaseDelay();

        try
        {
            _buttonMonitorTask?.Wait(
                TimeSpan.FromSeconds(1));
        }
        catch
        {
            // Cancellation while shutting down is harmless.
        }

        _waveIn.DataAvailable -=
            OnDataAvailable;

        _waveIn.RecordingStopped -=
            OnRecordingStopped;

        _waveIn.Dispose();

        lock (_sync)
        {
            _recognizer?.Dispose();
            _recognizer = null;

            _utteranceAudio?.Dispose();
            _utteranceAudio = null;
        }

        _model.Dispose();
        _cts.Dispose();
    }
}
