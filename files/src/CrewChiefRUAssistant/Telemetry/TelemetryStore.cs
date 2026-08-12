using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using CrewChiefRUAssistant.Utilities;

namespace CrewChiefRUAssistant.Telemetry;

public sealed class TelemetryStore
{
    private readonly ConcurrentDictionary<string, TelemetryValue> _values =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly TimeSpan _maxAge;
    private readonly object _sessionLock = new();
    private readonly Queue<double> _completedLapTimes = new();
    private readonly Queue<double> _fuelUsePerLap = new();

    private string? _activeSessionKey;
    private int? _trackedLapNumber;
    private int? _lastRecordedLapNumber;
    private double _maxCurrentLapTime;
    private double? _lastRecordedLapTime;
    private double? _latestFuelLevel;
    private double? _fuelAtLapStart;

    public TelemetryStore(TimeSpan maxAge)
    {
        _maxAge = maxAge;
    }

    public int Count => _values.Count;
    public DateTimeOffset? LastMessageAt { get; private set; }

    public void Ingest(string topic, string payload)
    {
        if (IsPlaceholderTopic(topic))
            return;

        UpdateSession(topic);

        var now = DateTimeOffset.UtcNow;
        LastMessageAt = now;

        if (TryParseJson(payload, out var document))
        {
            using (document)
            {
                var flattened = JsonFlattener.Flatten(document.RootElement);

                if (flattened.Count == 0)
                {
                    TrackDerivedTelemetry(topic, payload);
                    Store(topic, payload, topic, now);
                    return;
                }

                foreach (var pair in flattened)
                {
                    TrackDerivedTelemetry(pair.Key, pair.Value);

                    var fullKey = string.IsNullOrWhiteSpace(pair.Key)
                        ? topic
                        : $"{topic}.{pair.Key}";

                    Store(fullKey, pair.Value, topic, now);
                    Store(pair.Key, pair.Value, topic, now);
                }
            }

            return;
        }

        TrackDerivedTelemetry(topic, payload);

        Store(topic, payload, topic, now);
        var lastSegment = topic.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        if (!string.IsNullOrWhiteSpace(lastSegment))
        {
            Store(lastSegment, payload, topic, now);
        }
    }

    public bool TryGetNumber(out double value, params string[] aliases)
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var alias in aliases)
        {
            var candidates = _values
                .Where(pair =>
                    pair.Value.Number.HasValue &&
                    IsFresh(pair.Value, now) &&
                    KeyMatches(pair.Key, alias))
                .OrderByDescending(pair => pair.Value.UpdatedAt)
                .ToArray();

            if (candidates.Length > 0)
            {
                value = candidates[0].Value.Number!.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    public bool TryGetText(out string value, params string[] aliases)
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var alias in aliases)
        {
            var candidate = _values
                .Where(pair =>
                    !string.IsNullOrWhiteSpace(pair.Value.RawValue) &&
                    IsFresh(pair.Value, now) &&
                    KeyMatches(pair.Key, alias))
                .OrderByDescending(pair => pair.Value.UpdatedAt)
                .FirstOrDefault();

            if (candidate.Value is not null)
            {
                value = candidate.Value.RawValue;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    public bool TryGetBoolean(out bool value, params string[] aliases)
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var alias in aliases)
        {
            var candidate = _values
                .Where(pair =>
                    pair.Value.Boolean.HasValue &&
                    IsFresh(pair.Value, now) &&
                    KeyMatches(pair.Key, alias))
                .OrderByDescending(pair => pair.Value.UpdatedAt)
                .FirstOrDefault();

            if (candidate.Value is not null)
            {
                value = candidate.Value.Boolean!.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    public bool TryGetLastLapTime(out double value)
    {
        lock (_sessionLock)
        {
            if (_completedLapTimes.Count == 0)
            {
                value = default;
                return false;
            }

            value = _completedLapTimes.Last();
            return true;
        }
    }

    public bool TryGetBestLapTime(out double value)
    {
        lock (_sessionLock)
        {
            if (_completedLapTimes.Count == 0)
            {
                value = default;
                return false;
            }

            value = _completedLapTimes.Min();
            return true;
        }
    }

    public bool TryGetAverageLapTime(out double value)
    {
        lock (_sessionLock)
        {
            if (_completedLapTimes.Count == 0)
            {
                value = default;
                return false;
            }

            value = _completedLapTimes.Average();
            return true;
        }
    }

    public bool TryGetAverageFuelPerLap(out double value)
    {
        lock (_sessionLock)
        {
            if (_fuelUsePerLap.Count == 0)
            {
                value = default;
                return false;
            }

            value = _fuelUsePerLap.Average();
            return true;
        }
    }

    public IReadOnlyDictionary<string, TelemetryValue> GetRecentValues(int limit)
    {
        var now = DateTimeOffset.UtcNow;

        return _values
            .Where(pair => IsFresh(pair.Value, now))
            .OrderByDescending(pair => pair.Value.UpdatedAt)
            .Take(limit)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
    }

    public void LoadTestData()
    {
        var now = DateTimeOffset.UtcNow;
        LastMessageAt = now;

        var test = new Dictionary<string, string>
        {
            ["SessionData.OverallPosition"] = "7",
            ["SessionData.ClassPosition"] = "4",
            ["CarClass"] = "HYPER_CAR_RACE",
            ["SessionData.LapCount"] = "13",
            ["SessionData.CompletedLaps"] = "12",
            ["SessionData.SessionLapsRemaining"] = "8",
            ["SessionData.SessionNumberOfLaps"] = "20",
            ["SessionData.SessionTimeRemaining"] = "1234",
            ["SessionData.LapTimeCurrent"] = "42.417",
            ["SessionData.LapTimePrevious"] = "91.482",
            ["SessionData.PlayerLapTimeSessionBest"] = "90.917",
            ["SessionData.SectorNumber"] = "2",
            ["SessionData.LastSector1Time"] = "29.870",
            ["SessionData.LastSector2Time"] = "31.211",
            ["SessionData.LastSector3Time"] = "30.401",
            ["SessionData.TimeDeltaFront"] = "2.41",
            ["SessionData.TimeDeltaBehind"] = "1.73",
            ["SessionData.Flag"] = "GREEN",
            ["SessionData.CurrentIncidentCount"] = "3",
            ["SessionData.MaxIncidentCount"] = "17",
            ["SessionData.NumCarsOverall"] = "32",
            ["SessionData.NumCarsInPlayerClass"] = "12",
            ["SessionData.PreviousLapWasValid"] = "true",
            ["SessionData.IsLastLap"] = "false",
            ["SessionData.SessionType"] = "Race",
            ["SessionData.SessionPhase"] = "Green",
            ["SessionData.TrackDefinition.name"] = "Monza",
            ["SessionData.TrackDefinition.trackLength"] = "5793",
            ["Conditions.CurrentConditions.TrackTemperature"] = "31",
            ["Conditions.CurrentConditions.AmbientTemperature"] = "24",
            ["FuelData.FuelLeft"] = "19.4",
            ["FuelData.FuelCapacity"] = "100",
            ["BatteryData.BatteryPercentageLeft"] = "73",
            ["FlagData.distanceToNearestIncident"] = "420",
            ["FuelData.AverageUsagePerLap"] = "2.31",
            ["TyreData.FrontLeft_CenterTemp"] = "84",
            ["TyreData.FrontRight_CenterTemp"] = "86",
            ["TyreData.RearLeft_CenterTemp"] = "81",
            ["TyreData.RearRight_CenterTemp"] = "82",
            ["TyreData.FrontLeftPercentWear"] = "17",
            ["TyreData.FrontRightPercentWear"] = "20",
            ["TyreData.RearLeftPercentWear"] = "13",
            ["TyreData.RearRightPercentWear"] = "14",
            ["TyreData.FrontLeftPressure"] = "24.1",
            ["TyreData.FrontRightPressure"] = "24.0",
            ["TyreData.RearLeftPressure"] = "23.8",
            ["TyreData.RearRightPressure"] = "23.9",
            ["TyreData.TyreTypeName"] = "Medium",
            ["TyreData.fittedSet"] = "2",
            ["TyreData.LeftFrontBrakeTemp"] = "525",
            ["TyreData.RightFrontBrakeTemp"] = "530",
            ["TyreData.LeftRearBrakeTemp"] = "410",
            ["TyreData.RightRearBrakeTemp"] = "415",
            ["TyreData.LeftFrontIsLocked"] = "false",
            ["TyreData.RightFrontIsLocked"] = "false",
            ["TyreData.LeftRearIsLocked"] = "false",
            ["TyreData.RightRearIsLocked"] = "false",
            ["TyreData.LeftFrontIsSpinning"] = "false",
            ["TyreData.RightFrontIsSpinning"] = "false",
            ["TyreData.LeftRearIsSpinning"] = "false",
            ["TyreData.RightRearIsSpinning"] = "false",
            ["TyreData.LeftFrontAttached"] = "true",
            ["TyreData.RightFrontAttached"] = "true",
            ["TyreData.LeftRearAttached"] = "true",
            ["TyreData.RightRearAttached"] = "true",
            ["CarDamageData.OverallEngineDamage"] = "NONE",
            ["CarDamageData.OverallAeroDamage"] = "TRIVIAL",
            ["CarDamageData.OverallTransmissionDamage"] = "NONE"
        };

        foreach (var pair in test)
        {
            TrackDerivedTelemetry(pair.Key, pair.Value);
            Store(pair.Key, pair.Value, "test", now);
        }
    }

    private void UpdateSession(string topic)
    {
        var parts = topic.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3 ||
            !parts[0].Equals("crewchief", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var sessionKey = string.Join("/", parts.Take(3));

        lock (_sessionLock)
        {
            if (_activeSessionKey is null)
            {
                _activeSessionKey = sessionKey;
                return;
            }

            if (_activeSessionKey.Equals(sessionKey, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _activeSessionKey = sessionKey;
            ResetSessionStateLocked();
        }
    }

    private void TrackDerivedTelemetry(string key, string raw)
    {
        raw = raw.Trim().Trim('"');

        if (!double.TryParse(
                raw,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var number))
        {
            return;
        }

        lock (_sessionLock)
        {
            if (IsField(key, "FuelLeft") ||
                IsField(key, "FuelRemaining"))
            {
                if (number >= 0 && number <= 10000)
                {
                    _latestFuelLevel = number;

                    if (_trackedLapNumber.HasValue &&
                        !_fuelAtLapStart.HasValue)
                    {
                        _fuelAtLapStart = number;
                    }
                }

                return;
            }

            if (IsField(key, "LapTimePrevious") ||
                IsField(key, "PreviousLapTime") ||
                IsField(key, "LastLapTime"))
            {
                if (IsPlausibleLapTime(number))
                {
                    int? completedLap = _trackedLapNumber.HasValue
                        ? Math.Max(1, _trackedLapNumber.Value - 1)
                        : null;

                    RecordCompletedLapLocked(number, completedLap);
                }

                return;
            }

            if (IsField(key, "CurrentLapTime") ||
                IsField(key, "LapTimeCurrent"))
            {
                if (number >= 0 && number <= 7200)
                {
                    _maxCurrentLapTime = Math.Max(_maxCurrentLapTime, number);
                }

                return;
            }

            if (!IsField(key, "CurrentLap") &&
                !IsField(key, "LapCount"))
            {
                return;
            }

            var lapNumber = (int)Math.Round(number);
            if (lapNumber < 1)
                return;

            if (!_trackedLapNumber.HasValue)
            {
                _trackedLapNumber = lapNumber;
                _fuelAtLapStart = _latestFuelLevel;
                return;
            }

            if (lapNumber == _trackedLapNumber.Value)
                return;

            if (lapNumber == _trackedLapNumber.Value + 1)
            {
                if (IsPlausibleLapTime(_maxCurrentLapTime))
                {
                    RecordCompletedLapLocked(
                        _maxCurrentLapTime,
                        _trackedLapNumber.Value);
                }

                RecordFuelUseLocked();
            }

            _trackedLapNumber = lapNumber;
            _maxCurrentLapTime = 0;
            _fuelAtLapStart = _latestFuelLevel;
        }
    }

    private void RecordFuelUseLocked()
    {
        if (!_fuelAtLapStart.HasValue ||
            !_latestFuelLevel.HasValue)
        {
            return;
        }

        var used = _fuelAtLapStart.Value - _latestFuelLevel.Value;

        if (used < 0.05 || used > 100)
            return;

        _fuelUsePerLap.Enqueue(used);

        while (_fuelUsePerLap.Count > 10)
        {
            _fuelUsePerLap.Dequeue();
        }
    }

    private void RecordCompletedLapLocked(double lapTime, int? lapNumber)
    {
        if (lapNumber.HasValue &&
            _lastRecordedLapNumber.HasValue &&
            lapNumber.Value == _lastRecordedLapNumber.Value)
        {
            return;
        }

        if (!lapNumber.HasValue &&
            _lastRecordedLapTime.HasValue &&
            Math.Abs(_lastRecordedLapTime.Value - lapTime) < 0.001)
        {
            return;
        }

        if (lapNumber.HasValue &&
            !_lastRecordedLapNumber.HasValue &&
            _lastRecordedLapTime.HasValue &&
            Math.Abs(_lastRecordedLapTime.Value - lapTime) < 0.001)
        {
            _lastRecordedLapNumber = lapNumber;
            return;
        }

        _lastRecordedLapNumber = lapNumber;
        _lastRecordedLapTime = lapTime;
        _completedLapTimes.Enqueue(lapTime);

        while (_completedLapTimes.Count > 50)
        {
            _completedLapTimes.Dequeue();
        }
    }

    private void ResetSessionStateLocked()
    {
        _values.Clear();
        _completedLapTimes.Clear();
        _fuelUsePerLap.Clear();
        _trackedLapNumber = null;
        _lastRecordedLapNumber = null;
        _maxCurrentLapTime = 0;
        _lastRecordedLapTime = null;
        _latestFuelLevel = null;
        _fuelAtLapStart = null;
    }

    private bool IsFresh(TelemetryValue value, DateTimeOffset now)
    {
        if (string.Equals(value.Topic, "test", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return now - value.UpdatedAt <= _maxAge;
    }

    private void Store(string key, string raw, string topic, DateTimeOffset now)
    {
        raw = raw.Trim().Trim('"');

        double? number = null;
        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedNumber))
        {
            number = parsedNumber;
        }

        bool? boolean = null;
        if (bool.TryParse(raw, out var parsedBoolean))
        {
            boolean = parsedBoolean;
        }

        _values[key] = new TelemetryValue(raw, number, boolean, now, topic);
    }

    private static bool IsPlaceholderTopic(string topic) =>
        topic.Contains(
            "/Unknown/NewSession.",
            StringComparison.OrdinalIgnoreCase) ||
        topic.Contains(
            "/Unknown/NewSession/",
            StringComparison.OrdinalIgnoreCase);

    private static bool IsPlausibleLapTime(double value) =>
        value >= 10 && value <= 7200;

    private static bool IsField(string key, string field) =>
        key.Equals(field, StringComparison.OrdinalIgnoreCase) ||
        key.EndsWith($".{field}", StringComparison.OrdinalIgnoreCase) ||
        key.EndsWith($"/{field}", StringComparison.OrdinalIgnoreCase);

    private static bool KeyMatches(string key, string alias)
    {
        return key.Equals(alias, StringComparison.OrdinalIgnoreCase) ||
               key.EndsWith($".{alias}", StringComparison.OrdinalIgnoreCase) ||
               key.EndsWith($"/{alias}", StringComparison.OrdinalIgnoreCase) ||
               key.Contains(alias, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseJson(string payload, out JsonDocument document)
    {
        try
        {
            document = JsonDocument.Parse(payload);
            return true;
        }
        catch (JsonException)
        {
            document = null!;
            return false;
        }
    }
}
