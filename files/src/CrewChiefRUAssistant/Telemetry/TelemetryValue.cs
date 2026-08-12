namespace CrewChiefRUAssistant.Telemetry;

public sealed record TelemetryValue(
    string RawValue,
    double? Number,
    bool? Boolean,
    DateTimeOffset UpdatedAt,
    string Topic);
