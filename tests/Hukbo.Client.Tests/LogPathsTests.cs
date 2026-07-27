using Hukbo.Diagnostics;

namespace Hukbo.Client.Tests;

/// <summary>
/// Destination resolution and retention. Retention deletes files, so its bounds
/// are worth pinning down precisely.
/// </summary>
public sealed class LogPathsTests : IDisposable
{
    private readonly string _directory;

    public LogPathsTests()
    {
        _directory = Path.Combine(
            Path.GetTempPath(),
            "hukbo-log-paths-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public void RetentionKeepsTheNewestFilesAndDeletesTheRest()
    {
        for (var index = 0; index < 30; index++)
        {
            WriteLogFile($"2026010{index / 10}-0000{index % 10}-1");
        }

        LogPaths.ApplyRetention(_directory, retainedFileCount: 20);

        var remaining = Directory
            .GetFiles(_directory)
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(20, remaining.Length);
        Assert.Equal(LogPaths.FileNamePrefix + "20260101-00000-1.jsonl", remaining[0]);
        Assert.Equal(LogPaths.FileNamePrefix + "20260102-00009-1.jsonl", remaining[^1]);
    }

    [Fact]
    public void RetentionIgnoresFilesItDidNotWrite()
    {
        for (var index = 0; index < 5; index++)
        {
            WriteLogFile($"20260101-00000{index}-1");
        }

        var bystander = Path.Combine(_directory, "notes.txt");
        File.WriteAllText(bystander, "keep me");

        LogPaths.ApplyRetention(_directory, retainedFileCount: 2);

        Assert.True(File.Exists(bystander));
        Assert.Equal(
            2,
            Directory.GetFiles(
                _directory,
                LogPaths.FileNamePrefix + "*" + LogPaths.FileNameExtension).Length);
    }

    [Fact]
    public void RetentionOnAMissingDirectoryIsANoOperation()
    {
        var missing = Path.Combine(_directory, "does-not-exist");
        LogPaths.ApplyRetention(missing);
        Assert.False(Directory.Exists(missing));
    }

    [Fact]
    public void TheFileNameSortsChronologicallyAsText()
    {
        var earlier = LogPaths.BuildFileName(
            new DateTime(2026, 7, 27, 9, 5, 4, DateTimeKind.Utc),
            31544);
        var later = LogPaths.BuildFileName(
            new DateTime(2026, 7, 27, 14, 22, 11, DateTimeKind.Utc),
            2);

        Assert.Equal("hukbo-20260727-090504-31544.jsonl", earlier);
        Assert.Equal("hukbo-20260727-142211-2.jsonl", later);
        Assert.True(string.CompareOrdinal(earlier, later) < 0);
    }

    [Fact]
    public void AnExplicitOverrideWins()
    {
        var resolved = LogPaths.ResolveDirectory(_directory);
        Assert.Equal(Path.GetFullPath(_directory), resolved);
    }

    [Fact]
    public void TheDefaultDirectoryIsAbsoluteAndEndsInLogs()
    {
        var resolved = LogPaths.ResolveDirectory(null);
        Assert.True(Path.IsPathRooted(resolved));
        Assert.Equal("logs", Path.GetFileName(resolved));
    }

    [Fact]
    public void RepositoryRootDiscoveryReturnsNullWithoutAMarker()
    {
        Assert.Null(LogPaths.FindRepositoryRoot(_directory));
    }

    [Fact]
    public void RepositoryRootDiscoveryFindsAnAncestorMarker()
    {
        var nested = Path.Combine(_directory, "a", "b", "c");
        Directory.CreateDirectory(nested);
        File.WriteAllText(
            Path.Combine(_directory, LogPaths.RepositoryMarkerFileName),
            "<Solution />");

        Assert.Equal(
            new DirectoryInfo(_directory).FullName,
            LogPaths.FindRepositoryRoot(nested));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private void WriteLogFile(string stamp) =>
        File.WriteAllText(
            Path.Combine(
                _directory,
                LogPaths.FileNamePrefix + stamp + LogPaths.FileNameExtension),
            string.Empty);
}
