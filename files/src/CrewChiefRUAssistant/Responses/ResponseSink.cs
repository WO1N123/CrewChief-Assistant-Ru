using CrewChiefRUAssistant.Audio;

namespace CrewChiefRUAssistant.Responses;

public sealed class ResponseSink : IDisposable
{
    private readonly bool _speechEnabled;
    private readonly int _quietPeriodMilliseconds;
    private readonly int _maximumWaitMilliseconds;
    private readonly int _maximumReplays;

    private readonly RussianVoicePlanner _planner = new();
    private readonly VoiceBankPlayer _voicePlayer;
    private readonly CrewChiefAudioPriorityMonitor? _priorityMonitor;

    private readonly object _deliverySync = new();
    private CancellationTokenSource? _deliveryCancellation;
    private Task? _deliveryTask;
    private bool _disposed;

    public ResponseSink(
        string voiceBankDirectory,
        bool speechEnabled,
        int playbackDevice,
        float volume,
        bool crewChiefVoicePriority,
        double crewChiefAudioThreshold,
        int crewChiefActivityHoldMilliseconds,
        int crewChiefQuietPeriodMilliseconds,
        int crewChiefPriorityMaximumWaitMilliseconds,
        int crewChiefPriorityMaximumReplays)
    {
        _speechEnabled = speechEnabled;

        _quietPeriodMilliseconds = Math.Clamp(
            crewChiefQuietPeriodMilliseconds,
            0,
            5000);

        _maximumWaitMilliseconds = Math.Clamp(
            crewChiefPriorityMaximumWaitMilliseconds,
            500,
            120000);

        _maximumReplays = Math.Clamp(
            crewChiefPriorityMaximumReplays,
            0,
            10);

        _voicePlayer = new VoiceBankPlayer(
            voiceBankDirectory,
            playbackDevice,
            volume);

        if (_speechEnabled &&
            crewChiefVoicePriority)
        {
            _priorityMonitor =
                new CrewChiefAudioPriorityMonitor(
                    crewChiefAudioThreshold,
                    crewChiefActivityHoldMilliseconds);

            _priorityMonitor.MonitorError +=
                OnPriorityMonitorError;

            _priorityMonitor.Start();
        }
    }

    public bool VoiceBankReady =>
        _voicePlayer.IsReady;

    public bool CrewChiefPriorityEnabled =>
        _priorityMonitor is not null;

    public event EventHandler<string>? PlaybackError;
    public event EventHandler<string>? PriorityStatus;

    public void Deliver(
        AssistantResponse response)
    {
        if (!_speechEnabled ||
            !_voicePlayer.IsReady ||
            _disposed)
        {
            return;
        }

        var tokens =
            _planner.Plan(response);

        if (tokens.Count == 0)
            return;

        CancellationTokenSource cancellation;

        lock (_deliverySync)
        {
            _deliveryCancellation?.Cancel();
            _deliveryCancellation?.Dispose();

            _deliveryCancellation =
                new CancellationTokenSource();

            cancellation =
                _deliveryCancellation;

            _voicePlayer.Stop();

            _deliveryTask = Task.Run(
                () => DeliverAsync(
                    tokens,
                    cancellation.Token));
        }
    }

    private async Task DeliverAsync(
        IReadOnlyList<string> tokens,
        CancellationToken cancellationToken)
    {
        try
        {
            if (_priorityMonitor is null)
            {
                _voicePlayer.Play(tokens);
                return;
            }

            var wasDeferred =
                _priorityMonitor.IsSpeaking;

            if (wasDeferred)
            {
                PriorityStatus?.Invoke(
                    this,
                    "CrewChief говорит — ответ ассистента отложен.");
            }

            var quiet =
                await _priorityMonitor.WaitForQuietAsync(
                    _quietPeriodMilliseconds,
                    _maximumWaitMilliseconds,
                    cancellationToken);

            if (!quiet)
            {
                PriorityStatus?.Invoke(
                    this,
                    "Ответ ассистента отменён: CrewChief говорит слишком долго.");
                return;
            }

            if (wasDeferred)
            {
                PriorityStatus?.Invoke(
                    this,
                    "CrewChief закончил — ответ ассистента воспроизводится.");
            }

            var replayCount = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                _voicePlayer.Play(tokens);

                var interrupted = false;

                while (_voicePlayer.IsPlaying)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (_priorityMonitor.IsSpeaking)
                    {
                        interrupted = true;
                        _voicePlayer.Stop();
                        break;
                    }

                    await Task.Delay(
                        30,
                        cancellationToken);
                }

                if (!interrupted)
                    return;

                replayCount++;

                if (replayCount > _maximumReplays)
                {
                    PriorityStatus?.Invoke(
                        this,
                        "Ответ ассистента отменён после повторных сообщений CrewChief.");
                    return;
                }

                PriorityStatus?.Invoke(
                    this,
                    "CrewChief заговорил — ответ ассистента остановлен и будет повторён.");

                quiet =
                    await _priorityMonitor.WaitForQuietAsync(
                        _quietPeriodMilliseconds,
                        _maximumWaitMilliseconds,
                        cancellationToken);

                if (!quiet)
                {
                    PriorityStatus?.Invoke(
                        this,
                        "Ответ ассистента отменён: CrewChief говорит слишком долго.");
                    return;
                }

                PriorityStatus?.Invoke(
                    this,
                    "CrewChief закончил — ответ ассистента повторяется.");
            }
        }
        catch (OperationCanceledException)
        {
            _voicePlayer.Stop();
        }
        catch (Exception ex)
        {
            _voicePlayer.Stop();

            PlaybackError?.Invoke(
                this,
                ex.Message);
        }
    }

    private void OnPriorityMonitorError(
        object? sender,
        string message) =>
        PlaybackError?.Invoke(
            this,
            $"контроль приоритета CrewChief: {message}");

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        lock (_deliverySync)
        {
            _deliveryCancellation?.Cancel();
            _deliveryCancellation?.Dispose();
            _deliveryCancellation = null;
        }

        _voicePlayer.Stop();

        if (_priorityMonitor is not null)
        {
            _priorityMonitor.MonitorError -=
                OnPriorityMonitorError;

            _priorityMonitor.Dispose();
        }

        try
        {
            _deliveryTask?.Wait(
                TimeSpan.FromSeconds(1));
        }
        catch
        {
            // Cancellation while shutting down is harmless.
        }

        _voicePlayer.Dispose();
    }
}
