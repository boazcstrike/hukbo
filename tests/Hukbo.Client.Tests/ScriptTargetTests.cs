using Hukbo.Diagnostics;

namespace Hukbo.Client.Tests;

/// <summary>
/// Proves the acceptance criterion for plan task 41 of Sandata's
/// scaffold plan: the four game-specific
/// scripts no longer hardcode a project path, and the Hukbo branch of the
/// new shared table carries the exact literals those scripts hardcoded
/// before this change, so a caller that never passes -Game resolves to
/// exactly the same paths it always did.
/// </summary>
public sealed class ScriptTargetTests
{
    /// <summary>
    /// The four literal project paths named in
    /// docs/plans/2026-08-07-sandata-scaffold-design.md section 14: the
    /// scripts hardcoded these before scripts/_gametargets.ps1 existed
    /// (run.ps1:21, benchmark.ps1:42, test.ps1:15-16, package.ps1:12).
    /// </summary>
    private static readonly string[] PinnedHukboProjectPaths =
    [
        "src/Hukbo.Client/Hukbo.Client.csproj",
        "src/Hukbo.Headless/Hukbo.Headless.csproj",
        "tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj",
        "tests/Hukbo.Client.Tests/Hukbo.Client.Tests.csproj",
    ];

    /// <summary>
    /// The four scripts that gained -Game and now look up their project
    /// paths through scripts/_gametargets.ps1 instead of hardcoding them.
    /// </summary>
    private static readonly string[] GameTargetScripts =
    [
        "run.ps1",
        "test.ps1",
        "benchmark.ps1",
        "package.ps1",
    ];

    /// <summary>
    /// The Hukbo branch of Get-GameTarget must carry every pinned literal so
    /// the default resolution is byte-identical to what the scripts
    /// hardcoded before this table existed. This is the cheap, first-layer
    /// proof from design section 14; it catches a typo in the table.
    /// </summary>
    [Fact]
    public void TheGameTargetTableCarriesTheExactPreexistingHukboLiterals()
    {
        var root = GetRepositoryRoot();
        var content = File.ReadAllText(
            Path.Combine(root, "scripts", "_gametargets.ps1"));

        var missing = PinnedHukboProjectPaths
            .Where(path => !content.Contains(path, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(missing);
    }

    /// <summary>
    /// The inverse check from design section 14: the table can be added and
    /// then bypassed, so this asserts no game-specific script body still
    /// hardcodes a Hukbo project path directly rather than reading it out of
    /// Get-GameTarget.
    /// </summary>
    [Fact]
    public void NoGameTargetScriptStillHardcodesAHukboProjectPath()
    {
        var root = GetRepositoryRoot();
        var offenders = new List<string>();

        foreach (var scriptName in GameTargetScripts)
        {
            var path = Path.Combine(root, "scripts", scriptName);
            var lines = File.ReadAllLines(path);
            for (var index = 0; index < lines.Length; index++)
            {
                if (lines[index].Contains(
                        "src/Hukbo.", StringComparison.Ordinal) ||
                    lines[index].Contains(
                        "tests/Hukbo.", StringComparison.Ordinal))
                {
                    offenders.Add(scriptName + ":" + (index + 1));
                }
            }
        }

        Assert.Empty(offenders);
    }

    /// <summary>
    /// scripts/_gametargets.ps1 declares Get-GameTarget and nothing else
    /// executable, matching the design's "holds the table and nothing else."
    /// </summary>
    [Fact]
    public void TheGameTargetsScriptDeclaresGetGameTarget()
    {
        var root = GetRepositoryRoot();
        var content = File.ReadAllText(
            Path.Combine(root, "scripts", "_gametargets.ps1"));

        Assert.Contains(
            "function Get-GameTarget", content, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every game-specific script sources scripts/_gametargets.ps1 and
    /// exposes -Game with the two-member ValidateSet defaulting to 'Hukbo',
    /// matching the existing scripts' dot-sourcing and parameter style.
    /// </summary>
    [Theory]
    [InlineData("run.ps1")]
    [InlineData("test.ps1")]
    [InlineData("benchmark.ps1")]
    [InlineData("package.ps1")]
    public void EveryGameTargetScriptDeclaresTheGameParameter(string scriptName)
    {
        var root = GetRepositoryRoot();
        var content = File.ReadAllText(Path.Combine(root, "scripts", scriptName));

        Assert.Contains(
            "_gametargets.ps1", content, StringComparison.Ordinal);
        Assert.Contains(
            "ValidateSet('Hukbo', 'Sandata')", content, StringComparison.Ordinal);
        Assert.Contains(
            "$Game = 'Hukbo'", content, StringComparison.Ordinal);
        Assert.Contains(
            "Get-GameTarget -Game $Game", content, StringComparison.Ordinal);
    }

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
