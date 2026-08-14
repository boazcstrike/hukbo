using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Hukbo.Core.Combat;
using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests;

/// <summary>
/// RU-24's calibration instrument. Measures, but does not assert, the two
/// quantitative acceptance bands and the termination bar the ranged-units
/// plan's RU-24 row names, against <see cref="CombatPresetId.PrecolonialPhilippinesV5"/>
/// paired with <see cref="MovementPresetId.RangedStandoffV8"/> — the only
/// legal pairing for a ranged roster (V5 throws under V6 and V7, both of
/// which register no loadout movement profile for a ranged
/// <see cref="CombatLoadout"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Measures. Does not tune, does not assert.</b> Matches the precedent of
/// <c>PressureInterruptCalibrationHarness</c>: every threshold in
/// <see cref="PhilippineCombatPresetV5"/> is owned there, and this file only
/// produces a block of text a person reads and pastes into
/// the ranged units plan, section 9. Nothing here is a
/// second gate — <c>PhilippineCombatIntegrationTests.cs</c> and the future
/// <c>RangedTerminationTests.cs</c> (RU-29) own the pinned assertions.
/// </para>
/// <para>
/// <b>Not a test in the build the canonical gate performs.</b> The one
/// deliberate entry point, <see cref="RunCalibration"/>, is reachable only
/// from a <c>[Fact]</c> compiled behind the <c>HUKBO_CALIBRATION</c>
/// preprocessor symbol, which no script and no gate stage defines. Twenty
/// full battles plus a ten-cell matrix at up to 500 agents is minutes of
/// work no ordinary gate run has any reason to spend.
/// </para>
/// <para>
/// It lives in <c>Hukbo.Core.Tests</c> rather than <c>Hukbo.Headless</c> for
/// the same reason the plan's RU-24 row states: the headless runner exposes
/// no roster-share flag, and <c>AgentState</c> is internal to
/// <c>Hukbo.Core</c>, so only the test assembly can read the per-agent state
/// this harness needs.
/// </para>
/// <para>
/// <b>The roster share is a tuning lever this harness owns, not the
/// preset.</b> <c>Scenario.RosterCounts</c> is set explicitly on every
/// scenario this harness builds; <see cref="PhilippineCombatPresetV5"/> is
/// never edited to bias its own roster distribution, because
/// <c>BattleSimulation.cs:571-574</c> spreads warriors evenly over all nine
/// roster entries whenever <c>RosterCounts</c> is left unset, which would
/// field far more melee than ranged and never reach a defensible calibration.
/// </para>
/// <para>
/// <b>RU-45</b> appended two <see cref="ShieldId.TallHardwood"/> roster
/// entries (Kalis, Itak) to <see cref="PhilippineCombatPresetV5"/>, so this
/// file's roster-order lists, its default weights, and band (b) below all
/// move from seven entries to nine.
/// </para>
/// </remarks>
internal static class RangedCalibrationHarness
{
    /// <summary>
    /// The plan's own acceptance seed range for the two quantitative bands
    /// and the termination bar.
    /// </summary>
    private static readonly ulong[] TwentySeeds =
        [.. Enumerable.Range(1, 20).Select(value => (ulong)value)];

    /// <summary>
    /// The plan's own ten-cell matrix seeds.
    /// </summary>
    private static readonly ulong[] MatrixSeeds = [1, 2, 3, 5, 8];

    /// <summary>
    /// The plan's own ten-cell matrix agent counts.
    /// </summary>
    private static readonly int[] MatrixAgentCounts = [200, 500];

    /// <summary>
    /// The plan's own termination bar: "before the 5,000-tick cap".
    /// </summary>
    private const int TickCeiling = 5_000;

    /// <summary>
    /// The recorded <c>PersistentContingentsV4</c> baseline this harness
    /// prints beside its own ten-cell matrix, reproduced verbatim from
    /// <c>docs/research/ranged/2026-08-07-STANDOFF-ROOT-CAUSE.md:140-149</c>,
    /// itself sourced from
    /// the 2026-07-31 movement V7 pre-change baseline record, lines 553 to 562.
    /// Printed for comparison only — this harness does not re-run V4.
    /// </summary>
    private static readonly (int Agents, ulong Seed, long TerminalTick, string Outcome)[]
        V4Baseline =
        [
            (200, 1, 1_279, "Faction0Victory"),
            (200, 2, 1_439, "Faction0Victory"),
            (200, 3, 2_037, "Faction1Victory"),
            (200, 5, 2_230, "Faction1Victory"),
            (200, 8, 2_284, "Faction0Victory"),
            (500, 1, 2_934, "Faction0Victory"),
            (500, 2, 2_551, "Faction0Victory"),
            (500, 3, 4_085, "Faction0Victory"),
            (500, 5, 2_568, "Faction0Victory"),
            (500, 8, 4_405, "Faction1Victory"),
        ];

    /// <summary>
    /// Runs the whole RU-24 measurement: the twenty-seed table (bands a and
    /// b, plus the termination bar's per-seed data), then the ten-cell
    /// matrix, and returns the whole report as one block of text.
    /// </summary>
    /// <param name="rosterCounts">
    /// The nine-entry roster split to measure, indexed against
    /// <see cref="PhilippineCombatPresetV5.Rules"/>'s own roster order
    /// (Kampilan, Wasay, Kalis, Itak, Bangkaw, Busog, Arquebus, Kalis +
    /// TallHardwood, Itak + TallHardwood). Scaled by
    /// <see cref="BuildRosterCounts"/> to whatever <c>AgentsPerFaction</c> a
    /// given cell needs, using a largest-remainder apportionment so every
    /// scaled roster still sums exactly to that cell's <c>AgentsPerFaction</c>,
    /// as <c>Scenario.Validate</c> requires.
    /// </param>
    internal static string RunCalibration(IReadOnlyList<int> rosterCounts)
    {
        var rules = PhilippineCombatPresetV5.Rules;
        if (rosterCounts.Count != rules.Roster.Count)
        {
            throw new ArgumentException(
                $"rosterCounts must have exactly {rules.Roster.Count} " +
                $"entries, one per PhilippineCombatPresetV5 roster row; got " +
                $"{rosterCounts.Count}.",
                nameof(rosterCounts));
        }

        var report = new StringBuilder();
        WriteHeader(report, rosterCounts);

        var seedResults = new List<SeedResult>(TwentySeeds.Length);
        foreach (var seed in TwentySeeds)
        {
            seedResults.Add(RunOneSeed(seed, totalAgents: 200, rosterCounts));
        }

        WriteBandA(report, seedResults);
        WriteBandB(report, seedResults);
        WriteTerminationBar(report, seedResults);
        WriteTenCellMatrix(report, rosterCounts);

        return report.ToString().ReplaceLineEndings();
    }

    /// <summary>
    /// Apportions <paramref name="shareWeights"/> (one weight per
    /// <see cref="PhilippineCombatPresetV5.Rules"/> roster row, in that
    /// row's order) over <paramref name="agentsPerFaction"/> using the
    /// largest-remainder method, so the result always sums to exactly
    /// <paramref name="agentsPerFaction"/> regardless of rounding —
    /// <see cref="Simulation.Scenario.Validate"/> rejects anything else.
    /// </summary>
    internal static ImmutableArray<int> BuildRosterCounts(
        int agentsPerFaction,
        IReadOnlyList<int> shareWeights)
    {
        var totalWeight = shareWeights.Sum();
        if (totalWeight <= 0)
        {
            throw new ArgumentException(
                "shareWeights must sum to a positive total.",
                nameof(shareWeights));
        }

        var counts = new int[shareWeights.Count];
        var remainders = new double[shareWeights.Count];
        var assigned = 0;

        for (var index = 0; index < shareWeights.Count; index++)
        {
            var exact = (double)agentsPerFaction * shareWeights[index] / totalWeight;
            counts[index] = (int)Math.Floor(exact);
            remainders[index] = exact - counts[index];
            assigned += counts[index];
        }

        var remaining = agentsPerFaction - assigned;
        // Largest-remainder-first, ties broken on ascending index for a
        // stable, deterministic apportionment.
        foreach (var index in Enumerable.Range(0, shareWeights.Count)
                     .OrderByDescending(index => remainders[index])
                     .ThenBy(index => index))
        {
            if (remaining <= 0)
            {
                break;
            }

            counts[index]++;
            remaining--;
        }

        return [.. counts];
    }

    private static SeedResult RunOneSeed(
        ulong seed,
        int totalAgents,
        IReadOnlyList<int> shareWeights)
    {
        var agentsPerFaction = totalAgents / 2;
        var rosterCounts = BuildRosterCounts(agentsPerFaction, shareWeights);

        var scenario = Scenario.CreateDefault(seed, totalAgents) with
        {
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV5,
            MovementPreset = MovementPresetId.RangedStandoffV8,
            RosterCounts = rosterCounts,
            TickLimit = TickCeiling,
        };
        scenario.Validate();

        var simulation = BattleSimulation.Create(scenario);
        long accepted = 0, landed = 0, shieldBlocked = 0, parried = 0,
            deflected = 0, evaded = 0;
        var attacksReceived = new Dictionary<ulong, int>();

        while (simulation.Outcome == BattleOutcome.Ongoing &&
            simulation.Tick < scenario.TickLimit)
        {
            simulation.AdvanceOneTick();

            var tick = simulation.LastTickCombat;
            accepted += tick.AcceptedAttacks;
            landed += tick.LandedAttacks;
            shieldBlocked += tick.ShieldBlockedAttacks;
            parried += tick.ParriedAttacks;
            deflected += tick.DeflectedAttacks;
            evaded += tick.EvadedAttacks;

            foreach (var battleEvent in simulation.LastEvents)
            {
                if (battleEvent.Kind != BattleEventKind.Attack ||
                    battleEvent.TargetEntityId is not { } targetEntityId)
                {
                    continue;
                }

                attacksReceived.TryGetValue(targetEntityId, out var already);
                attacksReceived[targetEntityId] = already + 1;
            }
        }

        var metrics = new CombatMetrics(
            accepted, landed, shieldBlocked, parried, deflected, evaded);

        long shieldedReceived = 0, shieldlessReceived = 0;
        int shieldedTotal = 0, shieldlessTotal = 0;
        foreach (var agent in simulation.Agents)
        {
            attacksReceived.TryGetValue(agent.EntityId, out var received);
            if (agent.Loadout.Shield == ShieldId.None)
            {
                shieldlessTotal++;
                shieldlessReceived += received;
            }
            else
            {
                shieldedTotal++;
                shieldedReceived += received;
            }
        }

        return new SeedResult(
            seed,
            simulation.Tick,
            simulation.Outcome,
            metrics,
            shieldedReceived,
            shieldedTotal,
            shieldlessReceived,
            shieldlessTotal);
    }

    private static void WriteHeader(
        StringBuilder report,
        IReadOnlyList<int> rosterCounts)
    {
        var rules = PhilippineCombatPresetV5.Rules;
        report.AppendLine(
            "Hukbo RU-24 ranged calibration harness — PrecolonialPhilippinesV5 " +
            "+ RangedStandoffV8");
        report.AppendLine(
            "Measurement only. Nothing here asserts, passes, or fails.");
        report.AppendLine();
        report.AppendLine("roster share weights (200-agent battle, per faction):");
        for (var index = 0; index < rosterCounts.Count; index++)
        {
            report.Append(CultureInfo.InvariantCulture,
                $"  {rules.Roster[index].Weapon,-10} weight={rosterCounts[index]}\n");
        }

        report.AppendLine();
    }

    private static void WriteBandA(
        StringBuilder report,
        IReadOnlyList<SeedResult> seedResults)
    {
        const double LowerBound = 0.25;
        const double UpperBound = 0.45;

        report.AppendLine(
            "== Band a: CombatMetrics.DefenceAttributableShare, seeds 1-20, " +
            "band 0.25-0.45 ==");
        report.AppendLine("seed  acceptedAttacks  share    inBand");

        var allInBand = true;
        foreach (var result in seedResults)
        {
            var share = result.Metrics.DefenceAttributableShare;
            var inBand = result.Metrics.AcceptedAttacks > 0 &&
                share >= LowerBound && share <= UpperBound;
            allInBand &= inBand;

            report.Append(CultureInfo.InvariantCulture,
                $"{result.Seed,4}  {result.Metrics.AcceptedAttacks,15}  " +
                $"{share.ToString("F4", CultureInfo.InvariantCulture),7}  " +
                $"{(inBand ? "yes" : "NO")}\n");
        }

        report.AppendLine();
        report.Append(CultureInfo.InvariantCulture,
            $"band a verdict: {(allInBand ? "PASS" : "FAIL")} — all twenty " +
            $"seeds inside 0.25 to 0.45: {allInBand}\n");
        report.AppendLine();
    }

    private static void WriteBandB(
        StringBuilder report,
        IReadOnlyList<SeedResult> seedResults)
    {
        report.AppendLine(
            "== Band b: shielded roster entries absorb more blows before " +
            "dying than shieldless ones ==");

        // RU-45 gave PhilippineCombatPresetV5 two ShieldId.TallHardwood
        // roster entries (Kalis, Itak), so this band is measurable by
        // default. This guard stays as a defensive fallback only: a caller
        // that passes rosterCounts weighting both shielded entries to zero
        // would still get an honest UNMEASURABLE line rather than a
        // division-derived garbage ratio, exactly as before RU-45.
        var anyShielded = seedResults.Any(result => result.ShieldedTotal > 0);
        if (!anyShielded)
        {
            report.AppendLine(
                "UNMEASURABLE: the given rosterCounts field zero agents on " +
                "both of PhilippineCombatPresetV5's ShieldId.TallHardwood " +
                "roster entries (Kalis, Itak), so shieldedTotal is 0 for " +
                "every seed. Not widened, not fabricated.");
            report.AppendLine();
            return;
        }

        // Two different bars appear on every row, and they are not the same
        // thing. `criterion(>1x)` is the plan's acceptance test verbatim —
        // the ranged units plan band (b): "shielded roster
        // entries still absorbing more blows than shieldless". The verdict
        // line below is computed from this column only. `observation(>1.15x)`
        // is a stricter margin RU-45 added on top, never stated by the plan;
        // it is kept because it is genuinely informative headroom signal, not
        // because it gates anything. The two diverge at the shipped default
        // roster composition (HUKBO_RANGED_ROSTER_WEIGHTS=63,63,14,31,16,11,
        // 8,13,31): every seed clears the plan's >1x criterion there, but two
        // seeds (one of them exactly at the boundary) fall under the 1.15x
        // margin. A FAIL derived from the margin column would misreport a
        // configuration the plan calls passing.
        report.AppendLine(
            "seed  shieldedMean  shieldlessMean  ratio  criterion(>1x)  " +
            "observation(>1.15x)");
        var allMeetCriterion = true;
        var marginHoldCount = 0;
        foreach (var result in seedResults)
        {
            var shieldedMean = result.ShieldedTotal == 0
                ? 0
                : (double)result.ShieldedReceived / result.ShieldedTotal;
            var shieldlessMean = result.ShieldlessTotal == 0
                ? 0
                : (double)result.ShieldlessReceived / result.ShieldlessTotal;
            var meetsCriterion = shieldedMean > shieldlessMean;
            var meetsMargin = shieldedMean > shieldlessMean * 1.15;
            allMeetCriterion &= meetsCriterion;
            marginHoldCount += meetsMargin ? 1 : 0;

            report.Append(CultureInfo.InvariantCulture,
                $"{result.Seed,4}  " +
                $"{shieldedMean.ToString("F2", CultureInfo.InvariantCulture),12}  " +
                $"{shieldlessMean.ToString("F2", CultureInfo.InvariantCulture),14}  " +
                $"{(shieldlessMean == 0 ? 0 : shieldedMean / shieldlessMean).ToString("F2", CultureInfo.InvariantCulture),5}  " +
                $"{(meetsCriterion ? "yes" : "NO"),14}  " +
                $"{(meetsMargin ? "yes" : "no"),20}\n");
        }

        report.Append(CultureInfo.InvariantCulture,
            $"band b margin observation (not the verdict, informational " +
            $"only): {marginHoldCount} of {seedResults.Count} seeds clear " +
            $"the stricter 1.15x margin\n");
        report.Append(CultureInfo.InvariantCulture,
            $"band b verdict: {(allMeetCriterion ? "PASS" : "FAIL")} — the " +
            $"plan's criterion is shieldedMean strictly greater than " +
            $"shieldlessMean (ratio > 1.0x); the 1.15x column above is an " +
            $"observation, not part of this verdict\n");
        report.AppendLine();
    }

    private static void WriteTerminationBar(
        StringBuilder report,
        IReadOnlyList<SeedResult> seedResults)
    {
        report.AppendLine(
            "== Termination bar: 19/20 decisive before 5,000 ticks, median " +
            "<=5,000, each faction wins >=4/20 ==");
        report.AppendLine("seed  terminalTick  outcome           decisive");

        var decisiveTicks = new List<long>();
        var decisiveCount = 0;
        var faction0Wins = 0;
        var faction1Wins = 0;

        foreach (var result in seedResults)
        {
            var decisive = result.Outcome is BattleOutcome.Faction0Victory or
                BattleOutcome.Faction1Victory;
            if (decisive)
            {
                decisiveCount++;
                decisiveTicks.Add(result.TerminalTick);
            }

            if (result.Outcome == BattleOutcome.Faction0Victory)
            {
                faction0Wins++;
            }
            else if (result.Outcome == BattleOutcome.Faction1Victory)
            {
                faction1Wins++;
            }

            report.Append(CultureInfo.InvariantCulture,
                $"{result.Seed,4}  {result.TerminalTick,12}  " +
                $"{result.Outcome,-16}  {(decisive ? "yes" : "NO")}\n");
        }

        var median = Median(decisiveTicks);
        report.AppendLine();
        report.Append(CultureInfo.InvariantCulture,
            $"decisive: {decisiveCount}/20 (need >=19)\n");
        report.Append(CultureInfo.InvariantCulture,
            $"median decisive tick: {median.ToString("F1", CultureInfo.InvariantCulture)} (need <=5000)\n");
        report.Append(CultureInfo.InvariantCulture,
            $"faction 0 wins: {faction0Wins}/20 (need >=4)\n");
        report.Append(CultureInfo.InvariantCulture,
            $"faction 1 wins: {faction1Wins}/20 (need >=4)\n");

        var verdict = decisiveCount >= 19 && median <= TickCeiling &&
            faction0Wins >= 4 && faction1Wins >= 4;
        report.Append(CultureInfo.InvariantCulture,
            $"termination bar verdict: {(verdict ? "PASS" : "FAIL")}\n");
        report.AppendLine();
    }

    private static void WriteTenCellMatrix(
        StringBuilder report,
        IReadOnlyList<int> shareWeights)
    {
        report.AppendLine(
            "== Ten-cell matrix: seeds {1,2,3,5,8} x {200,500} agents, " +
            "beside the recorded V4 baseline ==");
        report.AppendLine(
            "V4 baseline is PersistentContingentsV4 + PrecolonialPhilippinesV2, " +
            "reproduced from docs/research/ranged/2026-08-07-STANDOFF-ROOT-CAUSE.md:140-149. " +
            "Printed for comparison only; not re-run here.");
        report.AppendLine();
        report.AppendLine(
            "agents  seed  V5/V8 terminalTick  V5/V8 outcome     " +
            "V4 terminalTick  V4 outcome");

        foreach (var agentCount in MatrixAgentCounts)
        {
            foreach (var seed in MatrixSeeds)
            {
                var result = RunOneSeed(seed, agentCount, shareWeights);
                var baseline = V4Baseline.First(
                    cell => cell.Agents == agentCount && cell.Seed == seed);

                report.Append(CultureInfo.InvariantCulture,
                    $"{agentCount,6}  {seed,4}  {result.TerminalTick,18}  " +
                    $"{result.Outcome,-16}  {baseline.TerminalTick,15}  " +
                    $"{baseline.Outcome}\n");
            }
        }

        report.AppendLine();
    }

    private static double Median(IReadOnlyList<long> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var sorted = values.OrderBy(value => value).ToArray();
        var middle = sorted.Length / 2;
        return (sorted.Length & 1) == 1
            ? sorted[middle]
            : (sorted[middle - 1] + sorted[middle]) / 2.0;
    }

    /// <summary>
    /// Everything one seed's battle produced. Built once inside
    /// <see cref="RunOneSeed"/> and handed over; nothing mutates it
    /// afterward.
    /// </summary>
    private sealed record SeedResult(
        ulong Seed,
        long TerminalTick,
        BattleOutcome Outcome,
        CombatMetrics Metrics,
        long ShieldedReceived,
        int ShieldedTotal,
        long ShieldlessReceived,
        int ShieldlessTotal);
}

#if HUKBO_CALIBRATION

/// <summary>
/// The command-line invocation of
/// <see cref="RangedCalibrationHarness.RunCalibration"/>.
/// </summary>
/// <remarks>
/// <b>This type does not exist in any ordinary build.</b> Compiled only when
/// the <c>HUKBO_CALIBRATION</c> preprocessor symbol is defined, matching
/// <c>PressureInterruptCalibrationHarness</c>'s own gated invocation type.
/// <code>
/// dotnet test tests/Hukbo.Core.Tests -c Release ^
///   -p:DefineConstants=HUKBO_CALIBRATION ^
///   --filter FullyQualifiedName~RangedCalibrationRun ^
///   --logger "console;verbosity=detailed"
/// </code>
/// The roster share weights are read from <c>HUKBO_RANGED_ROSTER_WEIGHTS</c>,
/// a comma-separated list of nine non-negative integers in
/// <see cref="Hukbo.Core.Combat.PhilippineCombatPresetV5"/> roster order
/// (Kampilan, Wasay, Kalis, Itak, Bangkaw, Busog, Arquebus, Kalis +
/// TallHardwood, Itak + TallHardwood). It defaults to this file's own
/// <see cref="DefaultRosterWeights"/> constant when unset.
/// </remarks>
public sealed class RangedCalibrationRun
{
    /// <summary>
    /// The roster share weights this harness measures by default, in
    /// <c>PhilippineCombatPresetV5</c> roster order. PROVISIONAL gameplay
    /// tuning, not a historical measurement: a three-quarters melee majority
    /// with the ranged quarter weighted toward the Bangkaw, since a thrown
    /// spear is the best-attested missile weapon in this record and the
    /// Arquebus is deliberately the rarest of the three.
    /// <para>
    /// RU-45: the pre-RU-45 seven-weight table's Kalis weight (19) and Itak
    /// weight (18) are each split roughly in half between that weapon's
    /// shieldless and <see cref="ShieldId.TallHardwood"/> row -- Kalis
    /// 10/9, Itak 9/9 -- because nothing in the record supports either
    /// variant being more common than the other. Every other weapon's weight
    /// is untouched, so the melee/ranged 75/25 split this table has always
    /// held is unchanged and the total still sums to 100.
    /// </para>
    /// </summary>
    internal static readonly int[] DefaultRosterWeights =
        [19, 19, 10, 9, 11, 8, 6, 9, 9];

    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public RangedCalibrationRun(Xunit.Abstractions.ITestOutputHelper output) =>
        _output = output;

    [Fact]
    public void PrintTheCalibrationReport()
    {
        var weights = ParseWeights(
            Environment.GetEnvironmentVariable("HUKBO_RANGED_ROSTER_WEIGHTS"))
            ?? DefaultRosterWeights;

        var report = Hukbo.Core.Tests.RangedCalibrationHarness.RunCalibration(
            weights);

        _output.WriteLine(report);
        Console.WriteLine(report);
    }

    private static int[]? ParseWeights(string? raw) =>
        raw is { Length: > 0 }
            ? raw.Split(',', StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries)
                .Select(part => int.Parse(part, CultureInfo.InvariantCulture))
                .ToArray()
            : null;
}

#endif
