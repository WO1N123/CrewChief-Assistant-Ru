using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CrewChiefRUAssistant.Shared;

public sealed record CrewChiefMqttConfigurationResult(
    bool Found,
    bool Changed,
    string? ConfigPath,
    int AddedChannels,
    string Message);

public static class CrewChiefMqttConfigurator
{
    private sealed record ChannelDefinition(
        string CrewChiefField,
        string TelemetryField);

    private static readonly ChannelDefinition[] Channels =
    [
        new("FuelData.FuelCapacity", "FuelCapacity"),
        new("FuelData.FuelLeft", "FuelLeft"),
        new("FuelData.FuelUseActive", "FuelUseActive"),
        new("BatteryData.BatteryPercentageLeft", "BatteryPercentageLeft"),
        new("BatteryData.BatteryUseActive", "BatteryUseActive"),

        new("SessionData.OverallPosition", "OverallPosition"),
        new("SessionData.ClassPosition", "ClassPosition"),
        new("SessionData.LapTimeCurrent", "CurrentLapTime"),
        new("SessionData.LapTimePrevious", "LapTimePrevious"),
        new("SessionData.PlayerLapTimeSessionBest", "BestLapTime"),
        new("SessionData.SessionTimeRemaining", "SessionTimeRemaining"),
        new("SessionData.SessionLapsRemaining", "SessionLapsRemaining"),
        new("SessionData.TimeDeltaFront", "TimeDeltaFront"),
        new("SessionData.TimeDeltaBehind", "TimeDeltaBehind"),

        new("SessionData.CompletedLaps", "CompletedLaps"),
        new("SessionData.CurrentIncidentCount", "CurrentIncidentCount"),
        new("SessionData.CurrentDriverIncidentCount", "CurrentDriverIncidentCount"),
        new("SessionData.CurrentTeamIncidentCount", "CurrentTeamIncidentCount"),
        new("SessionData.MaxIncidentCount", "MaxIncidentCount"),
        new("SessionData.Flag", "Flag"),
        new("FlagData.distanceToNearestIncident", "DistanceToNearestIncident"),
        new("SessionData.IsLastLap", "IsLastLap"),
        new("SessionData.LastSector1Time", "LastSector1Time"),
        new("SessionData.LastSector2Time", "LastSector2Time"),
        new("SessionData.LastSector3Time", "LastSector3Time"),
        new("SessionData.NumCarsInPlayerClass", "NumCarsInPlayerClass"),
        new("SessionData.NumCarsOverall", "NumCarsOverall"),
        new("SessionData.PreviousLapWasValid", "PreviousLapWasValid"),
        new("SessionData.SectorNumber", "SectorNumber"),
        new("SessionData.SessionNumberOfLaps", "SessionNumberOfLaps"),
        new("SessionData.SessionPhase", "SessionPhase"),
        new("SessionData.SessionType", "SessionType"),
        new("SessionData.TrackDefinition.name", "TrackName"),
        new("SessionData.TrackDefinition.trackLength", "TrackLength"),

        new("Conditions.CurrentConditions.TrackTemperature", "TrackTemperature"),
        new("Conditions.CurrentConditions.AmbientTemperature", "AmbientTemperature"),

        new("CarDamageData.DamageEnabled", "DamageEnabled"),
        new("CarDamageData.OverallEngineDamage", "EngineDamage"),
        new("CarDamageData.OverallAeroDamage", "AeroDamage"),
        new("CarDamageData.OverallTransmissionDamage", "TransmissionDamage"),

        new("TyreData.FrontLeft_CenterTemp", "TyreFLTemp"),
        new("TyreData.FrontRight_CenterTemp", "TyreFRTemp"),
        new("TyreData.RearLeft_CenterTemp", "TyreRLTemp"),
        new("TyreData.RearRight_CenterTemp", "TyreRRTemp"),

        new("TyreData.FrontLeftPercentWear", "TyreFLWear"),
        new("TyreData.FrontRightPercentWear", "TyreFRWear"),
        new("TyreData.RearLeftPercentWear", "TyreRLWear"),
        new("TyreData.RearRightPercentWear", "TyreRRWear"),

        new("TyreData.FrontLeftPressure", "TyreFLPressure"),
        new("TyreData.FrontRightPressure", "TyreFRPressure"),
        new("TyreData.RearLeftPressure", "TyreRLPressure"),
        new("TyreData.RearRightPressure", "TyreRRPressure"),

        new("TyreData.TyreTypeName", "TyreType"),
        new("TyreData.fittedSet", "TyreSet"),

        new("TyreData.LeftFrontBrakeTemp", "BrakeFLTemp"),
        new("TyreData.RightFrontBrakeTemp", "BrakeFRTemp"),
        new("TyreData.LeftRearBrakeTemp", "BrakeRLTemp"),
        new("TyreData.RightRearBrakeTemp", "BrakeRRTemp"),

        new("TyreData.LeftFrontAttached", "LeftFrontAttached"),
        new("TyreData.RightFrontAttached", "RightFrontAttached"),
        new("TyreData.LeftRearAttached", "LeftRearAttached"),
        new("TyreData.RightRearAttached", "RightRearAttached"),

        new("TyreData.LeftFrontIsLocked", "LeftFrontIsLocked"),
        new("TyreData.RightFrontIsLocked", "RightFrontIsLocked"),
        new("TyreData.LeftRearIsLocked", "LeftRearIsLocked"),
        new("TyreData.RightRearIsLocked", "RightRearIsLocked"),

        new("TyreData.LeftFrontIsSpinning", "LeftFrontIsSpinning"),
        new("TyreData.RightFrontIsSpinning", "RightFrontIsSpinning"),
        new("TyreData.LeftRearIsSpinning", "LeftRearIsSpinning"),
        new("TyreData.RightRearIsSpinning", "RightRearIsSpinning")
    ];

    public static Task<CrewChiefMqttConfigurationResult> ConfigureAsync(
        CancellationToken cancellationToken = default) =>
        Task.Run(
            () => Configure(cancellationToken),
            cancellationToken);

    private static CrewChiefMqttConfigurationResult Configure(
        CancellationToken cancellationToken)
    {
        var candidates = FindCandidates(cancellationToken)
            .Select(path => new FileInfo(path))
            .Where(file => file.Exists)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ToArray();

        foreach (var file in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var result = ConfigureFile(file.FullName);
                if (result.Found)
                    return result;
            }
            catch
            {
                // Continue with another candidate. A stale or unrelated JSON
                // file must not prevent automatic setup.
            }
        }

        return new CrewChiefMqttConfigurationResult(
            false,
            false,
            null,
            0,
            "Файл mqtt_telemetry.json пока не найден. Запусти CrewChief один раз и нажми «Настроить CrewChief» в программе.");
    }

    private static CrewChiefMqttConfigurationResult ConfigureFile(
        string path)
    {
        var root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
        if (root is null)
        {
            return new CrewChiefMqttConfigurationResult(
                false, false, path, 0, "Файл телеметрии имеет неизвестный формат.");
        }

        var channels = root["Channels"] as JsonArray;
        if (channels is null)
        {
            return new CrewChiefMqttConfigurationResult(
                false, false, path, 0, "Это не конфигурация MQTT CrewChief.");
        }

        var existing = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in channels)
        {
            if (item is JsonObject channel &&
                channel["TelemetryField"]?.GetValue<string>() is { Length: > 0 } field)
            {
                existing[field] = channel;
            }
        }

        var added = 0;
        var updated = 0;
        foreach (var definition in Channels)
        {
            if (existing.TryGetValue(definition.TelemetryField, out var channel))
            {
                var currentPath = channel["CrewChiefField"]?.GetValue<string>() ?? string.Empty;
                if (!string.Equals(currentPath, definition.CrewChiefField, StringComparison.OrdinalIgnoreCase))
                {
                    channel["CrewChiefField"] = definition.CrewChiefField;
                    updated++;
                }

                continue;
            }

            channels.Add(new JsonObject
            {
                ["CrewChiefField"] = definition.CrewChiefField,
                ["TelemetryField"] = definition.TelemetryField
            });
            added++;
        }

        if (added == 0 && updated == 0)
        {
            return new CrewChiefMqttConfigurationResult(
                true,
                false,
                path,
                0,
                "CrewChief уже настроен.");
        }

        var backup = path + ".backup_installer_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        File.Copy(path, backup, overwrite: false);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        File.WriteAllText(
            path,
            root.ToJsonString(options),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        return new CrewChiefMqttConfigurationResult(
            true,
            true,
            path,
            added,
            $"CrewChief настроен. Добавлено каналов: {added}, обновлено: {updated}.");
    }

    private static IEnumerable<string> FindCandidates(
        CancellationToken cancellationToken)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string? path)
        {
            if (!string.IsNullOrWhiteSpace(path))
                result.Add(Path.GetFullPath(path));
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var downloads = Path.Combine(home, "Downloads");

        Add(Path.Combine(documents, "CrewChiefV4", "mqtt_telemetry.json"));
        Add(Path.Combine(home, "Documents", "CrewChiefV4", "mqtt_telemetry.json"));
        Add(Path.Combine(home, "Документы", "CrewChiefV4", "mqtt_telemetry.json"));

        foreach (var cloud in new[]
                 {
                     Environment.GetEnvironmentVariable("OneDrive"),
                     Environment.GetEnvironmentVariable("OneDriveConsumer"),
                     Environment.GetEnvironmentVariable("OneDriveCommercial")
                 })
        {
            if (string.IsNullOrWhiteSpace(cloud))
                continue;

            Add(Path.Combine(cloud, "Documents", "CrewChiefV4", "mqtt_telemetry.json"));
            Add(Path.Combine(cloud, "Документы", "CrewChiefV4", "mqtt_telemetry.json"));
        }

        foreach (var root in new[] { documents, desktop, downloads })
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                continue;

            try
            {
                foreach (var path in Directory.EnumerateFiles(
                             root,
                             "mqtt_telemetry.json",
                             SearchOption.AllDirectories))
                {
                    Add(path);
                }
            }
            catch
            {
                // Access denied and disappearing folders are expected.
            }
        }

        return result;
    }
}
