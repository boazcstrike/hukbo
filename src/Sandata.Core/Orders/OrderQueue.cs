using System.Collections.Immutable;

namespace Sandata.Core.Orders;

/// <summary>
/// Sandata's order queue: every <see cref="Order"/> submitted so far, plus
/// the two dense, ascending submission counters design section 16 assigns
/// "at submission" — <see cref="NextOrderId"/> and
/// <see cref="NextOrderSequence"/>. Design section 5's fourteen-stage table,
/// stage 1: "Apply orders, ordered by <c>(targetTick, orderSequence)</c>."
/// <see cref="InApplicationOrder"/> is that ordering; a later task (61) is
/// the one that folds this type into <c>MissionState</c>, the snapshot, and
/// <c>SandataStateHasher</c> — design section 16: "The queue is authoritative
/// state. It is snapshotted and it folds into the state hash, in ascending
/// <c>(TargetTick, OrderSequence)</c>."
/// </summary>
/// <remarks>
/// <para>
/// <b>Why <see cref="NextOrderId"/> and <see cref="NextOrderSequence"/> are
/// two separate counters that always move together.</b> Design section 16
/// names them as two distinct fields with two distinct purposes — an order's
/// stable identity for later reference (the operator inspector's "active
/// order id") versus the pure tiebreaker stage 1's ordering rule reads — so
/// this type keeps them as two fields rather than collapsing them into one.
/// <see cref="Submit"/> increments both by exactly one on every call, so in
/// v0.1 the two values always coincide for any given order; nothing in
/// design section 16 or the plan's task 57 through 63 rows requires them to
/// diverge, and this record does not invent a reason for them to.
/// </para>
/// <para>
/// <b>Sorted flat structure, not a priority queue.</b> The plan's standing
/// rule 3 bans <c>PriorityQueue&lt;</c> from <c>Sandata.Core</c>.
/// <see cref="InApplicationOrder"/> instead sorts the plain
/// <see cref="ImmutableArray{T}"/> <see cref="Orders"/> holds, using
/// <see cref="CompareApplicationOrder"/> as a total comparator — see that
/// member's own remarks for why it is total rather than merely a total
/// preorder.
/// </para>
/// <para>
/// This type holds an <see cref="ImmutableArray{T}"/> member
/// (<see cref="Orders"/>), so — exactly like <c>Order</c>,
/// <c>OperatorState</c>, and <c>MissionState</c> — it overrides
/// <c>Equals</c>/<c>GetHashCode</c> rather than relying on the record
/// default, which would compare the backing array by reference.
/// </para>
/// </remarks>
public sealed record OrderQueue(long NextOrderId, long NextOrderSequence)
{
    /// <summary>An empty queue with both counters at zero — the value a new mission starts from.</summary>
    public static readonly OrderQueue Empty = new(0, 0);

    /// <summary>
    /// Every order submitted so far, in submission order (the order
    /// <see cref="Submit"/> appended them, not the applied order). Callers
    /// that need the applied order call <see cref="InApplicationOrder"/>
    /// instead of reading this property directly.
    /// </summary>
    public ImmutableArray<Order> Orders { get; init; } = ImmutableArray<Order>.Empty;

    /// <summary>
    /// The total comparator stage 1 applies the queue under: ascending
    /// <see cref="Order.TargetTick"/>, then ascending
    /// <see cref="Order.OrderSequence"/> when two orders share a
    /// <see cref="Order.TargetTick"/>.
    /// </summary>
    /// <remarks>
    /// <b>This comparator is provably total, not merely a total preorder.</b>
    /// A total preorder can still return <c>0</c> for two distinct elements,
    /// which is exactly the case where <see cref="ImmutableArray{T}.Sort(Comparison{T})"/>'s
    /// underlying introsort is allowed to reorder equal-comparing elements
    /// arbitrarily (it is not a stable sort). That case cannot occur here:
    /// <see cref="Order.OrderSequence"/> is "unique and never reused" (design
    /// section 16) — <see cref="Submit"/> assigns it from
    /// <see cref="NextOrderSequence"/>, which strictly increases by one on
    /// every call and is never reset or reassigned — so for any two distinct
    /// orders produced by this type, either their <see cref="Order.TargetTick"/>
    /// values differ (the first comparison already returns non-zero) or their
    /// <see cref="Order.TargetTick"/> values are equal and their
    /// <see cref="Order.OrderSequence"/> values differ, because no two
    /// distinct orders can share an <see cref="Order.OrderSequence"/> (the
    /// second comparison then returns non-zero). No pair of distinct orders
    /// compares equal, so this comparator induces a strict total order over
    /// any set of orders this type produced, and introsort's instability has
    /// no equal-key pair to make visible. <c>OrderQueueTests</c> exercises
    /// this directly with many orders sharing one <see cref="Order.TargetTick"/>.
    /// </remarks>
    public static int CompareApplicationOrder(Order left, Order right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        var tickComparison = left.TargetTick.CompareTo(right.TargetTick);
        return tickComparison != 0
            ? tickComparison
            : left.OrderSequence.CompareTo(right.OrderSequence);
    }

    /// <summary>
    /// Appends one new order, assigning it the next dense
    /// <see cref="Order.OrderId"/> and <see cref="Order.OrderSequence"/> and
    /// sorting <paramref name="addressees"/> into ascending order before it
    /// is stored — design section 16: "<c>Addressees</c> — entity ids in
    /// ascending order, so the set has one written form."
    /// </summary>
    /// <param name="targetTick">The tick the order takes effect.</param>
    /// <param name="factionId">The faction this order addresses.</param>
    /// <param name="addressees">
    /// The addressed entity ids, in any order; the returned
    /// <see cref="Order.Addressees"/> is always ascending regardless of the
    /// order this array arrives in.
    /// </param>
    /// <param name="kind">Which of the six v0.1 <see cref="OrderKind"/> members this order is.</param>
    /// <param name="pathNodes">
    /// The <see cref="OrderKind.MoveAlongPath"/> payload. Ignored in the
    /// sense that it is stored verbatim regardless of <paramref name="kind"/>
    /// — this method performs no per-kind validation, matching how
    /// <c>MapTokenizer</c> parses a record's fields without deciding whether
    /// they are valid for the record's kind (design section 12's
    /// cross-record validation is <c>MapValidator</c>'s job, not the
    /// tokenizer's); design section 16's rejection rules are task 58's job,
    /// not this type's. Defaults to an empty polyline for every kind that
    /// carries no path payload.
    /// </param>
    /// <returns>
    /// The updated queue and the <see cref="Order"/> just created, so a
    /// caller has both the new authoritative state and the concrete value it
    /// just submitted without re-deriving it from
    /// <see cref="Orders"/>.
    /// </returns>
    public (OrderQueue Queue, Order Submitted) Submit(
        long targetTick,
        int factionId,
        ImmutableArray<ulong> addressees,
        OrderKind kind,
        ImmutableArray<OrderPathNode> pathNodes = default)
    {
        var sortedAddressees = addressees.IsDefaultOrEmpty
            ? ImmutableArray<ulong>.Empty
            : addressees.Sort();

        var storedPathNodes = pathNodes.IsDefault
            ? ImmutableArray<OrderPathNode>.Empty
            : pathNodes;

        var order = new Order(NextOrderId, NextOrderSequence, targetTick, factionId, kind)
        {
            Addressees = sortedAddressees,
            PathNodes = storedPathNodes,
        };

        var updated = this with
        {
            NextOrderId = NextOrderId + 1,
            NextOrderSequence = NextOrderSequence + 1,
            Orders = Orders.Add(order),
        };

        return (updated, order);
    }

    /// <summary>
    /// Every submitted order, sorted ascending by
    /// <see cref="CompareApplicationOrder"/> — the order stage 1 applies
    /// them in. Does not mutate <see cref="Orders"/>;
    /// <see cref="ImmutableArray{T}.Sort(Comparison{T})"/> returns a new
    /// array.
    /// </summary>
    public ImmutableArray<Order> InApplicationOrder() =>
        Orders.IsDefaultOrEmpty ? Orders : Orders.Sort(CompareApplicationOrder);

    public bool Equals(OrderQueue? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return NextOrderId == other.NextOrderId &&
            NextOrderSequence == other.NextOrderSequence &&
            OrdersSpan.SequenceEqual(other.OrdersSpan);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(NextOrderId);
        hash.Add(NextOrderSequence);
        foreach (var order in OrdersSpan)
        {
            hash.Add(order);
        }

        return hash.ToHashCode();
    }

    private ReadOnlySpan<Order> OrdersSpan =>
        Orders.IsDefault ? ReadOnlySpan<Order>.Empty : Orders.AsSpan();
}
