using System.Globalization;
using System.Linq;
using System.Text.Json;
using Hukbo.Core.Determinism;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests;

/// <summary>
/// Freezes the current, unmodified output of
/// <see cref="FormationPlanner.PlanFactionDeployment"/> before the
/// contingent-shape workstream (docs/plans/2026-08-13-contingent-shape.md)
/// touches it. This file is not itself part of that workstream; it is the
/// oracle every task in it must reproduce byte-identically unless a task's
/// own remit is deployment geometry.
/// </summary>
/// <remarks>
/// <para>
/// Five cases exercise five distinct code paths inside
/// <see cref="FormationPlanner"/>: the default 200-agent scenario (the
/// ordinary lattice path), the minimum legal map (a degenerate one-point
/// region), a map half narrower than one body (the region-collapse branch in
/// <c>ResolveRegion</c>), a crowded population that overflows the lattice
/// (the <c>PlanDenseBlock</c> fallback), and a large army against a tall map
/// (the eight-contingent saturation ceiling). Each mirrors an existing
/// scenario in <see cref="FormationPlannerTests"/> rather than inventing a
/// new one, so the geometry this file freezes is geometry already proven
/// meaningful.
/// </para>
/// <para>
/// Each case's fixture entry records every faction-local member's
/// <c>XRaw</c>, <c>YRaw</c>, and <c>ContingentId</c> (the load-bearing
/// output), plus the caller's <see cref="SplitMix64"/> <c>State</c>
/// immediately before and immediately after the call. The <c>State</c> pair
/// matters most for the dense-block case:
/// <c>PlanDenseBlock</c> never receives <c>ref random</c> at all, so its
/// final <c>State</c> must equal its initial <c>State</c> exactly, proving
/// the random stream is left completely untouched rather than merely
/// drawn-from with zero effect (which is the separate case
/// <c>NextJitter</c>, FormationPlanner.cs:360-364, handles by returning 0
/// without drawing whenever <c>jitterLimit</c> is 0 on the ordinary lattice
/// path). A change that made the dense-block path start consuming the stream
/// would not move any of the frozen coordinates it also checks, and would
/// pass every other assertion in this file; the <c>State</c> comparison is
/// what catches it.
/// </para>
/// </remarks>
public sealed class FormationDeploymentFreezeTests
{
    private const string DigestFileName = "formation-deployment-freeze-digest.json";

    [Fact]
    public void Default200_MatchesTheFrozenDeployment()
    {
        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: 200);

        AssertCaseMatchesFixture("Default200", scenario);
    }

    [Fact]
    public void MinimumMap_MatchesTheFrozenDeployment()
    {
        var scenario = Scenario.CreateDefault(totalAgents: 2) with
        {
            MapWidth = 1,
            MapHeight = 1,
            AttackRangeRaw = 2,
            PerceptionRangeRaw = FixedPoint.Scale,
            MovementSpeedRaw = 1,
            BodyRadiusRaw = 1,
        };

        AssertCaseMatchesFixture("MinimumMap", scenario);
    }

    [Fact]
    public void HalfNarrowerThanOneBody_MatchesTheFrozenDeployment()
    {
        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: 2) with
        {
            MapWidth = 9,
            MapHeight = 100,
        };
        scenario.Validate();

        AssertCaseMatchesFixture("HalfNarrowerThanOneBody", scenario);
    }

    [Fact]
    public void DenseBlockFallback_MatchesTheFrozenDeploymentAndLeavesTheStreamUntouched()
    {
        var scenario = Scenario.CreateDefault(seed: 6, totalAgents: 100) with
        {
            MapWidth = 100,
            MapHeight = 100,
        };
        scenario.Validate();

        var (initialState, finalState) =
            AssertCaseMatchesFixture("DenseBlockFallback", scenario);

        // PlanDenseBlock (FormationPlanner.cs) never receives `ref random`,
        // so the dense-block path must leave the stream exactly where it
        // found it.
        Assert.Equal(initialState, finalState);
    }

    [Fact]
    public void EightContingentCeiling_MatchesTheFrozenDeployment()
    {
        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: 512) with
        {
            MapHeight = 2_000,
        };
        scenario.Validate();

        AssertCaseMatchesFixture("EightContingentCeiling", scenario);
    }

    private static (ulong InitialState, ulong FinalState) AssertCaseMatchesFixture(
        string caseName,
        Scenario scenario)
    {
        var digest = LoadDigest();
        var expected = digest.Cases.SingleOrDefault(entry => entry.Name == caseName);

        Assert.False(
            expected is null,
            $"The formation deployment freeze fixture has no case named " +
            $"'{caseName}'.");

        var random = new SplitMix64(scenario.Seed);
        var initialState = random.State;
        // fieldedChiefCount: 0 -- every fixture case below uses the default
        // MovementPresetId (never ContingentShapeV12), so the parameter is
        // ignored; see FormationPlanner.ResolveContingentSizes.
        var deployment = FormationPlanner.PlanFactionDeployment(
            scenario, fieldedChiefCount: 0, ref random);
        var finalState = random.State;

        Assert.Equal(expected!.InitialState, initialState);
        Assert.Equal(expected.FinalState, finalState);
        Assert.Equal(expected.Members.Count, deployment.Length);

        for (var index = 0; index < deployment.Length; index++)
        {
            var member = expected.Members[index];
            var actual = deployment[index];

            Assert.Equal(member.XRaw, actual.XRaw);
            Assert.Equal(member.YRaw, actual.YRaw);
            Assert.Equal(member.ContingentId, actual.ContingentId);
        }

        return (initialState, finalState);
    }

    private static FreezeDigest LoadDigest()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", DigestFileName);

        Assert.True(
            File.Exists(path),
            $"The formation deployment freeze fixture is missing at '{path}'. " +
            "It is committed under tests/Hukbo.Core.Tests/Fixtures and copied " +
            "to the output directory by the project's Fixtures item.");

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;

        var cases = new List<FreezeCase>();
        foreach (var caseElement in root.GetProperty("cases").EnumerateArray())
        {
            var members = new List<FreezeMember>();
            foreach (var memberElement in caseElement.GetProperty("members").EnumerateArray())
            {
                members.Add(
                    new FreezeMember(
                        memberElement.GetProperty("xRaw").GetInt32(),
                        memberElement.GetProperty("yRaw").GetInt32(),
                        memberElement.GetProperty("contingentId").GetInt32()));
            }

            cases.Add(
                new FreezeCase(
                    caseElement.GetProperty("name").GetString() ?? string.Empty,
                    ParseHex(caseElement.GetProperty("initialState").GetString()),
                    ParseHex(caseElement.GetProperty("finalState").GetString()),
                    members));
        }

        return new FreezeDigest(cases);
    }

    private static ulong ParseHex(string? value)
    {
        Assert.False(
            string.IsNullOrWhiteSpace(value),
            "The formation deployment freeze fixture carries an empty state field.");

        return ulong.Parse(
            value!,
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture);
    }

    private sealed record FreezeMember(int XRaw, int YRaw, int ContingentId);

    private sealed record FreezeCase(
        string Name,
        ulong InitialState,
        ulong FinalState,
        IReadOnlyList<FreezeMember> Members);

    private sealed record FreezeDigest(IReadOnlyList<FreezeCase> Cases);

#if HUKBO_CALIBRATION
    /// <summary>
    /// Capture routine for this fixture, gated exactly the way every other
    /// frozen-trajectory capture routine in this project is (see
    /// <c>MovementPresetFreezeTests.CaptureRangedStandoffV8Digest</c>):
    /// reachable only from a <c>[Fact]</c> compiled behind the
    /// <c>HUKBO_CALIBRATION</c> preprocessor symbol, which no script and no
    /// gate stage defines, so it adds zero tests to any ordinary build. Run
    /// once, from a clean Release build:
    ///
    /// <code>
    /// dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj -c Release ^
    ///   -p:DefineConstants=HUKBO_CALIBRATION ^
    ///   --filter FullyQualifiedName~CaptureFormationDeploymentFreezeDigest ^
    ///   --logger "console;verbosity=detailed"
    /// </code>
    ///
    /// Prints one JSON document to stdout in the exact shape
    /// <see cref="LoadDigest"/> reads back: commit it verbatim as
    /// <c>tests/Hukbo.Core.Tests/Fixtures/formation-deployment-freeze-digest.json</c>.
    /// </summary>
    [Fact]
    public void CaptureFormationDeploymentFreezeDigest()
    {
        var cases = new (string Name, Scenario Scenario)[]
        {
            ("Default200", Scenario.CreateDefault(seed: 1, totalAgents: 200)),
            (
                "MinimumMap",
                Scenario.CreateDefault(totalAgents: 2) with
                {
                    MapWidth = 1,
                    MapHeight = 1,
                    AttackRangeRaw = 2,
                    PerceptionRangeRaw = FixedPoint.Scale,
                    MovementSpeedRaw = 1,
                    BodyRadiusRaw = 1,
                }
            ),
            (
                "HalfNarrowerThanOneBody",
                Scenario.CreateDefault(seed: 1, totalAgents: 2) with
                {
                    MapWidth = 9,
                    MapHeight = 100,
                }
            ),
            (
                "DenseBlockFallback",
                Scenario.CreateDefault(seed: 6, totalAgents: 100) with
                {
                    MapWidth = 100,
                    MapHeight = 100,
                }
            ),
            (
                "EightContingentCeiling",
                Scenario.CreateDefault(seed: 1, totalAgents: 512) with
                {
                    MapHeight = 2_000,
                }
            ),
        };

        var caseDocuments = new List<object>();
        foreach (var (name, scenario) in cases)
        {
            var random = new SplitMix64(scenario.Seed);
            var initialState = random.State;
            var deployment = FormationPlanner.PlanFactionDeployment(
                scenario, fieldedChiefCount: 0, ref random);
            var finalState = random.State;

            caseDocuments.Add(new
            {
                name,
                initialState = initialState.ToString("X16", CultureInfo.InvariantCulture),
                finalState = finalState.ToString("X16", CultureInfo.InvariantCulture),
                members = deployment.Select(member => new
                {
                    xRaw = member.XRaw,
                    yRaw = member.YRaw,
                    contingentId = member.ContingentId,
                }),
            });
        }

        var document = new { cases = caseDocuments };

        Console.WriteLine(
            JsonSerializer.Serialize(
                document,
                new JsonSerializerOptions { WriteIndented = true }));
    }
#endif
}
