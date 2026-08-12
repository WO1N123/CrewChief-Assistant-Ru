using System.IO.Compression;

namespace CrewChiefRUAssistant;

public static class ModelInstaller
{
    private const string ModelUrl =
        "https://alphacephei.com/vosk/models/vosk-model-small-ru-0.22.zip";

    public static bool IsInstalled =>
        Directory.Exists(AppConfig.ModelDirectory) &&
        Directory.EnumerateFiles(
            AppConfig.ModelDirectory,
            "*",
            SearchOption.AllDirectories).Any();

    public static async Task EnsureInstalledAsync(
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        if (IsInstalled)
        {
            progress?.Report(100);
            return;
        }

        var modelParent = Path.GetDirectoryName(AppConfig.ModelDirectory)
                          ?? throw new InvalidOperationException("Не удалось определить папку модели.");

        Directory.CreateDirectory(modelParent);

        var archivePath = Path.Combine(AppConfig.DataDirectory, "vosk-model-small-ru-0.22.zip");
        var temporaryExtract = Path.Combine(AppConfig.DataDirectory, "model_extract");

        if (Directory.Exists(temporaryExtract))
        {
            Directory.Delete(temporaryExtract, recursive: true);
        }

        if (File.Exists(archivePath))
        {
            File.Delete(archivePath);
        }

        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(15)
        };

        using var response = await client.GetAsync(
            ModelUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var totalLength = response.Content.Headers.ContentLength;
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = File.Create(archivePath);

        var buffer = new byte[1024 * 128];
        long totalRead = 0;

        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            totalRead += read;

            if (totalLength is > 0)
            {
                progress?.Report((int)Math.Clamp(totalRead * 90 / totalLength.Value, 0, 90));
            }
        }

        await output.FlushAsync(cancellationToken);
        output.Close();

        progress?.Report(92);

        Directory.CreateDirectory(temporaryExtract);
        ZipFile.ExtractToDirectory(archivePath, temporaryExtract, overwriteFiles: true);

        var extracted = Directory.GetDirectories(temporaryExtract)
            .FirstOrDefault(path =>
                string.Equals(
                    Path.GetFileName(path),
                    "vosk-model-small-ru-0.22",
                    StringComparison.OrdinalIgnoreCase));

        if (extracted is null)
        {
            throw new InvalidDataException("В архиве не найдена папка модели Vosk.");
        }

        if (Directory.Exists(AppConfig.ModelDirectory))
        {
            Directory.Delete(AppConfig.ModelDirectory, recursive: true);
        }

        Directory.Move(extracted, AppConfig.ModelDirectory);

        Directory.Delete(temporaryExtract, recursive: true);
        File.Delete(archivePath);

        progress?.Report(100);
    }
}
