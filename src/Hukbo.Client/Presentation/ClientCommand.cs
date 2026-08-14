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

    // Appended, never inserted, for the same reason: no existing member's
    // ordinal moves.
    //
    // RequestExit asks; Exit acts. Every in-application quit path now issues
    // RequestExit, which raises the confirmation prompt, and only the prompt's
    // confirm button issues Exit. Alt+F4 bypasses both by design -- with no
    // title bar it is the guaranteed escape hatch if the in-game chrome ever
    // misbehaves.
    RequestExit,

    // Replaces the OS maximize button once the window goes borderless. The
    // handler reads the real window state from SDL rather than tracking a
    // boolean, because the user can maximize or restore outside the
    // application and a tracked flag would invert the button.
    ToggleMaximize,

    // Appended, never inserted, for the same reason: no existing member's
    // ordinal moves. Toggles the battle event log's own visibility, which now
    // defaults to hidden so the arena starts at full width; bound to F8 and
    // to the "Events" control-bar button, sitting beside ToggleSoundLog's F9
    // and "Sounds" button.
    ToggleEventLog,
}
