using Hukbo.Core.Simulation;

namespace Hukbo.Client.Presentation;

internal sealed class AgentSelection
{
    public ulong? SelectedEntityId { get; private set; }

    public void SelectNearest(
        IReadOnlyList<AgentView> agents,
        int pointerXRaw,
        int pointerYRaw,
        long maximumDistanceSquared)
    {
        ArgumentNullException.ThrowIfNull(agents);

        ulong? closestEntityId = null;
        var closestDistanceSquared = long.MaxValue;

        foreach (var agent in agents)
        {
            if (!agent.IsAlive)
            {
                continue;
            }

            var deltaX = checked((long)agent.XRaw - pointerXRaw);
            var deltaY = checked((long)agent.YRaw - pointerYRaw);
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
