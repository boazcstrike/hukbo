using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests;

public sealed class DeterminismTests
{
    [Fact]
    public void IndependentSameSeedRunsProduceIdenticalEventsAndStateHashes()
    {
        var scenario = Scenario.CreateDefault(seed: 0xDEADBEEF, totalAgents: 200);
        var left = BattleSimulation.Create(scenario);
        var right = BattleSimulation.Create(scenario);

        for (var tick = 0; tick < 2_000 && left.Outcome == BattleOutcome.Ongoing; tick++)
        {
            left.AdvanceOneTick();
            right.AdvanceOneTick();

            Assert.Equal(left.Tick, right.Tick);
            Assert.Equal(left.Outcome, right.Outcome);
            Assert.Equal(left.LastEvents, right.LastEvents);
            Assert.Equal(left.ComputeStateHash(), right.ComputeStateHash());
        }

        Assert.NotEqual(0UL, left.ComputeStateHash());
        Assert.NotEqual(BattleOutcome.Ongoing, left.Outcome);
    }

    [Fact]
    public void SnapshotIsAnImmutableCopyOfTheCompletedTick()
    {
        var simulation = BattleSimulation.Create(
            Scenario.CreateDefault(seed: 7, totalAgents: 20));
        simulation.AdvanceOneTick();

        var snapshot = simulation.CreateSnapshot();
        var firstAgent = snapshot.Agents[0];

        simulation.AdvanceOneTick();

        Assert.Equal(1, snapshot.Tick);
        Assert.Equal(firstAgent, snapshot.Agents[0]);
        Assert.NotEqual(snapshot.StateHash, simulation.ComputeStateHash());
    }
}
