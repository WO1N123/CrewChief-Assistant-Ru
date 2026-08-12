using CrewChiefRUAssistant.Intent;
using CrewChiefRUAssistant.Telemetry;

namespace CrewChiefRUAssistant.Responses;

public sealed class ResponseComposer
{
    private readonly TelemetryStore _store;

    public ResponseComposer(TelemetryStore store)
    {
        _store = store;
    }

    public AssistantResponse Compose(IntentResult intent)
    {
        return intent.Kind switch
        {
            IntentKind.RadioCheck => new AssistantResponse(
                IntentKind.RadioCheck,
                "Да, слышу тебя хорошо."),

            IntentKind.FuelLevel => FuelLevel(),
            IntentKind.FuelCapacity => FuelCapacity(),
            IntentKind.FuelConsumption => FuelConsumption(),
            IntentKind.FuelLapsRemaining => FuelLapsRemaining(),
            IntentKind.FuelToFinish => FuelToFinish(),
            IntentKind.FuelToAdd => FuelToAdd(),
            IntentKind.PitNeed => PitNeed(),

            IntentKind.Position => Position(classPosition: false),
            IntentKind.ClassPosition => Position(classPosition: true),
            IntentKind.CarClass => CarClass(),
            IntentKind.CarsCount => CarsCount(),

            IntentKind.LapsRemaining => LapsRemaining(),
            IntentKind.CompletedLaps => CompletedLaps(),
            IntentKind.LeaderCompletedLaps => LeaderCompletedLaps(),
            IntentKind.TimeRemaining => TimeRemaining(),
            IntentKind.CurrentLapNumber => CurrentLapNumber(),
            IntentKind.CurrentLap => CurrentLap(),
            IntentKind.LastLap => LapTime(best: false),
            IntentKind.BestLap => LapTime(best: true),
            IntentKind.AverageLap => AverageLap(),
            IntentKind.CurrentSector => CurrentSector(),
            IntentKind.SectorTimes => SectorTimes(),
            IntentKind.LastLapValidity => LastLapValidity(),
            IntentKind.LastLapStatus => LastLapStatus(),

            IntentKind.GapAhead => Gap(ahead: true),
            IntentKind.GapBehind => Gap(ahead: false),
            IntentKind.CarAheadSpeed => OpponentSpeed(ahead: true),
            IntentKind.CarBehindSpeed => OpponentSpeed(ahead: false),
            IntentKind.IncidentAhead => NearbyIncident(ahead: true),
            IntentKind.IncidentBehind => NearbyIncident(ahead: false),
            IntentKind.FlagStatus => FlagStatus(),
            IntentKind.IncidentStatus => IncidentStatus(),
            IntentKind.TrackInfo => TrackInfo(),
            IntentKind.TrackTemperature => EnvironmentTemperature(track: true),
            IntentKind.AirTemperature => EnvironmentTemperature(track: false),
            IntentKind.SessionInfo => SessionInfo(),
            IntentKind.BatteryLevel => BatteryLevel(),
            IntentKind.AbsSetting => DriverAidSetting(abs: true),
            IntentKind.TractionControlSetting => DriverAidSetting(abs: false),

            IntentKind.TyreTemperatures => TyreTemperatures(),
            IntentKind.TyrePressures => TyrePressures(),
            IntentKind.TyreWear => TyreWear(),
            IntentKind.TyreType => TyreType(),
            IntentKind.TyreSet => TyreSet(),
            IntentKind.BrakeTemperatures => BrakeTemperatures(),
            IntentKind.WheelStatus => WheelStatus(),

            IntentKind.Damage => Damage(),

            _ => new AssistantResponse(
                IntentKind.Unknown,
                "Я не понял вопрос. Попробуй спросить о топливе, пит-стопе, времени круга, секторах, позиции, флагах, шинах или тормозах.",
                "unknown")
        };
    }

    private AssistantResponse FuelLevel()
    {
        if (!TryFuel(out var fuel))
            return Unavailable(IntentKind.FuelLevel, "уровень топлива");

        return new AssistantResponse(
            IntentKind.FuelLevel,
            $"Осталось {Number(fuel, 1)} литра топлива.");
    }

    private AssistantResponse FuelCapacity()
    {
        if (!TryFuel(out var fuel) ||
            !_store.TryGetNumber(
                out var capacity,
                "FuelData.FuelCapacity",
                "FuelCapacity") ||
            capacity <= 0)
        {
            return Unavailable(IntentKind.FuelCapacity, "ёмкость бака");
        }

        var percent = Math.Clamp(fuel / capacity * 100, 0, 100);

        return new AssistantResponse(
            IntentKind.FuelCapacity,
            $"В баке {Number(fuel, 1)} из {Number(capacity, 1)} литров, {Math.Round(percent):0} процентов.");
    }

    private AssistantResponse FuelConsumption()
    {
        if (!TryFuelPerLap(out var usage))
            return Unavailable(IntentKind.FuelConsumption, "расход топлива на круг");

        return new AssistantResponse(
            IntentKind.FuelConsumption,
            $"Средний расход — {Number(usage, 2)} литра на круг.");
    }

    private AssistantResponse FuelLapsRemaining()
    {
        if (!TryFuel(out var fuel) || !TryFuelPerLap(out var usage) || usage <= 0.01)
            return Unavailable(IntentKind.FuelLapsRemaining, "запас топлива по кругам");

        var laps = fuel / usage;

        return new AssistantResponse(
            IntentKind.FuelLapsRemaining,
            $"Топлива хватит примерно на {Number(laps, 1)} круга.");
    }

    private AssistantResponse FuelToFinish()
    {
        if (!TryFuelPlan(out _, out _, out _, out _, out var margin))
            return Unavailable(IntentKind.FuelToFinish, "расчёт топлива до финиша");

        if (margin >= 0)
        {
            return new AssistantResponse(
                IntentKind.FuelToFinish,
                $"Да. С запасом примерно {Number(margin, 1)} литра.");
        }

        return new AssistantResponse(
            IntentKind.FuelToFinish,
            $"Нет. До финиша не хватает примерно {Number(-margin, 1)} литра.");
    }

    private AssistantResponse FuelToAdd()
    {
        if (!TryFuelPlan(out _, out _, out _, out _, out var margin))
            return Unavailable(IntentKind.FuelToAdd, "расчёт дозаправки");

        if (margin >= 0)
        {
            return new AssistantResponse(
                IntentKind.FuelToAdd,
                "Топлива хватает. Добавлять не нужно.");
        }

        return new AssistantResponse(
            IntentKind.FuelToAdd,
            $"До финиша нужно добавить примерно {Number(-margin, 1)} литра.");
    }

    private AssistantResponse PitNeed()
    {
        if (!TryFuelPlan(out _, out _, out _, out _, out var margin))
            return Unavailable(IntentKind.PitNeed, "расчёт необходимости пит-стопа");

        if (margin >= 0)
        {
            return new AssistantResponse(
                IntentKind.PitNeed,
                $"Пит-стоп по топливу пока не нужен. Запас {Number(margin, 1)} литра.");
        }

        return new AssistantResponse(
            IntentKind.PitNeed,
            $"Пит-стоп по топливу нужен. Не хватает примерно {Number(-margin, 1)} литра.");
    }

    private AssistantResponse Position(bool classPosition)
    {
        var aliases = classPosition
            ? new[] { "SessionData.ClassPosition", "ClassPosition" }
            : new[]
            {
                "SessionData.OverallPosition",
                "SessionData.Position",
                "OverallPosition",
                "Position"
            };

        if (!_store.TryGetNumber(out var position, aliases))
        {
            return Unavailable(
                classPosition ? IntentKind.ClassPosition : IntentKind.Position,
                classPosition ? "позицию в классе" : "текущую позицию");
        }

        var rounded = Math.Max(1, (int)Math.Round(position));
        var text = classPosition
            ? $"Ты на {rounded}-й позиции в классе."
            : $"Ты на {rounded}-й позиции.";

        return new AssistantResponse(
            classPosition ? IntentKind.ClassPosition : IntentKind.Position,
            text);
    }

    private AssistantResponse CarClass()
    {
        if (!_store.TryGetText(
                out var carClass,
                "CarClass",
                "carClass.carClassEnumString"))
        {
            return Unavailable(IntentKind.CarClass, "класс машины");
        }

        return new AssistantResponse(
            IntentKind.CarClass,
            $"Класс машины — {ReadableEnum(carClass)}.");
    }

    private AssistantResponse CarsCount()
    {
        var hasOverall = _store.TryGetNumber(
            out var overall,
            "SessionData.NumCarsOverall",
            "NumCarsOverall");

        var hasClass = _store.TryGetNumber(
            out var inClass,
            "SessionData.NumCarsInPlayerClass",
            "NumCarsInPlayerClass");

        if (!hasOverall && !hasClass)
            return Unavailable(IntentKind.CarsCount, "количество машин");

        if (hasOverall && hasClass)
        {
            return new AssistantResponse(
                IntentKind.CarsCount,
                $"В сессии {(int)Math.Round(overall)} машин, в классе {(int)Math.Round(inClass)} машин.");
        }

        var value = hasOverall ? overall : inClass;
        var label = hasOverall ? "В сессии" : "В классе";

        return new AssistantResponse(
            IntentKind.CarsCount,
            $"{label} {(int)Math.Round(value)} машин.");
    }

    private AssistantResponse LapsRemaining()
    {
        if (!TryLapsRemaining(out var laps))
            return Unavailable(IntentKind.LapsRemaining, "оставшиеся круги");

        var rounded = Math.Max(0, (int)Math.Ceiling(laps));
        return new AssistantResponse(
            IntentKind.LapsRemaining,
            $"Осталось примерно {rounded} {PluralWord(rounded, "круг", "круга", "кругов")}.");
    }

    private AssistantResponse CompletedLaps()
    {
        if (!_store.TryGetNumber(
                out var laps,
                "SessionData.CompletedLaps",
                "CompletedLaps"))
        {
            return Unavailable(IntentKind.CompletedLaps, "пройденные круги");
        }

        var rounded = Math.Max(0, (int)Math.Round(laps));
        return new AssistantResponse(
            IntentKind.CompletedLaps,
            $"Пройдено {rounded} {PluralWord(rounded, "круг", "круга", "кругов")}.");
    }

    private AssistantResponse LeaderCompletedLaps()
    {
        if (_store.TryGetNumber(out var total,
                "SessionData.SessionNumberOfLaps", "SessionNumberOfLaps") &&
            _store.TryGetNumber(out var remaining,
                "SessionData.SessionLapsRemaining", "SessionLapsRemaining") &&
            total > 0 && remaining >= 0 && remaining <= total)
        {
            var completed = Math.Max(0, (int)Math.Round(total - remaining));
            return new AssistantResponse(
                IntentKind.LeaderCompletedLaps,
                $"Лидер проехал {completed} {PluralWord(completed, "круг", "круга", "кругов")}.");
        }

        if (_store.TryGetNumber(out var position,
                "SessionData.OverallPosition", "OverallPosition") &&
            position <= 1.5 &&
            _store.TryGetNumber(out var ownLaps,
                "SessionData.CompletedLaps", "CompletedLaps"))
        {
            var completed = Math.Max(0, (int)Math.Round(ownLaps));
            return new AssistantResponse(
                IntentKind.LeaderCompletedLaps,
                $"Ты лидер. Пройдено {completed} {PluralWord(completed, "круг", "круга", "кругов")}.");
        }

        return Unavailable(IntentKind.LeaderCompletedLaps, "круги лидера");
    }

    private AssistantResponse TimeRemaining()
    {
        if (!_store.TryGetNumber(
                out var seconds,
                "SessionData.SessionTimeRemaining",
                "SessionTimeRemaining",
                "TimeRemaining") ||
            seconds < 0 ||
            seconds > 604800)
        {
            return Unavailable(IntentKind.TimeRemaining, "оставшееся время");
        }

        return new AssistantResponse(
            IntentKind.TimeRemaining,
            $"Осталось {Duration(seconds)}.");
    }

    private AssistantResponse CurrentLapNumber()
    {
        if (!_store.TryGetNumber(
                out var lap,
                "SessionData.LapCount",
                "LapCount",
                "CurrentLap") ||
            lap < 1)
        {
            return Unavailable(IntentKind.CurrentLapNumber, "номер текущего круга");
        }

        return new AssistantResponse(
            IntentKind.CurrentLapNumber,
            $"Сейчас {(int)Math.Round(lap)}-й круг.");
    }

    private AssistantResponse CurrentLap()
    {
        if (!_store.TryGetNumber(
                out var seconds,
                "SessionData.LapTimeCurrent",
                "SessionData.CurrentLapTime",
                "LapTimeCurrent",
                "CurrentLapTime") ||
            seconds < 0 ||
            seconds > 7200)
        {
            return Unavailable(IntentKind.CurrentLap, "время текущего круга");
        }

        return new AssistantResponse(
            IntentKind.CurrentLap,
            $"Текущий круг — {LapTimeText(seconds)}.");
    }

    private AssistantResponse LapTime(bool best)
    {
        var aliases = best
            ? new[]
            {
                "SessionData.PlayerLapTimeSessionBest",
                "SessionData.BestLapTime",
                "BestLapTime"
            }
            : new[]
            {
                "SessionData.LapTimePrevious",
                "SessionData.PreviousLapTime",
                "LapTimePrevious",
                "PreviousLapTime",
                "LastLapTime"
            };

        var hasMappedValue =
            _store.TryGetNumber(out var seconds, aliases) &&
            seconds > 0;

        if (!hasMappedValue)
        {
            var hasDerivedValue = best
                ? _store.TryGetBestLapTime(out seconds)
                : _store.TryGetLastLapTime(out seconds);

            if (!hasDerivedValue || seconds <= 0)
            {
                return Unavailable(
                    best ? IntentKind.BestLap : IntentKind.LastLap,
                    best ? "лучший круг" : "последний круг");
            }
        }

        return new AssistantResponse(
            best ? IntentKind.BestLap : IntentKind.LastLap,
            $"{(best ? "Лучший" : "Последний")} круг — {LapTimeText(seconds)}.");
    }

    private AssistantResponse AverageLap()
    {
        if (!_store.TryGetAverageLapTime(out var seconds) || seconds <= 0)
            return Unavailable(IntentKind.AverageLap, "среднее время круга");

        return new AssistantResponse(
            IntentKind.AverageLap,
            $"Среднее время круга — {LapTimeText(seconds)}.");
    }

    private AssistantResponse CurrentSector()
    {
        if (!_store.TryGetNumber(
                out var sector,
                "SessionData.SectorNumber",
                "SectorNumber") ||
            sector < 1)
        {
            return Unavailable(IntentKind.CurrentSector, "номер сектора");
        }

        return new AssistantResponse(
            IntentKind.CurrentSector,
            $"Сейчас {(int)Math.Round(sector)}-й сектор.");
    }

    private AssistantResponse SectorTimes()
    {
        var times = new List<double>();

        foreach (var aliases in new[]
        {
            new[] { "SessionData.LastSector1Time", "LastSector1Time" },
            new[] { "SessionData.LastSector2Time", "LastSector2Time" },
            new[] { "SessionData.LastSector3Time", "LastSector3Time" }
        })
        {
            if (_store.TryGetNumber(out var value, aliases) && value > 0)
                times.Add(value);
        }

        if (times.Count == 0)
            return Unavailable(IntentKind.SectorTimes, "времена секторов");

        return new AssistantResponse(
            IntentKind.SectorTimes,
            $"Сектора прошлого круга: {string.Join(", ", times.Select(LapTimeText))}.");
    }

    private AssistantResponse LastLapValidity()
    {
        if (!_store.TryGetBoolean(
                out var valid,
                "SessionData.PreviousLapWasValid",
                "PreviousLapWasValid"))
        {
            return Unavailable(IntentKind.LastLapValidity, "валидность прошлого круга");
        }

        return new AssistantResponse(
            IntentKind.LastLapValidity,
            valid
                ? "Последний круг зачётный."
                : "Последний круг недействительный.");
    }

    private AssistantResponse LastLapStatus()
    {
        if (!_store.TryGetBoolean(
                out var isLast,
                "SessionData.IsLastLap",
                "IsLastLap"))
        {
            return Unavailable(IntentKind.LastLapStatus, "признак последнего круга");
        }

        return new AssistantResponse(
            IntentKind.LastLapStatus,
            isLast
                ? "Это последний круг."
                : "Сейчас не последний круг.");
    }

    private AssistantResponse Gap(bool ahead)
    {
        var aliases = ahead
            ? new[]
            {
                "SessionData.TimeDeltaFront",
                "SessionData.GapInFront",
                "GapAhead",
                "TimeDeltaFront"
            }
            : new[]
            {
                "SessionData.TimeDeltaBehind",
                "SessionData.GapBehind",
                "GapBehind",
                "TimeDeltaBehind"
            };

        if (!_store.TryGetNumber(out var seconds, aliases))
        {
            return Unavailable(
                ahead ? IntentKind.GapAhead : IntentKind.GapBehind,
                ahead ? "отрыв до машины впереди" : "отрыв до машины сзади");
        }

        if (Math.Abs(seconds) < 0.005)
        {
            if (ahead &&
                _store.TryGetNumber(
                    out var position,
                    "SessionData.OverallPosition",
                    "SessionData.Position",
                    "OverallPosition",
                    "Position") &&
                position <= 1.5)
            {
                return new AssistantResponse(
                    IntentKind.GapAhead,
                    "Ты лидер. Машины впереди нет.");
            }

            return Unavailable(
                ahead ? IntentKind.GapAhead : IntentKind.GapBehind,
                ahead ? "отрыв до машины впереди" : "отрыв до машины сзади");
        }

        return new AssistantResponse(
            ahead ? IntentKind.GapAhead : IntentKind.GapBehind,
            $"Отрыв {(ahead ? "впереди" : "сзади")} — {Number(Math.Abs(seconds), 2)} секунды.");
    }

    private AssistantResponse OpponentSpeed(bool ahead)
    {
        // CrewChief's generic mapped game state exposes the time gap, but does
        // not expose a stable generic speed field for the particular car ahead
        // or behind. Do not answer this question with the gap by mistake.
        return Unavailable(
            ahead ? IntentKind.CarAheadSpeed : IntentKind.CarBehindSpeed,
            ahead ? "скорость машины впереди" : "скорость машины сзади");
    }

    private AssistantResponse NearbyIncident(bool ahead)
    {
        if (!_store.TryGetNumber(out var distance,
                "FlagData.distanceToNearestIncident",
                "DistanceToNearestIncident") ||
            Math.Abs(distance + 1) < 0.01)
        {
            return Unavailable(
                ahead ? IntentKind.IncidentAhead : IntentKind.IncidentBehind,
                ahead ? "аварию впереди" : "аварию сзади");
        }

        if (ahead && distance > 0)
        {
            var metres = Math.Max(0, (int)Math.Round(distance));
            return new AssistantResponse(
                IntentKind.IncidentAhead,
                $"Авария впереди примерно через {metres} {PluralWord(metres, "метр", "метра", "метров")}.");
        }

        if (!ahead && distance < -1)
        {
            var metres = Math.Max(0, (int)Math.Round(-distance));
            return new AssistantResponse(
                IntentKind.IncidentBehind,
                $"Авария позади примерно в {metres} {PluralWord(metres, "метре", "метрах", "метрах")}.");
        }

        return new AssistantResponse(
            ahead ? IntentKind.IncidentAhead : IntentKind.IncidentBehind,
            ahead ? "Ближайшая авария уже позади." : "Ближайшая авария находится впереди.",
            ahead ? "incident_is_behind" : "incident_is_ahead");
    }

    private AssistantResponse FlagStatus()
    {
        if (!_store.TryGetText(
                out var flag,
                "SessionData.Flag",
                "Flag"))
        {
            return Unavailable(IntentKind.FlagStatus, "текущий флаг");
        }

        return new AssistantResponse(
            IntentKind.FlagStatus,
            $"Флаг — {ReadableFlag(flag)}.");
    }

    private AssistantResponse IncidentStatus()
    {
        var hasCurrent = _store.TryGetNumber(
            out var current,
            "SessionData.CurrentIncidentCount",
            "SessionData.CurrentDriverIncidentCount",
            "CurrentIncidentCount",
            "CurrentDriverIncidentCount");

        var hasMax = _store.TryGetNumber(
            out var maximum,
            "SessionData.MaxIncidentCount",
            "MaxIncidentCount");

        if (!hasCurrent)
            return Unavailable(IntentKind.IncidentStatus, "количество инцидентов");

        var currentCount = Math.Max(0, (int)Math.Round(current));

        if (hasMax && maximum > 0)
        {
            var maxCount = Math.Max(0, (int)Math.Round(maximum));
            var remaining = Math.Max(0, maxCount - currentCount);

            return new AssistantResponse(
                IntentKind.IncidentStatus,
                $"Инцидентов: {currentCount} из {maxCount}. Осталось {remaining}.");
        }

        return new AssistantResponse(
            IntentKind.IncidentStatus,
            $"Инцидентов: {currentCount}.");
    }

    private AssistantResponse TrackInfo()
    {
        var hasName = _store.TryGetText(
            out var name,
            "SessionData.TrackDefinition.name",
            "TrackName");

        var hasLength = _store.TryGetNumber(
            out var length,
            "SessionData.TrackDefinition.trackLength",
            "TrackLength");

        if (!hasName && !hasLength)
            return Unavailable(IntentKind.TrackInfo, "данные трассы");

        if (hasName && hasLength && length > 0)
        {
            var kilometres = length > 100
                ? length / 1000.0
                : length;

            return new AssistantResponse(
                IntentKind.TrackInfo,
                $"Трасса — {ReadableEnum(name)}, длина {Number(kilometres, 2)} километра.");
        }

        if (hasName)
        {
            return new AssistantResponse(
                IntentKind.TrackInfo,
                $"Трасса — {ReadableEnum(name)}.");
        }

        return new AssistantResponse(
            IntentKind.TrackInfo,
            $"Длина трассы — {Number(length > 100 ? length / 1000.0 : length, 2)} километра.");
    }

    private AssistantResponse EnvironmentTemperature(bool track)
    {
        var aliases = track
            ? new[]
            {
                "Conditions.CurrentConditions.TrackTemperature",
                "Conditions.TrackTemperature",
                "TrackTemperature",
                "TrackTemp"
            }
            : new[]
            {
                "Conditions.CurrentConditions.AmbientTemperature",
                "Conditions.AmbientTemperature",
                "Conditions.AirTemperature",
                "AmbientTemperature",
                "AirTemperature",
                "AirTemp"
            };

        if (!_store.TryGetNumber(out var value, aliases) ||
            value < -100 ||
            value > 150)
        {
            return Unavailable(
                track ? IntentKind.TrackTemperature : IntentKind.AirTemperature,
                track ? "температуру трассы" : "температуру воздуха");
        }

        var rounded = (int)Math.Round(value);

        return new AssistantResponse(
            track ? IntentKind.TrackTemperature : IntentKind.AirTemperature,
            $"Температура {(track ? "трассы" : "воздуха")} — {rounded} {PluralWord(Math.Abs(rounded), "градус", "градуса", "градусов")}.");
    }

    private AssistantResponse SessionInfo()
    {
        var hasType = _store.TryGetText(
            out var type,
            "SessionData.SessionType",
            "SessionType");

        var hasPhase = _store.TryGetText(
            out var phase,
            "SessionData.SessionPhase",
            "SessionPhase");

        if (!hasType && !hasPhase)
            return Unavailable(IntentKind.SessionInfo, "тип и фазу сессии");

        if (hasType && hasPhase)
        {
            return new AssistantResponse(
                IntentKind.SessionInfo,
                $"Сессия — {ReadableSessionType(type)}, фаза — {ReadableSessionPhase(phase)}.");
        }

        return new AssistantResponse(
            IntentKind.SessionInfo,
            hasType
                ? $"Сессия — {ReadableSessionType(type)}."
                : $"Фаза сессии — {ReadableSessionPhase(phase)}.");
    }

    private AssistantResponse BatteryLevel()
    {
        if (!_store.TryGetNumber(out var value,
                "BatteryData.BatteryPercentageLeft",
                "BatteryPercentageLeft",
                "BatteryLevel") || value < 0)
        {
            return Unavailable(IntentKind.BatteryLevel, "заряд батареи");
        }

        if (value <= 1.5)
            value *= 100;

        var percent = Math.Clamp((int)Math.Round(value), 0, 100);
        return new AssistantResponse(
            IntentKind.BatteryLevel,
            $"Заряд батареи — {percent} {PluralWord(percent, "процент", "процента", "процентов")}.");
    }

    private AssistantResponse DriverAidSetting(bool abs)
    {
        var aliases = abs
            ? new[] { "ABSLevel", "AbsLevel", "AntiLockBrakesLevel" }
            : new[] { "TractionControlLevel", "TCLevel", "TcLevel" };

        if (!_store.TryGetNumber(out var value, aliases))
        {
            return Unavailable(
                abs ? IntentKind.AbsSetting : IntentKind.TractionControlSetting,
                abs ? "настройку ABS" : "настройку трекшн-контроля");
        }

        var level = Math.Max(0, (int)Math.Round(value));
        return new AssistantResponse(
            abs ? IntentKind.AbsSetting : IntentKind.TractionControlSetting,
            $"{(abs ? "ABS" : "Трекшн-контроль")} — уровень {level}.");
    }

    private AssistantResponse TyreTemperatures()
    {
        var values = ReadCornerValues(
            ("передняя левая шина", new[]
            {
                "TyreFLTemp",
                "TyreData.FrontLeft_CenterTemp",
                "TyreData.PeakFrontLeftTemperatureForLap"
            }),
            ("передняя правая шина", new[]
            {
                "TyreFRTemp",
                "TyreData.FrontRight_CenterTemp",
                "TyreData.PeakFrontRightTemperatureForLap"
            }),
            ("задняя левая шина", new[]
            {
                "TyreRLTemp",
                "TyreData.RearLeft_CenterTemp",
                "TyreData.PeakRearLeftTemperatureForLap"
            }),
            ("задняя правая шина", new[]
            {
                "TyreRRTemp",
                "TyreData.RearRight_CenterTemp",
                "TyreData.PeakRearRightTemperatureForLap"
            }));

        if (values.Count == 0)
            return Unavailable(IntentKind.TyreTemperatures, "температуры шин");

        return new AssistantResponse(
            IntentKind.TyreTemperatures,
            $"Температуры шин: {string.Join("; ", values.Select(
                value =>
                    $"{value.Label} — {NumberWhole(value.Value)} {PluralWord((int)Math.Round(value.Value), "градус", "градуса", "градусов")}"))}.");
    }

    private AssistantResponse TyrePressures()
    {
        var values = ReadCornerValues(
            ("передняя левая шина", new[]
            {
                "TyreFLPressure",
                "TyreData.FrontLeftPressure"
            }),
            ("передняя правая шина", new[]
            {
                "TyreFRPressure",
                "TyreData.FrontRightPressure"
            }),
            ("задняя левая шина", new[]
            {
                "TyreRLPressure",
                "TyreData.RearLeftPressure"
            }),
            ("задняя правая шина", new[]
            {
                "TyreRRPressure",
                "TyreData.RearRightPressure"
            }));

        if (values.Count == 0)
            return Unavailable(IntentKind.TyrePressures, "давление шин");

        return new AssistantResponse(
            IntentKind.TyrePressures,
            $"Давление шин: {string.Join("; ", values.Select(
                value =>
                    $"{value.Label} — {Number(value.Value, 1)}"))}.");
    }

    private AssistantResponse TyreWear()
    {
        var values = ReadCornerValues(
            ("передняя левая шина", new[]
            {
                "TyreFLWear",
                "TyreData.FrontLeftPercentWear"
            }),
            ("передняя правая шина", new[]
            {
                "TyreFRWear",
                "TyreData.FrontRightPercentWear"
            }),
            ("задняя левая шина", new[]
            {
                "TyreRLWear",
                "TyreData.RearLeftPercentWear"
            }),
            ("задняя правая шина", new[]
            {
                "TyreRRWear",
                "TyreData.RearRightPercentWear"
            }));

        if (values.Count == 0)
            return Unavailable(IntentKind.TyreWear, "износ шин");

        var percentages = values
            .Select(value =>
                new CornerReading(
                    value.Label,
                    Math.Clamp(
                        value.Value <= 1.5
                            ? value.Value * 100
                            : value.Value,
                        0,
                        100)))
            .ToArray();

        return new AssistantResponse(
            IntentKind.TyreWear,
            $"Износ шин: {string.Join("; ", percentages.Select(
                value =>
                    $"{value.Label} — {Math.Round(value.Value):0}%"))}.");
    }

    private AssistantResponse TyreType()
    {
        if (!_store.TryGetText(
                out var type,
                "TyreData.TyreTypeName",
                "TyreType"))
        {
            return Unavailable(IntentKind.TyreType, "тип шин");
        }

        return new AssistantResponse(
            IntentKind.TyreType,
            $"Тип шин — {ReadableTyreType(type)}.");
    }

    private AssistantResponse TyreSet()
    {
        if (!_store.TryGetNumber(
                out var set,
                "TyreData.fittedSet",
                "TyreData.selectedSet",
                "TyreSet"))
        {
            return Unavailable(IntentKind.TyreSet, "номер комплекта шин");
        }

        return new AssistantResponse(
            IntentKind.TyreSet,
            $"Установлен комплект шин номер {Math.Max(0, (int)Math.Round(set))}.");
    }

    private AssistantResponse BrakeTemperatures()
    {
        var values = ReadCornerValues(
            ("передний левый тормоз", new[]
            {
                "BrakeFLTemp",
                "TyreData.LeftFrontBrakeTemp"
            }),
            ("передний правый тормоз", new[]
            {
                "BrakeFRTemp",
                "TyreData.RightFrontBrakeTemp"
            }),
            ("задний левый тормоз", new[]
            {
                "BrakeRLTemp",
                "TyreData.LeftRearBrakeTemp"
            }),
            ("задний правый тормоз", new[]
            {
                "BrakeRRTemp",
                "TyreData.RightRearBrakeTemp"
            }));

        if (values.Count == 0)
            return Unavailable(IntentKind.BrakeTemperatures, "температуры тормозов");

        return new AssistantResponse(
            IntentKind.BrakeTemperatures,
            $"Температуры тормозов: {string.Join("; ", values.Select(
                value =>
                    $"{value.Label} — {NumberWhole(value.Value)} {PluralWord((int)Math.Round(value.Value), "градус", "градуса", "градусов")}"))}.");
    }

    private AssistantResponse WheelStatus()
    {
        var hasAny = false;
        var issues = new List<string>();

        ReadWheelFlag(ref hasAny, issues, "блокировка переднего левого", true,
            "TyreData.LeftFrontIsLocked", "LeftFrontIsLocked");
        ReadWheelFlag(ref hasAny, issues, "блокировка переднего правого", true,
            "TyreData.RightFrontIsLocked", "RightFrontIsLocked");
        ReadWheelFlag(ref hasAny, issues, "блокировка заднего левого", true,
            "TyreData.LeftRearIsLocked", "LeftRearIsLocked");
        ReadWheelFlag(ref hasAny, issues, "блокировка заднего правого", true,
            "TyreData.RightRearIsLocked", "RightRearIsLocked");

        ReadWheelFlag(ref hasAny, issues, "пробуксовка переднего левого", true,
            "TyreData.LeftFrontIsSpinning", "LeftFrontIsSpinning");
        ReadWheelFlag(ref hasAny, issues, "пробуксовка переднего правого", true,
            "TyreData.RightFrontIsSpinning", "RightFrontIsSpinning");
        ReadWheelFlag(ref hasAny, issues, "пробуксовка заднего левого", true,
            "TyreData.LeftRearIsSpinning", "LeftRearIsSpinning");
        ReadWheelFlag(ref hasAny, issues, "пробуксовка заднего правого", true,
            "TyreData.RightRearIsSpinning", "RightRearIsSpinning");

        ReadWheelFlag(ref hasAny, issues, "нет переднего левого колеса", false,
            "TyreData.LeftFrontAttached", "LeftFrontAttached");
        ReadWheelFlag(ref hasAny, issues, "нет переднего правого колеса", false,
            "TyreData.RightFrontAttached", "RightFrontAttached");
        ReadWheelFlag(ref hasAny, issues, "нет заднего левого колеса", false,
            "TyreData.LeftRearAttached", "LeftRearAttached");
        ReadWheelFlag(ref hasAny, issues, "нет заднего правого колеса", false,
            "TyreData.RightRearAttached", "RightRearAttached");

        if (!hasAny)
            return Unavailable(IntentKind.WheelStatus, "состояние колёс");

        if (issues.Count == 0)
        {
            return new AssistantResponse(
                IntentKind.WheelStatus,
                "Блокировок, пробуксовки и оторванных колёс нет.");
        }

        return new AssistantResponse(
            IntentKind.WheelStatus,
            $"Проблемы с колёсами: {string.Join("; ", issues)}.");
    }

    private AssistantResponse Damage()
    {
        var parts = new List<string>();

        AddDamageLevel(
            parts,
            "двигатель",
            "CarDamageData.OverallEngineDamage",
            "DamageData.EngineDamage",
            "EngineDamage");

        AddDamageLevel(
            parts,
            "аэродинамика",
            "CarDamageData.OverallAeroDamage",
            "DamageData.OverallAeroDamage",
            "OverallAeroDamage",
            "AeroDamage");

        AddDamageLevel(
            parts,
            "трансмиссия",
            "CarDamageData.OverallTransmissionDamage",
            "OverallTransmissionDamage",
            "TransmissionDamage");

        if (parts.Count == 0)
            return Unavailable(IntentKind.Damage, "данные о повреждениях");

        return new AssistantResponse(
            IntentKind.Damage,
            $"Повреждения: {string.Join(", ", parts)}.");
    }

    private void AddDamageLevel(
        ICollection<string> parts,
        string component,
        params string[] aliases)
    {
        if (!_store.TryGetText(out var raw, aliases))
            return;

        var description = DamageLevelDescription(raw);
        if (description is null)
            return;

        parts.Add($"{component} — {description}");
    }

    private static string? DamageLevelDescription(string raw)
    {
        var normalized = raw.Trim().ToUpperInvariant();
        return normalized switch
        {
            "1" or "NONE" => "без повреждений",
            "2" or "TRIVIAL" => "незначительные повреждения",
            "3" or "MINOR" => "лёгкие повреждения",
            "4" or "MAJOR" => "серьёзные повреждения",
            "5" or "DESTROYED" => "критические повреждения",
            "0" or "UNKNOWN" or "" => null,
            _ when double.TryParse(
                raw,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var numeric) => $"{DamagePercent(numeric)}",
            _ => ReadableEnum(raw).ToLowerInvariant()
        };
    }

    private sealed record CornerReading(
        string Label,
        double Value);

    private List<CornerReading> ReadCornerValues(
        params (string Label, string[] Aliases)[] corners)
    {
        var result = new List<CornerReading>(
            corners.Length);

        foreach (var corner in corners)
        {
            if (_store.TryGetNumber(
                    out var value,
                    corner.Aliases))
            {
                result.Add(
                    new CornerReading(
                        corner.Label,
                        value));
            }
        }

        return result;
    }

    private void ReadWheelFlag(
        ref bool hasAny,
        ICollection<string> issues,
        string issue,
        bool expectedValue,
        params string[] aliases)
    {
        if (!_store.TryGetBoolean(out var value, aliases))
            return;

        hasAny = true;

        if (value == expectedValue)
            issues.Add(issue);
    }

    private bool TryFuel(out double fuel) =>
        _store.TryGetNumber(
            out fuel,
            "FuelData.FuelLeft",
            "FuelData.FuelRemaining",
            "FuelLeft",
            "FuelRemaining",
            "FuelLevel");

    private bool TryFuelPerLap(out double usage)
    {
        if (_store.TryGetNumber(
                out usage,
                "FuelData.AverageUsagePerLap",
                "FuelData.FuelUsePerLap",
                "FuelData.FuelConsumptionPerLap",
                "AverageUsagePerLap",
                "FuelUsePerLap") &&
            usage > 0.01)
        {
            return true;
        }

        return _store.TryGetAverageFuelPerLap(out usage) &&
               usage > 0.01;
    }

    private bool TryFuelPlan(
        out double fuel,
        out double usage,
        out double laps,
        out double required,
        out double margin)
    {
        fuel = default;
        usage = default;
        laps = default;
        required = default;
        margin = default;

        if (!TryFuel(out fuel))
            return false;

        if (!TryFuelPerLap(out usage) || usage <= 0.01)
            return false;

        if (!TryLapsRemaining(out laps) || laps < 0)
            return false;

        required = laps * usage * 1.05;
        margin = fuel - required;
        return true;
    }

    private bool TryLapsRemaining(out double laps)
    {
        if (_store.TryGetNumber(
                out var direct,
                "SessionData.SessionLapsRemaining",
                "SessionData.LapsRemaining",
                "SessionLapsRemaining",
                "LapsRemaining") &&
            direct >= 0 &&
            direct < 100000)
        {
            laps = direct;
            return true;
        }

        if (_store.TryGetNumber(
                out var total,
                "SessionData.SessionNumberOfLaps",
                "SessionData.TotalLaps",
                "TotalLaps") &&
            _store.TryGetNumber(
                out var completed,
                "SessionData.CompletedLaps",
                "CompletedLaps") &&
            total > 0)
        {
            laps = Math.Max(0, total - completed);
            return true;
        }

        if (_store.TryGetNumber(
                out var seconds,
                "SessionData.SessionTimeRemaining",
                "SessionTimeRemaining",
                "TimeRemaining") &&
            seconds >= 0 &&
            seconds < 604800 &&
            TryGetLapEstimate(out var estimatedLap) &&
            estimatedLap > 10)
        {
            laps = Math.Max(
                0,
                Math.Ceiling(seconds / estimatedLap) + 1);

            return true;
        }

        laps = default;
        return false;
    }

    private bool TryGetLapEstimate(
        out double lapTime)
    {
        if (_store.TryGetAverageLapTime(out lapTime) &&
            lapTime > 10)
        {
            return true;
        }

        if (_store.TryGetLastLapTime(out lapTime) &&
            lapTime > 10)
        {
            return true;
        }

        if (_store.TryGetBestLapTime(out lapTime) &&
            lapTime > 10)
        {
            return true;
        }

        if (_store.TryGetNumber(
                out lapTime,
                "SessionData.LapTimePrevious",
                "SessionData.PreviousLapTime",
                "LapTimePrevious",
                "PreviousLapTime",
                "LastLapTime") &&
            lapTime > 10)
        {
            return true;
        }

        return _store.TryGetNumber(
                   out lapTime,
                   "SessionData.PlayerLapTimeSessionBest",
                   "SessionData.BestLapTime",
                   "BestLapTime") &&
               lapTime > 10;
    }

    private static AssistantResponse Unavailable(IntentKind intent, string field) =>
        new(intent, $"Сейчас CrewChief не передаёт {field}.", "unavailable");

    private static string Number(double value, int decimals) =>
        value.ToString($"F{decimals}", System.Globalization.CultureInfo.GetCultureInfo("ru-RU"));

    private static string NumberWhole(double value) =>
        Math.Round(value).ToString("0", System.Globalization.CultureInfo.InvariantCulture);

    private static string PluralWord(
        int value,
        string one,
        string few,
        string many)
    {
        var absolute = Math.Abs(value);
        var lastTwo = absolute % 100;

        if (lastTwo is >= 11 and <= 14)
            return many;

        return (absolute % 10) switch
        {
            1 => one,
            2 or 3 or 4 => few,
            _ => many
        };
    }

    private static string Duration(double seconds)
    {
        seconds = Math.Max(0, seconds);
        var span = TimeSpan.FromSeconds(seconds);

        if (span.TotalHours >= 1)
            return $"{(int)span.TotalHours} ч {span.Minutes} мин";

        if (span.TotalMinutes >= 1)
            return $"{span.Minutes} мин {span.Seconds} сек";

        return $"{span.Seconds} сек";
    }

    private static string LapTimeText(double seconds)
    {
        var span = TimeSpan.FromSeconds(seconds);
        return span.TotalMinutes >= 1
            ? $"{(int)span.TotalMinutes}:{span.Seconds:00}.{span.Milliseconds:000}"
            : $"{span.Seconds}.{span.Milliseconds:000}";
    }

    private static string DamagePercent(double value)
    {
        var percent = value <= 1.5 ? value * 100 : value;
        return $"{Math.Clamp(Math.Round(percent), 0, 100):0}%";
    }

    private static string ReadableEnum(string value) =>
        value.Replace('_', ' ').Trim();

    private static string ReadableFlag(string value)
    {
        var normalized = value.ToUpperInvariant();

        if (normalized.Contains("DOUBLE")) return "двойной жёлтый";
        if (normalized.Contains("GREEN")) return "зелёный";
        if (normalized.Contains("YELLOW")) return "жёлтый";
        if (normalized.Contains("BLUE")) return "синий";
        if (normalized.Contains("RED")) return "красный";
        if (normalized.Contains("WHITE")) return "белый";
        if (normalized.Contains("BLACK")) return "чёрный";
        if (normalized.Contains("CHEQUER")) return "клетчатый";

        return ReadableEnum(value).ToLowerInvariant();
    }

    private static string ReadableSessionType(string value)
    {
        var normalized = value.ToUpperInvariant();

        if (normalized.Contains("PRACT")) return "практика";
        if (normalized.Contains("QUAL")) return "квалификация";
        if (normalized.Contains("RACE")) return "гонка";
        if (normalized.Contains("HOTLAP")) return "быстрый круг";
        if (normalized.Contains("TIME")) return "заезд на время";

        return ReadableEnum(value).ToLowerInvariant();
    }

    private static string ReadableSessionPhase(string value)
    {
        var normalized = value.ToUpperInvariant();

        if (normalized.Contains("GREEN")) return "зелёная";
        if (normalized.Contains("COUNT")) return "обратный отсчёт";
        if (normalized.Contains("FORMATION")) return "формировочный круг";
        if (normalized.Contains("CHECK")) return "финиш";
        if (normalized.Contains("FINISH")) return "завершена";
        if (normalized.Contains("GARAGE")) return "гараж";

        return ReadableEnum(value).ToLowerInvariant();
    }

    private static string ReadableTyreType(string value)
    {
        var normalized = value.ToUpperInvariant();

        if (normalized.Contains("SOFT")) return "софт";
        if (normalized.Contains("MEDIUM")) return "медиум";
        if (normalized.Contains("HARD")) return "хард";
        if (normalized.Contains("INTER")) return "промежуточные";
        if (normalized.Contains("WET")) return "дождевые";

        return ReadableEnum(value).ToLowerInvariant();
    }
}
