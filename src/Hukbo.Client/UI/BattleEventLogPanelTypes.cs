using Microsoft.Xna.Framework;

namespace Hukbo.Client.UI;

internal enum BattleEventPanelState
{
    Events,
    NoEvents,
    NoMatches,
}

internal enum BattleEventFilterTarget
{
    None,
    Kind,
    Faction,
    Actor,
    Search,
    Reset,
}

internal enum BattleEventKeyboardFocusTarget
{
    None,
    List,
    Search,
}

internal readonly record struct BattleEventPanelLayout(
    Rectangle HeaderBounds,
    Rectangle LatestBounds,
    Rectangle KindFilterBounds,
    Rectangle FactionFilterBounds,
    Rectangle ActorFilterBounds,
    Rectangle SearchBounds,
    Rectangle ResetBounds,
    Rectangle ListBounds,
    Rectangle RowsBounds,
    Rectangle ScrollbarTrackBounds,
    Rectangle DetailsBounds);

internal readonly record struct BattleEventRowForegrounds(
    Color Tick,
    Color Actor,
    Color Action);
