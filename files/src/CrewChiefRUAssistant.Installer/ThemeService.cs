using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Win32;

namespace CrewChiefRUAssistant.Installer;

internal static class ThemeService
{
    private static readonly Uri LightTheme = new("Themes/Light.xaml", UriKind.Relative);
    private static readonly Uri DarkTheme = new("Themes/Dark.xaml", UriKind.Relative);
    private static InstallerThemeMode _mode = InstallerThemeMode.System;
    private static bool _initialized;

    public static InstallerThemeMode Mode => _mode;

    public static bool IsDark =>
        _mode == InstallerThemeMode.Dark ||
        (_mode == InstallerThemeMode.System && IsSystemDark());

    public static void Initialize(InstallerThemeMode mode)
    {
        if (!_initialized)
        {
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            _initialized = true;
        }

        Apply(mode);
    }

    public static void Apply(InstallerThemeMode mode)
    {
        _mode = mode;

        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var current = dictionaries.FirstOrDefault(dictionary =>
            dictionary.Source is not null &&
            (dictionary.Source.OriginalString.EndsWith("Light.xaml", StringComparison.OrdinalIgnoreCase) ||
             dictionary.Source.OriginalString.EndsWith("Dark.xaml", StringComparison.OrdinalIgnoreCase)));

        if (current is not null)
            dictionaries.Remove(current);

        dictionaries.Add(new ResourceDictionary
        {
            Source = IsDark ? DarkTheme : LightTheme
        });

        foreach (Window window in Application.Current.Windows)
            ApplyWindowChrome(window);
    }

    public static void ApplyWindowChrome(Window window)
    {
        try
        {
            var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero)
                return;

            var dark = IsDark ? 1 : 0;

            var darkModeAttribute =
                OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18985)
                    ? 20
                    : 19;

            DwmSetWindowAttribute(
                handle,
                darkModeAttribute,
                ref dark,
                sizeof(int));

            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            {
                var corners = 2;
                DwmSetWindowAttribute(
                    handle,
                    33,
                    ref corners,
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
        }
    }

    public static void Shutdown()
    {
        if (!_initialized)
            return;

        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _initialized = false;
    }

    private static bool IsSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void OnUserPreferenceChanged(
        object sender,
        UserPreferenceChangedEventArgs e)
    {
        if (_mode != InstallerThemeMode.System)
            return;

        Application.Current.Dispatcher.Invoke(() => Apply(InstallerThemeMode.System));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int attribute,
        ref int value,
        int valueSize);
}
