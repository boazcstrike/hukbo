using Hukbo.Core.Simulation;
using Hukbo.Headless;

namespace Hukbo.Core.Tests;

public sealed class CombatMetricsTests
{
    [Fact]
    public void BothConsumersExposeCombatMetricsAndReportAnEmptyStub()
    {
        // The simulation and the run report are the two members later tasks
        // dereference. Reading both here is what keeps this assembly compiling
        // while accumulation is still a stub; without it a whole phase of
        // tests would fail to build and take every other case down with them.
        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: 2);
        var simulation = BattleSimulation.Create(scenario);
        simulation.AdvanceOneTick();

        Assert.Equal(default, simulation.LastTickCombat);
        Assert.Equal(default, CreateReportWithoutCombatMetrics().CombatMetrics);
    }

    /// <summary>
    /// Builds a run report the way the headless runner still builds one, with
    /// no combat metrics argument. It compiles only while the new report
    /// parameter stays defaulted, which is what keeps the runner untouched
    /// until accumulation lands.
    /// </summary>
    private static RunReport CreateReportWithoutCombatMetrics() =>
        new(
            new RunEnvironment("os", "framework", "architecture", 1),
            Seed: 1,
            AgentCount: 2,
            RequestedTicks: 1,
            MeasuredTicks: 1,
            DurationMilliseconds: 0,
            new TickPercentiles(0, 0, 0, 0),
            AllocatedBytes: 0,
            Outcome: nameof(BattleOutcome.Ongoing),
            Faction0Survivors: 1,
            Faction1Survivors: 1,
            EventHash: "0000000000000000",
            StateHash: "0000000000000000",
            Deterministic: true,
            FirstMismatchTick: null,
            default(CollisionMetrics));

    [Fact]
    public void Ratio_IsZeroWhenNoAttackWasAccepted()
    {
        // Exactly zero, not the band centre and not any value inside the
        // acceptance band. The criterion-one band test reads this value, so a
        // convenient nonzero fallback would let it pass on a run that counted
        // nothing and would leave it unable to fail if accumulation regressed.
        Assert.Equal(0d, default(CombatMetrics).DefenceAttributableShare);
        Assert.Equal(0d, new CombatMetricsAccumulator().ToMetrics().DefenceAttributableShare);
    }

    [Fact]
    public void Ratio_CountsEveryNonLandedResolutionAgainstAcceptedAttacks()
    {
        var metrics = new CombatMetrics(
            AcceptedAttacks: 100,
            LandedAttacks: 60,
            ShieldBlockedAttacks: 20,
            ParriedAttacks: 5,
            DeflectedAttacks: 10,
            EvadedAttacks: 5);

        Assert.Equal(0.40d, metrics.DefenceAttributableShare);
    }

    [Fact]
    public void Accumulator_RejectsNegativeCountsAndResetsToZero()
    {
        var accumulator = new CombatMetricsAccumulator();
        accumulator.AddTick(
            acceptedAttacks: 7,
            landed: 3,
            shieldBlocked: 2,
            parried: 1,
            deflected: 1,
            evaded: 0);
        accumulator.AddTick(
            acceptedAttacks: 3,
            landed: 1,
            shieldBlocked: 0,
            parried: 0,
            deflected: 1,
            evaded: 1);

        Assert.Equal(
            new CombatMetrics(
                AcceptedAttacks: 10,
                LandedAttacks: 4,
                ShieldBlockedAttacks: 2,
                ParriedAttacks: 1,
                DeflectedAttacks: 2,
                EvadedAttacks: 1),
            accumulator.ToMetrics());

        AssertRejectsNegative(
            (ref CombatMetricsAccumulator target) =>
                target.AddTick(-1, 0, 0, 0, 0, 0));
        AssertRejectsNegative(
            (ref CombatMetricsAccumulator target) =>
                target.AddTick(0, -1, 0, 0, 0, 0));
        AssertRejectsNegative(
            (ref CombatMetricsAccumulator target) =>
                target.AddTick(0, 0, -1, 0, 0, 0));
        AssertRejectsNegative(
            (ref CombatMetricsAccumulator target) =>
                target.AddTick(0, 0, 0, -1, 0, 0));
        AssertRejectsNegative(
            (ref CombatMetricsAccumulator target) =>
                target.AddTick(0, 0, 0, 0, -1, 0));
        AssertRejectsNegative(
            (ref CombatMetricsAccumulator target) =>
                target.AddTick(0, 0, 0, 0, 0, -1));

        accumulator.Reset();

        Assert.Equal(default, accumulator.ToMetrics());
    }

    private delegate void AccumulatorAction(ref CombatMetricsAccumulator accumulator);

    /// <summary>
    /// Asserts the call is rejected <em>and</em> that the accumulator it was
    /// offered is left untouched, so a partially applied tick cannot slip past
    /// a passing exception assertion.
    /// </summary>
    private static void AssertRejectsNegative(AccumulatorAction action)
    {
        var accumulator = new CombatMetricsAccumulator();
        accumulator.AddTick(
            acceptedAttacks: 2,
            landed: 1,
            shieldBlocked: 1,
            parried: 0,
            deflected: 0,
            evaded: 0);
        var before = accumulator.ToMetrics();

        try
        {
            action(ref accumulator);
            Assert.Fail("Expected a negative count to be rejected.");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected.
        }

        Assert.Equal(before, accumulator.ToMetrics());
    }
}
