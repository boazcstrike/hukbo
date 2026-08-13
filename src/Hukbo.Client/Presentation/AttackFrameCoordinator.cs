using Hukbo.Client.Settings;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Presentation;

/// <summary>
/// Owns the frame boundary between authoritative attack events and the first
/// draw that consumes their baked contact pose.
/// </summary>
internal sealed class AttackFrameCoordinator
{
    private readonly Dictionary<ulong, AgentView> _agentsById;
    private readonly AttackContactBundle[] _released;
    private int _releasedCount;

    public AttackFrameCoordinator(int attackerCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(attackerCapacity);

        Dispatcher = new AttackContactDispatcher(attackerCapacity);
        Animations = new AttackAnimationSystem(attackerCapacity);
        _agentsById = new Dictionary<ulong, AgentView>(attackerCapacity);
        _released = new AttackContactBundle[attackerCapacity];
    }

    public AttackContactDispatcher Dispatcher { get; }

    public AttackAnimationSystem Animations { get; }

    public ReadOnlySpan<AttackContactBundle> ReleasedThisFrame =>
        _released.AsSpan(0, _releasedCount);

    public bool HasUndrawnContacts =>
        Dispatcher.PendingCount > 0 || Dispatcher.LatchedCount > 0;

    public void Ingest(IReadOnlyList<BattleEvent> events) =>
        Dispatcher.Ingest(events);

    /// <summary>
    /// Latches at most one contact per attacker and installs every selected
    /// contact at age zero. The latch survives until <see cref="AcknowledgeDraw"/>.
    /// </summary>
    public ReadOnlySpan<AttackContactBundle> ReleaseForDraw(
        IReadOnlyList<AgentView> agents,
        MotionIntensity motionIntensity,
        bool allowRelease)
    {
        ArgumentNullException.ThrowIfNull(agents);
        if (!Enum.IsDefined(motionIntensity))
        {
            throw new ArgumentOutOfRangeException(
                nameof(motionIntensity),
                motionIntensity,
                null);
        }

        _releasedCount = 0;
        if (!allowRelease || Dispatcher.PendingCount == 0)
        {
            return ReleasedThisFrame;
        }

        _agentsById.Clear();
        for (var index = 0; index < agents.Count; index++)
        {
            var agent = agents[index];
            _agentsById[agent.EntityId] = agent;
        }

        while (Dispatcher.TryLatchNext(out var contact))
        {
            if (!_agentsById.TryGetValue(contact.AttackerEntityId, out var attacker) ||
                !_agentsById.TryGetValue(contact.DefenderEntityId, out var defender))
            {
                throw new InvalidOperationException(
                    $"Attack contact {contact.Sequence} references an agent outside the current roster.");
            }

            var directionX = (float)defender.XRaw - attacker.XRaw;
            var directionY = (float)defender.YRaw - attacker.YRaw;
            var length = MathF.Sqrt(
                (directionX * directionX) + (directionY * directionY));
            if (length > 0f)
            {
                directionX /= length;
                directionY /= length;
            }
            else
            {
                directionX = 1f;
                directionY = 0f;
            }

            Animations.Ingest(
                contact,
                directionX,
                directionY,
                motionIntensity);
            _released[_releasedCount] = contact;
            _releasedCount++;
        }

        return ReleasedThisFrame;
    }

    public bool TryGetAgent(ulong entityId, out AgentView agent) =>
        _agentsById.TryGetValue(entityId, out agent);

    /// <summary>
    /// Releases every latch whose matching pose was present in the completed
    /// pawn pass. Sequence matching prevents an old frame from consuming a
    /// newer combo contact for the same attacker.
    /// </summary>
    public int AcknowledgeDraw()
    {
        var acknowledged = 0;
        var active = Animations.ActiveAnimations;
        for (var index = 0; index < active.Length; index++)
        {
            var animation = active[index];
            if (!animation.AwaitingDrawAcknowledgement ||
                !Dispatcher.TryGetLatched(
                    animation.AttackerEntityId,
                    out var contact) ||
                contact.Sequence != animation.Sequence)
            {
                continue;
            }

            if (!Animations.AcknowledgeDraw(
                    animation.AttackerEntityId,
                    animation.Sequence) ||
                !Dispatcher.AcknowledgeLatched(
                    animation.AttackerEntityId,
                    animation.Sequence))
            {
                throw new InvalidOperationException(
                    $"Attack contact {animation.Sequence} could not be acknowledged atomically.");
            }

            acknowledged++;
        }

        return acknowledged;
    }

    public void Advance(
        float elapsedSeconds,
        float speedMultiplier,
        bool advanceContacts)
    {
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        }

        if (!float.IsFinite(speedMultiplier) || speedMultiplier <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(speedMultiplier));
        }

        if (advanceContacts)
        {
            Animations.Advance(elapsedSeconds, speedMultiplier);
        }
    }

    public void Clear()
    {
        Dispatcher.Clear();
        Animations.Clear();
        _agentsById.Clear();
        Array.Clear(_released, 0, _releasedCount);
        _releasedCount = 0;
    }
}
