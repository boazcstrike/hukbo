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

    /// <summary>
    /// The directories that hold the visual improvement package's new
    /// procedural-variation presentation code (VIS-038). Deterministic
    /// variant selection anywhere under these directories must come from
    /// <c>EntityId</c> / <c>CombatLoadout</c> / <c>Scenario.Seed</c> mixed
    /// through a named salt in <c>PresentationSalts</c> — never from
    /// <c>System.Random</c>, a <c>GetHashCode</c> call, or the wall clock.
    /// </summary>
    private static readonly string[] PresentationVariationDirectories =
    [
        Path.Combine("src", "Hukbo.Client", "Presentation"),
        Path.Combine("src", "Hukbo.Client", "Rendering"),
        Path.Combine("src", "Hukbo.Client", "Settings"),
    ];

    /// <summary>
    /// Individual files outside <see cref="PresentationVariationDirectories"/>
    /// that are still part of the package's variation surface (VIS-038's
    /// dependency list names them explicitly).
    /// </summary>
    private static readonly string[] PresentationVariationFiles =
    [
        Path.Combine("src", "Hukbo.Client", "UI", "MotionIntensitySelector.cs"),
    ];

    /// <summary>
    /// The files that actually decide which catalog entry, color, or offset a
    /// pawn or backdrop gets: the catalogs, the fallback resolver, the
    /// appearance factory, and the salt registry that seeds them all. Scoped
    /// narrower than <see cref="PresentationVariationDirectories"/> because
    /// this is the one property with an unambiguous "never" — none of these
    /// files may enumerate a <c>Dictionary</c> or <c>HashSet</c> at all, since
    /// the only stable way to pick a variant deterministically is to index
    /// into an ordered list or array with a salted mix of stable identifiers.
    /// </summary>
    private static readonly string[] VariantSelectionSurfaceFiles =
    [
        Path.Combine(
            "src", "Hukbo.Client", "Presentation", "PresentationSalts.cs"),
        Path.Combine(
            "src", "Hukbo.Client", "Presentation", "PawnAppearanceFactory.cs"),
        Path.Combine(
            "src", "Hukbo.Client", "Presentation", "Catalogs",
            "AppearanceComponentCatalog.cs"),
        Path.Combine(
            "src", "Hukbo.Client", "Presentation", "Catalogs",
            "WeaponVisualCatalog.cs"),
        Path.Combine(
            "src", "Hukbo.Client", "Presentation", "Catalogs",
            "ShieldVisualCatalog.cs"),
        Path.Combine(
            "src", "Hukbo.Client", "Presentation", "Catalogs",
            "BackdropVisualCatalog.cs"),
        Path.Combine(
            "src", "Hukbo.Client", "Presentation", "Catalogs",
            "VisualFallbackResolver.cs"),
        Path.Combine(
            "src", "Hukbo.Client", "Presentation", "Catalogs", "DyePalette.cs"),
    ];

    /// <summary>
    /// Content pipeline entries in <c>Content.mgcb</c>: pinned exactly as
    /// <c>#begin &lt;path&gt;</c> lines. R-W6.18 forbids adding to this list
    /// without a separately reviewed dependency change, and the visual
    /// improvement package (fully procedural per OD-4) needs none.
    /// </summary>
    private static readonly string[] PinnedContentPipelineEntries =
    [
        "Fonts/UiCaption.spritefont",
        "Fonts/UiBody.spritefont",
        "Fonts/UiLabel.spritefont",
        "Fonts/UiSubtitle.spritefont",
        "Fonts/UiTitle.spritefont",
        "Fonts/UiDisplay.spritefont",
    ];

    /// <summary>
    /// The centrally managed package versions in <c>Directory.Packages.props</c>,
    /// pinned by name. R-W6.18 / R-X.15 forbid a new package for this package's
    /// scope.
    /// </summary>
    private static readonly string[] PinnedPackageNames =
    [
        "Microsoft.NET.Test.Sdk",
        "MonoGame.Content.Builder.Task",
        "MonoGame.Framework.DesktopGL",
        "xunit",
        "xunit.runner.visualstudio",
    ];

    /// <summary>
    /// No <c>System.Random</c> instantiation or <c>Random.Shared</c> use
    /// anywhere in the package's variation surface. Doc-comment mentions
    /// (several files describe the ban in prose) are excluded so this scan
    /// cannot fail on the sentence that documents the rule.
    /// </summary>
    [Fact]
    public void PresentationVariationCodeDoesNotUseSystemRandom()
    {
        var root = GetRepositoryRoot();
        var offenders = FindOffendingCodeLines(
            root,
            EnumeratePresentationVariationFiles(root),
            line =>
                line.Contains("System.Random", StringComparison.Ordinal) ||
                line.Contains("new Random(", StringComparison.Ordinal) ||
                line.Contains("Random.Shared", StringComparison.Ordinal));

        Assert.Empty(offenders);
    }

    /// <summary>
    /// No <c>GetHashCode</c> invocation anywhere in the package's variation
    /// surface. The pattern requires a preceding <c>.</c>, so a legitimate
    /// <c>public override int GetHashCode()</c> declaration elsewhere in the
    /// Client project (equality support, unrelated to variant selection) does
    /// not trip it.
    /// </summary>
    [Fact]
    public void PresentationVariationCodeDoesNotUseGetHashCodeForSelection()
    {
        var root = GetRepositoryRoot();
        var offenders = FindOffendingCodeLines(
            root,
            EnumeratePresentationVariationFiles(root),
            line => line.Contains(".GetHashCode()", StringComparison.Ordinal));

        Assert.Empty(offenders);
    }

    /// <summary>
    /// No wall-clock read anywhere in the package's variation surface.
    /// <c>Stopwatch</c> is deliberately not scanned for: the render probe's
    /// frame-timing instrumentation (opt-in, debug-time, measurement-only per
    /// the integration design section 11) legitimately uses it and does not
    /// feed any variant selection.
    /// </summary>
    [Fact]
    public void PresentationVariationCodeDoesNotReadTheWallClock()
    {
        var root = GetRepositoryRoot();
        var offenders = FindOffendingCodeLines(
            root,
            EnumeratePresentationVariationFiles(root),
            line =>
                line.Contains("DateTime.Now", StringComparison.Ordinal) ||
                line.Contains("DateTime.UtcNow", StringComparison.Ordinal) ||
                line.Contains("DateTime.Today", StringComparison.Ordinal) ||
                line.Contains("Environment.TickCount", StringComparison.Ordinal));

        Assert.Empty(offenders);
    }

    /// <summary>
    /// The narrowest, least ambiguous form of "no iteration-order
    /// dependence": the files that actually choose a catalog entry, color, or
    /// offset never declare or enumerate a <c>Dictionary</c> or
    /// <c>HashSet</c> at all. Every other file in the presentation layer is
    /// free to use them as ordinary lookup tables (several do, keyed by
    /// <c>EntityId</c>, and are unaffected by this test); this test exists
    /// because the selection surface specifically must stay index-into-an-
    /// ordered-list, salted-mix, with no enumeration order anywhere in the
    /// path from identity to variant.
    /// </summary>
    [Fact]
    public void VariantSelectionSurfaceDoesNotDependOnDictionaryOrHashSetIterationOrder()
    {
        var root = GetRepositoryRoot();
        var files = VariantSelectionSurfaceFiles
            .Select(relative => Path.Combine(root, relative))
            .Where(File.Exists);

        var offenders = FindOffendingCodeLines(
            root,
            files,
            line =>
                line.Contains("Dictionary<", StringComparison.Ordinal) ||
                line.Contains("HashSet<", StringComparison.Ordinal));

        Assert.Empty(offenders);
    }

    /// <summary>
    /// <c>Content.mgcb</c> stays exactly six spritefonts and nothing else
    /// (R-W6.18): the package ships zero textures, atlases, or shaders, per
    /// OD-4's fully procedural direction.
    /// </summary>
    [Fact]
    public void ContentPipelineEntriesAreUnchangedFromTheSixPinnedSpritefonts()
    {
        var root = GetRepositoryRoot();
        var mgcbPath = Path.Combine(
            root, "src", "Hukbo.Client", "Content", "Content.mgcb");
        var lines = File.ReadAllLines(mgcbPath);

        var entries = lines
            .Where(line => line.StartsWith("#begin ", StringComparison.Ordinal))
            .Select(line => line["#begin ".Length..].Trim())
            .ToArray();

        Assert.Equal(PinnedContentPipelineEntries, entries);
        Assert.All(
            entries,
            entry => Assert.EndsWith(
                ".spritefont", entry, StringComparison.Ordinal));
    }

    /// <summary>
    /// <c>Directory.Packages.props</c> carries exactly the five pinned
    /// package versions (R-W6.18, R-X.15): no new NuGet dependency for the
    /// visual improvement package's fully procedural scope.
    /// </summary>
    [Fact]
    public void CentralPackageVersionsAreUnchangedFromThePinnedList()
    {
        var root = GetRepositoryRoot();
        var propsPath = Path.Combine(root, "Directory.Packages.props");
        var content = File.ReadAllText(propsPath);

        var names = System.Text.RegularExpressions.Regex
            .Matches(content, "<PackageVersion Include=\"([^\"]+)\"")
            .Select(match => match.Groups[1].Value)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            PinnedPackageNames.Order(StringComparer.Ordinal).ToArray(),
            names);
    }

    /// <summary>
    /// Every recorded render-baseline artifact that
    /// <c>docs/development/testing.md</c> cites resolves to a file the
    /// repository actually carries.
    /// </summary>
    /// <remarks>
    /// The measurement tables in that document are only as trustworthy as the
    /// JSON they were read from. The first baselines were written under
    /// <c>artifacts/</c>, which <c>.gitignore</c> excludes, so the citation
    /// named a path that existed on one machine and nowhere else; a fresh
    /// clone could read the tables but could not open the evidence behind
    /// them. Scanning the citations here means a baseline can never again be
    /// quoted from a file the repository does not hold.
    /// </remarks>
    [Fact]
    public void EveryCitedRenderBaselineArtifactExistsInTheRepository()
    {
        var root = GetRepositoryRoot();
        var testingDocumentPath = Path.Combine(
            root, "docs", "development", "testing.md");
        var content = File.ReadAllText(testingDocumentPath);

        // A cited path is a backtick-quoted token naming a render-baseline
        // JSON file. Matching on the file name rather than on the directory
        // keeps the scan honest about a citation that points somewhere
        // untracked, which is the failure this test was written for.
        var citedPaths = System.Text.RegularExpressions.Regex
            .Matches(content, "`([^`\\s]*render-baseline[^`\\s]*\\.json)`")
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        // Deleting the citation rather than repairing it would leave this
        // test passing while asserting nothing, so the recorded baselines are
        // required to still be cited at all.
        Assert.NotEmpty(citedPaths);

        var missing = citedPaths
            .Where(cited => !File.Exists(Path.Combine(
                root,
                cited.Replace('/', Path.DirectorySeparatorChar))))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(missing);
    }

    /// <summary>
    /// The shipped client still asks for a retrace-synchronized device, and no
    /// file under <c>src/</c> hardcodes the opposite.
    /// </summary>
    /// <remarks>
    /// GPU-006 gave <c>Hukbo.Tools.RenderProbe</c> a way to run with vertical
    /// retrace disabled, because a blocking wait for the display is not the CPU
    /// cost the probe exists to measure. That override belongs to the probe
    /// alone: the spectator's game must keep presenting on the retrace, or it
    /// spends a whole core spinning out frames nobody sees. The override is a
    /// method parameter, so the literal <c>false</c> never appears in
    /// <c>src/</c>, and this test is what keeps it that way — a later change
    /// that switched the shipped default off, or that pinned the flag off in
    /// some other file, would fail here rather than ship silently.
    /// </remarks>
    [Fact]
    public void TheShippedClientStillSynchronizesWithTheVerticalRetrace()
    {
        var root = GetRepositoryRoot();
        var arenaGamePath = Path.Combine(
            root, "src", "Hukbo.Client", "ArenaGame.cs");

        Assert.Contains(
            "SynchronizeWithVerticalRetrace = true,",
            File.ReadAllText(arenaGamePath),
            StringComparison.Ordinal);

        var offenders = FindOffendingCodeLines(
            root,
            EnumerateSourceFiles(Path.Combine(root, "src")),
            line => line.Contains(
                "SynchronizeWithVerticalRetrace = false",
                StringComparison.Ordinal));

        Assert.Empty(offenders);
    }

    private static IEnumerable<string> EnumeratePresentationVariationFiles(
        string root)
    {
        var fromDirectories = PresentationVariationDirectories
            .Select(relative => Path.Combine(root, relative))
            .Where(Directory.Exists)
            .SelectMany(EnumerateSourceFiles);

        var namedFiles = PresentationVariationFiles
            .Select(relative => Path.Combine(root, relative))
            .Where(File.Exists);

        return fromDirectories.Concat(namedFiles);
    }

    /// <summary>
    /// Scans a set of files line by line for a banned pattern, skipping
    /// comment lines (<c>//</c> and <c>///</c>) so a doc comment that
    /// documents a prohibition by naming the prohibited API cannot trip the
    /// scan it is describing.
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
