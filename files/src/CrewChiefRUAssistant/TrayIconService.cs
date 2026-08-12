using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using Drawing = System.Drawing;

namespace CrewChiefRUAssistant;

internal sealed class TrayIconService : IDisposable
{
    private const uint NimAdd = 0x00000000;
    private const uint NimDelete = 0x00000002;
    private const uint NifMessage = 0x00000001;
    private const uint NifIcon = 0x00000002;
    private const uint NifTip = 0x00000004;
    private const int WmUser = 0x0400;
    private const int WmLButtonDblClk = 0x0203;
    private const int WmRButtonUp = 0x0205;

    private readonly Window _window;
    private readonly Action _restore;
    private readonly Func<Task> _start;
    private readonly Func<Task> _stop;
    private readonly Func<Task> _exit;
    private readonly ContextMenu _menu;

    private HwndSource? _source;
    private NotifyIconData _data;
    private bool _added;

    public TrayIconService(
        Window window,
        Action restore,
        Func<Task> start,
        Func<Task> stop,
        Func<Task> exit)
    {
        _window = window;
        _restore = restore;
        _start = start;
        _stop = stop;
        _exit = exit;

        _menu = BuildMenu();
        _window.SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(_window).Handle;
        _source = HwndSource.FromHwnd(handle);
        _source?.AddHook(WindowProc);

        _data = new NotifyIconData
        {
            cbSize = Marshal.SizeOf<NotifyIconData>(),
            hWnd = handle,
            uID = 1,
            uFlags = NifMessage | NifIcon | NifTip,
            uCallbackMessage = WmUser + 77,
            hIcon = Drawing.Icon.ExtractAssociatedIcon(
                Environment.ProcessPath!)?.Handle
                ?? Drawing.SystemIcons.Application.Handle,
            szTip = "CrewChief RU Assistant",
            szInfo = string.Empty,
            szInfoTitle = string.Empty
        };

        _added = ShellNotifyIcon(NimAdd, ref _data);
    }

    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu();

        var open = new MenuItem { Header = "Открыть" };
        open.Click += (_, _) => _restore();

        var start = new MenuItem { Header = "Запустить" };
        start.Click += async (_, _) => await _start();

        var stop = new MenuItem { Header = "Остановить" };
        stop.Click += async (_, _) => await _stop();

        var exit = new MenuItem { Header = "Выход" };
        exit.Click += async (_, _) => await _exit();

        menu.Items.Add(open);
        menu.Items.Add(start);
        menu.Items.Add(stop);
        menu.Items.Add(new Separator());
        menu.Items.Add(exit);

        return menu;
    }

    private IntPtr WindowProc(
        IntPtr hwnd,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (message != WmUser + 77)
            return IntPtr.Zero;

        switch (lParam.ToInt32())
        {
            case WmLButtonDblClk:
                _window.Dispatcher.BeginInvoke(_restore);
                handled = true;
                break;

            case WmRButtonUp:
                _window.Dispatcher.BeginInvoke(new Action(ShowMenu));
                handled = true;
                break;
        }

        return IntPtr.Zero;
    }

    private void ShowMenu()
    {
        GetCursorPos(out var point);
        _menu.Placement = PlacementMode.AbsolutePoint;
        _menu.HorizontalOffset = point.X;
        _menu.VerticalOffset = point.Y;
        _menu.IsOpen = true;
    }

    public void Dispose()
    {
        _window.SourceInitialized -= OnSourceInitialized;

        if (_source is not null)
        {
            _source.RemoveHook(WindowProc);
            _source = null;
        }

        if (_added)
        {
            ShellNotifyIcon(NimDelete, ref _data);
            _added = false;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;

        public uint dwState;
        public uint dwStateMask;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;

        public uint uTimeoutOrVersion;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;

        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(
        uint message,
        ref NotifyIconData data);

    private static bool ShellNotifyIcon(
        uint message,
        ref NotifyIconData data) =>
        Shell_NotifyIcon(message, ref data);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint point);
}
