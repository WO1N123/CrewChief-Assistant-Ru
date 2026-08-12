using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using CrewChiefRUAssistant.Input;
using AppInputBinding = CrewChiefRUAssistant.Input.InputBinding;
using System.Runtime.InteropServices;

namespace CrewChiefRUAssistant;

public partial class InputBindingCaptureWindow : Window
{
    private static readonly int[] MouseVirtualKeys =
    [
        0x01,
        0x02,
        0x04,
        0x05,
        0x06
    ];

    private static readonly int[] PreferredKeyboardVirtualKeys =
        BuildKeyboardScanOrder();

    private readonly InputBindingReader _reader = new();
    private readonly DispatcherTimer _timer = new();
    private readonly HashSet<int> _initialVirtualKeys = [];
    private readonly HashSet<(uint DeviceId, int Button)> _initialButtons = [];
    private readonly Dictionary<uint, int> _initialPov = [];

    private DateTime _captureStartsAt;

    public AppInputBinding? SelectedBinding { get; private set; }

    public InputBindingCaptureWindow()
    {
        InitializeComponent();

        _timer.Interval = TimeSpan.FromMilliseconds(12);
        _timer.Tick += (_, _) => PollInputs();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ThemeService.ApplyWindowChrome(this);
        StartPulseAnimation();

        var joysticks = _reader.GetJoystickSnapshots();

        DevicesText.Text = joysticks.Count == 0
            ? "Рули и геймпады через Windows сейчас не обнаружены."
            : "Обнаружено: " + string.Join(
                ", ",
                joysticks.Select(device =>
                    $"{device.Name} ({device.ButtonCount} кнопок)"));

        CaptureInitialState(joysticks);
        _captureStartsAt = DateTime.UtcNow.AddMilliseconds(450);
        _timer.Start();
    }

    private void Window_Closed(object? sender, EventArgs e) =>
        _timer.Stop();

    private void CaptureInitialState(
        IReadOnlyList<JoystickSnapshot> joysticks)
    {
        _initialVirtualKeys.Clear();
        _initialButtons.Clear();
        _initialPov.Clear();

        for (var virtualKey = 1; virtualKey <= 255; virtualKey++)
        {
            if (_reader.IsVirtualKeyDown(virtualKey))
                _initialVirtualKeys.Add(virtualKey);
        }

        foreach (var joystick in joysticks)
        {
            for (var button = 0; button < joystick.ButtonCount; button++)
            {
                if ((joystick.Buttons & (1u << button)) != 0)
                    _initialButtons.Add((joystick.Id, button));
            }

            if (joystick.PovDirection >= 0)
                _initialPov[joystick.Id] = joystick.PovDirection;
        }
    }

    private void PollInputs()
    {
        if (DateTime.UtcNow < _captureStartsAt)
            return;

        ReleaseInitialVirtualKeys();
        ReleaseInitialJoystickControls();

        var binding =
            TryCaptureJoystick() ??
            TryCaptureMouse() ??
            TryCaptureKeyboard();

        if (binding is null)
        {
            StatusText.Text = "Ожидание нажатия…";
            return;
        }

        SelectedBinding = binding;
        StatusText.Text = $"Назначено: {binding.DisplayName}";
        _timer.Stop();
        DialogResult = true;
        Close();
    }

    private AppInputBinding? TryCaptureJoystick()
    {
        foreach (var joystick in _reader.GetJoystickSnapshots())
        {
            for (var button = 0; button < joystick.ButtonCount; button++)
            {
                var key = (joystick.Id, button);
                var pressed = (joystick.Buttons & (1u << button)) != 0;

                if (pressed && !_initialButtons.Contains(key))
                {
                    return AppInputBinding.JoystickButton(
                        joystick.Id,
                        button,
                        joystick.Name);
                }
            }

            if (joystick.PovDirection < 0)
                continue;

            var isInitial =
                _initialPov.TryGetValue(joystick.Id, out var initialDirection) &&
                initialDirection == joystick.PovDirection;

            if (!isInitial)
            {
                return AppInputBinding.JoystickPov(
                    joystick.Id,
                    joystick.PovDirection,
                    joystick.Name);
            }
        }

        return null;
    }

    private AppInputBinding? TryCaptureMouse()
    {
        GetCursorPos(out var cursor);
        var topLeft = PointToScreen(new Point(0, 0));
        var inside =
            cursor.X >= topLeft.X &&
            cursor.Y >= topLeft.Y &&
            cursor.X <= topLeft.X + ActualWidth &&
            cursor.Y <= topLeft.Y + ActualHeight;

        if (inside)
            return null;

        foreach (var virtualKey in MouseVirtualKeys)
        {
            if (_reader.IsVirtualKeyDown(virtualKey) &&
                !_initialVirtualKeys.Contains(virtualKey))
            {
                return AppInputBinding.Mouse(virtualKey);
            }
        }

        return null;
    }

    private AppInputBinding? TryCaptureKeyboard()
    {
        foreach (var virtualKey in PreferredKeyboardVirtualKeys)
        {
            if (_reader.IsVirtualKeyDown(virtualKey) &&
                !_initialVirtualKeys.Contains(virtualKey))
            {
                return AppInputBinding.Keyboard(virtualKey);
            }
        }

        return null;
    }

    private void ReleaseInitialVirtualKeys() =>
        _initialVirtualKeys.RemoveWhere(
            virtualKey => !_reader.IsVirtualKeyDown(virtualKey));

    private void ReleaseInitialJoystickControls()
    {
        var snapshots = _reader.GetJoystickSnapshots();

        _initialButtons.RemoveWhere(item =>
        {
            var snapshot = snapshots.FirstOrDefault(
                joystick => joystick.Id == item.DeviceId);

            return snapshot is null ||
                   (snapshot.Buttons & (1u << item.Button)) == 0;
        });

        foreach (var deviceId in _initialPov.Keys.ToArray())
        {
            var snapshot = snapshots.FirstOrDefault(
                joystick => joystick.Id == deviceId);

            if (snapshot is null ||
                snapshot.PovDirection != _initialPov[deviceId])
            {
                _initialPov.Remove(deviceId);
            }
        }
    }

    private void StartPulseAnimation()
    {
        PulseRing.RenderTransformOrigin = new Point(0.5, 0.5);
        var transform = new ScaleTransform(1, 1);
        PulseRing.RenderTransform = transform;

        var animation = new DoubleAnimation(1, 1.35, TimeSpan.FromMilliseconds(900))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase()
        };

        transform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }

    private static int[] BuildKeyboardScanOrder()
    {
        var result = new List<int>
        {
            0xA0,
            0xA1,
            0xA2,
            0xA3,
            0xA4,
            0xA5
        };

        for (var virtualKey = 1; virtualKey <= 255; virtualKey++)
        {
            if (InputBindingFormatter.IsMouseVirtualKey(virtualKey))
                continue;

            if (virtualKey is 0x10 or 0x11 or 0x12)
                continue;

            if (!result.Contains(virtualKey))
                result.Add(virtualKey);
        }

        return result.ToArray();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint point);

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
