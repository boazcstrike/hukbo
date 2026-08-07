using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests.Movement;

public sealed class MovementBehaviorMetricsTests
{
    [Fact]
    public void FreshAccumulator_ReportsEveryFieldAsZero()
    {
        var accumulator = default(MovementBehaviorMetricsAccumulator);

        var metrics = accumulator.ToMetrics();

        Assert.Equal(default, metrics);
        Assert.Equal(0L, metrics.ApproachAgentTicks);
        Assert.Equal(0L, metrics.EngageAgentTicks);
        Assert.Equal(0L, metrics.CommitAgentTicks);
        Assert.Equal(0L, metrics.RecoverAgentTicks);
        Assert.Equal(0L, metrics.RefuseAgentTicks);
        Assert.Equal(0L, metrics.DisengageAgentTicks);
        Assert.Equal(0L, metrics.RegroupAgentTicks);
        Assert.Equal(0L, metrics.PursueAgentTicks);
        Assert.Equal(0L, metrics.PostureTransitions);
        Assert.Equal(0L, metrics.FacingStepsTurned);
        Assert.Equal(0L, metrics.DisengagementEntries);
        Assert.Equal(0L, metrics.ConflictDenials);
        Assert.Equal(0L, metrics.RouteRefusalNoCandidatesBuilt);
        Assert.Equal(0L, metrics.RouteRefusalStepEndpointRejected);
        Assert.Equal(0L, metrics.RouteRefusalDirectCandidateOmitted);
        Assert.Equal(0L, metrics.RouteRefusalLaneNotClear);
    }

    [Fact]
    public void AddTick_SumsTheRunningTotalsAcrossTicks()
    {
        var accumulator = default(MovementBehaviorMetricsAccumulator);

        accumulator.AddTick(
            approachAgents: 10,
            engageAgents: 9,
            commitAgents: 8,
            recoverAgents: 7,
            refuseAgents: 6,
            disengageAgents: 5,
            regroupAgents: 4,
            pursueAgents: 3,
            postureTransitions: 2,
            facingStepsTurned: 1,
            disengagementEntries: 11);
        accumulator.AddTick(
            approachAgents: 1,
            engageAgents: 2,
            commitAgents: 3,
            recoverAgents: 4,
            refuseAgents: 5,
            disengageAgents: 6,
            regroupAgents: 7,
            pursueAgents: 8,
            postureTransitions: 9,
            facingStepsTurned: 10,
            disengagementEntries: 12);

        var metrics = accumulator.ToMetrics();

        Assert.Equal(11L, metrics.ApproachAgentTicks);
        Assert.Equal(11L, metrics.EngageAgentTicks);
        Assert.Equal(11L, metrics.CommitAgentTicks);
        Assert.Equal(11L, metrics.RecoverAgentTicks);
        Assert.Equal(11L, metrics.RefuseAgentTicks);
        Assert.Equal(11L, metrics.DisengageAgentTicks);
        Assert.Equal(11L, metrics.RegroupAgentTicks);
        Assert.Equal(11L, metrics.PursueAgentTicks);
        Assert.Equal(11L, metrics.PostureTransitions);
        Assert.Equal(11L, metrics.FacingStepsTurned);
        Assert.Equal(23L, metrics.DisengagementEntries);
    }

    [Theory]
    [InlineData(0, "approachAgents")]
    [InlineData(1, "engageAgents")]
    [InlineData(2, "commitAgents")]
    [InlineData(3, "recoverAgents")]
    [InlineData(4, "refuseAgents")]
    [InlineData(5, "disengageAgents")]
    [InlineData(6, "regroupAgents")]
    [InlineData(7, "pursueAgents")]
    [InlineData(8, "postureTransitions")]
    [InlineData(9, "facingStepsTurned")]
    [InlineData(10, "disengagementEntries")]
    public void AddTick_RejectsANegativeArgument(int negativeIndex, string parameterName)
    {
        var values = new int[11];
        values[negativeIndex] = -1;
        var accumulator = default(MovementBehaviorMetricsAccumulator);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => accumulator.AddTick(
                values[0],
                values[1],
                values[2],
                values[3],
                values[4],
                values[5],
                values[6],
                values[7],
                values[8],
                values[9],
                values[10]));

        Assert.Equal(parameterName, exception.ParamName);
    }

    [Fact]
    public void AddTick_LeavesTheAccumulatorUnchangedWhenAnArgumentIsRejected()
    {
        var accumulator = default(MovementBehaviorMetricsAccumulator);
        accumulator.AddTick(3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3);
        var before = accumulator.ToMetrics();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => accumulator.AddTick(1, 1, 1, 1, 1, 1, 1, 1, 1, 1, -1));

        Assert.Equal(before, accumulator.ToMetrics());
    }

    [Fact]
    public void RecordConflictDenialTotal_RejectsANegativeTotal()
    {
        var accumulator = default(MovementBehaviorMetricsAccumulator);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => accumulator.RecordConflictDenialTotal(-1));

        Assert.Equal("totalDenials", exception.ParamName);
    }

    [Fact]
    public void RecordConflictDenialTotal_RecordsTheLatestCumulativeTotal()
    {
        var accumulator = default(MovementBehaviorMetricsAccumulator);

        accumulator.RecordConflictDenialTotal(3);
        accumulator.RecordConflictDenialTotal(7);

        Assert.Equal(7L, accumulator.ToMetrics().ConflictDenials);
    }

    [Fact]
    public void RecordConflictDenialTotal_DoesNotDisturbTheRunningTotals()
    {
        var accumulator = default(MovementBehaviorMetricsAccumulator);
        accumulator.AddTick(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11);

        accumulator.RecordConflictDenialTotal(13);

        var metrics = accumulator.ToMetrics();

        Assert.Equal(1L, metrics.ApproachAgentTicks);
        Assert.Equal(2L, metrics.EngageAgentTicks);
        Assert.Equal(3L, metrics.CommitAgentTicks);
        Assert.Equal(4L, metrics.RecoverAgentTicks);
        Assert.Equal(5L, metrics.RefuseAgentTicks);
        Assert.Equal(6L, metrics.DisengageAgentTicks);
        Assert.Equal(7L, metrics.RegroupAgentTicks);
        Assert.Equal(8L, metrics.PursueAgentTicks);
        Assert.Equal(9L, metrics.PostureTransitions);
        Assert.Equal(10L, metrics.FacingStepsTurned);
        Assert.Equal(11L, metrics.DisengagementEntries);
        Assert.Equal(13L, metrics.ConflictDenials);
    }

    [Fact]
    public void RecordRouteRefusalReasonTotals_RejectsANegativeArgument()
    {
        var accumulator = default(MovementBehaviorMetricsAccumulator);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => accumulator.RecordRouteRefusalReasonTotals(-1, 0, 0, 0));

        Assert.Equal("noCandidatesBuiltTotal", exception.ParamName);
    }

    [Fact]
    public void RecordRouteRefusalReasonTotals_RecordsTheLatestCumulativeTotals()
    {
        var accumulator = default(MovementBehaviorMetricsAccumulator);

        accumulator.RecordRouteRefusalReasonTotals(1, 2, 3, 4);
        accumulator.RecordRouteRefusalReasonTotals(5, 6, 7, 8);

        var metrics = accumulator.ToMetrics();

        Assert.Equal(5L, metrics.RouteRefusalNoCandidatesBuilt);
        Assert.Equal(6L, metrics.RouteRefusalStepEndpointRejected);
        Assert.Equal(7L, metrics.RouteRefusalDirectCandidateOmitted);
        Assert.Equal(8L, metrics.RouteRefusalLaneNotClear);
    }

    [Fact]
    public void RecordRouteRefusalReasonTotals_DoesNotDisturbTheRunningTotals()
    {
        var accumulator = default(MovementBehaviorMetricsAccumulator);
        accumulator.AddTick(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11);
        accumulator.RecordConflictDenialTotal(13);

        accumulator.RecordRouteRefusalReasonTotals(1, 2, 3, 4);

        var metrics = accumulator.ToMetrics();

        Assert.Equal(5L, metrics.RefuseAgentTicks);
        Assert.Equal(13L, metrics.ConflictDenials);
        Assert.Equal(1L, metrics.RouteRefusalNoCandidatesBuilt);
        Assert.Equal(2L, metrics.RouteRefusalStepEndpointRejected);
        Assert.Equal(3L, metrics.RouteRefusalDirectCandidateOmitted);
        Assert.Equal(4L, metrics.RouteRefusalLaneNotClear);
    }

    [Fact]
    public void Reset_RestoresTheStateOfAFreshlyConstructedAccumulator()
    {
        var accumulator = default(MovementBehaviorMetricsAccumulator);
        accumulator.AddTick(11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1);
        accumulator.RecordConflictDenialTotal(12);

        accumulator.Reset();

        Assert.Equal(
            default(MovementBehaviorMetricsAccumulator).ToMetrics(),
            accumulator.ToMetrics());
    }

    [Fact]
    public void Reset_AllowsAccumulationToStartAgainFromZero()
    {
        var accumulator = default(MovementBehaviorMetricsAccumulator);
        accumulator.AddTick(11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1);
        accumulator.RecordConflictDenialTotal(12);

        accumulator.Reset();
        accumulator.AddTick(1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1);

        var metrics = accumulator.ToMetrics();

        Assert.Equal(1L, metrics.ApproachAgentTicks);
        Assert.Equal(1L, metrics.DisengagementEntries);
        Assert.Equal(0L, metrics.ConflictDenials);
    }

    [Fact]
    public void AddTick_AccumulatesBeyondIntegerRangeAcrossManyTicks()
    {
        var accumulator = default(MovementBehaviorMetricsAccumulator);
        const int TickCount = 10_000;

        for (var tick = 0; tick < TickCount; tick++)
        {
            accumulator.AddTick(
                approachAgents: int.MaxValue,
                engageAgents: int.MaxValue,
                commitAgents: int.MaxValue,
                recoverAgents: int.MaxValue,
                refuseAgents: int.MaxValue,
                disengageAgents: int.MaxValue,
                regroupAgents: int.MaxValue,
                pursueAgents: int.MaxValue,
                postureTransitions: int.MaxValue,
                facingStepsTurned: int.MaxValue,
                disengagementEntries: int.MaxValue);
        }

        var metrics = accumulator.ToMetrics();
        var expected = (long)int.MaxValue * TickCount;

        Assert.True(expected > int.MaxValue);
        Assert.Equal(expected, metrics.ApproachAgentTicks);
        Assert.Equal(expected, metrics.EngageAgentTicks);
        Assert.Equal(expected, metrics.CommitAgentTicks);
        Assert.Equal(expected, metrics.RecoverAgentTicks);
        Assert.Equal(expected, metrics.RefuseAgentTicks);
        Assert.Equal(expected, metrics.DisengageAgentTicks);
        Assert.Equal(expected, metrics.RegroupAgentTicks);
        Assert.Equal(expected, metrics.PursueAgentTicks);
        Assert.Equal(expected, metrics.PostureTransitions);
        Assert.Equal(expected, metrics.FacingStepsTurned);
        Assert.Equal(expected, metrics.DisengagementEntries);
    }

    [Fact]
    public void ToMetrics_IsRepeatableAndDoesNotConsumeTheAccumulator()
    {
        var accumulator = default(MovementBehaviorMetricsAccumulator);
        accumulator.AddTick(6, 5, 4, 3, 2, 1, 1, 2, 3, 4, 5);
        accumulator.RecordConflictDenialTotal(7);

        var first = accumulator.ToMetrics();
        var second = accumulator.ToMetrics();

        Assert.Equal(first, second);
    }

    /// <summary>
    /// The RU-06 (F-A) acceptance measurement: on the same 200-agent,
    /// seed-1, 10,000-tick <c>EquipmentRelativeFootworkV6</c> run
    /// <c>./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1
    /// -MovementPreset EquipmentRelativeFootworkV6</c> would drive, the four
    /// <see cref="BattleSimulation"/> route-refusal-reason counters sum to
    /// exactly the run's own agent-tick count of
    /// <see cref="Movement.FootworkPhase.Refuse"/>, counted independently
    /// here from each tick's <see cref="AgentView"/> the same way
    /// <c>HeadlessRunner.ObserveMovementTick</c> counts it, rather than
    /// against a hand-typed constant — a mismatch here means an exit site
    /// double-counted or missed a call, not that the recorded baseline moved.
    /// </summary>
    [Fact]
    public void BattleSimulationV6_RouteRefusalReasonsSumToTheObservedRefuseAgentTicks()
    {
        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: 200) with
        {
            TickLimit = 10_000,
            MovementPreset = MovementPresetId.EquipmentRelativeFootworkV6,
        };
        var simulation = BattleSimulation.Create(scenario);
        var observedRefuseAgentTicks = 0L;

        for (var tick = 0;
             tick < 10_000 && simulation.Outcome == BattleOutcome.Ongoing;
             tick++)
        {
            simulation.AdvanceOneTick();

            foreach (var view in simulation.Agents)
            {
                if (view.IsAlive && view.FootworkPhase == FootworkPhase.Refuse)
                {
                    observedRefuseAgentTicks++;
                }
            }
        }

        var sum = checked(
            simulation.RouteRefusalNoCandidatesBuilt +
            simulation.RouteRefusalStepEndpointRejected +
            simulation.RouteRefusalDirectCandidateOmitted +
            simulation.RouteRefusalLaneNotClear);

        Assert.Equal(observedRefuseAgentTicks, sum);
        Assert.True(observedRefuseAgentTicks > 0);
    }
}
