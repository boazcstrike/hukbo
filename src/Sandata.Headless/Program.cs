using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Hukbo.Diagnostics;

namespace Sandata.Headless;

/// <summary>
/// Sandata's process entry point. Mirrors
/// <c>Hukbo.Headless.HeadlessRunner</c>'s argument-parsing shape and
/// exit-code contract (plan task 14 of Sandata's scaffold plan),
/// collapsed into this one file
/// because that task's file list did not include a separate runner class.
/// Task 51 adds that runner (<see cref="HeadlessRunner"/>) and this file's
/// dispatch to it: when <c>--agents</c>, <c>--ticks</c>, and <c>--seed</c>
/// are all supplied, this entry point runs the seeded determinism workload
/// and prints its <see cref="RunReport"/> as JSON instead of the placeholder
/// boot-only message. Supplying none of the three keeps that placeholder
/// behavior, so an operator can still probe logging and argument handling
/// alone.
/// </summary>
internal static class Program
{
    /// <summary>The run parsed its arguments, logged, and returned normally.</summary>
    public const int ExitSuccess = 0;

    /// <summary>An unhandled exception reached <see cref="Run"/>.</summary>
    public const int ExitUnhandledException = 1;

    /// <summary>The supplied arguments could not be parsed.</summary>
    public const int ExitArgumentError = 2;

    /// <summary>
    /// The determinism workload ran to completion but the two simulations it
    /// compared disagreed at some tick — see <see cref="RunReport.FirstMismatchTick"/>
    /// in the JSON report this run printed for exactly which one. Matches
    /// <c>Hukbo.Headless.HeadlessRunner</c>'s own exit code 3 for the same
    /// condition.
    /// </summary>
    public const int ExitDeterminismMismatch = 3;

    private const string UsageText =
        "Usage: sandata-headless [--help] " +
        "[--agents <positive-even-count>] [--ticks <positive-count>] " +
        "[--seed <uint64>] [--output <json-path>] " +
        "[--log-level off|err|warn|inf|dbg|trc] " +
        "[--log-channels all|<comma-separated>] " +
        "[--log-dir <directory>] " +
        "[--nav-map-density <0-80>] [--nav-changed-cells <0-2000>] " +
        "[--nav-seekers <1-256>] [--nav-query-distance <4-4096>] " +
        "[--nav-replan-rate <0-100>] [--nav-seed <uint64>] " +
        "[--nav-ticks <positive integer>] [--nav-fixture-path <path>]. " +
        "The five --nav-map-density/--nav-changed-cells/--nav-seekers/" +
        "--nav-query-distance/--nav-replan-rate flags together trigger the " +
        "navigation benchmark and, when used, all five are required. " +
        "The three --agents/--ticks/--seed flags together trigger the " +
        "determinism workload and, when any one is used, all three are " +
        "required.";

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

        var determinismMissingFlags = MissingDeterminismFlags(options);
        if (determinismMissingFlags.Count is > 0 and < 3)
        {
            standardError.WriteLine(
                "Argument error: the determinism workload requires all three " +
                "--agents/--ticks/--seed flags; missing: " +
                string.Join(", ", determinismMissingFlags) + ".");
            standardError.WriteLine(UsageText);
            return ExitArgumentError;
        }

        var runDeterminismWorkload = determinismMissingFlags.Count == 0;

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

            var exitCode = runDeterminismWorkload
                ? RunDeterminismWorkload(options, standardOutput, log)
                : RunBootOnly(standardOutput);

            log.Write(
                LogLevel.Information,
                LogChannel.Boot,
                LogEvents.BootSandataStopped,
                "reason",
                "exit",
                "uptimeMs",
                (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds);
            return exitCode;
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
    /// Every <c>--agents</c>/<c>--ticks</c>/<c>--seed</c> flag name that
    /// <paramref name="options"/> did not supply, in that order — empty when
    /// all three, or none, were supplied. The all-or-none dispatch rule this
    /// feeds mirrors <see cref="TryRunNavBenchmark"/>'s own rule for its five
    /// matrix flags: a caller that supplies some but not all of a required
    /// group gets a named list of what is missing, never a silently
    /// defaulted value.
    /// </summary>
    private static List<string> MissingDeterminismFlags(HeadlessOptions options)
    {
        var missing = new List<string>();
        if (options.AgentCount is null)
        {
            missing.Add("--agents");
        }

        if (options.TickCount is null)
        {
            missing.Add("--ticks");
        }

        if (options.Seed is null)
        {
            missing.Add("--seed");
        }

        // The caller (Run) treats an empty list as "all three supplied, run
        // the workload" and a full list of three as "none supplied, stay on
        // the boot-only path" — both are fine outcomes. Only a list of one
        // or two is the error case: some but not all of the required group,
        // which would otherwise let the missing ones quietly fall back to a
        // default the caller never asked for.
        return missing;
    }

    /// <summary>
    /// The placeholder boot-only report this entry point printed before task
    /// 51 added <see cref="HeadlessRunner"/> — still reachable today when
    /// none of <c>--agents</c>/<c>--ticks</c>/<c>--seed</c> is supplied, so a
    /// bare invocation can still probe argument parsing and logging alone
    /// without paying for a mission run.
    /// </summary>
    private static int RunBootOnly(TextWriter standardOutput)
    {
        standardOutput.WriteLine(
            "Sandata.Headless: argument parsing and logging only; no " +
            "--agents/--ticks/--seed supplied, so no mission ran " +
            "(task 51 of Sandata's scaffold plan).");
        return ExitSuccess;
    }

    /// <summary>
    /// Runs <see cref="HeadlessRunner.Execute(int, int, ulong, DiagnosticLog)"/>
    /// with the parsed <c>--agents</c>/<c>--ticks</c>/<c>--seed</c> (and
    /// optional <c>--output</c>) options, prints the resulting
    /// <see cref="RunReport"/> as indented, camelCase JSON to
    /// <paramref name="standardOutput"/>, mirrors it to
    /// <c>--output</c> when supplied, and returns
    /// <see cref="ExitDeterminismMismatch"/> when the two simulations it ran
    /// disagreed or <see cref="ExitSuccess"/> otherwise.
    /// </summary>
    private static int RunDeterminismWorkload(
        HeadlessOptions options, TextWriter standardOutput, DiagnosticLog log)
    {
        var report = HeadlessRunner.Execute(
            options.AgentCount!.Value, options.TickCount!.Value, options.Seed!.Value, log);
        var json = JsonSerializer.Serialize(
            report,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
            });
        standardOutput.WriteLine(json);

        if (options.OutputPath is not null)
        {
            var outputPath = Path.GetFullPath(options.OutputPath);
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(
                outputPath,
                json + Environment.NewLine,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        return DetermineDeterminismExitCode(report);
    }

    /// <summary>
    /// The exit-code half of <see cref="RunDeterminismWorkload"/>'s contract,
    /// pulled out to its own pure, internal method so a test can assert
    /// <see cref="ExitDeterminismMismatch"/>'s documented condition —
    /// "the determinism workload ran to completion but the two simulations
    /// it compared disagreed" — directly against a <see cref="RunReport"/>,
    /// without needing a CLI seam to force a real mismatch through
    /// <c>Program.Run</c> itself.
    /// </summary>
    internal static int DetermineDeterminismExitCode(RunReport report) =>
        report.Deterministic ? ExitSuccess : ExitDeterminismMismatch;

    /// <summary>
    /// Parses the arguments this entry point accepts. Grows as later tasks
    /// add work for this runner to do.
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
        int? agentCount = null;
        int? tickCount = null;
        ulong? seed = null;
        string? outputPath = null;
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

                case "--agents":
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedAgentCount) ||
                        parsedAgentCount <= 0 || (parsedAgentCount & 1) != 0 ||
                        parsedAgentCount > HeadlessRunner.MaxOperatorCount)
                    {
                        options = default!;
                        error =
                            "'--agents' must be a positive even integer no greater than " +
                            $"{HeadlessRunner.MaxOperatorCount}.";
                        return false;
                    }

                    agentCount = parsedAgentCount;
                    break;

                case "--ticks":
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedTickCount) ||
                        parsedTickCount <= 0)
                    {
                        options = default!;
                        error = "'--ticks' must be a positive integer.";
                        return false;
                    }

                    tickCount = parsedTickCount;
                    break;

                case "--seed":
                    if (!ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedSeed))
                    {
                        options = default!;
                        error = "'--seed' must be a non-negative 64-bit integer.";
                        return false;
                    }

                    seed = parsedSeed;
                    break;

                case "--output":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        options = default!;
                        error = "'--output' must be a nonempty file path.";
                        return false;
                    }

                    outputPath = value;
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
            agentCount,
            tickCount,
            seed,
            outputPath,
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
            "--agents" or "--ticks" or "--seed" or "--output" or
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
/// <param name="AgentCount">
/// Parsed <c>--agents</c>, or <see langword="null"/> if not supplied. One of
/// the three determinism-workload flags; see
/// <c>Program.MissingDeterminismFlags</c> for the all-three-or-none rule.
/// </param>
/// <param name="TickCount">Parsed <c>--ticks</c>, or <see langword="null"/> if not supplied.</param>
/// <param name="Seed">Parsed <c>--seed</c>, or <see langword="null"/> if not supplied.</param>
/// <param name="OutputPath">
/// Parsed <c>--output</c>, or <see langword="null"/> to print the
/// determinism workload's <see cref="RunReport"/> to stdout only.
/// </param>
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
    int? AgentCount = null,
    int? TickCount = null,
    ulong? Seed = null,
    string? OutputPath = null,
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
