using System.Linq;
using System.Text.RegularExpressions;
using Hukbo.Diagnostics;

namespace Hukbo.Client.Tests;

/// <summary>
/// Proves the acceptance criterion for plan task 47
/// (docs/plans/2026-08-07-sandata-scaffold.md): scripts/verify.ps1 gains a
/// -Game parameter and passes it through to test.ps1 and benchmark.ps1, and
/// with no -Game argument on the command line it still runs exactly the
/// command sequence it ran before this change -- one test.ps1 invocation and
/// one benchmark.ps1 invocation at Agents 200 / Ticks 10000 / Seed 1.
///
/// Design section 14, "When verify.ps1 starts running both games," is
/// explicit that the default gate keeps running the Hukbo workload alone
/// until task 51 records a Sandata baseline, and that a second benchmark
/// invocation is its own later task. A wrong implementation that adds a
/// second, unconditional benchmark.ps1 call for Sandata makes
/// <see cref="VerifyInvokesBenchmarkExactlyOnceWithTheCanonicalWorkload"/>
/// red, because that fact asserts the count is exactly one.
/// </summary>
public sealed class ScriptDefaultsTests
{
    [Fact]
    public void VerifyInvokesBenchmarkExactlyOnceWithTheCanonicalWorkload()
    {
        var content = ReadScript("verify.ps1");

        var benchmarkInvocations = Regex.Matches(
            content, @"Invoke-RepositoryScript\s+-Name\s+'benchmark\.ps1'");

        var onlyBenchmarkInvocation = Assert.Single(benchmarkInvocations.Cast<Match>());

        var block = ExtractBraceBlockAfter(content, onlyBenchmarkInvocation.Index);

        Assert.Contains("Agents = 200", block, StringComparison.Ordinal);
        Assert.Contains("Ticks = 10000", block, StringComparison.Ordinal);
        Assert.Contains("Seed = 1", block, StringComparison.Ordinal);
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
    /// through to both the test.ps1 and the benchmark.ps1 invocation, so a
    /// caller that never passes -Game resolves to 'Hukbo' at every layer.
    /// </summary>
    [Fact]
    public void VerifyDeclaresTheGameParameterDefaultingToHukboAndPassesItThrough()
    {
        var content = ReadScript("verify.ps1");

        Assert.Contains(
            "ValidateSet('Hukbo', 'Sandata')", content, StringComparison.Ordinal);
        Assert.Contains("$Game = 'Hukbo'", content, StringComparison.Ordinal);

        var passThroughCount = Regex.Matches(content, @"Game\s*=\s*\$Game").Count;
        Assert.Equal(2, passThroughCount);
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
