using System.Linq;
using System.Text.RegularExpressions;
using Hukbo.Diagnostics;

namespace Hukbo.Client.Tests;

/// <summary>
/// Proves the acceptance criterion for plan task 47 of Sandata's
/// scaffold plan, the RU-29 extension
/// (the ranged units plan), and task 14 of the archived
/// "Battlefield realism" plan (archived once its build merged):
/// scripts/verify.ps1 gains
/// a -Game parameter and passes it through to test.ps1 and benchmark.ps1,
/// and with no -Game argument on the command line it runs exactly five
/// benchmark.ps1 invocations -- the original canonical workload at
/// Agents 200 / Ticks 10000 / Seed 1, a second, Hukbo-guarded invocation
/// that exercises the ranged combat preset (PrecolonialPhilippinesV5 /
/// RangedStandoffV8), a third, also Hukbo-guarded, that exercises the
/// V10 retreat rung on the same map (PrecolonialPhilippinesV5 /
/// BattlefieldRealismV10), and a fourth that exercises the preset the client
/// actually ships (PrecolonialPhilippinesV5 / LastStandEngagementV11).
/// RU-29 added the second invocation because the
/// previous single-workload gate never exercised the ranged path; task 14
/// added the third for the same reason, so a broken BackingAway retreat
/// could not hide behind a green V8 workload that never reaches it. Design
/// section 9.2 (the battlefield realism design) records
/// why the V10 workload is a third block rather than a repointed V8 one: the
/// V8 block and its frozen digest stay the leak detector proving V10's
/// changes never reached V8. The last-stand engagement plan, task 11,
/// added the fourth
/// under that same precedent, when V11 rather than V10 became what the client
/// selects. The cohort lateral spread plan, task 13, added the fifth
/// on 2026-08-14, when CohortLateralSpreadV13 in turn became what the client
/// selects; the V11 block stays as the leak detector for it.
/// </summary>
public sealed class ScriptDefaultsTests
{
    [Fact]
    public void VerifyInvokesBenchmarkExactlySixTimesWithTheCanonicalRangedV10V11V13AndV14Workloads()
    {
        var content = ReadScript("verify.ps1");

        var benchmarkInvocations = Regex.Matches(
            content, @"Invoke-RepositoryScript\s+-Name\s+'benchmark\.ps1'");

        // Five Hukbo workloads, then Sandata's, which the bare gate adds and
        // an explicit -Game does not reach. The five Hukbo blocks are asserted
        // by position below and are unaffected by the sixth.
        // Six Hukbo workloads plus Sandata's one.
        Assert.Equal(7, benchmarkInvocations.Count);

        var invocations = benchmarkInvocations.Cast<Match>().ToList();
        var canonicalInvocation = invocations[0];
        var rangedInvocation = invocations[1];
        var v10Invocation = invocations[2];
        var v11Invocation = invocations[3];
        var v13Invocation = invocations[4];
        var v14Invocation = invocations[5];

        var canonicalBlock = ExtractBraceBlockAfter(content, canonicalInvocation.Index);

        Assert.Contains("Agents = 200", canonicalBlock, StringComparison.Ordinal);
        Assert.Contains("Ticks = 10000", canonicalBlock, StringComparison.Ordinal);
        Assert.Contains("Seed = 1", canonicalBlock, StringComparison.Ordinal);

        var rangedBlock = ExtractBraceBlockAfter(content, rangedInvocation.Index);

        Assert.Contains(
            "Preset = 'PrecolonialPhilippinesV5'", rangedBlock, StringComparison.Ordinal);
        Assert.Contains(
            "MovementPreset = 'RangedStandoffV8'", rangedBlock, StringComparison.Ordinal);

        var v10Block = ExtractBraceBlockAfter(content, v10Invocation.Index);

        Assert.Contains(
            "Preset = 'PrecolonialPhilippinesV5'", v10Block, StringComparison.Ordinal);
        Assert.Contains(
            "MovementPreset = 'BattlefieldRealismV10'", v10Block, StringComparison.Ordinal);

        var v11Block = ExtractBraceBlockAfter(content, v11Invocation.Index);

        Assert.Contains(
            "Preset = 'PrecolonialPhilippinesV5'", v11Block, StringComparison.Ordinal);
        Assert.Contains(
            "MovementPreset = 'LastStandEngagementV11'",
            v11Block,
            StringComparison.Ordinal);

        var v13Block = ExtractBraceBlockAfter(content, v13Invocation.Index);

        Assert.Contains(
            "Preset = 'PrecolonialPhilippinesV5'", v13Block, StringComparison.Ordinal);
        Assert.Contains(
            "MovementPreset = 'CohortLateralSpreadV13'",
            v13Block,
            StringComparison.Ordinal);

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
        Assert.True(
            rangedInvocation.Index < v10Invocation.Index,
            "The V10 benchmark.ps1 invocation must follow the ranged (V8) one -- " +
            "the V8 block stays untouched and the V10 block is the addition.");
        Assert.True(
            v10Invocation.Index < v11Invocation.Index,
            "The V11 benchmark.ps1 invocation must follow the V10 one, for the " +
            "same reason: earlier blocks stay untouched and each new shipped " +
            "preset appends its own.");
        Assert.True(
            v11Invocation.Index < v13Invocation.Index,
            "The V13 benchmark.ps1 invocation must follow the V11 one, for the " +
            "same reason again: the V11 block stays untouched as the leak " +
            "detector proving V13's riffled deployment never reached it.");
        Assert.True(
            v13Invocation.Index < v14Invocation.Index,
            "The V14 benchmark.ps1 invocation must follow the V13 one, for the " +
            "same reason once more: the V13 block stays untouched as the leak " +
            "detector proving V14's evasive rungs never reached it.");

        var v14Block = ExtractBraceBlockAfter(content, v14Invocation.Index);

        Assert.Contains(
            "Preset = 'PrecolonialPhilippinesV5'", v14Block, StringComparison.Ordinal);
        Assert.Contains(
            "MovementPreset = 'EvasiveFootworkV14'",
            v14Block,
            StringComparison.Ordinal);

        var guardBlock = ExtractBraceBlockAfter(content, hukboGuard.Index);
        Assert.Contains(
            "Invoke-RepositoryScript -Name 'benchmark.ps1'",
            guardBlock,
            StringComparison.Ordinal);
        Assert.Contains(
            "MovementPreset = 'BattlefieldRealismV10'",
            guardBlock,
            StringComparison.Ordinal);
    }

    [Fact]
    public void VerifyInvokesTestOncePerGame()
    {
        var content = ReadScript("verify.ps1");

        var testInvocations = Regex.Matches(
            content, @"Invoke-RepositoryScript\s+-Name\s+'test\.ps1'");

        Assert.Equal(2, testInvocations.Count);

        // The second invocation is Sandata's, and it is reachable only when
        // the caller named no game at all, so an explicit -Game still runs
        // exactly one game's suite.
        var second = testInvocations.Cast<Match>().ElementAt(1);
        Assert.Contains(
            BothGamesGuard,
            content[..second.Index],
            StringComparison.Ordinal);
        Assert.Contains(
            "Game = 'Sandata'",
            ExtractBraceBlockAfter(content, second.Index),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The bare gate runs both games and an explicit <c>-Game</c> runs one.
    /// Sandata's stages sit behind a guard on whether the caller bound the
    /// parameter at all, rather than behind a comparison against its value,
    /// because <c>-Game Hukbo</c> and no <c>-Game</c> resolve to the same
    /// value and must not resolve to the same behaviour.
    /// </summary>
    [Fact]
    public void VerifyRunsBothGamesOnlyWhenTheCallerNamedNeither()
    {
        var content = ReadScript("verify.ps1");

        Assert.Contains(BothGamesGuard, content, StringComparison.Ordinal);
    }

    /// <summary>
    /// The guard that makes the bare gate run both games. Matched literally,
    /// so that rewriting it into a comparison against <c>$Game</c> — which
    /// would silently make <c>-Game Hukbo</c> run Sandata as well — fails
    /// here rather than doubling somebody's gate without telling them.
    /// </summary>
    private const string BothGamesGuard =
        "if (-not $PSBoundParameters.ContainsKey('Game'))";

    /// <summary>
    /// verify.ps1 declares -Game with the same two-member ValidateSet and
    /// 'Hukbo' default as every other game-specific script, and passes it
    /// through to the test.ps1 invocation and all five benchmark.ps1
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

        // Every script invocation verify.ps1 makes must hand the game through
        // rather than letting a callee re-default it. The count rises by one
        // with each shipped preset that earns its own benchmark block; it is
        // seven since the V14 evasive-footwork workload was appended.
        var passThroughCount = Regex.Matches(content, @"Game\s*=\s*\$Game").Count;
        Assert.Equal(7, passThroughCount);
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
