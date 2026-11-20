using Hukbo.Core.Simulation;

namespace Hukbo.Client.Presentation;

internal sealed class AgentSelection
{
    public ulong? SelectedEntityId { get; private set; }

    /// <param name="drawnPositions">
    /// When given, each pawn is measured at the position it is drawn at
    /// (<see cref="RenderPositionInterpolator.Resolve"/> with
    /// <paramref name="alpha"/>) so a click lands on the pawn the spectator
    /// sees; when <c>null</c> it is measured at its authoritative tick
    /// position. It must hold the same agents, in the same order, as
    /// <paramref name="agents"/>.
    /// </param>
    public void SelectNearest(
        IReadOnlyList<AgentView> agents,
        int pointerXRaw,
        int pointerYRaw,
        long maximumDistanceSquared,
        RenderPositionInterpolator? drawnPositions = null,
        double alpha = 1d)
    {
        ArgumentNullException.ThrowIfNull(agents);

        ulong? closestEntityId = null;
        var closestDistanceSquared = long.MaxValue;

        for (var index = 0; index < agents.Count; index++)
        {
            var agent = agents[index];
            if (!agent.IsAlive)
            {
                continue;
            }

            var (xRaw, yRaw) = drawnPositions is null
                ? (agent.XRaw, agent.YRaw)
                : drawnPositions.Resolve(index, alpha);
            var deltaX = checked((long)xRaw - pointerXRaw);
            var deltaY = checked((long)yRaw - pointerYRaw);
            var distanceSquared = checked((deltaX * deltaX) + (deltaY * deltaY));
            if (distanceSquared > maximumDistanceSquared)
            {
                continue;
            }

            if (distanceSquared < closestDistanceSquared ||
                (distanceSquared == closestDistanceSquared &&
                    (!closestEntityId.HasValue ||
                        agent.EntityId < closestEntityId.Value)))
            {
                closestEntityId = agent.EntityId;
                closestDistanceSquared = distanceSquared;
            }
        }

        SelectedEntityId = closestEntityId;
    }

    public AgentView? Resolve(IReadOnlyList<AgentView> agents)
    {
        ArgumentNullException.ThrowIfNull(agents);

        if (!SelectedEntityId.HasValue)
        {
            return null;
        }

        foreach (var agent in agents)
        {
            if (agent.EntityId == SelectedEntityId.Value)
            {
                return agent;
            }
        }

        return null;
    }

    public void Clear() => SelectedEntityId = null;
}
