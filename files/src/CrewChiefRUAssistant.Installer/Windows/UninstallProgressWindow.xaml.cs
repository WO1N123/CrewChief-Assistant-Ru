using System.Diagnostics;
using System.Windows;

namespace CrewChiefRUAssistant.Installer;

public partial class UninstallProgressWindow : Window
{
    private readonly string _installDirectory;
    private readonly bool _deleteData;
    private readonly int _originalProcessId;

    public UninstallProgressWindow(
        string installDirectory,
        bool deleteData,
        int originalProcessId)
    {
        InitializeComponent();
        _installDirectory = installDirectory;
        _deleteData = deleteData;
        _originalProcessId = originalProcessId;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ThemeService.ApplyWindowChrome(this);
        await RemoveAsync();
    }

    private async Task RemoveAsync()
    {
        try
        {
            if (_originalProcessId > 0)
            {
                try
                {
                    using var original = Process.GetProcessById(_originalProcessId);
                    await original.WaitForExitAsync();
                }
                catch
                {
                }
            }

            await Task.Delay(250);

            var progress = new Progress<InstallProgress>(item =>
            {
                ProgressBar.Value = Math.Clamp(item.Percent, 0, 100);
                StatusText.Text = item.Message;
            });

            await InstallerEngine.UninstallAsync(
                _installDirectory,
                _deleteData,
                progress,
                CancellationToken.None);

            StatusText.Text = "CrewChief RU Assistant удалён";
            CloseButton.IsEnabled = true;
        }
        catch (Exception exception)
        {
            InstallerLog.WriteException("Uninstallation failed", exception);
            StatusText.Text = "Удаление завершилось с ошибкой";
            CloseButton.IsEnabled = true;

            MessageBox.Show(
                this,
                $"{exception.Message}\n\nЖурнал:\n{InstallerLog.CurrentLogPath}",
                "Ошибка удаления",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
