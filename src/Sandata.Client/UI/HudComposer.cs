using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Sandata.Core.Navigation;

namespace Sandata.Client.UI;

/// <summary>
/// Composes every element in design section 11's HUD element list into one
/// window-anchored layout. Task 69's own scope: pure composition of task 38's
/// panel helpers, task 46's minimap, and the in-world overlays that have no
/// window anchor at all. This file calls each helper's own
/// <c>CalculateBounds</c>; it invents no positioning math of its own.
/// </summary>
/// <remarks>
/// The seven panel-anchored elements (roster strip, contact list, alert
/// indicator, mission clock, event log, operator inspector, control bar) each
/// get a real, non-empty rectangle from their own task-38 helper. The
/// minimap (task 46) gets a real rectangle sized from a <see cref="NavGrid"/>.
/// The five elements design section 11 gives no window anchor at all — the
/// three in-world overlays (fire cone, order path, breach-point marker), the
/// in-world multi-select marquee, and the anchor-less undo stack ("—" in the
/// design table) — carry <see cref="Rectangle.Empty"/> here: this composer
/// only reasons about window-space HUD panels, and an empty rectangle can
/// never overlap another rectangle under <see cref="Rectangle.Intersects"/>'s
/// strict-inequality containment check, which is exactly the "present but
/// never contests panel space" reading design section 11 gives them.
/// </remarks>
internal static class HudComposer
{
    /// <summary>
    /// Vertical gap this composer places between <see cref="UI.Minimap"/> and
    /// <see cref="UI.OperatorInspector"/> when it stacks them, matching
    /// <see cref="SandataEventLog.Gap"/>'s already-established "8 pixels
    /// between two stacked panels" convention. Design section 11 anchors both
    /// elements to the top-left corner independently — task 38 and task 46
    /// each own one helper and neither knows the other exists — so composing
    /// them without an offset would overlap; this constant is this task's own
    /// decision, not a value either helper or the design doc states.
    /// </summary>
    private const int MinimapToInspectorGap = 8;

    /// <summary>
    /// One rectangle per design section 11 HUD element. Panel elements carry
    /// their real computed bounds; in-world and anchor-less elements carry
    /// <see cref="Rectangle.Empty"/>.
    /// </summary>
    internal readonly record struct Layout(
        Rectangle RosterStrip,
        Rectangle ContactList,
        Rectangle AlertIndicator,
        Rectangle MissionClock,
        Rectangle EventLog,
        Rectangle OperatorInspector,
        Rectangle ControlBar,
        Rectangle Minimap,
        Rectangle FireConeOverlay,
        Rectangle OrderPathOverlay,
        Rectangle BreachPointMarker,
        Rectangle MultiSelectMarquee,
        Rectangle UndoStack);

    /// <summary>
    /// Composes <see cref="Layout"/> for <paramref name="windowBounds"/>.
    /// <paramref name="operatorCount"/> feeds <see cref="UI.RosterStrip"/>,
    /// <paramref name="contactCount"/> feeds <see cref="UI.ContactList"/> (and,
    /// through it, <see cref="SandataEventLog"/>'s vertical offset), and
    /// <paramref name="minimapGrid"/> feeds <see cref="UI.Minimap"/>. All three
    /// are values this composer receives rather than decides — see this
    /// task's own report for which of them are real map/roster data and which
    /// are documented placeholders standing in for a simulation value that
    /// does not exist yet.
    /// </summary>
    internal static Layout Compose(
        Rectangle windowBounds,
        int operatorCount,
        int contactCount,
        NavGrid minimapGrid)
    {
        var rosterStrip = RosterStrip.CalculateBounds(windowBounds, operatorCount);
        var contactList = ContactList.CalculateBounds(windowBounds, contactCount);
        var alertIndicator = AlertIndicator.CalculateBounds(windowBounds);
        var missionClock = MissionClock.CalculateBounds(windowBounds);
        var eventLog = SandataEventLog.CalculateBounds(windowBounds, contactList);
        var controlBar = SandataControlBar.CalculateBounds(windowBounds);
        var minimap = Minimap.CalculateBounds(windowBounds, minimapGrid);

        // OperatorInspector.CalculateBounds anchors to the top-left corner of
        // whatever rectangle it is given, exactly as Minimap does. Passing it
        // a synthetic window rectangle whose top starts below the minimap
        // (rather than the real windowBounds) reuses that same helper
        // unmodified to stack the two panels instead of overlapping them.
        // The height is clamped to zero rather than left negative so a
        // window shorter than the minimap plus the gap still yields a
        // non-negative rectangle.
        var inspectorTop = minimap.Bottom + MinimapToInspectorGap;
        var inspectorWindow = new Rectangle(
            windowBounds.Left,
            inspectorTop,
            windowBounds.Width,
            Math.Max(0, windowBounds.Bottom - inspectorTop));
        var operatorInspector = OperatorInspector.CalculateBounds(inspectorWindow);

        return new Layout(
            RosterStrip: rosterStrip,
            ContactList: contactList,
            AlertIndicator: alertIndicator,
            MissionClock: missionClock,
            EventLog: eventLog,
            OperatorInspector: operatorInspector,
            ControlBar: controlBar,
            Minimap: minimap,
            FireConeOverlay: Rectangle.Empty,
            OrderPathOverlay: Rectangle.Empty,
            BreachPointMarker: Rectangle.Empty,
            MultiSelectMarquee: Rectangle.Empty,
            UndoStack: Rectangle.Empty);
    }

    /// <summary>
    /// Names every element in <paramref name="layout"/>, one entry per design
    /// section 11 HUD-list row, in that table's own order. Test-facing only —
    /// <see cref="Sandata.Client.SandataGame"/>'s draw path reads
    /// <see cref="Layout"/>'s fields directly and never allocates this list.
    /// </summary>
    internal static IReadOnlyList<(string Element, Rectangle Bounds)> ToNamedElements(Layout layout) =>
    [
        ("Roster strip", layout.RosterStrip),
        ("Contact list", layout.ContactList),
        ("Alert indicator", layout.AlertIndicator),
        ("Mission clock and tick counter", layout.MissionClock),
        ("Event log", layout.EventLog),
        ("Operator inspector", layout.OperatorInspector),
        ("Spectator control bar", layout.ControlBar),
        ("Fire cone overlay", layout.FireConeOverlay),
        ("Order path overlay", layout.OrderPathOverlay),
        ("Breach-point marker", layout.BreachPointMarker),
        ("Minimap", layout.Minimap),
        ("Multi-select marquee", layout.MultiSelectMarquee),
        ("Undo stack", layout.UndoStack),
    ];
}
