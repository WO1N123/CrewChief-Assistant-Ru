using System.Text.Json;
using System.Text.Json.Serialization;
using CrewChiefRUAssistant.Input;

namespace CrewChiefRUAssistant;

public sealed class AppConfig
{
    public int MicrophoneDevice { get; set; } = 0;

    // Kept for migration from v0.4.0.2 and earlier.
    public int HotkeyVirtualKey { get; set; } = 120;

    public InputBinding? PttBinding { get; set; }

    public int MqttPort { get; set; } = 1883;
    public int TelemetryMaxAgeSeconds { get; set; } = 300;
    public int RecognitionAlternatives { get; set; } = 5;
    public int RecognitionPreRollMs { get; set; } = 220;
    public int RecognitionPostRollMs { get; set; } = 180;
    public int RecognitionMinSpeechMs { get; set; } = 140;
    public bool RecognitionCommandPass { get; set; } = true;
    public bool PrintIncomingTopics { get; set; } = false;
    public bool SpeechEnabled { get; set; } = true;
    public int PlaybackDevice { get; set; } = -1;
    public int SpeechVolumePercent { get; set; } = 85;
    public string VoiceId { get; set; } = "eugene";
    public bool CrewChiefVoicePriority { get; set; } = true;
    public double CrewChiefAudioThreshold { get; set; } = 0.006;
    public int CrewChiefActivityHoldMs { get; set; } = 260;
    public int CrewChiefQuietPeriodMs { get; set; } = 650;
    public int CrewChiefPriorityMaxWaitMs { get; set; } = 15000;
    public int CrewChiefPriorityMaxReplays { get; set; } = 2;
    public bool MinimizeToTray { get; set; } = true;
    public AppThemeMode ThemeMode { get; set; } = AppThemeMode.System;

    public static string DataDirectory =>
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "CrewChiefRUAssistant");

    public static string ConfigPath =>
        Path.Combine(
            DataDirectory,
            "appsettings.json");

    public static string ModelDirectory =>
        Path.Combine(
            DataDirectory,
            "models",
            "vosk-model-small-ru-0.22");

    public string GetVoiceBankDirectory() =>
        GetVoiceBankDirectory(VoiceId);

    public static string GetVoiceBankDirectory(
        string? voiceId)
    {
        var normalized =
            NormalizeVoiceId(voiceId);

        return Path.Combine(
            DataDirectory,
            "audio",
            $"voice_bank_{normalized}_radio_v1");
    }

    public string GetVoiceDisplayName() =>
        GetVoiceDisplayName(VoiceId);

    public static string GetVoiceDisplayName(
        string? voiceId) =>
        NormalizeVoiceId(voiceId) == "xenia"
            ? "Xenia"
            : "Eugene";

    public static bool IsVoiceInstalled(
        string? voiceId)
    {
        var directory =
            GetVoiceBankDirectory(voiceId);

        return
            File.Exists(
                Path.Combine(
                    directory,
                    "READY.json")) &&
            File.Exists(
                Path.Combine(
                    directory,
                    "phrases",
                    "unknown.wav")) &&
            File.Exists(
                Path.Combine(
                    directory,
                    "numbers",
                    "0.wav")) &&
            File.Exists(
                Path.Combine(
                    directory,
                    "radio",
                    "open.wav")) &&
            File.Exists(
                Path.Combine(
                    directory,
                    "radio",
                    "close.wav"));
    }

    public static IReadOnlyList<string>
        GetInstalledVoiceIds()
    {
        var result = new List<string>(2);

        if (IsVoiceInstalled("eugene"))
            result.Add("eugene");

        if (IsVoiceInstalled("xenia"))
            result.Add("xenia");

        return result;
    }

    public static string NormalizeVoiceId(
        string? voiceId) =>
        string.Equals(
            voiceId,
            "xenia",
            StringComparison.OrdinalIgnoreCase)
                ? "xenia"
                : "eugene";

    public InputBinding GetPttBinding() =>
        (PttBinding ??
         InputBinding.Keyboard(
             HotkeyVirtualKey))
        .Clone();

    public static AppConfig LoadOrCreate()
    {
        Directory.CreateDirectory(
            DataDirectory);

        if (!File.Exists(ConfigPath))
        {
            var config = CreateDefault();
            config.Save();
            return config;
        }

        try
        {
            var json =
                File.ReadAllText(
                    ConfigPath);

            var config =
                JsonSerializer.Deserialize<AppConfig>(
                    json,
                    CreateJsonOptions(
                        indented: false))
                ?? CreateDefault();

            var changed = false;

            if (config.TelemetryMaxAgeSeconds < 60)
            {
                config.TelemetryMaxAgeSeconds = 300;
                changed = true;
            }

            if (!Enum.IsDefined(config.ThemeMode))
            {
                config.ThemeMode = AppThemeMode.System;
                changed = true;
            }

            // Existing installations used only HotkeyVirtualKey.
            if (config.PttBinding is null)
            {
                config.PttBinding =
                    InputBinding.Keyboard(
                        config.HotkeyVirtualKey);

                changed = true;
            }

            var normalizedVoice =
                NormalizeVoiceId(
                    config.VoiceId);

            if (!string.Equals(
                    normalizedVoice,
                    config.VoiceId,
                    StringComparison.Ordinal))
            {
                config.VoiceId =
                    normalizedVoice;

                changed = true;
            }

            // If the selected bank was removed but the other one remains,
            // automatically use the installed bank.
            if (!IsVoiceInstalled(
                    config.VoiceId))
            {
                var installed =
                    GetInstalledVoiceIds();

                if (installed.Count > 0)
                {
                    config.VoiceId =
                        installed[0];

                    changed = true;
                }
            }

            if (changed)
                config.Save();

            return config;
        }
        catch
        {
            var brokenPath =
                ConfigPath +
                ".broken_" +
                DateTime.Now.ToString(
                    "yyyyMMdd_HHmmss");

            File.Copy(
                ConfigPath,
                brokenPath,
                overwrite: true);

            var config = CreateDefault();
            config.Save();
            return config;
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(
            DataDirectory);

        PttBinding ??=
            InputBinding.Keyboard(
                HotkeyVirtualKey);

        VoiceId =
            NormalizeVoiceId(
                VoiceId);

        if (PttBinding.Kind ==
            InputBindingKind.Keyboard)
        {
            HotkeyVirtualKey =
                PttBinding.VirtualKey;
        }

        var json =
            JsonSerializer.Serialize(
                this,
                CreateJsonOptions(
                    indented: true));

        File.WriteAllText(
            ConfigPath,
            json);
    }

    private static AppConfig CreateDefault() =>
        new()
        {
            PttBinding =
                InputBinding.Keyboard(120)
        };

    private static JsonSerializerOptions
        CreateJsonOptions(
            bool indented)
    {
        var options =
            new JsonSerializerOptions
            {
                WriteIndented = indented,
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling =
                    JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

        options.Converters.Add(
            new JsonStringEnumConverter());

        return options;
    }
}
