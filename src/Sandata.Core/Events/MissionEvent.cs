using Sandata.Core.Orders;
using Sandata.Core.Weapons;

namespace Sandata.Core.Events;

/// <summary>
/// One authoritative Sandata mission event — the event-hash analogue of
/// <c>Hukbo.Core.Simulation.BattleEvent</c>, sized to exactly what design
/// section 16 currently asks for: a rejected order's id and reason code.
/// Construct instances only through <see cref="OrderRejected"/>, which
/// validates the combination; a later task adds a factory per new
/// <see cref="MissionEventKind"/> member rather than widening this one.
/// </summary>
/// <remarks>
/// Record structs always expose an implicit public parameterless
/// constructor in addition to any declared constructor, so
/// <c>default(MissionEvent)</c> bypasses this validation. That default
/// value is never a valid authoritative event and must not be produced by
/// simulation code — see <c>Hukbo.Core.Simulation.BattleEvent</c>'s
/// identical remark, which this type follows on purpose.
/// </remarks>
public readonly record struct MissionEvent
{
    private MissionEvent(long sequence, long tick, MissionEventKind kind, long subjectId, int reasonCode)
    {
        Sequence = sequence;
        Tick = tick;
        Kind = kind;
        SubjectId = subjectId;
        ReasonCode = reasonCode;
    }

    /// <summary>
    /// The dense, ascending event-sequence value this event was assigned at
    /// emission — drawn from <see cref="Simulation.MissionState.NextEventSequence"/>
    /// at the moment it was emitted, then that counter advances by one.
    /// </summary>
    public long Sequence { get; }

    /// <summary>The mission tick this event was emitted on.</summary>
    public long Tick { get; }

    public MissionEventKind Kind { get; }

    /// <summary>
    /// The subject this event is about. For
    /// <see cref="MissionEventKind.OrderRejected"/>, the rejected
    /// submission's assigned <c>OrderId</c> — design section 16: "carrying
    /// an order id at all requires one to have been assigned, so a rejected
    /// submission still consumes <c>NextOrderId</c>."
    /// </summary>
    public long SubjectId { get; }

    /// <summary>
    /// A kind-specific numeric detail. For
    /// <see cref="MissionEventKind.OrderRejected"/>, the numeric value of the
    /// <see cref="OrderRejectReason"/> the submission failed.
    /// </summary>
    public int ReasonCode { get; }

    /// <summary>
    /// Creates a validated <see cref="MissionEventKind.OrderRejected"/>
    /// event — design section 16: "A rejected order emits an authoritative
    /// event carrying the order id and a reason code."
    /// </summary>
    /// <param name="sequence">
    /// The value <see cref="Simulation.MissionState.NextEventSequence"/> held
    /// at the moment of emission.
    /// </param>
    /// <param name="tick">The mission tick the rejection was observed on.</param>
    /// <param name="orderId">The rejected submission's assigned order id.</param>
    /// <param name="reason">Which of design section 16's four rules the submission failed.</param>
    public static MissionEvent OrderRejected(long sequence, long tick, long orderId, OrderRejectReason reason)
    {
        if (sequence < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequence), sequence, "An event sequence must not be negative.");
        }

        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(
                nameof(reason), reason, "An order-rejected event requires a defined reason.");
        }

        return new MissionEvent(sequence, tick, MissionEventKind.OrderRejected, orderId, (int)reason);
    }

    /// <summary>
    /// Creates a validated <see cref="MissionEventKind.ShotFired"/> event —
    /// task 79d-1: stage 12's weapon chain completed a shot this tick,
    /// before hit resolution runs. <see cref="ReasonCode"/> carries the fire
    /// mode the shot was fired under.
    /// </summary>
    /// <param name="sequence">
    /// The value <see cref="Simulation.MissionState.NextEventSequence"/> held
    /// at the moment of emission.
    /// </param>
    /// <param name="tick">The mission tick the shot was fired on.</param>
    /// <param name="shooterEntityId">
    /// The firing operator's <see cref="Simulation.OperatorState.EntityId"/>,
    /// folded into <see cref="SubjectId"/> the same
    /// <c>unchecked((long)value)</c> way every other entity-id-carrying fold
    /// in this project reinterprets a <see langword="ulong"/> entity id.
    /// </param>
    /// <param name="mode">
    /// The single mode <see cref="Combat.FireModeSelection.SelectMode"/>
    /// resolved for this shot, stored in <see cref="ReasonCode"/> as its
    /// numeric <see cref="FireModeSet"/> value. Design section 9: "the
    /// simulation picks the mode, and the mode picks the sound slot" — this
    /// field is how the mode crosses out of <c>Sandata.Core</c> to the client
    /// that owns the sound catalog, without the client re-deriving a decision
    /// the simulation already made. Never a combination and never
    /// <see cref="FireModeSet.Safe"/>, both of which
    /// <see cref="Combat.FireModeSelection.SelectMode"/> already guarantees.
    /// </param>
    public static MissionEvent ShotFired(long sequence, long tick, ulong shooterEntityId, FireModeSet mode)
    {
        if (sequence < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequence), sequence, "An event sequence must not be negative.");
        }

        return new MissionEvent(
            sequence, tick, MissionEventKind.ShotFired, unchecked((long)shooterEntityId), (int)mode);
    }

    /// <summary>
    /// Creates a validated <see cref="MissionEventKind.WeaponLowered"/> or
    /// <see cref="MissionEventKind.WeaponRaised"/> event — design section 9:
    /// "the transition into it emits an authoritative event so the spectator
    /// can see the cause rather than only the effect."
    /// <see cref="ReasonCode"/> carries no meaning for either kind and is
    /// always <c>0</c>; which of the two states was entered is the kind
    /// itself, not a code.
    /// </summary>
    /// <param name="sequence">
    /// The value <see cref="Simulation.MissionState.NextEventSequence"/> held
    /// at the moment of emission.
    /// </param>
    /// <param name="tick">The mission tick the transition happened on.</param>
    /// <param name="operatorEntityId">
    /// The transitioning operator's
    /// <see cref="Simulation.OperatorState.EntityId"/>, folded into
    /// <see cref="SubjectId"/> exactly as <see cref="ShotFired"/> folds it.
    /// </param>
    /// <param name="lowered">
    /// <see langword="true"/> for the transition into the lowered state,
    /// <see langword="false"/> for the transition out of it.
    /// </param>
    public static MissionEvent WeaponLoweredChanged(
        long sequence, long tick, ulong operatorEntityId, bool lowered)
    {
        if (sequence < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequence), sequence, "An event sequence must not be negative.");
        }

        return new MissionEvent(
            sequence,
            tick,
            lowered ? MissionEventKind.WeaponLowered : MissionEventKind.WeaponRaised,
            unchecked((long)operatorEntityId),
            0);
    }

    /// <summary>
    /// Creates a validated <see cref="MissionEventKind.ShotHit"/> event —
    /// task 79d-1: the drawn angular error fell within the target's
    /// subtended half-angle, so the shot connects. <see cref="ReasonCode"/>
    /// carries no meaning for this kind and is always <c>0</c>.
    /// </summary>
    /// <param name="sequence">
    /// The value <see cref="Simulation.MissionState.NextEventSequence"/> held
    /// at the moment of emission.
    /// </param>
    /// <param name="tick">The mission tick the shot resolved on.</param>
    /// <param name="shooterEntityId">
    /// The firing operator's <see cref="Simulation.OperatorState.EntityId"/>,
    /// folded into <see cref="SubjectId"/> exactly as <see cref="ShotFired"/>
    /// folds it — the same shot, reported by the same subject.
    /// </param>
    public static MissionEvent ShotHit(long sequence, long tick, ulong shooterEntityId)
    {
        if (sequence < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequence), sequence, "An event sequence must not be negative.");
        }

        return new MissionEvent(sequence, tick, MissionEventKind.ShotHit, unchecked((long)shooterEntityId), 0);
    }

    /// <summary>
    /// Creates a validated <see cref="MissionEventKind.ShotMissed"/> event —
    /// task 79d-1: the drawn angular error exceeded the target's subtended
    /// half-angle, so the shot goes wide. <see cref="ReasonCode"/> carries no
    /// meaning for this kind and is always <c>0</c>.
    /// </summary>
    /// <param name="sequence">
    /// The value <see cref="Simulation.MissionState.NextEventSequence"/> held
    /// at the moment of emission.
    /// </param>
    /// <param name="tick">The mission tick the shot resolved on.</param>
    /// <param name="shooterEntityId">
    /// The firing operator's <see cref="Simulation.OperatorState.EntityId"/>,
    /// folded into <see cref="SubjectId"/> exactly as <see cref="ShotFired"/>
    /// folds it — the same shot, reported by the same subject.
    /// </param>
    public static MissionEvent ShotMissed(long sequence, long tick, ulong shooterEntityId)
    {
        if (sequence < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequence), sequence, "An event sequence must not be negative.");
        }

        return new MissionEvent(sequence, tick, MissionEventKind.ShotMissed, unchecked((long)shooterEntityId), 0);
    }

    /// <summary>
    /// Creates a validated <see cref="MissionEventKind.RoomCleared"/> event —
    /// Stage 0 of design decision 6 in
    /// <c>docs/plans/2026-08-14-sandata-clear-the-map-design.md</c>:
    /// <paramref name="roomId"/>'s clear flag flipped to
    /// <see langword="true"/> this tick. <see cref="ReasonCode"/> carries no
    /// meaning for this kind and is always <c>0</c>.
    /// </summary>
    /// <param name="sequence">
    /// The value <see cref="Simulation.MissionState.NextEventSequence"/> held
    /// at the moment of emission.
    /// </param>
    /// <param name="tick">The mission tick the room's clear flag flipped on.</param>
    /// <param name="roomId">
    /// The cleared room's <see cref="Navigation.RoomLayout"/> id, folded into
    /// <see cref="SubjectId"/> as a plain <see langword="long"/> widening — a
    /// room id is already <see langword="int"/>, unlike an entity id, so no
    /// <c>unchecked</c> reinterpretation is needed.
    /// </param>
    public static MissionEvent RoomCleared(long sequence, long tick, int roomId)
    {
        if (sequence < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequence), sequence, "An event sequence must not be negative.");
        }

        return new MissionEvent(sequence, tick, MissionEventKind.RoomCleared, roomId, 0);
    }
}
