using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Hukbo.Core.Combat;
using Hukbo.Core.Determinism;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
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
    string? LogDirectory = null,
    CombatPresetId? Preset = null,
    MovementPresetId? MovementPreset = null);

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
                "[--log-dir <directory>] " +
                "[--preset <CombatPresetId name or number>] " +
                "[--movement-preset <MovementPresetId name or number>]");
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
        CombatPresetId? preset = null;
        MovementPresetId? movementPreset = null;
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

                case "--preset":
                    if (!TryParsePreset(value, out var parsedPreset))
                    {
                        options = default!;
                        error =
                            $"'--preset' does not name a registered " +
                            $"CombatPresetId: '{value}'.";
                        return false;
                    }

                    preset = parsedPreset;
                    break;

                case "--movement-preset":
                    if (!TryParseMovementPreset(value, out var parsedMovementPreset))
                    {
                        options = default!;
                        error =
                            $"'--movement-preset' does not name a registered " +
                            $"MovementPresetId: '{value}'.";
                        return false;
                    }

                    movementPreset = parsedMovementPreset;
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
            logDirectory,
            preset,
            movementPreset);
        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Parses <c>--preset</c> either as a <see cref="CombatPresetId"/> member
    /// name (for example <c>PrecolonialPhilippinesV3</c>) or as its
    /// underlying numeric value, then confirms the result is registered so a
    /// stray future enum value cannot silently build an unfielded ruleset.
    /// </summary>
    private static bool TryParsePreset(string value, out CombatPresetId preset)
    {
        if (Enum.TryParse(value, ignoreCase: true, out preset) &&
            CombatPresetRegistry.IsRegistered(preset))
        {
            return true;
        }

        if (int.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var numeric))
        {
            preset = (CombatPresetId)numeric;
            return CombatPresetRegistry.IsRegistered(preset);
        }

        preset = default;
        return false;
    }

    /// <summary>
    /// Parses <c>--movement-preset</c> either as a <see cref="MovementPresetId"/>
    /// member name (for example <c>IndependentPursuitV1</c>) or as its
    /// underlying numeric value, then confirms the result is registered so a
    /// stray future enum value cannot silently build an unfielded ruleset.
    /// </summary>
    private static bool TryParseMovementPreset(
        string value,
        out MovementPresetId movementPreset)
    {
        if (Enum.TryParse(value, ignoreCase: true, out movementPreset) &&
            MovementPresetRegistry.IsRegistered(movementPreset))
        {
            return true;
        }

        if (int.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var numeric))
        {
            movementPreset = (MovementPresetId)numeric;
            return MovementPresetRegistry.IsRegistered(movementPreset);
        }

        movementPreset = default;
        return false;
    }

    private static RunReport Execute(HeadlessOptions options, DiagnosticLog log)
    {
        var scenario = Scenario.CreateDefault(options.Seed, options.AgentCount) with
        {
            TickLimit = options.TickCount,
        };
        if (options.Preset is { } preset)
        {
            scenario = scenario with { CombatPreset = preset };
        }

        if (options.MovementPreset is { } movementPreset)
        {
            scenario = scenario with { MovementPreset = movementPreset };
        }

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
        var combatMetrics = new CombatMetricsAccumulator();
        combatMetrics.Reset();
        var movementMetrics = new MovementBehaviorMetricsAccumulator();
        movementMetrics.Reset();
        var evasionMetrics = new EvasiveMovementMetricsAccumulator();
        evasionMetrics.Reset();

        // The derived movement observation of the weapon-relative movement
        // design, section 16: reconstructed here, outside the simulation, by
        // comparing each tick's views against the previous tick's. The
        // previous-view buffer is allocated once per run, and sized zero
        // under a legacy preset, which resolves no posture, phase, or facing
        // and would only ever observe zeros.
        var usesFootwork = MovementPresetRegistry
            .Get(scenario.MovementPreset)
            .UsesEquipmentRelativeFootwork;
        var previousViews = usesFootwork
            ? new AgentView[left.Agents.Count]
            : [];
        for (var index = 0; index < previousViews.Length; index++)
        {
            previousViews[index] = left.Agents[index];
        }

        // The derived in-fight evasion observation of the 2026-08-15 evasion
        // design, section 8: reconstructed here, outside the simulation, from
        // the same consecutive-view comparison the block above uses. It is
        // built for every preset rather than only for a footwork one, because
        // its movement half — living agent-ticks, rooted agent-ticks, travel,
        // retention, and net drift — is meaningful under all fourteen movement
        // presets and is the baseline the V14 bars are measured against. The
        // observer allocates its per-agent buffers once here and never again.
        var evasionObserver = new EvasiveMovementObserver(left.Agents, scenario);

        var allocationStart = GC.GetAllocatedBytesForCurrentThread();

        // Accumulated across the loop below: the managed bytes allocated by
        // left.AdvanceOneTick() alone, summed tick by tick. See RunReport's
        // CoreAllocatedBytes doc comment for exactly what this does and does
        // not include.
        var coreAllocatedBytes = 0L;

        for (var requestedTick = 0;
             requestedTick < options.TickCount &&
                left.Outcome == BattleOutcome.Ongoing;
             requestedTick++)
        {
            // The allocation reads sit immediately outside the Stopwatch
            // bracket, not between its two calls, so timing is unaffected by
            // adding this measurement.
            var tickAllocationStart = GC.GetAllocatedBytesForCurrentThread();
            var startTimestamp = Stopwatch.GetTimestamp();
            left.AdvanceOneTick();
            var elapsed = Stopwatch.GetElapsedTime(startTimestamp);
            coreAllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - tickAllocationStart;
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

            var tickCombat = left.LastTickCombat;
            combatMetrics.AddTick(
                checked((int)tickCombat.AcceptedAttacks),
                checked((int)tickCombat.LandedAttacks),
                checked((int)tickCombat.ShieldBlockedAttacks),
                checked((int)tickCombat.ParriedAttacks),
                checked((int)tickCombat.DeflectedAttacks),
                checked((int)tickCombat.EvadedAttacks));

            var tickRefusals = 0;
            if (usesFootwork)
            {
                var movementTick = ObserveMovementTick(
                    left.Agents, previousViews);
                movementMetrics.AddTick(
                    movementTick.ApproachAgents,
                    movementTick.EngageAgents,
                    movementTick.CommitAgents,
                    movementTick.RecoverAgents,
                    movementTick.RefuseAgents,
                    movementTick.DisengageAgents,
                    movementTick.RegroupAgents,
                    movementTick.PursueAgents,
                    movementTick.PostureTransitions,
                    movementTick.FacingStepsTurned,
                    movementTick.DisengagementEntries);
                tickRefusals = movementTick.RefuseAgents;
            }

            var evasionTick = evasionObserver.ObserveTick(left.Agents);
            evasionMetrics.AddTick(
                evasionTick.LivingAgents,
                evasionTick.RootedAgents,
                evasionTick.TravelRaw,
                evasionTick.ReachRetentionAgents,
                evasionTick.TargetHeldAgents,
                evasionTick.SlipLateralAgents,
                evasionTick.DodgeIncomingAgents,
                evasionTick.GiveGroundAgents,
                evasionTick.BreakOffAgents,
                evasionTick.BreakOffArmedAgents);

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
            LogTick(log, left, leftStateHash, usesFootwork, tickRefusals);
        }

        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
        collisionMetrics.ObserveBlockedStreak(left.LongestBlockedStreakTicks);
        movementMetrics.RecordConflictDenialTotal(left.MovementConflictDenials);
        movementMetrics.RecordRouteRefusalReasonTotals(
            left.RouteRefusalNoCandidatesBuilt,
            left.RouteRefusalStepEndpointRejected,
            left.RouteRefusalDirectCandidateOmitted,
            left.RouteRefusalLaneNotClear);

        // Net drift is a property of the terminal snapshot rather than of any
        // step, so it is the one evasion quantity computed after the loop
        // instead of inside it. A corpse keeps its final position and is
        // included, because where the dead lie is part of where the battle was
        // fought.
        evasionMetrics.RecordNetDisplacementSum(
            evasionObserver.ComputeNetDisplacementSumRaw(left.Agents));
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
            collisionMetrics.ToMetrics(),
            combatMetrics.ToMetrics(),
            coreAllocatedBytes,
            movementMetrics.ToMetrics(),
            scenario.CombatPreset,
            scenario.MovementPreset,
            evasionMetrics.ToMetrics());
    }

    /// <summary>
    /// One tick's derived movement behaviour counts, reconstructed entirely
    /// from views (weapon-relative movement design, section 16). Internal so
    /// the boundary test in <c>Hukbo.Core.Tests</c> can exercise the exact
    /// production observation against an unobserved twin simulation.
    /// </summary>
    internal readonly record struct MovementTickObservation(
        int ApproachAgents,
        int EngageAgents,
        int CommitAgents,
        int RecoverAgents,
        int RefuseAgents,
        int DisengageAgents,
        int RegroupAgents,
        int PursueAgents,
        int PostureTransitions,
        int FacingStepsTurned,
        int DisengagementEntries);

    /// <summary>
    /// Derives one tick's movement behaviour counts by comparing the current
    /// views against the previous tick's, then overwrites
    /// <paramref name="previousViews"/> with the current views so the next
    /// tick compares against this one. Allocation-free: the caller owns the
    /// single buffer for the whole run. Only living agents are counted;
    /// facing steps are counted only when both ticks carry a resolved facing,
    /// because <see cref="Facing16.None"/> is not a sector.
    /// </summary>
    internal static MovementTickObservation ObserveMovementTick(
        IReadOnlyList<AgentView> currentViews,
        AgentView[] previousViews)
    {
        ArgumentNullException.ThrowIfNull(currentViews);
        ArgumentNullException.ThrowIfNull(previousViews);
        if (currentViews.Count != previousViews.Length)
        {
            throw new ArgumentException(
                $"The view buffers disagree on the agent count: " +
                $"{currentViews.Count} current versus " +
                $"{previousViews.Length} previous.",
                nameof(previousViews));
        }

        var approachAgents = 0;
        var engageAgents = 0;
        var commitAgents = 0;
        var recoverAgents = 0;
        var refuseAgents = 0;
        var disengageAgents = 0;
        var regroupAgents = 0;
        var pursueAgents = 0;
        var postureTransitions = 0;
        var facingStepsTurned = 0;
        var disengagementEntries = 0;

        for (var index = 0; index < previousViews.Length; index++)
        {
            var current = currentViews[index];
            var previous = previousViews[index];
            previousViews[index] = current;

            if (!current.IsAlive)
            {
                continue;
            }

            switch (current.FootworkPhase)
            {
                case FootworkPhase.Approach:
                    approachAgents++;
                    break;
                case FootworkPhase.Engage:
                    engageAgents++;
                    break;
                case FootworkPhase.Commit:
                    commitAgents++;
                    break;
                case FootworkPhase.Recover:
                    recoverAgents++;
                    break;
                case FootworkPhase.Refuse:
                    refuseAgents++;
                    break;
                case FootworkPhase.Disengage:
                    disengageAgents++;
                    break;
                case FootworkPhase.Regroup:
                    regroupAgents++;
                    break;
                case FootworkPhase.Pursue:
                    pursueAgents++;
                    break;
                case FootworkPhase.None:
                default:
                    break;
            }

            if (current.TacticalPosture != previous.TacticalPosture)
            {
                postureTransitions++;
            }

            if (current.FootworkPhase == FootworkPhase.Disengage &&
                previous.FootworkPhase != FootworkPhase.Disengage)
            {
                disengagementEntries++;
            }

            if (current.Facing != Facing16.None &&
                previous.Facing != Facing16.None)
            {
                facingStepsTurned += FacingRules.SectorSeparation(
                    previous.Facing, current.Facing);
            }
        }

        return new MovementTickObservation(
            approachAgents,
            engageAgents,
            commitAgents,
            recoverAgents,
            refuseAgents,
            disengageAgents,
            regroupAgents,
            pursueAgents,
            postureTransitions,
            facingStepsTurned,
            disengagementEntries);
    }

    /// <summary>
    /// One tick's derived in-fight evasion counts, reconstructed entirely from
    /// views. Internal so the boundary test in <c>Hukbo.Core.Tests</c> can
    /// exercise the exact production observation against an unobserved twin
    /// simulation.
    /// </summary>
    /// <param name="LivingAgents">
    /// Agents alive at both ends of this tick's step.
    /// </param>
    /// <param name="RootedAgents">
    /// Of those, the ones whose displacement was strictly below
    /// <see cref="EvasiveMovementMetrics.RootedDisplacementThresholdRawPerTick"/>.
    /// </param>
    /// <param name="TravelRaw">
    /// The sum of this tick's displacement magnitudes across living agents, in
    /// raw units. A <see cref="long"/> rather than an <see cref="int"/> because
    /// it is a sum across the whole army rather than a per-agent figure.
    /// </param>
    /// <param name="ReachRetentionAgents">
    /// Living agents holding a living enemy target inside their own attack
    /// range, centre to centre.
    /// </param>
    /// <param name="TargetHeldAgents">
    /// Living agents holding a living enemy target at any range.
    /// </param>
    /// <param name="SlipLateralAgents">
    /// Living agents whose resolved action was
    /// <see cref="EvasiveAction.SlipLateral"/>; likewise for the four fields
    /// that follow.
    /// </param>
    /// <param name="DodgeIncomingAgents">
    /// Living agents whose action was <see cref="EvasiveAction.DodgeIncoming"/>.
    /// </param>
    /// <param name="GiveGroundAgents">
    /// Living agents whose action was <see cref="EvasiveAction.GiveGround"/>.
    /// </param>
    /// <param name="BreakOffAgents">
    /// Living agents whose action was <see cref="EvasiveAction.BreakOff"/>.
    /// </param>
    /// <param name="BreakOffArmedAgents">
    /// Living agents whose action was
    /// <see cref="EvasiveAction.BreakOffArmed"/>.
    /// </param>
    internal readonly record struct EvasiveTickObservation(
        int LivingAgents,
        int RootedAgents,
        long TravelRaw,
        int ReachRetentionAgents,
        int TargetHeldAgents,
        int SlipLateralAgents,
        int DodgeIncomingAgents,
        int GiveGroundAgents,
        int BreakOffAgents,
        int BreakOffArmedAgents);

    /// <summary>
    /// Reconstructs the in-fight evasion metrics of the 2026-08-15 evasion
    /// design outside the simulation, by comparing each tick's
    /// <see cref="AgentView"/>s against the previous tick's. Holds the whole
    /// run's per-agent scratch — spawn positions, previous positions, previous
    /// liveness, resolved attack ranges, and the identifier-to-slot map — so
    /// that the per-tick observation allocates nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every definition here deliberately matches
    /// <c>EvasionCalibrationHarness</c> in <c>Hukbo.Core.Tests</c>, the
    /// twenty-seed instrument that measured the V13 baseline the design's bars
    /// are written against, so the two instruments report the same quantity.
    /// The rooted test uses the same 60-raw threshold, and the retention test
    /// uses each warrior's own attack range rather than body contact, which the
    /// harness proved is always zero.
    /// </para>
    /// <para>
    /// It observes and never influences: it holds no reference to the
    /// simulation, it reads only the published view projection, and every
    /// figure it produces reaches the report and nothing else.
    /// </para>
    /// </remarks>
    internal sealed class EvasiveMovementObserver
    {
        private readonly int[] _spawnXRaw;
        private readonly int[] _spawnYRaw;
        private readonly int[] _previousXRaw;
        private readonly int[] _previousYRaw;
        private readonly bool[] _previousAlive;
        private readonly long[] _attackRangeSquared;

        // Read only by key and never enumerated, so no hash-set iteration
        // order can reach a reported figure. An entity identifier never
        // changes and no agent is ever added or removed mid-battle, so the map
        // is built once at spawn.
        private readonly Dictionary<ulong, int> _slotOfEntityId;

        /// <summary>
        /// Captures the spawn snapshot and resolves each warrior's own attack
        /// range once, from the same weapon profile
        /// <c>BattleSimulation.CreateAgent</c> reads. A loadout never changes
        /// over a battle, so resolving it per tick would be waste.
        /// </summary>
        /// <param name="spawnViews">
        /// The view projection as it stands before the first tick advances.
        /// </param>
        /// <param name="scenario">
        /// The validated scenario the run is executing, read for its combat
        /// preset and for its fallback attack range.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="spawnViews"/> is <see langword="null"/>.
        /// </exception>
        internal EvasiveMovementObserver(
            IReadOnlyList<AgentView> spawnViews,
            Scenario scenario)
        {
            ArgumentNullException.ThrowIfNull(spawnViews);

            var agentSlots = spawnViews.Count;
            _spawnXRaw = new int[agentSlots];
            _spawnYRaw = new int[agentSlots];
            _previousXRaw = new int[agentSlots];
            _previousYRaw = new int[agentSlots];
            _previousAlive = new bool[agentSlots];
            _attackRangeSquared = new long[agentSlots];
            _slotOfEntityId = new Dictionary<ulong, int>(agentSlots);

            var rules = CombatPresetRegistry.Get(scenario.CombatPreset);
            for (var index = 0; index < agentSlots; index++)
            {
                var view = spawnViews[index];
                _spawnXRaw[index] = view.XRaw;
                _spawnYRaw[index] = view.YRaw;
                _previousXRaw[index] = view.XRaw;
                _previousYRaw[index] = view.YRaw;
                _previousAlive[index] = view.IsAlive;
                _slotOfEntityId[view.EntityId] = index;

                var attackRangeRaw = rules.HasWeaponProfiles
                    ? rules.ResolveWeaponProfile(
                        view.Loadout.Weapon, view.Loadout.Shield).AttackRangeRaw
                    : scenario.AttackRangeRaw;
                _attackRangeSquared[index] =
                    checked((long)attackRangeRaw * attackRangeRaw);
            }
        }

        /// <summary>
        /// Derives one tick's evasion counts from the current views, then
        /// overwrites the retained positions and liveness so the next tick
        /// compares against this one. Allocation-free: the observer owns the
        /// single set of buffers for the whole run.
        /// </summary>
        /// <param name="currentViews">
        /// The view projection republished by the tick that just advanced.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="currentViews"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="currentViews"/> holds a different number of agents
        /// than the spawn snapshot did.
        /// </exception>
        internal EvasiveTickObservation ObserveTick(
            IReadOnlyList<AgentView> currentViews)
        {
            ArgumentNullException.ThrowIfNull(currentViews);
            if (currentViews.Count != _previousAlive.Length)
            {
                throw new ArgumentException(
                    $"The view buffers disagree on the agent count: " +
                    $"{currentViews.Count} current versus " +
                    $"{_previousAlive.Length} at spawn.",
                    nameof(currentViews));
            }

            var livingAgents = 0;
            var rootedAgents = 0;
            var travelRaw = 0L;
            var reachRetentionAgents = 0;
            var targetHeldAgents = 0;
            var slipLateralAgents = 0;
            var dodgeIncomingAgents = 0;
            var giveGroundAgents = 0;
            var breakOffAgents = 0;
            var breakOffArmedAgents = 0;

            for (var index = 0; index < _previousAlive.Length; index++)
            {
                var view = currentViews[index];

                // A displacement needs a living warrior at both ends of the
                // step. A warrior that died on this tick contributes neither a
                // living agent-tick nor a travel sample: dying is not standing
                // still, and it is not moving either.
                if (!view.IsAlive || !_previousAlive[index])
                {
                    _previousXRaw[index] = view.XRaw;
                    _previousYRaw[index] = view.YRaw;
                    _previousAlive[index] = view.IsAlive;
                    continue;
                }

                livingAgents++;

                var displacementRaw = FixedPoint.IntegerSquareRoot(
                    CollisionGeometry.SquaredDistance(
                        _previousXRaw[index],
                        _previousYRaw[index],
                        view.XRaw,
                        view.YRaw));
                travelRaw = checked(travelRaw + displacementRaw);
                if (displacementRaw <
                    EvasiveMovementMetrics.RootedDisplacementThresholdRawPerTick)
                {
                    rootedAgents++;
                }

                var targetSquaredDistance =
                    SquaredDistanceToLivingEnemyTarget(view, currentViews);
                if (targetSquaredDistance >= 0)
                {
                    targetHeldAgents++;
                    if (targetSquaredDistance <= _attackRangeSquared[index])
                    {
                        reachRetentionAgents++;
                    }
                }

                switch (view.EvasiveAction)
                {
                    case EvasiveAction.SlipLateral:
                        slipLateralAgents++;
                        break;
                    case EvasiveAction.DodgeIncoming:
                        dodgeIncomingAgents++;
                        break;
                    case EvasiveAction.GiveGround:
                        giveGroundAgents++;
                        break;
                    case EvasiveAction.BreakOff:
                        breakOffAgents++;
                        break;
                    case EvasiveAction.BreakOffArmed:
                        breakOffArmedAgents++;
                        break;
                    case EvasiveAction.None:
                    default:
                        break;
                }

                _previousXRaw[index] = view.XRaw;
                _previousYRaw[index] = view.YRaw;
                _previousAlive[index] = view.IsAlive;
            }

            return new EvasiveTickObservation(
                livingAgents,
                rootedAgents,
                travelRaw,
                reachRetentionAgents,
                targetHeldAgents,
                slipLateralAgents,
                dodgeIncomingAgents,
                giveGroundAgents,
                breakOffAgents,
                breakOffArmedAgents);
        }

        /// <summary>
        /// Sums the straight-line distance from every spawned agent's spawn
        /// position to its position in <paramref name="terminalViews"/>. Every
        /// slot contributes, alive or dead, because a corpse retains its final
        /// position and where the dead lie is part of where the battle was
        /// fought.
        /// </summary>
        /// <param name="terminalViews">
        /// The view projection at the run's terminal tick.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="terminalViews"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="terminalViews"/> holds a different number of agents
        /// than the spawn snapshot did.
        /// </exception>
        internal long ComputeNetDisplacementSumRaw(
            IReadOnlyList<AgentView> terminalViews)
        {
            ArgumentNullException.ThrowIfNull(terminalViews);
            if (terminalViews.Count != _spawnXRaw.Length)
            {
                throw new ArgumentException(
                    $"The view buffers disagree on the agent count: " +
                    $"{terminalViews.Count} terminal versus " +
                    $"{_spawnXRaw.Length} at spawn.",
                    nameof(terminalViews));
            }

            var netDisplacementSumRaw = 0L;
            for (var index = 0; index < _spawnXRaw.Length; index++)
            {
                var view = terminalViews[index];
                netDisplacementSumRaw = checked(netDisplacementSumRaw +
                    FixedPoint.IntegerSquareRoot(
                        CollisionGeometry.SquaredDistance(
                            _spawnXRaw[index],
                            _spawnYRaw[index],
                            view.XRaw,
                            view.YRaw)));
            }

            return netDisplacementSumRaw;
        }

        /// <summary>
        /// The squared centre distance from <paramref name="view"/> to its
        /// currently selected target, when that target is a living warrior of
        /// the other faction, and <c>-1</c> when the warrior holds no such
        /// target at all. Returning the distance rather than a boolean is what
        /// lets one pass over the views answer both retention questions — held
        /// at all, and held inside reach.
        /// </summary>
        private long SquaredDistanceToLivingEnemyTarget(
            AgentView view,
            IReadOnlyList<AgentView> views)
        {
            if (view.TargetEntityId is not { } targetEntityId)
            {
                return -1;
            }

            if (!_slotOfEntityId.TryGetValue(targetEntityId, out var targetSlot))
            {
                return -1;
            }

            var target = views[targetSlot];
            if (!target.IsAlive || target.FactionId == view.FactionId)
            {
                return -1;
            }

            return CollisionGeometry.SquaredDistance(
                view.XRaw, view.YRaw, target.XRaw, target.YRaw);
        }
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
    /// call the run would not otherwise make. The refusal count follows the
    /// same rule — the caller already derived it for the metrics accumulator
    /// — and the two movement fields ride the line only under a preset that
    /// resolves footwork at all, so a legacy run's lines stay byte-identical.
    /// </remarks>
    private static void LogTick(
        DiagnosticLog log,
        BattleSimulation simulation,
        ulong stateHash,
        bool includeMovementFields,
        int refuseAgents)
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

        if (includeMovementFields)
        {
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
                ToHex(stateHash),
                "refusals",
                refuseAgents,
                "conflictDenials",
                simulation.MovementConflictDenials);
            return;
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
            "--log-level" or "--log-channels" or "--log-dir" or "--preset" or
            "--movement-preset";

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
            battleEvent.Shield is { } shield
                ? unchecked((ulong)(uint)(int)shield)
                : ulong.MaxValue);
        AddToHash(
            ref hash,
            battleEvent.HitLocation is { } hitLocation
                ? unchecked((ulong)(uint)(int)hitLocation)
                : ulong.MaxValue);

        // The resolution is authoritative and rides on every attack event, so
        // a fold that ignored it would let a parry and a landed blow share a
        // replay signature. Absent-means-maximum, the same sentinel the two
        // nullable fields above use, so a non-attack event stays distinct from
        // any defined resolution.
        AddToHash(
            ref hash,
            battleEvent.Resolution is { } resolution
                ? unchecked((ulong)(uint)(int)resolution)
                : ulong.MaxValue);

        // The 12th and final word. A chain position is independently
        // nullable within an already-present combat context — most attacks
        // are not part of any chain even though Weapon/HitLocation/
        // Resolution are always present on an attack event — so a fold that
        // ignored it would let two otherwise-identical blows share a replay
        // signature even though one of them landed mid-chain and the other
        // did not. Same absent-means-maximum sentinel convention as the four
        // fields above.
        AddToHash(
            ref hash,
            battleEvent.ComboPosition is { } position
                ? (ulong)(uint)position
                : ulong.MaxValue);
    }

    private static void AddToHash(ref ulong hash, ulong value) =>
        Fnv1a.Add(ref hash, value);
}
