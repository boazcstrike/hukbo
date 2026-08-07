using System.Diagnostics;
using System.Globalization;
using System.Text;
using Hukbo.Diagnostics;

namespace Sandata.Client;

/// <summary>
/// Sandata's process entry point. Mirrors
/// <c>Sandata.Headless.Program</c>'s argument-parsing shape and exit-code
/// contract (plan task 14 of docs/plans/2026-08-07-sandata-scaffold.md),
/// which itself mirrors <c>Hukbo.Headless.HeadlessRunner</c>.
/// </summary>
/// <remarks>
/// <para>
/// There is no <c>SandataGame</c> window yet — plan task 33 adds the MonoGame
/// shell. This entry point exists to prove the argument, logging, and
/// exit-code contract now, so task 33 builds on a tested shape instead of
/// inventing its own. A successful run therefore opens no window; it only
/// parses arguments, opens the debug log, and reports that fact.
/// </para>
/// <para>
/// Task 33's file list does not name this file, even though it is the file
/// that will eventually have to construct and run <c>SandataGame</c>. That
/// is recorded as an open question for whoever plans task 33's work, not
/// resolved here.
/// </para>
/// </remarks>
internal static class Program
{
    /// <summary>The run parsed its arguments, logged, and returned normally.</summary>
    public const int ExitSuccess = 0;

    /// <summary>An unhandled exception reached <see cref="Run"/>.</summary>
    public const int ExitUnhandledException = 1;

    /// <summary>The supplied arguments could not be parsed.</summary>
    public const int ExitArgumentError = 2;

    private const string UsageText =
        "Usage: sandata-client [--help] " +
        "[--log-level off|err|warn|inf|dbg|trc] " +
        "[--log-channels all|<comma-separated>] " +
        "[--log-dir <directory>]";

    private static int Main(string[] args) =>
        Run(args, Console.Out, Console.Error);

    /// <summary>
    /// The whole entry point, factored out of <see cref="Main"/> so a test can
    /// drive it with captured writers instead of the real console.
    /// </summary>
    public static int Run(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);

        if (Array.IndexOf(arguments, "--help") >= 0)
        {
            standardOutput.WriteLine(UsageText);
            return ExitSuccess;
        }

        if (!TryParseArguments(arguments, out var options, out var error))
        {
            standardError.WriteLine($"Argument error: {error}");
            standardError.WriteLine(UsageText);
            return ExitArgumentError;
        }

        // Command-line switches outrank the environment, matching
        // Sandata.Headless and Hukbo.Client: a one-off diagnostic run should
        // never require mutating the shell.
        var logOptions = LogOptions
            .FromEnvironment(standardError)
            .WithOverrides(options.LogLevel, options.LogChannels, options.LogDirectory);
        var (log, ownedWriter, filePath) = OpenLog(logOptions, standardError);
        var startTimestamp = Stopwatch.GetTimestamp();

        try
        {
            log.SetTick(DiagnosticLog.NoTick);
            log.Write(
                LogLevel.Information,
                LogChannel.Boot,
                LogEvents.BootSandataStarted,
                "configuration",
                LogOptions.ConfigurationName,
                "level",
                LogLevels.ToWireName(logOptions.Level),
                "channels",
                logOptions.Channels.ToString(),
                "path",
                filePath);

            standardOutput.WriteLine(
                "Sandata.Client: argument parsing and logging only; " +
                "no window yet (docs/plans/2026-08-07-sandata-scaffold.md " +
                "task 33).");

            log.Write(
                LogLevel.Information,
                LogChannel.Boot,
                LogEvents.BootSandataStopped,
                "reason",
                "exit",
                "uptimeMs",
                (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds);
            return ExitSuccess;
        }
        catch (Exception exception)
        {
            // The log line carries the type and message for filtering; the
            // standard error line keeps the full detail a person needs in the
            // terminal.
            log.Write(
                LogLevel.Error,
                LogChannel.Boot,
                LogEvents.BootSandataCrashed,
                "reason",
                exception.GetType().Name,
                "msg",
                exception.Message,
                "uptimeMs",
                (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds);
            standardError.WriteLine($"Sandata client run failed: {exception.Message}");
            return ExitUnhandledException;
        }
        finally
        {
            log.Flush();
            log.Dispose();

            // DiagnosticLog.CreateForWriter never disposes a caller-supplied
            // writer (it is built for tests that keep the writer alive to
            // inspect it), so the writer opened by OpenLog is this method's
            // own responsibility to close.
            ownedWriter?.Dispose();
        }
    }

    /// <summary>
    /// Parses the arguments this entry point accepts. Kept deliberately small
    /// — there is no window or renderer yet for a flag to configure — and
    /// grows as later tasks add work for this shell to do.
    /// </summary>
    internal static bool TryParseArguments(
        IReadOnlyList<string> arguments,
        out ClientOptions options,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        LogLevel? logLevel = null;
        LogChannel? logChannels = null;
        string? logDirectory = null;
        var encounteredArguments = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < arguments.Count; index += 2)
        {
            var argument = arguments[index];
            if (!IsSupportedArgument(argument))
            {
                options = default!;
                error = $"Unsupported argument '{argument}'.";
                return false;
            }

            if (!encounteredArguments.Add(argument))
            {
                options = default!;
                error = $"Argument '{argument}' was provided more than once.";
                return false;
            }

            if (index + 1 >= arguments.Count)
            {
                options = default!;
                error = $"Argument '{argument}' requires a value.";
                return false;
            }

            var value = arguments[index + 1];
            switch (argument)
            {
                case "--log-level":
                    if (!LogLevels.TryParse(value, out var parsedLevel))
                    {
                        options = default!;
                        error =
                            "'--log-level' must be one of off, err, warn, " +
                            "inf, dbg, trc.";
                        return false;
                    }

                    logLevel = parsedLevel;
                    break;

                case "--log-channels":
                    if (!LogChannels.TryParseMask(
                            value, out var parsedChannels, out var unknownChannel))
                    {
                        options = default!;
                        error =
                            $"'--log-channels' names an unknown channel " +
                            $"'{unknownChannel}'. Valid names are boot, assets, " +
                            "settings, sim, audio, input, ui, render, all.";
                        return false;
                    }

                    logChannels = parsedChannels;
                    break;

                case "--log-dir":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        options = default!;
                        error = "'--log-dir' must be a nonempty directory path.";
                        return false;
                    }

                    logDirectory = value;
                    break;
            }
        }

        options = new ClientOptions(logLevel, logChannels, logDirectory);
        error = string.Empty;
        return true;
    }

    private static bool IsSupportedArgument(string argument) =>
        argument is "--log-level" or "--log-channels" or "--log-dir";

    /// <summary>
    /// Opens the debug log with the file name shape
    /// <c>sandata-&lt;utc&gt;-&lt;pid&gt;.jsonl</c>, rather than
    /// <see cref="DiagnosticLog.Create"/>'s hardcoded <c>hukbo-</c> prefix.
    /// Built entirely on the public <see cref="LogPaths.ResolveDirectory"/>
    /// and <see cref="DiagnosticLog.CreateForWriter"/> surface, because this
    /// task's file list does not include <c>LogPaths.cs</c> or
    /// <c>DiagnosticLog.cs</c> and may not add a prefix parameter to either.
    /// </summary>
    /// <remarks>
    /// Recorded rather than silently accepted: <see cref="LogPaths.ApplyRetention"/>
    /// only sweeps files matching the <c>hukbo-</c> prefix, so files produced
    /// by this method are never swept by it. Sandata's log directory grows
    /// without the bound Hukbo's enjoys until a task with access to
    /// <c>LogPaths.cs</c> parameterises the prefix.
    /// </remarks>
    private static (DiagnosticLog Log, StreamWriter? OwnedWriter, string FilePath) OpenLog(
        LogOptions options,
        TextWriter warningWriter)
    {
        if (options.Level == LogLevel.Off || options.Channels == LogChannel.None)
        {
            return (DiagnosticLog.Disabled, null, string.Empty);
        }

        try
        {
            var directory = LogPaths.ResolveDirectory(options.DirectoryPath);
            Directory.CreateDirectory(directory);
            var fileName =
                "sandata-" +
                DateTime.UtcNow.ToString(
                    "yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) +
                "-" +
                Environment.ProcessId.ToString(CultureInfo.InvariantCulture) +
                ".jsonl";
            var path = Path.GetFullPath(Path.Combine(directory, fileName));
            var writer = new StreamWriter(
                path,
                append: false,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
            {
                AutoFlush = false,
                NewLine = "\n",
            };
            return (DiagnosticLog.CreateForWriter(options, writer), writer, path);
        }
        catch (Exception exception) when (
            exception is IOException or
                UnauthorizedAccessException or
                NotSupportedException or
                ArgumentException)
        {
            warningWriter.WriteLine($"Debug logging disabled: {exception.Message}");
            return (DiagnosticLog.Disabled, null, string.Empty);
        }
    }
}

/// <summary>The parsed command-line configuration for one client run.</summary>
internal sealed record ClientOptions(
    LogLevel? LogLevel,
    LogChannel? LogChannels,
    string? LogDirectory);
