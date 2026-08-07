using System.Reflection;
using Hukbo.Diagnostics;

namespace Sandata.Core.Tests;

/// <summary>
/// The Sandata half of the build-gate widening recorded in design section 13
/// of docs/plans/2026-08-07-sandata-scaffold-design.md: <c>Sandata.Core</c>
/// never references <c>Hukbo.Diagnostics</c>, and its source tree never
/// spells out any of the numeric or collection types the determinism
/// contract bans (plan standing rule 3).
/// </summary>
/// <remarks>
/// <see cref="Hukbo.Client.Tests.SourceHygieneTests"/> and
/// <see cref="Hukbo.Core.Tests.DiagnosticLoggingBoundaryTests"/> cover the
/// equivalent facts for Hukbo and for the two ban-free assertions that hold
/// against <c>Hukbo.Shared.Core</c>. Neither of those files can host the
/// Sandata-specific facts below: <c>Hukbo.Client.Tests</c> and
/// <c>Hukbo.Core.Tests</c> hold no project reference to
/// <c>Sandata.Core</c> or <c>Sandata.Headless</c>, and adding one would
/// widen a file this task does not own. This file duplicates the scanning
/// technique those two files already use — enumerate <c>*.cs</c> under a
/// directory excluding <c>bin</c>/<c>obj</c>, skip a line whose trimmed text
/// starts with <c>//</c> before testing it — rather than inventing a second
/// one, exactly as docs/plans/2026-08-07-sandata-scaffold.md task 7 requires.
/// </remarks>
public sealed class SandataSourceHygieneTests
{
    /// <summary>
    /// <c>Sandata.Core</c> is forbidden the filesystem and the wall clock for
    /// the same reason <c>Hukbo.Core</c> is: the simulation must stay
    /// reachable from a save file and a replay, never from a live logger.
    /// </summary>
    [Fact]
    public void SandataCoreDoesNotReferenceTheDiagnosticsAssembly()
    {
        // Assembly.Load by simple name, not typeof(SomeSandataCoreType), on
        // purpose: Sandata.Core is still almost empty at the time this test
        // is written (parallel tasks are filling it in), so there is no
        // stable anchor type to hang typeof(...) off yet. Loading by name
        // works whether the assembly carries zero types or a hundred.
        var referenced = Assembly.Load(new AssemblyName("Sandata.Core"))
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToArray();

        Assert.DoesNotContain("Hukbo.Diagnostics", referenced);
    }

    /// <summary>
    /// The positive control for
    /// <see cref="SandataCoreDoesNotReferenceTheDiagnosticsAssembly"/>: without
    /// it, deleting <c>Hukbo.Diagnostics</c> entirely, or simply never wiring
    /// any Sandata project to it, would leave that fact passing and prove
    /// nothing.
    /// </summary>
    /// <remarks>
    /// This does not mirror
    /// <c>DiagnosticLoggingBoundaryTests.TheHeadlessRunnerDoesReferenceTheDiagnosticsAssembly</c>'s
    /// reflection technique, and that is a deliberate, reasoned deviation
    /// worth recording rather than a silent shortcut. <c>src/Sandata.Headless/Program.cs</c>
    /// is still the task-1 placeholder (<c>return 0;</c>) until plan task 14
    /// writes the real argument parsing and <c>DiagnosticLog</c> wiring.
    /// Measured directly against this repository's build: the C# compiler
    /// only emits an <c>AssemblyRef</c> metadata entry for a project reference
    /// that some compiled member actually uses, so
    /// <c>Sandata.Headless.dll</c>'s compiled metadata does not yet name
    /// <c>Hukbo.Diagnostics</c> even though <c>Sandata.Headless.csproj</c>
    /// already does — <c>typeof(HeadlessRunner).Assembly.GetReferencedAssemblies()</c>'s
    /// Sandata equivalent would read false today and only turn true once task
    /// 14 lands, which makes it an unstable positive control rather than a
    /// standing one. Reading the csproj directly instead proves the wiring is
    /// present now, stays present after task 14 adds real usage, and is
    /// exactly as breakable: remove the <c>&lt;ProjectReference&gt;</c> line
    /// and this test goes red.
    /// </remarks>
    [Fact]
    public void SandataHeadlessProjectIsWiredToTheDiagnosticsAssembly()
    {
        var root = GetRepositoryRoot();
        var csprojPath = Path.Combine(
            root, "src", "Sandata.Headless", "Sandata.Headless.csproj");
        var content = File.ReadAllText(csprojPath);

        Assert.Contains(
            "Hukbo.Diagnostics.csproj", content, StringComparison.Ordinal);
    }

    /// <summary>
    /// Mirrors
    /// <c>SourceHygieneTests.TheCoreProjectDoesNotImportTheDiagnosticsNamespace</c>,
    /// scoped to <c>src/Sandata.Core</c>, so a failure here names the
    /// offending line rather than only the assembly the fact above already
    /// covers.
    /// </summary>
    [Fact]
    public void SandataCoreSourceDoesNotMentionTheDiagnosticsNamespace()
    {
        var root = GetRepositoryRoot();
        var sandataCoreDirectory = Path.Combine(root, "src", "Sandata.Core");

        var offenders = EnumerateSourceFiles(sandataCoreDirectory)
            .Where(path => File.ReadAllText(path)
                .Contains("Hukbo.Diagnostics", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(root, path))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    /// <summary>
    /// The plan's standing rule 3 and design section 4: <c>Sandata.Core</c>
    /// never spells out <c>float</c>, <c>double</c>, <c>System.Random</c>,
    /// <c>Math.Sqrt</c>, <c>Math.Atan2</c>, <c>Dictionary&lt;</c>,
    /// <c>HashSet&lt;</c>, or <c>PriorityQueue&lt;</c> outside a doc comment.
    /// Every one of those is banned by the determinism contract, and this
    /// scan must hold on the almost-empty project today and stay correct once
    /// the parallel tasks finish filling it in.
    /// </summary>
    [Fact]
    public void SandataCoreDoesNotUseBannedNumericOrCollectionTypes()
    {
        var root = GetRepositoryRoot();
        var sandataCoreDirectory = Path.Combine(root, "src", "Sandata.Core");

        var offenders = FindOffendingCodeLines(
            root,
            EnumerateSourceFiles(sandataCoreDirectory),
            line => BannedSandataCoreTokens.Any(
                token => line.Contains(token, StringComparison.Ordinal)));

        Assert.Empty(offenders);
    }

    private static readonly string[] BannedSandataCoreTokens =
    [
        "float",
        "double",
        "System.Random",
        "Math.Sqrt",
        "Math.Atan2",
        "Dictionary<",
        "HashSet<",
        "PriorityQueue<",
    ];

    /// <summary>
    /// Scans a set of files line by line for a banned pattern, skipping
    /// comment lines (<c>//</c> and <c>///</c>) so a doc comment that
    /// documents a prohibition by naming the prohibited token cannot trip the
    /// scan it is describing. Duplicated verbatim from
    /// <c>SourceHygieneTests.FindOffendingCodeLines</c> in
    /// <c>Hukbo.Client.Tests</c> per plan task 7's instruction to match that
    /// technique exactly — the two test projects share no code, so the only
    /// way to reuse the technique is to copy it.
    /// </summary>
    private static string[] FindOffendingCodeLines(
        string root,
        IEnumerable<string> files,
        Func<string, bool> isBannedLine)
    {
        var offenders = new List<string>();

        foreach (var path in files)
        {
            var lines = File.ReadAllLines(path);
            for (var index = 0; index < lines.Length; index++)
            {
                var trimmed = lines[index].TrimStart();
                if (trimmed.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                if (isBannedLine(lines[index]))
                {
                    offenders.Add(
                        Path.GetRelativePath(root, path) + ":" + (index + 1));
                }
            }
        }

        return offenders.Order(StringComparer.Ordinal).ToArray();
    }

    private static IEnumerable<string> EnumerateSourceFiles(string directory) =>
        !Directory.Exists(directory)
            ? []
            : Directory
                .EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                .Where(path =>
                    !path.Contains(
                        Path.DirectorySeparatorChar + "obj" +
                        Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase) &&
                    !path.Contains(
                        Path.DirectorySeparatorChar + "bin" +
                        Path.DirectorySeparatorChar,
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
