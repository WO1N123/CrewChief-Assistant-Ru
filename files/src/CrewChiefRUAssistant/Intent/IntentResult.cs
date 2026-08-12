namespace CrewChiefRUAssistant.Intent;

public sealed record IntentResult(
    IntentKind Kind,
    double Confidence,
    string NormalizedText,
    bool IsFuzzy = false);
