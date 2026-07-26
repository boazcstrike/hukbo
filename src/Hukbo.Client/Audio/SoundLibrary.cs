namespace Hukbo.Client.Audio;

/// <summary>
/// Discovery of the owner's audio files. <see cref="Resolve"/> is pure — it
/// takes the folder's file names as data — so the resolution rules are testable
/// without touching a real directory.
/// </summary>
internal static class SoundLibrary
{
    /// <summary>
    /// The folder the game reads sound files from:
    /// <c>&lt;base directory&gt;/Content/Audio</c>.
    /// </summary>
    public static string GetDefaultDirectoryPath() =>
        Path.Combine(
            AppContext.BaseDirectory,
            "Content",
            SoundCatalog.FolderName);

    /// <summary>
    /// Lists the file names directly inside <paramref name="directoryPath"/>.
    /// A folder that does not exist, or that cannot be read, yields an empty
    /// list: an absent audio folder is a silent game, not an error.
    /// </summary>
    public static IReadOnlyList<string> ListFileNames(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        try
        {
            if (!Directory.Exists(directoryPath))
            {
                return [];
            }

            var paths = Directory.GetFiles(directoryPath);
            var fileNames = new string[paths.Length];
            for (var index = 0; index < paths.Length; index++)
            {
                fileNames[index] = Path.GetFileName(paths[index]);
            }

            return fileNames;
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>
    /// Pairs every slot in <see cref="SoundCatalog.AllSounds"/> with the file
    /// that backs it. Matching is case-insensitive so a file named
    /// <c>Death.WAV</c> works on a case-sensitive filesystem, and candidates are
    /// compared in ordinal order so two files differing only in case resolve to
    /// the same one on every run.
    /// </summary>
    public static IReadOnlyList<SoundBinding> Resolve(
        string directoryPath,
        IReadOnlyList<string> fileNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentNullException.ThrowIfNull(fileNames);

        var sortedFileNames = new string[fileNames.Count];
        for (var index = 0; index < fileNames.Count; index++)
        {
            sortedFileNames[index] = fileNames[index] ?? string.Empty;
        }

        Array.Sort(sortedFileNames, StringComparer.Ordinal);

        var bindings = new SoundBinding[SoundCatalog.AllSounds.Count];
        for (var index = 0; index < SoundCatalog.AllSounds.Count; index++)
        {
            var sound = SoundCatalog.AllSounds[index];
            var expectedFileName = SoundCatalog.GetFileName(sound);
            var matchedFileName = FindMatch(sortedFileNames, expectedFileName);
            bindings[index] = matchedFileName is null
                ? new SoundBinding(
                    sound,
                    expectedFileName,
                    FilePath: null,
                    SoundBindingStatus.Missing)
                : new SoundBinding(
                    sound,
                    expectedFileName,
                    Path.Combine(directoryPath, matchedFileName),
                    SoundBindingStatus.Ready);
        }

        return bindings;
    }

    private static string? FindMatch(
        string[] sortedFileNames,
        string expectedFileName)
    {
        foreach (var fileName in sortedFileNames)
        {
            if (string.Equals(
                    fileName,
                    expectedFileName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return fileName;
            }
        }

        return null;
    }
}
