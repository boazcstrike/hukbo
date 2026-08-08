using System;
using Microsoft.Xna.Framework;
using Sandata.Client.Theming;
using Sandata.Core.Orders;

namespace Sandata.Client.UI;

/// <summary>
/// Layout and content helpers for the order queue view: the queued orders a
/// spectator has submitted, and — design section 16 — "rejection is
/// observable," so a rejected order's specific
/// <see cref="OrderRejectReason"/> is retrievable here, not merely a generic
/// rejection marker.
/// </summary>
/// <remarks>
/// Design section 11's HUD element list has no row for this view; it
/// predates the order layer design in section 16. This file only computes
/// this view's own bounds and content, in the
/// <c>CalculateBounds(windowBounds, ...)</c> shape the other HUD panels
/// already use. Task 71 owns composing it into the HUD layout and amending
/// section 11.
/// </remarks>
internal static class OrderQueueView
{
    /// <summary>Distance in pixels this view keeps from the window edge, and between rows and its border.</summary>
    internal const int Margin = 12; // PROVISIONAL: matches OperatorInspector.Margin; no design value given.

    /// <summary>Fixed view width in pixels.</summary>
    internal const int Width = 260; // PROVISIONAL: no design value given.

    /// <summary>Height in pixels reserved for the view's title row.</summary>
    internal const int HeaderHeight = 20; // PROVISIONAL: matches ContactList.HeaderHeight.

    /// <summary>Height in pixels of one queued-order row.</summary>
    internal const int RowHeight = 18; // PROVISIONAL: one line of text plus a small margin.

    /// <summary>The largest number of order rows this view ever reserves height for at once.</summary>
    internal const int MaxVisibleRows = 12; // PROVISIONAL: no design value given.

    /// <summary>
    /// One entry in the queue view: an accepted order (<see cref="RejectReason"/>
    /// is <see langword="null"/>) or a rejected one (<see cref="IsRejected"/>
    /// is <see langword="true"/> and <see cref="RejectReason"/> carries the
    /// specific reason it failed <see cref="OrderValidation"/>).
    /// </summary>
    internal readonly record struct Entry(
        long OrderId,
        OrderKind Kind,
        long TargetTick,
        bool IsRejected,
        OrderRejectReason? RejectReason);

    /// <summary>Builds a queue entry from an order <see cref="OrderQueue.SubmitValidated"/> accepted.</summary>
    internal static Entry FromSubmittedOrder(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);
        return new Entry(order.OrderId, order.Kind, order.TargetTick, IsRejected: false, RejectReason: null);
    }

    /// <summary>
    /// Builds a queue entry from a rejection <see cref="OrderQueue.SubmitValidated"/>
    /// returned. <paramref name="kind"/> and <paramref name="targetTick"/> come
    /// from the caller because <see cref="OrderRejection"/> itself carries
    /// only the order id and the reason.
    /// </summary>
    internal static Entry FromRejection(OrderRejection rejection, OrderKind kind, long targetTick)
    {
        ArgumentNullException.ThrowIfNull(rejection);
        return new Entry(rejection.OrderId, kind, targetTick, IsRejected: true, RejectReason: rejection.Reason);
    }

    /// <summary>
    /// Computes this view's screen-space bounds, anchored to the window's
    /// bottom-right corner and clamped so it never exceeds the window on
    /// either axis. Task 71 may re-anchor it once it is composed alongside
    /// the other HUD panels; this is this view's own, independent placement.
    /// </summary>
    internal static Rectangle CalculateBounds(Rectangle windowBounds, int entryCount)
    {
        var visibleRows = Math.Clamp(entryCount, 0, MaxVisibleRows);
        var preferredHeight = HeaderHeight + (visibleRows * RowHeight) + (Margin * 2);
        var maxHeight = Math.Max(0, windowBounds.Height - (Margin * 2));
        var height = Math.Min(preferredHeight, maxHeight);

        var maxWidth = Math.Max(0, windowBounds.Width - (Margin * 2));
        var width = Math.Min(Width, maxWidth);

        return new Rectangle(
            windowBounds.Right - Margin - width,
            windowBounds.Bottom - Margin - height,
            width,
            height);
    }

    /// <summary>Computes the bounds of one row within a view already positioned by <see cref="CalculateBounds"/>.</summary>
    internal static Rectangle CalculateRowBounds(Rectangle viewBounds, int rowIndex) =>
        new(
            viewBounds.Left,
            viewBounds.Top + HeaderHeight + (rowIndex * RowHeight),
            viewBounds.Width,
            RowHeight);

    /// <summary>Formats one entry as the single line of text its row displays.</summary>
    internal static string FormatEntryLine(Entry entry) =>
        entry.IsRejected
            ? $"#{entry.OrderId} {entry.Kind} rejected: {entry.RejectReason}"
            : $"#{entry.OrderId} {entry.Kind} @t{entry.TargetTick}";

    /// <summary>Resolves the semantic theme color for one entry's row.</summary>
    internal static Color ResolveEntryColor(SandataThemeColors colors, Entry entry) =>
        entry.IsRejected ? colors.StatusDanger : colors.TextPrimary;
}
