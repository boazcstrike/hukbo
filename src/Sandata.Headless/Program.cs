using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Hukbo.Diagnostics;

namespace Sandata.Headless;

/// <summary>
/// Sandata's process entry point. Mirrors
/// <c>Hukbo.Headless.HeadlessRunner</c>'s argument-parsing shape and
/// exit-code contract (plan task 14 of
/// docs/plans/2026-08-07-sandata-scaffold.md), collapsed into this one file
/// because that task's file list does not include a separate runner class.
/// </summary>
/// <remarks>
/// <para>
/// There is no <c>Mission</c> or simulation in <c>Sandata.Core</c> yet — plan
/// task 51 adds the real determinism runner. This entry point exists to prove
/// the argument, logging, and exit-code contract now, so task 51 builds on a
/// tested shape instead of inventing its own. A successful run therefore does
/// no simulation work; it only parses arguments, opens the debug log, and
/// reports that fact.
/// </para>
/// <para>
/// Exit code 3, reserved on <c>Hukbo.Headless.HeadlessRunner</c> for a
/// determinism mismatch, is not used here: there is nothing yet to mismatch.
/// Task 51 is expected to add it.
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
        "Usage: sandata-headless [--help] " +
        "[--log-level off|err|warn|inf|dbg|trc] " +
        "[--log-channels all|<comma-separated>] " +
        "[--log-dir <directory>] " +
        "[--nav-map-density <0-80>] [--nav-changed-cells <0-2000>] " +
        "[--nav-seekers <1-256>] [--nav-query-distance <4-4096>] " +
        "[--nav-replan-rate <0-100>] [--nav-seed <uint64>] " +
        "[--nav-ticks <positive integer>] [--nav-fixture-path <path>]. " +
        "The five --nav-map-density/--nav-changed-cells/--nav-seekers/" +
        "--nav-query-distance/--nav-replan-rate flags together trigger the " +
        "navigation benchmark and, when used, all five are required.";

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

        // The navigation benchmark, when requested, runs and returns here,
        // before the debug log ever opens: plan task 50 requires the wall
        // clock measurement inside NavBenchmark.Run to stay strictly
        // outside anything that could itself add timed I/O around it.
        var navBenchmarkExitCode = TryRunNavBenchmark(options, standardOutput, standardError);
        if (navBenchmarkExitCode is not null)
        {
            return navBenchmarkExitCode.Value;
        }

        // Command-line switches outrank the environment, matching
        // Hukbo.Headless: a one-off diagnostic run should never require
        // mutating the shell.
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
                "Sandata.Headless: argument parsing and logging only; " +
                "no mission runner yet (docs/plans/2026-08-07-sandata-scaffold.md " +
                "task 51).");

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
            standardError.WriteLine($"Sandata headless run failed: {exception.Message}");
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
    /// Dispatches to <see cref="NavBenchmark.Run"/> when the navigation
    /// benchmark was requested, printing its report as JSON to
    /// <paramref name="standardOutput"/> and returning the exit code the
    /// process should return. Returns <see langword="null"/> when none of
    /// the five matrix flags were supplied at all, meaning the benchmark
    /// was not requested and the caller should continue with the ordinary
    /// boot-log flow.
    /// </summary>
    /// <remarks>
    /// A request is "some but not all five matrix flags present" is an
    /// argument error naming every flag still missing, never a benchmark
    /// run with an unstated value silently filled in — the same
    /// no-default-hides-a-missing-value rule <see cref="NavBenchmarkOptions.Create"/>
    /// itself enforces, applied here at the command-line layer too.
    /// </remarks>
    private static int? TryRunNavBenchmark(
        HeadlessOptions options, TextWriter standardOutput, TextWriter standardError)
    {
        var anyProvided =
            options.NavMapDensityPercent is not null ||
            options.NavChangedCellCount is not null ||
            options.NavConcurrentSeekers is not null ||
            options.NavQueryDistanceWu is not null ||
            options.NavReplanningRatePercent is not null;

        if (!anyProvided)
        {
            return null;
        }

        var missingFlags = new List<string>();
        if (options.NavMapDensityPercent is null)
        {
            missingFlags.Add("--nav-map-density");
        }

        if (options.NavChangedCellCount is null)
        {
            missingFlags.Add("--nav-changed-cells");
        }

        if (options.NavConcurrentSeekers is null)
        {
            missingFlags.Add("--nav-seekers");
        }

        if (options.NavQueryDistanceWu is null)
        {
            missingFlags.Add("--nav-query-distance");
        }

        if (options.NavReplanningRatePercent is null)
        {
            missingFlags.Add("--nav-replan-rate");
        }

        if (missingFlags.Count > 0)
        {
            standardError.WriteLine(
                "Argument error: the navigation benchmark requires all five matrix " +
                "flags; missing: " + string.Join(", ", missingFlags) + ".");
            standardError.WriteLine(UsageText);
            return ExitArgumentError;
        }

        NavBenchmarkOptions navOptions;
        try
        {
            navOptions = NavBenchmarkOptions.Create(
                options.NavMapDensityPercent!.Value,
                options.NavChangedCellCount!.Value,
                options.NavConcurrentSeekers!.Value,
                options.NavQueryDistanceWu!.Value,
                options.NavReplanningRatePercent!.Value);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            standardError.WriteLine($"Argument error: {exception.Message}");
            return ExitArgumentError;
        }

        string fixturePath;
        try
        {
            fixturePath = NavBenchmark.ResolveFixturePath(options.NavFixturePath);
        }
        catch (FileNotFoundException exception)
        {
            standardError.WriteLine($"Argument error: {exception.Message}");
            return ExitArgumentError;
        }

        var report = NavBenchmark.Run(navOptions, fixturePath, options.NavSeed, options.NavTickCount);
        var json = JsonSerializer.Serialize(
            report,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
            });
        standardOutput.WriteLine(json);
        return ExitSuccess;
    }

    /// <summary>
    /// Parses the arguments this entry point accepts. Kept deliberately small
    /// — there is no scenario or mission yet for a flag to configure — and
    /// grows as later tasks add work for this runner to do.
    /// </summary>
    internal static bool TryParseArguments(
        IReadOnlyList<string> arguments,
        out HeadlessOptions options,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        LogLevel? logLevel = null;
        LogChannel? logChannels = null;
        string? logDirectory = null;
        int? navMapDensityPercent = null;
        int? navChangedCellCount = null;
        int? navConcurrentSeekers = null;
        int? navQueryDistanceWu = null;
        int? navReplanningRatePercent = null;
        var navSeed = HeadlessOptions.DefaultNavSeed;
        var navTickCount = HeadlessOptions.DefaultNavTickCount;
        string? navFixturePath = null;
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

                case "--nav-map-density":
                    if (!TryParseNonNegativeInt(value, out navMapDensityPercent))
                    {
                        options = default!;
                        error = "'--nav-map-density' must be a non-negative integer.";
                        return false;
                    }

                    break;

                case "--nav-changed-cells":
                    if (!TryParseNonNegativeInt(value, out navChangedCellCount))
                    {
                        options = default!;
                        error = "'--nav-changed-cells' must be a non-negative integer.";
                        return false;
                    }

                    break;

                case "--nav-seekers":
                    if (!TryParseNonNegativeInt(value, out navConcurrentSeekers))
                    {
                        options = default!;
                        error = "'--nav-seekers' must be a non-negative integer.";
                        return false;
                    }

                    break;

                case "--nav-query-distance":
                    if (!TryParseNonNegativeInt(value, out navQueryDistanceWu))
                    {
                        options = default!;
                        error = "'--nav-query-distance' must be a non-negative integer.";
                        return false;
                    }

                    break;

                case "--nav-replan-rate":
                    if (!TryParseNonNegativeInt(value, out navReplanningRatePercent))
                    {
                        options = default!;
                        error = "'--nav-replan-rate' must be a non-negative integer.";
                        return false;
                    }

                    break;

                case "--nav-seed":
                    if (!ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out navSeed))
                    {
                        options = default!;
                        error = "'--nav-seed' must be a non-negative 64-bit integer.";
                        return false;
                    }

                    break;

                case "--nav-ticks":
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out navTickCount) ||
                        navTickCount <= 0)
                    {
                        options = default!;
                        error = "'--nav-ticks' must be a positive integer.";
                        return false;
                    }

                    break;

                case "--nav-fixture-path":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        options = default!;
                        error = "'--nav-fixture-path' must be a nonempty path.";
                        return false;
                    }

                    navFixturePath = value;
                    break;
            }
        }

        options = new HeadlessOptions(
            logLevel,
            logChannels,
            logDirectory,
            navMapDensityPercent,
            navChangedCellCount,
            navConcurrentSeekers,
            navQueryDistanceWu,
            navReplanningRatePercent,
            navSeed,
            navTickCount,
            navFixturePath);
        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Parses a base-10, non-negative integer, matching every
    /// <c>--nav-*</c> matrix flag's own textual contract: no leading sign,
    /// no thousands separator. Range validation against each flag's
    /// specific bounds is <see cref="NavBenchmarkOptions.Create"/>'s job,
    /// not this method's — this only rejects a value that cannot be an
    /// integer at all.
    /// </summary>
    private static bool TryParseNonNegativeInt(string value, out int? parsed)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result))
        {
            parsed = null;
            return false;
        }

        parsed = result;
        return true;
    }

    private static bool IsSupportedArgument(string argument) =>
        argument is "--log-level" or "--log-channels" or "--log-dir" or
            "--nav-map-density" or "--nav-changed-cells" or "--nav-seekers" or
            "--nav-query-distance" or "--nav-replan-rate" or "--nav-seed" or
            "--nav-ticks" or "--nav-fixture-path";

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

/// <summary>The parsed command-line configuration for one headless run.</summary>
/// <param name="NavMapDensityPercent">
/// Parsed <c>--nav-map-density</c>, or <see langword="null"/> if not
/// supplied. One of the five navigation benchmark matrix flags; see
/// <c>Program.TryRunNavBenchmark</c> for the all-five-or-none rule.
/// </param>
/// <param name="NavChangedCellCount">Parsed <c>--nav-changed-cells</c>, or <see langword="null"/> if not supplied.</param>
/// <param name="NavConcurrentSeekers">Parsed <c>--nav-seekers</c>, or <see langword="null"/> if not supplied.</param>
/// <param name="NavQueryDistanceWu">Parsed <c>--nav-query-distance</c>, or <see langword="null"/> if not supplied.</param>
/// <param name="NavReplanningRatePercent">Parsed <c>--nav-replan-rate</c>, or <see langword="null"/> if not supplied.</param>
/// <param name="NavSeed">
/// Parsed <c>--nav-seed</c>, defaulting to <see cref="DefaultNavSeed"/>.
/// Not one of the five matrix parameters plan task 50 names — it is an
/// operational reproducibility setting, so unlike the five it is safe to
/// default rather than require.
/// </param>
/// <param name="NavTickCount">
/// Parsed <c>--nav-ticks</c>, defaulting to <see cref="DefaultNavTickCount"/>.
/// Also not one of the five matrix parameters.
/// </param>
/// <param name="NavFixturePath">
/// Parsed <c>--nav-fixture-path</c>, or <see langword="null"/> to fall back
/// to repository-root discovery — see <see cref="NavBenchmark.ResolveFixturePath"/>.
/// </param>
internal sealed record HeadlessOptions(
    LogLevel? LogLevel,
    LogChannel? LogChannels,
    string? LogDirectory,
    int? NavMapDensityPercent = null,
    int? NavChangedCellCount = null,
    int? NavConcurrentSeekers = null,
    int? NavQueryDistanceWu = null,
    int? NavReplanningRatePercent = null,
    ulong NavSeed = HeadlessOptions.DefaultNavSeed,
    int NavTickCount = HeadlessOptions.DefaultNavTickCount,
    string? NavFixturePath = null)
{
    /// <summary>
    /// PROVISIONAL default for <see cref="NavSeed"/> when <c>--nav-seed</c>
    /// is not supplied — matches <c>Hukbo.Headless</c>'s own default seed of
    /// 1, so a navigation benchmark run with no seed flag is reproducible
    /// the same way an unflagged Hukbo determinism run is.
    /// </summary>
    public const ulong DefaultNavSeed = 1;

    /// <summary>
    /// PROVISIONAL default for <see cref="NavTickCount"/> when
    /// <c>--nav-ticks</c> is not supplied. Not a measured value — chosen to
    /// keep an unflagged benchmark run's wall-clock duration short while
    /// still producing enough tick-stage samples for a meaningful p99.
    /// </summary>
    public const int DefaultNavTickCount = 500;
}
