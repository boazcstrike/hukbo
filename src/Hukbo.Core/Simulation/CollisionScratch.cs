using Hukbo.Core.Mathematics;

namespace Hukbo.Core.Simulation;

/// <summary>
/// One tick's collision counts, handed to the headless runner so it can
/// aggregate them into a <see cref="CollisionMetrics"/>.
/// </summary>
/// <param name="CandidatePairs">
/// Living pairs the broad phase emitted this tick: every pair within the
/// inclusive contact distance, allies and enemies alike.
/// </param>
/// <param name="ContactPairs">
/// The cross-faction subset of <paramref name="CandidatePairs"/>. This is the
/// fighting front rather than incidental friendly crowding.
/// </param>
/// <param name="AcceptedMoves">
/// Movement proposals that resolved to a destination other than the agent's
/// tick-start position.
/// </param>
/// <param name="BlockedAgents">
/// Agents that resolved to <see cref="MovementResolution.Blocked"/>.
/// </param>
/// <param name="AttackCapableAgents">
/// Living agents holding a target inside attack reach at resolved positions.
/// </param>
/// <param name="FrontWidthRaw">
/// Vertical span of the agents holding at least one cross-faction contact.
/// </param>
/// <param name="FrontDepthRaw">
/// Horizontal span of the same set. Width and depth are named for the default
/// left-versus-right deployment; they are a readability signal, not geometry
/// any rule depends on.
/// </param>
/// <param name="PenetrationRaw">
/// Deepest living-body overlap measured after the commit. Must be zero under
/// <see cref="CollisionPolicy.Solid"/>; see
/// <see cref="CollisionMetrics.MaximumPenetrationRaw"/>.
/// </param>
internal readonly record struct CollisionTickMetrics(
    int CandidatePairs,
    int ContactPairs,
    int AcceptedMoves,
    int BlockedAgents,
    int AttackCapableAgents,
    int FrontWidthRaw,
    int FrontDepthRaw,
    int PenetrationRaw);

/// <summary>
/// Reusable derived storage for the collision stage.
/// </summary>
/// <remarks>
/// Everything here is a derived cache rebuilt from authoritative positions each
/// tick. None of it is hashed, snapshotted, or persisted, so none of it can
/// influence an outcome. Buffers are cleared logically rather than reallocated,
/// which is what keeps a warm collision tick inside its allocation budget.
/// </remarks>
internal sealed class CollisionScratch
{
    private readonly int[] _blockedStreakTicks;

    internal CollisionScratch(Scenario scenario, int agentCount)
    {
        Grid = new CollisionUniformGrid(checked(2 * scenario.BodyRadiusRaw));
        Resolver = new CollisionResolver(
            scenario.BodyRadiusRaw,
            checked(scenario.MapWidth * FixedPoint.Scale),
            checked(scenario.MapHeight * FixedPoint.Scale));
        Bodies = new List<CollisionBody>(agentCount);
        Requests = new List<CollisionMoveRequest>(agentCount);
        _blockedStreakTicks = new int[agentCount];
    }

    /// <summary>
    /// Broad-phase index over committed positions, used for metrics only. The
    /// resolver owns a separate grid because it indexes positions incrementally
    /// as it commits them.
    /// </summary>
    internal CollisionUniformGrid Grid { get; }

    internal CollisionResolver Resolver { get; }

    internal List<CollisionBody> Bodies { get; }

    internal List<CollisionMoveRequest> Requests { get; }

    /// <summary>
    /// Longest run of consecutive blocked ticks any single agent has reached so
    /// far in this battle.
    /// </summary>
    internal int LongestBlockedStreakTicks { get; private set; }

    /// <summary>
    /// Clears logical counts while keeping capacity, so a warm tick allocates
    /// nothing.
    /// </summary>
    internal void BeginTick()
    {
        Bodies.Clear();
        Requests.Clear();
    }

    /// <summary>
    /// Extends or ends one agent's blocked streak and keeps the running maximum.
    /// </summary>
    internal void RecordBlocked(int agentIndex, bool isBlocked)
    {
        if (!isBlocked)
        {
            _blockedStreakTicks[agentIndex] = 0;
            return;
        }

        var streak = checked(_blockedStreakTicks[agentIndex] + 1);
        _blockedStreakTicks[agentIndex] = streak;
        LongestBlockedStreakTicks = Math.Max(LongestBlockedStreakTicks, streak);
    }
}
