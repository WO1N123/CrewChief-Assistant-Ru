using System.Windows;
using System.Windows.Input;
using CrewChiefRUAssistant.Telemetry;

namespace CrewChiefRUAssistant;

public partial class FieldsWindow : Window
{
    public FieldsWindow(
        IReadOnlyDictionary<string, TelemetryValue> fields)
    {
        InitializeComponent();

        FieldsBox.Text = string.Join(
            Environment.NewLine,
            fields.Select(pair => $"{pair.Key} = {pair.Value.RawValue}"));
    }

    private void Window_Loaded(object sender, RoutedEventArgs e) =>
        ThemeService.ApplyWindowChrome(this);

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
