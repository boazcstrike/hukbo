using Hukbo.Core.Simulation;

namespace Hukbo.Client.Rendering;

/// <summary>
/// Pure mapping from the agent views to a per-pawn ranged pose, plus the
/// lookups a draw loop uses to fetch one pose and to decide whether that pose
/// suppresses the swing pose for the same pawn on the same frame.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="SwingPoseResolver"/> and <see cref="GaitPoseResolver"/>,
/// this resolver needs no animation store. <see cref="AgentView.RangedPhase"/>
/// and <see cref="AgentView.RangedPhaseTicksRemaining"/> arrive on the view
/// every tick, already derived by <see cref="RangedPhaseProjection.Derive"/>,
/// so there is no clock, no capacity, and no eviction to own here — section
/// 8.3 of the ranged-units design document calls this "a genuine
/// simplification over both existing systems".
/// </para>
/// <para>
/// <see cref="Resolve"/> fills a destination the caller owns rather than
/// returning a fresh dictionary, and copies <see cref="SwingPoseResolver"/>'s
/// early-out when nothing is active rather than
/// <see cref="GaitPoseResolver"/>'s omission of it: most battles under most
/// rosters have no ranged warriors at all, and design section 8.3 measures the
/// omitted check at 250,000 comparisons per frame at 500 agents.
/// </para>
/// </remarks>
internal static class RangedPoseResolver
{
    /// <summary>
    /// Resolves one pose per agent with a ranged phase in progress. An agent
    /// whose <see cref="AgentView.RangedPhase"/> is
    /// <see cref="RangedPhase.None"/> — including every agent carrying a
    /// melee weapon — gets no entry rather than a neutral one, so a caller
    /// cannot confuse "standing still" with "not drawn".
    /// </summary>
    /// <param name="agents">The agent views for the completed tick.</param>
    /// <param name="destination">
    /// A caller-owned buffer, cleared before it is filled and returned as the
    /// result so that the draw loop reads exactly what was written.
    /// </param>
    public static IReadOnlyDictionary<ulong, RangedPose> Resolve(
        IReadOnlyList<AgentView> agents,
        Dictionary<ulong, RangedPose> destination)
    {
        ArgumentNullException.ThrowIfNull(agents);
        ArgumentNullException.ThrowIfNull(destination);

        destination.Clear();

        if (!AnyAgentHasARangedPhase(agents))
        {
            return destination;
        }

        for (var index = 0; index < agents.Count; index++)
        {
            var agent = agents[index];
            if (agent.RangedPhase == RangedPhase.None)
            {
                continue;
            }

            destination[agent.EntityId] = RangedGeometry.ResolvePose(
                agent.Loadout.Weapon,
                agent.RangedPhase,
                agent.RangedPhaseTicksRemaining);
        }

        return destination;
    }

    /// <summary>
    /// The exact lookup shape a per-pawn draw loop uses. Pinned by a test so
    /// the shipped draw loop is covered rather than a method with no caller.
    /// </summary>
    public static bool TryGetPose(
        IReadOnlyDictionary<ulong, RangedPose> poses,
        ulong entityId,
        out RangedPose pose)
    {
        ArgumentNullException.ThrowIfNull(poses);

        return poses.TryGetValue(entityId, out pose);
    }

    /// <summary>
    /// Whether a ranged pose suppresses the swing pose for the same pawn on
    /// the same frame — true exactly when <paramref name="rangedPoses"/>
    /// carries an entry for <paramref name="entityId"/>. Both poses write the
    /// same weapon-line rotation and reach channels into the one weapon line a
    /// pawn has, per design section 8.3, so the two must never both apply.
    /// </summary>
    public static bool SuppressesSwing(
        IReadOnlyDictionary<ulong, RangedPose> rangedPoses,
        ulong entityId)
    {
        ArgumentNullException.ThrowIfNull(rangedPoses);

        return rangedPoses.ContainsKey(entityId);
    }

    private static bool AnyAgentHasARangedPhase(IReadOnlyList<AgentView> agents)
    {
        for (var index = 0; index < agents.Count; index++)
        {
            if (agents[index].RangedPhase != RangedPhase.None)
            {
                return true;
            }
        }

        return false;
    }
}
