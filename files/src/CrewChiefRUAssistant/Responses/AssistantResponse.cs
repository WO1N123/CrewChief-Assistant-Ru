using CrewChiefRUAssistant.Intent;

namespace CrewChiefRUAssistant.Responses;

public sealed record AssistantResponse(IntentKind Intent, string Text, string? StaticWavKey = null);
