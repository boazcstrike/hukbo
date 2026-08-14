using Hukbo.Client.Settings;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Presentation;

/// <summary>
/// Owns the frame boundary between authoritative attack events and the first
/// draw that consumes their baked contact pose.
/// </summary>
internal sealed class AttackFrameCoordinator
{
    /// <summary>
    /// How many consecutive Draw frames <see cref="AcknowledgeDraw"/> lets a
    /// latch stay open while its attacker's pawn goes undrawn — a cull
    /// rejection every legitimate frame resolves within one Draw call, but a
    /// permanently off-screen corpse (a dead-and-past-hold attacker whose
    /// cull rectangle never re-enters <c>arenaBounds</c>) would otherwise
    /// hold its latch forever, which is exactly the deadlock this bound
    /// exists to prevent.
    /// </summary>
    /// <remarks>
    /// No existing latch test exercises a legitimate multi-frame hold — every
    /// one of them installs a contact and acknowledges it on the very next
    /// call, so there is no observed "longest legitimate hold" frame count to
    /// read out of this file's own tests. The nearest pinned, tested bound on
    /// how far behind one attacker's presentation is allowed to fall is
    /// <see cref="AttackContactDispatcher.MaximumPendingContactsPerAttacker"/>
    /// (<c>AttackContactDispatcherTests.Ingest_RetainsFivePerAttackerAndCoalescesTheSixthWholeBundle</c>):
    /// the same 0.5-second, 20-tick catch-up window that bounds how many
    /// contacts one attacker can back up also bounds how many consecutive
    /// undrawn frames its current latch may legitimately need before it is a
    /// stuck, permanently culled attacker rather than a catching-up one.
    /// <para>
    /// PROVISIONAL. That reasoning is an analogy, not a measurement: pending
    /// contacts and undrawn frames are different quantities that happen to
    /// share a plausible bound. The value is safe in the direction that
    /// matters — too low releases a latch early for a pawn that was about to
    /// be drawn, too high delays a stuck one — and nothing observable
    /// currently distinguishes five from a neighbouring value. Replace it with
    /// a measured figure the first time a legitimate multi-frame hold is
    /// actually observed.
    /// </para>
    /// </remarks>
    internal const int MaximumLatchFrames =
        AttackContactDispatcher.MaximumPendingContactsPerAttacker;

    private readonly Dictionary<ulong, AgentView> _agentsById;
    private readonly AttackContactBundle[] _released;
    private int _releasedCount;

    /// <summary>
    /// Fixed, reused-across-frames record of which attacker entity ids had
    /// their pawn actually reach a draw call this frame, appended to by
    /// <see cref="RecordDrawn"/> and consumed and reset by
    /// <see cref="AcknowledgeDraw"/>. Pre-sized to <c>attackerCapacity</c>,
    /// the same parameter <see cref="AttackContactDispatcher"/>'s
    /// constructor takes, because every drawn pawn in a frame is one entry
    /// and the roster can never draw more agents than that capacity.
    /// </summary>
    private readonly ulong[] _drawnAttackerIds;
    private int _drawnCount;

    public AttackFrameCoordinator(int attackerCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(attackerCapacity);

        Dispatcher = new AttackContactDispatcher(attackerCapacity);
        Animations = new AttackAnimationSystem(attackerCapacity);
        _agentsById = new Dictionary<ulong, AgentView>(attackerCapacity);
        _released = new AttackContactBundle[attackerCapacity];
        _drawnAttackerIds = new ulong[attackerCapacity];
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
    /// Records that <paramref name="entityId"/>'s pawn reached an actual
    /// draw call this frame. Called from the draw path once per pawn that
    /// survives its cull test, for both the corpse pass and the living
    /// pass — <see cref="AcknowledgeDraw"/> reads this to decide whether a
    /// latched attacker's contact was really put on screen this frame, as
    /// opposed to latched-but-cull-rejected.
    /// </summary>
    public void RecordDrawn(ulong entityId)
    {
        // The buffer is sized to attackerCapacity, the same bound the whole
        // roster is drawn against, and each agent reaches at most one of the
        // two draw passes per frame, so this can never legitimately overflow.
        // The guard is defense against a caller invariant that broke
        // elsewhere, not a path this method expects to take.
        if (_drawnCount >= _drawnAttackerIds.Length)
        {
            return;
        }

        _drawnAttackerIds[_drawnCount] = entityId;
        _drawnCount++;
    }

    /// <summary>
    /// Releases every latch whose matching pose was present in the completed
    /// pawn pass. Sequence matching prevents an old frame from consuming a
    /// newer combo contact for the same attacker.
    /// </summary>
    /// <remarks>
    /// A latch whose attacker was not drawn this frame — cull-rejected, most
    /// often a dead-and-past-hold corpse whose envelope never re-enters
    /// <c>arenaBounds</c> — survives instead of releasing, up to
    /// <see cref="MaximumLatchFrames"/> consecutive undrawn frames, after
    /// which it is forced open so a permanently off-screen attacker can never
    /// hold <see cref="HasUndrawnContacts"/> true forever.
    /// </remarks>
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

            if (!WasDrawnThisFrame(animation.AttackerEntityId) &&
                Animations.IncrementUndrawnFrames(
                    animation.AttackerEntityId,
                    animation.Sequence) < MaximumLatchFrames)
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

        _drawnCount = 0;
        return acknowledged;
    }

    private bool WasDrawnThisFrame(ulong entityId)
    {
        for (var index = 0; index < _drawnCount; index++)
        {
            if (_drawnAttackerIds[index] == entityId)
            {
                return true;
            }
        }

        return false;
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
        Array.Clear(_drawnAttackerIds, 0, _drawnCount);
        _drawnCount = 0;
    }
}
