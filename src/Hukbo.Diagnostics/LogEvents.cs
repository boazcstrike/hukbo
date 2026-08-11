namespace Hukbo.Diagnostics;

/// <summary>
/// Every <c>ev</c> identifier the game can emit, declared once.
/// </summary>
/// <remarks>
/// <para>
/// An identifier is a stable machine key, not a sentence. It never contains a
/// value, never contains a count, and is never reworded to read better — the
/// data belongs in payload fields, and a reworded identifier silently breaks
/// every filter written against the old one.
/// </para>
/// <para>
/// Adding an event means adding a constant here first. Keeping the catalog in
/// one file is what lets a person or an agent learn the whole vocabulary by
/// reading a single screen, and it is what
/// <c>LogEventCatalogTests</c> asserts against.
/// </para>
/// </remarks>
public static class LogEvents
{
    // Boot channel.
    public const string BootCrashed = "boot.crashed";

    // Records whether the process managed to declare per-monitor DPI
    // awareness before its first window existed. Without that declaration
    // Windows hands the game a virtualised viewport and upscales the finished
    // frame, which is what made text pixelated in the 2026-08-11 smoke run.
    // The payload carries the resulting state so a log says which run is
    // which. See docs/plans/2026-08-11-display-dpi-awareness-design.md.
    public const string BootDpiAwareness = "boot.dpi.awareness";

    // Sandata's own boot lifecycle events (plan task 14 of
    // docs/plans/2026-08-07-sandata-scaffold.md). Named with a "sandata"
    // middle segment rather than a "sandata." leading prefix, because
    // LogEventCatalogTests.EveryIdentifierPrefixNamesADeclaredChannel
    // requires the leading segment to be a declared LogChannel wire name —
    // "boot" here, exactly as it is for the events immediately below.
    public const string BootSandataCrashed = "boot.sandata.crashed";
    public const string BootSandataStarted = "boot.sandata.started";
    public const string BootSandataStopped = "boot.sandata.stopped";

    public const string BootStarted = "boot.started";
    public const string BootStopped = "boot.stopped";
    public const string BootWindowCreated = "boot.window.created";

    // Assets channel.
    public const string AssetsFontFailed = "assets.font.failed";
    public const string AssetsFontLoaded = "assets.font.loaded";
    public const string AssetsSoundLoadFailed = "assets.sound.loadFailed";
    public const string AssetsSoundMissing = "assets.sound.missing";
    public const string AssetsSoundScanned = "assets.sound.scanned";
    public const string AssetsThemeFallback = "assets.theme.fallback";
    public const string AssetsThemeLoaded = "assets.theme.loaded";
    public const string AssetsVisualCatalogInvalid = "assets.visual.catalogInvalid";
    public const string AssetsVisualFallback = "assets.visual.fallback";
    public const string AssetsVisualVariantMissing = "assets.visual.variantMissing";

    // Settings channel.
    public const string SettingsChanged = "settings.changed";
    public const string SettingsInvalid = "settings.invalid";
    public const string SettingsLoaded = "settings.loaded";
    public const string SettingsSaveFailed = "settings.saveFailed";
    public const string SettingsSaved = "settings.saved";

    // Simulation channel.
    public const string SimMismatch = "sim.mismatch";
    public const string SimOutcome = "sim.outcome";
    public const string SimPlaybackChanged = "sim.playback.changed";
    public const string SimReset = "sim.reset";
    public const string SimRoundStarted = "sim.round.started";
    public const string SimScenarioBuilt = "sim.scenario.built";
    public const string SimSpeedChanged = "sim.speed.changed";

    // Sandata's living-roster line, written only on the tick a faction's
    // living count actually changes rather than once per tick — a casualty is
    // the one simulation event a spectator most often needs to place in time
    // afterwards, and it fires a handful of times in a whole mission.
    public const string SimSandataRoster = "sim.sandata.roster";

    public const string SimTick = "sim.tick";

    // Audio channel.
    public const string AudioCue = "audio.cue";
    public const string AudioFrame = "audio.frame";
    public const string AudioMuteToggled = "audio.mute.toggled";
    public const string AudioPlayerAttached = "audio.player.attached";

    // Sandata's own gunfire playback, from MonoGameSandataSoundOutput. Named
    // with a "sandata" middle segment rather than a "sandata." leading
    // prefix, for the same LogEventCatalogTests reason the boot/sim/input
    // sandata events above already are: the leading segment has to be a
    // declared LogChannel wire name, "audio" here.
    public const string AudioSandataCue = "audio.sandata.cue";
    public const string AudioSandataCueLoadFailed = "audio.sandata.cueLoadFailed";
    public const string AudioSandataCueMissing = "audio.sandata.cueMissing";

    // Input channel.
    public const string InputFocusChanged = "input.focus.changed";
    public const string InputKey = "input.key";
    public const string InputPointer = "input.pointer";

    // Sandata's spectator transport controls — pause, single step, speed, and
    // restart, whether reached from the control bar or from the keyboard.
    // Named with a "sandata" middle segment for the same reason the boot
    // events above are: the leading segment has to be a declared channel wire
    // name.
    public const string InputSandataTransport = "input.sandata.transport";

    // Render channel.
    public const string RenderAttackContactCollapsed =
        "render.attackContactCollapsed";
    public const string RenderFrame = "render.frame";
    public const string RenderStarved = "render.starved";
    public const string RenderViewportChanged = "render.viewport.changed";
    public const string RenderWindow = "render.window";
}
