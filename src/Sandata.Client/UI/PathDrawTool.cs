using System;
using System.Collections.Immutable;
using System.Linq;
using Sandata.Core.Orders;
using Sandata.Core.Simulation;

namespace Sandata.Client.UI;

/// <summary>
/// One node of an in-progress hand-drawn path, before it has ever been
/// submitted as an order.
/// </summary>
/// <remarks>
/// Deliberately its own primitive type rather than
/// <see cref="OrderPathNode"/> from <c>Sandata.Core.Orders</c>. Design
/// section 16, "Undo, multi-select, and drag capture stay
/// presentation-only": "Nothing about them crosses into
/// <c>Sandata.Core</c>." <see cref="PathDrawTool.Submit"/> is the one place
/// this type is ever converted to <see cref="OrderPathNode"/>, at the moment
/// of submission and nowhere else; <see cref="UndoStack{T}"/>, when built
/// over this type, never touches a <c>Sandata.Core</c> value at all.
/// </remarks>
internal readonly record struct DrawnPathNode(long X, long Y);

/// <summary>
/// The state one hand-drawn path holds before submission: the nodes drawn so
/// far, in drawing order, and the undo stack (task 46's
/// <see cref="UndoStack{T}"/>) that lets the last of them be taken back.
/// </summary>
/// <remarks>
/// <para>
/// This type holds both an <see cref="ImmutableArray{T}"/> and a reference to
/// a mutable <see cref="UI.UndoStack{T}"/>, so — like <c>MultiSelectState</c>
/// — it hand-writes <see cref="Equals(PathDrawState)"/> and
/// <see cref="GetHashCode"/> to compare <see cref="Nodes"/> structurally
/// rather than by the backing array's reference, the same trap
/// <c>MultiSelectState</c>'s own remarks document for
/// <see cref="ImmutableArray{T}"/>. <see cref="Undo"/> is compared by
/// reference deliberately: it is a mutable object with its own identity, not
/// a value, exactly like task 46 designed it.
/// </para>
/// <para>
/// <b>Single-owner discipline, matching <c>DragCapture</c>'s own remarks.</b>
/// <see cref="PathDrawTool.AddNode"/>, <see cref="PathDrawTool.Undo"/>, and
/// <see cref="PathDrawTool.Submit"/> each return a new
/// <see cref="PathDrawState"/> value, but the undo half of that value is
/// backed by one mutable <see cref="UI.UndoStack{T}"/> instance — task 46
/// already designed push/pop as a stateful operation, not a value
/// replacement. A caller is expected to hold one state across frames and
/// always call these methods against the value it was last handed, the same
/// discipline <c>DragCapture</c>'s own remarks already require: calling
/// <see cref="PathDrawTool.Undo"/> against a stale, already-superseded copy
/// re-pops the same live stack a second time rather than restoring history
/// that no longer exists.
/// </para>
/// </remarks>
internal readonly struct PathDrawState : IEquatable<PathDrawState>
{
    internal PathDrawState(ImmutableArray<DrawnPathNode> nodes, UndoStack<DrawnPathNode> undo)
    {
        Nodes = nodes;
        Undo = undo;
    }

    /// <summary>The nodes drawn so far, in drawing order, first to last.</summary>
    internal ImmutableArray<DrawnPathNode> Nodes { get; }

    /// <summary>The undo stack backing this drawing session.</summary>
    internal UndoStack<DrawnPathNode> Undo { get; }

    /// <summary>A fresh drawing session with no nodes and an empty undo stack.</summary>
    internal static PathDrawState CreateEmpty(int undoDepthLimit = UndoStack<DrawnPathNode>.DefaultDepthLimit) =>
        new(ImmutableArray<DrawnPathNode>.Empty, new UndoStack<DrawnPathNode>(undoDepthLimit));

    /// <inheritdoc/>
    public bool Equals(PathDrawState other) =>
        ReferenceEquals(Undo, other.Undo) && NodesSpan.SequenceEqual(other.NodesSpan);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PathDrawState other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(Undo);
        foreach (var node in NodesSpan)
        {
            hash.Add(node);
        }

        return hash.ToHashCode();
    }

    private ReadOnlySpan<DrawnPathNode> NodesSpan =>
        Nodes.IsDefault ? ReadOnlySpan<DrawnPathNode>.Empty : Nodes.AsSpan();
}

/// <summary>
/// The hand-drawn path tool: a set of pure state transitions over
/// <see cref="PathDrawState"/>, built on task 46's <see cref="MultiSelectState"/>,
/// <see cref="DragCapture"/>, and <see cref="UndoStack{T}"/>. Design section 16.
/// </summary>
/// <remarks>
/// Every member here is a pure function from one <see cref="PathDrawState"/>
/// (and, for <see cref="Submit"/>, an <see cref="OrderQueue"/>) to a new one.
/// A caller — task 71's composer — owns the current <see cref="PathDrawState"/>
/// and reassigns it after every call, the same pattern <c>DragCapture</c>
/// already establishes for this UI layer. Nothing in this type touches a
/// graphics device, a window, the wall clock, the filesystem, or the network.
/// </remarks>
internal static class PathDrawTool
{
    /// <summary>
    /// Appends <paramref name="node"/> to the drawn path and records it on
    /// the undo stack. The entry point task 71 routes a pointer-down event
    /// into, after converting the pointer's screen position to a world
    /// position (for example via <c>SandataCamera.ScreenToWorld</c>) and
    /// building a <see cref="DrawnPathNode"/> from it — this method takes no
    /// screen-space input itself, so it stays free of any rendering
    /// dependency.
    /// </summary>
    internal static PathDrawState AddNode(PathDrawState state, DrawnPathNode node)
    {
        state.Undo.Push(node);
        return new PathDrawState(state.Nodes.Add(node), state.Undo);
    }

    /// <summary>
    /// Removes the last unsubmitted node, if any. The undo stack is the sole
    /// source of truth for whether there is anything left to undo: a state
    /// produced by <see cref="Submit"/> carries a freshly created, empty
    /// undo stack, so calling this against a post-submission state is
    /// always a strict no-op — there is nothing in it for
    /// <see cref="UndoStack{T}.TryPop"/> to find, so it can never reach back
    /// into a node that already left this tool's ownership as part of a
    /// submitted order.
    /// </summary>
    internal static PathDrawState Undo(PathDrawState state)
    {
        if (!state.Undo.TryPop(out _))
        {
            return state;
        }

        return new PathDrawState(state.Nodes.RemoveAt(state.Nodes.Length - 1), state.Undo);
    }

    /// <summary>
    /// Converts a drawn path to the <c>Sandata.Core.Orders</c> node shape.
    /// The only place in this file <see cref="DrawnPathNode"/> is ever turned
    /// into an <see cref="OrderPathNode"/>.
    /// </summary>
    internal static ImmutableArray<OrderPathNode> ToOrderPathNodes(ImmutableArray<DrawnPathNode> nodes) =>
        nodes.IsDefaultOrEmpty
            ? ImmutableArray<OrderPathNode>.Empty
            : nodes.Select(node => new OrderPathNode(node.X, node.Y)).ToImmutableArray();

    /// <summary>
    /// Submits the drawn path as one <see cref="OrderKind.MoveAlongPath"/>
    /// order through <see cref="SandataSimulation.SubmitOrder"/> — the one
    /// production door into <see cref="OrderQueue"/>, which also emits
    /// <see cref="Sandata.Core.Events.MissionEventKind.OrderRejected"/> on
    /// rejection — and returns a fresh, empty drawing state regardless of
    /// whether the order is accepted or rejected. <paramref name="addressees"/>
    /// need not already be sorted: <see cref="OrderQueue"/> stores them in
    /// ascending order.
    /// </summary>
    internal static (PathDrawState State, Order? Submitted, OrderRejection? Rejection) Submit(
        PathDrawState state,
        SandataSimulation simulation,
        long targetTick,
        int factionId,
        ImmutableArray<ulong> addressees)
    {
        ArgumentNullException.ThrowIfNull(simulation);

        var pathNodes = ToOrderPathNodes(state.Nodes);

        var (_, submitted, rejection) = simulation.SubmitOrder(
            targetTick, factionId, addressees, OrderKind.MoveAlongPath, pathNodes);

        return (PathDrawState.CreateEmpty(state.Undo.DepthLimit), submitted, rejection);
    }
}
