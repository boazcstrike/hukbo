using Hukbo.Diagnostics;

namespace Hukbo.Client.Tests;

/// <summary>
/// Standing rules about the shape of the source tree, enforced by scanning it.
/// </summary>
public sealed class SourceHygieneTests
{
    /// <summary>
    /// The two entry points own the console; everything else writes through the
    /// debug log.
    /// </summary>
    /// <remarks>
    /// A stray <c>Console.WriteLine</c> is invisible in a windowed build, is
    /// absent from the log an agent is asked to read, and survives into
    /// <c>Release</c> where the log deliberately does not. Passing a
    /// <c>TextWriter</c> down from an entry point is the supported way to reach
    /// standard error from deeper code.
    /// </remarks>
    private static readonly string[] ConsoleOwners =
    [
        Path.Combine("src", "Hukbo.Client", "Program.cs"),
        Path.Combine("src", "Hukbo.Headless", "Program.cs"),
    ];

    [Fact]
    public void OnlyTheEntryPointsWriteDirectlyToTheConsole()
    {
        var root = GetRepositoryRoot();

        // Scoped to src/ on purpose. tests/ asserts about console usage and
        // tools/ holds hand-run measurement harnesses whose whole output is the
        // console; neither ships in the game.
        var offenders = EnumerateSourceFiles(Path.Combine(root, "src"))
            .Where(path => !IsConsoleOwner(root, path))
            .Where(path => File.ReadAllText(path)
                .Contains("Console.", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(root, path))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    /// <summary>
    /// The simulation is forbidden the filesystem and the wall clock, so it can
    /// never take a reference on the logger. Caught here as well as in
    /// <c>Hukbo.Core.Tests</c> so the failure names the offending line rather
    /// than only the assembly.
    /// </summary>
    [Fact]
    public void TheCoreProjectDoesNotImportTheDiagnosticsNamespace()
    {
        var root = GetRepositoryRoot();
        var coreDirectory = Path.Combine(root, "src", "Hukbo.Core");
        var offenders = EnumerateSourceFiles(coreDirectory)
            .Where(path => File.ReadAllText(path)
                .Contains("Hukbo.Diagnostics", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(root, path))
            .ToArray();

        Assert.Empty(offenders);
    }

    private static bool IsConsoleOwner(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        return ConsoleOwners.Contains(relative, StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> EnumerateSourceFiles(string directory) =>
        Directory
            .EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(path =>
                !path.Contains(
                    Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase) &&
                !path.Contains(
                    Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase));

    private static string GetRepositoryRoot()
    {
        var root = LogPaths.FindRepositoryRoot(AppContext.BaseDirectory);
        Assert.True(
            root is not null,
            "No ancestor of " + AppContext.BaseDirectory + " contains " +
            LogPaths.RepositoryMarkerFileName +
            ", so the source tree cannot be scanned.");
        return root!;
    }
}
