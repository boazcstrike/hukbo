using System.Globalization;
using System.Text;
using System.Text.Json;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Simulation;

namespace Hukbo.Tools.DeadlockProbe;

/// <summary>
/// Measures why a last-stand battle stalls at a body radius the resolver cannot
/// serve, without changing the resolver.
/// </summary>
/// <remarks>
/// <para>
/// This is step 1 of the follower-trailing deadlock design's section 9, which
/// asks which agents resolve <see cref="MovementResolution.Blocked"/> during a
/// stall, which body each was refused against, and whether that body was pending
/// or already committed. It also answers the design's step 2: whether the stall
/// is a half-rate column, where a follower is refused against a leader's stale
/// tick-start position and advances on roughly half of ticks, or a true mutual
/// lock, where neither of two agents can move at any rung regardless of order.
/// </para>
/// <para>
/// The design nominated the JSON Lines debug log as the vehicle. That is not
/// available inside the resolver: <c>Hukbo.Core</c> may never reference
/// <c>Hukbo.Diagnostics</c>, and the rule's own instruction is to observe the
/// simulation from outside instead. So this program reconstructs the resolver's
/// decision rather than instrumenting it, and every input to the reconstruction
/// is observable:
/// </para>
/// <list type="bullet">
/// <item>Who was blocked: <c>AgentView.MovementResolution</c> is authoritative
/// and is part of the state hash.</item>
/// <item>Where every body began and ended the tick: read the agent list before
/// and after <c>AdvanceOneTick</c>.</item>
/// <item>Which bodies were pending versus committed for a given mover: a
/// stationary body commits in pass one, before every mover; movers commit in
/// ascending <c>CollisionPriority.Resolve</c> key, which is a pure function of
/// the seed, the tick, and the entity ID.</item>
/// </list>
/// <para>
/// One thing is reconstructed as a bounded set rather than as a single body. The
/// resolver knows which specific body refused which specific rung; from outside,
/// what is recoverable exactly is the set of bodies geometrically capable of
/// refusing any candidate on the ladder. Every candidate lies within one
/// movement step of the agent's tick-start position and a refusal needs a centre
/// distance below one diameter, so a body further than
/// <c>diameter + movementSpeed</c> from that start position cannot have been
/// involved. That bound is exact, and it is a superset rather than a guess.
/// </para>
/// <para>
/// The probe runs the battle twice. The first pass finds the stall and the tick
/// of the last death; the second replays the same seed — the simulation is
/// deterministic, so it is the same battle — and emits per-tick detail for the
/// window after that death. That keeps the output bounded without needing to
/// decide in advance where the interesting window is.
/// </para>
/// </remarks>
internal static class Program
{
    private const int DefaultWindowTicks = 200;

    private static int Main(string[] args)
    {
        try
        {
            var options = ProbeOptions.Parse(args);

            return options.Survey
                ? RunSurvey(options)
                : RunSingleSeed(options);
        }
        catch (ArgumentException error)
        {
            Console.Error.WriteLine(error.Message);
            Console.Error.WriteLine();
            Console.Error.WriteLine(ProbeOptions.Usage);
            return 2;
        }
    }

    /// <summary>
    /// Runs every seed in the range and reports which ones stall, so a finding
    /// can say how widespread the failure is rather than assuming one seed is
    /// the only one.
    /// </summary>
    private static int RunSurvey(ProbeOptions options)
    {
        Console.WriteLine(
            $"Survey: seeds {options.FirstSeed} to {options.LastSeed}, " +
            $"{options.TotalAgents} agents, body radius " +
            $"{Describe(options.BodyRadiusRaw)} world units.");
        Console.WriteLine();
        Console.WriteLine("seed  ticks    outcome           living  stalled");

        var stalled = 0;

        for (var seed = options.FirstSeed; seed <= options.LastSeed; seed++)
        {
            var run = RunToCompletion(options, seed);

            if (run.Stalled)
            {
                stalled++;
            }

            Console.WriteLine(
                $"{seed,4}  {run.Ticks,7}  {run.Outcome,-16}  " +
                $"{run.LivingFaction0,2}/{run.LivingFaction1,-2}    " +
                $"{(run.Stalled ? "yes" : "no")}");
        }

        Console.WriteLine();
        Console.WriteLine(
            $"{stalled} of {options.LastSeed - options.FirstSeed + 1} seeds " +
            "reached the tick limit.");

        return 0;
    }

    private static int RunSingleSeed(ProbeOptions options)
    {
        var first = RunToCompletion(options, options.Seed);

        Console.WriteLine(
            $"Seed {options.Seed}, {options.TotalAgents} agents, body radius " +
            $"{Describe(options.BodyRadiusRaw)} world units.");
        Console.WriteLine(
            $"Stopped at tick {first.Ticks}, outcome {first.Outcome}, living " +
            $"{first.LivingFaction0}/{first.LivingFaction1}, last death at tick " +
            $"{first.LastDeathTick}.");

        if (!first.Stalled)
        {
            Console.WriteLine(
                "This seed did not reach the tick limit, so there is no stall " +
                "to classify. Nothing was written.");
            return 1;
        }

        var windowStart = Math.Max(1, first.LastDeathTick + 1);
        var windowEnd = Math.Min(first.Ticks, windowStart + options.WindowTicks - 1);

        Console.WriteLine(
            $"Classifying ticks {windowStart} to {windowEnd} on a replay of the " +
            "same seed.");
        Console.WriteLine();

        var classification = Classify(options, windowStart, windowEnd);

        WriteJsonLines(options.OutputPath, classification.Lines);
        Report(classification, windowStart, windowEnd);

        Console.WriteLine();
        Console.WriteLine($"Wrote {classification.Lines.Count} lines to {options.OutputPath}.");

        return 0;
    }

    private static RunSummary RunToCompletion(ProbeOptions options, ulong seed)
    {
        var scenario = BuildScenario(options, seed);
        var simulation = BattleSimulation.Create(scenario);
        var livingBefore = CountLiving(simulation);
        var lastDeathTick = 0L;

        while (simulation.Outcome == BattleOutcome.Ongoing &&
            simulation.Tick < scenario.TickLimit)
        {
            simulation.AdvanceOneTick();

            var living = CountLiving(simulation);

            if (living != livingBefore)
            {
                lastDeathTick = simulation.Tick;
                livingBefore = living;
            }
        }

        return new RunSummary(
            simulation.Tick,
            simulation.Outcome,
            simulation.Agents.Count(agent => agent.IsAlive && agent.FactionId == 0),
            simulation.Agents.Count(agent => agent.IsAlive && agent.FactionId == 1),
            lastDeathTick,
            simulation.Tick >= scenario.TickLimit);
    }

    /// <summary>
    /// Replays the battle and, for every tick inside the window, records each
    /// blocked agent together with the bodies capable of having refused it and
    /// how each of those bodies resolved.
    /// </summary>
    private static Classification Classify(
        ProbeOptions options,
        long windowStart,
        long windowEnd)
    {
        var scenario = BuildScenario(options, options.Seed);
        var simulation = BattleSimulation.Create(scenario);
        var diameterRaw = 2L * scenario.BodyRadiusRaw;
        var reachRaw = diameterRaw + scenario.MovementSpeedRaw;
        var lines = new List<string>();
        var blockedTicksByEntity = new Dictionary<ulong, int>();
        var observedTicksByEntity = new Dictionary<ulong, int>();
        var vacatingBlockerTicks = 0;
        var stationaryBlockerTicks = 0;
        var blockedRecords = 0;
        var blockerSetsByEntity = new Dictionary<ulong, HashSet<string>>();

        while (simulation.Outcome == BattleOutcome.Ongoing &&
            simulation.Tick < windowEnd)
        {
            var startPositions = simulation.Agents
                .Where(agent => agent.IsAlive)
                .ToDictionary(agent => agent.EntityId, agent => (agent.XRaw, agent.YRaw));

            simulation.AdvanceOneTick();

            var tick = simulation.Tick;

            if (tick < windowStart)
            {
                continue;
            }

            var living = simulation.Agents.Where(agent => agent.IsAlive).ToArray();

            foreach (var agent in living)
            {
                if (!observedTicksByEntity.TryAdd(agent.EntityId, 1))
                {
                    observedTicksByEntity[agent.EntityId]++;
                }
            }

            foreach (var agent in living)
            {
                if (agent.MovementResolution != MovementResolution.Blocked)
                {
                    continue;
                }

                if (!blockedTicksByEntity.TryAdd(agent.EntityId, 1))
                {
                    blockedTicksByEntity[agent.EntityId]++;
                }

                if (!startPositions.TryGetValue(agent.EntityId, out var start))
                {
                    continue;
                }

                var agentKey = PriorityKeyOf(scenario.Seed, tick, agent);
                var blockers = new List<BlockerRecord>();

                foreach (var other in living)
                {
                    if (other.EntityId == agent.EntityId ||
                        !startPositions.TryGetValue(other.EntityId, out var otherStart))
                    {
                        continue;
                    }

                    // A body further than one diameter plus one movement step
                    // from this agent's tick-start position cannot have refused
                    // any candidate on its ladder, at either of its positions.
                    if (!WithinReach(start, otherStart, reachRaw) &&
                        !WithinReach(start, (other.XRaw, other.YRaw), reachRaw))
                    {
                        continue;
                    }

                    var otherIsMover = IsMover(other.MovementResolution);
                    var otherKey = PriorityKeyOf(scenario.Seed, tick, other);
                    var wasPending = otherIsMover && otherKey > agentKey;
                    var vacated = otherStart.XRaw != other.XRaw ||
                        otherStart.YRaw != other.YRaw;

                    blockers.Add(new BlockerRecord(
                        other.EntityId,
                        wasPending,
                        vacated,
                        other.MovementResolution));
                }

                if (blockers.Count == 0)
                {
                    continue;
                }

                blockedRecords++;

                if (blockers.Any(blocker => blocker.WasPending && blocker.Vacated))
                {
                    vacatingBlockerTicks++;
                }

                if (blockers.All(blocker => !blocker.Vacated))
                {
                    stationaryBlockerTicks++;
                }

                var signature = string.Join(
                    ',',
                    blockers.Select(blocker => blocker.EntityId).Order());

                if (!blockerSetsByEntity.TryGetValue(agent.EntityId, out var signatures))
                {
                    signatures = [];
                    blockerSetsByEntity[agent.EntityId] = signatures;
                }

                signatures.Add(signature);

                lines.Add(SerializeBlocked(tick, agent, start, blockers));
            }
        }

        return new Classification(
            lines,
            blockedTicksByEntity,
            observedTicksByEntity,
            blockerSetsByEntity,
            blockedRecords,
            vacatingBlockerTicks,
            stationaryBlockerTicks);
    }

    private static void Report(
        Classification classification,
        long windowStart,
        long windowEnd)
    {
        var windowTicks = windowEnd - windowStart + 1;

        Console.WriteLine($"Window: {windowTicks} ticks.");
        Console.WriteLine($"Blocked agent-ticks with at least one blocker in reach: {classification.BlockedRecords}.");
        Console.WriteLine(
            $"  ... of which at least one pending blocker vacated its ground: " +
            $"{classification.VacatingBlockerTicks} " +
            $"({Percent(classification.VacatingBlockerTicks, classification.BlockedRecords)}).");
        Console.WriteLine(
            $"  ... of which every blocker stayed exactly put: " +
            $"{classification.StationaryBlockerTicks} " +
            $"({Percent(classification.StationaryBlockerTicks, classification.BlockedRecords)}).");
        Console.WriteLine();
        Console.WriteLine("Per agent, over the window:");
        Console.WriteLine("entity  ticks  blocked  blocked%  distinct blocker sets");

        foreach (var entityId in classification.ObservedTicks.Keys.Order())
        {
            var observed = classification.ObservedTicks[entityId];
            var blocked = classification.BlockedTicks.GetValueOrDefault(entityId);
            var distinctSets = classification.BlockerSets.TryGetValue(entityId, out var sets)
                ? sets.Count
                : 0;

            Console.WriteLine(
                $"{entityId,6}  {observed,5}  {blocked,7}  " +
                $"{Percent(blocked, observed),8}  {distinctSets,21}");
        }
    }

    private static string SerializeBlocked(
        long tick,
        AgentView agent,
        (int XRaw, int YRaw) start,
        List<BlockerRecord> blockers)
    {
        var buffer = new MemoryStream();

        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("t", tick);
            writer.WriteString("ev", "collision.blocked");
            writer.WriteNumber("entityId", agent.EntityId);
            writer.WriteNumber("factionId", agent.FactionId);
            writer.WriteNumber("startXRaw", start.XRaw);
            writer.WriteNumber("startYRaw", start.YRaw);
            writer.WriteNumber("blockerCount", blockers.Count);
            writer.WriteNumber(
                "pendingBlockerCount",
                blockers.Count(blocker => blocker.WasPending));
            writer.WriteNumber(
                "vacatingBlockerCount",
                blockers.Count(blocker => blocker.Vacated));
            writer.WriteString(
                "blockers",
                string.Join(
                    ' ',
                    blockers.Select(blocker =>
                        $"{blocker.EntityId}:" +
                        $"{(blocker.WasPending ? "pending" : "committed")}:" +
                        $"{(blocker.Vacated ? "vacated" : "stayed")}:" +
                        $"{blocker.Resolution}")));
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void WriteJsonLines(string path, IReadOnlyList<string> lines)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllLines(path, lines);
    }

    private static Scenario BuildScenario(ProbeOptions options, ulong seed) =>
        Scenario.CreateDefault(seed, options.TotalAgents) with
        {
            LastStandThresholdAgents = FormationRules.MaximumLastStandThresholdAgents,
            BodyRadiusRaw = options.BodyRadiusRaw,
        };

    private static ulong PriorityKeyOf(ulong seed, long tick, AgentView agent) =>
        IsMover(agent.MovementResolution)
            ? CollisionPriority.Resolve(seed, tick, agent.EntityId)
            : 0UL;

    /// <summary>
    /// A resolution that only a mover can produce. A body with no proposal
    /// resolves to <see cref="MovementResolution.None"/>, or to
    /// <see cref="MovementResolution.Separated"/> when the co-location repair
    /// moved it, and both commit in pass one ahead of every mover.
    /// </summary>
    private static bool IsMover(MovementResolution resolution) =>
        resolution is MovementResolution.Moved
            or MovementResolution.Slid
            or MovementResolution.Truncated
            or MovementResolution.Blocked;

    private static bool WithinReach(
        (int XRaw, int YRaw) left,
        (int XRaw, int YRaw) right,
        long reachRaw)
    {
        var deltaXRaw = (long)left.XRaw - right.XRaw;
        var deltaYRaw = (long)left.YRaw - right.YRaw;

        return (deltaXRaw * deltaXRaw) + (deltaYRaw * deltaYRaw) <= reachRaw * reachRaw;
    }

    private static int CountLiving(BattleSimulation simulation) =>
        simulation.Agents.Count(agent => agent.IsAlive);

    private static string Percent(int part, int whole) =>
        whole == 0
            ? "n/a"
            : ((double)part / whole).ToString("P1", CultureInfo.InvariantCulture);

    private static string Describe(int radiusRaw) =>
        ((double)radiusRaw / FixedPoint.Scale).ToString("0.###", CultureInfo.InvariantCulture);

    private sealed record RunSummary(
        long Ticks,
        BattleOutcome Outcome,
        int LivingFaction0,
        int LivingFaction1,
        long LastDeathTick,
        bool Stalled);

    private sealed record BlockerRecord(
        ulong EntityId,
        bool WasPending,
        bool Vacated,
        MovementResolution Resolution);

    private sealed record Classification(
        List<string> Lines,
        Dictionary<ulong, int> BlockedTicks,
        Dictionary<ulong, int> ObservedTicks,
        Dictionary<ulong, HashSet<string>> BlockerSets,
        int BlockedRecords,
        int VacatingBlockerTicks,
        int StationaryBlockerTicks);

    private sealed record ProbeOptions(
        ulong Seed,
        int TotalAgents,
        int BodyRadiusRaw,
        int WindowTicks,
        string OutputPath,
        bool Survey,
        ulong FirstSeed,
        ulong LastSeed)
    {
        internal const string Usage = """
            Usage:
              Hukbo.Tools.DeadlockProbe [options]

            Options:
              --seed <n>          Seed to classify. Default 12.
              --agents <n>        Total agents across both factions. Default 18.
              --radius-raw <n>    Body radius in raw fixed-point units.
                                  Default 4608, which is 4.5 world units.
              --window <n>        Ticks to classify after the last death.
                                  Default 200.
              --out <path>        JSON Lines output path. Default
                                  artifacts/deadlock-probe-seed<seed>.jsonl
              --survey            Report stall or no stall for a seed range
                                  instead of classifying one seed.
              --first-seed <n>    Survey range start. Default 1.
              --last-seed <n>     Survey range end. Default 20.
            """;

        internal static ProbeOptions Parse(string[] args)
        {
            var seed = 12UL;
            var totalAgents = 18;
            var bodyRadiusRaw = (9 * FixedPoint.Scale) / 2;
            var windowTicks = DefaultWindowTicks;
            string? outputPath = null;
            var survey = false;
            var firstSeed = 1UL;
            var lastSeed = 20UL;

            for (var index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--seed":
                        seed = ParseUInt64(args, ref index);
                        break;

                    case "--agents":
                        totalAgents = ParseInt32(args, ref index);
                        break;

                    case "--radius-raw":
                        bodyRadiusRaw = ParseInt32(args, ref index);
                        break;

                    case "--window":
                        windowTicks = ParseInt32(args, ref index);
                        break;

                    case "--out":
                        outputPath = ParseString(args, ref index);
                        break;

                    case "--survey":
                        survey = true;
                        break;

                    case "--first-seed":
                        firstSeed = ParseUInt64(args, ref index);
                        break;

                    case "--last-seed":
                        lastSeed = ParseUInt64(args, ref index);
                        break;

                    default:
                        throw new ArgumentException($"Unknown option '{args[index]}'.");
                }
            }

            if (windowTicks <= 0)
            {
                throw new ArgumentException("--window must be positive.");
            }

            return new ProbeOptions(
                seed,
                totalAgents,
                bodyRadiusRaw,
                windowTicks,
                outputPath ?? $"artifacts/deadlock-probe-seed{seed}.jsonl",
                survey,
                firstSeed,
                lastSeed);
        }

        private static string ParseString(string[] args, ref int index)
        {
            index++;

            if (index >= args.Length)
            {
                throw new ArgumentException($"Option '{args[index - 1]}' needs a value.");
            }

            return args[index];
        }

        private static int ParseInt32(string[] args, ref int index) =>
            int.Parse(ParseString(args, ref index), CultureInfo.InvariantCulture);

        private static ulong ParseUInt64(string[] args, ref int index) =>
            ulong.Parse(ParseString(args, ref index), CultureInfo.InvariantCulture);
    }
}
