namespace Hukbo.Diagnostics;

/// <summary>
/// The resolved logging configuration for one process: how loud, which
/// channels, and where the file goes.
/// </summary>
/// <param name="Level">
/// The severity threshold. <see cref="LogLevel.Off"/> means no file is created
/// at all.
/// </param>
/// <param name="Channels">The channel mask admitted through the filter.</param>
/// <param name="DirectoryPath">
/// The directory the log file is written into, or <see langword="null"/> to use
/// the resolved default.
/// </param>
public sealed record LogOptions(
    LogLevel Level,
    LogChannel Channels,
    string? DirectoryPath)
{
    /// <summary>Environment variable overriding the severity threshold.</summary>
    public const string LevelVariable = "HUKBO_LOG_LEVEL";

    /// <summary>Environment variable overriding the channel mask.</summary>
    public const string ChannelsVariable = "HUKBO_LOG_CHANNELS";

    /// <summary>Environment variable overriding the destination directory.</summary>
    public const string DirectoryVariable = "HUKBO_LOG_DIR";

    /// <summary>
    /// The level used when no environment override is present: verbose while
    /// developing, silent in a shipped build. The canonical gate builds
    /// <c>Release</c>, so its determinism workload is never logged by accident.
    /// </summary>
    public static LogLevel ConfigurationDefaultLevel =>
#if DEBUG
        LogLevel.Debug;
#else
        LogLevel.Off;
#endif

    /// <summary>The name of the build configuration, for the boot line.</summary>
    public static string ConfigurationName =>
#if DEBUG
        "Debug";
#else
        "Release";
#endif

    /// <summary>Logging disabled entirely.</summary>
    public static LogOptions Disabled { get; } =
        new(LogLevel.Off, LogChannel.None, null);

    /// <summary>
    /// Resolves the configuration from the environment, falling back to the
    /// build configuration default.
    /// </summary>
    /// <param name="warningWriter">
    /// Receives one line per malformed value. A typo in a debugging switch is
    /// reported rather than silently honoured as "off", because an empty log is
    /// otherwise indistinguishable from a quiet run.
    /// </param>
    public static LogOptions FromEnvironment(TextWriter? warningWriter = null)
    {
        var level = ConfigurationDefaultLevel;
        var levelText = Environment.GetEnvironmentVariable(LevelVariable);
        if (!string.IsNullOrWhiteSpace(levelText))
        {
            if (LogLevels.TryParse(levelText, out var parsedLevel))
            {
                level = parsedLevel;
            }
            else
            {
                warningWriter?.WriteLine(
                    $"{LevelVariable}='{levelText}' is not a known level " +
                    "(off, err, warn, inf, dbg, trc). Using " +
                    $"{LogLevels.ToWireName(level)}.");
            }
        }

        var channels = LogChannel.All;
        var channelsText = Environment.GetEnvironmentVariable(ChannelsVariable);
        if (!string.IsNullOrWhiteSpace(channelsText))
        {
            if (LogChannels.TryParseMask(channelsText, out var mask, out var unknown))
            {
                channels = mask;
            }
            else
            {
                warningWriter?.WriteLine(
                    $"{ChannelsVariable}='{channelsText}' names an unknown " +
                    $"channel '{unknown}' (boot, assets, settings, sim, " +
                    "audio, input, ui, all). Using all.");
            }
        }

        var directory = Environment.GetEnvironmentVariable(DirectoryVariable);
        return new LogOptions(
            level,
            channels,
            string.IsNullOrWhiteSpace(directory) ? null : directory);
    }

    /// <summary>
    /// Applies explicit command-line overrides on top of a resolved
    /// configuration. Arguments outrank the environment so a one-off diagnostic
    /// run never requires mutating the shell.
    /// </summary>
    public LogOptions WithOverrides(
        LogLevel? level,
        LogChannel? channels,
        string? directoryPath) =>
        new(
            level ?? Level,
            channels ?? Channels,
            string.IsNullOrWhiteSpace(directoryPath)
                ? DirectoryPath
                : directoryPath);
}
