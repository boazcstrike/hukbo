using System.Collections.Immutable;
using Sandata.Core.Navigation;

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
/// <para>
/// <b>Exactly two doors add an order to this type: <see cref="SubmitValidated"/>
/// and <see cref="RestoreForResume"/>.</b> An earlier defect (the plan's task
/// 72 row, "A second bypassable door on <c>OrderQueue</c>") let
/// <c>queue with { Orders = ... }</c> inject arbitrary, unvalidated orders
/// because <see cref="Orders"/>'s <see langword="init"/> accessor was
/// <see langword="public"/>. <see cref="Orders"/>'s remarks explain the
/// accessibility this type narrowed it to and why; <see cref="RestoreForResume"/>'s
/// remarks explain why snapshot resume needs a second, non-validating door
/// rather than reusing <see cref="SubmitValidated"/>.
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
    /// <remarks>
    /// <b>The <see langword="init"/> accessor is <see langword="internal"/>,
    /// not <see langword="public"/>.</b> A fully <see langword="private"/>
    /// accessor was considered and rejected for this task specifically: it
    /// would also close the door for <c>OrderStateHashTests.cs</c>'s
    /// pre-existing <c>QueueOf</c> helper and one direct construction, a file
    /// this task's brief explicitly forbids editing, and breaking that file's
    /// compilation would take the whole <c>Sandata.Core.Tests</c> assembly
    /// down with it — including every test this task itself adds.
    /// <see langword="internal"/> is the narrowing that actually holds
    /// without that cost: <c>Sandata.Core</c>'s <c>AssemblyInfo.cs</c> grants
    /// <c>[assembly: InternalsVisibleTo("Sandata.Core.Tests")]</c> and
    /// nothing else, so this accessor is reachable only from
    /// <c>Sandata.Core</c> itself and from that one declared test friend —
    /// never from <c>Sandata.Client</c>, <c>Sandata.Headless</c>, or any
    /// other assembly, which is the actual "arbitrary caller" the original
    /// defect named. Restricting a member to one declared test friend via
    /// <c>InternalsVisibleTo</c> is not a new idea in this repository —
    /// <c>ShotSlotResolver</c>'s own remarks describe <c>Hukbo.Client</c>
    /// granting <c>InternalsVisibleTo</c> to <c>Hukbo.Client.Tests</c> alone
    /// and nowhere else — this member applies the same
    /// one-friend-assembly discipline to a property instead of a type.
    /// </remarks>
    public ImmutableArray<Order> Orders { get; internal init; } = ImmutableArray<Order>.Empty;

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
    /// <remarks>
    /// This is the unvalidated storage primitive. Design section 16: "An
    /// order is validated when it is submitted" — so this member is
    /// deliberately <see langword="private"/>, and <see cref="SubmitValidated"/>
    /// and <see cref="RestoreForResume"/> are the only two doors into this
    /// type that can add an order — see this type's own remarks for why
    /// there are exactly two, not one. <see cref="SubmitValidated"/> calls
    /// this method for every accepted order, including every kind other
    /// than <see cref="OrderKind.MoveAlongPath"/>, which design section 16's
    /// four rejection rules do not apply to; <see cref="RestoreForResume"/>
    /// never calls this method, because a restored order was already
    /// validated once, at its original submission.
    /// </remarks>
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
    private (OrderQueue Queue, Order Submitted) Submit(
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
    /// The validating submission boundary — design section 16, "Validation
    /// happens at submission, and rejection is observable": "An order is
    /// validated when it is submitted, not when it is applied." For
    /// <see cref="OrderKind.MoveAlongPath"/>, <paramref name="pathNodes"/> is
    /// checked against <see cref="OrderValidation.ValidateMoveAlongPath"/>'s
    /// four rules before anything is stored; every other
    /// <see cref="OrderKind"/> carries no authored polyline for those rules
    /// to apply to, so it is accepted unconditionally, exactly as
    /// <see cref="Submit"/> already accepts it.
    /// </summary>
    /// <param name="targetTick">The tick the order takes effect.</param>
    /// <param name="factionId">The faction this order addresses.</param>
    /// <param name="addressees">
    /// The addressed entity ids, in any order; sorted ascending on
    /// acceptance, exactly as <see cref="Submit"/> already sorts them.
    /// </param>
    /// <param name="kind">Which of the six v0.1 <see cref="OrderKind"/> members this order is.</param>
    /// <param name="grid">
    /// The current nav bake <see cref="OrderValidation.ValidateMoveAlongPath"/>
    /// checks a <see cref="OrderKind.MoveAlongPath"/> polyline's bounds and
    /// blocked-cell rules against. Ignored for every other <paramref name="kind"/>.
    /// </param>
    /// <param name="wallBuckets">
    /// The wall bucket index <see cref="OrderValidation.ValidateMoveAlongPath"/>
    /// checks the wall-crossing rule's broad phase against, built over the
    /// same map <paramref name="grid"/> was baked from. Ignored for every
    /// other <paramref name="kind"/>.
    /// </param>
    /// <param name="pathNodes">
    /// The <see cref="OrderKind.MoveAlongPath"/> payload. Defaults to an
    /// empty polyline for every kind that carries no path payload, exactly
    /// as <see cref="Submit"/> already defaults it.
    /// </param>
    /// <returns>
    /// <para>
    /// If the submission is accepted: the updated queue, the
    /// <see cref="Order"/> just created (which <see cref="Orders"/> now
    /// contains), and a <see langword="null"/> <c>Rejection</c>.
    /// </para>
    /// <para>
    /// If the submission is rejected: the updated queue, a
    /// <see langword="null"/> <c>Submitted</c> order, and the
    /// <see cref="OrderRejection"/> naming the id that was assigned to it
    /// and the rule it failed. Design section 16 says a rejected order
    /// "emits an authoritative event carrying the order id" — carrying an
    /// order id at all requires one to have been assigned, so a rejected
    /// submission still consumes <see cref="NextOrderId"/> and
    /// <see cref="NextOrderSequence"/> exactly as an accepted one would,
    /// even though <see cref="Orders"/> never gains an entry for it. Both
    /// counters remain "unique, never reused" (design section 16) across
    /// every submission attempt; only the <em>stored</em> order ids are
    /// dense, not the raw counter values a rejected attempt also drew from.
    /// </para>
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="grid"/> or <paramref name="wallBuckets"/> is <see langword="null"/>.
    /// </exception>
    public (OrderQueue Queue, Order? Submitted, OrderRejection? Rejection) SubmitValidated(
        long targetTick,
        int factionId,
        ImmutableArray<ulong> addressees,
        OrderKind kind,
        NavGrid grid,
        WallBuckets wallBuckets,
        ImmutableArray<OrderPathNode> pathNodes = default)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(wallBuckets);

        if (kind == OrderKind.MoveAlongPath)
        {
            var storedPathNodes = pathNodes.IsDefault ? ImmutableArray<OrderPathNode>.Empty : pathNodes;
            var rejectReason = OrderValidation.ValidateMoveAlongPath(new AuthoredPath(storedPathNodes), grid, wallBuckets);

            if (rejectReason is { } reason)
            {
                var rejectedOrderId = NextOrderId;
                var queueAfterRejection = this with
                {
                    NextOrderId = NextOrderId + 1,
                    NextOrderSequence = NextOrderSequence + 1,
                };

                return (queueAfterRejection, null, new OrderRejection(rejectedOrderId, reason));
            }
        }

        var (queue, submitted) = Submit(targetTick, factionId, addressees, kind, pathNodes);
        return (queue, submitted, null);
    }

    /// <summary>
    /// Rebuilds an <see cref="OrderQueue"/> from already-validated, previously
    /// stored state, with no validation of its own. This is the second and
    /// only other door into this type besides <see cref="SubmitValidated"/> —
    /// see this type's own remarks for why there are exactly two.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Resume-only.</b> This method's anticipated caller is
    /// <see cref="Simulation.MissionSnapshot.ToState"/>, restoring the queue a
    /// running mission had at the moment it was snapshotted. Every
    /// <see cref="Order"/> <paramref name="orders"/> carries was already
    /// checked by <see cref="SubmitValidated"/> at the moment it was
    /// originally submitted, before that mission was ever snapshotted; this
    /// method's job is to place that already-accepted state back into a live
    /// <see cref="OrderQueue"/>, not to decide acceptance a second time. It
    /// is not a submission path, and a caller that wants to submit a new
    /// order — including one built from data that merely resembles a stored
    /// order — must call <see cref="SubmitValidated"/> instead.
    /// </para>
    /// <para>
    /// <b>Why revalidating on restore would be wrong.</b> Design section 16,
    /// "An authored polyline is authoritative, not derived": "An authored
    /// polyline is player input. It is stored verbatim in the snapshot and
    /// folds into the state hash. It is never recomputed, never re-smoothed,
    /// and never replaced by a search result. On resume it is restored
    /// exactly as it was drawn." A <see cref="OrderKind.MoveAlongPath"/>
    /// order that <see cref="OrderValidation.ValidateMoveAlongPath"/> would
    /// reject under today's nav bake can still be a perfectly legitimate
    /// stored order, because the bake in effect at resume is not necessarily
    /// the bake in effect at the order's original submission — the same
    /// design section names exactly this failure mode: "an authored path
    /// recomputed on resume is a defect that would let the nav bake state at
    /// load time rewrite a decision the player made an hour earlier."
    /// Calling <see cref="OrderValidation.ValidateMoveAlongPath"/> again here
    /// would do precisely that: reject, on resume, an order the player's
    /// original submission legitimately passed.
    /// </para>
    /// </remarks>
    /// <param name="nextOrderId">
    /// The stored <see cref="NextOrderId"/> counter, restored verbatim.
    /// </param>
    /// <param name="nextOrderSequence">
    /// The stored <see cref="NextOrderSequence"/> counter, restored verbatim.
    /// </param>
    /// <param name="orders">
    /// The stored <see cref="Orders"/> array, restored verbatim and in its
    /// original storage order; this method does not sort, filter, or
    /// otherwise transform it.
    /// </param>
    /// <returns>
    /// A new <see cref="OrderQueue"/> whose <see cref="NextOrderId"/>,
    /// <see cref="NextOrderSequence"/>, and <see cref="Orders"/> exactly
    /// match the three arguments given.
    /// </returns>
    public static OrderQueue RestoreForResume(long nextOrderId, long nextOrderSequence, ImmutableArray<Order> orders) =>
        new(nextOrderId, nextOrderSequence)
        {
            Orders = orders.IsDefault ? ImmutableArray<Order>.Empty : orders,
        };

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
