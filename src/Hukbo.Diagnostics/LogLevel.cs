namespace Hukbo.Diagnostics;

/// <summary>
/// Severity threshold for the debug log. Ordered from quietest to loudest, so
/// a configured level admits every level with a smaller numeric value.
/// </summary>
/// <remarks>
/// The numeric values are part of the comparison and nothing else. Unlike the
/// enums in <c>Hukbo.Core</c>, these never reach a state hash, so reordering
/// them does not require a preset version bump.
/// </remarks>
public enum LogLevel
{
    /// <summary>Nothing is written and no file is created.</summary>
    Off = 0,

    /// <summary>The run is degraded in a way the user will notice.</summary>
    Error = 1,

    /// <summary>A fallback was taken; the run continues but not as configured.</summary>
    Warning = 2,

    /// <summary>Lifecycle: startup, round boundaries, outcome, settings changes.</summary>
    Information = 3,

    /// <summary>Per-decision detail. The default verbose tier.</summary>
    Debug = 4,

    /// <summary>Per-tick and per-frame firehose. Never on by default.</summary>
    Trace = 5,
}

/// <summary>
/// Converts between <see cref="LogLevel"/> and the short names used on the
/// wire and accepted by <c>HUKBO_LOG_LEVEL</c>.
/// </summary>
public static class LogLevels
{
    /// <summary>
    /// The four-character-or-fewer name written to the <c>lvl</c> field. Kept
    /// short because it appears on every line of every log.
    /// </summary>
    public static string ToWireName(LogLevel level) => level switch
    {
        LogLevel.Error => "err",
        LogLevel.Warning => "warn",
        LogLevel.Information => "inf",
        LogLevel.Debug => "dbg",
        LogLevel.Trace => "trc",
        _ => "off",
    };

    /// <summary>
    /// Parses a value supplied by a human, accepting both the wire name and the
    /// full enum name. Returns false rather than guessing: a typo in a
    /// debugging switch must be reported, not silently downgraded.
    /// </summary>
    public static bool TryParse(string? text, out LogLevel level)
    {
        switch (text?.Trim().ToLowerInvariant())
        {
            case "off" or "none":
                level = LogLevel.Off;
                return true;
            case "err" or "error":
                level = LogLevel.Error;
                return true;
            case "warn" or "warning":
                level = LogLevel.Warning;
                return true;
            case "inf" or "info" or "information":
                level = LogLevel.Information;
                return true;
            case "dbg" or "debug":
                level = LogLevel.Debug;
                return true;
            case "trc" or "trace":
                level = LogLevel.Trace;
                return true;
            default:
                level = LogLevel.Off;
                return false;
        }
    }
}
