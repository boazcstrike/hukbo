using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Hukbo.Core.Combat;
using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// The hand-run calibration harness of task E0 in
/// <c>docs/archives/2026-08-06/movement/2026-07-31-movement-v7-pressure-interrupt.md</c>. It runs the
/// measurement matrix of the V7 design document section 2.2 under a
/// caller-supplied movement preset and reports, per cell, the terminal tick,
/// the outcome, both survivor counts, the measured <c>p50</c> tick duration,
/// the redefined phase-flip percentage of design section 2.3 over ticks 101
/// through 400, and the per-row pressure-interrupt firing counts.
/// </summary>
/// <remarks>
/// <para>
/// <b>This measures. It does not tune, and it does not assert.</b> Task E1
/// owns every weight and threshold in
/// <c>MovementPresetRegistry</c>; nothing here reads a shipped value in order
/// to check it, and nothing here passes or fails. The harness only produces a
/// block of text for a person to read and paste into the E1 record.
/// </para>
/// <para>
/// <b>It is not a test.</b> There is no <c>[Fact]</c> and no <c>[Theory]</c>
/// in the build the canonical gate performs, so the gate's test count is
/// unchanged by this file's presence. Ten cells, two runs each, up to ten
/// thousand ticks at two hundred and five hundred agents is minutes of work
/// that no gate run has any reason to do. The single deliberate entry point is
/// <see cref="RunMatrix"/>; the conditionally compiled invocation at the
/// bottom of this file exists only so that one method can be reached from a
/// command line, and it is compiled only when the
/// <c>HUKBO_CALIBRATION</c> preprocessor symbol is defined, which no ordinary
/// build, no script in <c>scripts/</c>, and no gate stage defines.
/// </para>
/// <para>
/// It lives in <c>Hukbo.Core.Tests</c> rather than in <c>Hukbo.Headless</c>
/// for the reason the plan's E0 row records: the headless runner exposes no
/// movement-preset selection to a caller in process, and the state this
/// harness reads is <c>internal</c> to <c>Hukbo.Core</c>, which only the test
/// assembly may see.
/// </para>
/// <para>
/// The harness reaches one private field of
/// <see cref="BattleSimulation"/> by reflection —
/// <c>_pressureInterruptFired</c> — for the reason set out on
/// <see cref="PressureInterruptFiredFieldName"/>. That is acceptable in a
/// hand-run measurement harness and would not be acceptable in shipped code.
/// If that field is ever renamed, this harness throws on its first cell with a
/// message naming the field, rather than silently reporting zero firings.
/// </para>
/// </remarks>
internal static class PressureInterruptCalibrationHarness
{
    /// <summary>
    /// The seeds of the design section 2.2 matrix.
    /// </summary>
    private static readonly ulong[] MatrixSeeds = [1, 2, 3, 5, 8];

    /// <summary>
    /// The agent counts of the design section 2.2 matrix.
    /// </summary>
    private static readonly int[] MatrixAgentCounts = [200, 500];

    /// <summary>
    /// The requested tick count of the design section 2.2 matrix. It is also
    /// <see cref="Scenario"/>'s own default <c>TickLimit</c>, so a run that
    /// reaches it ends in <see cref="BattleOutcome.Draw"/> — which is exactly
    /// the failure the design section 2.1 termination bar is written against.
    /// </summary>
    private const int MatrixRequestedTicks = 10_000;

    /// <summary>
    /// The first tick that contributes a phase-flip observation. The
    /// criterion discards the first hundred ticks as settling, so the first
    /// comparison made is tick 100 against tick 101.
    /// </summary>
    private const int FlipWindowFirstTick = 101;

    /// <summary>
    /// The last tick that contributes a phase-flip observation, matching the
    /// four-hundred-tick observation window the weapon sessions measured the
    /// original criterion over.
    /// </summary>
    private const int FlipWindowLastTick = 400;

    /// <summary>
    /// The number of canonical loadout rows — <c>KP, WA, KA, IT, KS, IS</c>.
    /// </summary>
    private const int CanonicalRowCount = 6;

    /// <summary>
    /// The bucket every loadout that is not one of the six canonical rows
    /// falls into. It exists so an unexpected roster is visible in the report
    /// rather than throwing in the middle of a ten-cell run.
    /// </summary>
    private const int OtherRowIndex = CanonicalRowCount;

    /// <summary>
    /// The number of buckets the per-row tables carry: the six canonical rows
    /// plus <see cref="OtherRowIndex"/>.
    /// </summary>
    private const int RowBucketCount = CanonicalRowCount + 1;

    /// <summary>
    /// The one private member of <see cref="BattleSimulation"/> this harness
    /// reaches by reflection: the per-tick scratch array holding, for every
    /// agent, whether the pressure interrupt fired for that agent on the tick
    /// that just finished.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is read rather than <c>AgentState.BrokeOffUnderPressure</c> because
    /// that flag is deliberately <b>not</b> a single-tick pulse: task B6 made
    /// it persist for as long as the warrior stays in the
    /// <see cref="FootworkPhase.Disengage"/> the interrupt produced, so
    /// counting ticks on which it is set counts <i>ticks spent broken off</i>
    /// and not <i>break-offs</i>, and overcounts by however many ticks a
    /// disengagement lasts. Counting <c>false</c>-to-<c>true</c> transitions
    /// of that flag would be closer, but still wrong in one direction: the
    /// flag is cleared again in the same tick it was set whenever lane
    /// clearance falls the finalised phase back off <c>Disengage</c>, so a
    /// firing that was immediately overruled would never be seen at all.
    /// </para>
    /// <para>
    /// The scratch array has neither problem. It is assigned for every agent
    /// on every tick — <c>false</c> for a dead agent, and the predicate's own
    /// answer for a living one — and the interrupt's cost is charged if and
    /// only if that slot is <c>true</c>. Summing it once per tick is therefore
    /// a count of firings, one per <c>(agent, tick)</c> pair on which
    /// <c>WeaponMovementRules.ShouldPressureInterrupt</c> returned
    /// <see langword="true"/>.
    /// </para>
    /// <para>
    /// The array is allocated zero-length under every preset whose
    /// <see cref="MovementRuleset.AppliesPressureInterrupt"/> is
    /// <see langword="false"/>, which is how this harness tells "the interrupt
    /// is not part of this preset" from "the interrupt is part of this preset
    /// and never fired".
    /// </para>
    /// </remarks>
    private const string PressureInterruptFiredFieldName =
        "_pressureInterruptFired";

    /// <summary>
    /// The canonical row labels, in the canonical
    /// <c>KP, WA, KA, IT, KS, IS</c> order, plus the label for
    /// <see cref="OtherRowIndex"/>.
    /// </summary>
    private static readonly string[] RowLabels =
        ["KP", "WA", "KA", "IT", "KS", "IS", "??"];

    /// <summary>
    /// The single deliberate entry point. Runs the design section 2.2 matrix
    /// and returns the whole report as one block of text.
    /// </summary>
    /// <param name="movementPreset">
    /// The movement preset under measurement. Named by the caller and never
    /// defaulted, because measuring whatever <see cref="Scenario"/>'s shipped
    /// default happens to be is the one thing this harness must not do.
    /// </param>
    /// <param name="seeds">
    /// The seeds to measure, or <see langword="null"/> for the matrix's own
    /// 1, 2, 3, 5, 8.
    /// </param>
    /// <param name="agentCounts">
    /// The agent counts to measure, or <see langword="null"/> for the matrix's
    /// own 200 and 500.
    /// </param>
    /// <param name="requestedTicks">
    /// The tick ceiling per run. A run also stops early on a decisive
    /// outcome.
    /// </param>
    /// <param name="bodyRadiusRaw">
    /// The scenario body radius. It defaults to the shipped
    /// <see cref="CollisionRules.DefaultBodyRadiusRaw"/> because that is what
    /// <c>scripts/benchmark.ps1</c> used to produce the "before" numbers in
    /// <c>docs/archives/2026-08-06/movement/2026-07-31-movement-v7-baseline.md</c>, and a p50
    /// compared against those medians has to have been measured under the same
    /// radius. The V6 trajectory fixture pins <c>4 * FixedPoint.Scale</c>
    /// instead, for reasons that belong to a frozen fixture and not to a
    /// performance comparison; pass that value explicitly to reproduce the
    /// fixture's battle. The value used is printed in the report header either
    /// way.
    /// </param>
    internal static string RunMatrix(
        MovementPresetId movementPreset,
        IReadOnlyList<ulong>? seeds = null,
        IReadOnlyList<int>? agentCounts = null,
        int requestedTicks = MatrixRequestedTicks,
        int bodyRadiusRaw = CollisionRules.DefaultBodyRadiusRaw)
    {
        var seedList = seeds ?? MatrixSeeds;
        var agentCountList = agentCounts ?? MatrixAgentCounts;
        var ruleset = MovementPresetRegistry.Get(movementPreset);

        var report = new StringBuilder();
        WriteHeader(
            report,
            movementPreset,
            ruleset,
            seedList,
            agentCountList,
            requestedTicks,
            bodyRadiusRaw);

        var cells = new List<CellResult>();
        foreach (var agentCount in agentCountList)
        {
            foreach (var seed in seedList)
            {
                // Design section 2.2's protocol: one warm run per cell,
                // discarded, then the measured run. Both are recorded below so
                // that the discard is visible rather than merely claimed.
                var warm = RunOneCell(
                    movementPreset,
                    seed,
                    agentCount,
                    requestedTicks,
                    bodyRadiusRaw);
                var measured = RunOneCell(
                    movementPreset,
                    seed,
                    agentCount,
                    requestedTicks,
                    bodyRadiusRaw);
                cells.Add(measured with
                {
                    WarmP50Milliseconds = warm.P50Milliseconds,
                });
            }
        }

        WriteCellTable(report, cells);
        WriteMedians(report, cells, agentCountList);
        WriteRowTable(report, cells);

        // The writers above mix AppendLine with lines that end in an explicit
        // newline inside an interpolated row, so the buffer holds both endings.
        // Normalising once here means the report a person pastes into the E1
        // record has one line ending throughout.
        return report.ToString().ReplaceLineEndings();
    }

    /// <summary>
    /// Runs one cell of the matrix to a decisive outcome or to the tick
    /// ceiling, whichever comes first, and returns everything measured.
    /// </summary>
    private static CellResult RunOneCell(
        MovementPresetId movementPreset,
        ulong seed,
        int agentCount,
        int requestedTicks,
        int bodyRadiusRaw)
    {
        // The combat preset is pinned rather than left to the shipped default
        // for the reason the design records in section 2.2:
        // PrecolonialPhilippinesV4's roster never pairs a shield with any
        // weapon, so a workload run under it would never field the KS or IS
        // rows -- and those two rows have the zero-window attack lifecycle
        // that motivated the pressure interrupt in the first place.
        var scenario = Scenario.CreateDefault(seed, agentCount) with
        {
            MovementPreset = movementPreset,
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV2,
            BodyRadiusRaw = bodyRadiusRaw,
        };
        scenario.Validate();

        var simulation = BattleSimulation.Create(scenario);
        var interruptFired = ReadInterruptScratch(simulation);
        var interruptApplied = interruptFired.Length > 0;

        var views = simulation.Agents;
        var agentSlots = views.Count;

        // A loadout never changes over a battle, so each agent's canonical row
        // is resolved once, at spawn, and reused every tick.
        var rowOf = new int[agentSlots];
        var rowSpawnAgents = new long[RowBucketCount];
        var rowLivingAgentTicks = new long[RowBucketCount];
        var rowInterruptFirings = new long[RowBucketCount];

        var previousAlive = new bool[agentSlots];
        var previousPosture = new TacticalPosture[agentSlots];
        var previousPhase = new FootworkPhase[agentSlots];
        var previousIntent = new AgentIntent[agentSlots];

        for (var index = 0; index < agentSlots; index++)
        {
            var view = views[index];
            rowOf[index] = CanonicalRowIndex(view.Loadout);
            rowSpawnAgents[rowOf[index]]++;
            previousAlive[index] = view.IsAlive;
            previousPosture[index] = view.TacticalPosture;
            previousPhase[index] = view.FootworkPhase;
            previousIntent[index] = view.Intent;
        }

        var tickDurations = new List<double>(
            Math.Min(requestedTicks, 100_000));
        var flipObservations = 0L;
        var flipCount = 0L;

        for (var requestedTick = 0;
             requestedTick < requestedTicks &&
                simulation.Outcome == BattleOutcome.Ongoing;
             requestedTick++)
        {
            // The Stopwatch bracket holds AdvanceOneTick and nothing else, so
            // the observation loop below cannot inflate the p50 this harness
            // reports.
            var startTimestamp = Stopwatch.GetTimestamp();
            simulation.AdvanceOneTick();
            var elapsed = Stopwatch.GetElapsedTime(startTimestamp);
            tickDurations.Add(elapsed.TotalMilliseconds);

            var tick = simulation.Tick;
            var inFlipWindow =
                tick >= FlipWindowFirstTick && tick <= FlipWindowLastTick;
            views = simulation.Agents;

            for (var index = 0; index < agentSlots; index++)
            {
                var view = views[index];
                var row = rowOf[index];

                if (view.IsAlive)
                {
                    rowLivingAgentTicks[row]++;
                }

                // The scratch array is index-aligned with the agent views:
                // BattleSimulation.UpdateViews writes _agentViews[index] from
                // _agentStates[index], and the interrupt writes
                // _pressureInterruptFired[index] from the same loop over the
                // same storage order.
                if (interruptApplied && interruptFired[index])
                {
                    rowInterruptFirings[row]++;
                }

                if (inFlipWindow && view.IsAlive && previousAlive[index])
                {
                    flipObservations++;
                    if (IsRedefinedFlip(
                            previousPosture[index],
                            previousPhase[index],
                            previousIntent[index],
                            view))
                    {
                        flipCount++;
                    }
                }

                previousAlive[index] = view.IsAlive;
                previousPosture[index] = view.TacticalPosture;
                previousPhase[index] = view.FootworkPhase;
                previousIntent[index] = view.Intent;
            }
        }

        var sortedDurations = tickDurations.ToArray();
        Array.Sort(sortedDurations);

        return new CellResult(
            agentCount,
            seed,
            simulation.Tick,
            simulation.Outcome,
            CountSurvivors(simulation, factionId: 0),
            CountSurvivors(simulation, factionId: 1),
            Percentile(sortedDurations, 0.50),
            Percentile(sortedDurations, 0.95),
            sortedDurations.Length == 0 ? 0 : sortedDurations[^1],
            flipObservations,
            flipCount,
            interruptApplied,
            rowSpawnAgents,
            rowLivingAgentTicks,
            rowInterruptFirings);
    }

    /// <summary>
    /// The redefined phase-flip predicate of design section 2.3, evaluated for
    /// one living warrior on one tick against its own previous tick.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The original criterion counted every tick on which the phase or the
    /// posture differed from one tick earlier. Design section 2.3 redefines it
    /// to count posture and intent changes only, <b>excluding the scripted
    /// <see cref="FootworkPhase.Commit"/> and
    /// <see cref="FootworkPhase.Recover"/> attack-lifecycle transitions</b>,
    /// because a pure four-tick commitment plus four-tick recovery rhythm
    /// produces exactly 25.0% on its own, before any decision has been made,
    /// and a ceiling that a legally specified rhythm fails unaided measures
    /// nothing.
    /// </para>
    /// <para>
    /// <b>Exactly two transitions are excluded here</b>, and they are the two
    /// that the unaided rhythm consists of:
    /// </para>
    /// <list type="number">
    /// <item>
    /// <description>
    /// Any change whose new phase is <see cref="FootworkPhase.Commit"/>. The
    /// ladder never returns <c>Commit</c> except as a continuation of a
    /// <c>Commit</c> already in progress, which is not a change at all, so
    /// every observed change <i>into</i> <c>Commit</c> is the
    /// attack-acceptance write that
    /// <c>ApplyEquipmentAttackFootworkAndDeathCleanup</c> performs — the
    /// scripted head of the lifecycle.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <see cref="FootworkPhase.Commit"/> to
    /// <see cref="FootworkPhase.Recover"/>: the commitment timer expiring into
    /// recovery, which the ladder performs unconditionally.
    /// </description>
    /// </item>
    /// </list>
    /// <para>
    /// Everything else counts, and two consequences of that are deliberate.
    /// A <c>Commit</c>-to-<c>Disengage</c> or
    /// <c>Recover</c>-to-<c>Disengage</c> transition is exactly what the
    /// pressure interrupt produces, and design section 2.3 is explicit that an
    /// interrupt firing <i>does</i> count against the redefined ceiling: a
    /// preset that interrupts every warrior every few ticks is oscillating,
    /// and the criterion should say so. A <c>Recover</c>-to-anything-else
    /// transition also counts, because an expiring recovery falls through the
    /// ladder into the ratio steps, and where it lands is a decision rather
    /// than a script.
    /// </para>
    /// <para>
    /// A tick counts once however many of the three channels moved on it,
    /// because the criterion is a share of <i>ticks</i>, not a sum of changes.
    /// A tick on which the warrior was not alive at both ends is not observed
    /// at all: dying is not indecision.
    /// </para>
    /// </remarks>
    private static bool IsRedefinedFlip(
        TacticalPosture previousPosture,
        FootworkPhase previousPhase,
        AgentIntent previousIntent,
        AgentView view)
    {
        if (view.TacticalPosture != previousPosture)
        {
            return true;
        }

        if (view.Intent != previousIntent)
        {
            return true;
        }

        if (view.FootworkPhase == previousPhase)
        {
            return false;
        }

        if (view.FootworkPhase == FootworkPhase.Commit)
        {
            return false;
        }

        return !(previousPhase == FootworkPhase.Commit &&
            view.FootworkPhase == FootworkPhase.Recover);
    }

    /// <summary>
    /// Fetches the per-tick interrupt scratch array once per run. The field is
    /// <c>readonly</c>, so the array reference cannot move under the harness
    /// and one lookup per run is enough.
    /// </summary>
    private static bool[] ReadInterruptScratch(BattleSimulation simulation)
    {
        var field = typeof(BattleSimulation).GetField(
            PressureInterruptFiredFieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field is null)
        {
            throw new InvalidOperationException(
                $"BattleSimulation no longer declares a private field named " +
                $"'{PressureInterruptFiredFieldName}'. This harness reads it " +
                $"by reflection to count interrupt firings; rename the " +
                $"constant beside this message to match, or the report's " +
                $"firing counts would silently read zero.");
        }

        return field.GetValue(simulation) as bool[] ??
            throw new InvalidOperationException(
                $"BattleSimulation's '{PressureInterruptFiredFieldName}' is " +
                $"no longer a bool[]. See the note above.");
    }

    private static int CountSurvivors(
        BattleSimulation simulation,
        int factionId) =>
        simulation.Agents.Count(
            agent => agent.IsAlive && agent.FactionId == factionId);

    /// <summary>
    /// Mirrors <c>MovementRuleset.CanonicalLoadoutIndex</c> and
    /// <c>MovementRouteRules.CanonicalOpponentIndex</c>, which are both
    /// private or throw on an unrecognised triple. This copy returns
    /// <see cref="OtherRowIndex"/> instead of throwing, so an unexpected
    /// roster shows up as a labelled bucket in the report rather than aborting
    /// a ten-cell run halfway through.
    /// </summary>
    private static int CanonicalRowIndex(CombatLoadout loadout) =>
        (loadout.Weapon, loadout.Armor, loadout.Shield) switch
        {
            (WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None) => 0,
            (WeaponId.Wasay, ArmorId.LightOrganic, ShieldId.None) => 1,
            (WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.None) => 2,
            (WeaponId.Itak, ArmorId.LightOrganic, ShieldId.None) => 3,
            (WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.TallHardwood) => 4,
            (WeaponId.Itak, ArmorId.LightOrganic, ShieldId.TallHardwood) => 5,
            _ => OtherRowIndex,
        };

    /// <summary>
    /// The same percentile rule <c>HeadlessRunner.Percentile</c> applies, so a
    /// p50 printed here and a <c>p50Milliseconds</c> printed by
    /// <c>scripts/benchmark.ps1</c> are the same statistic.
    /// </summary>
    private static double Percentile(double[] sortedValues, double percentile)
    {
        if (sortedValues.Length == 0)
        {
            return 0;
        }

        var rank = (int)Math.Ceiling(percentile * sortedValues.Length) - 1;
        return sortedValues[Math.Clamp(rank, 0, sortedValues.Length - 1)];
    }

    private static double Median(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var sorted = values.ToArray();
        Array.Sort(sorted);
        var middle = sorted.Length / 2;
        return (sorted.Length & 1) == 1
            ? sorted[middle]
            : (sorted[middle - 1] + sorted[middle]) / 2;
    }

    private static void WriteHeader(
        StringBuilder report,
        MovementPresetId movementPreset,
        MovementRuleset ruleset,
        IReadOnlyList<ulong> seeds,
        IReadOnlyList<int> agentCounts,
        int requestedTicks,
        int bodyRadiusRaw)
    {
        report.AppendLine(
            "Hukbo movement V7 pressure-interrupt calibration harness (task E0)");
        report.AppendLine(
            "Measurement only. Nothing here asserts, passes, or fails.");
        report.AppendLine();
        report.Append(CultureInfo.InvariantCulture,
            $"movementPreset        : {movementPreset}\n");
        report.Append(CultureInfo.InvariantCulture,
            $"combatPreset          : {CombatPresetId.PrecolonialPhilippinesV2} (pinned)\n");
        report.Append(CultureInfo.InvariantCulture,
            $"bodyRadiusRaw         : {bodyRadiusRaw.ToString(CultureInfo.InvariantCulture)}\n");
        report.Append(CultureInfo.InvariantCulture,
            $"requestedTicks        : {requestedTicks.ToString(CultureInfo.InvariantCulture)}\n");
        report.Append(CultureInfo.InvariantCulture,
            $"seeds                 : {string.Join(", ", seeds)}\n");
        report.Append(CultureInfo.InvariantCulture,
            $"agentCounts           : {string.Join(", ", agentCounts)}\n");
        report.Append(CultureInfo.InvariantCulture,
            $"usesFootwork          : {ruleset.UsesEquipmentRelativeFootwork}\n");
        report.Append(CultureInfo.InvariantCulture,
            $"appliesInterrupt      : {ruleset.AppliesPressureInterrupt}\n");
        report.Append(CultureInfo.InvariantCulture,
            $"weightsBasisPoints    : support={ruleset.SupportPressureWeightBasisPoints.ToString(CultureInfo.InvariantCulture)}, " +
            $"damage={ruleset.IncomingDamageWeightBasisPoints.ToString(CultureInfo.InvariantCulture)}, " +
            $"allyCollapse={ruleset.AllyCollapseWeightBasisPoints.ToString(CultureInfo.InvariantCulture)}\n");

        if (ruleset.LoadoutMovementProfiles.Length == CanonicalRowCount)
        {
            var thresholds = new List<string>(CanonicalRowCount);
            for (var row = 0; row < CanonicalRowCount; row++)
            {
                var threshold = ruleset.LoadoutMovementProfiles[row]
                    .PressureInterruptThresholdBasisPoints;
                thresholds.Add(
                    RowLabels[row] + "=" +
                    threshold.ToString(CultureInfo.InvariantCulture));
            }

            report.Append(CultureInfo.InvariantCulture,
                $"rowThresholdsBp       : {string.Join(" ", thresholds)}\n");
        }
        else
        {
            report.AppendLine(
                "rowThresholdsBp       : none (this preset registers no " +
                "loadout movement profiles)");
        }

        report.Append(CultureInfo.InvariantCulture,
            $"flipWindow            : ticks {FlipWindowFirstTick.ToString(CultureInfo.InvariantCulture)}"
            + $" through {FlipWindowLastTick.ToString(CultureInfo.InvariantCulture)} inclusive\n");
        report.Append(CultureInfo.InvariantCulture,
            $"operatingSystem       : {RuntimeInformation.OSDescription}\n");
        report.Append(CultureInfo.InvariantCulture,
            $"framework             : {RuntimeInformation.FrameworkDescription}\n");
        report.Append(CultureInfo.InvariantCulture,
            $"processArchitecture   : {RuntimeInformation.ProcessArchitecture}\n");
        report.Append(CultureInfo.InvariantCulture,
            $"processorCount        : {Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture)}\n");
        report.AppendLine();
    }

    private static void WriteCellTable(
        StringBuilder report,
        IReadOnlyList<CellResult> cells)
    {
        report.AppendLine(
            "== Cells: one discarded warm run then one measured run each ==");
        report.AppendLine(
            "agents  seed  terminalTick  outcome           F0    F1     " +
            "p50(ms)   p95(ms)   max(ms)  warmP50(ms)  flips  flipObs  flip%");

        foreach (var cell in cells)
        {
            report.Append(CultureInfo.InvariantCulture,
                $"{cell.AgentCount,6}  {cell.Seed,4}  {cell.TerminalTick,12}  " +
                $"{cell.Outcome,-16}  {cell.Faction0Survivors,4}  " +
                $"{cell.Faction1Survivors,4}  " +
                $"{cell.P50Milliseconds.ToString("F4", CultureInfo.InvariantCulture),9}  " +
                $"{cell.P95Milliseconds.ToString("F4", CultureInfo.InvariantCulture),8}  " +
                $"{cell.MaximumMilliseconds.ToString("F4", CultureInfo.InvariantCulture),8}  " +
                $"{cell.WarmP50Milliseconds.ToString("F4", CultureInfo.InvariantCulture),11}  " +
                $"{cell.FlipCount,5}  {cell.FlipObservations,7}  " +
                $"{FormatShare(cell.FlipCount, cell.FlipObservations),6}\n");
        }

        report.AppendLine();
        report.AppendLine(
            "flip% is the redefined design section 2.3 metric: the share of " +
            "living agent-ticks in the");
        report.AppendLine(
            "window on which the posture, the intent, or a non-scripted " +
            "footwork phase transition moved.");
        report.AppendLine(
            "The two excluded transitions are any change into Commit and " +
            "Commit to Recover. An interrupt");
        report.AppendLine(
            "firing is a Commit or Recover to Disengage transition and is " +
            "counted, by design.");
        report.AppendLine();
    }

    private static void WriteMedians(
        StringBuilder report,
        IReadOnlyList<CellResult> cells,
        IReadOnlyList<int> agentCounts)
    {
        report.AppendLine(
            "== Median p50 per agent count, the design section 2.2 budget " +
            "denominator ==");
        foreach (var agentCount in agentCounts)
        {
            var perSeed = cells
                .Where(cell => cell.AgentCount == agentCount)
                .Select(cell => cell.P50Milliseconds)
                .ToArray();
            report.Append(CultureInfo.InvariantCulture,
                $"{agentCount,6} agents: median p50 = " +
                $"{Median(perSeed).ToString("F4", CultureInfo.InvariantCulture)} ms " +
                $"over {perSeed.Length.ToString(CultureInfo.InvariantCulture)} seeds\n");
        }

        report.AppendLine();
        report.AppendLine(
            "== Termination bar, design section 2.1: every cell decisive " +
            "within 6,000 ticks ==");
        foreach (var cell in cells)
        {
            var decisive = cell.Outcome != BattleOutcome.Draw &&
                cell.Outcome != BattleOutcome.Ongoing;
            var withinBar = decisive && cell.TerminalTick <= 6_000;
            report.Append(CultureInfo.InvariantCulture,
                $"{cell.AgentCount,6} agents seed {cell.Seed}: " +
                $"{cell.Outcome} at tick {cell.TerminalTick.ToString(CultureInfo.InvariantCulture)} " +
                $"-> {(withinBar ? "within the bar" : "outside the bar")}\n");
        }

        report.AppendLine();
        report.AppendLine(
            "The lines above are arithmetic on measured numbers, not a " +
            "verdict. Task E1 owns the verdict.");
        report.AppendLine();
    }

    private static void WriteRowTable(
        StringBuilder report,
        IReadOnlyList<CellResult> cells)
    {
        report.AppendLine("== Per-row pressure-interrupt firings ==");
        report.AppendLine(
            "A firing is one (agent, tick) pair on which the interrupt " +
            "predicate returned true, read from");
        report.AppendLine(
            "BattleSimulation's per-tick scratch and not from the persistent " +
            "break-off flag, so it is a count");
        report.AppendLine(
            "of break-offs and not of ticks spent broken off. spawnAgents " +
            "distinguishes a row that was");
        report.AppendLine(
            "fielded and never fired from a row this cell never fielded at " +
            "all.");
        report.AppendLine();
        report.AppendLine(
            "agents  seed  row  spawnAgents  livingAgentTicks  firings  note");

        foreach (var cell in cells)
        {
            for (var row = 0; row < RowBucketCount; row++)
            {
                if (cell.RowSpawnAgents[row] == 0 && row == OtherRowIndex)
                {
                    continue;
                }

                var note = !cell.InterruptApplied
                    ? "preset applies no interrupt"
                    : cell.RowSpawnAgents[row] == 0
                        ? "row never fielded in this cell"
                        : cell.RowInterruptFirings[row] == 0
                            ? "fielded, never fired"
                            : string.Empty;

                report.Append(CultureInfo.InvariantCulture,
                    $"{cell.AgentCount,6}  {cell.Seed,4}  {RowLabels[row],3}  " +
                    $"{cell.RowSpawnAgents[row],11}  " +
                    $"{cell.RowLivingAgentTicks[row],16}  " +
                    $"{cell.RowInterruptFirings[row],7}  {note}\n");
            }
        }

        report.AppendLine();
        report.AppendLine("== Per-row totals across every measured cell ==");
        report.AppendLine("row  spawnAgents  livingAgentTicks  firings");
        for (var row = 0; row < RowBucketCount; row++)
        {
            var spawn = cells.Sum(cell => cell.RowSpawnAgents[row]);
            if (spawn == 0 && row == OtherRowIndex)
            {
                continue;
            }

            report.Append(CultureInfo.InvariantCulture,
                $"{RowLabels[row],3}  {spawn,11}  " +
                $"{cells.Sum(cell => cell.RowLivingAgentTicks[row]),16}  " +
                $"{cells.Sum(cell => cell.RowInterruptFirings[row]),7}\n");
        }

        report.AppendLine();
    }

    private static string FormatShare(long numerator, long denominator) =>
        denominator == 0
            ? "n/a"
            : (100.0 * numerator / denominator)
                .ToString("F2", CultureInfo.InvariantCulture);

    /// <summary>
    /// Everything one measured cell produced. Arrays are built once inside
    /// <see cref="RunOneCell"/> and handed over; nothing mutates them
    /// afterwards.
    /// </summary>
    private sealed record CellResult(
        int AgentCount,
        ulong Seed,
        long TerminalTick,
        BattleOutcome Outcome,
        int Faction0Survivors,
        int Faction1Survivors,
        double P50Milliseconds,
        double P95Milliseconds,
        double MaximumMilliseconds,
        long FlipObservations,
        long FlipCount,
        bool InterruptApplied,
        long[] RowSpawnAgents,
        long[] RowLivingAgentTicks,
        long[] RowInterruptFirings)
    {
        /// <summary>
        /// The p50 of the discarded warm run for the same cell, recorded so
        /// the discard is visible in the report rather than merely claimed.
        /// </summary>
        internal double WarmP50Milliseconds { get; init; }
    }
}

#if HUKBO_CALIBRATION

/// <summary>
/// The command-line invocation of
/// <see cref="PressureInterruptCalibrationHarness.RunMatrix"/>.
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
/// dotnet test tests/Hukbo.Core.Tests -c Release ^
///   -p:DefineConstants=HUKBO_CALIBRATION ^
///   --filter FullyQualifiedName~PressureInterruptCalibrationRun ^
///   --logger "console;verbosity=detailed"
/// </code>
/// <para>
/// It asserts nothing. It prints a report and passes, because it is a
/// measurement and a measurement has no verdict.
/// </para>
/// </remarks>
public sealed class PressureInterruptCalibrationRun
{
    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public PressureInterruptCalibrationRun(
        Xunit.Abstractions.ITestOutputHelper output) => _output = output;

    [Fact]
    public void PrintTheCalibrationMatrix()
    {
        var preset = Environment.GetEnvironmentVariable("HUKBO_MOVEMENT_PRESET")
            is { Length: > 0 } requested &&
            Enum.TryParse<MovementPresetId>(requested, out var parsed)
                ? parsed
                : MovementPresetId.EquipmentRelativeFootworkV7;

        var seeds = ParseSeeds(
            Environment.GetEnvironmentVariable("HUKBO_CALIBRATION_SEEDS"));
        var agentCounts = ParseAgentCounts(
            Environment.GetEnvironmentVariable("HUKBO_CALIBRATION_AGENTS"));

        var report = PressureInterruptCalibrationHarness.RunMatrix(
            preset,
            seeds,
            agentCounts);

        _output.WriteLine(report);
        Console.WriteLine(report);
    }

    private static ulong[]? ParseSeeds(string? raw) =>
        raw is { Length: > 0 }
            ? raw.Split(',', StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries)
                .Select(part => ulong.Parse(
                    part, System.Globalization.CultureInfo.InvariantCulture))
                .ToArray()
            : null;

    private static int[]? ParseAgentCounts(string? raw) =>
        raw is { Length: > 0 }
            ? raw.Split(',', StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries)
                .Select(part => int.Parse(
                    part, System.Globalization.CultureInfo.InvariantCulture))
                .ToArray()
            : null;
}

#endif
