using Hukbo.Client.Presentation;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Rendering;

/// <summary>
/// Pure mapping from the swing store and the agent views to a per-pawn pose,
/// plus the lookup a draw loop uses to fetch one pose.
/// </summary>
/// <remarks>
/// <para>
/// This exists so the per-pawn pose resolution does not live in
/// <c>ArenaGame</c>, which is banned from tests and therefore untestable by
/// construction. The lookup is pinned here as well as the mapping, because the
/// lookup is the part that lands in the untestable file.
/// </para>
/// <para>
/// <see cref="Resolve"/> fills a destination the caller owns rather than
/// returning a fresh dictionary. It runs once a frame on the draw path, and
/// the client keeps that path free of heap allocation.
/// </para>
/// </remarks>
internal static class SwingPoseResolver
{
    /// <summary>
    /// Resolves one pose per agent with a swing in flight. An agent with no
    /// swing gets no entry rather than a neutral one, so a caller cannot
    /// confuse "standing still" with "not drawn".
    /// </summary>
    /// <param name="swings">The swing store.</param>
    /// <param name="agents">
    /// The agent views for the completed tick. A swing whose attacker has left
    /// the views resolves to no pose, because there is no pawn to put in it.
    /// </param>
    /// <param name="destination">
    /// A caller-owned buffer, cleared before it is filled and returned as the
    /// result so that the draw loop reads exactly what was written.
    /// </param>
    public static IReadOnlyDictionary<ulong, SwingPose> Resolve(
        SwingAnimationSystem swings,
        IReadOnlyList<AgentView> agents,
        Dictionary<ulong, SwingPose> destination)
    {
        ArgumentNullException.ThrowIfNull(swings);
        ArgumentNullException.ThrowIfNull(agents);
        ArgumentNullException.ThrowIfNull(destination);

        destination.Clear();

        var active = swings.ActiveSwings;
        if (active.Length == 0)
        {
            return destination;
        }

        for (var index = 0; index < agents.Count; index++)
        {
            var entityId = agents[index].EntityId;
            if (!swings.TryGetSwing(entityId, out var swing))
            {
                continue;
            }

            destination[entityId] = SwingGeometry.ResolvePose(swing);
        }

        return destination;
    }

    /// <summary>
    /// The exact lookup shape a per-pawn draw loop uses. Pinned by a test so
    /// the shipped draw loop is covered rather than a method with no caller.
    /// </summary>
    public static bool TryGetPose(
        IReadOnlyDictionary<ulong, SwingPose> poses,
        ulong entityId,
        out SwingPose pose)
    {
        ArgumentNullException.ThrowIfNull(poses);

        return poses.TryGetValue(entityId, out pose);
    }
}
