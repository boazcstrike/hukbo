using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Sandata.Client.UI;
using Sandata.Core.Maps;
using Sandata.Core.Navigation;
using Sandata.Core.Orders;
using Sandata.Core.Rules;
using Sandata.Core.Simulation;

namespace Sandata.Client.Tests;

/// <summary>
/// Task 62 of docs/plans/2026-08-07-sandata-scaffold.md: the hand-drawn path
/// tool (<see cref="PathDrawTool"/>), the go-code panel
/// (<see cref="GoCodePanel"/>), and the order queue view
/// (<see cref="OrderQueueView"/>). Design section 16's "Undo, multi-select,
/// and drag capture stay presentation-only" is the load-bearing property:
/// this file both proves it holds for <see cref="UndoStack{T}"/> as used by
/// <see cref="PathDrawTool"/>, and proves the technique used to prove it
/// would actually catch a violation.
/// </summary>
public sealed class PathDrawToolTests
{
    // ---- shared fixtures, matching OrderQueueTests/OrderValidationTests ----

    private static NavGrid NewOpenGrid(int widthCells = 20, int heightCells = 20)
    {
        var grid = new NavGrid(widthCells, heightCells);
        Array.Fill(grid.Passability, NavCellFlags.Open);
        return grid;
    }

    private static WallBuckets NoWalls(NavGrid grid) => WallBuckets.Build(grid, [], [], [], []);

    /// <summary>
    /// The smallest valid <see cref="SandataSimulation"/> a test can submit
    /// an order through — one operator per faction (the floor
    /// <see cref="Mission"/>'s own constructor requires), an empty
    /// <see cref="MissionState.Operators"/> list (nothing here reads it), and
    /// the map geometry a caller already built via <see cref="NewOpenGrid"/>/
    /// <see cref="NoWalls"/>. Mirrors
    /// <c>Sandata.Headless.HeadlessRunner.BuildMission</c>'s own shape, the
    /// one existing production template for constructing a
    /// <see cref="Mission"/>.
    /// </summary>
    private static SandataSimulation NewSimulation(NavGrid grid, WallBuckets wallBuckets)
    {
        var mission = new Mission(
            formatVersion: Mission.CurrentFormatVersion,
            seed: 1UL,
            mapContentHash: 1UL,
            tickPolicy: new MissionTickPolicy(TickLimit: 100, StateHashCadenceTicks: 1),
            factionSetups: ImmutableArray.Create(
                new MissionFactionSetup(FactionId: 0, OperatorCount: 1),
                new MissionFactionSetup(FactionId: 1, OperatorCount: 1)),
            rulesetId: SandataPresetId.ModernTacticalV1);

        var initialState = new MissionState(Tick: 0, Phase: 1, Winner: -1, NextEntityId: 1, NextEventSequence: 0);

        return new SandataSimulation(mission, SandataRuleset.ModernTacticalV1, grid, wallBuckets, initialState);
    }

    // ==================== PathDrawTool: submission ====================

    /// <summary>
    /// A drawn node list submits as exactly one <see cref="OrderKind.MoveAlongPath"/>
    /// order, and the stored addressees come back in ascending order even
    /// though the caller supplied them unsorted — <see cref="OrderQueue"/>'s
    /// own sorting, exercised end to end through <see cref="PathDrawTool.Submit"/>.
    /// </summary>
    [Fact]
    public void Submit_ProducesExactlyOneMoveAlongPathOrderWithAscendingAddressees()
    {
        var grid = NewOpenGrid();
        var wallBuckets = NoWalls(grid);
        var simulation = NewSimulation(grid, wallBuckets);

        var state = PathDrawState.CreateEmpty();
        state = PathDrawTool.AddNode(state, new DrawnPathNode(0, 0));
        state = PathDrawTool.AddNode(state, new DrawnPathNode(4, 0));

        var unsortedAddressees = ImmutableArray.Create<ulong>(30, 5, 100, 2, 17);

        var (_, submitted, rejection) = PathDrawTool.Submit(
            state, simulation, targetTick: 10, factionId: 0, unsortedAddressees);

        Assert.Null(rejection);
        Assert.NotNull(submitted);
        var storedOrder = Assert.Single(simulation.State.OrderQueue.Orders);
        Assert.Equal(OrderKind.MoveAlongPath, submitted!.Kind);
        Assert.Equal(new ulong[] { 2, 5, 17, 30, 100 }, submitted.Addressees.ToArray());
        Assert.Equal(submitted, storedOrder);
    }

    // ==================== PathDrawTool: undo ====================

    /// <summary>Undo removes the LAST unsubmitted node, not the first or an arbitrary one.</summary>
    [Fact]
    public void Undo_RemovesTheLastUnsubmittedNode()
    {
        var state = PathDrawState.CreateEmpty();
        state = PathDrawTool.AddNode(state, new DrawnPathNode(0, 0));
        state = PathDrawTool.AddNode(state, new DrawnPathNode(4, 0));
        state = PathDrawTool.AddNode(state, new DrawnPathNode(8, 0));

        var afterUndo = PathDrawTool.Undo(state);

        Assert.Equal(
            [new DrawnPathNode(0, 0), new DrawnPathNode(4, 0)],
            afterUndo.Nodes.ToArray());
        Assert.Equal(2, afterUndo.Undo.Depth);
    }

    /// <summary>Undoing an empty drawing session is a strict no-op, never an exception.</summary>
    [Fact]
    public void Undo_OnAnEmptySession_IsANoOp()
    {
        var state = PathDrawState.CreateEmpty();

        var afterUndo = PathDrawTool.Undo(state);

        Assert.Empty(afterUndo.Nodes);
        Assert.Equal(0, afterUndo.Undo.Depth);
    }

    /// <summary>
    /// Undo cannot remove a node that has already been submitted. After
    /// <see cref="PathDrawTool.Submit"/>, the returned state's node list and
    /// undo stack are empty, so a subsequent <see cref="PathDrawTool.Undo"/>
    /// call is a strict no-op, and the order already stored in the queue is
    /// untouched by it.
    /// </summary>
    /// <remarks>
    /// This is the fixture that catches "a wrong implementation that pops
    /// blindly": an implementation of <see cref="PathDrawTool.Submit"/> that
    /// clears the node list but forgets to hand back a fresh, empty
    /// <see cref="UndoStack{T}"/> — reusing the same live stack instance
    /// instead — would still have three entries left on it here.
    /// <see cref="PathDrawTool.Undo"/> would then successfully pop one of
    /// them and try to remove the corresponding index out of an already-empty
    /// node list, throwing; a version that instead special-cased "nodes
    /// already empty" to silently swallow that pop would still leave
    /// <c>afterUndo.Undo.Depth</c> at 2 instead of the 0 this test requires.
    /// Either way, this test fails against that implementation.
    /// </remarks>
    [Fact]
    public void Undo_AfterSubmission_LeavesTheSubmittedOrderUntouched()
    {
        var grid = NewOpenGrid();
        var wallBuckets = NoWalls(grid);
        var simulation = NewSimulation(grid, wallBuckets);

        var state = PathDrawState.CreateEmpty();
        state = PathDrawTool.AddNode(state, new DrawnPathNode(0, 0));
        state = PathDrawTool.AddNode(state, new DrawnPathNode(4, 0));
        state = PathDrawTool.AddNode(state, new DrawnPathNode(8, 0));

        var (postSubmitState, submitted, rejection) = PathDrawTool.Submit(
            state, simulation, targetTick: 1, factionId: 0, ImmutableArray.Create<ulong>(5, 1, 3));

        Assert.Null(rejection);
        Assert.NotNull(submitted);
        Assert.Equal(3, submitted!.PathNodes.Length);

        var afterUndo = PathDrawTool.Undo(postSubmitState);

        Assert.Empty(afterUndo.Nodes);
        Assert.Equal(0, afterUndo.Undo.Depth);

        var storedOrder = Assert.Single(simulation.State.OrderQueue.Orders);
        Assert.Equal(3, storedOrder.PathNodes.Length);
        Assert.Equal(submitted.PathNodes, storedOrder.PathNodes);
    }

    // ==================== Boundary: Sandata.Core reachability ====================

    /// <summary>
    /// No <c>Sandata.Core</c> type is reachable from <see cref="UndoStack{DrawnPathNode}"/>
    /// as <see cref="PathDrawTool"/> actually uses it — walking field types,
    /// property types, and generic arguments recursively, not merely
    /// checking a <c>using</c> directive or an assembly reference. A `using`
    /// check would pass even if <c>UndoStack&lt;T&gt;</c> secretly declared a
    /// field of a <c>Sandata.Core</c> type, as long as nothing else in the
    /// same file happened to reference <c>Sandata.Core</c> by name; walking
    /// the actual type graph catches that regardless of what the file's
    /// `using` list says.
    /// </summary>
    [Fact]
    public void NoSandataCoreTypeIsReachableFromTheUndoStack()
    {
        var reachesCore = TypeGraphContains(typeof(UndoStack<DrawnPathNode>), IsSandataCoreType);

        Assert.False(reachesCore);
    }

    /// <summary>
    /// Positive control for <see cref="NoSandataCoreTypeIsReachableFromTheUndoStack"/>:
    /// proves the walker used above can actually find a reachable
    /// <c>Sandata.Core</c> type when one is really there, so the fact above
    /// is not vacuously true because the walker never descends into
    /// anything. <see cref="OperatorInspector.InspectorContent"/> carries
    /// <c>Sandata.Core.Navigation.PathReasonCode</c> and
    /// <c>Sandata.Core.Combat.CoverState</c> fields directly.
    /// </summary>
    [Fact]
    public void TheWalkerDoesFindASandataCoreTypeFromAStructThatActuallyCarriesOne()
    {
        var reachesCore = TypeGraphContains(typeof(OperatorInspector.InspectorContent), IsSandataCoreType);

        Assert.True(reachesCore);
    }

    /// <summary>
    /// The "invisible to the simulation" property stated positively: no
    /// member (method, constructor) declared anywhere in the
    /// <c>Sandata.Core</c> assembly can accept or return a value that is, or
    /// contains, a <see cref="DrawnPathNode"/>. A drawn-but-unsubmitted path
    /// therefore has no <c>Sandata.Core</c> API that could even be handed
    /// one — not merely that none happens to be called with one today.
    /// </summary>
    [Fact]
    public void NoSandataCoreMemberCanConsumeOrProduceAnUnsubmittedDrawnPathNode()
    {
        const BindingFlags allDeclaredMembers =
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
            BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        var coreAssembly = typeof(Order).Assembly;

        var offendingMembers = coreAssembly.GetTypes()
            .Where(IsSandataCoreType)
            .SelectMany(type => type.GetMethods(allDeclaredMembers)
                .Cast<MethodBase>()
                .Concat(type.GetConstructors(allDeclaredMembers)))
            .Where(member =>
                member.GetParameters().Any(parameter =>
                    TypeGraphContains(parameter.ParameterType, t => t == typeof(DrawnPathNode))) ||
                (member is MethodInfo methodInfo &&
                    TypeGraphContains(methodInfo.ReturnType, t => t == typeof(DrawnPathNode))))
            .Select(member => $"{member.DeclaringType}.{member.Name}")
            .ToArray();

        Assert.Empty(offendingMembers);
    }

    private static bool IsSandataCoreType(Type type) =>
        (type.Namespace ?? string.Empty).StartsWith("Sandata.Core", StringComparison.Ordinal);

    /// <summary>
    /// Recursively walks a type's reachable type graph — the type itself,
    /// its generic arguments, its array element type, its own declared
    /// fields and properties (descended into only when the type belongs to
    /// this codebase, so the walk does not wander off into the base class
    /// library), and its base type — testing every type it visits against
    /// <paramref name="predicate"/>.
    /// </summary>
    private static bool TypeGraphContains(Type root, Func<Type, bool> predicate, HashSet<Type>? visited = null)
    {
        visited ??= new HashSet<Type>();
        if (!visited.Add(root))
        {
            return false;
        }

        if (predicate(root))
        {
            return true;
        }

        if (root.IsGenericType)
        {
            foreach (var argument in root.GetGenericArguments())
            {
                if (TypeGraphContains(argument, predicate, visited))
                {
                    return true;
                }
            }
        }

        if (root.IsArray && root.GetElementType() is { } elementType &&
            TypeGraphContains(elementType, predicate, visited))
        {
            return true;
        }

        if (!ShouldDescendMembersOf(root))
        {
            return false;
        }

        const BindingFlags allDeclaredMembers =
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
            BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        foreach (var field in root.GetFields(allDeclaredMembers))
        {
            if (TypeGraphContains(field.FieldType, predicate, visited))
            {
                return true;
            }
        }

        foreach (var property in root.GetProperties(allDeclaredMembers))
        {
            if (TypeGraphContains(property.PropertyType, predicate, visited))
            {
                return true;
            }
        }

        if (root.BaseType is { } baseType && baseType != typeof(object) && baseType != typeof(ValueType) &&
            TypeGraphContains(baseType, predicate, visited))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Restricts field/property descent to this codebase's own namespaces so
    /// the walk cannot wander into unrelated base class library internals
    /// while still following every <c>Sandata.*</c> or <c>Hukbo.*</c> edge.
    /// </summary>
    private static bool ShouldDescendMembersOf(Type type)
    {
        var ns = type.Namespace ?? string.Empty;
        return ns.StartsWith("Sandata.", StringComparison.Ordinal) ||
            ns.StartsWith("Hukbo.", StringComparison.Ordinal);
    }

    // ==================== GoCodePanel geometry ====================

    [Fact]
    public void GoCodePanel_CalculateBounds_IsNonEmptyForANormalWindow()
    {
        var windowBounds = new Rectangle(0, 0, 1920, 1080);

        var bounds = GoCodePanel.CalculateBounds(windowBounds, goCodeCount: 4);

        Assert.True(bounds.Width > 0);
        Assert.True(bounds.Height > 0);
    }

    [Fact]
    public void GoCodePanel_CalculateBounds_ClipsSanelyForATooSmallWindow()
    {
        var windowBounds = new Rectangle(0, 0, 10, 10);

        var bounds = GoCodePanel.CalculateBounds(windowBounds, goCodeCount: 4);

        Assert.True(bounds.Width >= 0);
        Assert.True(bounds.Height >= 0);
        Assert.True(bounds.Width <= windowBounds.Width);
        Assert.True(bounds.Height <= windowBounds.Height);
    }

    [Fact]
    public void GoCodePanel_FormatEntryLine_MarksReleasedEntries()
    {
        var released = new GoCodePanel.GoCodeEntry(Letter: 'A', OperatorCount: 3, IsReleased: true);
        var pending = new GoCodePanel.GoCodeEntry(Letter: 'B', OperatorCount: 2, IsReleased: false);

        Assert.Contains("released", GoCodePanel.FormatEntryLine(released), StringComparison.Ordinal);
        Assert.DoesNotContain("released", GoCodePanel.FormatEntryLine(pending), StringComparison.Ordinal);
    }

    // ==================== OrderQueueView geometry and rejection surfacing ====================

    [Fact]
    public void OrderQueueView_CalculateBounds_IsNonEmptyForANormalWindow()
    {
        var windowBounds = new Rectangle(0, 0, 1920, 1080);

        var bounds = OrderQueueView.CalculateBounds(windowBounds, entryCount: 5);

        Assert.True(bounds.Width > 0);
        Assert.True(bounds.Height > 0);
    }

    [Fact]
    public void OrderQueueView_CalculateBounds_ClipsSanelyForATooSmallWindow()
    {
        var windowBounds = new Rectangle(0, 0, 10, 10);

        var bounds = OrderQueueView.CalculateBounds(windowBounds, entryCount: 5);

        Assert.True(bounds.Width >= 0);
        Assert.True(bounds.Height >= 0);
        Assert.True(bounds.Width <= windowBounds.Width);
        Assert.True(bounds.Height <= windowBounds.Height);
    }

    /// <summary>
    /// A rejected order surfaces its specific reason code, not merely a
    /// generic rejection marker: the exact <see cref="OrderRejectReason"/>
    /// value is retrievable from the entry, and appears in its formatted
    /// line.
    /// </summary>
    [Fact]
    public void OrderQueueView_RejectedEntry_ExposesTheSpecificReasonCode()
    {
        var rejection = new OrderRejection(OrderId: 3, Reason: OrderRejectReason.SegmentCrossesWall);

        var entry = OrderQueueView.FromRejection(rejection, OrderKind.MoveAlongPath, targetTick: 12);

        Assert.True(entry.IsRejected);
        Assert.Equal(OrderRejectReason.SegmentCrossesWall, entry.RejectReason);
        Assert.Contains(
            "SegmentCrossesWall", OrderQueueView.FormatEntryLine(entry), StringComparison.Ordinal);
    }

    [Fact]
    public void OrderQueueView_RejectedEntry_ReportsADifferentReasonWhenRejectedForADifferentRule()
    {
        var rejection = new OrderRejection(OrderId: 4, Reason: OrderRejectReason.NodeOutsideMapBounds);

        var entry = OrderQueueView.FromRejection(rejection, OrderKind.MoveAlongPath, targetTick: 12);

        Assert.Equal(OrderRejectReason.NodeOutsideMapBounds, entry.RejectReason);
        Assert.NotEqual(OrderRejectReason.SegmentCrossesWall, entry.RejectReason);
    }

    [Fact]
    public void OrderQueueView_AcceptedEntry_CarriesNoRejectReason()
    {
        var order = new Order(OrderId: 1, OrderSequence: 1, TargetTick: 5, FactionId: 0, Kind: OrderKind.Hold);

        var entry = OrderQueueView.FromSubmittedOrder(order);

        Assert.False(entry.IsRejected);
        Assert.Null(entry.RejectReason);
    }

    /// <summary>
    /// A rejection produced by an actual <see cref="OrderQueue.SubmitValidated"/>
    /// call, driven through <see cref="PathDrawTool.Submit"/>, surfaces
    /// correctly in the queue view — end to end, not only against a
    /// hand-built <see cref="OrderRejection"/>.
    /// </summary>
    [Fact]
    public void OrderQueueView_SurfacesAReasonCodeFromAnActualPathDrawToolRejection()
    {
        // Grid is 20x20 cells * CellSizeWu(4) = 80x80 wu, so x = 81 is one
        // world unit past the closed bounds interval.
        var grid = NewOpenGrid();
        var wallBuckets = NoWalls(grid);
        var simulation = NewSimulation(grid, wallBuckets);

        var state = PathDrawState.CreateEmpty();
        state = PathDrawTool.AddNode(state, new DrawnPathNode(0, 0));
        state = PathDrawTool.AddNode(state, new DrawnPathNode(81, 0));

        var (_, submitted, rejection) = PathDrawTool.Submit(
            state, simulation, targetTick: 1, factionId: 0, ImmutableArray.Create<ulong>(1));

        Assert.Null(submitted);
        Assert.NotNull(rejection);
        Assert.Equal(OrderRejectReason.NodeOutsideMapBounds, rejection!.Reason);

        var entry = OrderQueueView.FromRejection(rejection, OrderKind.MoveAlongPath, targetTick: 1);

        Assert.Equal(OrderRejectReason.NodeOutsideMapBounds, entry.RejectReason);
    }
}
