using System.Text;

namespace CrewChiefRUAssistant;

internal static class AppLog
{
    private static readonly object Sync = new();

    public static string LogDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CrewChiefRUAssistant",
            "logs");

    public static string CurrentLogPath =>
        Path.Combine(LogDirectory, "application.log");

    public static void Initialize(string[] args)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);

            lock (Sync)
            {
                File.AppendAllText(
                    CurrentLogPath,
                    Environment.NewLine +
                    new string('=', 72) +
                    Environment.NewLine +
                    $"Started: {DateTimeOffset.Now:O}" +
                    Environment.NewLine +
                    $"Process: {Environment.ProcessPath}" +
                    Environment.NewLine +
                    $"Base directory: {AppContext.BaseDirectory}" +
                    Environment.NewLine +
                    $"Arguments: {string.Join(" ", args)}" +
                    Environment.NewLine +
                    $"Windows: {Environment.OSVersion}" +
                    Environment.NewLine,
                    Encoding.UTF8);
            }
        }
        catch
        {
        }
    }

    public static void Write(string message)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);

            lock (Sync)
            {
                File.AppendAllText(
                    CurrentLogPath,
                    $"[{DateTimeOffset.Now:O}] {message}{Environment.NewLine}",
                    Encoding.UTF8);
            }
        }
        catch
        {
        }
    }

    public static void WriteException(string context, Exception exception) =>
        Write(context + Environment.NewLine + exception);
}
