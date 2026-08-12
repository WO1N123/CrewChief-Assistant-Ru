using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace CrewChiefRUAssistant.Installer;

public partial class UninstallWindow : Window
{
    private readonly string _installDirectory;

    public UninstallWindow(string installDirectory)
    {
        InitializeComponent();
        _installDirectory = installDirectory;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e) =>
        ThemeService.ApplyWindowChrome(this);

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                "CrewChiefRUAssistant-Uninstall-" + Guid.NewGuid().ToString("N"));

            var temporaryExecutable =
                InstallerRuntimeStager.StageCurrentRuntime(temporaryDirectory);

            Process.Start(new ProcessStartInfo
            {
                FileName = temporaryExecutable,
                WorkingDirectory = temporaryDirectory,
                ArgumentList =
                {
                    "--uninstall-worker",
                    _installDirectory,
                    (DeleteDataCheck.IsChecked == true).ToString(),
                    Environment.ProcessId.ToString()
                },
                UseShellExecute = true
            });

            Close();
        }
        catch (Exception exception)
        {
            InstallerLog.WriteException("Unable to start uninstall worker", exception);

            MessageBox.Show(
                this,
                $"Не удалось начать удаление.\n\n{exception.Message}\n\nЖурнал:\n{InstallerLog.CurrentLogPath}",
                "Ошибка удаления",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
