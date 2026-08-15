using System.Globalization;
using System.Text;
using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// The measurement instrument commissioned by task 1 of the 2026-08-15 in-fight
/// evasion plan. It runs a fixed twenty-seed matrix under one caller-supplied
/// movement preset and reports, per seed and pooled, the five quantities that
/// design section 8's anti-goal bars are written against: rooted share, travel
/// per living agent, mean net spawn-to-terminal displacement, mean contact
/// retention in agent-ticks, and each run's terminal tick and outcome.
/// </summary>
/// <remarks>
/// <para>
/// <b>This measures. It does not tune, and it does not assert.</b> Nothing here
/// reads a shipped constant in order to check it, nothing here passes or fails
/// on a threshold, and no production file participates. The eight bars of
/// design section 8 have no numbers behind them yet — producing those numbers
/// is the whole purpose of this file, and asserting a bar before it has been
/// measured would only pin a guess.
/// </para>
/// <para>
/// It follows the shape of the two calibration harnesses already in this
/// project, <c>PressureInterruptCalibrationHarness</c> and
/// <c>RangedCalibrationHarness</c>: a pure static reporting class whose single
/// entry point returns the whole report as one block of text, with the xunit
/// entry point separated out below it. It lives in <c>Hukbo.Core.Tests</c>
/// rather than in <c>Hukbo.Headless</c> for the reason those two record — the
/// headless runner exposes no movement-preset selection to an in-process
/// caller, and the constants this harness compares against are
/// <c>internal</c> to <c>Hukbo.Core</c> and <c>Hukbo.Shared.Core</c>, which
/// only the test assembly may see.
/// </para>
/// <para>
/// Unlike those two, this harness reaches no private field by reflection. Every
/// quantity below is derived from the public <see cref="AgentView"/> projection
/// that <see cref="BattleSimulation.Agents"/> republishes each tick, so it
/// measures exactly what a spectator or the client could observe, and it cannot
/// break on a rename inside the simulation.
/// </para>
/// <para>
/// All accumulation is integer. The <see cref="double"/> values below appear
/// only inside the formatting helpers, so that a share can be printed as a
/// decimal fraction; no measured quantity is ever carried in one, and no
/// arithmetic that decides a reported figure passes through floating point.
/// </para>
/// </remarks>
internal static class EvasionCalibrationHarness
{
    /// <summary>
    /// The twenty seeds of the plan's task 1 matrix, <c>1</c> through
    /// <c>20</c> inclusive. Twenty is the denominator design section 8's
    /// decisiveness bar ("19 of 20 seeds decisive") is stated over, so the
    /// seed list is fixed here rather than defaulted by a caller.
    /// </summary>
    private static readonly ulong[] MatrixSeeds =
        [1, 2, 3, 4, 5, 6, 7, 8, 9, 10,
         11, 12, 13, 14, 15, 16, 17, 18, 19, 20];

    /// <summary>
    /// The agent count of the task 1 matrix: two hundred warriors, one hundred
    /// a side, the same size the canonical gate's determinism workload runs.
    /// </summary>
    private const int MatrixAgentCount = 200;

    /// <summary>
    /// The per-tick displacement, in raw fixed-point units, strictly below
    /// which a living warrior counts as <i>rooted</i> for the rooted-share
    /// figure.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the value of <c>GaitGeometry.CrawlThresholdRawPerTick</c> in
    /// <c>Hukbo.Client</c> — the renderer's legibility floor, the speed below
    /// which the gait animation stops swinging legs and the pawn reads as
    /// standing still. It is restated as a literal here because this project
    /// cannot reference <c>Hukbo.Client</c>, and it is deliberately the
    /// renderer's number rather than a number of this harness's own choosing:
    /// the anti-goal it serves is about what a spectator sees, so the
    /// threshold has to be the one the spectator's pawn actually uses.
    /// </para>
    /// <para>
    /// If that client constant ever moves, this literal has to move with it or
    /// the rooted share stops meaning what its name says. Nothing enforces
    /// that automatically, because a test project may not reach across the
    /// simulation-to-client boundary to read it.
    /// </para>
    /// </remarks>
    private const int CrawlThresholdRawPerTick = 60;

    /// <summary>
    /// The single deliberate entry point. Runs the twenty-seed matrix under
    /// <paramref name="movementPreset"/> and returns the whole report as one
    /// block of Markdown.
    /// </summary>
    /// <param name="movementPreset">
    /// The movement preset under measurement. Named by the caller and never
    /// defaulted, because measuring whatever the shipped default happens to be
    /// is the one thing a calibration harness must not silently do.
    /// </param>
    /// <param name="combatPreset">
    /// The combat preset under measurement. Defaults to
    /// <see cref="CombatPresetId.PrecolonialPhilippinesV5"/>, the preset the
    /// client ships, so the measurement describes the battle a player actually
    /// watches rather than a historical control shape.
    /// </param>
    /// <param name="seeds">
    /// The seeds to measure, or <see langword="null"/> for the matrix's own
    /// one through twenty.
    /// </param>
    /// <param name="agentCount">
    /// The total agent count per run, halved into two equal factions.
    /// </param>
    internal static string RunMatrix(
        MovementPresetId movementPreset,
        CombatPresetId combatPreset = CombatPresetId.PrecolonialPhilippinesV5,
        IReadOnlyList<ulong>? seeds = null,
        int agentCount = MatrixAgentCount)
    {
        var seedList = seeds ?? MatrixSeeds;

        var results = new List<SeedResult>(seedList.Count);
        foreach (var seed in seedList)
        {
            results.Add(RunOneSeed(
                movementPreset, combatPreset, seed, agentCount));
        }

        var report = new StringBuilder();
        WriteHeader(report, movementPreset, combatPreset, seedList, agentCount);
        WriteTable(report, results);
        WriteDefinitions(report);

        // The writers below mix AppendLine with rows that end in an explicit
        // newline inside an interpolated string, so the buffer holds both
        // endings. Normalising once here means the block a person pastes into
        // the design document has one line ending throughout.
        return report.ToString().ReplaceLineEndings();
    }

    /// <summary>
    /// Runs one seed to its own termination — the simulation stops advancing
    /// once <see cref="BattleSimulation.Outcome"/> leaves
    /// <see cref="BattleOutcome.Ongoing"/>, which it does either on a decisive
    /// outcome or on reaching the scenario's own tick limit — accumulating
    /// every measured quantity from consecutive <see cref="AgentView"/>
    /// snapshots as it goes.
    /// </summary>
    private static SeedResult RunOneSeed(
        MovementPresetId movementPreset,
        CombatPresetId combatPreset,
        ulong seed,
        int agentCount)
    {
        var scenario = Scenario.CreateDefault(seed, agentCount) with
        {
            MovementPreset = movementPreset,
            CombatPreset = combatPreset,
        };
        scenario.Validate();

        var simulation = BattleSimulation.Create(scenario);
        var contactSquaredDistance =
            CollisionGeometry.ContactSquaredDistance(scenario.BodyRadiusRaw);
        var rules = CombatPresetRegistry.Get(combatPreset);

        var views = simulation.Agents;
        var agentSlots = views.Count;

        var spawnXRaw = new int[agentSlots];
        var spawnYRaw = new int[agentSlots];
        var previousXRaw = new int[agentSlots];
        var previousYRaw = new int[agentSlots];
        var previousAlive = new bool[agentSlots];

        // A loadout never changes over a battle, so each warrior's own reach
        // is resolved once at spawn from the same weapon profile
        // BattleSimulation.CreateAgent reads. Reach is centre-to-centre, which
        // is what makes it directly comparable with the centre-to-centre
        // distances measured below.
        var attackRangeSquared = new long[agentSlots];

        // An entity identifier never changes and no agent is ever added or
        // removed mid-battle, so the identifier-to-slot map is built once at
        // spawn. It is only ever read by key; it is never enumerated, so no
        // hash-set iteration order can reach a reported figure.
        var slotOfEntityId = new Dictionary<ulong, int>(agentSlots);

        for (var index = 0; index < agentSlots; index++)
        {
            var view = views[index];
            spawnXRaw[index] = view.XRaw;
            spawnYRaw[index] = view.YRaw;
            previousXRaw[index] = view.XRaw;
            previousYRaw[index] = view.YRaw;
            previousAlive[index] = view.IsAlive;
            slotOfEntityId[view.EntityId] = index;

            var attackRangeRaw = rules.HasWeaponProfiles
                ? rules.ResolveWeaponProfile(
                    view.Loadout.Weapon, view.Loadout.Shield).AttackRangeRaw
                : scenario.AttackRangeRaw;
            attackRangeSquared[index] =
                checked((long)attackRangeRaw * attackRangeRaw);
        }

        var livingAgentTicks = 0L;
        var rootedAgentTicks = 0L;
        var totalTravelRaw = 0L;
        var targetHeldAgentTicks = 0L;
        var contactAgentTicks = 0L;
        var reachAgentTicks = 0L;
        var minimumTargetSquaredDistance = long.MaxValue;

        while (simulation.Outcome == BattleOutcome.Ongoing)
        {
            simulation.AdvanceOneTick();
            views = simulation.Agents;

            for (var index = 0; index < agentSlots; index++)
            {
                var view = views[index];

                // A displacement needs a living warrior at both ends of the
                // step. A warrior that died on this tick contributes neither a
                // living agent-tick nor a travel sample: dying is not
                // standing still, and it is not moving either.
                if (!view.IsAlive || !previousAlive[index])
                {
                    previousXRaw[index] = view.XRaw;
                    previousYRaw[index] = view.YRaw;
                    previousAlive[index] = view.IsAlive;
                    continue;
                }

                livingAgentTicks++;

                var displacementRaw = FixedPoint.IntegerSquareRoot(
                    CollisionGeometry.SquaredDistance(
                        previousXRaw[index],
                        previousYRaw[index],
                        view.XRaw,
                        view.YRaw));
                totalTravelRaw = checked(totalTravelRaw + displacementRaw);

                if (displacementRaw < CrawlThresholdRawPerTick)
                {
                    rootedAgentTicks++;
                }

                var targetSquaredDistance = SquaredDistanceToLivingEnemyTarget(
                    view, views, slotOfEntityId);
                if (targetSquaredDistance >= 0)
                {
                    targetHeldAgentTicks++;

                    if (targetSquaredDistance <= contactSquaredDistance)
                    {
                        contactAgentTicks++;
                    }

                    if (targetSquaredDistance <= attackRangeSquared[index])
                    {
                        reachAgentTicks++;
                    }

                    // The minimum is tracked squared rather than rooted,
                    // because IntegerSquareRoot floors: two distances a
                    // fraction of a raw unit apart root to the same integer,
                    // and the whole point of this figure is to be compared
                    // exactly against the contact distance.
                    if (targetSquaredDistance < minimumTargetSquaredDistance)
                    {
                        minimumTargetSquaredDistance = targetSquaredDistance;
                    }
                }

                previousXRaw[index] = view.XRaw;
                previousYRaw[index] = view.YRaw;
                previousAlive[index] = view.IsAlive;
            }
        }

        // A corpse retains its final position, so the terminal snapshot gives
        // every agent a spawn-to-terminal displacement whether it survived or
        // not. The mean below is therefore over the whole army, which is what
        // "net drift" has to mean if it is to describe where the battle was
        // fought rather than only where the winners ended up.
        var netDisplacementSumRaw = 0L;
        views = simulation.Agents;
        for (var index = 0; index < agentSlots; index++)
        {
            var view = views[index];
            netDisplacementSumRaw = checked(netDisplacementSumRaw +
                FixedPoint.IntegerSquareRoot(
                    CollisionGeometry.SquaredDistance(
                        spawnXRaw[index],
                        spawnYRaw[index],
                        view.XRaw,
                        view.YRaw)));
        }

        return new SeedResult(
            seed,
            agentSlots,
            simulation.Tick,
            simulation.Outcome,
            CountSurvivors(simulation, factionId: 0),
            CountSurvivors(simulation, factionId: 1),
            livingAgentTicks,
            rootedAgentTicks,
            totalTravelRaw,
            netDisplacementSumRaw,
            targetHeldAgentTicks,
            contactAgentTicks,
            reachAgentTicks,
            minimumTargetSquaredDistance,
            contactSquaredDistance);
    }

    /// <summary>
    /// The squared centre distance from <paramref name="view"/> to its
    /// currently selected target, when that target is a living warrior of the
    /// other faction, and <c>-1</c> when the warrior holds no such target at
    /// all. Returning the distance rather than a boolean is what lets one pass
    /// over the views answer every retention question the report asks — held
    /// at all, held inside body contact, held inside reach, and how close the
    /// closest such pairing ever came.
    /// </summary>
    private static long SquaredDistanceToLivingEnemyTarget(
        AgentView view,
        IReadOnlyList<AgentView> views,
        Dictionary<ulong, int> slotOfEntityId)
    {
        if (view.TargetEntityId is not { } targetEntityId)
        {
            return -1;
        }

        if (!slotOfEntityId.TryGetValue(targetEntityId, out var targetSlot))
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

    private static int CountSurvivors(
        BattleSimulation simulation,
        int factionId) =>
        simulation.Agents.Count(
            agent => agent.IsAlive && agent.FactionId == factionId);

    private static void WriteHeader(
        StringBuilder report,
        MovementPresetId movementPreset,
        CombatPresetId combatPreset,
        IReadOnlyList<ulong> seeds,
        int agentCount)
    {
        report.AppendLine("# Hukbo in-fight evasion calibration harness");
        report.AppendLine();
        report.AppendLine(
            "Measurement only. Nothing here asserts, passes, or fails.");
        report.AppendLine();
        report.Append(CultureInfo.InvariantCulture,
            $"- `movementPreset`: `{movementPreset}`\n");
        report.Append(CultureInfo.InvariantCulture,
            $"- `combatPreset`: `{combatPreset}`\n");
        report.Append(CultureInfo.InvariantCulture,
            $"- `totalAgents`: {agentCount.ToString(CultureInfo.InvariantCulture)}\n");
        report.Append(CultureInfo.InvariantCulture,
            $"- `seeds`: {string.Join(", ", seeds)}\n");
        report.Append(CultureInfo.InvariantCulture,
            $"- `rootedThresholdRawPerTick`: {CrawlThresholdRawPerTick.ToString(CultureInfo.InvariantCulture)}"
            + $" (GaitGeometry's crawl threshold, the renderer legibility floor)\n");
        report.AppendLine(
            "- every other scenario field is `Scenario.CreateDefault`'s own, " +
            "including the shipped body radius, map size, and 10,000-tick limit");
        report.AppendLine();
    }

    private static void WriteTable(
        StringBuilder report,
        IReadOnlyList<SeedResult> results)
    {
        report.AppendLine("## Per seed");
        report.AppendLine();
        report.AppendLine(
            "| seed | terminalTick | outcome | F0 | F1 | rootedShare | " +
            "travelPerLivingAgent | meanNetDisplacement | " +
            "contactRetentionAgentTicks | reachRetentionAgentTicks | " +
            "targetHeldAgentTicks | closestTargetSquared |");
        report.AppendLine(
            "| ---: | ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: " +
            "| ---: | ---: | ---: |");

        foreach (var result in results)
        {
            AppendRow(
                report,
                result.Seed.ToString(CultureInfo.InvariantCulture),
                result);
        }

        report.AppendLine();
        report.AppendLine("## Pooled");
        report.AppendLine();
        report.AppendLine(
            "| seeds | totalTicks | decisive | F0 wins | F1 wins | " +
            "rootedShare | travelPerLivingAgent | meanNetDisplacement | " +
            "contactRetentionAgentTicks | reachRetentionAgentTicks | " +
            "targetHeldAgentTicks | closestTargetSquared |");
        report.AppendLine(
            "| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: " +
            "| ---: | ---: | ---: |");

        var pooled = Pool(results);
        var decisive = results.Count(result => IsDecisive(result.Outcome));
        var faction0Wins = results.Count(
            result => result.Outcome == BattleOutcome.Faction0Victory);
        var faction1Wins = results.Count(
            result => result.Outcome == BattleOutcome.Faction1Victory);

        report.Append(CultureInfo.InvariantCulture,
            $"| {results.Count.ToString(CultureInfo.InvariantCulture)} " +
            $"| {pooled.TerminalTick.ToString(CultureInfo.InvariantCulture)} " +
            $"| {decisive.ToString(CultureInfo.InvariantCulture)} of {results.Count.ToString(CultureInfo.InvariantCulture)} " +
            $"| {faction0Wins.ToString(CultureInfo.InvariantCulture)} " +
            $"| {faction1Wins.ToString(CultureInfo.InvariantCulture)} " +
            $"| {FormatShare(pooled.RootedAgentTicks, pooled.LivingAgentTicks)} " +
            $"| {FormatQuotient(pooled.TotalTravelRaw, pooled.AgentSlots)} " +
            $"| {FormatQuotient(pooled.NetDisplacementSumRaw, pooled.AgentSlots)} " +
            $"| {FormatQuotient(pooled.ContactAgentTicks, pooled.AgentSlots)} " +
            $"| {FormatQuotient(pooled.ReachAgentTicks, pooled.AgentSlots)} " +
            $"| {FormatQuotient(pooled.TargetHeldAgentTicks, pooled.AgentSlots)} " +
            $"| {pooled.MinimumTargetSquaredDistance.ToString(CultureInfo.InvariantCulture)} |\n");

        report.AppendLine();
        report.AppendLine("### Pooled raw accumulators");
        report.AppendLine();
        report.Append(CultureInfo.InvariantCulture,
            $"- `livingAgentTicks`: {pooled.LivingAgentTicks.ToString(CultureInfo.InvariantCulture)}\n");
        report.Append(CultureInfo.InvariantCulture,
            $"- `rootedAgentTicks`: {pooled.RootedAgentTicks.ToString(CultureInfo.InvariantCulture)}\n");
        report.Append(CultureInfo.InvariantCulture,
            $"- `totalTravelRaw`: {pooled.TotalTravelRaw.ToString(CultureInfo.InvariantCulture)}\n");
        report.Append(CultureInfo.InvariantCulture,
            $"- `netDisplacementSumRaw`: {pooled.NetDisplacementSumRaw.ToString(CultureInfo.InvariantCulture)}\n");
        report.Append(CultureInfo.InvariantCulture,
            $"- `targetHeldAgentTicks`: {pooled.TargetHeldAgentTicks.ToString(CultureInfo.InvariantCulture)}\n");
        report.Append(CultureInfo.InvariantCulture,
            $"- `contactAgentTicks`: {pooled.ContactAgentTicks.ToString(CultureInfo.InvariantCulture)}\n");
        report.Append(CultureInfo.InvariantCulture,
            $"- `reachAgentTicks`: {pooled.ReachAgentTicks.ToString(CultureInfo.InvariantCulture)}\n");
        report.Append(CultureInfo.InvariantCulture,
            $"- `spawnAgentSlots`: {pooled.AgentSlots.ToString(CultureInfo.InvariantCulture)}\n");
        report.Append(CultureInfo.InvariantCulture,
            $"- `bodyContactSquared`: {pooled.ContactSquaredDistance.ToString(CultureInfo.InvariantCulture)}"
            + $" (`(2 * bodyRadius)^2`, the value the contact column tests against)\n");
        report.Append(CultureInfo.InvariantCulture,
            $"- `closestTargetSquared`: {pooled.MinimumTargetSquaredDistance.ToString(CultureInfo.InvariantCulture)}"
            + $" (the closest any warrior ever came, squared, to a living enemy target it held)\n");
        report.AppendLine();
    }

    private static void AppendRow(
        StringBuilder report,
        string label,
        SeedResult result) =>
        report.Append(CultureInfo.InvariantCulture,
            $"| {label} " +
            $"| {result.TerminalTick.ToString(CultureInfo.InvariantCulture)} " +
            $"| {result.Outcome} " +
            $"| {result.Faction0Survivors.ToString(CultureInfo.InvariantCulture)} " +
            $"| {result.Faction1Survivors.ToString(CultureInfo.InvariantCulture)} " +
            $"| {FormatShare(result.RootedAgentTicks, result.LivingAgentTicks)} " +
            $"| {FormatQuotient(result.TotalTravelRaw, result.AgentSlots)} " +
            $"| {FormatQuotient(result.NetDisplacementSumRaw, result.AgentSlots)} " +
            $"| {FormatQuotient(result.ContactAgentTicks, result.AgentSlots)} " +
            $"| {FormatQuotient(result.ReachAgentTicks, result.AgentSlots)} " +
            $"| {FormatQuotient(result.TargetHeldAgentTicks, result.AgentSlots)} " +
            $"| {result.MinimumTargetSquaredDistance.ToString(CultureInfo.InvariantCulture)} |\n");

    private static void WriteDefinitions(StringBuilder report)
    {
        report.AppendLine("## What each column is");
        report.AppendLine();
        report.AppendLine(
            "- `rootedShare` — living agent-ticks whose per-tick displacement " +
            "was strictly below 60 raw, divided by living agent-ticks. A tick " +
            "counts as a living agent-tick only when the warrior was alive at " +
            "both ends of the step, so a death contributes neither a rooted " +
            "tick nor a moving one.");
        report.AppendLine(
            "- `travelPerLivingAgent` — the sum of every per-tick " +
            "displacement, in raw units, divided by the number of agents the " +
            "run spawned. Every agent is alive at spawn and accrues travel " +
            "only while alive, so the divisor is the spawn count.");
        report.AppendLine(
            "- `meanNetDisplacement` — the mean, over every spawned agent, of " +
            "the straight-line distance in raw units from its spawn position " +
            "to its terminal position. Corpses are included; a corpse keeps " +
            "its final position, and where the dead lie is part of where the " +
            "battle was fought.");
        report.AppendLine(
            "- `contactRetentionAgentTicks` — the mean, over every spawned " +
            "agent, of the number of ticks on which that agent was alive and " +
            "held a selected target that was a living enemy inside body " +
            "contact, `squaredDistance <= (2 * bodyRadius)^2`. **Read the " +
            "warning below before using this column.**");
        report.AppendLine(
            "- `reachRetentionAgentTicks` — the same count with body contact " +
            "replaced by that warrior's own centre-to-centre attack range, " +
            "resolved from its weapon profile exactly as " +
            "`BattleSimulation.CreateAgent` resolves it. This is the column " +
            "that actually measures whether a warrior is still in the fight " +
            "it chose.");
        report.AppendLine(
            "- `targetHeldAgentTicks` — the same count with no distance test " +
            "at all: the agent was alive and held a living enemy target at " +
            "any range. It is the denominator the two columns above are " +
            "shares of.");
        report.AppendLine(
            "- `closestTargetSquared` — the smallest squared centre-to-centre " +
            "distance, in squared raw units, that any warrior ever reached " +
            "against a living enemy target it held. It is reported squared " +
            "rather than rooted because `IntegerSquareRoot` floors, and a " +
            "floored distance cannot be compared exactly against the contact " +
            "distance, which is the one comparison this figure exists to " +
            "make.");
        report.AppendLine(
            "- `F0` / `F1` — survivors of each faction at the terminal tick.");
        report.AppendLine();
        report.AppendLine(
            "### Warning: the body-contact column is degenerate, by " +
            "construction");
        report.AppendLine();
        report.AppendLine(
            "`CollisionResolver` enforces non-penetration on every committed " +
            "position, so a committed `squaredDistance` is never strictly " +
            "below `(2 * bodyRadius)^2`. The contact test is " +
            "tangency-inclusive, so the only distance that can satisfy it is " +
            "**exactly** `2 * bodyRadius`, and integer steps toward a moving " +
            "target land on that single value essentially never. Compare " +
            "`closestTargetSquared` with `bodyContactSquared` above: the " +
            "measured minimum over the whole matrix is the evidence, not the " +
            "argument.");
        report.AppendLine();
        report.AppendLine(
            "A retention bar written against this column is therefore " +
            "vacuous — it compares zero with zero and passes whatever an " +
            "evasion rule does. Use `reachRetentionAgentTicks` instead.");
        report.AppendLine();
        report.AppendLine(
            "Every distance is a raw fixed-point unit; `FixedPoint.Scale` raw " +
            "units are one world unit. Every accumulator above is a " +
            "`checked` integer; the decimal figures are produced by the " +
            "formatters at print time and nothing measured is carried in a " +
            "floating-point value.");
        report.AppendLine();
    }

    private static bool IsDecisive(BattleOutcome outcome) =>
        outcome is BattleOutcome.Faction0Victory or
            BattleOutcome.Faction1Victory;

    /// <summary>
    /// Sums every accumulator across the measured seeds. The pooled figures
    /// are therefore ratios of sums rather than means of ratios, so a long
    /// battle weighs proportionally more than a short one — which is the
    /// correct weighting for a share of agent-ticks.
    /// <c>TerminalTick</c> in the pooled row is the sum of terminal ticks,
    /// that is the total simulated ticks across the matrix, and the outcome
    /// and survivor fields are not meaningful pooled and are not read.
    /// </summary>
    private static SeedResult Pool(IReadOnlyList<SeedResult> results)
    {
        var agentSlots = 0L;
        var terminalTick = 0L;
        var livingAgentTicks = 0L;
        var rootedAgentTicks = 0L;
        var totalTravelRaw = 0L;
        var netDisplacementSumRaw = 0L;
        var targetHeldAgentTicks = 0L;
        var contactAgentTicks = 0L;
        var reachAgentTicks = 0L;
        var minimumTargetSquaredDistance = long.MaxValue;
        var contactSquaredDistance = 0L;

        foreach (var result in results)
        {
            agentSlots = checked(agentSlots + result.AgentSlots);
            terminalTick = checked(terminalTick + result.TerminalTick);
            livingAgentTicks =
                checked(livingAgentTicks + result.LivingAgentTicks);
            rootedAgentTicks =
                checked(rootedAgentTicks + result.RootedAgentTicks);
            totalTravelRaw = checked(totalTravelRaw + result.TotalTravelRaw);
            netDisplacementSumRaw = checked(
                netDisplacementSumRaw + result.NetDisplacementSumRaw);
            targetHeldAgentTicks =
                checked(targetHeldAgentTicks + result.TargetHeldAgentTicks);
            contactAgentTicks =
                checked(contactAgentTicks + result.ContactAgentTicks);
            reachAgentTicks =
                checked(reachAgentTicks + result.ReachAgentTicks);
            minimumTargetSquaredDistance = Math.Min(
                minimumTargetSquaredDistance,
                result.MinimumTargetSquaredDistance);
            contactSquaredDistance = result.ContactSquaredDistance;
        }

        return new SeedResult(
            Seed: 0,
            AgentSlots: agentSlots,
            TerminalTick: terminalTick,
            Outcome: BattleOutcome.Ongoing,
            Faction0Survivors: 0,
            Faction1Survivors: 0,
            LivingAgentTicks: livingAgentTicks,
            RootedAgentTicks: rootedAgentTicks,
            TotalTravelRaw: totalTravelRaw,
            NetDisplacementSumRaw: netDisplacementSumRaw,
            TargetHeldAgentTicks: targetHeldAgentTicks,
            ContactAgentTicks: contactAgentTicks,
            ReachAgentTicks: reachAgentTicks,
            MinimumTargetSquaredDistance: minimumTargetSquaredDistance,
            ContactSquaredDistance: contactSquaredDistance);
    }

    /// <summary>
    /// Formats a ratio of two integer accumulators as a decimal fraction with
    /// four places. The division happens at print time and only at print time.
    /// </summary>
    private static string FormatShare(long numerator, long denominator) =>
        denominator == 0
            ? "n/a"
            : ((double)numerator / denominator)
                .ToString("F4", CultureInfo.InvariantCulture);

    /// <summary>
    /// Formats a per-agent quotient with two decimal places, for the columns
    /// whose magnitude is far above one and whose fourth decimal place would
    /// be noise.
    /// </summary>
    private static string FormatQuotient(long numerator, long denominator) =>
        denominator == 0
            ? "n/a"
            : ((double)numerator / denominator)
                .ToString("F2", CultureInfo.InvariantCulture);

    /// <summary>
    /// Everything one measured seed produced. Every field is an integer
    /// accumulator; no ratio is stored, so a reader of the pooled row can
    /// re-derive any other definition of these quantities from the raw
    /// accumulators the report prints beneath it.
    /// </summary>
    private sealed record SeedResult(
        ulong Seed,
        long AgentSlots,
        long TerminalTick,
        BattleOutcome Outcome,
        int Faction0Survivors,
        int Faction1Survivors,
        long LivingAgentTicks,
        long RootedAgentTicks,
        long TotalTravelRaw,
        long NetDisplacementSumRaw,
        long TargetHeldAgentTicks,
        long ContactAgentTicks,
        long ReachAgentTicks,
        long MinimumTargetSquaredDistance,
        long ContactSquaredDistance);
}

#if HUKBO_CALIBRATION

/// <summary>
/// The xunit entry point for
/// <see cref="EvasionCalibrationHarness"/>. It asserts nothing and always
/// passes, because a measurement has no verdict; it exists so the twenty-seed
/// table can be produced by a person and pasted into section 8 of the
/// 2026-08-15 in-fight evasion design document.
/// </summary>
/// <remarks>
/// <para>
/// <b>This type does not exist in any ordinary build.</b> It is compiled only
/// when the <c>HUKBO_CALIBRATION</c> preprocessor symbol is defined, which
/// nothing in <c>Directory.Build.props</c>, in the project file, in
/// <c>scripts/</c>, or in the canonical gate defines. A gate run therefore
/// discovers no test here at all, and the suite's test count is unmoved by
/// this file. That matches
/// <see cref="PressureInterruptCalibrationHarness"/>, which reached the same
/// conclusion for the same reason: a twenty-seed sweep is several seconds of
/// work with no verdict at the end of it, and the gate should not pay for it
/// on every run.
/// </para>
/// <para>
/// Run it deliberately, and only deliberately:
/// </para>
/// <code>
/// dotnet test tests/Hukbo.Core.Tests -c Release ^
///   -p:DefineConstants=HUKBO_CALIBRATION ^
///   --filter FullyQualifiedName~EvasionCalibrationRun ^
///   --logger "console;verbosity=detailed"
/// </code>
/// <para>
/// The measured movement preset is read from the
/// <c>HUKBO_EVASION_MOVEMENT_PRESET</c> environment variable and falls back to
/// <see cref="MovementPresetId.CohortLateralSpreadV13"/>, the preset the client
/// ships today and therefore the one the task-1 baseline column describes. Plan
/// task 13 re-runs this same harness with that variable set to
/// <c>EvasiveFootworkV14</c> and records the second column beside the first, so
/// the two columns are produced by one instrument rather than two.
/// </para>
/// </remarks>
public sealed class EvasionCalibrationRun
{
    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    /// <summary>
    /// Receives xunit's per-test output sink. The report is written there and
    /// never to the console, matching the repository-wide rule that only the
    /// four <c>Program.cs</c> entry points may touch the console.
    /// </summary>
    public EvasionCalibrationRun(Xunit.Abstractions.ITestOutputHelper output) =>
        _output = output;

    /// <summary>
    /// Prints the twenty-seed table and the pooled row. This is a harness and
    /// not an assertion suite: it has no threshold to check, because producing
    /// the numbers those thresholds will be written against is the task it
    /// serves.
    /// </summary>
    [Fact]
    public void PrintTheEvasionCalibrationTable()
    {
        var movementPreset = Environment.GetEnvironmentVariable(
                "HUKBO_EVASION_MOVEMENT_PRESET") is { Length: > 0 } requested &&
            Enum.TryParse<MovementPresetId>(requested, out var parsed)
                ? parsed
                : MovementPresetId.CohortLateralSpreadV13;

        _output.WriteLine(EvasionCalibrationHarness.RunMatrix(movementPreset));
    }
}

#endif
