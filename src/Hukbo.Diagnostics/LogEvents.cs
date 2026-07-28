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
    public const string SimTick = "sim.tick";

    // Audio channel.
    public const string AudioCue = "audio.cue";
    public const string AudioFrame = "audio.frame";
    public const string AudioMuteToggled = "audio.mute.toggled";
    public const string AudioPlayerAttached = "audio.player.attached";

    // Input channel.
    public const string InputFocusChanged = "input.focus.changed";
    public const string InputKey = "input.key";
    public const string InputPointer = "input.pointer";
}
