using System.Collections.Immutable;
using System.Reflection;
using Sandata.Core.Navigation;
using Sandata.Core.Orders;

namespace Sandata.Core.Tests;

/// <summary>
/// Task 60 of Sandata's scaffold plan: <c>GoCodeRules</c>
/// against design section 16, "Sync sets and go-codes" — "releasing that
/// letter is itself an order... A keypress therefore enters the same queue
/// as everything else and gets the same determinism guarantee for free. No
/// separate input path reaches the simulation."
/// </summary>
public sealed class GoCodeRulesTests
{
    /// <summary>
    /// The core property: a <see cref="OrderKind.GoCodeRelease"/> order
    /// submitted through <see cref="OrderQueue.SubmitValidated"/> is
    /// stored identically to a <see cref="OrderKind.Hold"/> order with the
    /// same schedule and addressees, once the one field that legitimately
    /// differs — <see cref="Order.Kind"/> — is normalized away. A wrong
    /// implementation with a side channel (an extra hidden field, a
    /// different addressee-sorting rule, a different default
    /// <see cref="Order.PathNodes"/>) fails this equality.
    /// </summary>
    [Fact]
    public void SubmitValidated_GoCodeReleaseAndHold_StoreIdenticallyOnceKindIsNormalized()
    {
        var grid = NewOpenGrid();
        var wallBuckets = NoWalls(grid);
        var addressees = ImmutableArray.Create<ulong>(30, 5, 100, 2, 17);

        var (_, goCodeOrder, goCodeRejection) = OrderQueue.Empty.SubmitValidated(
            targetTick: 12, factionId: 1, addressees, OrderKind.GoCodeRelease, grid, wallBuckets);
        var (_, holdOrder, holdRejection) = OrderQueue.Empty.SubmitValidated(
            targetTick: 12, factionId: 1, addressees, OrderKind.Hold, grid, wallBuckets);

        Assert.Null(goCodeRejection);
        Assert.Null(holdRejection);
        Assert.NotNull(goCodeOrder);
        Assert.NotNull(holdOrder);

        Assert.Equal(holdOrder, goCodeOrder! with { Kind = OrderKind.Hold });
    }

    /// <summary>
    /// The applied-order position of a <see cref="OrderKind.GoCodeRelease"/>
    /// order depends only on <c>(TargetTick, OrderSequence)</c>, exactly as
    /// it would for any other kind — swapping which of two same-tick orders
    /// is the <see cref="OrderKind.GoCodeRelease"/> does not change which
    /// position either one lands in under
    /// <see cref="OrderQueue.InApplicationOrder"/>.
    /// </summary>
    [Fact]
    public void InApplicationOrder_GoCodeReleasePosition_DependsOnlyOnTargetTickAndOrderSequence_NotOnKind()
    {
        var grid = NewOpenGrid();
        var wallBuckets = NoWalls(grid);

        var queueGoCodeFirst = OrderQueue.Empty;
        (queueGoCodeFirst, var goCodeFirst, _) = queueGoCodeFirst.SubmitValidated(
            targetTick: 7, factionId: 0, ImmutableArray<ulong>.Empty, OrderKind.GoCodeRelease, grid, wallBuckets);
        (queueGoCodeFirst, var holdSecond, _) = queueGoCodeFirst.SubmitValidated(
            targetTick: 7, factionId: 0, ImmutableArray<ulong>.Empty, OrderKind.Hold, grid, wallBuckets);

        var queueHoldFirst = OrderQueue.Empty;
        (queueHoldFirst, var holdFirst, _) = queueHoldFirst.SubmitValidated(
            targetTick: 7, factionId: 0, ImmutableArray<ulong>.Empty, OrderKind.Hold, grid, wallBuckets);
        (queueHoldFirst, var goCodeSecond, _) = queueHoldFirst.SubmitValidated(
            targetTick: 7, factionId: 0, ImmutableArray<ulong>.Empty, OrderKind.GoCodeRelease, grid, wallBuckets);

        var appliedGoCodeFirst = queueGoCodeFirst.InApplicationOrder();
        var appliedHoldFirst = queueHoldFirst.InApplicationOrder();

        // In both queues, submission order 1st-then-2nd is preserved by
        // InApplicationOrder because the two orders share a TargetTick and
        // OrderSequence is assigned in submission order — regardless of
        // which of the two carries OrderKind.GoCodeRelease.
        Assert.Equal(new[] { goCodeFirst, holdSecond }, appliedGoCodeFirst.ToArray());
        Assert.Equal(new[] { holdFirst, goCodeSecond }, appliedHoldFirst.ToArray());
    }

    /// <summary>
    /// <see cref="GoCodeRules.ReleasesOperator"/> answers "is this operator
    /// addressed" against a real applied order produced by
    /// <see cref="OrderQueue.SubmitValidated"/>, exercising the binary
    /// search against the ascending <see cref="Order.Addressees"/> that
    /// storage actually produces rather than a hand-built literal.
    /// </summary>
    [Fact]
    public void ReleasesOperator_TrueForAnAddressedEntity_FalseForOneNotAddressed()
    {
        var grid = NewOpenGrid();
        var wallBuckets = NoWalls(grid);
        var addressees = ImmutableArray.Create<ulong>(30, 5, 100, 2, 17);

        var (_, order, rejection) = OrderQueue.Empty.SubmitValidated(
            targetTick: 1, factionId: 0, addressees, OrderKind.GoCodeRelease, grid, wallBuckets);

        Assert.Null(rejection);
        Assert.NotNull(order);

        Assert.True(GoCodeRules.ReleasesOperator(order!, 2));
        Assert.True(GoCodeRules.ReleasesOperator(order!, 100));
        Assert.False(GoCodeRules.ReleasesOperator(order!, 6));
        Assert.False(GoCodeRules.ReleasesOperator(order!, 0));
    }

    /// <summary>
    /// <see cref="GoCodeRules.ReleasesOperator"/> is narrowly scoped to
    /// <see cref="OrderKind.GoCodeRelease"/>, so passing any other kind is a
    /// caller error rather than a silent "false".
    /// </summary>
    [Fact]
    public void ReleasesOperator_NonGoCodeReleaseOrder_Throws()
    {
        var grid = NewOpenGrid();
        var wallBuckets = NoWalls(grid);
        var (_, order, rejection) = OrderQueue.Empty.SubmitValidated(
            targetTick: 1, factionId: 0, ImmutableArray.Create<ulong>(1), OrderKind.Hold, grid, wallBuckets);

        Assert.Null(rejection);
        Assert.NotNull(order);

        Assert.Throws<ArgumentException>(() => GoCodeRules.ReleasesOperator(order!, 1));
    }

    /// <summary>
    /// "Nothing about a go-code bypasses the queue — no second entry point
    /// exists in your files that produces a release without an order."
    /// Structural proof, in two parts: (1) <c>GoCodeRules.cs</c>'s own
    /// source never constructs an <c>Order</c> or touches
    /// <c>OrderQueue.Orders</c> directly, so nothing in this file can
    /// fabricate a release; and (2) <c>OrderQueue</c> itself exposes exactly
    /// one public instance method capable of adding an order to the queue —
    /// <c>SubmitValidated</c> — so no other door exists anywhere for this
    /// file, or any other, to walk through instead.
    /// </summary>
    [Fact]
    public void NoSecondEntryPointProducesAReleaseWithoutAnOrder()
    {
        var sourcePath = FindSourceFile("GoCodeRules.cs");
        var text = File.ReadAllText(sourcePath);

        Assert.DoesNotContain("new Order(", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Orders =", text, StringComparison.Ordinal);
        Assert.DoesNotContain(".Submit(", text, StringComparison.Ordinal);

        var orderProducingMethodNames = typeof(OrderQueue)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method =>
                !method.IsSpecialName && // excludes property accessors and the record's compiler-generated `<Clone>$`
                !method.Name.StartsWith('<') &&
                method.Name is not (nameof(OrderQueue.Equals) or nameof(OrderQueue.GetHashCode) or nameof(OrderQueue.ToString)) &&
                ReturnsAnOrderQueue(method.ReturnType))
            .Select(method => method.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[] { nameof(OrderQueue.SubmitValidated) },
            orderProducingMethodNames);
    }

    /// <summary>
    /// True when <paramref name="returnType"/> is <see cref="OrderQueue"/>
    /// itself, or a generic type (a tuple, in <c>OrderQueue</c>'s case) that
    /// names <see cref="OrderQueue"/> among its type arguments — the shape
    /// of "this method hands the caller a new or updated queue," which is
    /// exactly the capability "produces a release without an order" would
    /// need a second door for.
    /// </summary>
    private static bool ReturnsAnOrderQueue(Type returnType) =>
        returnType == typeof(OrderQueue) ||
        (returnType.IsGenericType && returnType.GetGenericArguments().Contains(typeof(OrderQueue)));

    private static string FindSourceFile(string fileName)
    {
        var root = Hukbo.Diagnostics.LogPaths.FindRepositoryRoot(AppContext.BaseDirectory);
        Assert.True(root is not null, "Could not locate the repository root to find " + fileName + ".");

        var path = Path.Combine(root!, "src", "Sandata.Core", "Orders", fileName);
        Assert.True(File.Exists(path), path + " does not exist.");

        return path;
    }

    private static NavGrid NewOpenGrid(int widthCells = 10, int heightCells = 10)
    {
        var grid = new NavGrid(width: widthCells, height: heightCells);
        Array.Fill(grid.Passability, NavCellFlags.Open);
        return grid;
    }

    private static WallBuckets NoWalls(NavGrid grid) => WallBuckets.Build(grid, [], [], [], []);
}
