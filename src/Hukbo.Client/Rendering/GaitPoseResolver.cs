using Hukbo.Client.Presentation;
using Hukbo.Client.Settings;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Rendering;

/// <summary>
/// Pure mapping from the gait store and the agent views to a per-pawn pose,
/// plus the lookup a draw loop uses to fetch one pose. Mirrors
/// <see cref="AttackPoseResolver"/> exactly, with the spectator's
/// <see cref="MotionIntensity"/> setting folded in as an extra argument, since
/// unlike a swing's pose a gait pose is not meaningful without knowing whether
/// ambient motion is currently suppressed.
/// </summary>
/// <remarks>
/// <see cref="Resolve"/> fills a destination the caller owns rather than
/// returning a fresh dictionary, and runs once a frame on the draw path with
/// no heap allocation of its own, matching <see cref="AttackPoseResolver"/>'s
/// rule.
/// </remarks>
internal static class GaitPoseResolver
{
    /// <summary>
    /// Resolves one pose per living agent the gait store already holds an
    /// entry for. An agent the store has not yet ingested — which happens
    /// only before the store's first <c>Ingest</c> call — gets no entry
    /// rather than a neutral one, so a caller cannot confuse "standing still"
    /// with "not yet tracked".
    /// </summary>
    /// <param name="gait">The gait store.</param>
    /// <param name="agents">
    /// The agent views for the completed tick. Used only to enumerate which
    /// entities a pose is wanted for; the pose itself is built from the
    /// store's own entry, not from these views directly.
    /// </param>
    /// <param name="motionIntensity">The spectator's ambient-motion setting.</param>
    /// <param name="destination">
    /// A caller-owned buffer, cleared before it is filled and returned as the
    /// result so that the draw loop reads exactly what was written.
    /// </param>
    public static IReadOnlyDictionary<ulong, GaitPose> Resolve(
        GaitAnimationSystem gait,
        IReadOnlyList<AgentView> agents,
        MotionIntensity motionIntensity,
        Dictionary<ulong, GaitPose> destination)
    {
        ArgumentNullException.ThrowIfNull(gait);
        ArgumentNullException.ThrowIfNull(agents);
        ArgumentNullException.ThrowIfNull(destination);

        if (!Enum.IsDefined(motionIntensity))
        {
            throw new ArgumentOutOfRangeException(
                nameof(motionIntensity),
                motionIntensity,
                "Unknown motion intensity.");
        }

        destination.Clear();

        for (var index = 0; index < agents.Count; index++)
        {
            var entityId = agents[index].EntityId;
            if (!gait.TryGetEntry(entityId, out var entry))
            {
                continue;
            }

            destination[entityId] = GaitGeometry.ResolvePose(
                entry.Mode,
                entry.PhaseTurns,
                entry.DirectionSign,
                motionIntensity);
        }

        return destination;
    }

    /// <summary>
    /// The exact lookup shape a per-pawn draw loop uses. Pinned by a test so
    /// the shipped draw loop is covered rather than a method with no caller.
    /// </summary>
    public static bool TryGetPose(
        IReadOnlyDictionary<ulong, GaitPose> poses,
        ulong entityId,
        out GaitPose pose)
    {
        ArgumentNullException.ThrowIfNull(poses);

        return poses.TryGetValue(entityId, out pose);
    }
}
