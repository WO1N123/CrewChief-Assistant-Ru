using System.Runtime.InteropServices;

namespace CrewChiefRUAssistant.Input;

public sealed record JoystickSnapshot(
    uint Id,
    string Name,
    int ButtonCount,
    uint Buttons,
    int PovDirection);

public sealed class InputBindingReader
{
    private const uint JoyReturnButtons = 0x00000080;
    private const uint JoyReturnPov = 0x00000040;
    private const uint JoyPovCentered = 0x0000FFFF;
    private const int JoyErrorNoError = 0;

    private uint? _resolvedJoystickId;

    public bool IsPressed(InputBinding binding) =>
        binding.Kind switch
        {
            InputBindingKind.Keyboard or InputBindingKind.Mouse =>
                IsVirtualKeyDown(binding.VirtualKey),

            InputBindingKind.JoystickButton =>
                IsJoystickButtonPressed(binding),

            InputBindingKind.JoystickPov =>
                IsJoystickPovPressed(binding),

            _ => false
        };

    public bool IsVirtualKeyDown(int virtualKey) =>
        (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    public IReadOnlyList<JoystickSnapshot> GetJoystickSnapshots()
    {
        var result = new List<JoystickSnapshot>();
        var count = joyGetNumDevs();

        for (uint id = 0; id < count; id++)
        {
            if (!TryReadJoystick(id, out var state, out var capabilities))
                continue;

            result.Add(
                new JoystickSnapshot(
                    id,
                    string.IsNullOrWhiteSpace(capabilities.szPname)
                        ? $"Контроллер #{id}"
                        : capabilities.szPname.Trim(),
                    Math.Clamp((int)capabilities.wNumButtons, 0, 32),
                    state.dwButtons,
                    NormalizePov(state.dwPOV)));
        }

        return result;
    }

    private bool IsJoystickButtonPressed(InputBinding binding)
    {
        if (binding.ButtonIndex is < 0 or > 31)
            return false;

        if (!TryResolveAndRead(binding, out var state))
            return false;

        var mask = 1u << binding.ButtonIndex;
        return (state.dwButtons & mask) != 0;
    }

    private bool IsJoystickPovPressed(InputBinding binding)
    {
        if (!TryResolveAndRead(binding, out var state))
            return false;

        return NormalizePov(state.dwPOV) == binding.PovDirection;
    }

    private bool TryResolveAndRead(
        InputBinding binding,
        out JoyInfoEx state)
    {
        if (_resolvedJoystickId.HasValue &&
            TryReadJoystick(
                _resolvedJoystickId.Value,
                out state,
                out _))
        {
            return true;
        }

        if (TryReadJoystick(
                binding.JoystickId,
                out state,
                out var directCapabilities) &&
            DeviceMatches(binding, directCapabilities.szPname))
        {
            _resolvedJoystickId = binding.JoystickId;
            return true;
        }

        foreach (var snapshot in GetJoystickSnapshots())
        {
            if (!string.IsNullOrWhiteSpace(binding.DeviceName) &&
                snapshot.Name.Equals(
                    binding.DeviceName,
                    StringComparison.OrdinalIgnoreCase) &&
                TryReadJoystick(snapshot.Id, out state, out _))
            {
                _resolvedJoystickId = snapshot.Id;
                return true;
            }
        }

        state = default;
        return false;
    }

    private static bool DeviceMatches(
        InputBinding binding,
        string actualName)
    {
        var normalizedActualName = actualName?.Trim() ?? string.Empty;

        return string.IsNullOrWhiteSpace(binding.DeviceName) ||
               binding.DeviceName.Equals(
                   normalizedActualName,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryReadJoystick(
        uint id,
        out JoyInfoEx state,
        out JoyCaps capabilities)
    {
        capabilities = default;

        if (joyGetDevCapsW(
                (UIntPtr)id,
                out capabilities,
                (uint)Marshal.SizeOf<JoyCaps>()) != JoyErrorNoError)
        {
            state = default;
            return false;
        }

        state = new JoyInfoEx
        {
            dwSize = (uint)Marshal.SizeOf<JoyInfoEx>(),
            dwFlags = JoyReturnButtons | JoyReturnPov
        };

        return joyGetPosEx(id, ref state) == JoyErrorNoError;
    }

    public static int NormalizePov(uint rawPov)
    {
        if (rawPov == JoyPovCentered || rawPov > 35999)
            return -1;

        var direction = (int)Math.Round(rawPov / 4500.0) * 4500;
        return direction % 36000;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JoyInfoEx
    {
        public uint dwSize;
        public uint dwFlags;
        public uint dwXpos;
        public uint dwYpos;
        public uint dwZpos;
        public uint dwRpos;
        public uint dwUpos;
        public uint dwVpos;
        public uint dwButtons;
        public uint dwButtonNumber;
        public uint dwPOV;
        public uint dwReserved1;
        public uint dwReserved2;
    }

    [StructLayout(
        LayoutKind.Sequential,
        CharSet = CharSet.Unicode)]
    private struct JoyCaps
    {
        public ushort wMid;
        public ushort wPid;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szPname;

        public uint wXmin;
        public uint wXmax;
        public uint wYmin;
        public uint wYmax;
        public uint wZmin;
        public uint wZmax;
        public uint wNumButtons;
        public uint wPeriodMin;
        public uint wPeriodMax;
        public uint wRmin;
        public uint wRmax;
        public uint wUmin;
        public uint wUmax;
        public uint wVmin;
        public uint wVmax;
        public uint wCaps;
        public uint wMaxAxes;
        public uint wNumAxes;
        public uint wMaxButtons;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szRegKey;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szOEMVxD;
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("winmm.dll")]
    private static extern uint joyGetNumDevs();

    [DllImport("winmm.dll")]
    private static extern int joyGetPosEx(
        uint joystickId,
        ref JoyInfoEx info);

    [DllImport(
        "winmm.dll",
        CharSet = CharSet.Unicode,
        EntryPoint = "joyGetDevCapsW")]
    private static extern int joyGetDevCapsW(
        UIntPtr joystickId,
        out JoyCaps capabilities,
        uint size);
}
