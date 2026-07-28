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
    /// <summary>
    /// The per-faction split must be a partition of the undivided total on
    /// every tick, not merely on average or at the end. Every accepted attack
    /// has exactly one attacker and therefore exactly one attacking faction, so
    /// any drift means a resolution was credited twice or not at all.
    /// </summary>
    /// <remarks>
    /// This runs a real battle rather than a synthetic pair so the assertion
    /// covers ticks where one faction attacks and the other cannot, where both
    /// attack, and where neither does. A test over hand-built counters would
    /// pass while the simulation credited the wrong faction.
    /// </remarks>
    [Fact]
    public void PerFactionAttackCountsPartitionTheUndividedTotalOnEveryTick()
    {
        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: 40);
        var simulation = BattleSimulation.Create(scenario);

        var sawAnyAttack = false;

        for (var tick = 0; tick < 400; tick++)
        {
            simulation.AdvanceOneTick();

            var split = simulation.LastTickCombatByFaction;
            Assert.Equal(simulation.LastTickCombat, split.Total);
            Assert.Equal(split.Faction0, split.ForFaction(0));
            Assert.Equal(split.Faction1, split.ForFaction(1));

            // Each side's own five outcome counters must also be exhaustive,
            // which is the property that makes a per-faction
            // DefenceAttributableShare meaningful.
            foreach (var side in (CombatMetrics[])[split.Faction0, split.Faction1])
            {
                Assert.Equal(
                    side.AcceptedAttacks,
                    side.LandedAttacks +
                        side.ShieldBlockedAttacks +
                        side.ParriedAttacks +
                        side.DeflectedAttacks +
                        side.EvadedAttacks);
            }

            if (simulation.LastTickCombat.AcceptedAttacks > 0)
            {
                sawAnyAttack = true;
            }
        }

        // Guards against the assertions above passing vacuously on a run where
        // nothing ever attacked.
        Assert.True(sawAnyAttack, "No attack was resolved, so nothing was verified.");
    }

    [Fact]
    public void ForFactionRejectsAFactionThatDoesNotExist()
    {
        var split = new FactionCombatMetrics(default, default);

        Assert.Throws<ArgumentOutOfRangeException>(() => split.ForFaction(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => split.ForFaction(-1));
    }

}
