using CrewChiefRUAssistant.Intent;
using CrewChiefRUAssistant.Recognition;
using CrewChiefRUAssistant.Responses;
using CrewChiefRUAssistant.Telemetry;
using CrewChiefRUAssistant.Utilities;

namespace CrewChiefRUAssistant;

public sealed record AssistantStats(
    bool Running,
    long MqttMessages,
    int TelemetryFields,
    DateTimeOffset? LastMessageAt);

public sealed class QuestionAnsweredEventArgs : EventArgs
{
    public required string Question { get; init; }
    public required string Answer { get; init; }
}

public sealed class AssistantController : IAsyncDisposable
{
    private readonly AppConfig _config;

    private TelemetryStore? _store;
    private MqttTelemetryBroker? _broker;
    private VoskPushToTalkRecognizer? _recognizer;
    private ResponseSink? _responseSink;
    private IntentEngine? _intentEngine;
    private ResponseComposer? _responseComposer;
    private int? _activeMqttPort;

    public bool IsRunning { get; private set; }

    public event EventHandler<string>? Log;
    public event EventHandler<bool>? ListeningChanged;
    public event EventHandler<QuestionAnsweredEventArgs>? QuestionAnswered;

    public AssistantController(AppConfig config)
    {
        _config = config;
    }

    public async Task StartAsync()
    {
        if (IsRunning)
            return;

        if (_broker is not null &&
            _activeMqttPort != _config.MqttPort)
        {
            await _broker.StopAsync();
            await _broker.DisposeAsync();
            _broker = null;
            _store = null;
            _activeMqttPort = null;
        }

        _store ??= new TelemetryStore(
            TimeSpan.FromSeconds(
                _config.TelemetryMaxAgeSeconds));

        _broker ??= new MqttTelemetryBroker(
            _config.MqttPort,
            _store,
            _config.PrintIncomingTopics);

        _activeMqttPort = _config.MqttPort;
        _intentEngine = new IntentEngine();
        _responseComposer =
            new ResponseComposer(_store);

        _responseSink = new ResponseSink(
            _config.GetVoiceBankDirectory(),
            _config.SpeechEnabled,
            _config.PlaybackDevice,
            _config.SpeechVolumePercent / 100f,
            _config.CrewChiefVoicePriority,
            _config.CrewChiefAudioThreshold,
            _config.CrewChiefActivityHoldMs,
            _config.CrewChiefQuietPeriodMs,
            _config.CrewChiefPriorityMaxWaitMs,
            _config.CrewChiefPriorityMaxReplays);

        _responseSink.PlaybackError +=
            OnPlaybackError;

        _responseSink.PriorityStatus +=
            OnPriorityStatus;

        if (_config.SpeechEnabled)
        {
            var voiceName =
                _config.GetVoiceDisplayName();

            Log?.Invoke(
                this,
                _responseSink.VoiceBankReady
                    ? $"Озвучка Silero {voiceName} с эффектом рации готова."
                    : $"Радио-голос Silero {voiceName} не найден. Переустанови программу с полным голосовым пакетом.");

            if (_responseSink.CrewChiefPriorityEnabled)
            {
                Log?.Invoke(
                    this,
                    "Приоритет CrewChief включён: ассистент ждёт освобождения радиоканала.");
            }
        }

        var pttBinding =
            _config.GetPttBinding();

        _recognizer =
            new VoskPushToTalkRecognizer(
                AppConfig.ModelDirectory,
                _config.MicrophoneDevice,
                pttBinding,
                _config.RecognitionAlternatives,
                _config.RecognitionPreRollMs,
                _config.RecognitionPostRollMs,
                _config.RecognitionMinSpeechMs,
                _config.RecognitionCommandPass);

        Log?.Invoke(
            this,
            $"Кнопка разговора: {pttBinding.DisplayName}");

        Log?.Invoke(
            this,
            $"Распознавание: гибридное, до {_config.RecognitionAlternatives} вариантов, " +
            $"предзапись {_config.RecognitionPreRollMs} мс, окончание {_config.RecognitionPostRollMs} мс.");

        _recognizer.RecognitionCompleted +=
            OnRecognitionCompleted;

        _recognizer.RecognitionFailed +=
            OnRecognitionFailed;

        _recognizer.ListeningStateChanged +=
            OnListeningStateChanged;

        try
        {
            if (!_broker.IsRunning)
                await _broker.StartAsync();

            _recognizer.Start();

            IsRunning = true;

            Log?.Invoke(
                this,
                $"Ассистент запущен. MQTT: 127.0.0.1:{_config.MqttPort}");
        }
        catch
        {
            await StopAsync(preserveTelemetryConnection: false);
            throw;
        }
    }

    public async Task StopAsync(bool preserveTelemetryConnection = false)
    {
        if (_recognizer is not null)
        {
            _recognizer.RecognitionCompleted -=
                OnRecognitionCompleted;

            _recognizer.RecognitionFailed -=
                OnRecognitionFailed;

            _recognizer.ListeningStateChanged -=
                OnListeningStateChanged;

            _recognizer.Stop();
            _recognizer.Dispose();
            _recognizer = null;
        }

        if (!preserveTelemetryConnection && _broker is not null)
        {
            await _broker.StopAsync();
            await _broker.DisposeAsync();
            _broker = null;
            _store = null;
            _activeMqttPort = null;
        }

        if (_responseSink is not null)
        {
            _responseSink.PlaybackError -=
                OnPlaybackError;

            _responseSink.PriorityStatus -=
                OnPriorityStatus;

            _responseSink.Dispose();
            _responseSink = null;
        }

        _intentEngine = null;
        _responseComposer = null;

        if (IsRunning)
            Log?.Invoke(this, "Ассистент остановлен.");

        IsRunning = false;
    }

    public void LoadTestData()
    {
        _store?.LoadTestData();
        Log?.Invoke(
            this,
            "Тестовая телеметрия загружена.");
    }

    public IReadOnlyDictionary<string, TelemetryValue> GetRecentFields(
        int limit = 100) =>
        _store?.GetRecentValues(limit)
        ?? new Dictionary<string, TelemetryValue>();

    public AssistantStats GetStats() =>
        new(
            IsRunning,
            _broker?.MessageCount ?? 0,
            _store?.Count ?? 0,
            _store?.LastMessageAt);

    private void OnRecognitionCompleted(
        object? sender,
        SpeechRecognitionResult result)
    {
        if (_intentEngine is null ||
            _responseComposer is null ||
            result.Candidates.Count == 0)
        {
            return;
        }

        var evaluated = result.Candidates
            .Select(
                candidate =>
                {
                    var intent =
                        _intentEngine.Match(
                            candidate.Text);

                    var knownBonus =
                        intent.Kind == IntentKind.Unknown
                            ? 0
                            : 3.0 + intent.Confidence;

                    var fuzzyPenalty =
                        intent.IsFuzzy
                            ? 0.40
                            : 0;

                    return new
                    {
                        Candidate = candidate,
                        Intent = intent,
                        Score =
                            candidate.Confidence +
                            knownBonus -
                            fuzzyPenalty
                    };
                })
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item =>
                item.Candidate.Confidence)
            .ToArray();

        var acoustic = result.Candidates
            .Where(candidate => candidate.Source == "свободный")
            .OrderByDescending(candidate => candidate.Confidence)
            .FirstOrDefault()
            ?? result.Candidates
                .OrderByDescending(candidate => candidate.Confidence)
                .First();

        var acousticEvaluation = evaluated.First(item =>
            ReferenceEquals(item.Candidate, acoustic) ||
            (item.Candidate.Text == acoustic.Text && item.Candidate.Source == acoustic.Source));

        var closeNaturalExact = evaluated
            .Where(item =>
                item.Candidate.Source == "свободный" &&
                item.Intent.Kind != IntentKind.Unknown &&
                !item.Intent.IsFuzzy &&
                item.Candidate.Confidence >= acoustic.Confidence - 0.08)
            .OrderByDescending(item => item.Candidate.Confidence)
            .ThenByDescending(item => item.Intent.Confidence)
            .FirstOrDefault();

        var closeCommandExact = evaluated
            .Where(item =>
                item.Candidate.Source == "командный" &&
                item.Intent.Kind != IntentKind.Unknown &&
                !item.Intent.IsFuzzy &&
                item.Candidate.Confidence >= acoustic.Confidence - 0.08 &&
                WordOverlap(acoustic.Text, item.Candidate.Text) >= 0.55)
            .OrderByDescending(item => item.Candidate.Confidence)
            .ThenByDescending(item => item.Intent.Confidence)
            .FirstOrDefault();

        // Keep the best natural hypothesis whenever it already maps to an
        // intent, including a fuzzy one. The constrained command recognizer is
        // allowed to rescue only an unknown phrase with substantial word
        // overlap, so it cannot turn an unrelated sentence into another command.
        var selected =
            acousticEvaluation.Intent.Kind != IntentKind.Unknown
                ? acousticEvaluation
                : closeNaturalExact ?? closeCommandExact ?? acousticEvaluation;

        var displayText = acoustic.Text;
        if (selected.Candidate.Source == "командный" &&
            acousticEvaluation.Intent.Kind == IntentKind.Unknown)
        {
            displayText = selected.Candidate.Text;
        }

        if (!selected.Candidate.Text.Equals(acoustic.Text, StringComparison.OrdinalIgnoreCase) ||
            acousticEvaluation.Intent.Kind != selected.Intent.Kind)
        {
            Log?.Invoke(this,
                $"Распознавание уточнено: «{acoustic.Text}» → «{selected.Candidate.Text}».");
        }

        var response =
            RussianSpeakerMorphology.Apply(
                _responseComposer.Compose(
                    selected.Intent),
                _config.VoiceId);

        _responseSink?.Deliver(response);

        QuestionAnswered?.Invoke(
            this,
            new QuestionAnsweredEventArgs
            {
                Question = displayText,
                Answer = response.Text
            });
    }

    private static double WordOverlap(string left, string right)
    {
        var leftWords = RussianText.Normalize(left).Words.ToHashSet(StringComparer.Ordinal);
        var rightWords = RussianText.Normalize(right).Words.ToHashSet(StringComparer.Ordinal);

        if (leftWords.Count == 0 || rightWords.Count == 0)
            return 0;

        var common = leftWords.Count(rightWords.Contains);
        return common / (double)Math.Max(leftWords.Count, rightWords.Count);
    }

    private void OnRecognitionFailed(
        object? sender,
        string message) =>
        Log?.Invoke(
            this,
            $"Распознавание: {message}");

    private void OnPlaybackError(
        object? sender,
        string message) =>
        Log?.Invoke(
            this,
            $"Озвучка: {message}");

    private void OnPriorityStatus(
        object? sender,
        string message) =>
        Log?.Invoke(
            this,
            message);

    private void OnListeningStateChanged(
        object? sender,
        bool listening) =>
        ListeningChanged?.Invoke(
            this,
            listening);

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}
