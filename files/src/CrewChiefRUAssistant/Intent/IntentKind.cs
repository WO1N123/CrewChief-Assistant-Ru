namespace CrewChiefRUAssistant.Intent;

public enum IntentKind
{
    Unknown,
    RadioCheck,

    FuelLevel,
    FuelCapacity,
    FuelConsumption,
    FuelLapsRemaining,
    FuelToFinish,
    FuelToAdd,
    PitNeed,

    Position,
    ClassPosition,
    CarClass,
    CarsCount,

    LapsRemaining,
    CompletedLaps,
    LeaderCompletedLaps,
    TimeRemaining,
    CurrentLapNumber,
    CurrentLap,
    LastLap,
    BestLap,
    AverageLap,
    CurrentSector,
    SectorTimes,
    LastLapValidity,
    LastLapStatus,

    GapAhead,
    GapBehind,
    CarAheadSpeed,
    CarBehindSpeed,
    IncidentAhead,
    IncidentBehind,
    FlagStatus,
    IncidentStatus,
    TrackInfo,
    TrackTemperature,
    AirTemperature,
    SessionInfo,
    BatteryLevel,
    AbsSetting,
    TractionControlSetting,

    TyreTemperatures,
    TyrePressures,
    TyreWear,
    TyreType,
    TyreSet,
    BrakeTemperatures,
    WheelStatus,

    Damage
}
