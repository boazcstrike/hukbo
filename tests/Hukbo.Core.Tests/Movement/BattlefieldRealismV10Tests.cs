using System.Collections.Generic;
using System.Linq;
using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// Plan task 9's equivalence and mirror tests for
/// <see cref="MovementPresetId.BattlefieldRealismV10"/>. The four
/// assertions below are exactly the plan row's four: (a) a melee-only
/// roster runs byte-identically to its V8 twin apart from the deployment
/// permutation; (b) a shooter that is never threatened produces V8's exact
/// event stream; (c) a populated <c>RosterCounts</c> keeps the exact
/// per-index mirror V6 already established for its own preset; (d) the
/// default rotating roster is positionally equivalent but not per-index
/// identical, proven through the positive grouping and depth assertions
/// design section 7.2 requires rather than through a bare inequality.
/// </summary>
public sealed class BattlefieldRealismV10Tests
{
    private static readonly CombatLoadout Kampilan =
        new(WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None);

    private static readonly CombatLoadout Bangkaw =
        new(WeaponId.Bangkaw, ArmorId.LightOrganic, ShieldId.None);

    // ----- (a): a melee-only roster matches V8 apart from the deployment -----

    /// <summary>
    /// Plan task 9(a): a V10 battle whose roster fields no ranged weapon is
    /// event-identical and hash-identical to the same battle under V8,
    /// except for the deployment permutation. <c>PrecolonialPhilippinesV2</c>'s
    /// six roster rows are all melee -- <see cref="WeaponProfile.StandoffDistanceRaw"/>
    /// is zero for every one of them -- so V10's retreat rung (design
    /// section 5.2) never fires for this roster under either preset, and
    /// the deployment permutation (design sections 4.2 to 4.6) is the only
    /// other place the two presets could ever disagree. That permutation
    /// runs only inside <c>BattleSimulation.Create(Scenario)</c>'s spawn
    /// planning, so this test bypasses it entirely through
    /// <c>CreateForTesting</c> -- the sanctioned way in this suite to hold
    /// a battle's starting positions fixed and read its subsequent
    /// behaviour in isolation, exactly as
    /// <c>RangedStandoffTests.MeleeOnlyRosterUnderV8MatchesV4TrajectoriesAndEvents</c>
    /// already does for the V4/V8 pair. As that test's own comment records,
    /// <c>Scenario.MovementPreset</c> folds directly into
    /// <c>ComputeStateHash()</c> (and <c>MovementRuleset.ContentHash</c>
    /// folds its own <c>Id</c> too), so a V8 run and a V10 run can never
    /// produce a literally equal hash even with byte-identical agent
    /// behaviour; the operative "hash-identical" proof here is the same
    /// one that comment names -- full per-agent field equality every tick
    /// plus full <c>LastEvents</c> equality.
    /// </summary>
    [Fact]
    public void MeleeOnlyRosterUnderV10MatchesV8TrajectoriesAndEventsApartFromDeployment()
    {
        var v8Scenario = CreateMeleeScenario(MovementPresetId.RangedStandoffV8);
        var v10Scenario = CreateMeleeScenario(MovementPresetId.BattlefieldRealismV10);

        var v8 = BattleSimulation.CreateForTesting(v8Scenario, CreateMeleeRoster(v8Scenario));
        var v10 = BattleSimulation.CreateForTesting(v10Scenario, CreateMeleeRoster(v10Scenario));

        var comparedTicks = 0;
        for (var tick = 0; tick < 600 && v8.Outcome == BattleOutcome.Ongoing; tick++)
        {
            v8.AdvanceOneTick();
            v10.AdvanceOneTick();
            comparedTicks++;

            Assert.Equal(v8.Tick, v10.Tick);
            Assert.Equal(v8.Outcome, v10.Outcome);
            Assert.Equal(v8.LastEvents, v10.LastEvents);

            Assert.Equal(v8.Agents.Count, v10.Agents.Count);
            for (var i = 0; i < v8.Agents.Count; i++)
            {
                var left = v8.Agents[i];
                var right = v10.Agents[i];

                Assert.Equal(left.EntityId, right.EntityId);
                Assert.Equal(left.XRaw, right.XRaw);
                Assert.Equal(left.YRaw, right.YRaw);
                Assert.Equal(left.HitPoints, right.HitPoints);
                Assert.Equal(left.Intent, right.Intent);
                Assert.Equal(left.IsAlive, right.IsAlive);
                Assert.Equal(left.TargetEntityId, right.TargetEntityId);
                Assert.Equal(left.MovementResolution, right.MovementResolution);
            }
        }

        // A trajectory comparison over zero ticks would prove nothing; this
        // is the guard against the loop above silently no-op-ing because the
        // battle resolved (or was already Ongoing == false) before the
        // first AdvanceOneTick call.
        Assert.True(comparedTicks > 50, $"Only compared {comparedTicks} ticks.");
    }

    // ----- (b): an unthreatened shooter matches V8's exact event stream -----

    /// <summary>
    /// Plan task 9(b): a V10 battle in which no shooter is ever threatened
    /// produces the same ordered event stream as V8. A Bangkaw shooter
    /// placed exactly at its own <see cref="WeaponProfile.StandoffDistanceRaw"/>
    /// from a frozen (<c>movementSpeedRawOverride: 0</c>) melee target
    /// never moves, under either preset's identical standoff hold --
    /// <c>RangedStandoffTests.RangedWarriorAtStandoffDistanceHoldsAndDoesNotMove</c>
    /// proves this for V8 -- so the distance between the two never changes
    /// either: it stays exactly <c>standoffRaw</c> for the whole run, which
    /// this test asserts up front is strictly greater than
    /// <see cref="RangedRetreatRules.ThreatRadiusRaw"/>'s half-of-standoff
    /// result. V10's retreat rung (design section 5.2) is therefore never
    /// triggered on any tick, which the loop below checks directly by
    /// asserting the shooter's <see cref="AgentIntent"/> is never
    /// <see cref="AgentIntent.BackingAway"/> -- the one intent value only
    /// that rung ever writes. The deployment permutation is bypassed the
    /// same way test (a) bypasses it, through <c>CreateForTesting</c>.
    /// </summary>
    [Fact]
    public void UnthreatenedShooterUnderV10MatchesV8TrajectoriesAndEvents()
    {
        var rules = RangedRules();
        var standoffRaw = rules
            .ResolveWeaponProfile(Bangkaw.Weapon, Bangkaw.Shield)
            .StandoffDistanceRaw;
        Assert.True(standoffRaw > 0, "Bangkaw must declare a positive standoff distance.");

        var threatRadiusRaw = RangedRetreatRules.ThreatRadiusRaw(standoffRaw);
        Assert.True(
            threatRadiusRaw < standoffRaw,
            "The fixture depends on the threat radius sitting strictly " +
            "inside the standoff distance the shooter and the target hold " +
            "at for the whole run.");

        var v8Scenario = CreateRangedScenario(MovementPresetId.RangedStandoffV8);
        var v10Scenario = CreateRangedScenario(MovementPresetId.BattlefieldRealismV10);

        var v8Shooter = CreateRangedAgent(1, factionId: 0, xRaw: 0, yRaw: 0, v8Scenario, rules, Bangkaw);
        var v8Target = CreateRangedAgent(
            2, factionId: 1, xRaw: standoffRaw, yRaw: 0, v8Scenario, rules, Kampilan,
            movementSpeedRawOverride: 0);
        var v10Shooter = CreateRangedAgent(1, factionId: 0, xRaw: 0, yRaw: 0, v10Scenario, rules, Bangkaw);
        var v10Target = CreateRangedAgent(
            2, factionId: 1, xRaw: standoffRaw, yRaw: 0, v10Scenario, rules, Kampilan,
            movementSpeedRawOverride: 0);

        var v8 = BattleSimulation.CreateForTesting(v8Scenario, rules, v8Shooter, v8Target);
        var v10 = BattleSimulation.CreateForTesting(v10Scenario, rules, v10Shooter, v10Target);

        var comparedTicks = 0;
        for (var tick = 0; tick < 60; tick++)
        {
            v8.AdvanceOneTick();
            v10.AdvanceOneTick();
            comparedTicks++;

            Assert.Equal(v8.LastEvents, v10.LastEvents);

            Assert.Equal(v8Shooter.XRaw, v10Shooter.XRaw);
            Assert.Equal(v8Shooter.YRaw, v10Shooter.YRaw);
            Assert.Equal(v8Shooter.Intent, v10Shooter.Intent);
            Assert.Equal(v8Shooter.HitPoints, v10Shooter.HitPoints);
            Assert.Equal(v8Target.HitPoints, v10Target.HitPoints);
            Assert.Equal(v8Target.IsAlive, v10Target.IsAlive);

            // The fixture's own guarantee, checked rather than assumed on
            // every tick, not just asserted once up front: the melee
            // target's actual distance from the shooter -- which can drift
            // by a small, preset-independent nudge on the opening tick, the
            // same arrival-taper floor that touches every zero-speed agent
            // under V4, V8, and V10 alike, per
            // RangedStandoffTests.RangedWarriorInsideStandoffDistanceHoldsAndDoesNotMove's
            // own comment -- stays well outside the threat radius, so the
            // retreat rung -- the one and only producer of BackingAway --
            // never wrote it.
            var dxRaw = checked(v10Target.XRaw - v10Shooter.XRaw);
            var dyRaw = checked(v10Target.YRaw - v10Shooter.YRaw);
            var distanceSquared = checked(((long)dxRaw * dxRaw) + ((long)dyRaw * dyRaw));
            Assert.False(
                RangedRetreatRules.IsThreatened(distanceSquared, threatRadiusRaw),
                "Expected the shooter to never sit inside its own melee " +
                "threat radius.");
            Assert.NotEqual(AgentIntent.BackingAway, v10Shooter.Intent);
        }

        Assert.True(comparedTicks > 20, $"Only compared {comparedTicks} ticks.");
    }

    // ----- (c): a populated roster keeps the exact per-index mirror -----

    /// <summary>
    /// Plan task 9(c): under V10 with a populated <c>RosterCounts</c>, the
    /// two factions are exact per-index mirrors. Design section 7.1's
    /// second fact: <c>RosterCountExpansion.Expand</c> is a pure function
    /// of the counts, indexed by faction-local index for both factions, so
    /// an explicit, symmetric roster gives the two factions identical
    /// faction-local loadout sequences, and <c>CohortDeploymentAssignment</c>
    /// -- itself faction-blind, per its own remarks -- then produces the
    /// exact same permutation of the exact same canonical slots for both.
    /// Mirrors <c>EquipmentFormationAssignmentTests.SymmetricRosterCountsMirrorExactlyUnderV6</c>,
    /// the same claim already proven for V6's own equipment-relative
    /// permutation, run here against V10's cohort permutation instead.
    /// </summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(1729UL)]
    public void PopulatedRosterCountsMirrorExactlyUnderV10(ulong seed)
    {
        var scenario = Scenario.CreateDefault(seed, totalAgents: 80) with
        {
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV2,
            MovementPreset = MovementPresetId.BattlefieldRealismV10,
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

    // ----- (d): the default rotating roster is positionally equivalent, -----
    // ----- weapon-grouped, and shield-forward, but not per-index identical -----

    /// <summary>
    /// Plan task 9(d), binding on design section 7.2: under V10 with the
    /// default rotating roster, the two factions are positionally
    /// equivalent but not required to mirror per index. Design section
    /// 7.1's third fact: the default resolution is
    /// <c>(entityId - 1) % roster.Count</c>, and faction 1's entity ids are
    /// offset by <c>AgentsPerFaction</c>, so the two factions'
    /// faction-local loadout sequences are rotations of each other and
    /// coincide only when the roster length divides the per-faction count
    /// -- 6 does not divide 40 here, so the two sequences are guaranteed to
    /// disagree at least once.
    /// <para>
    /// Written exactly as design section 7.2 demands: a positive assertion
    /// about grouping and depth, never a bare inequality. A bare "these
    /// two factions differ" assertion would also pass if the V10
    /// permutation silently stopped running altogether, because a plain
    /// round-robin deployment (<c>FormationPlanner.cs</c>'s own
    /// "round-robin deal" comment) is <em>already</em> positionally
    /// equivalent and not per-index identical for the same rotation
    /// reason, with no V10 code involved at all. What a plain round-robin
    /// deployment would <em>not</em> do is confine a cohort's members to at
    /// most <c>contingentCount - 1</c> contingent boundaries -- the same
    /// bound <c>CohortDeploymentAssignmentTests.SplitsNumberAtMostContingentCountMinusOneArmyWide</c>
    /// proves by construction for the pure function -- or put every
    /// shield-bearing warrior of a mixed contingent on that contingent's
    /// own forward-most slots. Both are computed here directly from the
    /// spawned battle's own agents: not re-derived by hand, and not
    /// produced by calling the method under test to manufacture its own
    /// expectation.
    /// </para>
    /// </summary>
    [Fact]
    public void DefaultRotatingRosterIsPositionallyEquivalentButNotPerIndexIdenticalUnderV10()
    {
        var scenario = Scenario.CreateDefault(seed: 11, totalAgents: 80) with
        {
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV2,
            MovementPreset = MovementPresetId.BattlefieldRealismV10,
        };
        var mapWidthRaw = checked(scenario.MapWidth * FixedPoint.Scale);
        var perFaction = scenario.AgentsPerFaction;

        var simulation = BattleSimulation.Create(scenario);

        var faction0 = new AgentView[perFaction];
        var faction1 = new AgentView[perFaction];
        for (var index = 0; index < perFaction; index++)
        {
            faction0[index] = simulation.Agents[index];
            faction1[index] = simulation.Agents[perFaction + index];
        }

        // ----- Positional equivalence: the same occupied slot set, mirrored -----
        var faction0Coordinates = SortedCoordinates(faction0, mirror: false, mapWidthRaw);
        var faction1MirroredCoordinates = SortedCoordinates(faction1, mirror: true, mapWidthRaw);
        Assert.Equal(faction0Coordinates, faction1MirroredCoordinates);

        // ----- Not per-index identical -----
        var sawAnIndexMismatch = false;
        for (var index = 0; index < perFaction; index++)
        {
            if (faction0[index].ContingentId != faction1[index].ContingentId ||
                faction0[index].Loadout != faction1[index].Loadout)
            {
                sawAnIndexMismatch = true;
                break;
            }
        }

        Assert.True(
            sawAnIndexMismatch,
            "Expected the two factions' default round-robin loadout " +
            "sequences -- rotations of each other by AgentsPerFaction " +
            "modulo the roster length -- to disagree at least once; a " +
            "fixture where they never do would not exercise this " +
            "assertion at all.");

        // ----- Positive assertion 1: cohorts are grouped (design 4.4) -----
        AssertCohortsAreGroupedWithinContingentCountMinusOneSplits(faction0);
        AssertCohortsAreGroupedWithinContingentCountMinusOneSplits(faction1);

        // ----- Positive assertion 2: shields hold the forward-most slots (design 4.5) -----
        AssertShieldBearersOccupyTheForwardMostSlotsOfEachMixedContingent(
            faction0, forwardIsHigherXRaw: true);
        AssertShieldBearersOccupyTheForwardMostSlotsOfEachMixedContingent(
            faction1, forwardIsHigherXRaw: false);
    }

    // ----- Helpers for (d) -----

    /// <summary>
    /// One faction's occupied coordinates, optionally reflected across
    /// <paramref name="mapWidthRaw"/>, sorted into a stable order so two
    /// unordered sets can be compared with a plain sequence equality.
    /// </summary>
    private static (int XRaw, int YRaw)[] SortedCoordinates(
        AgentView[] faction, bool mirror, int mapWidthRaw)
    {
        var coordinates = new (int XRaw, int YRaw)[faction.Length];
        for (var index = 0; index < faction.Length; index++)
        {
            var xRaw = mirror
                ? checked(mapWidthRaw - faction[index].XRaw)
                : faction[index].XRaw;
            coordinates[index] = (xRaw, faction[index].YRaw);
        }

        Array.Sort(coordinates, (left, right) =>
        {
            var byX = left.XRaw.CompareTo(right.XRaw);
            return byX != 0 ? byX : left.YRaw.CompareTo(right.YRaw);
        });

        return coordinates;
    }

    /// <summary>
    /// Design section 4.4's split bound, checked directly against a
    /// spawned faction's own agents: a contiguous cohort-ordered list cut
    /// into <c>contingentCount</c> runs has only
    /// <c>contingentCount - 1</c> internal cut points, and each one can
    /// fall inside at most one cohort's own contiguous block, so at most
    /// that many distinct loadouts ("cohorts", matching
    /// <see cref="CohortDeploymentAssignment"/>'s own cohort identity) may
    /// ever straddle more than one contingent.
    /// </summary>
    private static void AssertCohortsAreGroupedWithinContingentCountMinusOneSplits(
        AgentView[] faction)
    {
        var contingentsByCohort = new Dictionary<CombatLoadout, HashSet<int>>();
        var contingentIds = new HashSet<int>();
        foreach (var agent in faction)
        {
            contingentIds.Add(agent.ContingentId);
            if (!contingentsByCohort.TryGetValue(agent.Loadout, out var set))
            {
                set = new HashSet<int>();
                contingentsByCohort[agent.Loadout] = set;
            }

            set.Add(agent.ContingentId);
        }

        // Non-vacuity: the bound is only a meaningful, discriminating claim
        // when there is more than one contingent and more than one cohort
        // that could possibly be scattered across them.
        Assert.True(contingentIds.Count > 1, "Expected more than one contingent.");
        Assert.True(contingentsByCohort.Count > 1, "Expected more than one cohort.");

        var splitCohorts = 0;
        foreach (var set in contingentsByCohort.Values)
        {
            if (set.Count > 1)
            {
                splitCohorts++;
            }
        }

        Assert.True(
            splitCohorts <= contingentIds.Count - 1,
            $"{splitCohorts} cohorts straddled a contingent boundary, " +
            $"more than the {contingentIds.Count - 1} internal cut " +
            "points a grouped deployment could ever produce.");
    }

    /// <summary>
    /// Design section 4.5: inside every contingent that has both shield-
    /// and non-shield-bearing members, the shield bearers occupy that
    /// contingent's own forward-most slots. "Forward" means toward the
    /// enemy on both sides of the mirror: higher <c>XRaw</c> for faction 0
    /// (spawned unmirrored, so its own canonical <c>XRaw</c> is its actual
    /// one), lower <c>XRaw</c> for faction 1 (spawned at
    /// <c>mapWidthRaw - canonicalXRaw</c>, so the larger canonical,
    /// toward-enemy <c>XRaw</c> becomes the smaller actual one).
    /// </summary>
    private static void AssertShieldBearersOccupyTheForwardMostSlotsOfEachMixedContingent(
        AgentView[] faction, bool forwardIsHigherXRaw)
    {
        var byContingent = new Dictionary<int, List<AgentView>>();
        foreach (var agent in faction)
        {
            if (!byContingent.TryGetValue(agent.ContingentId, out var members))
            {
                members = new List<AgentView>();
                byContingent[agent.ContingentId] = members;
            }

            members.Add(agent);
        }

        var mixedContingentsChecked = 0;
        foreach (var members in byContingent.Values)
        {
            var shieldCount = members.Count(member => member.Loadout.Shield != ShieldId.None);
            if (shieldCount == 0 || shieldCount == members.Count)
            {
                continue; // Not a mixed contingent; the prefix claim is vacuous here.
            }

            mixedContingentsChecked++;

            var ordered = members
                .OrderBy(member => forwardIsHigherXRaw ? -member.XRaw : member.XRaw)
                .ThenBy(member => member.YRaw)
                .ToArray();

            for (var position = 0; position < shieldCount; position++)
            {
                Assert.True(
                    ordered[position].Loadout.Shield != ShieldId.None,
                    "Expected the forward-most slots of a mixed " +
                    "contingent to all be shield-bearing.");
            }
        }

        Assert.True(
            mixedContingentsChecked > 0,
            "Expected at least one mixed (shield and non-shield) " +
            "contingent to exercise the forward-slot assertion.");
    }

    // ----- Helpers for (a) -----

    /// <summary>
    /// A melee-only scenario on <see cref="CombatPresetId.PrecolonialPhilippinesV2"/>,
    /// matching <c>RangedStandoffTests.CreateMeleeScenario</c>'s shape but
    /// with its own preset choice.
    /// </summary>
    private static Scenario CreateMeleeScenario(MovementPresetId movementPreset) =>
        new(
            Seed: 1,
            MapWidth: 300,
            MapHeight: 200,
            AgentsPerFaction: 3,
            TickRate: 20,
            TickLimit: 4_000)
        {
            MaximumHitPoints = 60,
            DamagePerAttack = 8,
            AttackRangeRaw = 5 * FixedPoint.Scale,
            PerceptionRangeRaw = 200 * FixedPoint.Scale,
            BodyRadiusRaw = FixedPoint.Scale / 2,
            MovementSpeedRaw = FixedPoint.Scale / 2,
            AttackCooldownTicks = 5,
            LastStandThresholdAgents = 0,
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV2,
            MovementPreset = movementPreset,
        };

    /// <summary>
    /// Three Kampilan-armed warriors per faction, closing from roughly 20
    /// world units apart along three parallel lines.
    /// </summary>
    private static AgentState[] CreateMeleeRoster(Scenario scenario) =>
        new[]
        {
            CreateMeleeAgent(1, factionId: 0, xRaw: 0, yRaw: 0, scenario),
            CreateMeleeAgent(2, factionId: 0, xRaw: 0, yRaw: 1_200, scenario),
            CreateMeleeAgent(3, factionId: 0, xRaw: 0, yRaw: 2_400, scenario),
            CreateMeleeAgent(4, factionId: 1, xRaw: 20 * FixedPoint.Scale, yRaw: 0, scenario),
            CreateMeleeAgent(5, factionId: 1, xRaw: 20 * FixedPoint.Scale, yRaw: 1_200, scenario),
            CreateMeleeAgent(6, factionId: 1, xRaw: 20 * FixedPoint.Scale, yRaw: 2_400, scenario),
        };

    private static AgentState CreateMeleeAgent(
        ulong entityId, int factionId, int xRaw, int yRaw, Scenario scenario) =>
        new(
            entityId,
            factionId,
            xRaw,
            yRaw,
            scenario.MaximumHitPoints,
            scenario.MovementSpeedRaw,
            scenario.PerceptionRangeRaw,
            scenario.AttackRangeRaw,
            scenario.DamagePerAttack,
            scenario.AttackCooldownTicks,
            Kampilan);

    // ----- Helpers for (b) -----

    private static CombatRuleset RangedRules() =>
        CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV5);

    /// <summary>
    /// A scenario on the V5 ranged preset with a caller-chosen movement
    /// preset, matching <c>RangedStandoffTests.CreateRangedScenario</c>'s
    /// shape.
    /// </summary>
    private static Scenario CreateRangedScenario(MovementPresetId movementPreset) =>
        new(
            Seed: 1,
            MapWidth: 500,
            MapHeight: 500,
            AgentsPerFaction: 2,
            TickRate: 20,
            TickLimit: 500)
        {
            MaximumHitPoints = 1_000,
            DamagePerAttack = 10,
            AttackRangeRaw = 12 * FixedPoint.Scale,
            PerceptionRangeRaw = 1_000 * FixedPoint.Scale,
            BodyRadiusRaw = FixedPoint.Scale / 2,
            MovementSpeedRaw = FixedPoint.Scale / 2,
            AttackCooldownTicks = 1,
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV5,
            MovementPreset = movementPreset,
            MaximumProjectilesInFlight = 4,
        };

    private static AgentState CreateRangedAgent(
        ulong entityId,
        int factionId,
        int xRaw,
        int yRaw,
        Scenario scenario,
        CombatRuleset rules,
        CombatLoadout loadout,
        int? movementSpeedRawOverride = null)
    {
        var profile = rules.ResolveWeaponProfile(loadout.Weapon, loadout.Shield);
        return new(
            entityId,
            factionId,
            xRaw,
            yRaw,
            scenario.MaximumHitPoints,
            movementSpeedRawOverride ?? scenario.MovementSpeedRaw,
            scenario.PerceptionRangeRaw,
            profile.AttackRangeRaw,
            profile.DamagePerAttack,
            profile.AttackCooldownTicks,
            loadout);
    }
}
