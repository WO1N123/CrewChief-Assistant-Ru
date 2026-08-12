namespace CrewChiefRUAssistant.Input;

public enum InputBindingKind
{
    Keyboard,
    Mouse,
    JoystickButton,
    JoystickPov
}

public sealed class InputBinding
{
    public InputBindingKind Kind { get; set; } = InputBindingKind.Keyboard;
    public int VirtualKey { get; set; } = 120;
    public uint JoystickId { get; set; }
    public int ButtonIndex { get; set; }
    public int PovDirection { get; set; } = -1;
    public string DeviceName { get; set; } = string.Empty;

    public string DisplayName => InputBindingFormatter.Format(this);

    public InputBinding Clone() =>
        new()
        {
            Kind = Kind,
            VirtualKey = VirtualKey,
            JoystickId = JoystickId,
            ButtonIndex = ButtonIndex,
            PovDirection = PovDirection,
            DeviceName = DeviceName
        };

    public static InputBinding Keyboard(int virtualKey) =>
        new()
        {
            Kind = InputBindingKind.Keyboard,
            VirtualKey = virtualKey
        };

    public static InputBinding Mouse(int virtualKey) =>
        new()
        {
            Kind = InputBindingKind.Mouse,
            VirtualKey = virtualKey
        };

    public static InputBinding JoystickButton(
        uint joystickId,
        int buttonIndex,
        string deviceName) =>
        new()
        {
            Kind = InputBindingKind.JoystickButton,
            JoystickId = joystickId,
            ButtonIndex = buttonIndex,
            DeviceName = deviceName
        };

    public static InputBinding JoystickPov(
        uint joystickId,
        int direction,
        string deviceName) =>
        new()
        {
            Kind = InputBindingKind.JoystickPov,
            JoystickId = joystickId,
            PovDirection = direction,
            DeviceName = deviceName
        };
}

public static class InputBindingFormatter
{
    private static readonly IReadOnlyDictionary<int, string> KeyNames =
        new Dictionary<int, string>
        {
            [0x08] = "Backspace",
            [0x09] = "Tab",
            [0x0D] = "Enter",
            [0x10] = "Shift",
            [0x11] = "Ctrl",
            [0x12] = "Alt",
            [0x13] = "Pause",
            [0x14] = "Caps Lock",
            [0x1B] = "Escape",
            [0x20] = "Пробел",
            [0x21] = "Page Up",
            [0x22] = "Page Down",
            [0x23] = "End",
            [0x24] = "Home",
            [0x25] = "Стрелка влево",
            [0x26] = "Стрелка вверх",
            [0x27] = "Стрелка вправо",
            [0x28] = "Стрелка вниз",
            [0x2C] = "Print Screen",
            [0x2D] = "Insert",
            [0x2E] = "Delete",
            [0x5B] = "Левая Win",
            [0x5C] = "Правая Win",
            [0x5D] = "Меню",
            [0x60] = "Num 0",
            [0x61] = "Num 1",
            [0x62] = "Num 2",
            [0x63] = "Num 3",
            [0x64] = "Num 4",
            [0x65] = "Num 5",
            [0x66] = "Num 6",
            [0x67] = "Num 7",
            [0x68] = "Num 8",
            [0x69] = "Num 9",
            [0x6A] = "Num *",
            [0x6B] = "Num +",
            [0x6D] = "Num -",
            [0x6E] = "Num .",
            [0x6F] = "Num /",
            [0x90] = "Num Lock",
            [0x91] = "Scroll Lock",
            [0xA0] = "Левый Shift",
            [0xA1] = "Правый Shift",
            [0xA2] = "Левый Ctrl",
            [0xA3] = "Правый Ctrl",
            [0xA4] = "Левый Alt",
            [0xA5] = "Правый Alt",
            [0xAD] = "Выключить звук",
            [0xAE] = "Тише",
            [0xAF] = "Громче",
            [0xB0] = "Следующий трек",
            [0xB1] = "Предыдущий трек",
            [0xB2] = "Стоп",
            [0xB3] = "Воспроизведение/пауза"
        };

    private static readonly IReadOnlyDictionary<int, string> MouseNames =
        new Dictionary<int, string>
        {
            [0x01] = "Левая кнопка мыши",
            [0x02] = "Правая кнопка мыши",
            [0x04] = "Средняя кнопка мыши",
            [0x05] = "Дополнительная кнопка мыши 1",
            [0x06] = "Дополнительная кнопка мыши 2"
        };

    public static string Format(InputBinding binding) =>
        binding.Kind switch
        {
            InputBindingKind.Keyboard =>
                $"Клавиатура: {FormatKey(binding.VirtualKey)}",

            InputBindingKind.Mouse =>
                $"Мышь: {FormatMouse(binding.VirtualKey)}",

            InputBindingKind.JoystickButton =>
                $"{Device(binding)}: кнопка {binding.ButtonIndex + 1}",

            InputBindingKind.JoystickPov =>
                $"{Device(binding)}: POV {FormatPov(binding.PovDirection)}",

            _ => "Не назначено"
        };

    public static string FormatKey(int virtualKey)
    {
        if (KeyNames.TryGetValue(virtualKey, out var name))
            return name;

        if (virtualKey is >= 0x30 and <= 0x39)
            return ((char)virtualKey).ToString();

        if (virtualKey is >= 0x41 and <= 0x5A)
            return ((char)virtualKey).ToString();

        if (virtualKey is >= 0x70 and <= 0x87)
            return $"F{virtualKey - 0x6F}";

        return $"VK 0x{virtualKey:X2}";
    }

    public static string FormatMouse(int virtualKey) =>
        MouseNames.TryGetValue(virtualKey, out var name)
            ? name
            : $"кнопка VK 0x{virtualKey:X2}";

    public static bool IsMouseVirtualKey(int virtualKey) =>
        MouseNames.ContainsKey(virtualKey);

    public static string FormatPov(int direction) =>
        direction switch
        {
            0 => "вверх",
            4500 => "вверх-вправо",
            9000 => "вправо",
            13500 => "вниз-вправо",
            18000 => "вниз",
            22500 => "вниз-влево",
            27000 => "влево",
            31500 => "вверх-влево",
            _ => $"{direction / 100.0:0.#}°"
        };

    private static string Device(InputBinding binding) =>
        string.IsNullOrWhiteSpace(binding.DeviceName)
            ? $"Контроллер #{binding.JoystickId}"
            : binding.DeviceName;
}
