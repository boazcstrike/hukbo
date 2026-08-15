using System.Globalization;
using System.Reflection;
using System.Text;
using Hukbo.Core.Combat;
using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// The hand-run calibration harness for task 8 of
/// <c>docs/plans/2026-08-14-contingent-cohesion-before-contact.md</c>. It
/// measures, per seed across seeds 1 through 20 and for both
/// <see cref="MovementPresetId.CohortLateralSpreadV13"/> and
/// <see cref="MovementPresetId.ContingentCohesionBeforeContactV14"/>, the four
/// numbers task 9 has to choose the three V14 tunables from: the share of
/// living-contingent-ticks resolved to <see cref="ContingentState.Hold"/>, the
/// share of <see cref="ContingentState.Advance"/> members granted a cohesion
/// destination, the tick of first contact, and the terminal tick and outcome.
/// </summary>
/// <remarks>
/// <para>
/// <b>This measures. It does not tune, and it does not assert.</b> Every value
/// in <c>MovementPresetRegistry</c> is owned by task 9; nothing here reads a
/// registered value in order to check it, and nothing here passes or fails.
/// The harness produces a block of text for a person to read and paste into
/// the plan's results section.
/// </para>
/// <para>
/// <b>It is not a test.</b> There is no <c>[Fact]</c> and no <c>[Theory]</c>
/// in the build the canonical gate performs, so the gate's test count is
/// unchanged by this file's presence. Forty full battles of two hundred agents
/// each is minutes of work that no gate run has any reason to do. The single
/// deliberate entry point is <see cref="RunSweep"/>; the conditionally compiled
/// invocation at the bottom of this file exists only so that one method can be
/// reached from a command line, and it is compiled only when the
/// <c>HUKBO_CALIBRATION</c> preprocessor symbol is defined, which no ordinary
/// build, no script in <c>scripts/</c>, and no gate stage defines. This is the
/// same shape <c>PressureInterruptCalibrationHarness</c> and
/// <c>MovementPresetFreezeTests</c>'s capture routine already use.
/// </para>
/// <para>
/// <b>The scenario is the twenty-seed termination sweep's, deliberately.</b>
/// Two hundred agents, <see cref="CombatPresetId.PrecolonialPhilippinesV5"/>,
/// the RU-24/RU-45 roster share weights, and a five-thousand-tick cap are
/// exactly what
/// <c>RangedTerminationTests.SeedsOneThroughTwentyProduceVictoriesForBothFactionsUnderBattlefieldRealism</c>
/// runs. Task 11 adds the V14 form of that test and task 9 chooses the
/// tunables from this harness's table, so the two must read against one
/// yardstick: a band tuned against a differently shaped battle would be tuned
/// against a battle the termination clause never measures.
/// </para>
/// <para>
/// It lives in <c>Hukbo.Core.Tests</c> rather than in <c>Hukbo.Headless</c>
/// for the same reason <c>PressureInterruptCalibrationHarness</c> does: the
/// headless runner exposes no movement-preset selection to a caller in
/// process, and the per-slot state this harness reads is <c>private</c> to
/// <see cref="BattleSimulation"/> inside an assembly only the test assembly
/// may see the internals of.
/// </para>
/// <para>
/// <b>Six private fields of <see cref="BattleSimulation"/> are read by
/// reflection</b>, once per run each, because all six are <c>readonly</c>
/// array references that cannot move under the harness. They are the
/// authoritative per-slot state the six movement gates themselves consult, so
/// reading them is a measurement of what the simulation decided rather than a
/// reconstruction of what it might have decided. If any one of them is ever
/// renamed, this harness throws on its first seed with a message naming the
/// field, rather than silently reporting zero. That is acceptable in a
/// hand-run measurement harness and would not be acceptable in shipped code.
/// </para>
/// <para>
/// <b>What the granted-cohesion share does and does not count.</b> The
/// simulation evaluates the six gates inside
/// <c>BattleSimulation.TryResolveContingentCohesionAimPoint</c>, which is
/// reached only after the caller's own earlier branches decline — the ranged
/// standoff branch, the attacking hold, the regrouping branch, and the
/// disengage phase all return before it. This harness deliberately evaluates
/// the six gates for <i>every</i> living non-leader member whose contingent
/// resolved to <see cref="ContingentState.Advance"/>, whatever that member was
/// otherwise doing, because the quantity task 9 is choosing between is the
/// gate's answer and not the number of proposals a particular tick's branch
/// order happened to let through. A member that clears all six gates is
/// counted as granted; the aim point it would have been given is not computed,
/// because it does not vary with any of the three tunables.
/// </para>
/// <para>
/// <b>The straggler test below is a reconstruction and is the one place this
/// harness can drift.</b> Gate 4's comparison lives at
/// <c>BattleSimulation.TryResolveContingentCohesionAimPoint</c> and is
/// reproduced in <see cref="IsStraggling"/> in the same <c>Int128</c> widening
/// and the same ruleset-gated form, including the unchanged sixteen-over-nine
/// comparison every preset up to V13 executes. Everything else the gates read
/// is taken from the simulation's own arrays rather than recomputed. If gate
/// 4's arithmetic changes, <see cref="IsStraggling"/> has to change with it.
/// </para>
/// <para>
/// <b>Sampling.</b> Every per-slot array this harness reads is written by
/// <c>ResolveContingentStates</c>, which runs earlier in the tick than the
/// movement-proposal stage that consults them, and none of them is written
/// again before the tick ends — so reading them after
/// <see cref="BattleSimulation.AdvanceOneTick"/> returns yields exactly the
/// values the gates saw on that tick. Member and leader positions are the
/// exception: the gate reads them before the tick's movement is applied, so
/// the harness compares the positions it snapshotted <i>before</i> advancing,
/// which are the same values.
/// </para>
/// </remarks>
internal static class ContingentCohesionCalibrationHarness
{
    /// <summary>
    /// The first seed of the sweep, matching the twenty-seed termination test.
    /// </summary>
    private const ulong FirstSeed = 1;

    /// <summary>
    /// The last seed of the sweep, matching the twenty-seed termination test.
    /// </summary>
    private const ulong LastSeed = 20;

    /// <summary>
    /// The agent count of the twenty-seed termination sweep.
    /// </summary>
    private const int TotalAgents = 200;

    /// <summary>
    /// The tick cap of the twenty-seed termination sweep. It is that test's
    /// <c>MedianDecisiveTickLimit</c> used as the scenario's
    /// <c>TickLimit</c>, which is what makes a seed that reaches the cap
    /// visible as an undecided row rather than as a long one.
    /// </summary>
    private const int TickLimit = 5_000;

    /// <summary>
    /// RU-24/RU-45's roster share weights, in
    /// <see cref="PhilippineCombatPresetV5.Rules"/> roster order (Kampilan,
    /// Wasay, Kalis, Itak, Bangkaw, Busog, Arquebus, Kalis + TallHardwood,
    /// Itak + TallHardwood). Copied from <c>RangedTerminationTests</c>'s own
    /// private array of the same name so this harness measures the battle the
    /// termination clause measures. Provisional gameplay tuning, not a
    /// historical measurement.
    /// </summary>
    private static readonly int[] RangedRosterShareWeights =
        [19, 19, 10, 9, 11, 8, 6, 9, 9];

    /// <summary>
    /// The two presets under measurement, in the fixed order every table in
    /// the report prints them: the shipped client default first, the candidate
    /// second, so a reader compares down a pair of adjacent rows.
    /// </summary>
    private static readonly MovementPresetId[] MeasuredPresets =
    [
        MovementPresetId.CohortLateralSpreadV13,
        MovementPresetId.ContingentCohesionBeforeContactV14,
    ];

    /// <summary>
    /// The short labels the per-seed table prints, index-aligned with
    /// <see cref="MeasuredPresets"/>. The full names are in the header, so the
    /// table itself stays inside a readable width.
    /// </summary>
    private static readonly string[] MeasuredPresetLabels = ["V13", "V14"];

    /// <summary>
    /// The contingent state each slot resolved to on the tick just finished.
    /// This is the authoritative per-slot value
    /// <c>ResolveContingentStates</c> writes and then copies onto every living
    /// member, so it is read here rather than inferred from any one member's
    /// view.
    /// </summary>
    private const string ResolvedStatesFieldName = "_contingentResolvedStates";

    /// <summary>
    /// The living member count per slot, as scanned at the start of the tick.
    /// It is the denominator of the <see cref="ContingentState.Hold"/> share:
    /// a slot with no living members has no state worth counting.
    /// </summary>
    private const string LivingCountsFieldName = "_contingentLivingCounts";

    /// <summary>
    /// The selected leader per slot. Gate 2 exempts the leader, and the whole
    /// gate denies when the slot has no leader at all, so the harness needs
    /// the same identity the simulation selected rather than its own guess at
    /// one.
    /// </summary>
    private const string LeaderEntityIdsFieldName = "_contingentLeaderEntityIds";

    /// <summary>
    /// The count of members within the close radius of their own selected
    /// target, per slot, recomputed from scratch every tick. It is the
    /// simulation's own notion of contact — the quantity
    /// <c>MovementRules.ResolveContingentState</c> reads to move a contingent
    /// into <see cref="ContingentState.Close"/> — so the first tick on which
    /// any slot's count is non-zero is the tick of first contact this report
    /// prints.
    /// </summary>
    private const string ContactCountsFieldName = "_contingentContactCounts";

    /// <summary>Gate 5's answer per slot, for the tick just finished.</summary>
    private const string SquareFitsMapFieldName = "_contingentSquareFitsMap";

    /// <summary>Gate 6's answer per slot, for the tick just finished.</summary>
    private const string SquareOverlapsFieldName =
        "_contingentSquareOverlapsAnother";

    /// <summary>
    /// The single deliberate entry point. Runs both presets over the requested
    /// seeds and returns the whole report as one block of text.
    /// </summary>
    /// <param name="seeds">
    /// The seeds to measure, or <see langword="null"/> for the sweep's own
    /// seeds 1 through 20. Narrowing the sweep is what makes iterating on the
    /// three tunables in task 9 affordable; the table task 9 records is the
    /// full twenty.
    /// </param>
    /// <returns>
    /// The report, with line endings normalised so the text a reader pastes
    /// into the plan is the text the harness printed.
    /// </returns>
    internal static string RunSweep(IReadOnlyList<ulong>? seeds = null)
    {
        var seedList = seeds ?? DefaultSeeds();
        var agentsPerFaction = TotalAgents / 2;
        var rosterCounts = RangedCalibrationHarness.BuildRosterCounts(
            agentsPerFaction,
            RangedRosterShareWeights);

        var report = new StringBuilder();
        WriteHeader(report, seedList, rosterCounts);

        var results = new List<SeedResult>(seedList.Count * MeasuredPresets.Length);

        // Seed ascending on the outside, preset in its fixed order on the
        // inside. Both loops are over ordered lists and nothing here reads a
        // clock, so two runs of this harness against one build print
        // byte-identical text.
        foreach (var seed in seedList)
        {
            foreach (var preset in MeasuredPresets)
            {
                results.Add(RunOneSeed(seed, preset, rosterCounts));
            }
        }

        WriteSeedTable(report, results);
        WriteTotals(report, results);

        report.AppendLine(
            "End of report. Nothing above asserted, passed, or failed.");

        return report.ToString().ReplaceLineEndings();
    }

    /// <summary>
    /// Runs one battle to its own termination or to the tick cap, whichever
    /// comes first, and returns the four measures for it.
    /// </summary>
    private static SeedResult RunOneSeed(
        ulong seed,
        MovementPresetId preset,
        IReadOnlyList<int> rosterCounts)
    {
        var scenario = Scenario.CreateDefault(seed, TotalAgents) with
        {
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV5,
            MovementPreset = preset,
            RosterCounts = [.. rosterCounts],
            TickLimit = TickLimit,
        };
        scenario.Validate();

        var rules = MovementPresetRegistry.Get(preset);
        var simulation = BattleSimulation.Create(scenario);

        var resolvedStates = ReadPrivateArray<ContingentState[]>(
            simulation, ResolvedStatesFieldName);
        var livingCounts = ReadPrivateArray<int[]>(
            simulation, LivingCountsFieldName);
        var leaderEntityIds = ReadPrivateArray<ulong[]>(
            simulation, LeaderEntityIdsFieldName);
        var contactCounts = ReadPrivateArray<int[]>(
            simulation, ContactCountsFieldName);
        var squareFitsMap = ReadPrivateArray<bool[]>(
            simulation, SquareFitsMapFieldName);
        var squareOverlapsAnother = ReadPrivateArray<bool[]>(
            simulation, SquareOverlapsFieldName);

        var slotCount = resolvedStates.Length;
        var views = simulation.Agents;
        var agentSlots = views.Count;

        // Agent views are index-stable for the whole battle --
        // BattleSimulation.UpdateViews writes _agentViews[index] from
        // _agentStates[index] -- so the entity-id-to-index map is built once
        // and the per-tick position snapshot is two flat arrays rather than a
        // dictionary rebuilt every tick.
        var indexOfEntityId = new Dictionary<ulong, int>(agentSlots);
        for (var index = 0; index < agentSlots; index++)
        {
            indexOfEntityId[views[index].EntityId] = index;
        }

        var startOfTickXRaw = new int[agentSlots];
        var startOfTickYRaw = new int[agentSlots];
        SnapshotPositions(views, startOfTickXRaw, startOfTickYRaw);

        long holdSlotTicks = 0;
        long livingSlotTicks = 0;
        long grantedAdvanceMemberTicks = 0;
        long advanceMemberTicks = 0;
        long firstContactTick = 0;

        // Bounded by the scenario's own tick limit. An unbounded loop turns a
        // stall into a harness that hangs with no diagnosis rather than a row
        // that names the seed.
        while (simulation.Outcome == BattleOutcome.Ongoing &&
            simulation.Tick < scenario.TickLimit)
        {
            simulation.AdvanceOneTick();

            var tick = checked((int)simulation.Tick);
            views = simulation.Agents;

            for (var slot = 0; slot < slotCount; slot++)
            {
                if (livingCounts[slot] == 0)
                {
                    continue;
                }

                livingSlotTicks++;
                if (resolvedStates[slot] == ContingentState.Hold)
                {
                    holdSlotTicks++;
                }

                if (firstContactTick == 0 && contactCounts[slot] > 0)
                {
                    firstContactTick = tick;
                }
            }

            for (var index = 0; index < agentSlots; index++)
            {
                var view = views[index];
                if (!view.IsAlive ||
                    view.ContingentState != ContingentState.Advance)
                {
                    continue;
                }

                var slot = (view.FactionId * FormationPlanner.MaximumContingents) +
                    view.ContingentId;
                var leaderEntityId = leaderEntityIds[slot];
                if (leaderEntityId == view.EntityId)
                {
                    // Gate 2 exempts the leader unconditionally, so a leader
                    // is not a member the grant could ever be denied to and
                    // does not belong in the denominator.
                    continue;
                }

                advanceMemberTicks++;

                if (leaderEntityId == 0 ||
                    !indexOfEntityId.TryGetValue(leaderEntityId, out var leaderIndex))
                {
                    continue;
                }

                var straggling = IsStraggling(
                    rules,
                    scenario.BodyRadiusRaw,
                    startOfTickXRaw[index],
                    startOfTickYRaw[index],
                    startOfTickXRaw[leaderIndex],
                    startOfTickYRaw[leaderIndex]);

                if (MovementRules.IsCohesionEligible(
                    ContingentState.Advance,
                    isLeader: false,
                    MovementRules.IsCohesionWindowOpen(
                        tick,
                        slot,
                        rules.CohesionCycleTicks,
                        rules.CohesionDutyTicks),
                    straggling,
                    squareFitsMap[slot],
                    squareOverlapsAnother[slot]))
                {
                    grantedAdvanceMemberTicks++;
                }
            }

            SnapshotPositions(views, startOfTickXRaw, startOfTickYRaw);
        }

        return new SeedResult(
            seed,
            preset,
            holdSlotTicks,
            livingSlotTicks,
            grantedAdvanceMemberTicks,
            advanceMemberTicks,
            firstContactTick,
            simulation.Tick,
            simulation.Outcome);
    }

    /// <summary>
    /// Gate 4's straggler test, reproduced from
    /// <c>BattleSimulation.TryResolveContingentCohesionAimPoint</c> in the
    /// same <c>Int128</c> widening and the same ruleset-gated form. Under a
    /// preset whose <see cref="MovementRuleset.GathersContingentsBeforeContact"/>
    /// is <see langword="false"/> this is the unchanged sixteen-over-nine
    /// comparison every preset up to V13 executes; under one whose gate is
    /// <see langword="true"/> it is the registered band, cross-multiplied so
    /// no division and no rounding enters the comparison.
    /// </summary>
    private static bool IsStraggling(
        MovementRuleset rules,
        int bodyRadiusRaw,
        int memberXRaw,
        int memberYRaw,
        int leaderXRaw,
        int leaderYRaw)
    {
        var deltaX = (long)memberXRaw - leaderXRaw;
        var deltaY = (long)memberYRaw - leaderYRaw;
        var memberSquared = checked((deltaX * deltaX) + (deltaY * deltaY));
        var cohesionRadiusRaw = checked(
            (long)rules.CohesionRadiusMultiplier * bodyRadiusRaw);

        if (!rules.GathersContingentsBeforeContact)
        {
            return (Int128)16 * memberSquared >
                (Int128)9 * cohesionRadiusRaw * cohesionRadiusRaw;
        }

        var bandNumerator = (Int128)rules.CohesionBandNumerator;
        var bandDenominator = (Int128)rules.CohesionBandDenominator;
        return bandDenominator * bandDenominator * memberSquared >
            bandNumerator * bandNumerator *
            cohesionRadiusRaw * cohesionRadiusRaw;
    }

    /// <summary>
    /// Copies every agent's current position into the two flat arrays the next
    /// tick's gate-4 reconstruction reads, so the harness compares the
    /// positions the gate compares rather than the post-movement ones.
    /// </summary>
    private static void SnapshotPositions(
        IReadOnlyList<AgentView> views,
        int[] xRaw,
        int[] yRaw)
    {
        for (var index = 0; index < xRaw.Length; index++)
        {
            var view = views[index];
            xRaw[index] = view.XRaw;
            yRaw[index] = view.YRaw;
        }
    }

    /// <summary>
    /// Fetches one private array field of <see cref="BattleSimulation"/> once
    /// per run. Every field named by this harness is <c>readonly</c>, so the
    /// reference cannot move underneath it and one lookup per run is enough.
    /// </summary>
    private static T ReadPrivateArray<T>(
        BattleSimulation simulation,
        string fieldName)
        where T : class
    {
        var field = typeof(BattleSimulation).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field is null)
        {
            throw new InvalidOperationException(
                $"BattleSimulation no longer declares a private field named " +
                $"'{fieldName}'. This calibration harness reads it by " +
                $"reflection to measure the six cohesion gates; rename the " +
                $"constant beside this message to match, or the report's " +
                $"shares would silently read zero.");
        }

        return field.GetValue(simulation) as T ??
            throw new InvalidOperationException(
                $"BattleSimulation's '{fieldName}' is no longer a " +
                $"{typeof(T).Name}. See the note above.");
    }

    /// <summary>
    /// The sweep's own seeds, 1 through 20, as an ordered list.
    /// </summary>
    private static ulong[] DefaultSeeds()
    {
        var seeds = new ulong[LastSeed - FirstSeed + 1];
        for (var offset = 0; offset < seeds.Length; offset++)
        {
            seeds[offset] = FirstSeed + (ulong)offset;
        }

        return seeds;
    }

    private static void WriteHeader(
        StringBuilder report,
        IReadOnlyList<ulong> seeds,
        IReadOnlyList<int> rosterCounts)
    {
        report.AppendLine(
            "Hukbo contingent cohesion calibration harness");
        report.AppendLine(
            "Measurement only. Nothing asserts, passes, or fails.");
        report.AppendLine(
            "Task 8 of docs/plans/2026-08-14-contingent-cohesion-before-contact.md");
        report.AppendLine();

        report.Append(CultureInfo.InvariantCulture,
            $"combatPreset   : {CombatPresetId.PrecolonialPhilippinesV5} (pinned)\n");
        report.Append(CultureInfo.InvariantCulture,
            $"totalAgents    : {TotalAgents.ToString(CultureInfo.InvariantCulture)}\n");
        report.Append(CultureInfo.InvariantCulture,
            $"tickLimit      : {TickLimit.ToString(CultureInfo.InvariantCulture)}\n");
        report.Append(CultureInfo.InvariantCulture,
            $"seeds          : {string.Join(", ", seeds)}\n");
        report.Append(CultureInfo.InvariantCulture,
            $"rosterWeights  : {string.Join(", ", RangedRosterShareWeights)}\n");
        report.Append(CultureInfo.InvariantCulture,
            $"rosterCounts   : {string.Join(", ", rosterCounts)}\n");
        report.AppendLine();
        report.AppendLine(
            "The scenario shape above is the twenty-seed termination sweep's, " +
            "so task 9's choice and");
        report.AppendLine(
            "task 11's gate are read against one yardstick.");
        report.AppendLine();

        report.AppendLine("== Registered cohesion settings per measured preset ==");
        report.AppendLine(
            "label preset                             gathers band   marginBp " +
            "radiusMul cycle duty");
        for (var index = 0; index < MeasuredPresets.Length; index++)
        {
            var preset = MeasuredPresets[index];
            var rules = MovementPresetRegistry.Get(preset);
            var band = rules.GathersContingentsBeforeContact
                ? string.Create(
                    CultureInfo.InvariantCulture,
                    $"{rules.CohesionBandNumerator}/{rules.CohesionBandDenominator}")
                : "3/4";
            var margin = rules.GathersContingentsBeforeContact
                ? rules.CohesionSquareMarginBasisPoints.ToString(
                    CultureInfo.InvariantCulture)
                : MovementRuleset.UnscaledCohesionSquareMarginBasisPoints
                    .ToString(CultureInfo.InvariantCulture);

            report.Append(CultureInfo.InvariantCulture,
                $"{MeasuredPresetLabels[index],-5} {preset,-34} " +
                $"{rules.GathersContingentsBeforeContact,-7} {band,-6} " +
                $"{margin,-8} " +
                $"{rules.CohesionRadiusMultiplier.ToString(CultureInfo.InvariantCulture),-9} " +
                $"{rules.CohesionCycleTicks.ToString(CultureInfo.InvariantCulture),-5} " +
                $"{rules.CohesionDutyTicks.ToString(CultureInfo.InvariantCulture)}\n");
        }

        report.AppendLine();
        report.AppendLine(
            "band is the straggler threshold as a fraction of the cohesion " +
            "radius. A preset that does");
        report.AppendLine(
            "not gather before contact executes the hardcoded three-quarters " +
            "comparison, shown as 3/4,");
        report.AppendLine(
            "and claims the unscaled packing margin, shown as 10000 basis " +
            "points.");
        report.AppendLine();
    }

    private static void WriteSeedTable(
        StringBuilder report,
        IReadOnlyList<SeedResult> results)
    {
        report.AppendLine("== Per seed, per preset ==");
        report.AppendLine(
            "seed pre  holdTicks livingTicks   hold%  granted advMemTicks " +
            "grant%  firstContact  terminal outcome");

        foreach (var result in results)
        {
            report.Append(CultureInfo.InvariantCulture,
                $"{result.Seed,4} {LabelOf(result.Preset),-4} " +
                $"{result.HoldSlotTicks,9} {result.LivingSlotTicks,11} " +
                $"{FormatShare(result.HoldSlotTicks, result.LivingSlotTicks),7} " +
                $"{result.GrantedAdvanceMemberTicks,8} " +
                $"{result.AdvanceMemberTicks,11} " +
                $"{FormatShare(result.GrantedAdvanceMemberTicks, result.AdvanceMemberTicks),7} " +
                $"{FormatFirstContact(result.FirstContactTick),13} " +
                $"{result.TerminalTick,9} {result.Outcome}\n");
        }

        report.AppendLine();
        report.AppendLine(
            "holdTicks / livingTicks is the share of living-contingent-ticks " +
            "resolved to Hold. One");
        report.AppendLine(
            "observation is one (slot, tick) pair on which the slot had at " +
            "least one living member.");
        report.AppendLine(
            "granted / advMemTicks is the share of Advance members granted a " +
            "cohesion destination. One");
        report.AppendLine(
            "observation is one (living non-leader member, tick) pair whose " +
            "contingent resolved to");
        report.AppendLine(
            "Advance; leaders are excluded because gate 2 exempts them " +
            "unconditionally.");
        report.AppendLine(
            "firstContact is the first tick on which any contingent had a " +
            "member inside the close");
        report.AppendLine(
            "radius of its own selected target, which is the quantity that " +
            "moves a contingent to Close.");
        report.AppendLine(
            "'never' means no contingent reached contact before the run " +
            "ended.");
        report.AppendLine(
            "terminal is the tick the run stopped on, and outcome is what it " +
            "stopped as. An Ongoing row");
        report.AppendLine(
            "reached the tick cap without deciding.");
        report.AppendLine();
    }

    private static void WriteTotals(
        StringBuilder report,
        IReadOnlyList<SeedResult> results)
    {
        report.AppendLine("== Totals across every measured seed, per preset ==");
        report.AppendLine(
            "pre  holdTicks livingTicks   hold%  granted advMemTicks grant%  " +
            "medianFirstContact medianTerminal decided");

        foreach (var preset in MeasuredPresets)
        {
            var rows = results.Where(result => result.Preset == preset).ToArray();
            if (rows.Length == 0)
            {
                continue;
            }

            var holdTicks = rows.Sum(row => row.HoldSlotTicks);
            var livingTicks = rows.Sum(row => row.LivingSlotTicks);
            var granted = rows.Sum(row => row.GrantedAdvanceMemberTicks);
            var advanceTicks = rows.Sum(row => row.AdvanceMemberTicks);
            var decided = rows.Count(row => row.Outcome != BattleOutcome.Ongoing);

            var contactTicks = rows
                .Where(row => row.FirstContactTick > 0)
                .Select(row => row.FirstContactTick)
                .ToArray();

            report.Append(CultureInfo.InvariantCulture,
                $"{LabelOf(preset),-4} {holdTicks,9} {livingTicks,11} " +
                $"{FormatShare(holdTicks, livingTicks),7} {granted,8} " +
                $"{advanceTicks,11} " +
                $"{FormatShare(granted, advanceTicks),7} " +
                $"{FormatFirstContact(Median(contactTicks)),18} " +
                $"{Median(rows.Select(row => row.TerminalTick).ToArray()),14} " +
                $"{decided.ToString(CultureInfo.InvariantCulture),7}\n");
        }

        report.AppendLine();
        report.AppendLine(
            "decided counts the seeds whose outcome left Ongoing before the " +
            "tick cap. It is not the");
        report.AppendLine(
            "termination clause -- task 11's test is -- but a preset that " +
            "gathers and never resolves");
        report.AppendLine(
            "shows up here first.");
        report.AppendLine();
    }

    /// <summary>
    /// The lower median of an ordered copy of <paramref name="values"/>, or
    /// zero when there is nothing to take a median of. The lower median is
    /// used rather than the midpoint average so the printed figure is always
    /// a tick that some seed actually reached.
    /// </summary>
    private static long Median(long[] values)
    {
        if (values.Length == 0)
        {
            return 0;
        }

        var sorted = values.ToArray();
        Array.Sort(sorted);
        return sorted[sorted.Length / 2];
    }

    private static string LabelOf(MovementPresetId preset)
    {
        for (var index = 0; index < MeasuredPresets.Length; index++)
        {
            if (MeasuredPresets[index] == preset)
            {
                return MeasuredPresetLabels[index];
            }
        }

        return preset.ToString();
    }

    private static string FormatFirstContact(long tick) =>
        tick == 0
            ? "never"
            : tick.ToString(CultureInfo.InvariantCulture);

    private static string FormatShare(long numerator, long denominator) =>
        denominator == 0
            ? "n/a"
            : (100.0 * numerator / denominator)
                .ToString("F2", CultureInfo.InvariantCulture);

    /// <summary>
    /// Everything <see cref="RunOneSeed"/> measured about one battle. The
    /// counts are carried rather than the shares so
    /// <see cref="WriteTotals"/> can sum numerators and denominators instead
    /// of averaging percentages, which would weight a short seed the same as
    /// a long one.
    /// </summary>
    private sealed record SeedResult(
        ulong Seed,
        MovementPresetId Preset,
        long HoldSlotTicks,
        long LivingSlotTicks,
        long GrantedAdvanceMemberTicks,
        long AdvanceMemberTicks,
        long FirstContactTick,
        long TerminalTick,
        BattleOutcome Outcome);
}

#if HUKBO_CALIBRATION

/// <summary>
/// The command-line invocation of
/// <see cref="ContingentCohesionCalibrationHarness.RunSweep"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>This type does not exist in any ordinary build.</b> It is compiled only
/// when the <c>HUKBO_CALIBRATION</c> preprocessor symbol is defined, which
/// nothing in <c>Directory.Build.props</c>, in the project file, in
/// <c>scripts/</c>, or in the canonical gate defines. A gate run therefore
/// discovers no test here at all, and the suite's test count is unmoved by
/// this file.
/// </para>
/// <para>
/// Run it deliberately, and only deliberately:
/// </para>
/// <code>
/// dotnet test tests/Hukbo.Core.Tests/Hukbo.Core.Tests.csproj -c Release ^
///   -p:DefineConstants=HUKBO_CALIBRATION ^
///   --filter FullyQualifiedName~ContingentCohesionCalibrationRun ^
///   --logger "console;verbosity=detailed"
/// </code>
/// <para>
/// Set <c>HUKBO_CALIBRATION_SEEDS</c> to a comma-separated list to narrow the
/// sweep while iterating on the three tunables. The table task 9 records is
/// the full seeds 1 through 20, which is what an unset variable runs.
/// </para>
/// <para>
/// It asserts nothing. It prints a report and passes, because it is a
/// measurement and a measurement has no verdict.
/// </para>
/// </remarks>
public sealed class ContingentCohesionCalibrationRun
{
    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public ContingentCohesionCalibrationRun(
        Xunit.Abstractions.ITestOutputHelper output) => _output = output;

    [Fact]
    public void PrintTheCohesionSweep()
    {
        var seeds = ParseSeeds(
            Environment.GetEnvironmentVariable("HUKBO_CALIBRATION_SEEDS"));

        var report = ContingentCohesionCalibrationHarness.RunSweep(seeds);

        _output.WriteLine(report);
        Console.WriteLine(report);
    }

    private static ulong[]? ParseSeeds(string? value) =>
        value is { Length: > 0 }
            ? value.Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries)
                .Select(entry => ulong.Parse(
                    entry,
                    System.Globalization.CultureInfo.InvariantCulture))
                .ToArray()
            : null;
}

#endif
