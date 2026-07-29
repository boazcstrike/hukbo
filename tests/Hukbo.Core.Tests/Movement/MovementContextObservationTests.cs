using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// The bounded local loadout context of the weapon-relative movement design,
/// section 7: the <see cref="LoadoutCompositionCounts"/> buckets and role
/// flags, the pure span seam <see cref="MovementContextQuery.Derive"/> with
/// its explicitly permuted storage orders, the inclusive exact-radius
/// membership rule, the lower-<c>EntityId</c> tie-breaks, the production
/// accumulation hook inside <c>SelectTargetsAndIntents</c> checked field for
/// field against <see cref="NaiveMovementContextQuery"/>, the global
/// surviving totals of section 7.5, and the byte-identical legacy controls
/// of section 11.3.
/// </summary>
public sealed class MovementContextObservationTests
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

    // The test-fixture body radius is FixedPoint.Scale / 2, so the body
    // diameter is exactly FixedPoint.Scale and the registered basis points
    // resolve to these two raw radii: 2.5 and 6 body diameters.
    private const int TestBodyRadiusRaw = FixedPoint.Scale / 2;
    private const long TestImmediateRadiusRaw = (long)FixedPoint.Scale * 25 / 10;
    private const long TestSupportRadiusRaw = 6L * FixedPoint.Scale;

    // ----- LoadoutCompositionCounts: buckets and roles (7.1, 7.5) -----

    [Fact]
    public void AddIncrementsExactlyTheCanonicalBucketOfEachLoadout()
    {
        var counts = default(LoadoutCompositionCounts)
            .Add(Kampilan)
            .Add(Wasay)
            .Add(Kalis)
            .Add(Itak)
            .Add(KalisShield)
            .Add(ItakShield)
            .Add(ItakShield);

        Assert.Equal(
            new LoadoutCompositionCounts(1, 1, 1, 1, 1, 2),
            counts);
        Assert.Equal(7, counts.Total);
    }

    [Fact]
    public void AddIsRankIndependent()
    {
        var datu = Kampilan with { Rank = RankId.Datu };

        Assert.Equal(
            default(LoadoutCompositionCounts).Add(Kampilan),
            default(LoadoutCompositionCounts).Add(datu));
    }

    [Fact]
    public void AddThrowsForANonCanonicalEquipmentTriple()
    {
        var shieldedKampilan = new CombatLoadout(
            WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.TallHardwood);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => default(LoadoutCompositionCounts).Add(shieldedKampilan));
    }

    [Fact]
    public void RoleCoverageCountsTheDistinctPresentRoles()
    {
        Assert.Equal(0, default(LoadoutCompositionCounts).RoleCoverage);

        var longClearanceOnly = new LoadoutCompositionCounts(0, 2, 0, 0, 0, 0);
        Assert.True(longClearanceOnly.HasLongClearanceRole);
        Assert.False(longClearanceOnly.HasMobileBladeRole);
        Assert.False(longClearanceOnly.HasShieldSupportRole);
        Assert.Equal(1, longClearanceOnly.RoleCoverage);

        var mobileAndShield = new LoadoutCompositionCounts(0, 0, 1, 0, 0, 3);
        Assert.False(mobileAndShield.HasLongClearanceRole);
        Assert.True(mobileAndShield.HasMobileBladeRole);
        Assert.True(mobileAndShield.HasShieldSupportRole);
        Assert.Equal(2, mobileAndShield.RoleCoverage);

        var allThree = new LoadoutCompositionCounts(1, 0, 0, 1, 1, 0);
        Assert.Equal(3, allThree.RoleCoverage);
    }

    // ----- The pure span seam (7.4) -----

    [Fact]
    public void AZeroNeighbourActorSeesOnlyItself()
    {
        var actor = CreateState(1, factionId: 0, xRaw: 0, yRaw: 0, Kalis);

        var context = Derive([actor], actor, selectedTargetEntityId: null);

        Assert.Equal(
            new LocalMovementContext(
                ImmediateAllies: 0,
                ImmediateEnemies: 0,
                SupportAllies: 1,
                SupportEnemies: 0,
                AlliedComposition: new LoadoutCompositionCounts(0, 0, 1, 0, 0, 0),
                EnemyComposition: default,
                NearestAllyEntityId: null,
                SecondThreatEntityId: null),
            context);
    }

    [Fact]
    public void MembershipAtTheExactRadiusIsInclusive()
    {
        var actor = CreateState(1, factionId: 0, xRaw: 0, yRaw: 0);
        var agents = new[]
        {
            actor,
            CreateState(2, 0, xRaw: checked((int)TestImmediateRadiusRaw), 0),
            CreateState(3, 0, xRaw: checked((int)TestSupportRadiusRaw), 0),
            CreateState(4, 0, xRaw: checked((int)TestSupportRadiusRaw) + 1, 0),
            CreateState(5, 1, xRaw: -checked((int)TestImmediateRadiusRaw), 0),
            CreateState(6, 1, 0, yRaw: checked((int)TestSupportRadiusRaw)),
            CreateState(7, 1, 0, yRaw: checked((int)TestSupportRadiusRaw) + 1),
        };

        var context = Derive(agents, actor, selectedTargetEntityId: 5);

        // Entity 2 sits exactly on the immediate radius and entity 3 exactly
        // on the support radius: both count, inclusively. Entities 4 and 7,
        // one raw unit past the support radius, count nowhere. Entity 5 is
        // the selected target, so no other immediate enemy remains for the
        // second threat.
        Assert.Equal(1, context.ImmediateAllies);
        Assert.Equal(3, context.SupportAllies);
        Assert.Equal(1, context.ImmediateEnemies);
        Assert.Equal(2, context.SupportEnemies);
        Assert.Equal(2UL, context.NearestAllyEntityId);
        Assert.Null(context.SecondThreatEntityId);
        Assert.Equal(3, context.AlliedComposition.Kampilan);
        Assert.Equal(2, context.EnemyComposition.Kampilan);
        Assert.Equal(context, DeriveOracle(agents, actor, 5));
    }

    [Fact]
    public void DeadAgentsCountNowhere()
    {
        var actor = CreateState(1, factionId: 0, xRaw: 0, yRaw: 0);
        var agents = new[]
        {
            actor,
            CreateState(2, 0, xRaw: 100, yRaw: 0, alive: false),
            CreateState(3, 1, xRaw: -100, yRaw: 0, alive: false),
            CreateState(4, 1, xRaw: 200, yRaw: 0),
        };

        var context = Derive(agents, actor, selectedTargetEntityId: 4);

        Assert.Equal(0, context.ImmediateAllies);
        Assert.Equal(1, context.SupportAllies);
        Assert.Equal(1, context.ImmediateEnemies);
        Assert.Equal(1, context.SupportEnemies);
        Assert.Null(context.NearestAllyEntityId);
        Assert.Null(context.SecondThreatEntityId);
        Assert.Equal(context, DeriveOracle(agents, actor, 4));
    }

    [Fact]
    public void AllSixLoadoutsAccumulateIntoTheirOwnBuckets()
    {
        var actor = CreateState(1, factionId: 0, xRaw: 0, yRaw: 0, Kampilan);
        var loadouts = new[]
        {
            Kampilan, Wasay, Kalis, Itak, KalisShield, ItakShield,
        };
        var agents = new List<AgentState> { actor };
        for (var index = 0; index < loadouts.Length; index++)
        {
            agents.Add(CreateState(
                (ulong)(index + 2),
                factionId: 0,
                xRaw: 100 * (index + 1),
                yRaw: 0,
                loadouts[index]));
            agents.Add(CreateState(
                (ulong)(index + 8),
                factionId: 1,
                xRaw: -100 * (index + 1),
                yRaw: 0,
                loadouts[index]));
        }

        var context = Derive([.. agents], actor, selectedTargetEntityId: 8);

        // The allied composition carries the actor's own Kampilan bucket on
        // top of one ally per canonical loadout.
        Assert.Equal(
            new LoadoutCompositionCounts(2, 1, 1, 1, 1, 1),
            context.AlliedComposition);
        Assert.Equal(
            new LoadoutCompositionCounts(1, 1, 1, 1, 1, 1),
            context.EnemyComposition);
        Assert.Equal(context, DeriveOracle([.. agents], actor, 8));
    }

    [Fact]
    public void SquaredDistanceTiesBreakOnLowerEntityId()
    {
        var actor = CreateState(5, factionId: 0, xRaw: 0, yRaw: 0);
        var agents = new[]
        {
            actor,
            CreateState(9, 0, xRaw: 300, yRaw: 0),
            CreateState(2, 0, xRaw: 0, yRaw: 300),
            CreateState(11, 1, xRaw: -400, yRaw: 0),
            CreateState(3, 1, xRaw: 0, yRaw: -400),
            CreateState(12, 1, xRaw: 400, yRaw: 0),
        };

        // Both allies sit at squared distance 90,000 and both non-target
        // enemies at 160,000: the lower entity id wins each tie. Entity 3 is
        // the selected target (the tie the selection stage itself breaks),
        // leaving 11 and 12 tied for second threat.
        var context = Derive(agents, actor, selectedTargetEntityId: 3);

        Assert.Equal(2UL, context.NearestAllyEntityId);
        Assert.Equal(11UL, context.SecondThreatEntityId);
        Assert.Equal(context, DeriveOracle(agents, actor, 3));
    }

    [Fact]
    public void TheSelectedTargetIsExcludedFromTheSecondThreat()
    {
        var actor = CreateState(1, factionId: 0, xRaw: 0, yRaw: 0);
        var near = CreateState(2, 1, xRaw: 100, yRaw: 0);
        var far = CreateState(3, 1, xRaw: 200, yRaw: 0);
        var agents = new[] { actor, near, far };

        // With the nearest immediate enemy selected as target, the second
        // threat falls to the next-nearest one.
        Assert.Equal(
            3UL,
            Derive(agents, actor, selectedTargetEntityId: 2)
                .SecondThreatEntityId);

        // With no target selected at all, the nearest immediate enemy is the
        // second threat — nothing is excluded.
        Assert.Equal(
            2UL,
            Derive(agents, actor, selectedTargetEntityId: null)
                .SecondThreatEntityId);

        // With the only immediate enemy selected as target, no second threat
        // remains.
        Assert.Null(
            Derive([actor, near], actor, selectedTargetEntityId: 2)
                .SecondThreatEntityId);
    }

    [Fact]
    public void AnEnemyBeyondPerceptionIsNotObserved()
    {
        // Perception reaches 1,000 raw units; the support radius reaches
        // 6,144. The enemy at 2,000 sits inside the support ring but beyond
        // perception, so it counts nowhere — the context never reacts to an
        // enemy the actor could not have targeted. The ally at the same
        // range carries no perception test and counts.
        var actor = CreateState(
            1, factionId: 0, xRaw: 0, yRaw: 0, perceptionRangeRaw: 1_000);
        var agents = new[]
        {
            actor,
            CreateState(2, 0, xRaw: 2_000, yRaw: 0),
            CreateState(3, 1, xRaw: -2_000, yRaw: 0),
        };

        var context = Derive(agents, actor, selectedTargetEntityId: null);

        Assert.Equal(2, context.SupportAllies);
        Assert.Equal(0, context.SupportEnemies);
        Assert.Equal(2UL, context.NearestAllyEntityId);
        Assert.Null(context.SecondThreatEntityId);
        Assert.Equal(context, DeriveOracle(agents, actor, null));
    }

    [Fact]
    public void ExplicitlyPermutedSpansDeriveIdenticalContexts()
    {
        var actor = CreateState(4, factionId: 0, xRaw: 1_000, yRaw: 1_000, Kalis);
        var canonical = new[]
        {
            CreateState(1, 1, xRaw: 1_300, yRaw: 1_000, Wasay),
            CreateState(2, 0, xRaw: 1_000, yRaw: 1_300, ItakShield),
            CreateState(3, 1, xRaw: 700, yRaw: 1_000, Itak),
            actor,
            CreateState(5, 0, xRaw: 1_000, yRaw: 700, KalisShield),
            CreateState(6, 1, xRaw: 4_000, yRaw: 1_000, Kampilan),
            CreateState(7, 0, xRaw: 1_000, yRaw: 4_000, Kampilan),
            CreateState(8, 1, xRaw: 1_300, yRaw: 1_300, Kalis, alive: false),
        };

        // Three explicit permutations of the same eight agents: reversed,
        // rotated by three, and a hand-picked interleave. CreateForTesting
        // canonicalizes storage by EntityId, so this span seam is the only
        // honest storage-order coverage.
        var reversed = canonical.Reverse().ToArray();
        var rotated = canonical.Skip(3).Concat(canonical.Take(3)).ToArray();
        var interleaved = new[]
        {
            canonical[5], canonical[0], canonical[7], canonical[2],
            canonical[4], canonical[6], canonical[1], canonical[3],
        };

        var expected = DeriveOracle(canonical, actor, 3);
        Assert.Equal(expected, Derive(canonical, actor, 3));
        Assert.Equal(expected, Derive(reversed, actor, 3));
        Assert.Equal(expected, Derive(rotated, actor, 3));
        Assert.Equal(expected, Derive(interleaved, actor, 3));
    }

    [Fact]
    public void MaximumSupportedCoordinatesDeriveWithoutOverflow()
    {
        // The corner of the largest validatable world: MaximumMapDimension
        // whole units, in raw fixed-point. Deltas and radii behave exactly
        // as they do at the origin.
        var cornerRaw = checked(Scenario.MaximumMapDimension * FixedPoint.Scale);
        var actor = CreateState(1, factionId: 0, xRaw: cornerRaw, yRaw: cornerRaw);
        var agents = new[]
        {
            actor,
            CreateState(
                2,
                0,
                xRaw: cornerRaw - checked((int)TestSupportRadiusRaw),
                yRaw: cornerRaw),
            CreateState(
                3,
                1,
                xRaw: cornerRaw,
                yRaw: cornerRaw - checked((int)TestImmediateRadiusRaw)),
        };

        var context = Derive(agents, actor, selectedTargetEntityId: 3);

        Assert.Equal(2, context.SupportAllies);
        Assert.Equal(1, context.ImmediateEnemies);
        Assert.Equal(2UL, context.NearestAllyEntityId);
        Assert.Equal(context, DeriveOracle(agents, actor, 3));
    }

    [Fact]
    public void MaximumValidBodyRadiusSquaresThroughInt128()
    {
        // The largest body radius the jitter-span guard admits. Its support
        // radius is 3,221,225,460 raw — past int.MaxValue without being
        // invalid — and that radius squared is above long.MaxValue, so this
        // comparison only works widened through Int128 (design 4.4).
        const int maximumBodyRadiusRaw = (int.MaxValue - 1) / 8;
        var supportRadiusRaw = MovementContextQuery.ContextRadiusRaw(
            maximumBodyRadiusRaw, bodyDiametersBasisPoints: 60_000);
        Assert.True(supportRadiusRaw > int.MaxValue);
        Assert.True(
            MovementContextQuery.SquaredContextRadius(supportRadiusRaw) >
            long.MaxValue);

        // The two most distant positions a maximum-size validated map can
        // hold; their squared separation still fits the production long, and
        // both neighbours land inside the giant support ring. Perception is
        // the map dimension too, so the corner enemy sits exactly on the
        // inclusive perception boundary.
        var cornerRaw = checked(Scenario.MaximumMapDimension * FixedPoint.Scale);
        var actor = CreateState(
            1, factionId: 0, xRaw: 0, yRaw: 0, perceptionRangeRaw: cornerRaw);
        var agents = new[]
        {
            actor,
            CreateState(2, 0, xRaw: cornerRaw, yRaw: cornerRaw),
            CreateState(3, 1, xRaw: cornerRaw, yRaw: 0),
        };

        var context = MovementContextQuery.Derive(
            agents,
            actor,
            selectedTargetEntityId: null,
            MovementContextQuery.SquaredContextRadius(
                MovementContextQuery.ContextRadiusRaw(
                    maximumBodyRadiusRaw, bodyDiametersBasisPoints: 25_000)),
            MovementContextQuery.SquaredContextRadius(supportRadiusRaw));

        Assert.Equal(2, context.SupportAllies);
        Assert.Equal(1, context.SupportEnemies);
        Assert.Equal(2UL, context.NearestAllyEntityId);
    }

    // ----- The production hook (7.3), against the oracle -----

    [Fact]
    public void SeededWorldsMatchTheNaiveOracleFieldForField()
    {
        foreach (var seed in new ulong[] { 1, 2, 7 })
        {
            var scenario = Scenario.CreateDefault(seed, totalAgents: 40) with
            {
                MovementPreset = MovementPresetId.EquipmentRelativeFootworkV6,
                CombatPreset = CombatPresetId.PrecolonialPhilippinesV2,
            };
            var ruleset = MovementPresetRegistry.Get(scenario.MovementPreset);
            var immediateRadiusSquared = MovementContextQuery.SquaredContextRadius(
                MovementContextQuery.ContextRadiusRaw(
                    scenario.BodyRadiusRaw,
                    ruleset.ImmediateRadiusBodyDiametersBasisPoints));
            var supportRadiusSquared = MovementContextQuery.SquaredContextRadius(
                MovementContextQuery.ContextRadiusRaw(
                    scenario.BodyRadiusRaw,
                    ruleset.SupportRadiusBodyDiametersBasisPoints));
            var simulation = BattleSimulation.Create(scenario);

            for (var tick = 0; tick < 40; tick++)
            {
                var bodies = simulation.Agents
                    .Select(view => ToBody(view, scenario.PerceptionRangeRaw))
                    .ToList();
                simulation.AdvanceOneTick();
                var viewsById = simulation.Agents
                    .ToDictionary(view => view.EntityId);

                foreach (var actor in bodies.Where(body => body.IsAlive))
                {
                    var expectedTarget = NaiveMovementContextQuery
                        .ExpectedSelectedTarget(bodies, actor);
                    var expected = NaiveMovementContextQuery.Compute(
                        bodies,
                        actor,
                        expectedTarget,
                        immediateRadiusSquared,
                        supportRadiusSquared);

                    Assert.Equal(
                        expectedTarget,
                        viewsById[actor.EntityId].TargetEntityId);
                    Assert.Equal(
                        expected,
                        simulation.LocalMovementContextForTesting(actor.EntityId));
                }

                if (simulation.Outcome != BattleOutcome.Ongoing)
                {
                    break;
                }
            }
        }
    }

    [Fact]
    public void HandPlacedTangenciesMatchThroughTheProductionHook()
    {
        var scenario = CreateContextTestScenario();
        var origin = 100 * FixedPoint.Scale;
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            CreateState(1, 0, origin, origin),
            CreateState(2, 0, origin + checked((int)TestSupportRadiusRaw), origin),
            CreateState(3, 0, origin + checked((int)TestImmediateRadiusRaw), origin),
            CreateState(4, 0, origin + checked((int)TestSupportRadiusRaw) + 1, origin),
            CreateState(5, 1, origin - checked((int)TestImmediateRadiusRaw), origin),
            CreateState(6, 1, origin, origin + checked((int)TestSupportRadiusRaw)),
            CreateState(7, 1, origin, origin + checked((int)TestSupportRadiusRaw) + 1));

        simulation.AdvanceOneTick();

        var context = simulation.LocalMovementContextForTesting(1);
        var actorView = Assert.Single(
            simulation.Agents, view => view.EntityId == 1);

        // Entity 5, exactly on the immediate radius, is both the nearest
        // enemy — the tick's selected target — and the only immediate enemy,
        // so no second threat remains. Entities 4 and 7 sit one raw unit
        // past the support radius and count nowhere.
        Assert.Equal(5UL, actorView.TargetEntityId);
        Assert.Equal(1, context.ImmediateAllies);
        Assert.Equal(3, context.SupportAllies);
        Assert.Equal(1, context.ImmediateEnemies);
        Assert.Equal(2, context.SupportEnemies);
        Assert.Equal(3UL, context.NearestAllyEntityId);
        Assert.Null(context.SecondThreatEntityId);
        Assert.Equal(3, context.AlliedComposition.Kampilan);
        Assert.Equal(2, context.EnemyComposition.Kampilan);
    }

    [Fact]
    public void TargetSelectionOnTickOneIsIdenticalUnderV4AndV6()
    {
        // A uniform roster: the design section 12 deployment assignment is
        // the exact identity when every warrior resolves the same ally
        // clearance, so both presets open on byte-identical spawns and any
        // targeting difference could only come from the context hook this
        // test polices. A mixed roster would move V6 warriors to different
        // slots on purpose and prove nothing about targeting.
        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: 40) with
        {
            RosterCounts = [0, 20, 0, 0],
        };
        var legacy = BattleSimulation.Create(scenario with
        {
            MovementPreset = MovementPresetId.PersistentContingentsV4,
        });
        var equipped = BattleSimulation.Create(scenario with
        {
            MovementPreset = MovementPresetId.EquipmentRelativeFootworkV6,
        });

        legacy.AdvanceOneTick();
        equipped.AdvanceOneTick();

        Assert.Equal(
            legacy.Agents.Select(view => (view.EntityId, view.TargetEntityId)),
            equipped.Agents.Select(view => (view.EntityId, view.TargetEntityId)));
    }

    // ----- Legacy control (11.3) and the once-per-agent seam -----

    [Fact]
    public void LegacyPresetsNeverInvokeContextAccumulation()
    {
        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: 40) with
        {
            MovementPreset = MovementPresetId.PersistentContingentsV4,
        };
        var simulation = BattleSimulation.Create(scenario);

        for (var tick = 0; tick < 10; tick++)
        {
            simulation.AdvanceOneTick();
        }

        Assert.Equal(0L, simulation.LocalMovementContextDerivationsForTesting);
        Assert.Throws<InvalidOperationException>(
            () => simulation.LocalMovementContextForTesting(1));
        Assert.Equal(
            default(LoadoutCompositionCounts),
            simulation.SurvivingCompositionForTesting(0));
        Assert.Equal(
            default(LoadoutCompositionCounts),
            simulation.SurvivingCompositionForTesting(1));
    }

    [Fact]
    public void AV6WorldDerivesOneContextPerLivingAgentPerTick()
    {
        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: 40) with
        {
            MovementPreset = MovementPresetId.EquipmentRelativeFootworkV6,
        };
        var simulation = BattleSimulation.Create(scenario);

        simulation.AdvanceOneTick();
        Assert.Equal(40L, simulation.LocalMovementContextDerivationsForTesting);

        simulation.AdvanceOneTick();
        Assert.Equal(80L, simulation.LocalMovementContextDerivationsForTesting);
    }

    // ----- Global surviving totals (7.5) -----

    [Fact]
    public void SurvivingCompositionsMatchTheLivingRosterEachTick()
    {
        var scenario = Scenario.CreateDefault(seed: 3, totalAgents: 40) with
        {
            MovementPreset = MovementPresetId.EquipmentRelativeFootworkV6,
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV2,
        };
        var simulation = BattleSimulation.Create(scenario);

        for (var tick = 0; tick < 30; tick++)
        {
            // The totals are derived at tick start, over the agents alive as
            // of the previous tick's end — exactly the roster visible here.
            var expectedByFaction = new LoadoutCompositionCounts[2];
            foreach (var view in simulation.Agents.Where(view => view.IsAlive))
            {
                expectedByFaction[view.FactionId] =
                    expectedByFaction[view.FactionId].Add(view.Loadout);
            }

            simulation.AdvanceOneTick();

            Assert.Equal(
                expectedByFaction[0],
                simulation.SurvivingCompositionForTesting(0));
            Assert.Equal(
                expectedByFaction[1],
                simulation.SurvivingCompositionForTesting(1));

            if (simulation.Outcome != BattleOutcome.Ongoing)
            {
                break;
            }
        }
    }

    // ----- Warm-loop allocation (scratch rows allocated once) -----

    [Fact]
    public void RepeatedQuietV6TicksHaveBoundedAllocations()
    {
        const int measuredTicks = 1_000;

        // The same measured ceiling BattleSimulationTests uses for the
        // legacy quiet-tick window: context derivation reuses its
        // constructor-allocated scratch rows and a stack accumulator, so a
        // warm V6 tick allocates nothing either.
        const long maximumAllocatedBytes = 8_192;
        var scenario = CreateContextTestScenario() with
        {
            TickLimit = measuredTicks + 100,
            AttackRangeRaw = FixedPoint.Scale,
            PerceptionRangeRaw = 5 * FixedPoint.Scale,
        };
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            CreateState(1, 0, 10 * FixedPoint.Scale, 10 * FixedPoint.Scale),
            CreateState(2, 1, 190 * FixedPoint.Scale, 90 * FixedPoint.Scale));

        for (var tick = 0; tick < 32; tick++)
        {
            simulation.AdvanceOneTick();
        }

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var tick = 0; tick < measuredTicks; tick++)
        {
            simulation.AdvanceOneTick();
        }

        var allocatedBytes =
            GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        Assert.True(
            allocatedBytes <= maximumAllocatedBytes,
            $"Quiet V6 ticks allocated {allocatedBytes:N0} bytes; " +
            $"expected at most {maximumAllocatedBytes:N0}.");
    }

    // ----- Helpers -----

    /// <summary>
    /// A hand-placement scenario mirroring the one
    /// <c>BattleSimulationTests</c> uses, pinned to combat V1 for the same
    /// roster-count reason, with the movement preset opted into
    /// equipment-relative footwork. Its body radius makes the registered
    /// basis points resolve to <see cref="TestImmediateRadiusRaw"/> and
    /// <see cref="TestSupportRadiusRaw"/> exactly.
    /// </summary>
    private static Scenario CreateContextTestScenario() =>
        new(
            Seed: 1,
            MapWidth: 200,
            MapHeight: 100,
            AgentsPerFaction: 1,
            TickRate: 20,
            TickLimit: 1_000)
        {
            MaximumHitPoints = 100,
            DamagePerAttack = 10,
            AttackRangeRaw = 5 * FixedPoint.Scale,
            PerceptionRangeRaw = 200 * FixedPoint.Scale,
            BodyRadiusRaw = TestBodyRadiusRaw,
            MovementSpeedRaw = FixedPoint.Scale / 2,
            AttackCooldownTicks = 1,
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV1,
            MovementPreset = MovementPresetId.EquipmentRelativeFootworkV6,
        };

    private static AgentState CreateState(
        ulong entityId,
        int factionId,
        int xRaw,
        int yRaw,
        CombatLoadout? loadout = null,
        int perceptionRangeRaw = 200 * FixedPoint.Scale,
        bool alive = true)
    {
        var state = new AgentState(
            entityId,
            factionId,
            xRaw,
            yRaw,
            maximumHitPoints: 100,
            movementSpeedRaw: FixedPoint.Scale / 2,
            perceptionRangeRaw: perceptionRangeRaw,
            attackRangeRaw: 5 * FixedPoint.Scale,
            damagePerAttack: 10,
            attackCooldownTicks: 1,
            loadout ?? Kampilan);
        if (!alive)
        {
            state.HitPoints = 0;
        }

        return state;
    }

    /// <summary>
    /// Calls the production span seam with this fixture's exact raw radii.
    /// </summary>
    private static LocalMovementContext Derive(
        AgentState[] agents,
        AgentState actor,
        ulong? selectedTargetEntityId) =>
        MovementContextQuery.Derive(
            agents,
            actor,
            selectedTargetEntityId,
            MovementContextQuery.SquaredContextRadius(TestImmediateRadiusRaw),
            MovementContextQuery.SquaredContextRadius(TestSupportRadiusRaw));

    /// <summary>
    /// Computes the naive oracle's answer for the same fixture radii.
    /// </summary>
    private static LocalMovementContext DeriveOracle(
        AgentState[] agents,
        AgentState actor,
        ulong? selectedTargetEntityId) =>
        NaiveMovementContextQuery.Compute(
            agents.Select(state => ToBody(state)).ToList(),
            ToBody(actor),
            selectedTargetEntityId,
            MovementContextQuery.SquaredContextRadius(TestImmediateRadiusRaw),
            MovementContextQuery.SquaredContextRadius(TestSupportRadiusRaw));

    private static NaiveMovementContextQuery.Body ToBody(AgentState state) =>
        new(
            state.EntityId,
            state.FactionId,
            state.XRaw,
            state.YRaw,
            state.IsAlive,
            state.Loadout,
            state.PerceptionRangeRaw);

    private static NaiveMovementContextQuery.Body ToBody(
        AgentView view,
        int perceptionRangeRaw) =>
        new(
            view.EntityId,
            view.FactionId,
            view.XRaw,
            view.YRaw,
            view.IsAlive,
            view.Loadout,
            perceptionRangeRaw);
}
