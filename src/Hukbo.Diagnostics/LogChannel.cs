namespace Hukbo.Diagnostics;

/// <summary>
/// The subsystem a log line belongs to. Flags rather than a plain enum so
/// channel filtering is a single bitmask test on the hot path.
/// </summary>
[Flags]
public enum LogChannel
{
    /// <summary>No channel. Used only as the empty mask.</summary>
    None = 0,

    /// <summary>Process lifecycle: start, window creation, shutdown, crash.</summary>
    Boot = 1 << 0,

    /// <summary>Content loaded from disk at runtime: themes, fonts, sounds.</summary>
    Assets = 1 << 1,

    /// <summary>The settings file and the preferences derived from it.</summary>
    Settings = 1 << 2,

    /// <summary>
    /// Observation of <c>Hukbo.Core</c> from outside. Nothing in
    /// <c>Hukbo.Core</c> writes to this channel, or to any other.
    /// </summary>
    Simulation = 1 << 3,

    /// <summary>The sound pipeline: cue mapping, budget, voices, playback.</summary>
    Audio = 1 << 4,

    /// <summary>Keyboard, pointer, and the pointer priority chain.</summary>
    Input = 1 << 5,

    /// <summary>
    /// Reserved. Declared so the channel set is stable; nothing emits on it
    /// yet.
    /// </summary>
    Ui = 1 << 6,

    /// <summary>
    /// The frame loop itself: how long a frame took, how much of it was
    /// update and how much was draw, and whether the simulation kept pace
    /// with the wall clock. Nothing here describes what was drawn, only what
    /// it cost.
    /// </summary>
    Render = 1 << 7,

    /// <summary>Every declared channel.</summary>
    All = Boot | Assets | Settings | Simulation | Audio | Input | Ui | Render,
}

/// <summary>
/// Converts between <see cref="LogChannel"/> and the names used on the wire
/// and accepted by <c>HUKBO_LOG_CHANNELS</c>.
/// </summary>
public static class LogChannels
{
    /// <summary>
    /// The name written to the <c>ch</c> field. Only a single-flag value has a
    /// wire name; a composite mask is a filter, never a line's channel.
    /// </summary>
    public static string ToWireName(LogChannel channel) => channel switch
    {
        LogChannel.Boot => "boot",
        LogChannel.Assets => "assets",
        LogChannel.Settings => "settings",
        LogChannel.Simulation => "sim",
        LogChannel.Audio => "audio",
        LogChannel.Input => "input",
        LogChannel.Ui => "ui",
        LogChannel.Render => "render",
        _ => "none",
    };

    /// <summary>
    /// Parses a single channel name. Returns false for an unknown name so the
    /// caller can report it rather than silently dropping a requested channel.
    /// </summary>
    public static bool TryParse(string? text, out LogChannel channel)
    {
        switch (text?.Trim().ToLowerInvariant())
        {
            case "boot":
                channel = LogChannel.Boot;
                return true;
            case "assets":
                channel = LogChannel.Assets;
                return true;
            case "settings":
                channel = LogChannel.Settings;
                return true;
            case "sim" or "simulation":
                channel = LogChannel.Simulation;
                return true;
            case "audio" or "sound":
                channel = LogChannel.Audio;
                return true;
            case "input":
                channel = LogChannel.Input;
                return true;
            case "ui":
                channel = LogChannel.Ui;
                return true;
            case "render" or "frame":
                channel = LogChannel.Render;
                return true;
            case "all":
                channel = LogChannel.All;
                return true;
            default:
                channel = LogChannel.None;
                return false;
        }
    }

    /// <summary>
    /// Parses a comma-separated mask such as <c>audio,input</c> or <c>all</c>.
    /// An unknown name anywhere in the list fails the whole parse and names the
    /// offender, because a partially honoured filter is worse than a rejected
    /// one: the log looks right and is missing what was asked for.
    /// </summary>
    public static bool TryParseMask(
        string? text,
        out LogChannel mask,
        out string unknownName)
    {
        mask = LogChannel.None;
        unknownName = string.Empty;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        foreach (var part in text.Split(
                     ',',
                     StringSplitOptions.RemoveEmptyEntries |
                     StringSplitOptions.TrimEntries))
        {
            if (!TryParse(part, out var channel))
            {
                unknownName = part;
                mask = LogChannel.None;
                return false;
            }

            mask |= channel;
        }

        return mask != LogChannel.None;
    }
}
