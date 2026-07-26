using Hukbo.Client.UI;

namespace Hukbo.Client;

/// <summary>
/// Which keyboard pan bindings the spectator camera may read this frame.
/// </summary>
[Flags]
internal enum SpectatorPanInput
{
    None = 0,

    /// <summary>W, A, S, and D.</summary>
    Letters = 1,

    /// <summary>The four arrow keys.</summary>
    Arrows = 2,

    All = Letters | Arrows,
}

/// <summary>
/// Decides which spectator keyboard bindings stay live while the battle event
/// log panel holds keyboard focus. Pure so it can be tested without a window.
/// </summary>
/// <remarks>
/// The search box captures typed characters, so every letter and digit binding
/// must yield to it. The event list only reads Up, Down, Home, and End, so it
/// yields the arrow keys alone and leaves play, pause, round, reset, speed, and
/// W/A/S/D panning reachable. Wheel zoom is never gated here; it is decided by
/// pointer position through <see cref="SpectatorCamera.Update"/>'s allowZoom.
/// </remarks>
internal readonly record struct SpectatorInputGate(
    bool AllowSpectatorCommands,
    bool AllowSpeedShortcuts,
    SpectatorPanInput PanInput)
{
    public static SpectatorInputGate Resolve(
        BattleEventKeyboardFocusTarget focusTarget) =>
        focusTarget switch
        {
            BattleEventKeyboardFocusTarget.Search => new SpectatorInputGate(
                AllowSpectatorCommands: false,
                AllowSpeedShortcuts: false,
                SpectatorPanInput.None),
            BattleEventKeyboardFocusTarget.List => new SpectatorInputGate(
                AllowSpectatorCommands: true,
                AllowSpeedShortcuts: true,
                SpectatorPanInput.Letters),
            _ => new SpectatorInputGate(
                AllowSpectatorCommands: true,
                AllowSpeedShortcuts: true,
                SpectatorPanInput.All),
        };
}
