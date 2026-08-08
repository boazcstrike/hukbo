using Sandata.Core.Orders;

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
    /// before hit resolution runs. <see cref="ReasonCode"/> carries no
    /// meaning for this kind and is always <c>0</c>.
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
    public static MissionEvent ShotFired(long sequence, long tick, ulong shooterEntityId)
    {
        if (sequence < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequence), sequence, "An event sequence must not be negative.");
        }

        return new MissionEvent(sequence, tick, MissionEventKind.ShotFired, unchecked((long)shooterEntityId), 0);
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
}
