using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;

namespace CrewChiefRUAssistant.Installer;

public partial class InstallerWindow : Window
{
    private sealed record VoiceChoice(string Id, string Text)
    {
        public override string ToString() => Text;
    }

    private readonly CancellationTokenSource _cancellation = new();
    private readonly bool _alreadyInstalled;
    private int _step;
    private bool _busy;

    public InstallerWindow()
    {
        InitializeComponent();

        InstallEugeneCheck.Checked += VoiceSelectionChanged;
        InstallEugeneCheck.Unchecked += VoiceSelectionChanged;
        InstallXeniaCheck.Checked += VoiceSelectionChanged;
        InstallXeniaCheck.Unchecked += VoiceSelectionChanged;

        _alreadyInstalled = InstallerEngine.IsInstalled;
        InstallPathBox.Text = _alreadyInstalled
            ? InstallPaths.GetInstalledDirectory()
            : InstallPaths.DefaultInstallDirectory;

        WelcomeTitle.Text = _alreadyInstalled
            ? "Изменение установки"
            : "Добро пожаловать";

        WelcomeSubtitle.Text = _alreadyInstalled
            ? "Обнови программу, измени голоса или дополнительные параметры."
            : "Установщик подготовит программу, русскую модель распознавания и выбранные голоса.";

        RemoveButton.Visibility = _alreadyInstalled
            ? Visibility.Visible
            : Visibility.Collapsed;

        ThemeCombo.SelectedIndex = 0;
        LoadVoiceState();
        UpdateStepVisuals();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ThemeService.ApplyWindowChrome(this);
        AnimatePage(LocationPage);
    }

    private void LoadVoiceState()
    {
        var eugeneInstalled = IsVoiceInstalled("eugene");
        var xeniaInstalled = IsVoiceInstalled("xenia");

        EugeneInstalledText.Text = eugeneInstalled
            ? "✓ Уже установлен"
            : "Не установлен";

        XeniaInstalledText.Text = xeniaInstalled
            ? "✓ Уже установлен"
            : "Не установлен";

        EugeneInstalledText.Foreground = (Brush)FindResource(
            eugeneInstalled ? "SuccessBrush" : "TextSecondaryBrush");

        XeniaInstalledText.Foreground = (Brush)FindResource(
            xeniaInstalled ? "SuccessBrush" : "TextSecondaryBrush");

        InstallEugeneCheck.IsChecked = _alreadyInstalled
            ? eugeneInstalled
            : true;

        InstallXeniaCheck.IsChecked = _alreadyInstalled
            ? xeniaInstalled
            : true;

        if (InstallEugeneCheck.IsChecked != true &&
            InstallXeniaCheck.IsChecked != true)
        {
            InstallEugeneCheck.IsChecked = true;
        }

        UpdateDefaultVoiceChoices(ReadCurrentVoiceId());
        UpdateVoiceCards();
    }

    private void UpdateDefaultVoiceChoices(string? preferredVoice = null)
    {
        var previous = preferredVoice ??
                       (DefaultVoiceCombo.SelectedItem as VoiceChoice)?.Id ??
                       ReadCurrentVoiceId();

        DefaultVoiceCombo.Items.Clear();

        if (InstallEugeneCheck.IsChecked == true)
            DefaultVoiceCombo.Items.Add(new VoiceChoice("eugene", "Мужской — Eugene"));

        if (InstallXeniaCheck.IsChecked == true)
            DefaultVoiceCombo.Items.Add(new VoiceChoice("xenia", "Женский — Xenia"));

        if (DefaultVoiceCombo.Items.Count == 0)
        {
            DefaultVoiceCombo.SelectedIndex = -1;
            return;
        }

        for (var index = 0; index < DefaultVoiceCombo.Items.Count; index++)
        {
            if (DefaultVoiceCombo.Items[index] is VoiceChoice choice &&
                choice.Id == previous)
            {
                DefaultVoiceCombo.SelectedIndex = index;
                return;
            }
        }

        DefaultVoiceCombo.SelectedIndex = 0;
    }

    private void UpdateVoiceCards()
    {
        var accent = (Brush)FindResource("AccentBrush");
        var border = (Brush)FindResource("BorderBrush");
        var selectedBackground = (Brush)FindResource("AccentSoftBrush");
        var normalBackground = (Brush)FindResource("SurfaceBrush");

        EugeneChoiceCard.BorderBrush = InstallEugeneCheck.IsChecked == true
            ? accent
            : border;
        EugeneChoiceCard.BorderThickness = InstallEugeneCheck.IsChecked == true
            ? new Thickness(2)
            : new Thickness(1);
        EugeneChoiceCard.Background = InstallEugeneCheck.IsChecked == true
            ? selectedBackground
            : normalBackground;

        XeniaChoiceCard.BorderBrush = InstallXeniaCheck.IsChecked == true
            ? accent
            : border;
        XeniaChoiceCard.BorderThickness = InstallXeniaCheck.IsChecked == true
            ? new Thickness(2)
            : new Thickness(1);
        XeniaChoiceCard.Background = InstallXeniaCheck.IsChecked == true
            ? selectedBackground
            : normalBackground;
    }

    private void RefreshThemeDependentVisuals()
    {
        var eugeneInstalled = IsVoiceInstalled("eugene");
        var xeniaInstalled = IsVoiceInstalled("xenia");

        EugeneInstalledText.Foreground = (Brush)FindResource(
            eugeneInstalled ? "SuccessBrush" : "TextSecondaryBrush");

        XeniaInstalledText.Foreground = (Brush)FindResource(
            xeniaInstalled ? "SuccessBrush" : "TextSecondaryBrush");

        UpdateVoiceCards();
        UpdateStepVisuals();
        ThemeService.ApplyWindowChrome(this);
    }

    private void ShowStep(int step)
    {
        _step = Math.Clamp(step, 0, 2);

        var target = _step switch
        {
            0 => LocationPage,
            1 => VoicesPage,
            _ => OptionsPage
        };

        LocationPage.Visibility = target == LocationPage
            ? Visibility.Visible
            : Visibility.Collapsed;
        VoicesPage.Visibility = target == VoicesPage
            ? Visibility.Visible
            : Visibility.Collapsed;
        OptionsPage.Visibility = target == OptionsPage
            ? Visibility.Visible
            : Visibility.Collapsed;
        ProgressPage.Visibility = Visibility.Collapsed;
        CompletePage.Visibility = Visibility.Collapsed;

        AnimatePage(target);
        UpdateStepVisuals();
    }

    private void UpdateStepVisuals()
    {
        SetStepIndicator(StepOneIndicator, _step == 0);
        SetStepIndicator(StepTwoIndicator, _step == 1);
        SetStepIndicator(StepThreeIndicator, _step == 2);

        BackButton.Visibility = _step > 0 && !_busy
            ? Visibility.Visible
            : Visibility.Collapsed;

        NextButton.Content = _step == 2
            ? (_alreadyInstalled ? "Применить" : "Установить")
            : "Далее";

        if (_step == 2)
            UpdateSummary();
    }

    private static void SetStepIndicator(Border border, bool active)
    {
        border.Opacity = active ? 1 : 0.55;
    }

    private void UpdateSummary()
    {
        var voices = new List<string>();

        if (InstallEugeneCheck.IsChecked == true)
            voices.Add("Eugene");
        if (InstallXeniaCheck.IsChecked == true)
            voices.Add("Xenia");

        SummaryText.Text =
            $"Папка: {InstallPathBox.Text}\n" +
            $"Голоса: {string.Join(", ", voices)}\n" +
            $"Основной: {(DefaultVoiceCombo.SelectedItem as VoiceChoice)?.Text ?? "не выбран"}";
    }

    private void AnimatePage(FrameworkElement page)
    {
        page.Opacity = 0;
        page.RenderTransform = new TranslateTransform(22, 0);

        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(240);

        page.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(0, 1, duration) { EasingFunction = easing });

        ((TranslateTransform)page.RenderTransform).BeginAnimation(
            TranslateTransform.XProperty,
            new DoubleAnimation(22, 0, duration) { EasingFunction = easing });
    }

    private async Task InstallAsync()
    {
        if (string.IsNullOrWhiteSpace(InstallPathBox.Text))
        {
            ShowWarning("Выбери папку установки.");
            return;
        }

        if (InstallEugeneCheck.IsChecked != true &&
            InstallXeniaCheck.IsChecked != true)
        {
            ShowWarning("Выбери хотя бы один голос.");
            return;
        }

        if (DefaultVoiceCombo.SelectedItem is not VoiceChoice activeVoice)
        {
            ShowWarning("Выбери голос по умолчанию.");
            return;
        }

        _busy = true;
        SetNavigationEnabled(false);
        LocationPage.Visibility = Visibility.Collapsed;
        VoicesPage.Visibility = Visibility.Collapsed;
        OptionsPage.Visibility = Visibility.Collapsed;
        ProgressPage.Visibility = Visibility.Visible;
        CompletePage.Visibility = Visibility.Collapsed;
        AnimatePage(ProgressPage);
        StartProgressAnimation();

        try
        {
            var progress = new Progress<InstallProgress>(item =>
            {
                InstallProgressBar.Value = Math.Clamp(item.Percent, 0, 100);
                ProgressPercentText.Text = $"{Math.Clamp(item.Percent, 0, 100)}%";
                ProgressStatusText.Text = item.Message;
            });

            var crewChiefMessage = await InstallerEngine.InstallAsync(
                new InstallOptions(
                    InstallPathBox.Text,
                    DesktopShortcutCheck.IsChecked == true,
                    AutoStartCheck.IsChecked == true,
                    ConfigureCrewChiefCheck.IsChecked == true,
                    LaunchAfterCheck.IsChecked == true,
                    InstallEugeneCheck.IsChecked == true,
                    InstallXeniaCheck.IsChecked == true,
                    activeVoice.Id),
                progress,
                _cancellation.Token);

            StopProgressAnimation();
            ProgressPage.Visibility = Visibility.Collapsed;
            CompletePage.Visibility = Visibility.Visible;

            var voices = new List<string>();
            if (InstallEugeneCheck.IsChecked == true)
                voices.Add("Eugene");
            if (InstallXeniaCheck.IsChecked == true)
                voices.Add("Xenia");

            CompleteSummaryText.Text =
                $"Установлены голоса: {string.Join(", ", voices)}.\n" +
                $"Основной голос: {activeVoice.Text}." +
                (string.IsNullOrWhiteSpace(crewChiefMessage)
                    ? string.Empty
                    : $"\n\n{crewChiefMessage}");

            AnimatePage(CompletePage);
            NextButton.Content = "Закрыть";
            NextButton.Visibility = Visibility.Visible;
            BackButton.Visibility = Visibility.Collapsed;
            OpenLogButton.Visibility = Visibility.Visible;
            RemoveButton.Visibility = Visibility.Collapsed;
        }
        catch (OperationCanceledException)
        {
            ProgressStatusText.Text = "Установка отменена";
            SetNavigationEnabled(true);
            ShowStep(_step);
        }
        catch (Exception exception)
        {
            InstallerLog.WriteException("Installation failed", exception);
            StopProgressAnimation();
            SetNavigationEnabled(true);
            ShowStep(_step);

            MessageBox.Show(
                this,
                $"Установка не завершена.\n\n{exception.Message}\n\nЖурнал:\n{InstallerLog.CurrentLogPath}",
                "Ошибка установки",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _busy = false;
        }
    }

    private void SetNavigationEnabled(bool enabled)
    {
        NextButton.IsEnabled = enabled;
        BackButton.IsEnabled = enabled;
        OpenLogButton.IsEnabled = true;
        RemoveButton.IsEnabled = enabled;
    }

    private void StartProgressAnimation()
    {
        ProgressPulse.RenderTransformOrigin = new Point(0.5, 0.5);
        var transform = new ScaleTransform(1, 1);
        ProgressPulse.RenderTransform = transform;

        var scale = new DoubleAnimation(1, 1.55, TimeSpan.FromMilliseconds(900))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase()
        };

        transform.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, scale);
    }

    private void StopProgressAnimation()
    {
        if (ProgressPulse.RenderTransform is not ScaleTransform transform)
            return;

        transform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
    }

    private static bool IsVoiceInstalled(string voiceId)
    {
        var directory = Path.Combine(
            InstallPaths.DataDirectory,
            "audio",
            $"voice_bank_{voiceId}_radio_v1");

        return File.Exists(Path.Combine(directory, "READY.json")) &&
               File.Exists(Path.Combine(directory, "phrases", "unknown.wav")) &&
               File.Exists(Path.Combine(directory, "numbers", "0.wav"));
    }

    private static string ReadCurrentVoiceId()
    {
        var configPath = Path.Combine(InstallPaths.DataDirectory, "appsettings.json");

        try
        {
            if (!File.Exists(configPath))
                return "eugene";

            using var document = JsonDocument.Parse(File.ReadAllText(configPath));

            if (document.RootElement.TryGetProperty("VoiceId", out var voice) &&
                string.Equals(voice.GetString(), "xenia", StringComparison.OrdinalIgnoreCase))
            {
                return "xenia";
            }
        }
        catch
        {
        }

        return "eugene";
    }

    private void ShowWarning(string message) =>
        MessageBox.Show(
            this,
            message,
            "Проверь параметры",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Выбери папку установки",
            InitialDirectory = InstallPathBox.Text,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
            InstallPathBox.Text = dialog.FolderName;
    }

    private async void Next_Click(object sender, RoutedEventArgs e)
    {
        if (CompletePage.Visibility == Visibility.Visible)
        {
            Close();
            return;
        }

        if (_step < 2)
        {
            ShowStep(_step + 1);
            return;
        }

        await InstallAsync();
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_step > 0)
            ShowStep(_step - 1);
    }

    private void VoiceSelectionChanged(object sender, RoutedEventArgs e)
    {
        UpdateDefaultVoiceChoices();
        UpdateVoiceCards();
    }

    private void EugeneCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (FindParentCheckBox(e.OriginalSource as DependencyObject))
            return;

        InstallEugeneCheck.IsChecked = InstallEugeneCheck.IsChecked != true;
    }

    private void XeniaCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (FindParentCheckBox(e.OriginalSource as DependencyObject))
            return;

        InstallXeniaCheck.IsChecked = InstallXeniaCheck.IsChecked != true;
    }

    private static bool FindParentCheckBox(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is CheckBox)
                return true;

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void OpenLog_Click(object sender, RoutedEventArgs e) => InstallerLog.Open();

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var setupPath = Environment.ProcessPath
                            ?? throw new InvalidOperationException("Не удалось определить путь установщика.");

            Process.Start(new ProcessStartInfo
            {
                FileName = setupPath,
                ArgumentList = { "--uninstall" },
                UseShellExecute = true
            });

            Close();
        }
        catch (Exception exception)
        {
            InstallerLog.WriteException("Unable to open uninstaller", exception);
            MessageBox.Show(this, exception.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        var mode = ThemeCombo.SelectedIndex switch
        {
            1 => InstallerThemeMode.Light,
            2 => InstallerThemeMode.Dark,
            _ => InstallerThemeMode.System
        };

        ThemeService.Apply(mode);
        RefreshThemeDependentVisuals();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!_busy)
            return;

        var answer = MessageBox.Show(
            this,
            "Установка ещё выполняется. Отменить её?",
            "Отмена установки",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer != MessageBoxResult.Yes)
        {
            e.Cancel = true;
            return;
        }

        _cancellation.Cancel();
    }
}
