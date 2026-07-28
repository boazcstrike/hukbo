namespace Hukbo.Client.Presentation;

internal enum ClientCommand
{
    None,
    Play,
    Pause,
    OpenMenu,
    NextRound,
    FullReset,
    Exit,
    ToggleSoundLog,
    OpenArmyComposition,

    // Appended rather than inserted so no existing member's ordinal moves.
    // ToggleBattleReport is consumed by battle-report's MatchSummaryPanel
    // and BattleReportPanel (task R8); Minimize is the window-chrome
    // workstream's own addition (task W2) and is not used by this
    // workstream.
    ToggleBattleReport,

    // Appended rather than inserted for the same reason: no existing
    // member's ordinal moves. Replaces the OS minimize button once the
    // window goes borderless (task W1/W4).
    Minimize,
}
