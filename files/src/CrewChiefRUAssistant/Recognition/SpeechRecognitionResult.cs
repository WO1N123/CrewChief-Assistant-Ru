namespace CrewChiefRUAssistant.Recognition;

public sealed record SpeechCandidate(
    string Text,
    double Confidence,
    string Source);

public sealed class SpeechRecognitionResult : EventArgs
{
    public SpeechRecognitionResult(
        IReadOnlyList<SpeechCandidate> candidates)
    {
        Candidates = candidates;
    }

    public IReadOnlyList<SpeechCandidate> Candidates { get; }
}
