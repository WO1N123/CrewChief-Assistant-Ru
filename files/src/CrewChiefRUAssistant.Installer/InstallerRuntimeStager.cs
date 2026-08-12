namespace CrewChiefRUAssistant.Installer;

internal static class InstallerRuntimeStager
{
    public static string StageCurrentRuntime(string destinationDirectory)
    {
        var processPath = Environment.ProcessPath
                          ?? throw new InvalidOperationException(
                              "Не удалось определить путь установщика.");

        Directory.CreateDirectory(destinationDirectory);

        var baseDirectory = Path.GetFullPath(AppContext.BaseDirectory);
        var companionAssembly = Path.Combine(
            baseDirectory,
            "CrewChiefRU_Setup.dll");

        if (File.Exists(companionAssembly))
        {
            CopyDirectory(baseDirectory, destinationDirectory);

            var stagedExecutable = Path.Combine(
                destinationDirectory,
                Path.GetFileName(processPath));

            if (!File.Exists(stagedExecutable))
            {
                throw new FileNotFoundException(
                    "После копирования не найден файл установщика.",
                    stagedExecutable);
            }

            return stagedExecutable;
        }

        var singleFileTarget = Path.Combine(
            destinationDirectory,
            InstallPaths.InstallerFileName);

        File.Copy(processPath, singleFileTarget, overwrite: true);
        return singleFileTarget;
    }

    private static void CopyDirectory(
        string sourceDirectory,
        string destinationDirectory)
    {
        var sourceRoot = Path.GetFullPath(sourceDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        foreach (var directory in Directory.EnumerateDirectories(
                     sourceRoot,
                     "*",
                     SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceRoot, directory);
            Directory.CreateDirectory(Path.Combine(destinationDirectory, relative));
        }

        foreach (var file in Directory.EnumerateFiles(
                     sourceRoot,
                     "*",
                     SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceRoot, file);
            var target = Path.Combine(destinationDirectory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }
}
