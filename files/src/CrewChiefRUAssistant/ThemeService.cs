using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Win32;

namespace CrewChiefRUAssistant;

public static class ThemeService
{
    private static readonly Uri LightTheme =
        new("Themes/Light.xaml", UriKind.Relative);

    private static readonly Uri DarkTheme =
        new("Themes/Dark.xaml", UriKind.Relative);

    private static bool _initialized;
    private static AppThemeMode _mode = AppThemeMode.System;

    public static event EventHandler? ThemeChanged;

    public static AppThemeMode Mode => _mode;

    public static bool IsDark =>
        _mode == AppThemeMode.Dark ||
        (_mode == AppThemeMode.System && IsSystemDark());

    public static void Initialize(AppThemeMode mode)
    {
        if (!_initialized)
        {
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            _initialized = true;
        }

        Apply(mode);
    }

    public static void Apply(AppThemeMode mode)
    {
        _mode = mode;

        var dictionaries =
            Application.Current.Resources.MergedDictionaries;

        var current = dictionaries.FirstOrDefault(
            dictionary =>
                dictionary.Source is not null &&
                (dictionary.Source.OriginalString.EndsWith(
                     "Light.xaml",
                     StringComparison.OrdinalIgnoreCase) ||
                 dictionary.Source.OriginalString.EndsWith(
                     "Dark.xaml",
                     StringComparison.OrdinalIgnoreCase)));

        if (current is not null)
            dictionaries.Remove(current);

        dictionaries.Add(
            new ResourceDictionary
            {
                Source = IsDark
                    ? DarkTheme
                    : LightTheme
            });

        foreach (Window window in Application.Current.Windows)
            ApplyWindowChrome(window);

        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    public static bool IsSystemDark()
    {
        try
        {
            using var key =
                Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

            return key?.GetValue("AppsUseLightTheme") is int value &&
                   value == 0;
        }
        catch
        {
            return false;
        }
    }

    public static void ApplyWindowChrome(Window window)
    {
        try
        {
            var handle =
                new System.Windows.Interop.WindowInteropHelper(window).Handle;

            if (handle == IntPtr.Zero)
                return;

            var dark = IsDark ? 1 : 0;

            // Windows 10 1809 used attribute 19. Later Windows 10 builds
            // and Windows 11 use attribute 20.
            var darkModeAttribute =
                OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18985)
                    ? 20
                    : 19;

            DwmSetWindowAttribute(
                handle,
                darkModeAttribute,
                ref dark,
                sizeof(int));

            // Rounded DWM corners and system backdrops are Windows 11-only.
            // Windows 10 keeps the regular WPF surface and custom WindowChrome.
            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            {
                var cornerPreference = 2;
                DwmSetWindowAttribute(
                    handle,
                    33,
                    ref cornerPreference,
                    sizeof(int));

                var backdrop = 2;
                DwmSetWindowAttribute(
                    handle,
                    38,
                    ref backdrop,
                    sizeof(int));
            }
        }
        catch
        {
            // Older Windows builds simply ignore modern DWM attributes.
        }
    }

    public static void Shutdown()
    {
        if (!_initialized)
            return;

        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _initialized = false;
    }

    private static void OnUserPreferenceChanged(
        object sender,
        UserPreferenceChangedEventArgs e)
    {
        if (_mode != AppThemeMode.System)
            return;

        Application.Current.Dispatcher.Invoke(
            () => Apply(AppThemeMode.System));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int attribute,
        ref int value,
        int valueSize);
}
