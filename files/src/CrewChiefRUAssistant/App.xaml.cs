using System.Windows;
using System.Windows.Threading;

namespace CrewChiefRUAssistant;

public partial class App : Application
{
    private SingleInstanceCoordinator? _singleInstance;

    public AppConfig Config { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new SingleInstanceCoordinator();
        if (!_singleInstance.IsPrimaryInstance)
        {
            if (!_singleInstance.ActivationSignalSent)
            {
                MessageBox.Show(
                    "CrewChief RU Assistant уже запущен. Проверь значок приложения в области уведомлений.",
                    "Приложение уже работает",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            Shutdown(0);
            return;
        }

        AppLog.Initialize(e.Args);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            AppLog.Write($"Compatibility check: {WindowsCompatibility.CurrentDescription}");

            if (!WindowsCompatibility.IsSupported)
            {
                AppLog.Write("Unsupported Windows version or architecture.");

                MessageBox.Show(
                    WindowsCompatibility.UnsupportedMessage,
                    "Неподдерживаемая версия Windows",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                Shutdown(1);
                return;
            }

            Config = AppConfig.LoadOrCreate();
            ThemeService.Initialize(Config.ThemeMode);

            var window = new MainWindow(Config);
            MainWindow = window;
            window.Show();

            _singleInstance.StartListening(() =>
                Dispatcher.BeginInvoke(
                    new Action(window.ActivateFromSecondInstance),
                    DispatcherPriority.Normal));

            AppLog.Write("Main window shown successfully.");
        }
        catch (Exception exception)
        {
            AppLog.WriteException("Application startup failed", exception);

            MessageBox.Show(
                $"Приложение не удалось запустить.\n\n{exception.Message}\n\nЖурнал:\n{AppLog.CurrentLogPath}",
                "Ошибка запуска CrewChief RU Assistant",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppLog.Write($"Application exit code: {e.ApplicationExitCode}");
        ThemeService.Shutdown();
        _singleInstance?.Dispose();
        _singleInstance = null;
        base.OnExit(e);
    }

    private static void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        AppLog.WriteException("Dispatcher exception", e.Exception);

        MessageBox.Show(
            $"{e.Exception}\n\nЖурнал:\n{AppLog.CurrentLogPath}",
            "Ошибка CrewChief RU Assistant",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }

    private static void OnUnhandledException(
        object? sender,
        UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is not Exception exception)
            return;

        AppLog.WriteException("Unhandled application exception", exception);
    }

    private static void OnUnobservedTaskException(
        object? sender,
        UnobservedTaskExceptionEventArgs e)
    {
        AppLog.WriteException("Unobserved task exception", e.Exception);
        e.SetObserved();
    }
}
