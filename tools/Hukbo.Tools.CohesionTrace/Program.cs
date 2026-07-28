using System.Globalization;
using System.Text;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;

namespace Hukbo.Tools.CohesionTrace;

/// <summary>
/// Measures why contingent cohesion stops firing partway through a faction's
/// advance, which is the one thing design section 7 of
/// docs/plans/2026-07-28-cohesion-scan-narrowing-design.md records as not
/// established. Section 10.3 of the formation-movement design forbids moving
/// the inertness bar's thresholds until that cause is known, so this program
/// exists to establish it rather than to change anything.
/// </summary>
/// <remarks>
/// <para>
/// The reconstruction is the same one
/// <c>PersistentContingentTests.CohesionObserver</c> performs: the six movement
/// gates are decided by pure functions of observable state, so two consecutive
/// agent snapshots plus the tick number are enough to replay every gate's answer
/// from outside the simulation. This program calls the same production statics
/// the test does, and adds two things the test cannot report: which gate denied
/// a contingent on each tick it did not cohere, and what the answer would have
/// been under a gate 6 that follows the preset it is measuring.
/// </para>
/// <para>
/// That second variant is the point. Gate 6, the cross-contingent bias-square
/// overlap test, is not one rule but two: under a preset that registers
/// <c>NarrowsCohesionScanToCohesionCapableContingents</c> the simulation skips
/// contingents whose tick-start state is <see cref="ContingentState.Close"/> or
/// <see cref="ContingentState.Break"/>, and denies every slot it skipped. The
/// naive variant below reproduces the test observer exactly, scanning every
/// living contingent and denying nothing extra. The narrowed variant reproduces
/// <c>BattleSimulation.TakesPartInCrossContingentScan</c>, including its two
/// details that are easy to lose: the state it reads is the leader's state at
/// the <em>start</em> of the tick, not the state the tick resolves to, and
/// exclusion from the scan is itself a denial.
/// </para>
/// <para>
/// Running both against the same battle separates a simulation that stops
/// cohering from an observer that stops seeing cohesion. Those two have the same
/// failure text and opposite remedies.
/// </para>
/// </remarks>
internal static class Program
{
    /// <summary>
    /// The reasons a contingent can fail to be granted a cohesion destination
    /// on a tick. The four slot-level members are properties of the contingent,
    /// so they deny every member at once; the three member-level members are
    /// recorded only when no slot-level reason applies, which is the case where
    /// the contingent was permitted to cohere and no individual member
    /// qualified.
    /// </summary>
    [Flags]
    private enum Denial
    {
        None = 0,

        /// <summary>Gate 1: state was not Hold or Advance.</summary>
        StateNotEligible = 1 << 0,

        /// <summary>Gate 3: the duty-cycle window was shut.</summary>
        WindowShut = 1 << 1,

        /// <summary>Gate 5: the bias square did not fit the map.</summary>
        SquareOffMap = 1 << 2,

        /// <summary>Gate 6: the bias square overlapped another contingent's.</summary>
        SquareOverlaps = 1 << 3,

        /// <summary>Gate 2: the only living member was the leader.</summary>
        LeaderOnly = 1 << 4,

        /// <summary>Gate 4: advancing, and no member was straggling.</summary>
        NotStraggling = 1 << 5,

        /// <summary>No living non-leader member was pursuing an enemy.</summary>
        NoMovingMember = 1 << 6,
    }

    /// <summary>Per-contingent totals accumulated over one battle.</summary>
    private sealed class SlotTally
    {
        internal int WindowLength { get; set; }

        internal int CoheringTicks { get; set; }

        internal int LastCoheringRelativeTick { get; set; }

        internal Dictionary<Denial, int> DeniedTicksByCause { get; } = [];

        internal Dictionary<Denial, int> SoleCauseTicks { get; } = [];
    }

    private static int Main(string[] args)
    {
        var firstSeed = 1UL;
        var lastSeed = 20UL;
        var preset = MovementPresetId.PersistentContingentsV4;
        var timelineSeed = 0UL;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--first-seed" when i + 1 < args.Length:
                    firstSeed = ulong.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
                case "--last-seed" when i + 1 < args.Length:
                    lastSeed = ulong.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
                case "--seed" when i + 1 < args.Length:
                    firstSeed = lastSeed = ulong.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
                case "--preset" when i + 1 < args.Length:
                    preset = Enum.Parse<MovementPresetId>(args[++i]);
                    break;
                case "--timeline" when i + 1 < args.Length:
                    timelineSeed = ulong.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
                default:
                    Console.Error.WriteLine($"unrecognised argument: {args[i]}");
                    return 2;
            }
        }

        Console.WriteLine($"preset {preset}, seeds {firstSeed}-{lastSeed}");
        Console.WriteLine(
            "naive gate 6 = every living contingent scanned (what the test " +
            "observer does)");
        Console.WriteLine(
            "narrowed gate 6 = Close/Break contingents skipped and denied " +
            "(what BattleSimulation does under this preset)");
        Console.WriteLine();

        var naiveFailures = 0;
        var narrowedFailures = 0;

        for (var seed = firstSeed; seed <= lastSeed; seed++)
        {
            var naive = RunOneSeed(seed, preset, narrow: false, timeline: false);
            var narrowed = RunOneSeed(
                seed,
                preset,
                narrow: true,
                timeline: seed == timelineSeed);

            naiveFailures += ReportSeed(seed, "naive   ", naive);
            narrowedFailures += ReportSeed(seed, "narrowed", narrowed);
        }

        Console.WriteLine();
        Console.WriteLine(
            $"inertness-bar failing rows: naive {naiveFailures}, " +
            $"narrowed {narrowedFailures}");
        return 0;
    }

    /// <summary>
    /// Replays one seed and returns, per faction, the per-contingent tallies
    /// the inertness bar reads plus the denial attribution it does not.
    /// </summary>
    private static Dictionary<int, Dictionary<int, SlotTally>> RunOneSeed(
        ulong seed,
        MovementPresetId preset,
        bool narrow,
        bool timeline)
    {
        var scenario = Scenario.CreateDefault(seed, totalAgents: 200) with
        {
            MovementPreset = preset,
        };
        var rules = MovementPresetRegistry.Get(preset);
        var simulation = BattleSimulation.Create(scenario);
        var mapWidthRaw = checked(scenario.MapWidth * FixedPoint.Scale);
        var mapHeightRaw = checked(scenario.MapHeight * FixedPoint.Scale);

        var tallies = new Dictionary<int, Dictionary<int, SlotTally>>();
        var windowIsOpen = new Dictionary<(int Faction, int Contingent), bool>();
        var hadALivingMember = new HashSet<(int Faction, int Contingent)>();

        foreach (var agent in simulation.Agents)
        {
            if (!tallies.TryGetValue(agent.FactionId, out var byContingent))
            {
                byContingent = [];
                tallies[agent.FactionId] = byContingent;
            }

            if (!byContingent.ContainsKey(agent.ContingentId))
            {
                byContingent[agent.ContingentId] = new SlotTally();
                windowIsOpen[(agent.FactionId, agent.ContingentId)] = true;
            }
        }

        var timelineRows = timeline ? new List<string>() : null;
        var preTick = Snapshot(simulation);

        while (simulation.Outcome == BattleOutcome.Ongoing &&
            simulation.Tick < scenario.TickLimit)
        {
            simulation.AdvanceOneTick();
            var postTick = Snapshot(simulation);
            var tick = checked((int)simulation.Tick);

            var evaluation = Evaluate(
                preTick,
                postTick,
                tick,
                rules,
                narrow,
                scenario.BodyRadiusRaw,
                mapWidthRaw,
                mapHeightRaw);

            var livingByContingent = new Dictionary<(int, int), int>();
            var stateByContingent = new Dictionary<(int, int), ContingentState>();
            foreach (var agent in postTick.Values)
            {
                if (!agent.IsAlive)
                {
                    continue;
                }

                var key = (agent.FactionId, agent.ContingentId);
                stateByContingent[key] = agent.ContingentState;
                livingByContingent[key] = livingByContingent.GetValueOrDefault(key) + 1;
            }

            foreach (var key in windowIsOpen.Keys.ToList())
            {
                if (!windowIsOpen[key])
                {
                    continue;
                }

                var living = livingByContingent.GetValueOrDefault(key);
                if (living > 0)
                {
                    hadALivingMember.Add(key);
                }

                var state = stateByContingent.GetValueOrDefault(key, ContingentState.None);
                var wipedOut = state == ContingentState.None &&
                    living == 0 &&
                    hadALivingMember.Contains(key);

                if (state is ContingentState.Close or ContingentState.Break || wipedOut)
                {
                    windowIsOpen[key] = false;
                    continue;
                }

                var tally = tallies[key.Item1][key.Item2];
                tally.WindowLength++;

                var slot = Slot(key.Item1, key.Item2);
                if (evaluation.SlotCoheres.Contains(slot))
                {
                    tally.CoheringTicks++;
                    tally.LastCoheringRelativeTick = tally.WindowLength;
                }
                else
                {
                    var cause = evaluation.SlotDenial.GetValueOrDefault(slot, Denial.None);
                    foreach (var flag in Enum.GetValues<Denial>())
                    {
                        if (flag != Denial.None && cause.HasFlag(flag))
                        {
                            tally.DeniedTicksByCause[flag] =
                                tally.DeniedTicksByCause.GetValueOrDefault(flag) + 1;
                        }
                    }

                    if (int.PopCount((int)cause) == 1)
                    {
                        tally.SoleCauseTicks[cause] =
                            tally.SoleCauseTicks.GetValueOrDefault(cause) + 1;
                    }
                }

                if (timelineRows is not null && key.Item1 == 1)
                {
                    var label = evaluation.SlotCoheres.Contains(slot)
                        ? "COHERE"
                        : Describe(evaluation.SlotDenial.GetValueOrDefault(slot, Denial.None));
                    timelineRows.Add(
                        $"t={tick,4} c{key.Item2} rel={tally.WindowLength,4} {label}");
                }
            }

            preTick = postTick;
        }

        if (timelineRows is not null)
        {
            WriteTimeline(seed, timelineRows);
        }

        return tallies;
    }

    /// <summary>
    /// Prints the three inertness-bar clauses for one seed and returns how many
    /// of them failed, so the caller can total failing rows across seeds.
    /// </summary>
    private static int ReportSeed(
        ulong seed,
        string label,
        Dictionary<int, Dictionary<int, SlotTally>> tallies)
    {
        var failures = 0;

        foreach (var (faction, byContingent) in tallies.OrderBy(entry => entry.Key))
        {
            var longestWindow = byContingent.Values.Max(tally => tally.WindowLength);
            var laterHalfThreshold = longestWindow / 2;
            var bestRelativeTick = byContingent.Values.Max(
                tally => tally.LastCoheringRelativeTick);
            var totalWindow = byContingent.Values.Sum(tally => tally.WindowLength);
            var totalCohering = byContingent.Values.Sum(tally => tally.CoheringTicks);
            var persistence = totalWindow == 0 ? 0.0 : (double)totalCohering / totalWindow;
            var covered = byContingent.Values.Count(tally => tally.CoheringTicks > 0);

            var spreadFailed = bestRelativeTick < laterHalfThreshold;
            var persistenceFailed = persistence < 0.10;
            var coverageFailed = covered < Math.Max(2, byContingent.Count / 2);
            failures += (spreadFailed ? 1 : 0) +
                (persistenceFailed ? 1 : 0) +
                (coverageFailed ? 1 : 0);

            Console.WriteLine(
                $"seed {seed,2} faction {faction} {label}: " +
                $"persistence {totalCohering}/{totalWindow} = {persistence:P1}" +
                $"{(persistenceFailed ? " FAIL" : string.Empty)}, " +
                $"coverage {covered}/{byContingent.Count}" +
                $"{(coverageFailed ? " FAIL" : string.Empty)}, " +
                $"latest cohering tick {bestRelativeTick} of window {longestWindow} " +
                $"(needs {laterHalfThreshold})" +
                $"{(spreadFailed ? " FAIL" : string.Empty)}");

            foreach (var (contingent, tally) in byContingent.OrderBy(entry => entry.Key))
            {
                var causes = string.Join(
                    " ",
                    tally.DeniedTicksByCause
                        .OrderByDescending(entry => entry.Value)
                        .Select(entry => $"{entry.Key}={entry.Value}"));
                var sole = string.Join(
                    " ",
                    tally.SoleCauseTicks
                        .OrderByDescending(entry => entry.Value)
                        .Select(entry => $"{entry.Key}={entry.Value}"));
                Console.WriteLine(
                    $"    c{contingent} window={tally.WindowLength,4} " +
                    $"cohered={tally.CoheringTicks,4} " +
                    $"last={tally.LastCoheringRelativeTick,4} | denied {causes} " +
                    $"| sole {sole}");
            }
        }

        return failures;
    }

    private static void WriteTimeline(ulong seed, List<string> rows)
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            $"cohesion-timeline-seed{seed}.txt");
        File.WriteAllLines(path, rows, Encoding.UTF8);
        Console.WriteLine($"timeline for faction 1 written to {path} ({rows.Count} rows)");
    }

    private static string Describe(Denial denial) =>
        denial == Denial.None ? "none" : denial.ToString().Replace(", ", "+");

    private readonly record struct TickResult(
        HashSet<int> SlotCoheres,
        Dictionary<int, Denial> SlotDenial);

    /// <summary>
    /// Replays every movement gate for one tick from two consecutive snapshots.
    /// With <paramref name="narrow"/> false this is line-for-line the test
    /// observer; with it true, gate 6 follows
    /// <c>BattleSimulation.TakesPartInCrossContingentScan</c> instead.
    /// </summary>
    private static TickResult Evaluate(
        IReadOnlyDictionary<ulong, AgentView> preTick,
        IReadOnlyDictionary<ulong, AgentView> postTick,
        int tick,
        MovementRuleset rules,
        bool narrow,
        int bodyRadiusRaw,
        int mapWidthRaw,
        int mapHeightRaw)
    {
        var slotCount = FormationPlanner.MaximumContingents * 2;
        var leaderEntityId = new ulong[slotCount];
        var livingCount = new int[slotCount];

        foreach (var agent in preTick.Values)
        {
            if (!agent.IsAlive)
            {
                continue;
            }

            var slot = Slot(agent.FactionId, agent.ContingentId);
            livingCount[slot]++;
            if (leaderEntityId[slot] == 0 || agent.EntityId < leaderEntityId[slot])
            {
                leaderEntityId[slot] = agent.EntityId;
            }
        }

        var trailBaseX = new int[slotCount];
        var trailBaseY = new int[slotCount];
        var margin = new int[slotCount];
        var squareFitsMap = new bool[slotCount];
        var squareOverlapsAnother = new bool[slotCount];
        var takesPartInScan = new bool[slotCount];

        for (var slot = 0; slot < slotCount; slot++)
        {
            if (livingCount[slot] == 0)
            {
                continue;
            }

            var leaderId = leaderEntityId[slot];
            var leaderPre = preTick[leaderId];
            var leaderPost = postTick[leaderId];

            // The state the production scan reads is the leader's at the start
            // of the tick, so it comes from the pre-tick snapshot. Reading the
            // resolved state here would make gate 6 and the state transition
            // mutually dependent, which is the circularity design section 3.1
            // breaks in this direction.
            takesPartInScan[slot] = !narrow ||
                MovementRules.ParticipatesInCrossContingentScan(leaderPre.ContingentState);

            var jitterRaw = FormationRules.ComputeContingentJitterRaw(
                bodyRadiusRaw, livingCount[slot]);
            var trailRaw = FormationRules.ComputeContingentTrailRaw(bodyRadiusRaw, jitterRaw);

            long deltaX = 0;
            long deltaY = 0;
            long distanceRaw = 0;
            if (leaderPost.TargetEntityId is { } targetId &&
                preTick.TryGetValue(targetId, out var targetPre))
            {
                deltaX = (long)targetPre.XRaw - leaderPre.XRaw;
                deltaY = (long)targetPre.YRaw - leaderPre.YRaw;
                distanceRaw = IntegerSquareRoot(checked((deltaX * deltaX) + (deltaY * deltaY)));
            }

            int tbx;
            int tby;
            if (distanceRaw == 0)
            {
                tbx = leaderPre.XRaw;
                tby = leaderPre.YRaw;
            }
            else
            {
                tbx = checked((int)(leaderPre.XRaw - (deltaX * trailRaw / distanceRaw)));
                tby = checked((int)(leaderPre.YRaw - (deltaY * trailRaw / distanceRaw)));
            }

            trailBaseX[slot] = tbx;
            trailBaseY[slot] = tby;
            margin[slot] = checked(jitterRaw + bodyRadiusRaw);
            squareFitsMap[slot] = FormationRules.IsCohesionSquareWithinBounds(
                tbx, tby, jitterRaw, bodyRadiusRaw, mapWidthRaw, mapHeightRaw);
        }

        for (var faction = 0; faction < 2; faction++)
        {
            var baseSlot = faction * FormationPlanner.MaximumContingents;
            for (var outer = 0; outer < FormationPlanner.MaximumContingents; outer++)
            {
                var outerSlot = baseSlot + outer;
                if (livingCount[outerSlot] == 0)
                {
                    continue;
                }

                // Exclusion from the scan and denial of the grant are one
                // decision, never two: a contingent skipped here has its
                // overlap flag forced true, exactly as BattleSimulation does.
                if (!takesPartInScan[outerSlot])
                {
                    squareOverlapsAnother[outerSlot] = true;
                    continue;
                }

                for (var inner = outer + 1; inner < FormationPlanner.MaximumContingents; inner++)
                {
                    var innerSlot = baseSlot + inner;
                    if (livingCount[innerSlot] == 0 || !takesPartInScan[innerSlot])
                    {
                        continue;
                    }

                    if (!FormationRules.DoCohesionSquaresOverlap(
                        trailBaseX[outerSlot], trailBaseY[outerSlot], margin[outerSlot],
                        trailBaseX[innerSlot], trailBaseY[innerSlot], margin[innerSlot]))
                    {
                        continue;
                    }

                    squareOverlapsAnother[outerSlot] = true;
                    squareOverlapsAnother[innerSlot] = true;
                }
            }
        }

        var slotCoheres = new HashSet<int>();
        var slotDenial = new Dictionary<int, Denial>();
        var slotHadMovingMember = new bool[slotCount];
        var slotHadNonLeader = new bool[slotCount];

        foreach (var (entityId, agentPre) in preTick)
        {
            if (!agentPre.IsAlive)
            {
                continue;
            }

            var slot = Slot(agentPre.FactionId, agentPre.ContingentId);
            var agentPost = postTick[entityId];
            var isLeader = entityId == leaderEntityId[slot];
            if (!isLeader)
            {
                slotHadNonLeader[slot] = true;
            }

            if (agentPost.Intent != AgentIntent.Moving || agentPost.TargetEntityId is null)
            {
                continue;
            }

            if (!isLeader)
            {
                slotHadMovingMember[slot] = true;
            }

            var windowOpen = MovementRules.IsCohesionWindowOpen(
                tick, slot, rules.CohesionCycleTicks, rules.CohesionDutyTicks);

            var straggling = false;
            if (agentPost.ContingentState == ContingentState.Advance)
            {
                var leaderPre = preTick[leaderEntityId[slot]];
                var dx = (long)agentPre.XRaw - leaderPre.XRaw;
                var dy = (long)agentPre.YRaw - leaderPre.YRaw;
                var memberSquared = checked((dx * dx) + (dy * dy));
                var cohesionRadiusRaw = checked(
                    (long)rules.CohesionRadiusMultiplier * bodyRadiusRaw);
                straggling = checked(16 * memberSquared) >
                    checked(9 * cohesionRadiusRaw * cohesionRadiusRaw);
            }

            if (MovementRules.IsCohesionEligible(
                agentPost.ContingentState,
                isLeader,
                windowOpen,
                straggling,
                squareFitsMap[slot],
                squareOverlapsAnother[slot]))
            {
                slotCoheres.Add(slot);
                continue;
            }

            if (!isLeader && straggling)
            {
                // This member cleared gate 4 and was still denied, so whatever
                // denied it is slot-level and is recorded below.
                slotHadMovingMember[slot] = true;
            }
        }

        for (var slot = 0; slot < slotCount; slot++)
        {
            if (livingCount[slot] == 0 || slotCoheres.Contains(slot))
            {
                continue;
            }

            var state = StateOfSlot(postTick, slot);
            var denial = Denial.None;

            if (state != ContingentState.Hold && state != ContingentState.Advance)
            {
                denial |= Denial.StateNotEligible;
            }

            if (!MovementRules.IsCohesionWindowOpen(
                tick, slot, rules.CohesionCycleTicks, rules.CohesionDutyTicks))
            {
                denial |= Denial.WindowShut;
            }

            if (!squareFitsMap[slot])
            {
                denial |= Denial.SquareOffMap;
            }

            if (squareOverlapsAnother[slot])
            {
                denial |= Denial.SquareOverlaps;
            }

            if (denial == Denial.None)
            {
                if (!slotHadNonLeader[slot])
                {
                    denial |= Denial.LeaderOnly;
                }
                else if (!slotHadMovingMember[slot])
                {
                    denial |= Denial.NoMovingMember;
                }
                else
                {
                    denial |= Denial.NotStraggling;
                }
            }

            slotDenial[slot] = denial;
        }

        return new TickResult(slotCoheres, slotDenial);
    }

    private static ContingentState StateOfSlot(
        IReadOnlyDictionary<ulong, AgentView> postTick,
        int slot)
    {
        foreach (var agent in postTick.Values)
        {
            if (agent.IsAlive && Slot(agent.FactionId, agent.ContingentId) == slot)
            {
                return agent.ContingentState;
            }
        }

        return ContingentState.None;
    }

    private static Dictionary<ulong, AgentView> Snapshot(BattleSimulation simulation) =>
        simulation.Agents.ToDictionary(agent => agent.EntityId);

    private static int Slot(int factionId, int contingentId) =>
        (factionId * FormationPlanner.MaximumContingents) + contingentId;

    private static long IntegerSquareRoot(long value)
    {
        var remainder = checked((ulong)value);
        ulong root = 0;
        var bit = 1UL << 62;

        while (bit > remainder)
        {
            bit >>= 2;
        }

        while (bit != 0)
        {
            if (remainder >= root + bit)
            {
                remainder -= root + bit;
                root = (root >> 1) + bit;
            }
            else
            {
                root >>= 1;
            }

            bit >>= 2;
        }

        return checked((long)root);
    }
}
