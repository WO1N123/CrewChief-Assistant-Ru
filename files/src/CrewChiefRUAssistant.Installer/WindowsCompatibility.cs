namespace CrewChiefRUAssistant.Installer;

internal static class WindowsCompatibility
{
    private const int MinimumWindows10Build = 17763;

    public static bool IsSupported =>
        OperatingSystem.IsWindowsVersionAtLeast(
            10,
            0,
            MinimumWindows10Build) &&
        Environment.Is64BitOperatingSystem;

    public static string CurrentDescription
    {
        get
        {
            var version =
                Environment.OSVersion.Version;

            var family =
                version.Build >= 22000
                    ? "Windows 11"
                    : "Windows 10";

            return
                $"{family} {version.Major}.{version.Minor}.{version.Build} " +
                $"({(Environment.Is64BitOperatingSystem ? "x64" : "x86")})";
        }
    }

    public static string UnsupportedMessage =>
        "Установщик поддерживает Windows 10 x64 версии 1809 " +
        "(сборка 17763) или новее, а также Windows 11 x64.\n\n" +
        $"Обнаружено: {CurrentDescription}\n\n" +
        "Для Windows 10 рекомендуется версия 21H2/22H2 или LTSC.";
}
