using Hukbo.Core.Combat;
using Hukbo.Core.Determinism;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;
using Hukbo.Headless;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// Coverage for <see cref="MovementPresetId.ContingentShapeV12"/>, registered
/// as a verbatim restatement of
/// <see cref="MovementPresetId.LastStandEngagementV11"/>'s field values under
/// its own <c>id</c>. This preset carries no behaviour of its own yet — it
/// exists only so the identity is registered and can be selected — so the
/// coverage here is limited to the numeric value, registration, and field
/// equality against V11, the same shape
/// <c>LastStandEngagementV11Tests.LastStandEngagementV11IsRegistered</c> and
/// <c>LastStandEngagementV11Tests.LastStandEngagementV11CarriesItsOwnIdentity</c>
/// use for V11 against V10.
/// </summary>
public sealed class ContingentShapeV12Tests
{
    [Fact]
    public void ContingentShapeV12HasTheExpectedNumericValue()
    {
        Assert.Equal(12, (int)MovementPresetId.ContingentShapeV12);
    }

    [Fact]
    public void ContingentShapeV12IsRegistered()
    {
        Assert.True(
            MovementPresetRegistry.IsRegistered(
                MovementPresetId.ContingentShapeV12));
    }

    [Fact]
    public void ContingentShapeV12CarriesItsOwnIdentity()
    {
        var v11 = MovementPresetRegistry.Get(MovementPresetId.LastStandEngagementV11);
        var v12 = MovementPresetRegistry.Get(MovementPresetId.ContingentShapeV12);

        Assert.Equal(MovementPresetId.ContingentShapeV12, v12.Id);

        // The ruleset is a verbatim restatement of V11's field values, so
        // every tunable matches; only the folded Id separates the two
        // content hashes.
        Assert.Equal(v11.CohesionRadiusMultiplier, v12.CohesionRadiusMultiplier);
        Assert.Equal(v11.CloseRadiusMultiplier, v12.CloseRadiusMultiplier);
        Assert.Equal(v11.CohesionCycleTicks, v12.CohesionCycleTicks);
        Assert.Equal(v11.CohesionDutyTicks, v12.CohesionDutyTicks);
        Assert.Equal(v11.ArrivalTaperMultiplier, v12.ArrivalTaperMultiplier);
        Assert.Equal(v11.OffsetUnit, v12.OffsetUnit);
        Assert.Equal(
            v11.NarrowsCohesionScanToCohesionCapableContingents,
            v12.NarrowsCohesionScanToCohesionCapableContingents);
        Assert.Equal(v11.SelectsLeaderByRank, v12.SelectsLeaderByRank);
        Assert.Equal(
            v11.UsesEquipmentRelativeFootwork,
            v12.UsesEquipmentRelativeFootwork);
        Assert.Equal(v11.AppliesPressureInterrupt, v12.AppliesPressureInterrupt);
        Assert.NotEqual(v11.ContentHash, v12.ContentHash);
    }

    // ----- BattleSimulation.UsesBattlefieldRealism admits V12 -----

    /// <summary>
    /// A full seed-1, two-hundred-agent battle under
    /// <see cref="MovementPresetId.ContingentShapeV12"/> reproduces the exact
    /// same trajectory -- per-agent position, hit points, intent, target,
    /// and movement resolution on every tick, plus the exact ordered event
    /// stream -- as the same battle under
    /// <see cref="MovementPresetId.LastStandEngagementV11"/>. V12 carries no
    /// ruleset field that differs from V11 (<see
    /// cref="ContingentShapeV12CarriesItsOwnIdentity"/> proves that), so
    /// this is what proves V12 is a true behavioural superset of V11 for the
    /// three behaviours <c>UsesBattlefieldRealism</c> admits it to -- cohort
    /// deployment, the nearest-melee-threat scratch, and the ranged retreat
    /// rung -- before any contingent-shaping behaviour is layered on top of
    /// it. Before the fix this diverges at tick 1 on the deployment
    /// permutation alone, which is the failure this test exists to catch.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>LastStandThresholdAgents</c> is pinned to zero here, disabling the
    /// last-stand regroup yield entirely, deliberately mirroring
    /// <c>LastStandEngagementV11Tests.WithNoLastStandV11RunsByteIdenticallyToV10</c>'s
    /// identical scoping choice for the V10/V11 pair. <c>BattleSimulation</c>
    /// gates that yield through a second, separate predicate,
    /// <c>YieldsLastStandEngagement</c>, which is not
    /// <c>UsesBattlefieldRealism</c> and is out of this task's owned scope
    /// -- and it does not admit V12 either. Measured directly: with the
    /// default <c>LastStandThresholdAgents</c> of six, this same comparison
    /// diverges at tick 1629, entity 197's <c>Move</c> event naming enemy
    /// 100 under V11 (the yield lets it keep pursuing) against rally agent
    /// 123 under V12 (no yield, so it regroups instead). That is a second,
    /// separate defect -- <c>YieldsLastStandEngagement</c> also needs
    /// <c>MovementPresetId.ContingentShapeV12</c> admitted before V12 is a
    /// true superset of V11 under every scenario -- reported here rather
    /// than silently fixed, because this task owns only
    /// <c>UsesBattlefieldRealism</c>.
    /// </para>
    /// <para>
    /// <c>Scenario.MovementPreset</c> folds directly into
    /// <c>BattleSimulation.ComputeStateHash()</c>, and
    /// <c>MovementRuleset.ContentHash</c> folds its own <c>Id</c>, so two
    /// different presets can never produce a literally equal state hash
    /// however identically their agents behave -- the same fact
    /// <c>BattlefieldRealismV10Tests</c> and
    /// <c>LastStandEngagementV11Tests</c> already document for the V8/V10
    /// and V10/V11 pairs. The equality proved here is the one those two
    /// suites already use for the same reason: full per-agent field
    /// equality every tick plus full <c>LastEvents</c> equality, which
    /// together also fold to the same ordered event hash.
    /// </para>
    /// </remarks>
    [Fact]
    public void ContingentShapeV12ProducesAByteIdenticalFullBattleToLastStandEngagementV11()
    {
        var v11 = CreateFullBattleControlRun(
            MovementPresetId.LastStandEngagementV11, lastStandThresholdAgents: 0);
        var v12 = CreateFullBattleControlRun(
            MovementPresetId.ContingentShapeV12, lastStandThresholdAgents: 0);

        var v11EventFold = Fnv1a.OffsetBasis;
        var v12EventFold = Fnv1a.OffsetBasis;

        var comparedTicks = 0;
        while (v11.Outcome == BattleOutcome.Ongoing && comparedTicks < 10_000)
        {
            v11.AdvanceOneTick();
            v12.AdvanceOneTick();
            comparedTicks++;

            Assert.Equal(v11.Tick, v12.Tick);
            Assert.Equal(v11.Outcome, v12.Outcome);
            Assert.Equal(v11.LastEvents, v12.LastEvents);
            Assert.Equal(v11.Agents.Count, v12.Agents.Count);

            for (var i = 0; i < v11.Agents.Count; i++)
            {
                var left = v11.Agents[i];
                var right = v12.Agents[i];

                Assert.Equal(left.EntityId, right.EntityId);
                Assert.Equal(left.XRaw, right.XRaw);
                Assert.Equal(left.YRaw, right.YRaw);
                Assert.Equal(left.HitPoints, right.HitPoints);
                Assert.Equal(left.Intent, right.Intent);
                Assert.Equal(left.IsAlive, right.IsAlive);
                Assert.Equal(left.TargetEntityId, right.TargetEntityId);
                Assert.Equal(left.MovementResolution, right.MovementResolution);
            }

            foreach (var battleEvent in v11.LastEvents)
            {
                HeadlessRunner.AddEventToHash(ref v11EventFold, battleEvent);
            }

            foreach (var battleEvent in v12.LastEvents)
            {
                HeadlessRunner.AddEventToHash(ref v12EventFold, battleEvent);
            }

            Assert.Equal(v11EventFold, v12EventFold);
        }

        // A trajectory comparison over zero ticks -- or one that only ran
        // because the battle was already resolved before the first
        // AdvanceOneTick call -- would prove nothing.
        Assert.True(comparedTicks > 50, $"Only compared {comparedTicks} ticks.");
        Assert.Equal(v11.Outcome, v12.Outcome);
        Assert.Equal(v11.Tick, v12.Tick);
    }

    /// <summary>
    /// <see cref="MovementPresetId.BattlefieldRealismV10"/>'s own full-battle
    /// trajectory is unmoved by admitting
    /// <see cref="MovementPresetId.ContingentShapeV12"/> to
    /// <c>BattleSimulation.UsesBattlefieldRealism</c>: the predicate is a
    /// closed <c>is ... or ...</c> pattern, so appending a third disjunct
    /// cannot change the boolean it returns for a V10 input. This pins that
    /// fact against the actual terminal state hash, ordered-event fold,
    /// outcome, and tick count this build produces, the same four fields
    /// <c>MovementPresetFreezeTests</c> pins for every earlier preset, so a
    /// later change to the predicate -- or to anything else V10's three
    /// behaviours touch -- that does move this trajectory is caught here
    /// rather than only inferred from the pattern shape.
    /// </summary>
    [Fact]
    public void BattlefieldRealismV10FullBattleReproducesItsPinnedTrajectory()
    {
        var simulation = CreateFullBattleControlRun(
            MovementPresetId.BattlefieldRealismV10);
        var result = RunToCompletion(simulation);

        Assert.Equal(1988L, result.Tick);
        Assert.Equal(BattleOutcome.Faction0Victory, result.Outcome);
        Assert.Equal(0x763F63C24832232AUL, result.StateHash);
        Assert.Equal(0xC6DEAD4FE4E4A307UL, result.EventFold);
    }

    /// <summary>
    /// <see cref="MovementPresetId.LastStandEngagementV11"/>'s own
    /// full-battle trajectory is unmoved by the same predicate edit, for the
    /// same reason <see
    /// cref="BattlefieldRealismV10FullBattleReproducesItsPinnedTrajectory"/>
    /// pins it for V10.
    /// </summary>
    [Fact]
    public void LastStandEngagementV11FullBattleReproducesItsPinnedTrajectory()
    {
        var simulation = CreateFullBattleControlRun(
            MovementPresetId.LastStandEngagementV11);
        var result = RunToCompletion(simulation);

        Assert.Equal(1913L, result.Tick);
        Assert.Equal(BattleOutcome.Faction0Victory, result.Outcome);
        Assert.Equal(0x7FF1B1115BAD8960UL, result.StateHash);
        Assert.Equal(0xEF488DCE17F57587UL, result.EventFold);
    }

    /// <summary>
    /// Builds a full-battle control run through the ordinary
    /// <c>BattleSimulation.Create(Scenario)</c> path -- deployment planning
    /// included, unlike <c>CreateForTesting</c> -- pinning the same seed,
    /// roster size, body radius, and combat preset
    /// <c>MovementPresetFreezeTests.CreateControlRun</c> pins, for the same
    /// reason: a fixture or a pinned literal is only meaningful against a
    /// scenario that cannot silently drift out from under it when an
    /// unrelated default changes elsewhere in the codebase.
    /// <paramref name="lastStandThresholdAgents"/> defaults to
    /// <c>Scenario.CreateDefault</c>'s own value (six) and is overridden to
    /// zero by the one comparison that must stay clear of the separate,
    /// unadmitted <c>YieldsLastStandEngagement</c> gate documented on
    /// <see cref="ContingentShapeV12ProducesAByteIdenticalFullBattleToLastStandEngagementV11"/>.
    /// </summary>
    private static BattleSimulation CreateFullBattleControlRun(
        MovementPresetId movementPreset,
        int? lastStandThresholdAgents = null)
    {
        const int CapturedBodyRadiusRaw = 4 * FixedPoint.Scale;

        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: 200) with
        {
            MovementPreset = movementPreset,
            BodyRadiusRaw = CapturedBodyRadiusRaw,
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV2,
        };

        if (lastStandThresholdAgents.HasValue)
        {
            scenario = scenario with
            {
                LastStandThresholdAgents = lastStandThresholdAgents.Value,
            };
        }

        scenario.Validate();
        return BattleSimulation.Create(scenario);
    }

    /// <summary>
    /// Runs <paramref name="simulation"/> to its own termination -- it stops
    /// advancing once <see cref="BattleSimulation.Outcome"/> leaves
    /// <see cref="BattleOutcome.Ongoing"/> -- folding the ordered event
    /// stream into a single running FNV-1a hash exactly the way
    /// <c>HeadlessRunner</c> folds it for the determinism workload, and
    /// returns the terminal state hash, event fold, outcome, and tick
    /// alongside it.
    /// </summary>
    private static (long Tick, BattleOutcome Outcome, ulong StateHash, ulong EventFold)
        RunToCompletion(BattleSimulation simulation)
    {
        var fold = Fnv1a.OffsetBasis;
        while (simulation.Outcome == BattleOutcome.Ongoing)
        {
            simulation.AdvanceOneTick();

            foreach (var battleEvent in simulation.LastEvents)
            {
                HeadlessRunner.AddEventToHash(ref fold, battleEvent);
            }
        }

        return (simulation.Tick, simulation.Outcome, simulation.ComputeStateHash(), fold);
    }
}
