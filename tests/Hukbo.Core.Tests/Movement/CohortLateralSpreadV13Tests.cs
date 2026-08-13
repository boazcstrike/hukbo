using Hukbo.Core.Combat;
using Hukbo.Core.Determinism;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;
using Hukbo.Headless;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// Coverage for <see cref="MovementPresetId.CohortLateralSpreadV13"/>,
/// plan tasks 6 through 9
/// (docs/plans/2026-08-14-cohort-lateral-spread.md). Registry facts mirror
/// <c>ContingentShapeV12Tests.cs:25-45</c>; the traversal-order, split-bound,
/// and mirror properties are new, exercising
/// <see cref="CohortDeploymentAssignment.AssignForFaction"/>'s
/// <c>spreadCohortsLaterally</c> parameter and the movement preset that is
/// the only caller to pass <see langword="true"/> for it
/// (docs/plans/2026-08-14-cohort-lateral-spread-design.md section 3).
/// </summary>
public sealed class CohortLateralSpreadV13Tests
{
    // Roster order of PrecolonialPhilippinesV2 (content-hash contract, does
    // not change): 0 Kampilan, 1 Wasay, 2 Kalis, 3 Kalis+TallHardwood,
    // 4 Itak, 5 Itak+TallHardwood.
    private static CombatRuleset Rules =>
        CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV2);

    private static readonly CombatLoadout Kampilan =
        new(WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None);

    private static readonly CombatLoadout Wasay =
        new(WeaponId.Wasay, ArmorId.LightOrganic, ShieldId.None);

    private static readonly CombatLoadout Kalis =
        new(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.None);

    // ----- Task 6: registry facts, mirroring ContingentShapeV12Tests.cs:25-45 -----

    [Fact]
    public void CohortLateralSpreadV13HasTheExpectedNumericValue()
    {
        Assert.Equal(13, (int)MovementPresetId.CohortLateralSpreadV13);
    }

    [Fact]
    public void CohortLateralSpreadV13IsRegistered()
    {
        Assert.True(
            MovementPresetRegistry.IsRegistered(
                MovementPresetId.CohortLateralSpreadV13));
    }

    [Fact]
    public void CohortLateralSpreadV13CarriesItsOwnIdentity()
    {
        var v11 = MovementPresetRegistry.Get(MovementPresetId.LastStandEngagementV11);
        var v13 = MovementPresetRegistry.Get(MovementPresetId.CohortLateralSpreadV13);

        Assert.Equal(MovementPresetId.CohortLateralSpreadV13, v13.Id);

        // The ruleset is a verbatim restatement of V11's field values, so
        // every tunable matches; only the folded Id separates the two
        // content hashes.
        Assert.Equal(v11.CohesionRadiusMultiplier, v13.CohesionRadiusMultiplier);
        Assert.Equal(v11.CloseRadiusMultiplier, v13.CloseRadiusMultiplier);
        Assert.Equal(v11.CloseFractionNumerator, v13.CloseFractionNumerator);
        Assert.Equal(v11.CloseFractionDenominator, v13.CloseFractionDenominator);
        Assert.Equal(v11.MinimumCohesiveMembers, v13.MinimumCohesiveMembers);
        Assert.Equal(v11.CohesionCycleTicks, v13.CohesionCycleTicks);
        Assert.Equal(v11.CohesionDutyTicks, v13.CohesionDutyTicks);
        Assert.Equal(v11.ArrivalTaperMultiplier, v13.ArrivalTaperMultiplier);
        Assert.Equal(v11.OffsetUnit, v13.OffsetUnit);
        Assert.Equal(
            v11.NarrowsCohesionScanToCohesionCapableContingents,
            v13.NarrowsCohesionScanToCohesionCapableContingents);
        Assert.Equal(v11.SelectsLeaderByRank, v13.SelectsLeaderByRank);
        Assert.Equal(
            v11.UsesEquipmentRelativeFootwork,
            v13.UsesEquipmentRelativeFootwork);
        Assert.Equal(v11.AppliesPressureInterrupt, v13.AppliesPressureInterrupt);
        Assert.NotEqual(v11.ContentHash, v13.ContentHash);
    }

    // ----- Task 7(a)+(b): the lateral-riffle traversal order -----

    /// <summary>
    /// Design section 3's worked example: for seven contingents the
    /// traversal is <c>0, 2, 4, 6, 1, 3, 5</c> -- even ids ascending, then
    /// odd ids ascending. Observed through the public
    /// <see cref="CohortDeploymentAssignment.AssignForFaction"/> entry
    /// point alone -- <see cref="ObserveLateralRiffleOrder"/> below reaches
    /// no private member -- by giving every one of the seven contingents a
    /// single slot and every warrior the same cohort, so the cohort-order
    /// sort's tie-break (original faction-local index, ascending) is the
    /// only thing that orders the warrior list, and the contingent a
    /// warrior lands in is read straight off the traversal order itself.
    /// </summary>
    [Fact]
    public void SevenContingentsRiffleAsEvenIdsThenOddIds()
    {
        var observed = ObserveLateralRiffleOrder(contingentCount: 7);

        Assert.Equal(new[] { 0, 2, 4, 6, 1, 3, 5 }, observed);
    }

    /// <summary>
    /// The same observation, generalised across every contingent count
    /// <see cref="Simulation.FormationPlanner"/> can ever produce
    /// (design section 6, question 3: at most eight), against the riffle
    /// rule stated independently here -- even ids ascending, then odd ids
    /// ascending -- rather than by calling the method under test twice to
    /// manufacture its own expectation.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void RiffleOrderIsEvenIdsAscendingThenOddIdsAscending(int contingentCount)
    {
        var observed = ObserveLateralRiffleOrder(contingentCount);
        var expected = HandComputedRiffleOrder(contingentCount);

        Assert.Equal(expected, observed);
    }

    // ----- Task 7(c): no two size-ranked-adjacent runs land on adjacent lanes -----

    /// <summary>
    /// Design section 3's first bullet: two cohort runs adjacent in the
    /// riffle-ordered sequence are never assigned to adjacent contingent
    /// ids, except at the single wrap point where the even pass hands over
    /// to the odd pass. Checked against every contingent count
    /// <see cref="Simulation.FormationPlanner"/> can produce (one through
    /// eight); the wrap point is only ever adjacent for the four smallest
    /// counts (two through four; count one has no consecutive pair to
    /// violate at all), and never adjacent from five contingents up, so the
    /// bound below -- at most one violation -- holds at every size actually
    /// checked, not merely on average.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void AtMostOneConsecutivePairOfRunsLandsOnAdjacentContingentIds(
        int contingentCount)
    {
        var order = ObserveLateralRiffleOrder(contingentCount);

        var adjacentLaneViolations = 0;
        for (var index = 0; index < order.Length - 1; index++)
        {
            if (Math.Abs(order[index] - order[index + 1]) == 1)
            {
                adjacentLaneViolations++;
            }
        }

        Assert.True(
            adjacentLaneViolations <= 1,
            $"Expected at most one wrap-point violation for " +
            $"{contingentCount} contingents; observed " +
            $"{adjacentLaneViolations}.");
    }

    // ----- Task 7(f): V10 and V11 still traverse ascending (regression guard) -----

    /// <summary>
    /// <see cref="CohortDeploymentAssignment.AssignForFaction"/>'s
    /// <c>spreadCohortsLaterally: false</c> path -- the one every preset
    /// except <see cref="MovementPresetId.CohortLateralSpreadV13"/> passes,
    /// including <see cref="MovementPresetId.BattlefieldRealismV10"/> and
    /// <see cref="MovementPresetId.LastStandEngagementV11"/>
    /// (<c>BattleSimulation.cs:708-713</c>) -- still produces the ascending
    /// contingent-id traversal the design's section 2 pins as today's
    /// pathology, observed through the same synthetic single-cohort setup
    /// as the riffle tests above so the two traversal modes are read off
    /// the identical construction.
    /// </summary>
    [Theory]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(8)]
    public void FalseStillProducesTheAscendingTraversal(int contingentCount)
    {
        var observed = ObserveSizeRankedTraversalOrder(contingentCount);
        var expected = Enumerable.Range(0, contingentCount).ToArray();

        Assert.Equal(expected, observed);
    }

    /// <summary>
    /// Builds a deployment of <paramref name="contingentCount"/> single-slot
    /// contingents and hands every warrior the identical cohort, then reads
    /// the contingent id each rank position lands in straight off
    /// <see cref="CohortDeploymentAssignment.AssignForFaction"/>'s public
    /// result -- element <c>k</c> of the returned array is the contingent id
    /// the riffle traversal visits at position <c>k</c>, because every
    /// contingent holds exactly one warrior and the warrior list is already
    /// in ascending faction-local-index order (identical cohort keys and
    /// sizes leave the sort's index tie-break as the only thing that
    /// orders it).
    /// </summary>
    private static int[] ObserveLateralRiffleOrder(int contingentCount) =>
        ObserveTraversalOrder(contingentCount, spreadCohortsLaterally: true);

    /// <summary>
    /// The <c>spreadCohortsLaterally: false</c> twin of
    /// <see cref="ObserveLateralRiffleOrder"/>, same construction.
    /// </summary>
    private static int[] ObserveSizeRankedTraversalOrder(int contingentCount) =>
        ObserveTraversalOrder(contingentCount, spreadCohortsLaterally: false);

    private static int[] ObserveTraversalOrder(
        int contingentCount, bool spreadCohortsLaterally)
    {
        var deployment = new (int XRaw, int YRaw, int ContingentId)[contingentCount];
        var loadouts = new CombatLoadout[contingentCount];
        for (var contingentId = 0; contingentId < contingentCount; contingentId++)
        {
            deployment[contingentId] = (contingentId * 10, 0, contingentId);
            loadouts[contingentId] = Kampilan;
        }

        var assigned = CohortDeploymentAssignment.AssignForFaction(
            deployment, loadouts, Rules, spreadCohortsLaterally);

        var order = new int[contingentCount];
        for (var rank = 0; rank < contingentCount; rank++)
        {
            order[rank] = assigned[rank].ContingentId;
        }

        return order;
    }

    /// <summary>
    /// The riffle rule restated independently of
    /// <see cref="CohortDeploymentAssignment"/>'s own implementation: even
    /// ids ascending, then odd ids ascending.
    /// </summary>
    private static int[] HandComputedRiffleOrder(int contingentCount)
    {
        var order = new List<int>(contingentCount);
        for (var id = 0; id < contingentCount; id += 2)
        {
            order.Add(id);
        }

        for (var id = 1; id < contingentCount; id += 2)
        {
            order.Add(id);
        }

        return [.. order];
    }

    // ----- Task 7(d): row-58 regression -- shield bearers span both halves -----

    /// <summary>
    /// Row 58's own reported shape, reproduced under the shipped client
    /// composition: 250 warriors a side, <c>PrecolonialPhilippinesV5</c>,
    /// <c>RosterCounts</c> hand-derived from
    /// <c>ArmyComposition.Default</c> (63 Datu, 63
    /// Maharlika, 62 Timawa, 62 Aliping Namamahay) the way
    /// <c>ArenaGame.ExpandCompositionToRosterCounts</c> derives it --
    /// replicated here rather than referenced, because this project may not
    /// depend on <c>Hukbo.Client</c>. At this shape
    /// <see cref="Simulation.FormationPlanner"/> resolves seven contingents
    /// (<c>IntegerSquareRoot(250) / 2 = 7</c>), matching design section 3's
    /// own worked example.
    /// <para>
    /// Under <see cref="MovementPresetId.CohortLateralSpreadV13"/> (riffle
    /// traversal), both shield-bearing roster rows -- Kalis+TallHardwood and
    /// Itak+TallHardwood -- land in contingents on both halves of the
    /// seven-lane span. Under
    /// <see cref="MovementPresetId.LastStandEngagementV11"/> (the
    /// size-ranked traversal every preset before V13 uses, same
    /// <c>BattleSimulation.cs:708-713</c> call site with
    /// <c>spreadCohortsLaterally: false</c>), the two shield-bearing rows
    /// sort last in cohort rank -- neither is the largest cohort -- and so
    /// collect at one edge of the span instead, exactly the row-58 defect.
    /// Both runs are asserted here so the fix is proved against the defect
    /// it replaces, not only against its own new behaviour.
    /// </para>
    /// </summary>
    [Fact]
    public void ShieldBearersSpanBothHalvesUnderV13ButNotUnderV11()
    {
        var rosterCounts = ShippedDefaultRosterCounts();

        var v13Scenario = Scenario.CreateDefault(seed: 1, totalAgents: 500) with
        {
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV5,
            MovementPreset = MovementPresetId.CohortLateralSpreadV13,
            RosterCounts = rosterCounts,
        };
        v13Scenario.Validate();
        var v13Simulation = BattleSimulation.Create(v13Scenario);
        var v13ContingentCount = v13Simulation.Agents
            .Take(v13Scenario.AgentsPerFaction)
            .Select(agent => agent.ContingentId)
            .Distinct()
            .Count();

        Assert.Equal(7, v13ContingentCount);

        var v13ShieldContingents = ShieldBearingContingentIds(
            v13Simulation, v13Scenario.AgentsPerFaction);
        var lowHalfCeiling = v13ContingentCount / 2;

        Assert.Contains(v13ShieldContingents, id => id < lowHalfCeiling);
        Assert.Contains(v13ShieldContingents, id => id >= lowHalfCeiling);

        var v11Scenario = v13Scenario with
        {
            MovementPreset = MovementPresetId.LastStandEngagementV11,
        };
        v11Scenario.Validate();
        var v11Simulation = BattleSimulation.Create(v11Scenario);
        var v11ShieldContingents = ShieldBearingContingentIds(
            v11Simulation, v11Scenario.AgentsPerFaction);

        Assert.False(
            v11ShieldContingents.Any(id => id < lowHalfCeiling)
                && v11ShieldContingents.Any(id => id >= lowHalfCeiling),
            "Expected the pre-fix (V11) traversal to still collect shield " +
            "bearers at one edge of the span; both halves were reached, " +
            "which means this reproduction no longer isolates the row-58 " +
            "defect.");
    }

    private static HashSet<int> ShieldBearingContingentIds(
        BattleSimulation simulation, int agentsPerFaction) =>
        simulation.Agents
            .Take(agentsPerFaction)
            .Where(agent => agent.Loadout.Shield != ShieldId.None)
            .Select(agent => agent.ContingentId)
            .ToHashSet();

    // ----- Task 7(e): a contingent is still dominated by one cohort -----

    /// <summary>
    /// Design section 3's second bullet, proved the same constructive way
    /// as <c>CohortDeploymentAssignmentTests.ACohortAtLeastAsLargeAsAContingentFillsThatContingentPurely</c>
    /// but under <c>spreadCohortsLaterally: true</c>: four two-slot
    /// contingents (ids 0-3, riffle order <c>0, 2, 1, 3</c>), five Kampilan
    /// warriors against three Wasay (cohort-ordered list Kampilan(0-4),
    /// Wasay(5-7)). Cutting that list into blocks of 2 in riffle order gives
    /// contingent 0 = warriors 0-1 (Kampilan, Kampilan), contingent 2 =
    /// warriors 2-3 (Kampilan, Kampilan), contingent 1 = warriors 4-5
    /// (Kampilan, Wasay), contingent 3 = warriors 6-7 (Wasay, Wasay).
    /// Contingents 0 and 2 -- non-adjacent ids, the even pass -- both come
    /// out purely Kampilan even though the Kampilan cohort is larger than
    /// either contingent alone and has to spill into contingent 1 as well:
    /// exactly the "spread over non-adjacent lanes instead of one contiguous
    /// block" claim, not merely a cohort-fits-in-one-contingent case.
    /// </summary>
    [Fact]
    public void ACohortSpanningMultipleContingentsUnderTheRiffleLandsOnNonAdjacentPureLanes()
    {
        var deployment = new[]
        {
            (XRaw: 0, YRaw: 0, ContingentId: 0),
            (XRaw: 10, YRaw: 0, ContingentId: 0),
            (XRaw: 20, YRaw: 0, ContingentId: 1),
            (XRaw: 30, YRaw: 0, ContingentId: 1),
            (XRaw: 40, YRaw: 0, ContingentId: 2),
            (XRaw: 50, YRaw: 0, ContingentId: 2),
            (XRaw: 60, YRaw: 0, ContingentId: 3),
            (XRaw: 70, YRaw: 0, ContingentId: 3),
        };
        var loadouts = new[]
        {
            Kampilan, Kampilan, Kampilan, Kampilan, Kampilan, Wasay, Wasay, Wasay,
        };

        var assigned = CohortDeploymentAssignment.AssignForFaction(
            deployment, loadouts, Rules, true);

        var loadoutByContingent = assigned
            .Select((slot, index) => (slot.ContingentId, Loadout: loadouts[index]))
            .GroupBy(pair => pair.ContingentId)
            .ToDictionary(g => g.Key, g => g.Select(p => p.Loadout).ToList());

        Assert.All(
            loadoutByContingent[0],
            loadout => Assert.Equal(WeaponId.Kampilan, loadout.Weapon));
        Assert.All(
            loadoutByContingent[2],
            loadout => Assert.Equal(WeaponId.Kampilan, loadout.Weapon));
        Assert.Contains(
            loadoutByContingent[1], loadout => loadout.Weapon == WeaponId.Kampilan);
        Assert.Contains(
            loadoutByContingent[1], loadout => loadout.Weapon == WeaponId.Wasay);
    }

    /// <summary>
    /// The split-bound half of design section 3's third bullet, proved the
    /// same constructive way as
    /// <c>CohortDeploymentAssignmentTests.SplitsNumberAtMostContingentCountMinusOneArmyWide</c>
    /// but under <c>spreadCohortsLaterally: true</c>: three contingents of
    /// sizes 3, 3, 4 (ids 0, 1, 2; riffle order <c>0, 2, 1</c>), cohorts
    /// Kampilan (5), Wasay (3), Kalis (2). Cutting the cohort-ordered list
    /// 0-9 into blocks of 3, 4, 3 for contingents 0, 2, 1 gives contingent 0
    /// = warriors 0-2 (Kampilan), contingent 2 = warriors 3-6 (Kampilan,
    /// Kampilan, Wasay, Wasay), contingent 1 = warriors 7-9 (Wasay, Kalis,
    /// Kalis). Kampilan spans {0, 2}, Wasay spans {2, 1} -- two splits,
    /// still <c>contingentCount - 1</c> for three contingents, the same
    /// bound the size-ranked traversal holds, just at different cut points.
    /// </summary>
    [Fact]
    public void SplitsNumberAtMostContingentCountMinusOneArmyWideUnderTheRiffle()
    {
        const int ContingentCount = 3;
        const int ExpectedSplitsByHand = 2;

        var deployment = new[]
        {
            (XRaw: 0, YRaw: 0, ContingentId: 0),
            (XRaw: 10, YRaw: 0, ContingentId: 0),
            (XRaw: 20, YRaw: 0, ContingentId: 0),
            (XRaw: 30, YRaw: 0, ContingentId: 1),
            (XRaw: 40, YRaw: 0, ContingentId: 1),
            (XRaw: 50, YRaw: 0, ContingentId: 1),
            (XRaw: 60, YRaw: 0, ContingentId: 2),
            (XRaw: 70, YRaw: 0, ContingentId: 2),
            (XRaw: 80, YRaw: 0, ContingentId: 2),
            (XRaw: 90, YRaw: 0, ContingentId: 2),
        };
        var loadouts = new[]
        {
            Kampilan, Kampilan, Kampilan, Kampilan, Kampilan,
            Wasay, Wasay, Wasay,
            Kalis, Kalis,
        };

        var assigned = CohortDeploymentAssignment.AssignForFaction(
            deployment, loadouts, Rules, true);

        var contingentIdsByWeapon = new Dictionary<WeaponId, HashSet<int>>();
        for (var index = 0; index < loadouts.Length; index++)
        {
            var weapon = loadouts[index].Weapon;
            if (!contingentIdsByWeapon.TryGetValue(weapon, out var set))
            {
                set = new HashSet<int>();
                contingentIdsByWeapon[weapon] = set;
            }

            set.Add(assigned[index].ContingentId);
        }

        var actualSplits = contingentIdsByWeapon.Values.Count(set => set.Count > 1);

        Assert.Equal(ExpectedSplitsByHand, actualSplits);
        Assert.True(actualSplits <= ContingentCount - 1);
    }

    /// <summary>
    /// Finding, not an assertion: at the shipped 250-a-side
    /// <c>PrecolonialPhilippinesV5</c> shape (see
    /// <see cref="ShippedDefaultRosterCounts"/>), one of the seven resolved
    /// contingents (id 3, in the seed-1 run) splits three ways -- Bangkaw
    /// 16, Kalis 11, Itak+TallHardwood 9, of 36 members -- so its largest
    /// cohort holds a plurality (16 of 36, about 44%) rather than a strict
    /// majority. Design section 3's "a contingent is still dominated by one
    /// cohort" does not state a numeric threshold, and the two pure-function
    /// tests above prove the bound the algorithm actually guarantees by
    /// construction -- purity when a cohort run is large enough, and at
    /// most <c>contingentCount - 1</c> splits army-wide. A strict
    /// <c>&gt;50%</c> per-contingent majority is not one of those
    /// guarantees at every roster shape, so this test does not assert it;
    /// the plurality figure above was read from a real run of the shipped
    /// build (<c>BattleSimulation.Create</c> with this roster and seed) and
    /// is reported here rather than pinned as a passing assertion, so a
    /// reviewer who wants a numeric "dominated" bar for the shipped shape
    /// can see what the code actually produces.
    /// </summary>
    [Fact]
    public void ShippedShapeSplitCohortCountStaysWithinTheArmyWideBound()
    {
        var rosterCounts = ShippedDefaultRosterCounts();
        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: 500) with
        {
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV5,
            MovementPreset = MovementPresetId.CohortLateralSpreadV13,
            RosterCounts = rosterCounts,
        };
        scenario.Validate();

        var simulation = BattleSimulation.Create(scenario);
        var faction0 = simulation.Agents.Take(scenario.AgentsPerFaction).ToList();

        var contingentCount = faction0
            .Select(agent => agent.ContingentId)
            .Distinct()
            .Count();
        var splitCohorts = faction0
            .GroupBy(agent => agent.Loadout)
            .Count(group => group.Select(agent => agent.ContingentId).Distinct().Count() > 1);

        Assert.True(
            splitCohorts <= contingentCount - 1,
            $"Expected at most {contingentCount - 1} split cohorts across " +
            $"{contingentCount} contingents; observed {splitCohorts}.");
    }

    /// <summary>
    /// Hand-derived largest-remainder apportionment of
    /// <c>ArmyComposition.Default</c>'s four rank sliders
    /// (63 Datu, 63 Maharlika, 62 Timawa, 62 Aliping Namamahay) across
    /// <c>PrecolonialPhilippinesV5</c>'s nine roster rows, replicating
    /// <c>ArenaGame.ExpandCompositionToRosterCounts</c> and its
    /// <c>CalibratedRosterEntryWeights</c> table without referencing
    /// <c>Hukbo.Client</c>: Datu (row 0, Kampilan) and Maharlika (row 1,
    /// Wasay) each field exactly one roster row, so their sliders pass
    /// through unchanged; Timawa (rows 2, 4, 5, 6, 7 -- Kalis, Bangkaw,
    /// Busog, Arquebus, Kalis+TallHardwood, calibrated weights 10, 11, 8, 6,
    /// 9) and Aliping Namamahay (rows 3, 8 -- Itak, Itak+TallHardwood,
    /// weights 9, 9) apportion by largest remainder. Cross-checked against
    /// the method under test by
    /// <c>ArmyCompositionRosterExpansionTests</c> in the Client test suite
    /// (not re-verified here, since this project cannot reference
    /// <c>Hukbo.Client</c>); the sum below is 250, matching
    /// <see cref="Scenario.AgentsPerFaction"/> for a 500-agent scenario.
    /// </summary>
    private static System.Collections.Immutable.ImmutableArray<int>
        ShippedDefaultRosterCounts() =>
        [63, 63, 14, 31, 16, 11, 8, 13, 31];
}
