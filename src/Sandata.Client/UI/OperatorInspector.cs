using System;
using System.Collections.Generic;
using Hukbo.Core.Mathematics;
using Microsoft.Xna.Framework;
using Sandata.Core.Combat;
using Sandata.Core.Navigation;
using Sandata.Core.Weapons;

namespace Sandata.Client.UI;

/// <summary>
/// The operator inspector, design section 11's HUD element list and design
/// section 16's order-layer addition. Pure layout and content-line helpers
/// only — no <c>SpriteBatch</c>, no font.
/// </summary>
/// <remarks>
/// <para>
/// Design section 5's tick-seam explanation is why this inspector shows both
/// <see cref="InspectorContent.DecisionPositionX"/>/<see cref="InspectorContent.DecisionPositionY"/>
/// (the frozen tick-start view an operator aimed or decided against, stages
/// 5-9) and <see cref="InspectorContent.ResolutionPositionX"/>/
/// <see cref="InspectorContent.ResolutionPositionY"/> (the committed state a
/// shot actually resolves against, stages 10-14): "a sprinting target
/// crossing a doorway can be missed by a shot that was correctly aimed 20 ms
/// earlier." Neither position is optional.
/// </para>
/// <para>
/// Design section 16 adds three order-layer rows — the active order id, the
/// node index currently being walked, and the reason code that cleared the
/// last assignment — so "an operator that stopped following a drawn path can
/// therefore be asked why it stopped, and it answers." The order layer itself
/// (tasks 57-63) has not landed: no <c>OrderId</c>, <c>OrderAssignment</c>, or
/// clear-reason enum exists yet in this worktree. These three rows are
/// therefore nullable primitives here rather than a not-yet-existent type,
/// per this task's own instruction to say so rather than invent the type.
/// </para>
/// <para>
/// <see cref="InspectorContent.Intent"/> is likewise the raw
/// <see langword="int"/> <c>OperatorState.Intent</c> already holds — task 44
/// owns the <c>OperatorIntent</c> enum, and it has not landed either.
/// <see cref="InspectorContent.GroupId"/> and <see cref="InspectorContent.SlotIndex"/>
/// are raw primitives rather than <c>Sandata.Core.Squads.SquadSlot</c>,
/// because that type is <see langword="internal"/> to <c>Sandata.Core</c> and
/// not visible from <c>Sandata.Client</c>.
/// </para>
/// </remarks>
internal static class OperatorInspector
{
    /// <summary>Distance from the window edge to the panel.</summary>
    internal const int Margin = 12;

    /// <summary>The panel's fixed width, before any window clamp.</summary>
    internal const int Width = 320;

    /// <summary>One content line's fixed height.</summary>
    internal const int LineHeight = 18;

    /// <summary>The number of lines <see cref="BuildLines"/> always returns.</summary>
    internal const int LineCount = 13;

    /// <summary>
    /// The name table the firearm row reads.
    /// </summary>
    /// <remarks>
    /// Design section 15 records "real weapon names versus generic aliases" as
    /// an open question, and nothing in <c>src/</c> selected a table before
    /// this row existed. Choosing <see cref="WeaponNameSetId.Generic"/> here
    /// does not settle that question: it is the conservative side of it, and
    /// it is one constant to change if the answer comes back the other way.
    /// </remarks>
    internal const WeaponNameSetId NameSet = WeaponNameSetId.Generic;

    /// <summary>
    /// The panel's bounding rectangle, anchored to the top-left corner of
    /// <paramref name="windowBounds"/>, clamped to fit inside it.
    /// </summary>
    internal static Rectangle CalculateBounds(Rectangle windowBounds)
    {
        var preferredHeight = (LineCount * LineHeight) + (Margin * 2);
        var height = Math.Min(preferredHeight, Math.Max(0, windowBounds.Height - (Margin * 2)));
        var width = Math.Min(Width, Math.Max(0, windowBounds.Width - (Margin * 2)));

        return new Rectangle(
            windowBounds.Left + Margin,
            windowBounds.Top + Margin,
            width,
            height);
    }

    /// <summary>
    /// Every field the operator inspector must show, per this task's own
    /// "Done when" list plus design section 16's three order-layer rows.
    /// </summary>
    /// <param name="Intent">
    /// The raw backing value for the not-yet-declared <c>OperatorIntent</c>
    /// enum (task 44).
    /// </param>
    /// <param name="ReasonCode">
    /// Why the operator's group currently holds the path it holds, or holds
    /// none — <see cref="PathReasonCode"/>.
    /// </param>
    /// <param name="ChainPhase">The operator's current weapon-chain phase.</param>
    /// <param name="ChainRemainingTicks">
    /// Ticks remaining in <paramref name="ChainPhase"/>, meaningless for a
    /// phase with no timer of its own (<see cref="WeaponChainPhase.Turning"/>).
    /// </param>
    /// <param name="Cover">The operator's cover affiliation, arc, and posture.</param>
    /// <param name="GroupId">
    /// The operator's derived group id (the minimum entity id in the group,
    /// design section 8) — raw <see langword="ulong"/> because
    /// <c>SquadSlot</c> is internal to <c>Sandata.Core</c>.
    /// </param>
    /// <param name="SlotIndex">
    /// The operator's derived slot index within its group, or
    /// <see langword="null"/> when the operator belongs to no group.
    /// </param>
    /// <param name="DecisionPositionX">
    /// The operator's X position from the frozen tick-start view — what a
    /// shot fired this tick was aimed against.
    /// </param>
    /// <param name="DecisionPositionY">The Y counterpart of <paramref name="DecisionPositionX"/>.</param>
    /// <param name="ResolutionPositionX">
    /// The operator's X position from committed state — what a shot fired
    /// this tick actually resolves against.
    /// </param>
    /// <param name="ResolutionPositionY">The Y counterpart of <paramref name="ResolutionPositionX"/>.</param>
    /// <param name="ActiveOrderId">
    /// The order id the operator's group is currently executing, or
    /// <see langword="null"/> when none is active. The order layer (tasks
    /// 57-63) has not landed, so this is a raw nullable <see langword="ulong"/>
    /// rather than an <c>OrderId</c> type.
    /// </param>
    /// <param name="OrderNodeIndex">
    /// The polyline node index the group is currently walking toward, or
    /// <see langword="null"/> when no order is active.
    /// </param>
    /// <param name="OrderClearReasonCode">
    /// The raw reason code that cleared the group's last order assignment, or
    /// <see langword="null"/> when no assignment has ever been cleared. The
    /// order layer's clear-reason enum has not landed, so this stays a raw
    /// nullable <see langword="int"/>.
    /// </param>
    /// <param name="Firearm">
    /// The weapon the operator carries. Two operators walking the same route
    /// carrying a rifle and a pistol are indistinguishable on screen without
    /// this — nothing in <c>src/Sandata.Client/UI</c> named a weapon before
    /// the 2026-08-14 lowered-weapon design's decision D2, which is half of
    /// why smoke row <c>SD-4</c> failed three times: a tester watching the
    /// pistol operator sees the exemption working correctly and reads it as
    /// the feature not working at all.
    /// </param>
    /// <param name="WeaponLowered">
    /// <c>OperatorState.WeaponLowered</c>, read straight through. The flag is
    /// authoritative simulation state and the row updates with it, so a
    /// paused, single-stepped run can be read rather than watched.
    /// </param>
    internal readonly record struct InspectorContent(
        int Intent,
        PathReasonCode ReasonCode,
        WeaponChainPhase ChainPhase,
        int ChainRemainingTicks,
        CoverState Cover,
        ulong GroupId,
        int? SlotIndex,
        FixedPoint DecisionPositionX,
        FixedPoint DecisionPositionY,
        FixedPoint ResolutionPositionX,
        FixedPoint ResolutionPositionY,
        ulong? ActiveOrderId,
        int? OrderNodeIndex,
        int? OrderClearReasonCode,
        FirearmId Firearm,
        bool WeaponLowered);

    /// <summary>Formats the intent row.</summary>
    internal static string FormatIntentLine(InspectorContent content) =>
        $"Intent: {content.Intent}";

    /// <summary>Formats the path reason-code row.</summary>
    internal static string FormatReasonCodeLine(InspectorContent content) =>
        $"Reason: {content.ReasonCode}";

    /// <summary>Formats the combined weapon-chain phase and remaining-ticks row.</summary>
    internal static string FormatChainPhaseLine(InspectorContent content) =>
        $"Chain: {content.ChainPhase} ({content.ChainRemainingTicks}t)";

    /// <summary>
    /// Formats the firearm row: the weapon's name from <see cref="NameSet"/>,
    /// then its <see cref="WeaponClass"/>, which is what tells a rifle from a
    /// pistol without the reader knowing the roster by heart.
    /// </summary>
    internal static string FormatFirearmLine(InspectorContent content) =>
        $"Weapon: {WeaponNameSets.GetName(content.Firearm, NameSet)} " +
        $"({FirearmCatalog.Rows[(int)content.Firearm].Class})";

    /// <summary>
    /// Formats the weapon-state row. A pistol carrier reads "raised" for the
    /// whole of every run, which is the designed behaviour rather than a
    /// missing feature — <c>WeaponLoweredRules.IsForcedLowered</c> returns
    /// false for an exempt weapon before it evaluates any geometry.
    /// </summary>
    internal static string FormatWeaponStateLine(InspectorContent content) =>
        content.WeaponLowered ? "Weapon state: lowered" : "Weapon state: raised";

    /// <summary>Formats the combined cover-state and arc row.</summary>
    internal static string FormatCoverLine(InspectorContent content) =>
        content.Cover.InCover
            ? $"Cover: {content.Cover.Posture} arc {content.Cover.ArcCentreBam.Raw}±{content.Cover.ArcHalfBam}"
            : "Cover: none";

    /// <summary>Formats the group-id row.</summary>
    internal static string FormatGroupLine(InspectorContent content) =>
        $"Group: {content.GroupId}";

    /// <summary>Formats the slot-index row.</summary>
    internal static string FormatSlotLine(InspectorContent content) =>
        content.SlotIndex is { } slot ? $"Slot: {slot}" : "Slot: -";

    /// <summary>Formats the decision-position row — the frozen tick-start view.</summary>
    internal static string FormatDecisionPositionLine(InspectorContent content) =>
        $"Decision pos: ({content.DecisionPositionX.ToDouble():0.00}, {content.DecisionPositionY.ToDouble():0.00})";

    /// <summary>Formats the resolution-position row — committed state.</summary>
    internal static string FormatResolutionPositionLine(InspectorContent content) =>
        $"Resolution pos: ({content.ResolutionPositionX.ToDouble():0.00}, {content.ResolutionPositionY.ToDouble():0.00})";

    /// <summary>Formats the active-order-id row (design section 16).</summary>
    internal static string FormatActiveOrderLine(InspectorContent content) =>
        content.ActiveOrderId is { } orderId ? $"Order: {orderId}" : "Order: none";

    /// <summary>Formats the order-node-index row (design section 16).</summary>
    internal static string FormatOrderNodeLine(InspectorContent content) =>
        content.OrderNodeIndex is { } nodeIndex ? $"Node: {nodeIndex}" : "Node: -";

    /// <summary>Formats the order-clear-reason row (design section 16).</summary>
    internal static string FormatOrderClearReasonLine(InspectorContent content) =>
        content.OrderClearReasonCode is { } reason ? $"Order cleared: {reason}" : "Order cleared: -";

    /// <summary>
    /// Every inspector line, in a fixed order, for the caller to draw one per
    /// row. Always has exactly <see cref="LineCount"/> entries.
    /// </summary>
    internal static IReadOnlyList<string> BuildLines(InspectorContent content) =>
    [
        FormatIntentLine(content),
        FormatReasonCodeLine(content),
        FormatFirearmLine(content),
        FormatWeaponStateLine(content),
        FormatChainPhaseLine(content),
        FormatCoverLine(content),
        FormatGroupLine(content),
        FormatSlotLine(content),
        FormatDecisionPositionLine(content),
        FormatResolutionPositionLine(content),
        FormatActiveOrderLine(content),
        FormatOrderNodeLine(content),
        FormatOrderClearReasonLine(content),
    ];
}
