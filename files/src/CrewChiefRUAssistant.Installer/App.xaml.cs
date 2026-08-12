using System.Windows;
using System.Windows.Threading;

namespace CrewChiefRUAssistant.Installer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        InstallerLog.Initialize(e.Args);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            InstallerLog.Write(
                $"Compatibility check: {WindowsCompatibility.CurrentDescription}");

            if (!WindowsCompatibility.IsSupported)
            {
                InstallerLog.Write(
                    "Unsupported Windows version or architecture.");

                MessageBox.Show(
                    WindowsCompatibility.UnsupportedMessage,
                    "Неподдерживаемая версия Windows",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                Shutdown(1);
                return;
            }

            ThemeService.Initialize(InstallerThemeMode.System);

            Window window;

            if (e.Args.Length > 0 &&
                e.Args[0].Equals("--uninstall-worker", StringComparison.OrdinalIgnoreCase))
            {
                var installDirectory = e.Args.Length > 1
                    ? e.Args[1]
                    : InstallPaths.DefaultInstallDirectory;

                var deleteData = e.Args.Length > 2 &&
                                 bool.TryParse(e.Args[2], out var parsed) &&
                                 parsed;

                var originalProcessId = e.Args.Length > 3 &&
                                        int.TryParse(e.Args[3], out var processId)
                    ? processId
                    : 0;

                window = new UninstallProgressWindow(
                    installDirectory,
                    deleteData,
                    originalProcessId);
            }
            else if (e.Args.Length > 0 &&
                     e.Args[0].Equals("--uninstall", StringComparison.OrdinalIgnoreCase))
            {
                window = new UninstallWindow(InstallPaths.GetInstalledDirectory());
            }
            else
            {
                window = new InstallerWindow();
            }

            MainWindow = window;
            window.Show();
            InstallerLog.Write("Installer window shown successfully.");
        }
        catch (Exception exception)
        {
            InstallerLog.WriteException("Installer startup failed", exception);

            MessageBox.Show(
                $"Установщик не удалось запустить.\n\n{exception.Message}\n\nЖурнал:\n{InstallerLog.CurrentLogPath}",
                "Ошибка запуска установщика",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        InstallerLog.Write($"Installer exit code: {e.ApplicationExitCode}");
        ThemeService.Shutdown();
        base.OnExit(e);
    }

    private static void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        InstallerLog.WriteException("Unhandled installer exception", e.Exception);

        MessageBox.Show(
            $"Установщик завершился с ошибкой.\n\n{e.Exception.Message}\n\nЖурнал:\n{InstallerLog.CurrentLogPath}",
            "Ошибка установщика",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }

    private static void OnUnhandledException(
        object? sender,
        UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
            InstallerLog.WriteException("Unhandled installer AppDomain exception", exception);
    }

    private static void OnUnobservedTaskException(
        object? sender,
        UnobservedTaskExceptionEventArgs e)
    {
        InstallerLog.WriteException("Unobserved installer task exception", e.Exception);
        e.SetObserved();
    }
}
