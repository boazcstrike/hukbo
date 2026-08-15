namespace Hukbo.Core.Movement;

/// <summary>
/// One warrior's resolved in-fight evasive movement for a single tick under
/// <see cref="MovementPresetId.EvasiveFootworkV14"/>. Every other preset leaves
/// the field at <see cref="None"/> forever, and the value is folded into the
/// state hash only under that preset. Numeric values are part of the
/// deterministic replay and state-hash contract; the enum is append-only —
/// never renumber, reorder, or remove a member.
/// </summary>
/// <remarks>
/// <para>
/// This enum exists instead of new <see cref="FootworkPhase"/> members because
/// V14 does not resolve footwork phases at all: it registers
/// <see cref="MovementRuleset.UsesEquipmentRelativeFootwork"/> as
/// <see langword="false"/>, so the phase machinery is inert under it. Adding
/// members to <see cref="FootworkPhase"/> would also move the pinned member
/// count, the pressure-interrupt sweep, and the inspector's phase table, none
/// of which this feature touches.
/// </para>
/// <para>
/// Exactly one value is resolved per living warrior per tick, by a
/// priority-ordered, mutually exclusive ladder. A rung fires only for a warrior
/// that is already engaged — alive, holding a living enemy target, and
/// intending to move or attack — so no value here can ever be reached by a
/// warrior that is regrouping, holding, or backing away. That is the property
/// that makes this movement <i>during</i> a fight rather than away from one.
/// </para>
/// </remarks>
public enum EvasiveAction : byte
{
    /// <summary>
    /// No evasive movement this tick. The value every preset from V1 to V13
    /// leaves forever, the value a warrior outside an engagement holds, and the
    /// value death cleanup writes so a corpse cannot carry a stale action into
    /// the state hash.
    /// </summary>
    None = 0,

    /// <summary>
    /// The warrior wove laterally while closing on its target rather than
    /// walking straight in. Documented in general form — Pigafetta describes
    /// men moving from side to side at Mactan in 1521 — while the angle, the
    /// interval, and the distance are provisional gameplay tuning.
    /// </summary>
    SlipLateral = 1,

    /// <summary>
    /// The warrior stepped off the line of a missile already in flight toward
    /// it. This is the best-attested evasive movement in the research corpus:
    /// two independent manuscripts record that the men at Mactan would never
    /// stand still but leaped about under missile fire.
    /// <para>
    /// A projectile's clash outcome is resolved from its launch tick, so a
    /// warrior that visibly leaps aside can still be recorded as hit. That is
    /// deliberate, and it is also what the source describes: the leaping did
    /// not save them.
    /// </para>
    /// </summary>
    DodgeIncoming = 2,

    /// <summary>
    /// The warrior yielded a foot while pinned in contact, without dropping its
    /// target or leaving its own attack range. Provisional reconstruction. This
    /// is the one rung that moves a warrior backwards, and it is bounded so
    /// that it cannot read as a rout: a fixed short step, at most once every
    /// twelve ticks, and never enough to leave the warrior's own reach.
    /// </summary>
    GiveGround = 3,

    /// <summary>
    /// The warrior stepped off the line after an exchange against it was turned
    /// aside, circling its enemy at contact distance rather than opening the
    /// range. Provisional reconstruction: that a fighter moves after a bind or
    /// a blocked cut is a general property of contact weapons and is not
    /// attested for any specific sixteenth-century Philippine engagement.
    /// </summary>
    BreakOff = 4,

    /// <summary>
    /// The carrier for <see cref="BreakOff"/>. An exchange against this warrior
    /// was intercepted on the tick just resolved, and the break step is owed on
    /// the next one.
    /// </summary>
    /// <remarks>
    /// The attack stage writes this only onto a warrior whose action is
    /// <see cref="None"/>. That single condition makes the write idempotent
    /// when several attackers strike one defender in the same tick, keeps it
    /// from masking an evasive movement the warrior actually executed, and caps
    /// break-off at once every other tick.
    /// </remarks>
    BreakOffArmed = 5,
}
