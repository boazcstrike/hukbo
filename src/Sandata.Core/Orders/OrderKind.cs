namespace Sandata.Core.Orders;

/// <summary>
/// The six v0.1 order kinds design section 16 names, in the order that
/// section lists them: "The order kinds in v0.1 are <c>MoveAlongPath</c>,
/// <c>Hold</c>, <c>Breach</c>, <c>Sync</c>, <c>GoCodeRelease</c>, and
/// <c>Cancel</c>." Each numeric value is part of the replay contract in the
/// same sense <see cref="Rules.SandataPresetId"/>'s own doc comment states
/// for a preset id: a saved replay and the order stream section 16's
/// "What this does to the determinism contract" names name a kind by this
/// numeric value, not by its member name.
/// </summary>
/// <remarks>
/// <b>Append-only.</b> A member's numeric value never changes once it has
/// shipped, a member is never reordered, and a retired value is never reused
/// for a different kind — the same rule <see cref="Rules.SandataPresetId"/>
/// and <see cref="Maps.MapRecordKind"/> already state for their own members.
/// <c>OrderQueueTests</c> pins every member's numeric value as a literal so
/// an accidental renumbering fails loudly rather than silently re-keying
/// every recorded order-stream replay.
/// </remarks>
public enum OrderKind
{
    /// <summary>
    /// Follow an authored polyline, carried on <see cref="Order.PathNodes"/>.
    /// Design section 16, "An operator whose <c>OrderAssignment</c> is
    /// present follows the authored polyline that assignment names."
    /// </summary>
    MoveAlongPath = 0,

    /// <summary>Hold position. Carries no extra payload beyond the schedule and addressees.</summary>
    Hold = 1,

    /// <summary>
    /// Breach — one of Door Kickers 2's grouping primitives design section 16
    /// names as a v0.1 kind. Its payload shape beyond the addressees and
    /// schedule this record already carries is not specified by design
    /// section 16 or by any task in the order-layer wave (57 through 63); a
    /// later task that implements breach behaviour defines and owns that
    /// payload.
    /// </summary>
    Breach = 2,

    /// <summary>
    /// Pace-matches every living member of <see cref="Order.Addressees"/>.
    /// Design section 16, "Sync sets and go-codes": "the set is keyed by its
    /// lowest entity id" — which is <see cref="Order.Addressees"/>'s own
    /// first element, since that array is stored ascending. No payload beyond
    /// the addressee set is needed.
    /// </summary>
    Sync = 3,

    /// <summary>
    /// Releases a go-code. Design section 16: "releasing that letter is
    /// itself an order — a <c>GoCodeRelease</c> with its own
    /// <c>TargetTick</c> and <c>OrderSequence</c>." <see cref="Order.Addressees"/>
    /// names exactly the operators tied to the released code; no separate
    /// letter payload is required for the order to function, so none is
    /// declared here.
    /// </summary>
    GoCodeRelease = 4,

    /// <summary>
    /// Clears the current <c>OrderAssignment</c> of every addressed operator.
    /// Design section 16's clearing condition 2 addresses this order to the
    /// <b>operator</b>, not to an order id, so <see cref="Order.Addressees"/>
    /// alone identifies what is cancelled and no payload is needed.
    /// </summary>
    Cancel = 5,
}
