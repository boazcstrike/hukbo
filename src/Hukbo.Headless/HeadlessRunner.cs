using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Hukbo.Core.Determinism;
using Hukbo.Core.Simulation;
using Hukbo.Diagnostics;

namespace Hukbo.Headless;

public sealed record HeadlessOptions(
    int AgentCount,
    int TickCount,
    ulong Seed,
    string? OutputPath,
    LogLevel? LogLevel = null,
    LogChannel? LogChannels = null,
    string? LogDirectory = null);

public static class HeadlessRunner
{
    public static int Run(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);

        if (!TryParseArguments(arguments, out var options, out var error))
        {
            standardError.WriteLine($"Argument error: {error}");
            standardError.WriteLine(
                "Usage: --agents <positive-even-count> --ticks <positive-count> " +
                "--seed <unsigned-integer> [--output <json-path>] " +
                "[--log-level off|err|warn|inf|dbg|trc] " +
                "[--log-channels all|<comma-separated>] " +
                "[--log-dir <directory>]");
            return 2;
        }

        // Command-line switches outrank the environment: a one-off diagnostic
        // run should never require mutating the shell.
        var logOptions = LogOptions
            .FromEnvironment(standardError)
            .WithOverrides(
                options.LogLevel,
                options.LogChannels,
                options.LogDirectory);
        using var log = DiagnosticLog.Create(logOptions, standardError);

        try
        {
            var report = Execute(options, log);
            log.Flush();
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

            return report.Deterministic ? 0 : 3;
        }
        catch (Exception exception)
        {
            standardError.WriteLine($"Headless run failed: {exception.Message}");
            return 1;
        }
    }

    public static bool TryParseArguments(
        IReadOnlyList<string> arguments,
        out HeadlessOptions options,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var agentCount = 200;
        var tickCount = 10_000;
        ulong seed = 1;
        string? outputPath = null;
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
                case "--agents":
                    if (!int.TryParse(
                            value,
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out agentCount) ||
                        agentCount <= 0 ||
                        (agentCount & 1) != 0 ||
                        agentCount > Scenario.MaximumAgentsPerFaction * 2)
                    {
                        options = default!;
                        error =
                            "'--agents' must be a positive even integer no greater " +
                            $"than {Scenario.MaximumAgentsPerFaction * 2}.";
                        return false;
                    }

                    break;

                case "--ticks":
                    if (!int.TryParse(
                            value,
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out tickCount) ||
                        tickCount <= 0 ||
                        tickCount > Scenario.MaximumTickLimit)
                    {
                        options = default!;
                        error =
                            "'--ticks' must be a positive integer no greater than " +
                            $"{Scenario.MaximumTickLimit}.";
                        return false;
                    }

                    break;

                case "--seed":
                    if (!ulong.TryParse(
                            value,
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out seed))
                    {
                        options = default!;
                        error = "'--seed' must be an unsigned 64-bit integer.";
                        return false;
                    }

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
                            value,
                            out var parsedChannels,
                            out var unknownChannel))
                    {
                        options = default!;
                        error =
                            $"'--log-channels' names an unknown channel " +
                            $"'{unknownChannel}'. Valid names are boot, " +
                            "assets, settings, sim, audio, input, ui, all.";
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

        options = new HeadlessOptions(
            agentCount,
            tickCount,
            seed,
            outputPath,
            logLevel,
            logChannels,
            logDirectory);
        error = string.Empty;
        return true;
    }

    private static RunReport Execute(HeadlessOptions options, DiagnosticLog log)
    {
        var scenario = Scenario.CreateDefault(options.Seed, options.AgentCount) with
        {
            TickLimit = options.TickCount,
        };
        scenario.Validate();

        log.SetTick(DiagnosticLog.NoTick);
        log.Write(
            LogLevel.Information,
            LogChannel.Simulation,
            LogEvents.SimScenarioBuilt,
            "seed",
            options.Seed,
            "agents",
            options.AgentCount,
            "requestedTicks",
            options.TickCount,
            "mapWidth",
            scenario.MapWidth,
            "mapHeight",
            scenario.MapHeight);

        var left = BattleSimulation.Create(scenario);
        var right = BattleSimulation.Create(scenario);
        var tickDurations = new List<double>(
            Math.Min(options.TickCount, 100_000));
        var eventHash = Fnv1a.OffsetBasis;
        long? firstMismatchTick = null;

        // Fed from the left simulation only. The right simulation exists purely
        // to prove determinism, and both are verified identical every tick, so
        // aggregating one is aggregating both.
        var collisionMetrics = new CollisionMetricsAccumulator();
        collisionMetrics.Reset();

        var allocationStart = GC.GetAllocatedBytesForCurrentThread();

        for (var requestedTick = 0;
             requestedTick < options.TickCount &&
                left.Outcome == BattleOutcome.Ongoing;
             requestedTick++)
        {
            var startTimestamp = Stopwatch.GetTimestamp();
            left.AdvanceOneTick();
            var elapsed = Stopwatch.GetElapsedTime(startTimestamp);
            tickDurations.Add(elapsed.TotalMilliseconds);

            var tickCollision = left.LastTickCollision;
            collisionMetrics.AddTick(
                tickCollision.CandidatePairs,
                tickCollision.ContactPairs,
                tickCollision.AcceptedMoves,
                tickCollision.BlockedAgents,
                tickCollision.AttackCapableAgents,
                tickCollision.FrontWidthRaw,
                tickCollision.FrontDepthRaw,
                tickCollision.PenetrationRaw);

            right.AdvanceOneTick();
            var leftStateHash = left.ComputeStateHash();
            var rightStateHash = right.ComputeStateHash();
            if (left.Tick != right.Tick ||
                left.Outcome != right.Outcome ||
                leftStateHash != rightStateHash ||
                !left.LastEvents.SequenceEqual(right.LastEvents))
            {
                firstMismatchTick = left.Tick;
                log.SetTick(left.Tick);

                // The single highest-value line in the whole facility: when the
                // two simulations part ways, this is the only record of what
                // each of them believed at the moment they did.
                log.Write(
                    LogLevel.Error,
                    LogChannel.Simulation,
                    LogEvents.SimMismatch,
                    "leftTick",
                    left.Tick,
                    "rightTick",
                    right.Tick,
                    "leftOutcome",
                    left.Outcome.ToString(),
                    "rightOutcome",
                    right.Outcome.ToString(),
                    "leftStateHash",
                    ToHex(leftStateHash),
                    "rightStateHash",
                    ToHex(rightStateHash));
                break;
            }

            foreach (var battleEvent in left.LastEvents)
            {
                AddEventToHash(ref eventHash, battleEvent);
            }

            log.SetTick(left.Tick);
            LogTick(log, left, leftStateHash);
        }

        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
        collisionMetrics.ObserveBlockedStreak(left.LongestBlockedStreakTicks);
        var sortedDurations = tickDurations.Order().ToArray();
        var survivors = left.Agents
            .Where(agent => agent.IsAlive)
            .GroupBy(agent => agent.FactionId)
            .ToDictionary(group => group.Key, group => group.Count());
        var totalDuration = tickDurations.Sum();
        var finalStateHash = left.ComputeStateHash();

        log.SetTick(left.Tick);
        log.Write(
            LogLevel.Information,
            LogChannel.Simulation,
            LogEvents.SimOutcome,
            "outcome",
            left.Outcome.ToString(),
            "tick",
            left.Tick,
            "survivors0",
            survivors.GetValueOrDefault(0),
            "survivors1",
            survivors.GetValueOrDefault(1),
            "stateHash",
            ToHex(finalStateHash),
            "eventHash",
            ToHex(eventHash));

        return new RunReport(
            new RunEnvironment(
                RuntimeInformation.OSDescription,
                RuntimeInformation.FrameworkDescription,
                RuntimeInformation.ProcessArchitecture.ToString(),
                Environment.ProcessorCount),
            options.Seed,
            options.AgentCount,
            options.TickCount,
            left.Tick,
            totalDuration,
            new TickPercentiles(
                Percentile(sortedDurations, 0.50),
                Percentile(sortedDurations, 0.95),
                Percentile(sortedDurations, 0.99),
                sortedDurations.Length == 0 ? 0 : sortedDurations[^1]),
            allocatedBytes,
            left.Outcome.ToString(),
            survivors.GetValueOrDefault(0),
            survivors.GetValueOrDefault(1),
            ToHex(eventHash),
            ToHex(finalStateHash),
            firstMismatchTick is null,
            firstMismatchTick,
            collisionMetrics.ToMetrics());
    }

    /// <summary>
    /// Emits one observation of the tick that just advanced. Sampled ticks go
    /// out at <see cref="LogLevel.Debug"/> and every other tick at
    /// <see cref="LogLevel.Trace"/>, so an ordinary verbose run carries a
    /// bisectable skeleton and a trace run carries the whole thing.
    /// </summary>
    /// <remarks>
    /// The state hash is passed in rather than recomputed: the caller already
    /// computed it to compare the two simulations, and a log must never add a
    /// call the run would not otherwise make.
    /// </remarks>
    private static void LogTick(
        DiagnosticLog log,
        BattleSimulation simulation,
        ulong stateHash)
    {
        var level = LogSampling.IsSampledTick(simulation.Tick)
            ? LogLevel.Debug
            : LogLevel.Trace;
        if (!log.IsEnabledFor(level, LogChannel.Simulation))
        {
            return;
        }

        var alive0 = 0;
        var alive1 = 0;
        foreach (var agent in simulation.Agents)
        {
            if (!agent.IsAlive)
            {
                continue;
            }

            if (agent.FactionId == 0)
            {
                alive0++;
            }
            else
            {
                alive1++;
            }
        }

        log.Write(
            level,
            LogChannel.Simulation,
            LogEvents.SimTick,
            "tick",
            simulation.Tick,
            "alive0",
            alive0,
            "alive1",
            alive1,
            "events",
            simulation.LastEvents.Count,
            "stateHash",
            ToHex(stateHash));
    }

    private static string ToHex(ulong value) =>
        value.ToString("X16", CultureInfo.InvariantCulture);

    private static bool IsSupportedArgument(string argument) =>
        argument is "--agents" or "--ticks" or "--seed" or "--output" or
            "--log-level" or "--log-channels" or "--log-dir";

    private static double Percentile(double[] sortedValues, double percentile)
    {
        if (sortedValues.Length == 0)
        {
            return 0;
        }

        var rank = (int)Math.Ceiling(percentile * sortedValues.Length) - 1;
        return sortedValues[Math.Clamp(rank, 0, sortedValues.Length - 1)];
    }

    /// <summary>
    /// Mixes one authoritative event into the running headless event hash.
    /// Internal (rather than private) so its field sensitivity, including
    /// the nullable weapon/hit-location sentinel, can be verified directly
    /// by <c>Hukbo.Core.Tests</c> without running a full simulation.
    /// </summary>
    internal static void AddEventToHash(
        ref ulong hash,
        BattleEvent battleEvent)
    {
        AddToHash(ref hash, unchecked((ulong)battleEvent.Sequence));
        AddToHash(ref hash, unchecked((ulong)battleEvent.Tick));
        AddToHash(ref hash, (ulong)battleEvent.Kind);
        AddToHash(ref hash, battleEvent.SourceEntityId);
        AddToHash(ref hash, battleEvent.TargetEntityId ?? 0);
        AddToHash(ref hash, unchecked((ulong)(uint)battleEvent.Value));
        AddToHash(
            ref hash,
            battleEvent.FactionId is { } factionId
                ? unchecked((ulong)(uint)factionId)
                : ulong.MaxValue);
        AddToHash(
            ref hash,
            battleEvent.Weapon is { } weapon
                ? unchecked((ulong)(uint)(int)weapon)
                : ulong.MaxValue);
        AddToHash(
            ref hash,
            battleEvent.HitLocation is { } hitLocation
                ? unchecked((ulong)(uint)(int)hitLocation)
                : ulong.MaxValue);
    }

    private static void AddToHash(ref ulong hash, ulong value) =>
        Fnv1a.Add(ref hash, value);
}
