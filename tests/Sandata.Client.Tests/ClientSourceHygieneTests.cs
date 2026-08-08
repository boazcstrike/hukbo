using Hukbo.Diagnostics;

namespace Sandata.Client.Tests;

/// <summary>
/// Task 80's own standing proof: <c>src/Sandata.Client</c> never spells out
/// <c>OrderQueue.SubmitValidated</c> anywhere — the defect this task fixed
/// was <c>SandataGame.cs</c> and <c>PathDrawTool.cs</c> calling that method
/// directly instead of going through <see cref="Sandata.Core.Simulation.SandataSimulation.SubmitOrder"/>,
/// the one production door that also emits
/// <see cref="Sandata.Core.Events.MissionEventKind.OrderRejected"/> on
/// rejection. Duplicates the scanning technique
/// <c>Sandata.Core.Tests.SandataSourceHygieneTests</c> already uses —
/// enumerate <c>*.cs</c> under a directory excluding <c>bin</c>/<c>obj</c>,
/// skip a line whose trimmed text starts with <c>//</c> before testing it —
/// rather than inventing a second one. That file is not this task's to edit,
/// so this is this project's own copy of the same technique.
/// </summary>
public sealed class ClientSourceHygieneTests
{
    [Fact]
    public void SandataClientSourceNeverCallsSubmitValidatedDirectly()
    {
        var root = GetRepositoryRoot();
        var sandataClientDirectory = Path.Combine(root, "src", "Sandata.Client");

        var offenders = FindOffendingCodeLines(
            root,
            EnumerateSourceFiles(sandataClientDirectory),
            line => line.Contains("SubmitValidated", StringComparison.Ordinal));

        Assert.Empty(offenders);
    }

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
