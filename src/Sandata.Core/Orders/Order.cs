using System.Collections.Immutable;

namespace Sandata.Core.Orders;

/// <summary>
/// One node of an authored polyline — the <see cref="OrderKind.MoveAlongPath"/>
/// payload carried on <see cref="Order.PathNodes"/>. Coordinates are plain
/// <see langword="long"/> world units, the exact representation
/// <c>PathSmoothing.Smooth</c> already writes into its output spans and
/// <c>ExactPredicates.ClassifySegments</c> already takes as input, so a node
/// crossing this boundary needs no conversion before design section 16's
/// wall-crossing and bounds checks — task 58's job — run against it. See
/// <c>Order</c>'s own remarks for why this representation was chosen over a
/// <see cref="Hukbo.Core.Mathematics.FixedPoint"/> pair.
/// </summary>
public readonly record struct OrderPathNode(long X, long Y);

/// <summary>
/// An immutable order record. Design section 16, "Order records and the
/// queue": "An order is an immutable record carrying an identity, a
/// schedule, an addressee set, a kind, and that kind's payload."
/// </summary>
/// <param name="OrderId">
/// "Dense, ascending, assigned at submission." The stable identity a later
/// task's operator inspector names when it shows "the active order id"
/// (design section 16, "What a spectator sees").
/// </param>
/// <param name="OrderSequence">
/// "The submission counter, unique and never reused." The sole tiebreaker
/// stage 1 uses when two orders share a <see cref="TargetTick"/> — design
/// section 5's fourteen-stage table, stage 1: "Apply orders, ordered by
/// <c>(targetTick, orderSequence)</c>."
/// </param>
/// <param name="TargetTick">"The tick at which the order takes effect."</param>
/// <param name="FactionId">
/// "Orders address one faction's operators only." <see langword="int"/>,
/// matching <c>OperatorState.Faction</c> and <c>FactionAlertState.FactionId</c>
/// — a two-valued faction selector, not an entity or group identifier, so
/// task 64's 2026-08-07 identifier-widening pass does not touch this field.
/// </param>
/// <param name="Kind">Which of the six v0.1 <see cref="OrderKind"/> members this order is.</param>
/// <remarks>
/// <para>
/// <b><see cref="OrderId"/> and <see cref="OrderSequence"/> are both
/// <see langword="long"/>, not <see langword="ulong"/>.</b> Neither is an
/// entity or group identifier, so task 64's 2026-08-07 identifier-widening
/// pass — which widened only <c>OperatorState.EntityId</c>-shaped fields to
/// <see langword="ulong"/> — does not reach them; they follow
/// <c>MissionState.NextEventSequence</c>'s precedent instead, the closest
/// existing "dense submission counter" field, which is also
/// <see langword="long"/>.
/// </para>
/// <para>
/// <b>Payload representation, and the choice this file makes.</b> Design
/// section 16 says a payload exists ("<c>Kind</c> and that kind's payload")
/// without naming its shape. This file declares one payload field,
/// <see cref="PathNodes"/>, populated only when <see cref="Kind"/> is
/// <see cref="OrderKind.MoveAlongPath"/> and empty for every other kind:
/// <see cref="OrderKind.Hold"/>, <see cref="OrderKind.Sync"/>, and
/// <see cref="OrderKind.Cancel"/> need nothing beyond
/// <see cref="Addressees"/> and the schedule (see each member's own remarks
/// on <see cref="OrderKind"/>), <see cref="OrderKind.GoCodeRelease"/> is
/// fully identified by its addressee set without a separate letter field,
/// and <see cref="OrderKind.Breach"/>'s payload is undefined by design and by
/// every task in the order-layer wave, so it is left for whichever later
/// task implements breach behaviour to add. <b>Rejected alternative:</b> a
/// <c>MapRecord</c>-shaped polymorphic hierarchy (<c>abstract record Order</c>
/// plus one sealed derived record per <see cref="OrderKind"/>, mirroring
/// <c>MapRecord</c>/<c>MapRecordKind</c>) was considered and rejected for
/// this task specifically because the plan's task 57 row asks to "Declare
/// <c>Order</c> with <c>OrderId</c>, <c>OrderSequence</c>, <c>TargetTick</c>,
/// <c>FactionId</c>, ascending <c>Addressees</c>, <c>Kind</c>, and payload" —
/// a flat member list naming one type, not six — and because design section
/// 16 itself calls the node cap "a <c>const</c> on the order type" (singular),
/// which only reads naturally against one concrete <c>Order</c> type rather
/// than a base type whose <c>const</c> a subtype would have to inherit.
/// <b>Rejected alternative:</b> a generic <c>object</c> or
/// boxed-payload field was rejected outright — it would defeat
/// nullable-reference and compile-time checking for no benefit, and every
/// value this record needs to hold is already expressible without boxing.
/// </para>
/// <para>
/// <b>Why <see langword="long"/> world units for <see cref="OrderPathNode"/>,
/// not <see cref="Hukbo.Core.Mathematics.FixedPoint"/>.</b>
/// <c>OperatorState.PositionX</c>/<c>PositionY</c> are <c>FixedPoint</c>
/// because they are live simulation state read every tick alongside other
/// <c>FixedPoint</c> quantities. An authored polyline node is drawn input,
/// consumed only by <c>ExactPredicates.ClassifySegments</c> (task 58) and by
/// movement code that already converts to world units via
/// <c>WorldUnits.FromFixedPoint</c> before calling geometry (see that type's
/// own remarks). Storing the node already in world units means task 58's
/// wall-crossing check needs no conversion step, and it keeps this record
/// free of a <c>FixedPoint</c> dependency it does not otherwise need.
/// </para>
/// <para>
/// This type holds two <see cref="ImmutableArray{T}"/> members
/// (<see cref="Addressees"/> and <see cref="PathNodes"/>), so — exactly like
/// <c>OperatorState</c> and <c>MissionState</c> — it overrides
/// <c>Equals</c>/<c>GetHashCode</c> rather than relying on the record
/// default, which would compare each backing array by reference.
/// </para>
/// </remarks>
public sealed record Order(
    long OrderId,
    long OrderSequence,
    long TargetTick,
    int FactionId,
    OrderKind Kind)
{
    /// <summary>
    /// The structural cap on an authored polyline's node count. Design
    /// section 16: "The node-count cap on an authored polyline is a
    /// <c>const</c> on the order type, not a field on
    /// <c>SandataRuleset</c>. It is a structural limit that exists so a
    /// malformed input cannot allocate without bound, not a tuning value a
    /// designer would ever sweep, and putting it on the ruleset would move
    /// <c>ContentHash</c> for a constant that never varies."
    /// </summary>
    /// <remarks>
    /// <b>PROVISIONAL.</b> Neither design section 16 nor the plan's task 57
    /// row names a number. <c>128</c> is this task's own invented value,
    /// generous enough for any hand-drawn path a spectator could plausibly
    /// click out in one drag while small enough to bound a malformed input's
    /// allocation, and it carries no historical or tuning claim — task 58,
    /// which enforces this cap as a rejection rule, is free to revise it
    /// before it is ever exercised by a real fixture.
    /// </remarks>
    public const int MaxAuthoredPathNodeCount = 128;

    /// <summary>
    /// Entity ids in ascending order, "so the set has one written form"
    /// (design section 16). <see langword="ulong"/> to match
    /// <c>OperatorState.EntityId</c> (task 64's 2026-08-07
    /// identifier-widening pass). Ordering is this type's own invariant here
    /// — <see cref="Orders.OrderQueue.Submit"/> sorts a caller-supplied,
    /// possibly-unordered addressee list before it ever reaches this
    /// property's backing value.
    /// </summary>
    public ImmutableArray<ulong> Addressees { get; init; } = ImmutableArray<ulong>.Empty;

    /// <summary>
    /// The <see cref="OrderKind.MoveAlongPath"/> payload: the authored
    /// polyline, drawing order first to last, never re-smoothed and never
    /// recomputed (design section 16, "An authored polyline is authoritative,
    /// not derived"). Empty for every other <see cref="Kind"/> in v0.1 — see
    /// this type's own remarks for why no other kind needs a payload field.
    /// </summary>
    public ImmutableArray<OrderPathNode> PathNodes { get; init; } = ImmutableArray<OrderPathNode>.Empty;

    public bool Equals(Order? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return OrderId == other.OrderId &&
            OrderSequence == other.OrderSequence &&
            TargetTick == other.TargetTick &&
            FactionId == other.FactionId &&
            Kind == other.Kind &&
            AddresseesSpan.SequenceEqual(other.AddresseesSpan) &&
            PathNodesSpan.SequenceEqual(other.PathNodesSpan);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(OrderId);
        hash.Add(OrderSequence);
        hash.Add(TargetTick);
        hash.Add(FactionId);
        hash.Add(Kind);
        foreach (var addressee in AddresseesSpan)
        {
            hash.Add(addressee);
        }

        foreach (var node in PathNodesSpan)
        {
            hash.Add(node);
        }

        return hash.ToHashCode();
    }

    private ReadOnlySpan<ulong> AddresseesSpan =>
        Addressees.IsDefault ? ReadOnlySpan<ulong>.Empty : Addressees.AsSpan();

    private ReadOnlySpan<OrderPathNode> PathNodesSpan =>
        PathNodes.IsDefault ? ReadOnlySpan<OrderPathNode>.Empty : PathNodes.AsSpan();
}
