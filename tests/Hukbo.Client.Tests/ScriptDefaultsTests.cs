using System.Linq;
using System.Text.RegularExpressions;
using Hukbo.Diagnostics;

namespace Hukbo.Client.Tests;

/// <summary>
/// Proves the acceptance criterion for plan task 47
/// (docs/plans/2026-08-07-sandata-scaffold.md) plus the RU-29 extension
/// (docs/plans/2026-08-07-ranged-units.md): scripts/verify.ps1 gains a
/// -Game parameter and passes it through to test.ps1 and benchmark.ps1, and
/// with no -Game argument on the command line it runs exactly two
/// benchmark.ps1 invocations -- the original canonical workload at
/// Agents 200 / Ticks 10000 / Seed 1, plus a second, Hukbo-guarded
/// invocation that exercises the ranged combat preset
/// (PrecolonialPhilippinesV5 / RangedStandoffV8). RU-29 added the second
/// invocation on purpose: the previous single-workload gate never exercised
/// the ranged path, so a completely broken ranged combat preset would have
/// left the gate green.
/// </summary>
public sealed class ScriptDefaultsTests
{
    [Fact]
    public void VerifyInvokesBenchmarkExactlyTwiceWithTheCanonicalAndRangedWorkloads()
    {
        var content = ReadScript("verify.ps1");

        var benchmarkInvocations = Regex.Matches(
            content, @"Invoke-RepositoryScript\s+-Name\s+'benchmark\.ps1'");

        Assert.Equal(2, benchmarkInvocations.Count);

        var invocations = benchmarkInvocations.Cast<Match>().ToList();
        var canonicalInvocation = invocations[0];
        var rangedInvocation = invocations[1];

        var canonicalBlock = ExtractBraceBlockAfter(content, canonicalInvocation.Index);

        Assert.Contains("Agents = 200", canonicalBlock, StringComparison.Ordinal);
        Assert.Contains("Ticks = 10000", canonicalBlock, StringComparison.Ordinal);
        Assert.Contains("Seed = 1", canonicalBlock, StringComparison.Ordinal);

        var rangedBlock = ExtractBraceBlockAfter(content, rangedInvocation.Index);

        Assert.Contains(
            "Preset = 'PrecolonialPhilippinesV5'", rangedBlock, StringComparison.Ordinal);
        Assert.Contains(
            "MovementPreset = 'RangedStandoffV8'", rangedBlock, StringComparison.Ordinal);

        var hukboGuard = Regex.Match(content, @"if\s*\(\s*\$Game\s+-eq\s+'Hukbo'\s*\)\s*\{");
        Assert.True(
            hukboGuard.Success,
            "Expected an `if ($Game -eq 'Hukbo') { ... }` guard in verify.ps1.");
        Assert.True(
            hukboGuard.Index > canonicalInvocation.Index,
            "The canonical benchmark.ps1 invocation must run unconditionally -- " +
            "before the Hukbo guard -- so a caller that passes no -Game still runs it.");
        Assert.True(
            hukboGuard.Index < rangedInvocation.Index,
            "The Hukbo guard must precede the ranged benchmark.ps1 invocation.");

        var guardBlock = ExtractBraceBlockAfter(content, hukboGuard.Index);
        Assert.Contains(
            "Invoke-RepositoryScript -Name 'benchmark.ps1'",
            guardBlock,
            StringComparison.Ordinal);
    }

    [Fact]
    public void VerifyInvokesTestExactlyOnce()
    {
        var content = ReadScript("verify.ps1");

        var testInvocations = Regex.Matches(
            content, @"Invoke-RepositoryScript\s+-Name\s+'test\.ps1'");

        Assert.Single(testInvocations.Cast<Match>());
    }

    /// <summary>
    /// verify.ps1 declares -Game with the same two-member ValidateSet and
    /// 'Hukbo' default as every other game-specific script, and passes it
    /// through to the test.ps1 invocation and both benchmark.ps1
    /// invocations, so a caller that never passes -Game resolves to
    /// 'Hukbo' at every layer.
    /// </summary>
    [Fact]
    public void VerifyDeclaresTheGameParameterDefaultingToHukboAndPassesItThrough()
    {
        var content = ReadScript("verify.ps1");

        Assert.Contains(
            "ValidateSet('Hukbo', 'Sandata')", content, StringComparison.Ordinal);
        Assert.Contains("$Game = 'Hukbo'", content, StringComparison.Ordinal);

        var passThroughCount = Regex.Matches(content, @"Game\s*=\s*\$Game").Count;
        Assert.Equal(3, passThroughCount);
    }

    private static string ExtractBraceBlockAfter(string content, int searchStartIndex)
    {
        var openBraceIndex = content.IndexOf('{', searchStartIndex);
        Assert.True(
            openBraceIndex >= 0,
            "Expected an opening brace after the benchmark.ps1 invocation.");

        var depth = 0;
        for (var index = openBraceIndex; index < content.Length; index++)
        {
            if (content[index] == '{')
            {
                depth++;
            }
            else if (content[index] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return content.Substring(
                        openBraceIndex, index - openBraceIndex + 1);
                }
            }
        }

        throw new InvalidOperationException(
            "Unterminated benchmark.ps1 invocation block in verify.ps1.");
    }

    private static string ReadScript(string scriptName)
    {
        var root = GetRepositoryRoot();
        return File.ReadAllText(Path.Combine(root, "scripts", scriptName));
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
