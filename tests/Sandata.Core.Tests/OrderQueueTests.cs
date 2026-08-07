using System.Collections.Immutable;
using Sandata.Core.Navigation;
using Sandata.Core.Orders;

namespace Sandata.Core.Tests;

/// <summary>
/// Task 57 of docs/plans/2026-08-07-sandata-scaffold.md: <c>Order</c>,
/// <c>OrderKind</c>, and <c>OrderQueue</c> against design section 16, "Order
/// records and the queue," and design section 5's stage 1, "Apply orders,
/// ordered by <c>(targetTick, orderSequence)</c>."
/// </summary>
public sealed class OrderQueueTests
{
    /// <summary>
    /// Design section 16 lists exactly these six kinds, in this order:
    /// "<c>MoveAlongPath</c>, <c>Hold</c>, <c>Breach</c>, <c>Sync</c>,
    /// <c>GoCodeRelease</c>, and <c>Cancel</c>." Every numeric value is
    /// pinned as a literal so an accidental renumbering — which would re-key
    /// every recorded order-stream replay per design section 16's
    /// determinism-contract amendment — fails loudly here instead.
    /// </summary>
    [Theory]
    [InlineData(OrderKind.MoveAlongPath, 0)]
    [InlineData(OrderKind.Hold, 1)]
    [InlineData(OrderKind.Breach, 2)]
    [InlineData(OrderKind.Sync, 3)]
    [InlineData(OrderKind.GoCodeRelease, 4)]
    [InlineData(OrderKind.Cancel, 5)]
    public void OrderKind_NumericValueIsPinned(OrderKind kind, int expectedValue)
    {
        Assert.Equal(expectedValue, (int)kind);
    }

    /// <summary>
    /// <see cref="OrderQueue.Submit"/> assigns <see cref="Order.OrderSequence"/>
    /// in call order, so "a deliberately shuffled submission order" means
    /// shuffling which <see cref="Order.TargetTick"/> is submitted at each
    /// call. Four distinct permutations of the same four target ticks (one
    /// of which repeats, to also exercise a tie within the shuffle) all
    /// still produce <see cref="OrderQueue.InApplicationOrder"/> results that
    /// equal a stable sort by <c>(TargetTick, OrderSequence)</c> — expressed
    /// here as the exact expected <see cref="Order.OrderSequence"/> sequence,
    /// computed by hand for each permutation and asserted directly, not
    /// re-derived from the type under test.
    /// </summary>
    [Theory]
    [MemberData(nameof(ShuffledSubmissionOrders))]
    public void InApplicationOrder_MatchesTargetTickThenOrderSequence_ForShuffledSubmissionOrder(
        long[] submittedTargetTicks, long[] expectedOrderSequence)
    {
        var queue = OrderQueue.Empty;
        var grid = NewOpenGrid();
        var wallBuckets = NoWalls(grid);
        foreach (var targetTick in submittedTargetTicks)
        {
            (queue, _, _) = queue.SubmitValidated(
                targetTick, factionId: 0, ImmutableArray<ulong>.Empty, OrderKind.Hold, grid, wallBuckets);
        }

        var applied = queue.InApplicationOrder();

        Assert.Equal(
            expectedOrderSequence,
            applied.Select(order => order.OrderSequence).ToArray());
    }

    /// <summary>
    /// Four distinct permutations of the target-tick set <c>{10, 5, 20, 5}</c>,
    /// each paired with the applied <see cref="Order.OrderSequence"/> order a
    /// stable sort by <c>(TargetTick, OrderSequence)</c> produces once
    /// <see cref="OrderQueue.Submit"/> has assigned sequence numbers 0, 1, 2,
    /// 3 in submission-call order.
    /// </summary>
    public static IEnumerable<object[]> ShuffledSubmissionOrders()
    {
        // Submitted [10, 5, 20, 5] -> pairs (10,0) (5,1) (20,2) (5,3).
        // Sorted: (5,1) (5,3) (10,0) (20,2).
        yield return new object[]
        {
            new long[] { 10, 5, 20, 5 },
            new long[] { 1, 3, 0, 2 },
        };

        // Submitted [5, 5, 20, 10] -> pairs (5,0) (5,1) (20,2) (10,3).
        // Sorted: (5,0) (5,1) (10,3) (20,2).
        yield return new object[]
        {
            new long[] { 5, 5, 20, 10 },
            new long[] { 0, 1, 3, 2 },
        };

        // Submitted [20, 10, 5, 5] -> pairs (20,0) (10,1) (5,2) (5,3).
        // Sorted: (5,2) (5,3) (10,1) (20,0).
        yield return new object[]
        {
            new long[] { 20, 10, 5, 5 },
            new long[] { 2, 3, 1, 0 },
        };

        // Submitted [5, 20, 5, 10] -> pairs (5,0) (20,1) (5,2) (10,3).
        // Sorted: (5,0) (5,2) (10,3) (20,1).
        yield return new object[]
        {
            new long[] { 5, 20, 5, 10 },
            new long[] { 0, 2, 3, 1 },
        };
    }

    /// <summary>
    /// Two orders sharing a <see cref="Order.TargetTick"/> resolve on
    /// <see cref="Order.OrderSequence"/> alone — asserted with
    /// <see cref="Order.OrderId"/> deliberately uncorrelated with
    /// <see cref="Order.OrderSequence"/> (the higher id carries the lower
    /// sequence) so a comparator that accidentally consulted
    /// <see cref="Order.OrderId"/> instead would fail this test. Both
    /// possible storage-array orders are checked, so the result cannot be an
    /// artefact of <see cref="ImmutableArray{T}.Add"/> append order either.
    /// </summary>
    [Fact]
    public void InApplicationOrder_TwoOrdersSharingATargetTick_ResolveOnOrderSequenceAlone()
    {
        var lowerSequenceHigherId = MakeOrder(orderId: 9, orderSequence: 3, targetTick: 100);
        var higherSequenceLowerId = MakeOrder(orderId: 2, orderSequence: 7, targetTick: 100);

        var queueLowerFirst = OrderQueue.Empty with
        {
            Orders = ImmutableArray.Create(lowerSequenceHigherId, higherSequenceLowerId),
        };
        var queueHigherFirst = OrderQueue.Empty with
        {
            Orders = ImmutableArray.Create(higherSequenceLowerId, lowerSequenceHigherId),
        };

        var expected = new[] { lowerSequenceHigherId, higherSequenceLowerId };

        Assert.Equal(expected, queueLowerFirst.InApplicationOrder());
        Assert.Equal(expected, queueHigherFirst.InApplicationOrder());
    }

    /// <summary>
    /// Proves <see cref="OrderQueue.CompareApplicationOrder"/> is a strict
    /// total order rather than a total preorder that merely happens not to
    /// collide in the smaller tests above. Thirty orders share one
    /// <see cref="Order.TargetTick"/> — the case that puts maximum pressure
    /// on the comparator, since every comparison falls through to
    /// <see cref="Order.OrderSequence"/> — and are stored in a thoroughly
    /// shuffled array position, using <c>(sequence * 7 + 3) % 30</c> as the
    /// storage index. That map is a bijection on <c>[0, 30)</c> because
    /// <c>gcd(7, 30) == 1</c>, so every one of the 30 orders lands at a
    /// distinct storage slot and the shuffle is provably a genuine
    /// permutation, not an accidental near-identity. If
    /// <see cref="Order.OrderSequence"/> uniqueness did not make the
    /// comparator total, <see cref="ImmutableArray{T}.Sort(Comparison{T})"/>'s
    /// unstable introsort would be free to leave this shuffle only partially
    /// undone; instead the result must come back in exact ascending
    /// <see cref="Order.OrderSequence"/> order every time.
    /// </summary>
    [Fact]
    public void InApplicationOrder_OrderSequenceUniquenessTotallyOrdersManyOrdersSharingOneTargetTick()
    {
        const int count = 30;
        const long sharedTargetTick = 999;

        var byAscendingSequence = new Order[count];
        for (var sequence = 0; sequence < count; sequence++)
        {
            byAscendingSequence[sequence] = MakeOrder(
                orderId: sequence, orderSequence: sequence, targetTick: sharedTargetTick);
        }

        var shuffledStorage = new Order[count];
        for (var sequence = 0; sequence < count; sequence++)
        {
            var storageSlot = (sequence * 7 + 3) % count;
            shuffledStorage[storageSlot] = byAscendingSequence[sequence];
        }

        Assert.DoesNotContain(shuffledStorage, order => order is null);

        var queue = OrderQueue.Empty with { Orders = ImmutableArray.Create(shuffledStorage) };

        var applied = queue.InApplicationOrder();

        Assert.Equal(
            Enumerable.Range(0, count).Select(sequence => (long)sequence),
            applied.Select(order => order.OrderSequence));
    }

    /// <summary>
    /// Design section 16: "<c>Addressees</c> — entity ids in ascending
    /// order, so the set has one written form." <see cref="OrderQueue.Submit"/>
    /// is exercised with three distinct permutations of the same five
    /// entity ids, each producing the identical ascending
    /// <see cref="Order.Addressees"/> array.
    /// </summary>
    [Theory]
    [MemberData(nameof(UnorderedAddresseeLists))]
    public void Submit_StoresAddresseesAscending_RegardlessOfSubmittedOrder(ulong[] submittedAddressees)
    {
        var queue = OrderQueue.Empty;
        var grid = NewOpenGrid();
        var wallBuckets = NoWalls(grid);

        (_, var submitted, var rejection) = queue.SubmitValidated(
            targetTick: 1,
            factionId: 0,
            ImmutableArray.Create(submittedAddressees),
            OrderKind.Hold,
            grid,
            wallBuckets);

        Assert.Null(rejection);
        Assert.NotNull(submitted);
        Assert.Equal(
            new ulong[] { 2, 5, 17, 30, 100 }, submitted!.Addressees.ToArray());
    }

    public static IEnumerable<object[]> UnorderedAddresseeLists()
    {
        yield return new object[] { new ulong[] { 30, 5, 100, 2, 17 } };
        yield return new object[] { new ulong[] { 100, 30, 17, 5, 2 } };
        yield return new object[] { new ulong[] { 2, 100, 5, 30, 17 } };
    }

    /// <summary>
    /// <see cref="OrderQueue.Submit"/> assigns dense, ascending, never-reused
    /// values to both <see cref="Order.OrderId"/> and
    /// <see cref="Order.OrderSequence"/> — design section 16's own two
    /// stated properties for each field — and stores the
    /// <see cref="OrderKind.MoveAlongPath"/> polyline payload verbatim, which
    /// is what makes it "snapshottable verbatim by task 61" rather than a
    /// derived value.
    /// </summary>
    [Fact]
    public void Submit_AssignsDenseAscendingIdsAndStoresThePathPayloadVerbatim()
    {
        var queue = OrderQueue.Empty;
        var nodes = ImmutableArray.Create(
            new OrderPathNode(10, 20), new OrderPathNode(30, 40), new OrderPathNode(50, 60));

        // Widened to 20x20 cells (80x80 world units) so every node in
        // `nodes` — up to (50, 60) — is in bounds; the default 10x10 grid
        // used elsewhere in this file is too small for this fixture's own
        // path, now that SubmitValidated runs bounds validation on it.
        var grid = NewOpenGrid(widthCells: 20, heightCells: 20);
        var wallBuckets = NoWalls(grid);

        (queue, var first, var firstRejection) = queue.SubmitValidated(
            targetTick: 5, factionId: 0, ImmutableArray<ulong>.Empty, OrderKind.MoveAlongPath, grid, wallBuckets, nodes);
        (queue, var second, var secondRejection) = queue.SubmitValidated(
            targetTick: 5, factionId: 1, ImmutableArray<ulong>.Empty, OrderKind.Hold, grid, wallBuckets);

        Assert.Null(firstRejection);
        Assert.Null(secondRejection);
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(0, first!.OrderId);
        Assert.Equal(0, first.OrderSequence);
        Assert.Equal(1, second!.OrderId);
        Assert.Equal(1, second.OrderSequence);
        Assert.Equal(nodes, first.PathNodes);
        Assert.Empty(second.PathNodes);
    }

    private static Order MakeOrder(long orderId, long orderSequence, long targetTick) =>
        new(orderId, orderSequence, targetTick, FactionId: 0, OrderKind.Hold);

    private static NavGrid NewOpenGrid(int widthCells = 10, int heightCells = 10)
    {
        var grid = new NavGrid(width: widthCells, height: heightCells);
        Array.Fill(grid.Passability, NavCellFlags.Open);
        return grid;
    }

    private static WallBuckets NoWalls(NavGrid grid) => WallBuckets.Build(grid, [], [], [], []);

    /// <summary>
    /// Task 58's wiring requirement: <see cref="OrderQueue.SubmitValidated"/>
    /// runs design section 16's four rejection rules at the submission
    /// boundary, not as an uncalled helper. An empty, open grid with no
    /// walls accepts every rule except the node-count one, which a
    /// single-node path deliberately fails, isolating the wiring itself from
    /// any one rule's own logic (already covered fixture by fixture in
    /// <c>OrderValidationTests</c>).
    /// </summary>
    [Fact]
    public void SubmitValidated_RejectedOrder_DoesNotEnterTheQueueButConsumesIdAndSequence()
    {
        var grid = NewOpenGrid();
        var wallBuckets = NoWalls(grid);
        var queue = OrderQueue.Empty;

        var (queueAfterRejection, submitted, rejection) = queue.SubmitValidated(
            targetTick: 5,
            factionId: 0,
            ImmutableArray<ulong>.Empty,
            OrderKind.MoveAlongPath,
            grid,
            wallBuckets,
            ImmutableArray.Create(new OrderPathNode(0, 0)));

        Assert.Null(submitted);
        Assert.NotNull(rejection);
        Assert.Equal(0, rejection!.OrderId);
        Assert.Equal(OrderRejectReason.InvalidNodeCount, rejection.Reason);
        Assert.Empty(queueAfterRejection.Orders);

        // Design section 16 says the rejection event "carries the order
        // id" — an id had to be assigned to carry, even though the order
        // never entered Orders. Both counters advance exactly as an
        // accepted Submit would advance them, so the next accepted
        // submission draws OrderId 1, not the rejected attempt's 0 again.
        Assert.Equal(1, queueAfterRejection.NextOrderId);
        Assert.Equal(1, queueAfterRejection.NextOrderSequence);

        var (queueAfterAcceptance, secondSubmitted, secondRejection) = queueAfterRejection.SubmitValidated(
            targetTick: 6,
            factionId: 0,
            ImmutableArray<ulong>.Empty,
            OrderKind.Hold,
            grid,
            wallBuckets);

        Assert.Null(secondRejection);
        Assert.NotNull(secondSubmitted);
        Assert.Equal(1, secondSubmitted!.OrderId);
        Assert.Equal(1, secondSubmitted.OrderSequence);
        Assert.Single(queueAfterAcceptance.Orders);
    }

    /// <summary>
    /// The accepted path through <see cref="OrderQueue.SubmitValidated"/>
    /// must behave exactly like the existing, unmodified <see cref="OrderQueue.Submit"/>
    /// — same dense id assignment, same verbatim path storage — so a caller
    /// migrating from one to the other sees no behavioural change for a
    /// valid order.
    /// </summary>
    [Fact]
    public void SubmitValidated_AcceptedMoveAlongPath_StoresOrderIdenticallyToSubmit()
    {
        var grid = NewOpenGrid();
        var wallBuckets = NoWalls(grid);
        var nodes = ImmutableArray.Create(new OrderPathNode(0, 0), new OrderPathNode(4, 0));

        var (queue, submitted, rejection) = OrderQueue.Empty.SubmitValidated(
            targetTick: 3,
            factionId: 1,
            ImmutableArray<ulong>.Empty,
            OrderKind.MoveAlongPath,
            grid,
            wallBuckets,
            nodes);

        Assert.Null(rejection);
        Assert.NotNull(submitted);
        Assert.Equal(0, submitted!.OrderId);
        Assert.Equal(0, submitted.OrderSequence);
        Assert.Equal(nodes, submitted.PathNodes);
        Assert.Single(queue.Orders);
        Assert.Equal(submitted, queue.Orders[0]);
    }
}
