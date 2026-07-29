using System.Collections.Immutable;
using Hukbo.Core.Combat;
using Hukbo.Core.Determinism;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// The V6-only equipment-aware deployment assignment of the weapon-relative
/// movement design, section 12: warriors sorted by resolved ally clearance
/// descending meet the existing formation slots sorted by isolation
/// descending, inside each contingent, with the exact design 12.2 tie-break
/// chains. The integration tests prove the <c>BattleSimulation.Create</c>
/// seam applies that assignment to the canonical, unmirrored deployment
/// while consuming zero additional SplitMix64 draws, that explicitly
/// symmetric rosters still mirror across the vertical centre line, that
/// contingent membership never changes, and that V4 and V5 spawn exactly as
/// they always have.
/// </summary>
public sealed class EquipmentFormationAssignmentTests
{
    private static readonly CombatLoadout Kampilan =
        new(WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None);

    private static readonly CombatLoadout Wasay =
        new(WeaponId.Wasay, ArmorId.LightOrganic, ShieldId.None);

    private static readonly CombatLoadout Kalis =
        new(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.None);

    private static readonly CombatLoadout Itak =
        new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.None);

    private static readonly CombatLoadout KalisShield =
        new(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.TallHardwood);

    private static readonly CombatLoadout ItakShield =
        new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.TallHardwood);

    private static MovementRuleset V6 =>
        MovementPresetRegistry.Get(MovementPresetId.EquipmentRelativeFootworkV6);

    // ----- Design 12.2 rule 1: the identity cases -----

    [Fact]
    public void ASingletonContingentKeepsItsOriginalSlotExactly()
    {
        var deployment = new[]
        {
            (XRaw: 4_000, YRaw: 100, ContingentId: 0),
            (XRaw: 1_000, YRaw: 900, ContingentId: 1),
            (XRaw: 9_000, YRaw: 500, ContingentId: 2),
        };
        var loadouts = new[] { Itak, Wasay, Kampilan };

        var assigned = EquipmentDeploymentAssignment.AssignForFaction(
            deployment, loadouts, V6);

        Assert.Equal(deployment, assigned);
    }

    [Fact]
    public void AUniformClearanceContingentKeepsItsOriginalMappingExactly()
    {
        // Irregular geometry that a sort would visibly reorder — the most
        // isolated slot is last and the array is not coordinate-ordered —
        // held by one contingent whose warriors all resolve the same
        // clearance because rank has no movement meaning.
        var deployment = new[]
        {
            (XRaw: 3_000, YRaw: 0, ContingentId: 0),
            (XRaw: 1_000, YRaw: 0, ContingentId: 0),
            (XRaw: 2_000, YRaw: 0, ContingentId: 0),
            (XRaw: 40_000, YRaw: 0, ContingentId: 0),
        };
        var loadouts = new[]
        {
            Itak,
            Itak with { Rank = RankId.Datu },
            Itak with { Rank = RankId.Maharlika },
            Itak,
        };

        var assigned = EquipmentDeploymentAssignment.AssignForFaction(
            deployment, loadouts, V6);

        Assert.Equal(deployment, assigned);
    }

    // ----- Design 12.2 steps 2 through 5: the pairing -----

    [Fact]
    public void TheHighestClearanceWarriorTakesTheMostIsolatedSlot()
    {
        // One contingent on a line: slot 2 is by far the most isolated, and
        // slots 0 and 1 tie on isolation, so the canonical XRaw ascending
        // tie-break ranks slot 0 ahead of slot 1. Wasay (17,500) outranks
        // Kampilan (15,000) which outranks Itak (11,500).
        var deployment = new[]
        {
            (XRaw: 0, YRaw: 0, ContingentId: 0),
            (XRaw: 1_000, YRaw: 0, ContingentId: 0),
            (XRaw: 5_000, YRaw: 0, ContingentId: 0),
        };
        var loadouts = new[] { Itak, Wasay, Kampilan };

        var assigned = EquipmentDeploymentAssignment.AssignForFaction(
            deployment, loadouts, V6);

        Assert.Equal((5_000, 0, 0), assigned[1]);
        Assert.Equal((0, 0, 0), assigned[2]);
        Assert.Equal((1_000, 0, 0), assigned[0]);
    }

    [Fact]
    public void SlotIsolationTiesBreakOnCanonicalXRawThenYRawAscending()
    {
        // Four slots on the corners of a square all tie on isolation — each
        // corner's nearest neighbour is one side away — so the ranking is
        // decided entirely by canonical XRaw then YRaw ascending, not by
        // storage order, which is deliberately scrambled here. Clearances
        // descend Wasay (17,500), Kampilan (15,000), Kalis + shield
        // (14,000), Itak + shield (13,500).
        var deployment = new[]
        {
            (XRaw: 1_000, YRaw: 1_000, ContingentId: 0),
            (XRaw: 0, YRaw: 1_000, ContingentId: 0),
            (XRaw: 1_000, YRaw: 0, ContingentId: 0),
            (XRaw: 0, YRaw: 0, ContingentId: 0),
        };
        var loadouts = new[] { Kampilan, Wasay, ItakShield, KalisShield };

        var assigned = EquipmentDeploymentAssignment.AssignForFaction(
            deployment, loadouts, V6);

        Assert.Equal((0, 0, 0), assigned[1]);
        Assert.Equal((0, 1_000, 0), assigned[0]);
        Assert.Equal((1_000, 0, 0), assigned[3]);
        Assert.Equal((1_000, 1_000, 0), assigned[2]);
    }

    [Fact]
    public void CoincidentSlotsStillProduceAPermutationOfTheContingentSlots()
    {
        // Two slots at identical coordinates tie on every slot key except
        // the original slot index, the total-order guard. Their outcomes are
        // indistinguishable by construction, so this asserts the result is a
        // permutation: the most isolated distinct slot still goes to the
        // highest clearance, and the coincident pair is preserved.
        var deployment = new[]
        {
            (XRaw: 500, YRaw: 500, ContingentId: 0),
            (XRaw: 500, YRaw: 500, ContingentId: 0),
            (XRaw: 3_000, YRaw: 0, ContingentId: 0),
        };
        var loadouts = new[] { Kalis, Wasay, Kampilan };

        var assigned = EquipmentDeploymentAssignment.AssignForFaction(
            deployment, loadouts, V6);

        Assert.Equal((3_000, 0, 0), assigned[1]);
        Assert.Equal((500, 500, 0), assigned[0]);
        Assert.Equal((500, 500, 0), assigned[2]);
    }

    [Fact]
    public void EqualClearanceWarriorsTieBreakOnCanonicalLoadoutIndexThenFactionLocalIndex()
    {
        // The shipped V6 rows carry six distinct clearances, so the warrior
        // tie-breaks need a V6-shaped variant ruleset whose Wasay row is
        // pinned to Kampilan's 15,000. Slots rank 3, 2, 0, 1 by isolation
        // and the XRaw tie-break; warriors rank Kampilan at local 1 first
        // (canonical loadout index 0 beats Wasay's 1 on the 15,000 tie),
        // then Wasay at local 0 before Wasay at local 2 (faction-local index
        // ascending), then Itak (11,500).
        var movement = BuildV6VariantWithWasayClearance(15_000);
        var deployment = new[]
        {
            (XRaw: 0, YRaw: 0, ContingentId: 0),
            (XRaw: 1_000, YRaw: 0, ContingentId: 0),
            (XRaw: 5_000, YRaw: 0, ContingentId: 0),
            (XRaw: 20_000, YRaw: 0, ContingentId: 0),
        };
        var loadouts = new[] { Wasay, Kampilan, Wasay, Itak };

        var assigned = EquipmentDeploymentAssignment.AssignForFaction(
            deployment, loadouts, movement);

        Assert.Equal((20_000, 0, 0), assigned[1]);
        Assert.Equal((5_000, 0, 0), assigned[0]);
        Assert.Equal((0, 0, 0), assigned[2]);
        Assert.Equal((1_000, 0, 0), assigned[3]);
    }

    [Fact]
    public void ReassignmentStaysInsideEachContingent()
    {
        // Two contingents interleaved the way the round-robin deal produces
        // them. The Wasay in contingent 0 may take contingent 0's isolated
        // slot but never contingent 1's even more isolated one.
        var deployment = new[]
        {
            (XRaw: 0, YRaw: 0, ContingentId: 0),
            (XRaw: 0, YRaw: 10_000, ContingentId: 1),
            (XRaw: 1_000, YRaw: 0, ContingentId: 0),
            (XRaw: 1_000, YRaw: 10_000, ContingentId: 1),
            (XRaw: 5_000, YRaw: 0, ContingentId: 0),
            (XRaw: 90_000, YRaw: 10_000, ContingentId: 1),
        };
        var loadouts = new[] { Wasay, Itak, Itak, Kampilan, Itak, Kalis };

        var assigned = EquipmentDeploymentAssignment.AssignForFaction(
            deployment, loadouts, V6);

        for (var index = 0; index < deployment.Length; index++)
        {
            Assert.Equal(
                deployment[index].ContingentId,
                assigned[index].ContingentId);
        }

        Assert.Equal((5_000, 0, 0), assigned[0]);
        Assert.Equal((90_000, 10_000, 1), assigned[3]);
    }

    [Fact]
    public void TheCanonicalDeploymentArrayIsNeverMutated()
    {
        var deployment = new[]
        {
            (XRaw: 0, YRaw: 0, ContingentId: 0),
            (XRaw: 1_000, YRaw: 0, ContingentId: 0),
            (XRaw: 5_000, YRaw: 0, ContingentId: 0),
        };
        var snapshot = (
            (int XRaw, int YRaw, int ContingentId)[])deployment.Clone();
        var loadouts = new[] { Itak, Wasay, Kampilan };

        var assigned = EquipmentDeploymentAssignment.AssignForFaction(
            deployment, loadouts, V6);

        Assert.NotSame(deployment, assigned);
        Assert.Equal(snapshot, deployment);
    }

    [Fact]
    public void ThrowsForAPresetWithoutEquipmentRelativeFootwork()
    {
        var movement = MovementPresetRegistry.Get(
            MovementPresetId.PersistentContingentsV5);
        var deployment = new[] { (XRaw: 0, YRaw: 0, ContingentId: 0) };

        Assert.Throws<ArgumentException>(
            () => EquipmentDeploymentAssignment.AssignForFaction(
                deployment, new[] { Itak }, movement));
    }

    [Fact]
    public void ThrowsWhenTheLoadoutCountDisagreesWithTheSlotCount()
    {
        var deployment = new[]
        {
            (XRaw: 0, YRaw: 0, ContingentId: 0),
            (XRaw: 1_000, YRaw: 0, ContingentId: 0),
        };

        Assert.Throws<ArgumentException>(
            () => EquipmentDeploymentAssignment.AssignForFaction(
                deployment, new[] { Itak }, V6));
    }

    // ----- The Create seam (design 12.1) -----

    /// <summary>
    /// The full seam equivalence: a V6 battle spawns every warrior of both
    /// factions exactly where the assignment of the independently replanned
    /// canonical deployment says, faction 1 X-reflected. Replanning with a
    /// fresh stream from the same seed reproducing the slots byte-for-byte
    /// is also the draw-sequence proof — any draw taken before or between
    /// the planner's own draws would shift the jitter and break equality.
    /// </summary>
    [Fact]
    public void V6SpawnsApplyTheAssignmentToTheCanonicalDeploymentForBothFactions()
    {
        var scenario = Scenario.CreateDefault(seed: 11, totalAgents: 80) with
        {
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV2,
            MovementPreset = MovementPresetId.EquipmentRelativeFootworkV6,
            RosterCounts = [7, 7, 7, 7, 6, 6],
        };
        var deployment = PlanCanonicalDeployment(scenario);
        var combat = CombatPresetRegistry.Get(scenario.CombatPreset);

        var simulation = BattleSimulation.Create(scenario);

        foreach (var factionId in new[] { 0, 1 })
        {
            var expected = EquipmentDeploymentAssignment.AssignForFaction(
                deployment,
                FactionLoadouts(scenario, combat, factionId),
                V6);

            // Non-vacuity guard: a mixed roster must actually move someone,
            // or this test would also pass with the seam deleted.
            Assert.NotEqual(deployment, expected);
            AssertFactionSpawnedAt(simulation, scenario, factionId, expected);
        }
    }

    /// <summary>
    /// With the default empty <c>RosterCounts</c>, loadouts resolve
    /// round-robin from the global entity id, so the two factions carry
    /// different faction-local loadout sequences and each must receive its
    /// own assignment against the same canonical slot ranking.
    /// </summary>
    [Fact]
    public void V6DefaultRoundRobinLoadoutsGetEachFactionsOwnAssignment()
    {
        var scenario = Scenario.CreateDefault(seed: 5, totalAgents: 80) with
        {
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV2,
            MovementPreset = MovementPresetId.EquipmentRelativeFootworkV6,
        };
        var deployment = PlanCanonicalDeployment(scenario);
        var combat = CombatPresetRegistry.Get(scenario.CombatPreset);

        var simulation = BattleSimulation.Create(scenario);

        foreach (var factionId in new[] { 0, 1 })
        {
            var expected = EquipmentDeploymentAssignment.AssignForFaction(
                deployment,
                FactionLoadouts(scenario, combat, factionId),
                V6);
            AssertFactionSpawnedAt(simulation, scenario, factionId, expected);
        }
    }

    /// <summary>
    /// The twin-simulation draw-sequence proof: under the same seed, a V6
    /// battle opens on exactly the multiset of positions its V5 twin opens
    /// on, per faction, and every entity keeps its V5 contingent and
    /// loadout. The slots, the round-robin membership deal, and the
    /// SplitMix64 draw count are all untouched — V6 only permutes who
    /// stands where inside each contingent.
    /// </summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(11UL)]
    public void V6SpawnsPermuteTheV5SlotsWithZeroAdditionalDraws(ulong seed)
    {
        var v5 = Scenario.CreateDefault(seed, totalAgents: 80) with
        {
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV2,
            MovementPreset = MovementPresetId.PersistentContingentsV5,
        };
        var v6 = v5 with
        {
            MovementPreset = MovementPresetId.EquipmentRelativeFootworkV6,
        };

        var v5Agents = BattleSimulation.Create(v5).Agents;
        var v6Agents = BattleSimulation.Create(v6).Agents;

        for (var index = 0; index < v5Agents.Count; index++)
        {
            Assert.Equal(v5Agents[index].EntityId, v6Agents[index].EntityId);
            Assert.Equal(
                v5Agents[index].ContingentId, v6Agents[index].ContingentId);
            Assert.Equal(v5Agents[index].Loadout, v6Agents[index].Loadout);
        }

        foreach (var factionId in new[] { 0, 1 })
        {
            Assert.Equal(
                FactionPositionsOrdered(v5Agents, factionId),
                FactionPositionsOrdered(v6Agents, factionId));
        }
    }

    /// <summary>
    /// A single-loadout army resolves one clearance everywhere, so every
    /// contingent takes design 12.2's rule-1 identity and the V6 battle
    /// opens byte-identically to its V5 twin, per index.
    /// </summary>
    [Fact]
    public void AUniformLoadoutV6ArmySpawnsByteIdenticallyToItsV5Twin()
    {
        var v5 = Scenario.CreateDefault(seed: 7, totalAgents: 80) with
        {
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV2,
            MovementPreset = MovementPresetId.PersistentContingentsV5,
            RosterCounts = [0, 40, 0, 0, 0, 0],
        };
        var v6 = v5 with
        {
            MovementPreset = MovementPresetId.EquipmentRelativeFootworkV6,
        };

        var v5Agents = BattleSimulation.Create(v5).Agents;
        var v6Agents = BattleSimulation.Create(v6).Agents;

        for (var index = 0; index < v5Agents.Count; index++)
        {
            Assert.Equal(v5Agents[index].XRaw, v6Agents[index].XRaw);
            Assert.Equal(v5Agents[index].YRaw, v6Agents[index].YRaw);
            Assert.Equal(
                v5Agents[index].ContingentId, v6Agents[index].ContingentId);
        }
    }

    /// <summary>
    /// Explicitly symmetric rosters mirror (design 12.2 step 5): equal
    /// faction-local loadout sequences produce the same permutation of the
    /// same canonical slots, so faction 1 stays the exact X-reflection of
    /// faction 0 after the reassignment.
    /// </summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(1729UL)]
    public void SymmetricRosterCountsMirrorExactlyUnderV6(ulong seed)
    {
        var scenario = Scenario.CreateDefault(seed, totalAgents: 80) with
        {
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV2,
            MovementPreset = MovementPresetId.EquipmentRelativeFootworkV6,
            RosterCounts = [7, 7, 7, 7, 6, 6],
        };
        var mapWidthRaw = checked(scenario.MapWidth * FixedPoint.Scale);

        var simulation = BattleSimulation.Create(scenario);

        for (var index = 0; index < scenario.AgentsPerFaction; index++)
        {
            var left = simulation.Agents[index];
            var right = simulation.Agents[scenario.AgentsPerFaction + index];

            Assert.Equal(0, left.FactionId);
            Assert.Equal(1, right.FactionId);
            Assert.Equal(checked(mapWidthRaw - left.XRaw), right.XRaw);
            Assert.Equal(left.YRaw, right.YRaw);
            Assert.Equal(left.ContingentId, right.ContingentId);
            Assert.Equal(left.Loadout, right.Loadout);
        }
    }

    /// <summary>
    /// The explicit spawn-position control under V4: a legacy preset spawns
    /// every warrior exactly on the planner's slot for its own faction-local
    /// index — no reassignment runs, even with the same mixed roster the V6
    /// tests use.
    /// </summary>
    [Fact]
    public void V4SpawnPositionsMatchThePlannedDeploymentIdentically()
    {
        var scenario = Scenario.CreateDefault(seed: 11, totalAgents: 80) with
        {
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV2,
            MovementPreset = MovementPresetId.PersistentContingentsV4,
            RosterCounts = [7, 7, 7, 7, 6, 6],
        };
        var deployment = PlanCanonicalDeployment(scenario);

        var simulation = BattleSimulation.Create(scenario);

        AssertFactionSpawnedAt(simulation, scenario, factionId: 0, deployment);
        AssertFactionSpawnedAt(simulation, scenario, factionId: 1, deployment);
    }

    // ----- Helpers -----

    /// <summary>
    /// Replans the canonical, unmirrored deployment from a fresh stream on
    /// the scenario's own seed — byte-for-byte what <c>Create</c> plans,
    /// because spawn planning is the stream's first and only consumer.
    /// </summary>
    private static (int XRaw, int YRaw, int ContingentId)[] PlanCanonicalDeployment(
        Scenario scenario)
    {
        var random = new SplitMix64(scenario.Seed);
        return FormationPlanner.PlanFactionDeployment(scenario, ref random);
    }

    /// <summary>
    /// One faction's loadouts by faction-local index, resolved exactly the
    /// way <c>BattleSimulation.Create</c> resolves them: by global entity id
    /// when <c>RosterCounts</c> is empty, by expanded roster index
    /// otherwise.
    /// </summary>
    private static CombatLoadout[] FactionLoadouts(
        Scenario scenario,
        CombatRuleset combat,
        int factionId)
    {
        var expanded = scenario.RosterCounts.IsDefaultOrEmpty
            ? ImmutableArray<int>.Empty
            : RosterCountExpansion.Expand(scenario.RosterCounts);
        var loadouts = new CombatLoadout[scenario.AgentsPerFaction];

        for (var index = 0; index < loadouts.Length; index++)
        {
            var entityId = checked(
                (ulong)((factionId * scenario.AgentsPerFaction) + index) + 1);
            loadouts[index] = scenario.RosterCounts.IsDefaultOrEmpty
                ? combat.ResolveLoadout(entityId)
                : combat.Roster[expanded[index]];
        }

        return loadouts;
    }

    private static void AssertFactionSpawnedAt(
        BattleSimulation simulation,
        Scenario scenario,
        int factionId,
        (int XRaw, int YRaw, int ContingentId)[] expected)
    {
        var mapWidthRaw = checked(scenario.MapWidth * FixedPoint.Scale);

        for (var index = 0; index < expected.Length; index++)
        {
            var view = simulation.Agents[
                (factionId * scenario.AgentsPerFaction) + index];
            var expectedXRaw = factionId == 0
                ? expected[index].XRaw
                : checked(mapWidthRaw - expected[index].XRaw);

            Assert.Equal(factionId, view.FactionId);
            Assert.Equal(expectedXRaw, view.XRaw);
            Assert.Equal(expected[index].YRaw, view.YRaw);
            Assert.Equal(expected[index].ContingentId, view.ContingentId);
        }
    }

    /// <summary>
    /// One faction's opening positions and their contingent ids, in a total
    /// coordinate order, so two battles can be compared as position
    /// multisets regardless of which warrior stands on which slot.
    /// </summary>
    private static (int XRaw, int YRaw, int ContingentId)[] FactionPositionsOrdered(
        IReadOnlyList<AgentView> agents,
        int factionId) =>
        agents
            .Where(view => view.FactionId == factionId)
            .Select(view => (view.XRaw, view.YRaw, view.ContingentId))
            .OrderBy(entry => entry.XRaw)
            .ThenBy(entry => entry.YRaw)
            .ThenBy(entry => entry.ContingentId)
            .ToArray();

    /// <summary>
    /// A V6-shaped variant ruleset whose Wasay row carries an explicit ally
    /// clearance, so a warrior-sort clearance tie is reachable — the shipped
    /// rows carry six distinct values on purpose. Every other value copies
    /// the registered V6 entry. Test scaffolding only; the values assert
    /// nothing historical.
    /// </summary>
    private static MovementRuleset BuildV6VariantWithWasayClearance(
        int clearanceBasisPoints)
    {
        var rows = V6.LoadoutMovementProfiles;
        var builder = ImmutableArray.CreateBuilder<LoadoutMovementProfile>(
            rows.Length);
        for (var row = 0; row < rows.Length; row++)
        {
            builder.Add(row == 1
                ? CloneWithClearance(rows[row], clearanceBasisPoints)
                : rows[row]);
        }

        return new MovementRuleset(
            id: MovementPresetId.EquipmentRelativeFootworkV6,
            version: 1,
            cohesionRadiusMultiplier: V6.CohesionRadiusMultiplier,
            closeRadiusMultiplier: V6.CloseRadiusMultiplier,
            closeFractionNumerator: V6.CloseFractionNumerator,
            closeFractionDenominator: V6.CloseFractionDenominator,
            minimumCohesiveMembers: V6.MinimumCohesiveMembers,
            cohesionCycleTicks: V6.CohesionCycleTicks,
            cohesionDutyTicks: V6.CohesionDutyTicks,
            arrivalTaperMultiplier: V6.ArrivalTaperMultiplier,
            offsetUnit: V6.OffsetUnit,
            narrowsCohesionScanToCohesionCapableContingents:
                V6.NarrowsCohesionScanToCohesionCapableContingents,
            selectsLeaderByRank: V6.SelectsLeaderByRank,
            usesEquipmentRelativeFootwork: true,
            immediateRadiusBodyDiametersBasisPoints:
                V6.ImmediateRadiusBodyDiametersBasisPoints,
            supportRadiusBodyDiametersBasisPoints:
                V6.SupportRadiusBodyDiametersBasisPoints,
            loadoutMovementProfiles: builder.MoveToImmutable());
    }

    private static LoadoutMovementProfile CloneWithClearance(
        LoadoutMovementProfile source,
        int clearanceBasisPoints) =>
        new(
            source.Loadout,
            source.ForwardPaceBasisPoints,
            source.LateralPaceBasisPoints,
            source.BackwardPaceBasisPoints,
            source.CommittedPaceBasisPoints,
            source.PreferredDistanceBasisPoints,
            source.OpponentDistanceOffsetBasisPoints,
            source.MaximumFacingStepsPerTick,
            source.CommittedFacingStepsPerTick,
            source.AccelerationBasisPointsPerTick,
            source.DecelerationBasisPointsPerTick,
            source.CommitmentTicks,
            source.RecoveryTicks,
            clearanceBasisPoints,
            source.DisengageEnemyToAllyBasisPoints,
            source.ReengageEnemyToAllyBasisPoints,
            source.PursuitSupportBodyDiametersBasisPoints);
}
