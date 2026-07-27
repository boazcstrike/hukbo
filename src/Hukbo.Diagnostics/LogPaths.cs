using System.Globalization;

namespace Hukbo.Diagnostics;

/// <summary>
/// Decides where a log file goes and keeps the destination directory from
/// growing without bound.
/// </summary>
public static class LogPaths
{
    /// <summary>
    /// The marker file that identifies the repository root. Chosen because it
    /// is the one file guaranteed to sit at the root of a source checkout and
    /// guaranteed to be absent from a packaged build.
    /// </summary>
    public const string RepositoryMarkerFileName = "Hukbo.slnx";

    /// <summary>
    /// Prefix and extension of every log file. Retention only ever considers
    /// files matching this shape, so an unrelated file dropped in the directory
    /// is never deleted.
    /// </summary>
    public const string FileNamePrefix = "hukbo-";

    /// <inheritdoc cref="FileNamePrefix" />
    public const string FileNameExtension = ".jsonl";

    /// <summary>
    /// How many log files survive a retention sweep. Bounded on purpose: this
    /// facility is on by default while developing, and an unbounded directory
    /// is the same hazard as an unbounded cache.
    /// </summary>
    public const int RetainedFileCount = 20;

    /// <summary>
    /// Walks up from <paramref name="startDirectory"/> looking for the
    /// repository marker. Returns <see langword="null"/> for a packaged build,
    /// where no ancestor has one.
    /// </summary>
    public static string? FindRepositoryRoot(string startDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startDirectory);

        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            var marker = Path.Combine(
                directory.FullName,
                RepositoryMarkerFileName);
            if (File.Exists(marker))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    /// <summary>
    /// The directory logs are written into: the explicit override if one was
    /// given, otherwise <c>artifacts/logs</c> under the repository root,
    /// otherwise <c>logs</c> beside the executable.
    /// </summary>
    public static string ResolveDirectory(string? overridePath)
    {
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return Path.GetFullPath(overridePath);
        }

        var baseDirectory = AppContext.BaseDirectory;
        var root = FindRepositoryRoot(baseDirectory);
        return root is null
            ? Path.GetFullPath(Path.Combine(baseDirectory, "logs"))
            : Path.GetFullPath(Path.Combine(root, "artifacts", "logs"));
    }

    /// <summary>
    /// Builds the file name for one run. The timestamp sorts chronologically as
    /// text, which is what lets retention order files without trusting
    /// filesystem timestamps; the process id disambiguates two runs started in
    /// the same second.
    /// </summary>
    public static string BuildFileName(DateTime utcNow, int processId) =>
        FileNamePrefix +
        utcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) +
        "-" +
        processId.ToString(CultureInfo.InvariantCulture) +
        FileNameExtension;

    /// <summary>
    /// Deletes all but the newest <see cref="RetainedFileCount"/> log files.
    /// Only files matching the generated name shape are considered, and a file
    /// that cannot be deleted — one still held open by another run — is skipped
    /// rather than allowed to fail the sweep.
    /// </summary>
    public static void ApplyRetention(
        string directoryPath,
        int retainedFileCount = RetainedFileCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentOutOfRangeException.ThrowIfNegative(retainedFileCount);

        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        var files = Directory
            .GetFiles(directoryPath, FileNamePrefix + "*" + FileNameExtension)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var excessCount = files.Length - retainedFileCount;
        for (var index = 0; index < excessCount; index++)
        {
            try
            {
                File.Delete(files[index]);
            }
            catch (IOException)
            {
                // Held open by a concurrent run. It will be swept next time.
            }
            catch (UnauthorizedAccessException)
            {
                // Read-only or otherwise protected. Not worth failing a run.
            }
        }
    }
}
