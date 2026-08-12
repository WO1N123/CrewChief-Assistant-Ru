using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Runtime.InteropServices;
using CrewChiefRUAssistant.Shared;
using Microsoft.Win32;

namespace CrewChiefRUAssistant.Installer;

internal sealed record InstallOptions(
    string InstallDirectory,
    bool CreateDesktopShortcut,
    bool StartWithWindows,
    bool ConfigureCrewChief,
    bool LaunchAfterInstall,
    bool InstallEugene,
    bool InstallXenia,
    string VoiceId);

internal sealed record InstallProgress(
    int Percent,
    string Message);

internal static class InstallerEngine
{
    private const string PayloadResourceName =
        "CrewChiefRUAssistant.Installer.payload.zip";

    public static bool IsInstalled =>
        File.Exists(Path.Combine(
            InstallPaths.GetInstalledDirectory(),
            "CrewChiefRUAssistant.exe"));

    public static async Task<string?> InstallAsync(
        InstallOptions options,
        IProgress<InstallProgress> progress,
        CancellationToken cancellationToken)
    {
        InstallerLog.Write(
            $"Install requested. Directory={options.InstallDirectory}; " +
            $"Eugene={options.InstallEugene}; Xenia={options.InstallXenia}; " +
            $"Voice={options.VoiceId}; ConfigureCrewChief={options.ConfigureCrewChief}; " +
            $"AutoStart={options.StartWithWindows}; Launch={options.LaunchAfterInstall}");
        if (!options.InstallEugene &&
            !options.InstallXenia)
        {
            throw new InvalidOperationException(
                "Выбери хотя бы один голос для установки.");
        }

        var installDirectory = Path.GetFullPath(options.InstallDirectory);
        var temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            "CrewChiefRUAssistant-Install-" + Guid.NewGuid().ToString("N"));
        var extractedRoot = Path.Combine(temporaryRoot, "payload");

        Directory.CreateDirectory(extractedRoot);

        var stagedInstallerDirectory = Path.Combine(
            temporaryRoot,
            "installer-runtime");

        // Stage the running Setup before replacing the installation directory,
        // so the same executable can also serve as the installed uninstaller.
        _ = InstallerRuntimeStager.StageCurrentRuntime(
            stagedInstallerDirectory);

        try
        {
            InstallerLog.Write("Progress 2%: Подготовка установки…");
            progress.Report(new InstallProgress(2, "Подготовка установки…"));
            await StopRunningAssistantAsync(cancellationToken);

            InstallerLog.Write("Progress 8%: Распаковка программы…");
            progress.Report(new InstallProgress(8, "Распаковка программы…"));
            await ExtractPayloadAsync(
                extractedRoot,
                progress,
                cancellationToken);

            var payloadApp = Path.Combine(extractedRoot, "app");
            var payloadData = Path.Combine(extractedRoot, "data");

            if (!File.Exists(Path.Combine(payloadApp, "CrewChiefRUAssistant.exe")))
                throw new InvalidDataException("В установщике отсутствует файл программы.");

            InstallerLog.Write("Progress 62%: Установка файлов…");
            progress.Report(new InstallProgress(62, "Установка файлов…"));
            ReplaceApplicationDirectory(payloadApp, installDirectory);

            if (Directory.Exists(payloadData))
            {
                progress.Report(
                    new InstallProgress(
                        72,
                        "Установка модели и выбранных голосов…"));

                InstallSelectedData(
                    payloadData,
                    options);
            }

            var installedUninstallerDirectory =
                InstallPaths.GetUninstallerDirectory(installDirectory);

            TryDeleteDirectory(installedUninstallerDirectory);
            CopyDirectory(
                stagedInstallerDirectory,
                installedUninstallerDirectory,
                overwrite: true);

            var installedSetup =
                InstallPaths.GetUninstallerExecutable(installDirectory);

            if (!File.Exists(installedSetup))
            {
                throw new FileNotFoundException(
                    "Не удалось установить модуль удаления.",
                    installedSetup);
            }

            // Remove legacy installer filenames after an update.
            TryDelete(Path.Combine(installDirectory, "Uninstall.exe"));
            TryDelete(Path.Combine(installDirectory, "CrewChiefRUAssistant_Setup.exe"));
            TryDelete(Path.Combine(installDirectory, InstallPaths.InstallerFileName));

            ApplyVoicePreference(
                ResolveActiveVoice(options));

            InstallerLog.Write("Progress 82%: Создание ярлыков…");
            progress.Report(new InstallProgress(82, "Создание ярлыков…"));
            CreateShortcut(
                InstallPaths.StartMenuShortcut,
                Path.Combine(installDirectory, "CrewChiefRUAssistant.exe"),
                installDirectory);

            if (options.CreateDesktopShortcut)
            {
                CreateShortcut(
                    InstallPaths.DesktopShortcut,
                    Path.Combine(installDirectory, "CrewChiefRUAssistant.exe"),
                    installDirectory);
            }
            else
            {
                TryDelete(InstallPaths.DesktopShortcut);
            }

            RegisterUninstaller(installDirectory);
            ConfigureAutomaticStart(installDirectory, options.StartWithWindows);

            string? crewChiefMessage = null;
            if (options.ConfigureCrewChief)
            {
                InstallerLog.Write("Progress 90%: Настройка CrewChief…");
            progress.Report(new InstallProgress(90, "Настройка CrewChief…"));
                var result = await CrewChiefMqttConfigurator.ConfigureAsync(cancellationToken);
                crewChiefMessage = result.Message;
            }

            InstallerLog.Write("Progress 100%: Установка завершена");
            progress.Report(new InstallProgress(100, "Установка завершена"));

            if (options.LaunchAfterInstall)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Path.Combine(installDirectory, "CrewChiefRUAssistant.exe"),
                    WorkingDirectory = installDirectory,
                    UseShellExecute = true
                });
            }

            return crewChiefMessage;
        }
        catch (Exception exception)
        {
            InstallerLog.WriteException(
                "Installation failed",
                exception);

            throw;
        }
        finally
        {
            TryDeleteDirectory(temporaryRoot);
        }
    }

    public static async Task UninstallAsync(
        string installDirectory,
        bool deleteData,
        IProgress<InstallProgress> progress,
        CancellationToken cancellationToken)
    {
        InstallerLog.Write(
            $"Uninstall requested. Directory={installDirectory}; DeleteData={deleteData}");
        InstallerLog.Write("Progress 10%: Закрытие программы…");
            progress.Report(new InstallProgress(10, "Закрытие программы…"));
        await StopRunningAssistantAsync(cancellationToken);

        InstallerLog.Write("Progress 35%: Удаление ярлыков…");
            progress.Report(new InstallProgress(35, "Удаление ярлыков…"));
        TryDelete(InstallPaths.DesktopShortcut);
        TryDelete(InstallPaths.StartMenuShortcut);

        using (var uninstall = Registry.CurrentUser.OpenSubKey(
                   @"Software\Microsoft\Windows\CurrentVersion\Uninstall",
                   writable: true))
        {
            uninstall?.DeleteSubKeyTree(
                InstallPaths.UninstallKeyName,
                throwOnMissingSubKey: false);
        }

        using (var run = Registry.CurrentUser.OpenSubKey(
                   InstallPaths.RunRegistryPath,
                   writable: true))
        {
            run?.DeleteValue(InstallPaths.UninstallKeyName, throwOnMissingValue: false);
        }

        InstallerLog.Write("Progress 65%: Удаление файлов программы…");
            progress.Report(new InstallProgress(65, "Удаление файлов программы…"));
        TryDeleteDirectory(installDirectory);
        if (Directory.Exists(installDirectory))
        {
            throw new IOException(
                "Не удалось удалить папку программы. Закрой все окна программы и повтори удаление.");
        }

        if (deleteData)
        {
            InstallerLog.Write("Progress 85%: Удаление настроек и голосовых данных…");
            progress.Report(new InstallProgress(85, "Удаление настроек и голосовых данных…"));
            TryDeleteDirectory(InstallPaths.DataDirectory);
        }

        InstallerLog.Write("Progress 100%: Программа удалена");
            progress.Report(new InstallProgress(100, "Программа удалена"));
    }

    private static async Task ExtractPayloadAsync(
        string destination,
        IProgress<InstallProgress> progress,
        CancellationToken cancellationToken)
    {
        var assembly = Assembly.GetExecutingAssembly();
        await using var payload = assembly.GetManifestResourceStream(PayloadResourceName)
                                  ?? throw new InvalidOperationException(
                                      "Установочный пакет не встроен в Setup.exe.");

        using var archive = new ZipArchive(payload, ZipArchiveMode.Read);
        var entries = archive.Entries.Where(entry => entry.Length > 0).ToArray();
        var completed = 0;
        var destinationRoot = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;

        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var target = Path.GetFullPath(Path.Combine(destination, entry.FullName));
            if (!target.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Установочный архив содержит небезопасный путь.");

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(target);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await using var input = entry.Open();
            await using var output = new FileStream(
                target,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                131072,
                useAsync: true);
            await input.CopyToAsync(output, cancellationToken);

            completed++;
            var percent = 8 + (int)(50d * completed / Math.Max(1, entries.Length));
            progress.Report(new InstallProgress(percent, "Распаковка программы…"));
        }
    }

    private static async Task StopRunningAssistantAsync(
        CancellationToken cancellationToken)
    {
        var currentId = Environment.ProcessId;
        var processes = Process.GetProcessesByName("CrewChiefRUAssistant")
            .Where(process => process.Id != currentId)
            .ToArray();

        foreach (var process in processes)
        {
            using (process)
            {
                try
                {
                    process.CloseMainWindow();
                    var exited = await Task.Run(
                        () => process.WaitForExit(2500),
                        cancellationToken);

                    if (!exited && !process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                        await process.WaitForExitAsync(cancellationToken);
                    }
                }
                catch
                {
                    // The process may exit while it is being inspected.
                }
            }
        }
    }

    private static void ReplaceApplicationDirectory(
        string source,
        string destination)
    {
        var backup = destination + ".old";
        TryDeleteDirectory(backup);

        if (Directory.Exists(destination))
            Directory.Move(destination, backup);

        try
        {
            CopyDirectory(source, destination, overwrite: true);
            TryDeleteDirectory(source);
            TryDeleteDirectory(backup);
        }
        catch
        {
            TryDeleteDirectory(destination);
            if (Directory.Exists(backup))
                Directory.Move(backup, destination);
            throw;
        }
    }

    private static void CopyDirectory(
        string source,
        string destination,
        bool overwrite)
    {
        Directory.CreateDirectory(destination);

        foreach (var directory in Directory.EnumerateDirectories(
                     source,
                     "*",
                     SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(
                destination,
                Path.GetRelativePath(source, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(
                     source,
                     "*",
                     SearchOption.AllDirectories))
        {
            var target = Path.Combine(
                destination,
                Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite);
        }
    }

    private static void CreateShortcut(
        string shortcutPath,
        string targetPath,
        string workingDirectory)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath)!);

        var shellType = Type.GetTypeFromProgID("WScript.Shell")
                        ?? throw new InvalidOperationException("Windows Script Host недоступен.");
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(shortcutPath);

        try
        {
            shortcut.TargetPath = targetPath;
            shortcut.WorkingDirectory = workingDirectory;
            shortcut.IconLocation = targetPath + ",0";
            shortcut.Description = InstallPaths.ProductName;
            shortcut.Save();
        }
        finally
        {
            if (Marshal.IsComObject(shortcut))
                Marshal.FinalReleaseComObject(shortcut);
            if (Marshal.IsComObject(shell))
                Marshal.FinalReleaseComObject(shell);
        }
    }

    private static void InstallSelectedData(
        string payloadData,
        InstallOptions options)
    {
        var payloadModel = Path.Combine(
            payloadData,
            "models",
            "vosk-model-small-ru-0.22");

        if (!Directory.Exists(payloadModel))
        {
            throw new InvalidDataException(
                "В установочном пакете отсутствует русская модель Vosk.");
        }

        var targetModel = Path.Combine(
            InstallPaths.DataDirectory,
            "models",
            "vosk-model-small-ru-0.22");

        TryDeleteDirectory(
            targetModel);

        CopyDirectory(
            payloadModel,
            targetModel,
            overwrite: true);

        InstallVoicePackage(
            payloadData,
            "eugene",
            options.InstallEugene);

        InstallVoicePackage(
            payloadData,
            "xenia",
            options.InstallXenia);
    }

    private static void InstallVoicePackage(
        string payloadData,
        string voiceId,
        bool install)
    {
        var directoryName =
            $"voice_bank_{voiceId}_radio_v1";

        var target = Path.Combine(
            InstallPaths.DataDirectory,
            "audio",
            directoryName);

        if (!install)
        {
            TryDeleteDirectory(
                target);

            if (Directory.Exists(target))
            {
                throw new IOException(
                    $"Не удалось удалить голосовой пакет {voiceId}. " +
                    "Закрой программу и повтори установку.");
            }

            return;
        }

        var source = Path.Combine(
            payloadData,
            "audio",
            directoryName);

        if (!File.Exists(
                Path.Combine(
                    source,
                    "READY.json")))
        {
            throw new InvalidDataException(
                $"В установочном пакете отсутствует голос {voiceId}.");
        }

        TryDeleteDirectory(
            target);

        CopyDirectory(
            source,
            target,
            overwrite: true);
    }

    private static string ResolveActiveVoice(
        InstallOptions options)
    {
        var requested =
            string.Equals(
                options.VoiceId,
                "xenia",
                StringComparison.OrdinalIgnoreCase)
                    ? "xenia"
                    : "eugene";

        if (requested == "xenia" &&
            options.InstallXenia)
        {
            return "xenia";
        }

        if (requested == "eugene" &&
            options.InstallEugene)
        {
            return "eugene";
        }

        return options.InstallEugene
            ? "eugene"
            : "xenia";
    }

    private static void ApplyVoicePreference(
        string voiceId)
    {
        var normalizedVoice =
            string.Equals(
                voiceId,
                "xenia",
                StringComparison.OrdinalIgnoreCase)
                    ? "xenia"
                    : "eugene";

        Directory.CreateDirectory(
            InstallPaths.DataDirectory);

        var configPath = Path.Combine(
            InstallPaths.DataDirectory,
            "appsettings.json");

        JsonObject config;

        try
        {
            config = File.Exists(configPath)
                ? JsonNode.Parse(
                      File.ReadAllText(configPath))
                      as JsonObject
                  ?? new JsonObject()
                : new JsonObject();
        }
        catch
        {
            config = new JsonObject();
        }

        config["VoiceId"] =
            normalizedVoice;

        File.WriteAllText(
            configPath,
            config.ToJsonString(
                new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
    }

    private static void RegisterUninstaller(string installDirectory)
    {
        using var key = Registry.CurrentUser.CreateSubKey(
            InstallPaths.UninstallRegistryPath,
            writable: true);

        var setupExe =
            InstallPaths.GetUninstallerExecutable(installDirectory);

        key.SetValue("DisplayName", InstallPaths.ProductName);
        key.SetValue("DisplayVersion", InstallPaths.ProductVersion);
        key.SetValue("Publisher", "CrewChief RU Assistant");
        key.SetValue("InstallLocation", installDirectory);
        key.SetValue("DisplayIcon", Path.Combine(installDirectory, "CrewChiefRUAssistant.exe"));
        key.SetValue("UninstallString", $"\"{setupExe}\" --uninstall");
        key.SetValue("QuietUninstallString", $"\"{setupExe}\" --uninstall");
        key.SetValue("ModifyPath", $"\"{setupExe}\"");
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }

    private static void ConfigureAutomaticStart(
        string installDirectory,
        bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(
            InstallPaths.RunRegistryPath,
            writable: true);

        if (enabled)
        {
            key.SetValue(
                InstallPaths.UninstallKeyName,
                $"\"{Path.Combine(installDirectory, "CrewChiefRUAssistant.exe")}\"");
        }
        else
        {
            key.DeleteValue(InstallPaths.UninstallKeyName, throwOnMissingValue: false);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // A shortcut can be removed manually later.
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // The caller decides whether a remaining directory is fatal.
        }
    }
}
