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
/// <b>No-op stub.</b> It resolves no poses yet.
/// </para>
/// </remarks>
internal static class SwingPoseResolver
{
    private static readonly Dictionary<ulong, SwingPose> EmptyPoses = [];

    /// <summary>
    /// Resolves one pose per agent with a swing in flight. An agent with no
    /// swing gets no entry rather than a neutral one, so a caller cannot
    /// confuse "standing still" with "not drawn".
    /// </summary>
    public static IReadOnlyDictionary<ulong, SwingPose> Resolve(
        SwingAnimationSystem swings,
        IReadOnlyList<AgentView> agents)
    {
        ArgumentNullException.ThrowIfNull(swings);
        ArgumentNullException.ThrowIfNull(agents);

        return EmptyPoses;
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
