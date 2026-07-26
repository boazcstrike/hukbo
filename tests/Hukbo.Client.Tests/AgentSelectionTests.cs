using Hukbo.Client.Presentation;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Tests;

public sealed class AgentSelectionTests
{
    [Fact]
    public void SelectNearest_SelectsOnlyCandidateWithinRadius()
    {
        var selection = new AgentSelection();
        AgentView[] agents =
        [
            CreateAgent(1, xRaw: 3, yRaw: 4),
            CreateAgent(2, xRaw: 6, yRaw: 0),
        ];

        selection.SelectNearest(agents, 0, 0, maximumDistanceSquared: 25);

        Assert.Equal(1UL, selection.SelectedEntityId);
    }

    [Fact]
    public void SelectNearest_SelectsClosestCandidate()
    {
        var selection = new AgentSelection();
        AgentView[] agents =
        [
            CreateAgent(2, xRaw: 4, yRaw: 0),
            CreateAgent(1, xRaw: 2, yRaw: 0),
        ];

        selection.SelectNearest(agents, 0, 0, maximumDistanceSquared: 25);

        Assert.Equal(1UL, selection.SelectedEntityId);
    }

    [Fact]
    public void SelectNearest_UsesEntityIdAsDistanceTieBreaker()
    {
        var selection = new AgentSelection();
        AgentView[] agents =
        [
            CreateAgent(8, xRaw: -3, yRaw: 4),
            CreateAgent(3, xRaw: 3, yRaw: 4),
        ];

        selection.SelectNearest(agents, 0, 0, maximumDistanceSquared: 25);

        Assert.Equal(3UL, selection.SelectedEntityId);
    }

    [Fact]
    public void SelectNearest_IgnoresDeadCandidates()
    {
        var selection = new AgentSelection();
        AgentView[] agents =
        [
            CreateAgent(1, xRaw: 0, yRaw: 0, isAlive: false),
            CreateAgent(2, xRaw: 2, yRaw: 0),
        ];

        selection.SelectNearest(agents, 0, 0, maximumDistanceSquared: 4);

        Assert.Equal(2UL, selection.SelectedEntityId);
    }

    [Fact]
    public void SelectNearest_ClearsSelectionForEmptyClick()
    {
        var selection = new AgentSelection();
        AgentView[] agents = [CreateAgent(1, xRaw: 0, yRaw: 0)];
        selection.SelectNearest(agents, 0, 0, maximumDistanceSquared: 1);

        selection.SelectNearest(agents, 10, 10, maximumDistanceSquared: 1);

        Assert.Null(selection.SelectedEntityId);
    }

    [Fact]
    public void Resolve_ReturnsSelectedAgentAfterDeath()
    {
        var selection = new AgentSelection();
        AgentView[] livingAgents = [CreateAgent(1, xRaw: 0, yRaw: 0)];
        selection.SelectNearest(livingAgents, 0, 0, maximumDistanceSquared: 0);
        AgentView[] finalAgents =
        [
            CreateAgent(1, xRaw: 0, yRaw: 0, isAlive: false),
        ];

        var resolved = selection.Resolve(finalAgents);

        Assert.NotNull(resolved);
        Assert.False(resolved.Value.IsAlive);
    }

    [Fact]
    public void Clear_RemovesSelection()
    {
        var selection = new AgentSelection();
        AgentView[] agents = [CreateAgent(1, xRaw: 0, yRaw: 0)];
        selection.SelectNearest(agents, 0, 0, maximumDistanceSquared: 0);

        selection.Clear();
        selection.Clear();

        Assert.Null(selection.SelectedEntityId);
        Assert.Null(selection.Resolve(agents));
    }

    private static AgentView CreateAgent(
        ulong entityId,
        int xRaw,
        int yRaw,
        bool isAlive = true) =>
        new(
            entityId,
            FactionId: 0,
            xRaw,
            yRaw,
            HitPoints: isAlive ? 100 : 0,
            MaximumHitPoints: 100,
            TargetEntityId: null,
            Intent: isAlive ? AgentIntent.Idle : AgentIntent.Dead,
            isAlive);
}
