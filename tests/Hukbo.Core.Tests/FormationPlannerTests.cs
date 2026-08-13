using System.Collections.Immutable;
using Hukbo.Core.Determinism;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests;

/// <summary>
/// Starting-deployment contract: both factions open a battle from the same
/// arrangement, mirrored across the vertical centre line, arranged as several
/// separated contingents, with no two bodies overlapping before the first tick.
/// </summary>
public sealed class FormationPlannerTests
{
    /// <summary>
    /// The jitter reach on the default scenario. Lattice spacing there is six
    /// body radii, which is 25.5 world units at the enlarged 4.25-world-unit
    /// collision radius (task C1,
    /// the collision report and window shell plan; it
    /// was 24 world units before that change), and the jitter is
    /// half the clearance above a body diameter, so a warrior sits within nine
    /// units of its lattice cell and two independently jittered plans differ
    /// by at most eighteen. The value was eight and sixteen respectively
    /// before task C1.
    /// </summary>
    private const int DefaultJitterReachRaw = 9 * FixedPoint.Scale;

    [Theory]
    [InlineData(1UL, 200)]
    [InlineData(2UL, 200)]
    [InlineData(7UL, 60)]
    [InlineData(0xDEADBEEFUL, 20)]
    [InlineData(3UL, 2)]
    public void BothFactionsDeployAsExactMirrorsAcrossTheVerticalCentreLine(
        ulong seed,
        int totalAgents)
    {
        var scenario = Scenario.CreateDefault(seed, totalAgents);
        var mapWidthRaw = scenario.MapWidth * FixedPoint.Scale;
        var simulation = BattleSimulation.Create(scenario);
        var perFaction = scenario.AgentsPerFaction;

        for (var index = 0; index < perFaction; index++)
        {
            var left = simulation.Agents[index];
            var right = simulation.Agents[perFaction + index];

            Assert.Equal(0, left.FactionId);
            Assert.Equal(1, right.FactionId);
            Assert.Equal(mapWidthRaw - left.XRaw, right.XRaw);
            Assert.Equal(left.YRaw, right.YRaw);
        }
    }

    [Theory]
    [InlineData(1UL, 200)]
    [InlineData(2UL, 60)]
    [InlineData(3UL, 2)]
    [InlineData(4UL, 200)]
    [InlineData(5UL, 26)]
    public void NoTwoBodiesComeWithinContactBeforeTheFirstTick(
        ulong seed,
        int totalAgents)
    {
        var scenario = Scenario.CreateDefault(seed, totalAgents);
        var simulation = BattleSimulation.Create(scenario);

        AssertClearOfContact(simulation, scenario.BodyRadiusRaw);
    }

    [Theory]
    [InlineData(1280, 720, 200)]
    [InlineData(1280, 720, 60)]
    [InlineData(182, 720, 60)]
    [InlineData(100, 300, 200)]
    public void EachFactionDeploysInsideItsOwnHalfOfTheMap(
        int mapWidth,
        int mapHeight,
        int totalAgents)
    {
        var scenario = Scenario.CreateDefault(seed: 1, totalAgents) with
        {
            MapWidth = mapWidth,
            MapHeight = mapHeight,
        };
        scenario.Validate();
        var centreLineRaw = (mapWidth * FixedPoint.Scale) / 2;
        var simulation = BattleSimulation.Create(scenario);

        Assert.All(
            simulation.Agents,
            agent =>
            {
                if (agent.FactionId == 0)
                {
                    Assert.True(
                        agent.XRaw <= centreLineRaw,
                        $"Entity {agent.EntityId} crossed the centre line at " +
                        $"{agent.XRaw}.");
                }
                else
                {
                    Assert.True(
                        agent.XRaw >= centreLineRaw,
                        $"Entity {agent.EntityId} crossed the centre line at " +
                        $"{agent.XRaw}.");
                }
            });
    }

    /// <summary>
    /// The visible result the change exists for: an army opens as several
    /// separated groups rather than one cloud. Faction 0's vertical positions
    /// are clustered by gap. On the default 200-agent scenario the lattice
    /// spacing is 6 body radii, which is 25.5 world units at the enlarged
    /// 4.25-world-unit collision radius (task C1,
    /// the collision report and window shell plan).
    /// Regenerated for that task: a real run across seeds 1, 2, and 9
    /// measured the widest within-group row gap at about 22 world units and
    /// the narrowest between-group gap at about 44, both markedly closer
    /// together than at the old four-world-unit radius (spacing 24,
    /// between-group gap comfortably above the old 48-unit threshold). A
    /// 32-unit threshold sits clear of both bounds with roughly ten world
    /// units of margin on either side; the old 48-unit threshold sat above
    /// every measured between-group gap and merged contingents into fewer,
    /// unequal groups.
    /// </summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(9UL)]
    public void ADefaultArmyOpensAsFiveSeparatedContingentsOfEqualSize(ulong seed)
    {
        var scenario = Scenario.CreateDefault(seed, totalAgents: 200);
        var simulation = BattleSimulation.Create(scenario);

        foreach (var factionId in new[] { 0, 1 })
        {
            var groupSizes = GroupSizesByVerticalGap(simulation, factionId);

            Assert.Equal(5, groupSizes.Count);
            Assert.All(groupSizes, size => Assert.Equal(20, size));
        }
    }

    /// <summary>
    /// The contingent count saturates at eight rather than growing with the
    /// army: a larger army makes each group bigger, not the frame busier. The
    /// map is made taller so that eight groups still leave gaps a reader - and
    /// this test's gap threshold - can see; on a short map the groups fill
    /// their lanes and merge visually, which is a readability limit of very
    /// large armies rather than a change in the arrangement.
    /// </summary>
    [Fact]
    public void ALargeArmyStopsAtEightContingents()
    {
        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: 512) with
        {
            MapHeight = 2_000,
        };
        scenario.Validate();
        var simulation = BattleSimulation.Create(scenario);

        var groupSizes = GroupSizesByVerticalGap(simulation, factionId: 0);

        Assert.Equal(8, groupSizes.Count);
        Assert.Equal(scenario.AgentsPerFaction, groupSizes.Sum());
    }

    [Fact]
    public void TheSameSeedDeploysIdenticallyAndAnotherSeedDoesNot()
    {
        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: 60);
        var repeated = Scenario.CreateDefault(seed: 1, totalAgents: 60);
        var other = Scenario.CreateDefault(seed: 2, totalAgents: 60);

        var first = Positions(BattleSimulation.Create(scenario));
        var second = Positions(BattleSimulation.Create(repeated));
        var third = Positions(BattleSimulation.Create(other));

        Assert.Equal(first, second);
        Assert.NotEqual(first, third);
    }

    /// <summary>
    /// Only the jitter is seeded, so a different seed must move bodies without
    /// moving the group structure: the same contingent still holds the same
    /// warriors and stays in the same lane.
    /// </summary>
    [Fact]
    public void ADifferentSeedMovesBodiesWithoutMovingTheContingentStructure()
    {
        var first = Positions(
            BattleSimulation.Create(Scenario.CreateDefault(seed: 1, totalAgents: 200)));
        var second = Positions(
            BattleSimulation.Create(Scenario.CreateDefault(seed: 2, totalAgents: 200)));

        Assert.NotEqual(first, second);

        for (var index = 0; index < first.Count; index++)
        {
            Assert.InRange(
                second[index].YRaw,
                first[index].YRaw - (2 * DefaultJitterReachRaw),
                first[index].YRaw + (2 * DefaultJitterReachRaw));
        }
    }

    [Fact]
    public void ASingleWarriorPerFactionDeploysWithoutThrowing()
    {
        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: 2);
        var mapWidthRaw = scenario.MapWidth * FixedPoint.Scale;

        var simulation = BattleSimulation.Create(scenario);

        Assert.Equal(2, simulation.Agents.Count);
        Assert.Equal(
            mapWidthRaw - simulation.Agents[0].XRaw,
            simulation.Agents[1].XRaw);
    }

    [Fact]
    public void TheMinimumMapDeploysInsideValidatedBounds()
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

        var simulation = BattleSimulation.Create(scenario);

        AssertInsideBounds(simulation, scenario);
        AssertClearOfContact(simulation, scenario.BodyRadiusRaw);
    }

    /// <summary>
    /// A map half narrower than one body, which collapses the deployment region
    /// onto a single legal coordinate. That coordinate and its mirror must both
    /// be legal centres, or the repair pass corrects the two sides by different
    /// amounts and leaves bodies in contact.
    /// </summary>
    [Fact]
    public void AHalfNarrowerThanOneBodyStillDeploysInsideValidatedBounds()
    {
        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: 2) with
        {
            MapWidth = 9,
            MapHeight = 100,
        };
        scenario.Validate();

        var simulation = BattleSimulation.Create(scenario);

        AssertInsideBounds(simulation, scenario);
        AssertClearOfContact(simulation, scenario.BodyRadiusRaw);
    }

    [Fact]
    public void TheMaximumMapDeploysInsideValidatedBounds()
    {
        var scenario = Scenario.CreateDefault(seed: 4, totalAgents: 60) with
        {
            MapWidth = Scenario.MaximumMapDimension,
            MapHeight = Scenario.MaximumMapDimension,
        };

        var simulation = BattleSimulation.Create(scenario);

        AssertInsideBounds(simulation, scenario);
        AssertClearOfContact(simulation, scenario.BodyRadiusRaw);
    }

    /// <summary>
    /// A crowded but legal population, where separated contingents no longer
    /// fit and the planner falls back to one dense block. The fallback is
    /// confirmed by its own signature rather than assumed: spacing sits at the
    /// floor of one body diameter plus one raw unit, and with no room for
    /// jitter every warrior lands on an exact multiple of that spacing away
    /// from its neighbours.
    /// </summary>
    /// <remarks>
    /// This configuration cannot be placed at all before this change: the
    /// random spawn band leaves bodies the repair scan cannot separate, and
    /// <see cref="BattleSimulation.Create"/> throws. Densities closer still to
    /// the bound <see cref="Scenario.Validate"/> accepts remain unplaceable,
    /// which is unchanged behaviour and not addressed here.
    /// </remarks>
    [Fact]
    public void ACrowdedPopulationFallsBackToADenseBlockWithoutOverlapping()
    {
        var scenario = Scenario.CreateDefault(seed: 6, totalAgents: 100) with
        {
            MapWidth = 100,
            MapHeight = 100,
        };
        scenario.Validate();
        var stepRaw = (2 * scenario.BodyRadiusRaw) + 1;

        var simulation = BattleSimulation.Create(scenario);

        AssertInsideBounds(simulation, scenario);
        AssertClearOfContact(simulation, scenario.BodyRadiusRaw);

        var verticalPositions = simulation.Agents
            .Where(agent => agent.FactionId == 0)
            .Select(agent => agent.YRaw)
            .Distinct()
            .OrderBy(value => value)
            .ToArray();

        for (var index = 1; index < verticalPositions.Length; index++)
        {
            var gap = verticalPositions[index] - verticalPositions[index - 1];
            Assert.True(
                gap % stepRaw == 0,
                $"Row gap {gap} is not a multiple of the dense-block step " +
                $"{stepRaw}, so the fallback lattice was not used.");
        }
    }

    /// <summary>
    /// Membership is dealt round-robin across contingents regardless of which
    /// placement path ran. The contingent count is not asserted against a
    /// duplicated copy of <c>FormationPlanner</c>'s internal arithmetic; it is
    /// read back from the result itself, as one more than the highest
    /// <c>ContingentId</c> dealt, which the round-robin rule guarantees appears
    /// at least once whenever the faction is at least as large as the
    /// contingent count it produces.
    /// </summary>
    [Theory]
    [InlineData(1UL, 200, 1280, 720)]
    [InlineData(6UL, 100, 100, 100)]
    public void MembershipDealsRoundRobinAcrossContingentsOnBothPlacementPaths(
        ulong seed,
        int totalAgents,
        int mapWidth,
        int mapHeight)
    {
        var scenario = Scenario.CreateDefault(seed, totalAgents) with
        {
            MapWidth = mapWidth,
            MapHeight = mapHeight,
        };
        scenario.Validate();
        var random = new SplitMix64(scenario.Seed);

        // fieldedChiefCount: 0 -- this scenario uses the default
        // MovementPresetId (never ContingentShapeV12), so the parameter is
        // ignored; see FormationPlanner.ResolveContingentSizes.
        var deployment = FormationPlanner.PlanFactionDeployment(
            scenario, fieldedChiefCount: 0, ref random);

        var contingentCount = deployment.Max(member => member.ContingentId) + 1;
        for (var localIndex = 0; localIndex < deployment.Length; localIndex++)
        {
            Assert.Equal(
                localIndex % contingentCount,
                deployment[localIndex].ContingentId);
        }
    }

    // ----- Task 5: exact draw-count contract -----
    //
    // NextJitter (FormationPlanner.cs) draws once per axis per warrior when
    // its lattice's JitterLimit is positive, and draws nothing at all when
    // JitterLimit is zero or when the dense-block fallback runs (it never
    // receives `ref random`). SplitMix64's gamma is fixed per draw, so the
    // post-call State uniquely encodes how many draws happened for a known
    // seed: replaying the same seed for the known-correct draw count and
    // comparing states is an exact assertion on draw count, not merely a
    // before/after inequality.

    [Fact]
    public void DefaultTwoHundredAgentScenarioDrawsExactlyTwoHundredTimes()
    {
        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: 200);

        AssertDrawCount(scenario, expectedDrawCount: 200);
    }

    [Fact]
    public void FiveHundredTwelveAgentScenarioDrawsExactlyFiveHundredTwelveTimes()
    {
        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: 512) with
        {
            MapHeight = 2_000,
        };
        scenario.Validate();

        AssertDrawCount(scenario, expectedDrawCount: 512);
    }

    [Fact]
    public void TheMinimumMapScenarioDrawsExactlyTwoTimes()
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

        AssertDrawCount(scenario, expectedDrawCount: 2);
    }

    [Fact]
    public void AHalfNarrowerThanOneBodyScenarioLeavesTheStreamUntouched()
    {
        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: 2) with
        {
            MapWidth = 9,
            MapHeight = 100,
        };
        scenario.Validate();

        AssertDrawCount(scenario, expectedDrawCount: 0);
    }

    [Fact]
    public void ACrowdedDenseBlockFallbackScenarioLeavesTheStreamUntouched()
    {
        var scenario = Scenario.CreateDefault(seed: 6, totalAgents: 100) with
        {
            MapWidth = 100,
            MapHeight = 100,
        };
        scenario.Validate();

        AssertDrawCount(scenario, expectedDrawCount: 0);
    }

    // ----- Task 4: MovementPresetId.ContingentShapeV12 authored sizes -----

    /// <summary>
    /// Under <see cref="MovementPresetId.ContingentShapeV12"/> with an
    /// authored <see cref="Scenario.ContingentSizes"/>, membership is still
    /// dealt round-robin (as
    /// <see cref="MembershipDealsRoundRobinAcrossContingentsOnBothPlacementPaths"/>
    /// proves for the square-root split), so the resulting per-contingent
    /// counts land on the authored array exactly, including its earliest
    /// entry, which is deliberately not the largest.
    /// </summary>
    [Fact]
    public void AuthoredContingentSizesUnderContingentShapeV12AreDealtExactly()
    {
        var authoredSizes = ImmutableArray.Create(10, 30, 60);
        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: 200) with
        {
            MovementPreset = MovementPresetId.ContingentShapeV12,
            ContingentSizes = authoredSizes,
        };
        scenario.Validate();
        var random = new SplitMix64(scenario.Seed);

        // fieldedChiefCount: 0 -- ContingentSizes is authored, so the
        // authored-array branch wins regardless of chief count; see
        // FormationPlanner.ResolveContingentSizes.
        var deployment = FormationPlanner.PlanFactionDeployment(
            scenario, fieldedChiefCount: 0, ref random);

        var counts = new int[authoredSizes.Length];
        foreach (var member in deployment)
        {
            counts[member.ContingentId]++;
        }

        Assert.Equal(authoredSizes, counts);
    }

    /// <summary>
    /// Authored sizes whose earliest entry is not the largest
    /// (<c>[10, 30, 60]</c>) must still size the shared lattice off the
    /// largest contingent, not off index zero: the named hazard this task
    /// guards against. If the lattice were sized for the smallest contingent
    /// instead, the sixty-member contingent would overflow its cell and land
    /// on top of a neighbour.
    /// </summary>
    [Fact]
    public void AuthoredContingentSizesWhoseEarliestEntryIsNotTheLargestProduceNoOverlap()
    {
        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: 200) with
        {
            MovementPreset = MovementPresetId.ContingentShapeV12,
            ContingentSizes = ImmutableArray.Create(10, 30, 60),
        };
        scenario.Validate();

        var simulation = BattleSimulation.Create(scenario);

        AssertClearOfContact(simulation, scenario.BodyRadiusRaw);
    }

    /// <summary>
    /// <see cref="MovementPresetId.LastStandEngagementV11"/> with an authored
    /// <see cref="Scenario.ContingentSizes"/> populated deploys
    /// byte-identically to the same scenario without the field: the gate is
    /// on preset identity, and a populated field alone admits nothing under
    /// any preset except <see cref="MovementPresetId.ContingentShapeV12"/>.
    /// </summary>
    [Fact]
    public void LastStandEngagementV11IgnoresAnAuthoredContingentSizesField()
    {
        var withoutField = Scenario.CreateDefault(seed: 1, totalAgents: 200) with
        {
            MovementPreset = MovementPresetId.LastStandEngagementV11,
        };
        var withField = Scenario.CreateDefault(seed: 1, totalAgents: 200) with
        {
            MovementPreset = MovementPresetId.LastStandEngagementV11,
            ContingentSizes = ImmutableArray.Create(10, 30, 60),
        };
        withoutField.Validate();
        withField.Validate();
        var randomWithoutField = new SplitMix64(withoutField.Seed);
        var randomWithField = new SplitMix64(withField.Seed);

        // fieldedChiefCount: 0 -- MovementPresetId.LastStandEngagementV11
        // always takes the square-root path, so the parameter is ignored on
        // both scenarios here; see FormationPlanner.ResolveContingentSizes.
        var deploymentWithoutField = FormationPlanner.PlanFactionDeployment(
            withoutField, fieldedChiefCount: 0, ref randomWithoutField);
        var deploymentWithField = FormationPlanner.PlanFactionDeployment(
            withField, fieldedChiefCount: 0, ref randomWithField);

        Assert.Equal(deploymentWithoutField, deploymentWithField);
        Assert.Equal(randomWithoutField.State, randomWithField.State);
    }

    // ----- Tasks 6 and 7: MovementPresetId.ContingentShapeV12's
    // chief-derived contingent count -----
    //
    // FormationPlanner never resolves a loadout and never learns what a Datu
    // is (contingent-shape design section 2); the caller
    // (BattleSimulation.Create) counts fielded chiefs once, from its own
    // already-resolved loadouts, and passes the count in. The retired test
    // this section replaces,
    // ContingentShapeV12WithoutAuthoredSizesDeploysIdenticallyToLastStandEngagementV11,
    // asserted that an unauthored V12 scenario deployed exactly like its V11
    // twin; that premise is what tasks 6 and 7 exist to end, so the four
    // cases below pin the chief-derived rule itself, directly against
    // FormationPlanner's public entry point, rather than depending on any
    // particular combat preset's roster shape to produce a given chief count.

    [Fact]
    public void ZeroFieldedChiefsFloorsToOneContingent()
    {
        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: 200) with
        {
            MovementPreset = MovementPresetId.ContingentShapeV12,
        };
        scenario.Validate();
        var random = new SplitMix64(scenario.Seed);

        var deployment = FormationPlanner.PlanFactionDeployment(
            scenario, fieldedChiefCount: 0, ref random);

        Assert.Equal(0, deployment.Max(member => member.ContingentId));
    }

    [Fact]
    public void OneFieldedChiefProducesOneContingent()
    {
        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: 200) with
        {
            MovementPreset = MovementPresetId.ContingentShapeV12,
        };
        scenario.Validate();
        var random = new SplitMix64(scenario.Seed);

        var deployment = FormationPlanner.PlanFactionDeployment(
            scenario, fieldedChiefCount: 1, ref random);

        Assert.Equal(0, deployment.Max(member => member.ContingentId));
    }

    [Fact]
    public void ExactlyEightFieldedChiefsProduceExactlyEightContingents()
    {
        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: 200) with
        {
            MovementPreset = MovementPresetId.ContingentShapeV12,
        };
        scenario.Validate();
        var random = new SplitMix64(scenario.Seed);

        var deployment = FormationPlanner.PlanFactionDeployment(
            scenario, fieldedChiefCount: 8, ref random);

        Assert.Equal(7, deployment.Max(member => member.ContingentId));
    }

    [Fact]
    public void MoreThanEightFieldedChiefsAreCappedAtEightContingents()
    {
        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: 200) with
        {
            MovementPreset = MovementPresetId.ContingentShapeV12,
        };
        scenario.Validate();
        var random = new SplitMix64(scenario.Seed);

        var deployment = FormationPlanner.PlanFactionDeployment(
            scenario, fieldedChiefCount: 12, ref random);

        Assert.Equal(7, deployment.Max(member => member.ContingentId));
    }

    /// <summary>
    /// The planning-level half of the "every contingent gets a chief"
    /// guarantee (task 7): <see cref="RosterCountExpansion.Expand"/> groups a
    /// rank's warriors into a contiguous block of the lowest faction-local
    /// indices when that rank occupies roster row zero -- true of both
    /// combat presets Hukbo ships, <c>PrecolonialPhilippinesV5</c> and
    /// <c>PrecolonialPhilippinesV6</c> -- so the first
    /// <paramref name="fieldedChiefCount"/> faction-local indices are exactly
    /// where the chiefs stand. Since <c>ResolveContingentSizesByChiefCount</c>
    /// sets the contingent count to that same
    /// <paramref name="fieldedChiefCount"/> (clamped only by the cap and the
    /// warrior-count floor, neither of which binds here), and the dealing
    /// loop in <see cref="FormationPlanner.PlanFactionDeployment"/> (never
    /// modified by this task) deals contingent
    /// <c>localIndex % contingentCount</c> whenever every declared size
    /// already equals what that plain modulus gives it -- true of an even
    /// split that never varies by more than one -- those first
    /// <paramref name="fieldedChiefCount"/> indices land on every contingent
    /// id exactly once. This is the guarantee proven, and the only level it
    /// is proven at: what happens to it afterward, under a preset that also
    /// runs <c>CohortDeploymentAssignment</c>, is a separate, documented
    /// caveat (see
    /// <c>ContingentShapeV12Tests.CohortDeploymentAssignmentCanConcentrateEveryFieldedChiefIntoOneContingent</c>).
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(8)]
    public void EveryContingentReceivesAtLeastOneOfTheFirstFieldedChiefCountLocalIndices(
        int fieldedChiefCount)
    {
        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: 200) with
        {
            MovementPreset = MovementPresetId.ContingentShapeV12,
        };
        scenario.Validate();
        var random = new SplitMix64(scenario.Seed);

        var deployment = FormationPlanner.PlanFactionDeployment(
            scenario, fieldedChiefCount, ref random);

        var contingentCount = deployment.Max(member => member.ContingentId) + 1;
        Assert.Equal(fieldedChiefCount, contingentCount);

        var contingentsAmongChiefs = deployment
            .Take(fieldedChiefCount)
            .Select(member => member.ContingentId)
            .Distinct()
            .Count();

        Assert.Equal(contingentCount, contingentsAmongChiefs);
    }

    /// <summary>
    /// Replays <paramref name="scenario"/>'s own seed for
    /// <paramref name="expectedDrawCount"/> raw draws and asserts the
    /// resulting state matches the state <see cref="FormationPlanner"/>
    /// actually leaves the stream in, so the assertion pins the exact draw
    /// count rather than only whether the state moved.
    /// </summary>
    private static void AssertDrawCount(Scenario scenario, int expectedDrawCount)
    {
        var actual = new SplitMix64(scenario.Seed);
        // fieldedChiefCount: 0 -- every caller of this helper uses the
        // default MovementPresetId (never ContingentShapeV12), so the
        // parameter is ignored; see FormationPlanner.ResolveContingentSizes.
        FormationPlanner.PlanFactionDeployment(scenario, fieldedChiefCount: 0, ref actual);

        var expected = new SplitMix64(scenario.Seed);
        for (var index = 0; index < expectedDrawCount; index++)
        {
            expected.NextUInt64();
        }

        Assert.Equal(expected.State, actual.State);
    }

    private static List<int> GroupSizesByVerticalGap(
        BattleSimulation simulation,
        int factionId)
    {
        // 32 world units, regenerated for task C5
        // (the collision report and window shell plan)
        // alongside the summary above: it clears the widest measured
        // within-group row gap (~22 world units) and sits below the
        // narrowest measured between-group gap (~44 world units) at the
        // enlarged 4.25-world-unit collision radius. The old
        // 48-unit threshold predates that radius and no longer discriminates
        // reliably.
        const int separationRaw = 32 * FixedPoint.Scale;
        var verticalPositions = simulation.Agents
            .Where(agent => agent.FactionId == factionId)
            .Select(agent => agent.YRaw)
            .OrderBy(value => value)
            .ToArray();
        var groupSizes = new List<int> { 1 };

        for (var index = 1; index < verticalPositions.Length; index++)
        {
            if (verticalPositions[index] - verticalPositions[index - 1] >
                separationRaw)
            {
                groupSizes.Add(0);
            }

            groupSizes[^1]++;
        }

        return groupSizes;
    }

    private static List<(int XRaw, int YRaw)> Positions(BattleSimulation simulation) =>
        [.. simulation.Agents.Select(agent => (agent.XRaw, agent.YRaw))];

    private static void AssertInsideBounds(
        BattleSimulation simulation,
        Scenario scenario)
    {
        var mapWidthRaw = scenario.MapWidth * FixedPoint.Scale;
        var mapHeightRaw = scenario.MapHeight * FixedPoint.Scale;

        Assert.All(
            simulation.Agents,
            agent =>
            {
                Assert.InRange(
                    agent.XRaw,
                    scenario.BodyRadiusRaw,
                    mapWidthRaw - scenario.BodyRadiusRaw);
                Assert.InRange(
                    agent.YRaw,
                    scenario.BodyRadiusRaw,
                    mapHeightRaw - scenario.BodyRadiusRaw);
            });
    }

    /// <summary>
    /// Asserts the planner's own guarantee, which is stricter than the repair
    /// pass's post-condition: bodies are at least one raw unit clear of
    /// tangency. A merely tangent placement would satisfy the no-overlap rule
    /// while sending every body through the repair scan.
    /// </summary>
    private static void AssertClearOfContact(
        BattleSimulation simulation,
        int bodyRadiusRaw)
    {
        var clearanceRaw = (2L * bodyRadiusRaw) + 1;
        var clearanceSquared = clearanceRaw * clearanceRaw;
        var agents = simulation.Agents;

        for (var first = 0; first < agents.Count; first++)
        {
            for (var second = first + 1; second < agents.Count; second++)
            {
                var deltaX = (long)agents[second].XRaw - agents[first].XRaw;
                var deltaY = (long)agents[second].YRaw - agents[first].YRaw;
                var squared = (deltaX * deltaX) + (deltaY * deltaY);

                if (squared < clearanceSquared)
                {
                    Assert.Fail(
                        $"Entities {agents[first].EntityId} and " +
                        $"{agents[second].EntityId} are not clear of contact at " +
                        $"spawn: squared distance {squared} is below the " +
                        $"clearance bound {clearanceSquared}.");
                }
            }
        }
    }
}
