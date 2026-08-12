using Microsoft.Win32;

namespace CrewChiefRUAssistant.Installer;

internal static class InstallPaths
{
    public const string ProductName = "CrewChief RU Assistant";
    public const string ProductVersion = "0.9.4";
    public const string UninstallKeyName = "CrewChiefRUAssistant";
    public const string InstallerFileName = "CrewChiefRU_Setup.exe";
    public const string UninstallerDirectoryName = "uninstaller";

    public static string DefaultInstallDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "CrewChief RU Assistant");

    public static string DataDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CrewChiefRUAssistant");

    public static string StartMenuShortcut =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            "Programs",
            "CrewChief RU Assistant.lnk");

    public static string DesktopShortcut =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "CrewChief RU Assistant.lnk");

    public static string UninstallRegistryPath =>
        $@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{UninstallKeyName}";

    public static string RunRegistryPath =>
        @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static string GetUninstallerDirectory(string installDirectory) =>
        Path.Combine(installDirectory, UninstallerDirectoryName);

    public static string GetUninstallerExecutable(string installDirectory) =>
        Path.Combine(GetUninstallerDirectory(installDirectory), InstallerFileName);

    public static string GetInstalledDirectory()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(UninstallRegistryPath);
            var location = key?.GetValue("InstallLocation") as string;

            if (!string.IsNullOrWhiteSpace(location))
                return location;
        }
        catch
        {
            // Fall back to the directory containing the installed Setup.exe.
        }

        return AppContext.BaseDirectory.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
    }
}
