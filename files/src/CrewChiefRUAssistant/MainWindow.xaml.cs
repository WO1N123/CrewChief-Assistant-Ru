using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using CrewChiefRUAssistant.Input;
using AppInputBinding = CrewChiefRUAssistant.Input.InputBinding;
using CrewChiefRUAssistant.Recognition;
using CrewChiefRUAssistant.Responses;
using CrewChiefRUAssistant.Shared;
using NAudio.Wave;

namespace CrewChiefRUAssistant;

public partial class MainWindow : Window
{
    private sealed record VoiceOption(
        string Id,
        string DisplayName,
        bool Installed)
    {
        public override string ToString() =>
            Installed
                ? $"{DisplayName}  ✓"
                : $"{DisplayName}  — не установлен";
    }

    private readonly AppConfig _config;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly DispatcherTimer _statusTimer = new();
    private readonly SemaphoreSlim _assistantLifecycleGate = new(1, 1);

    private AssistantController? _controller;
    private TrayIconService? _trayIcon;
    private AppInputBinding _selectedBinding;
    private FrameworkElement? _activePage;
    private bool _exitRequested;
    private bool _updatingUi;
    private bool _finalizingClose;
    private VoiceBankPlayer? _voicePreviewPlayer;

    public MainWindow(AppConfig config)
    {
        InitializeComponent();

        _config = config;
        _selectedBinding = config.GetPttBinding();

        ThemeService.ThemeChanged += ThemeService_ThemeChanged;
        InitializeTrayIcon();

        _statusTimer.Interval = TimeSpan.FromSeconds(1);
        _statusTimer.Tick += (_, _) => RefreshStatus();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ThemeService.ApplyWindowChrome(this);
        LoadSettingsIntoControls();
        ShowPage(DashboardPage, DashboardNav, "Главная");

        _statusTimer.Start();
        AppendLog("Программа готова. Нажми «Запустить».");
        await EnsureModelAsync(downloadOnlyWhenMissing: true);
    }

    private void LoadSettingsIntoControls()
    {
        _updatingUi = true;

        try
        {
            MicrophoneCombo.Items.Clear();

            try
            {
                foreach (var device in VoskPushToTalkRecognizer.GetRecordingDevices())
                    MicrophoneCombo.Items.Add(device);
            }
            catch (Exception exception)
            {
                AppendLog($"Не удалось получить список микрофонов: {exception.Message}");
            }

            if (MicrophoneCombo.Items.Count == 0)
                MicrophoneCombo.Items.Add("0: устройство Windows по умолчанию");

            MicrophoneCombo.SelectedIndex = Math.Clamp(
                _config.MicrophoneDevice,
                0,
                MicrophoneCombo.Items.Count - 1);

            BindingText.Text = _selectedBinding.DisplayName;
            MqttPortBox.Text = _config.MqttPort.ToString();
            VolumeSlider.Value = Math.Clamp(_config.SpeechVolumePercent, 0, 100);

            PlaybackCombo.Items.Clear();
            PlaybackCombo.Items.Add("Windows: устройство по умолчанию");

            try
            {
                for (var device = 0; device < WaveOut.DeviceCount; device++)
                {
                    var capabilities = WaveOut.GetCapabilities(device);
                    PlaybackCombo.Items.Add($"{device}: {capabilities.ProductName}");
                }
            }
            catch (Exception exception)
            {
                AppendLog($"Не удалось получить устройства вывода: {exception.Message}");
            }

            PlaybackCombo.SelectedIndex = Math.Clamp(
                _config.PlaybackDevice + 1,
                0,
                PlaybackCombo.Items.Count - 1);

            SpeechEnabledToggle.IsChecked = _config.SpeechEnabled;
            CrewChiefPriorityToggle.IsChecked = _config.CrewChiefVoicePriority;
            MinimizeToTrayToggle.IsChecked = _config.MinimizeToTray;

            ThemeCombo.SelectedIndex = _config.ThemeMode switch
            {
                AppThemeMode.Light => 1,
                AppThemeMode.Dark => 2,
                _ => 0
            };

            RefreshVoiceAvailability(persistFallback: true);
        }
        finally
        {
            _updatingUi = false;
        }
    }

    private bool SaveControlsIntoSettings(bool showValidation = true)
    {
        if (!int.TryParse(MqttPortBox.Text, out var port) ||
            port is < 1 or > 65535)
        {
            if (showValidation)
            {
                MessageBox.Show(
                    this,
                    "Порт MQTT должен быть числом от 1 до 65535.",
                    "Проверь настройки",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            return false;
        }

        _config.MicrophoneDevice = Math.Max(0, MicrophoneCombo.SelectedIndex);
        _config.PttBinding = _selectedBinding.Clone();
        _config.MqttPort = port;
        _config.PlaybackDevice = PlaybackCombo.SelectedIndex - 1;
        _config.SpeechEnabled = SpeechEnabledToggle.IsChecked == true;
        _config.SpeechVolumePercent = (int)Math.Round(VolumeSlider.Value);
        _config.CrewChiefVoicePriority = CrewChiefPriorityToggle.IsChecked == true;
        _config.MinimizeToTray = MinimizeToTrayToggle.IsChecked == true;

        if (VoiceCombo.SelectedItem is VoiceOption voice && voice.Installed)
            _config.VoiceId = voice.Id;

        _config.ThemeMode = ThemeCombo.SelectedIndex switch
        {
            1 => AppThemeMode.Light,
            2 => AppThemeMode.Dark,
            _ => AppThemeMode.System
        };

        _config.Save();
        return true;
    }

    private void RefreshVoiceAvailability(bool persistFallback)
    {
        var eugeneInstalled = AppConfig.IsVoiceInstalled("eugene");
        var xeniaInstalled = AppConfig.IsVoiceInstalled("xenia");

        ApplyVoiceBadge(EugeneStatusBadge, EugeneStatusText, eugeneInstalled);
        ApplyVoiceBadge(XeniaStatusBadge, XeniaStatusText, xeniaInstalled);

        UseEugeneButton.IsEnabled = eugeneInstalled;
        UseXeniaButton.IsEnabled = xeniaInstalled;
        PreviewEugeneButton.IsEnabled = eugeneInstalled;
        PreviewXeniaButton.IsEnabled = xeniaInstalled;

        var preferred = AppConfig.NormalizeVoiceId(_config.VoiceId);

        if (!AppConfig.IsVoiceInstalled(preferred))
        {
            if (eugeneInstalled)
                preferred = "eugene";
            else if (xeniaInstalled)
                preferred = "xenia";
        }

        _updatingUi = true;

        try
        {
            VoiceCombo.Items.Clear();
            VoiceCombo.Items.Add(new VoiceOption("eugene", "Мужской — Eugene", eugeneInstalled));
            VoiceCombo.Items.Add(new VoiceOption("xenia", "Женский — Xenia", xeniaInstalled));
            VoiceCombo.SelectedIndex = preferred == "xenia" ? 1 : 0;
        }
        finally
        {
            _updatingUi = false;
        }

        if (persistFallback &&
            !string.Equals(_config.VoiceId, preferred, StringComparison.Ordinal))
        {
            _config.VoiceId = preferred;
            _config.Save();
        }

        ActiveVoiceText.Text = AppConfig.GetVoiceDisplayName(preferred);
        HighlightActiveVoice(preferred);
    }

    private static void ApplyVoiceBadge(
        Border badge,
        TextBlock text,
        bool installed)
    {
        text.Text = installed ? "Установлен и готов" : "Не установлен";
        text.Foreground = (Brush)Application.Current.FindResource(
            installed ? "SuccessBrush" : "DangerBrush");
        badge.Background = (Brush)Application.Current.FindResource(
            installed ? "SuccessSoftBrush" : "DangerSoftBrush");
    }

    private void HighlightActiveVoice(string voiceId)
    {
        EugeneCard.BorderBrush = (Brush)Application.Current.FindResource(
            voiceId == "eugene" ? "AccentBrush" : "BorderBrush");

        EugeneCard.BorderThickness = voiceId == "eugene"
            ? new Thickness(2)
            : new Thickness(1);

        XeniaCard.BorderBrush = (Brush)Application.Current.FindResource(
            voiceId == "xenia" ? "AccentBrush" : "BorderBrush");

        XeniaCard.BorderThickness = voiceId == "xenia"
            ? new Thickness(2)
            : new Thickness(1);
    }

    private async Task StartAssistantAsync()
    {
        if (!await _assistantLifecycleGate.WaitAsync(0))
            return;

        try
        {
            await StartAssistantCoreAsync();
        }
        finally
        {
            _assistantLifecycleGate.Release();
        }
    }

    private async Task StartAssistantCoreAsync()
    {
        if (_controller?.IsRunning == true)
        {
            SetRunningState(true);
            return;
        }

        if (!SaveControlsIntoSettings())
            return;

        if (_config.SpeechEnabled && !AppConfig.IsVoiceInstalled(_config.VoiceId))
        {
            MessageBox.Show(
                this,
                $"Голос {_config.GetVoiceDisplayName()} не установлен.\n\nОткрой раздел «Голоса» и добавь его через установщик.",
                "Голос не установлен",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            ShowPage(VoicePage, VoiceNav, "Голоса");
            return;
        }

        if (!await EnsureModelAsync(downloadOnlyWhenMissing: false))
            return;

        try
        {
            SetRunningState(true);

            if (_controller is null)
            {
                _controller = new AssistantController(_config);
                _controller.Log += ControllerOnLog;
                _controller.ListeningChanged += ControllerOnListeningChanged;
                _controller.QuestionAnswered += ControllerOnQuestionAnswered;
            }

            await _controller.StartAsync();

            StateText.Text = "Работает";
            HeroStatusText.Text = "Ассистент на связи";
            HeroSubtitleText.Text = "Удерживай назначенную кнопку и задай вопрос по-русски.";
            AppendLog("Ассистент запущен.");
        }
        catch (Exception exception)
        {
            AppendLog($"Ошибка запуска: {exception.Message}");

            MessageBox.Show(
                this,
                exception.Message,
                "Не удалось запустить ассистент",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            await StopAssistantCoreAsync(disposeController: true);
        }
    }

    private async Task StopAssistantAsync(bool disposeController = false)
    {
        await _assistantLifecycleGate.WaitAsync();

        try
        {
            await StopAssistantCoreAsync(disposeController);
        }
        finally
        {
            _assistantLifecycleGate.Release();
        }
    }

    private async Task StopAssistantCoreAsync(bool disposeController = false)
    {
        if (_controller is not null)
        {
            if (disposeController)
            {
                _controller.Log -= ControllerOnLog;
                _controller.ListeningChanged -= ControllerOnListeningChanged;
                _controller.QuestionAnswered -= ControllerOnQuestionAnswered;
                await _controller.DisposeAsync();
                _controller = null;
            }
            else
            {
                await _controller.StopAsync(preserveTelemetryConnection: true);
            }
        }

        SetRunningState(false);
        StateText.Text = "Остановлен";
        HeroStatusText.Text = "Ассистент остановлен";
        HeroSubtitleText.Text = "Нажми «Запустить», когда CrewChief и игра готовы.";
        StopListeningAnimation();
    }

    private void SetRunningState(bool running)
    {
        StartButton.IsEnabled = !running;
        StopButton.IsEnabled = running;

        MicrophoneCombo.IsEnabled = !running;
        MqttPortBox.IsEnabled = !running;
        PlaybackCombo.IsEnabled = !running;
        SpeechEnabledToggle.IsEnabled = !running;
        CrewChiefPriorityToggle.IsEnabled = !running;
        VoiceCombo.IsEnabled = !running;
        UseEugeneButton.IsEnabled = !running && AppConfig.IsVoiceInstalled("eugene");
        UseXeniaButton.IsEnabled = !running && AppConfig.IsVoiceInstalled("xenia");
    }

    private async Task<bool> EnsureModelAsync(bool downloadOnlyWhenMissing)
    {
        if (ModelInstaller.IsInstalled)
        {
            ModelStatusText.Text = "Русская модель Vosk установлена";
            ModelProgress.Visibility = Visibility.Collapsed;
            return true;
        }

        if (!downloadOnlyWhenMissing)
        {
            var answer = MessageBox.Show(
                this,
                "Для распознавания нужно один раз скачать русскую модель Vosk (около 45 МБ). Продолжить?",
                "Загрузка модели",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (answer != MessageBoxResult.Yes)
                return false;
        }

        try
        {
            StartButton.IsEnabled = false;
            ModelProgress.Visibility = Visibility.Visible;
            ModelProgress.Value = 0;
            ModelStatusText.Text = "Загрузка русской модели Vosk…";

            var progress = new Progress<int>(value =>
            {
                ModelProgress.Value = Math.Clamp(value, 0, 100);
                ModelStatusText.Text = value < 92
                    ? $"Загрузка русской модели Vosk: {value}%"
                    : "Распаковка русской модели Vosk…";
            });

            await ModelInstaller.EnsureInstalledAsync(progress, _shutdown.Token);

            ModelProgress.Value = 100;
            ModelProgress.Visibility = Visibility.Collapsed;
            ModelStatusText.Text = "Русская модель Vosk установлена";
            AppendLog("Русская модель Vosk установлена.");
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception exception)
        {
            AppendLog($"Ошибка установки модели: {exception.Message}");

            MessageBox.Show(
                this,
                exception.Message,
                "Не удалось установить модель Vosk",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return false;
        }
        finally
        {
            StartButton.IsEnabled = _controller is null;
        }
    }

    private void ControllerOnLog(object? sender, string message) =>
        Dispatcher.Invoke(() => AppendLog(message));

    private void ControllerOnListeningChanged(object? sender, bool listening) =>
        Dispatcher.Invoke(() =>
        {
            if (listening)
            {
                HeroStatusText.Text = "Слушаю вопрос…";
                HeroSubtitleText.Text = "Говори естественно. После отпускания кнопки начнётся обработка.";
                StartListeningAnimation();
            }
            else
            {
                HeroStatusText.Text = "Ассистент на связи";
                HeroSubtitleText.Text = "Удерживай назначенную кнопку и задай вопрос по-русски.";
                StopListeningAnimation();
            }
        });

    private void ControllerOnQuestionAnswered(
        object? sender,
        QuestionAnsweredEventArgs args) =>
        Dispatcher.Invoke(() =>
        {
            LastQuestionText.Text = args.Question;
            LastQuestionText.Foreground = (Brush)Application.Current.FindResource("TextPrimaryBrush");
            LastAnswerText.Text = args.Answer;
            LastAnswerText.Foreground = (Brush)Application.Current.FindResource("TextPrimaryBrush");
            AppendLog($"Вопрос: {args.Question}");
            AppendLog($"Ответ: {args.Answer}");
        });

    private void RefreshStatus()
    {
        using var process = Process.GetCurrentProcess();
        MemoryText.Text = $"{process.WorkingSet64 / 1024d / 1024d:F0} МБ";

        var stats = _controller?.GetStats();
        MqttText.Text = stats is null
            ? "0 сообщений"
            : $"{stats.MqttMessages} сообщений";
    }

    private void AppendLog(string message)
    {
        if (!IsLoaded || LogBox.Document is null)
            return;

        var brushKey =
            message.StartsWith("Ошибка", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("не удалось", StringComparison.OrdinalIgnoreCase)
                ? "DangerBrush"
                : message.StartsWith("Вопрос:", StringComparison.OrdinalIgnoreCase)
                    ? "AccentBrush"
                    : message.StartsWith("Ответ:", StringComparison.OrdinalIgnoreCase)
                        ? "SuccessBrush"
                        : message.Contains("предупреж", StringComparison.OrdinalIgnoreCase)
                            ? "WarningBrush"
                            : "TextPrimaryBrush";

        var paragraph = new Paragraph
        {
            Margin = new Thickness(0, 0, 0, 5),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12
        };

        paragraph.Inlines.Add(
            new Run($"[{DateTime.Now:HH:mm:ss}] ")
            {
                Foreground = (Brush)Application.Current.FindResource("TextSecondaryBrush")
            });

        paragraph.Inlines.Add(
            new Run(message)
            {
                Foreground = (Brush)Application.Current.FindResource(brushKey)
            });

        LogBox.Document.Blocks.Add(paragraph);
        LogBox.ScrollToEnd();
    }

    private void StartListeningAnimation()
    {
        var transform = ListeningPulse.RenderTransform as ScaleTransform;

        if (transform is null)
        {
            transform = new ScaleTransform(1, 1);
            ListeningPulse.RenderTransform = transform;
            ListeningPulse.RenderTransformOrigin = new Point(0.5, 0.5);
        }

        var scale = new DoubleAnimation(1, 1.75, TimeSpan.FromMilliseconds(850))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase()
        };

        var opacity = new DoubleAnimation(0.28, 0.05, TimeSpan.FromMilliseconds(850))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };

        transform.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, scale);
        ListeningPulse.BeginAnimation(OpacityProperty, opacity);
    }

    private void StopListeningAnimation()
    {
        if (ListeningPulse.RenderTransform is ScaleTransform transform)
        {
            transform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            transform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            transform.ScaleX = 1;
            transform.ScaleY = 1;
        }

        ListeningPulse.BeginAnimation(OpacityProperty, null);
        ListeningPulse.Opacity = 0.18;
    }

    private void ShowPage(
        FrameworkElement page,
        Button navigationButton,
        string title)
    {
        foreach (var candidate in new FrameworkElement[]
                 {
                     DashboardPage,
                     VoicePage,
                     SettingsPage,
                     LogsPage
                 })
        {
            candidate.Visibility = candidate == page
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        foreach (var button in new[]
                 {
                     DashboardNav,
                     VoiceNav,
                     SettingsNav,
                     LogsNav
                 })
        {
            button.Tag = button == navigationButton
                ? "Selected"
                : null;
        }

        WindowTitleText.Text = title;
        _activePage = page;

        page.Opacity = 0;
        page.RenderTransform = new TranslateTransform(18, 0);

        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(220);

        page.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(0, 1, duration) { EasingFunction = easing });

        ((TranslateTransform)page.RenderTransform).BeginAnimation(
            TranslateTransform.XProperty,
            new DoubleAnimation(18, 0, duration) { EasingFunction = easing });
    }

    private async Task ConfigureCrewChiefAsync()
    {
        try
        {
            AppendLog("Поиск конфигурации CrewChief…");

            var result = await CrewChiefMqttConfigurator.ConfigureAsync(_shutdown.Token);
            AppendLog(result.Message);

            MessageBox.Show(
                this,
                result.Message +
                (result.Found
                    ? "\n\nПосле изменения полностью перезапусти CrewChief."
                    : string.Empty),
                "Настройка CrewChief",
                MessageBoxButton.OK,
                result.Found ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            AppendLog($"Ошибка настройки CrewChief: {exception.Message}");

            MessageBox.Show(
                this,
                exception.Message,
                "Не удалось настроить CrewChief",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void AssignPttBinding()
    {
        var dialog = new InputBindingCaptureWindow
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true || dialog.SelectedBinding is null)
            return;

        _selectedBinding = dialog.SelectedBinding.Clone();
        BindingText.Text = _selectedBinding.DisplayName;
        _config.PttBinding = _selectedBinding.Clone();
        _config.Save();
        AppendLog($"Назначена кнопка разговора: {_selectedBinding.DisplayName}");
    }

    private void OpenVoiceInstaller()
    {
        var setupPath = Path.Combine(AppContext.BaseDirectory, "CrewChiefRU_Setup.exe");

        if (!File.Exists(setupPath))
        {
            var legacy = Path.Combine(AppContext.BaseDirectory, "CrewChiefRUAssistant_Setup.exe");
            if (File.Exists(legacy))
                setupPath = legacy;
        }

        if (!File.Exists(setupPath))
        {
            MessageBox.Show(
                this,
                "Установщик не найден рядом с программой.\n\nЗапусти скачанный CrewChiefRU_Setup.exe, чтобы изменить набор голосов.",
                "Установщик не найден",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var answer = MessageBox.Show(
            this,
            "Откроется установщик для добавления или удаления Eugene и Xenia. Приложение будет закрыто.",
            "Изменение голосов",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer != MessageBoxResult.Yes)
            return;

        Process.Start(new ProcessStartInfo
        {
            FileName = setupPath,
            UseShellExecute = true
        });

        _exitRequested = true;
        Close();
    }

    private void UseVoice(string voiceId)
    {
        if (!AppConfig.IsVoiceInstalled(voiceId))
        {
            MessageBox.Show(
                this,
                $"Голос {AppConfig.GetVoiceDisplayName(voiceId)} не установлен.",
                "Голос недоступен",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        _config.VoiceId = voiceId;
        _config.Save();
        RefreshVoiceAvailability(persistFallback: false);
        AppendLog($"Активный голос: {AppConfig.GetVoiceDisplayName(voiceId)}.");
    }

    private void InitializeTrayIcon()
    {
        _trayIcon = new TrayIconService(
            this,
            RestoreFromTray,
            async () =>
            {
                RestoreFromTray();
                if (_controller?.IsRunning != true)
                    await StartAssistantAsync();
            },
            () => StopAssistantAsync(),
            ExitApplicationAsync);
    }

    internal void ActivateFromSecondInstance()
    {
        RestoreFromTray();

        // A short Topmost pulse reliably brings a minimized or covered WPF
        // window to the front without leaving it permanently above other apps.
        Topmost = true;
        Activate();
        Topmost = false;
        Focus();
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private async Task ExitApplicationAsync()
    {
        _exitRequested = true;
        await StopAssistantAsync();
        Close();
    }

    private void ThemeService_ThemeChanged(object? sender, EventArgs e)
    {
        ThemeService.ApplyWindowChrome(this);
        RefreshVoiceAvailability(persistFallback: false);
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingUi || !IsLoaded)
            return;

        _config.ThemeMode = ThemeCombo.SelectedIndex switch
        {
            1 => AppThemeMode.Light,
            2 => AppThemeMode.Dark,
            _ => AppThemeMode.System
        };

        _config.Save();
        ThemeService.Apply(_config.ThemeMode);
    }

    private void VoiceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingUi || VoiceCombo.SelectedItem is not VoiceOption voice)
            return;

        if (!voice.Installed)
        {
            MessageBox.Show(
                this,
                $"Голос {AppConfig.GetVoiceDisplayName(voice.Id)} не установлен. Открой раздел «Голоса», чтобы добавить его.",
                "Голос не установлен",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            RefreshVoiceAvailability(persistFallback: false);
            return;
        }

        UseVoice(voice.Id);
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized && _config.MinimizeToTray)
            Hide();
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!_exitRequested && _config.MinimizeToTray)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        if (_finalizingClose)
            return;

        e.Cancel = true;
        _finalizingClose = true;

        ThemeService.ThemeChanged -= ThemeService_ThemeChanged;
        _statusTimer.Stop();
        _shutdown.Cancel();
        await StopAssistantAsync(disposeController: true);

        _voicePreviewPlayer?.Dispose();
        _voicePreviewPlayer = null;

        _trayIcon?.Dispose();
        _trayIcon = null;

        Application.Current.Shutdown();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void DashboardNav_Click(object sender, RoutedEventArgs e) =>
        ShowPage(DashboardPage, DashboardNav, "Главная");

    private void VoiceNav_Click(object sender, RoutedEventArgs e) =>
        ShowPage(VoicePage, VoiceNav, "Голоса");

    private void SettingsNav_Click(object sender, RoutedEventArgs e) =>
        ShowPage(SettingsPage, SettingsNav, "Настройки");

    private void LogsNav_Click(object sender, RoutedEventArgs e) =>
        ShowPage(LogsPage, LogsNav, "Журнал");

    private async void StartButton_Click(object sender, RoutedEventArgs e) =>
        await StartAssistantAsync();

    private async void StopButton_Click(object sender, RoutedEventArgs e) =>
        await StopAssistantAsync();

    private void BindButton_Click(object sender, RoutedEventArgs e) => AssignPttBinding();

    private void UseEugeneButton_Click(object sender, RoutedEventArgs e) => UseVoice("eugene");

    private void UseXeniaButton_Click(object sender, RoutedEventArgs e) => UseVoice("xenia");

    private void PreviewEugeneButton_Click(object sender, RoutedEventArgs e) =>
        PreviewVoice("eugene");

    private void PreviewXeniaButton_Click(object sender, RoutedEventArgs e) =>
        PreviewVoice("xenia");

    private void PreviewVoice(string voiceId)
    {
        if (!AppConfig.IsVoiceInstalled(voiceId))
        {
            MessageBox.Show(
                this,
                $"Голос {AppConfig.GetVoiceDisplayName(voiceId)} не установлен.",
                "Предварительное прослушивание",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        try
        {
            _voicePreviewPlayer?.Dispose();

            _voicePreviewPlayer =
                new VoiceBankPlayer(
                    AppConfig.GetVoiceBankDirectory(voiceId),
                    _config.PlaybackDevice,
                    _config.SpeechVolumePercent / 100f);

            _voicePreviewPlayer.Play(
                ["phrases/preview"]);

            AppendLog(
                $"Предварительное прослушивание: {AppConfig.GetVoiceDisplayName(voiceId)}.");
        }
        catch (Exception exception)
        {
            AppendLog(
                $"Ошибка прослушивания голоса: {exception.Message}");

            MessageBox.Show(
                this,
                exception.Message,
                "Не удалось воспроизвести голос",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ManageVoicesButton_Click(object sender, RoutedEventArgs e) => OpenVoiceInstaller();

    private void OpenDataButton_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(AppConfig.DataDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = AppConfig.DataDirectory,
            UseShellExecute = true
        });
    }

    private async void ConfigureCrewChiefButton_Click(object sender, RoutedEventArgs e) =>
        await ConfigureCrewChiefAsync();

    private void ShowFieldsButton_Click(object sender, RoutedEventArgs e)
    {
        var fields = _controller?.GetRecentFields(250);

        if (fields is null || fields.Count == 0)
        {
            MessageBox.Show(
                this,
                "Телеметрия пока не поступала. Запусти CrewChief и игру либо нажми «Тестовые данные».",
                "Поля телеметрии",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var window = new FieldsWindow(fields)
        {
            Owner = this
        };

        window.ShowDialog();
    }

    private void TestDataButton_Click(object sender, RoutedEventArgs e) =>
        _controller?.LoadTestData();

    private void ClearLogButton_Click(object sender, RoutedEventArgs e) =>
        LogBox.Document.Blocks.Clear();
}
