using System.Globalization;
using System.Text;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;

// Measures two things a spectator reported and nobody has a number for, both
// from the 2026-07-28 manual pass recorded in docs/development/testing.md:
//
//   Row 104 (FAIL) - "A mid-battle contingent gather sometimes read as a line
//   rather than as a ragged clump." This tool answers it with the principal-
//   axis aspect ratio of each contingent's living members, plus the angle that
//   major axis makes with the world axes and with the contingent's own
//   direction of advance. A ragged clump sits near an aspect of 1; a line does
//   not. The two angles separate the two competing explanations: a cloud that
//   elongates along a world axis regardless of where the contingent is heading
//   points at the axis-aligned bias square, while one that elongates across
//   the direction of advance points at something in the approach itself.
//
//   Row 114 (FAIL) - "Gathering was seen only near the start of the advance.
//   It was not seen again once groups were already fighting." This tool
//   answers it by tallying how many ticks each contingent spends in each
//   ContingentState, how many separate Hold episodes open before and after the
//   contingent first reaches ContingentState.Close, and - for every tick a
//   contingent spends in Advance - which of the four possible reasons denied
//   it cohesion.
//
// Runs real BattleSimulation instances. Read-only against Hukbo.Core, and
// writes nothing to a repository file: the optional CSV goes to a path you
// name.
//
// This harness reconstructs three things the simulation computes internally,
// because the members that hold them are internal to Hukbo.Core:
//
//   1. The contingent leader - the lowest living EntityId among the
//      contingent's members, matching
//      MovementRules.ScanContingentLeadersAndLivingCounts.
//   2. The cohesion trail base - matching BattleSimulation.ComputeRallyDirection
//      and BattleSimulation.ComputeRallyTrailBase
//      (src/Hukbo.Core/Simulation/BattleSimulation.cs:1378-1429), including the
//      integer square root and the leader-position fallback when the leader has
//      no target.
//   3. The hysteresis-banded gathering test of transition rule 5
//      (src/Hukbo.Core/Movement/MovementRules.cs:231-235), including its Int128
//      widening.
//
// Everything else - the jitter radius, the trail distance, the map-edge gate,
// and the cross-contingent overlap gate - is called directly on the public
// FormationRules, so those four cannot drift from the simulation. If any of the
// three reconstructions above changes in Hukbo.Core, this file has to change
// with it or its attribution numbers become fiction.
//
// Floating point appears here and only here, in the eigenvalue and angle
// arithmetic of the shape metric. This is a measurement tool that never feeds
// the simulation, so the determinism contract in SIMULATION-GAME-STANDARDS.md
// section 4 does not reach it. Every accumulation that could overflow is done
// in long or Int128 first, and only the final ratio is taken in double.

// Mirrors FormationPlanner.MaximumContingents, which is internal. A view
// carrying a ContingentId at or above this would mean the two have diverged,
// so the scan below throws rather than silently mis-slotting an agent.
const int MaximumContingents = 8;
const int SlotCount = 2 * MaximumContingents;

// A contingent smaller than this has no shape worth measuring - three or four
// bodies are a line, a triangle, or a clump depending on nothing but chance -
// so shape samples are taken only at or above it. State occupancy and denial
// attribution are tallied for every contingent regardless of size.
const int MinimumMembersForShape = 6;

// How long after a leader change a shape sample still counts as "shortly
// after a leader change" for the re-aim comparison.
const int LeaderChangeWindowTicks = 60;

var ticks = args.Length > 0 ? int.Parse(args[0], CultureInfo.InvariantCulture) : 10_000;
var agentCount = args.Length > 1 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 200;
var seedCount = args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 5;
var preset = args.Length > 3
    ? Enum.Parse<MovementPresetId>(args[3])
    : MovementPresetId.PersistentContingentsV2;
var csvPath = args.Length > 4 ? args[4] : null;

// The same binary digit-by-digit extraction BattleSimulation, CollisionResolver,
// and FormationRules each carry their own copy of: exact for every non-negative
// long, and needing no floating point.
static long IntegerSquareRoot(long value)
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

static int SaturateToInt32(long value) => value switch
{
    > int.MaxValue => int.MaxValue,
    < int.MinValue => int.MinValue,
    _ => (int)value,
};

static long SquaredDistance(AgentView left, AgentView right)
{
    var deltaX = (long)left.XRaw - right.XRaw;
    var deltaY = (long)left.YRaw - right.YRaw;
    return checked((deltaX * deltaX) + (deltaY * deltaY));
}

// Folds an undirected axis angle, in radians, onto [0, 90] degrees. A major
// axis has no head and no tail, so an axis at 175 degrees and one at 355 are
// the same axis and must not average out to something in between.
static double FoldToRightAngle(double radians)
{
    var degrees = radians * 180.0 / Math.PI;
    degrees = Math.Abs(degrees % 180.0);
    return degrees > 90.0 ? 180.0 - degrees : degrees;
}

static double Percentile(List<double> sorted, double fraction)
{
    if (sorted.Count == 0)
    {
        return double.NaN;
    }

    var index = (int)(fraction * (sorted.Count - 1));
    return sorted[index];
}

static double Mean(List<double> values)
{
    if (values.Count == 0)
    {
        return double.NaN;
    }

    var total = 0.0;
    foreach (var value in values)
    {
        total += value;
    }

    return total / values.Count;
}

var stateTicks = new Dictionary<ContingentState, long>();
var denialTicks = new Dictionary<string, long>();
var shapeByState = new Dictionary<ContingentState, List<ShapeSample>>();
var aspectAfterLeaderChange = new List<double>();
var aspectAwayFromLeaderChange = new List<double>();

// Row 114's core counters. A "Hold episode" is one transition into
// ContingentState.Hold; "before" and "after" are relative to the first tick
// that contingent reached ContingentState.Close in that battle.
long holdTicksBeforeFirstClose = 0;
long holdTicksAfterFirstClose = 0;
var holdEpisodesBeforeFirstClose = 0;
var holdEpisodesAfterFirstClose = 0;
var contingentBattlesObserved = 0;
var contingentBattlesReachingClose = 0;
var contingentBattlesHoldingAfterClose = 0;
var memberStateDisagreements = 0;

var csv = csvPath is null ? null : new StringBuilder();
csv?.AppendLine(
    "seed,tick,faction,contingent,state,living,leaderEntityId,leaderChangedTick," +
    "spreadSquared,cohesionRadiusSquared,squareFitsMap,squareOverlapsAnother," +
    "denialReason,aspect,majorAxisToWorldAxisDegrees,majorAxisToAdvanceDegrees");

var ruleset = MovementPresetRegistry.Get(preset);

for (var seedIndex = 0; seedIndex < seedCount; seedIndex++)
{
    var seed = (ulong)(seedIndex + 1);
    var scenario = Scenario.CreateDefault(seed, agentCount) with
    {
        TickLimit = ticks,
        MovementPreset = preset,
    };

    scenario.Validate();
    var simulation = BattleSimulation.Create(scenario);

    var bodyRadiusRaw = scenario.BodyRadiusRaw;
    var cohesionRadiusRaw = (long)ruleset.CohesionRadiusMultiplier * bodyRadiusRaw;
    var cohesionRadiusSquared = checked(cohesionRadiusRaw * cohesionRadiusRaw);
    var mapWidthRaw = checked(scenario.MapWidth * FixedPoint.Scale);
    var mapHeightRaw = checked(scenario.MapHeight * FixedPoint.Scale);

    // The view array is indexed by a stable agent index for the whole battle
    // (BattleSimulation.cs:2303-2306 rewrites each slot in place), so this
    // lookup is built once per seed rather than once per tick.
    var views = simulation.Agents;
    var indexByEntityId = new Dictionary<ulong, int>(views.Count);
    for (var index = 0; index < views.Count; index++)
    {
        indexByEntityId[views[index].EntityId] = index;
    }

    var living = new int[SlotCount];
    var leaderEntityIds = new ulong[SlotCount];
    var leaderIndexes = new int[SlotCount];
    var sumXRaw = new long[SlotCount];
    var sumYRaw = new long[SlotCount];
    var spreadSquared = new long[SlotCount];
    var covarianceXx = new long[SlotCount];
    var covarianceYy = new long[SlotCount];
    var covarianceXy = new long[SlotCount];
    var trailBaseXRaw = new int[SlotCount];
    var trailBaseYRaw = new int[SlotCount];
    var marginRaw = new int[SlotCount];
    var advanceDeltaXRaw = new long[SlotCount];
    var advanceDeltaYRaw = new long[SlotCount];
    var hasAdvanceDirection = new bool[SlotCount];
    var squareFitsMap = new bool[SlotCount];
    var squareOverlapsAnother = new bool[SlotCount];
    var states = new ContingentState[SlotCount];

    var previousStates = new ContingentState[SlotCount];
    var previousLeaderEntityIds = new ulong[SlotCount];
    var lastLeaderChangeTick = new long[SlotCount];
    var firstCloseTick = new long[SlotCount];
    var everLived = new bool[SlotCount];
    var heldAfterClose = new bool[SlotCount];
    Array.Fill(lastLeaderChangeTick, long.MinValue);
    Array.Fill(firstCloseTick, -1);

    while (simulation.Outcome == BattleOutcome.Ongoing)
    {
        simulation.AdvanceOneTick();
        var tick = simulation.Tick;

        Array.Clear(living);
        Array.Clear(leaderEntityIds);
        Array.Fill(leaderIndexes, -1);
        Array.Clear(sumXRaw);
        Array.Clear(sumYRaw);
        Array.Clear(spreadSquared);
        Array.Clear(covarianceXx);
        Array.Clear(covarianceYy);
        Array.Clear(covarianceXy);
        Array.Clear(hasAdvanceDirection);
        Array.Clear(squareFitsMap);
        Array.Clear(squareOverlapsAnother);
        Array.Fill(states, ContingentState.None);

        // Pass one: living headcount, leader, and centroid sums per slot.
        for (var index = 0; index < views.Count; index++)
        {
            var view = views[index];
            if (!view.IsAlive)
            {
                continue;
            }

            if (view.ContingentId is < 0 or >= MaximumContingents)
            {
                throw new InvalidOperationException(
                    $"Agent {view.EntityId} carries ContingentId {view.ContingentId}, " +
                    $"which this tool's MaximumContingents of {MaximumContingents} " +
                    "no longer matches FormationPlanner.MaximumContingents.");
            }

            var slot = (view.FactionId * MaximumContingents) + view.ContingentId;
            living[slot]++;
            sumXRaw[slot] += view.XRaw;
            sumYRaw[slot] += view.YRaw;

            if (leaderEntityIds[slot] == 0 || view.EntityId < leaderEntityIds[slot])
            {
                leaderEntityIds[slot] = view.EntityId;
                leaderIndexes[slot] = index;
            }

            if (states[slot] == ContingentState.None)
            {
                states[slot] = view.ContingentState;
            }
            else if (states[slot] != view.ContingentState)
            {
                memberStateDisagreements++;
            }
        }

        // Pass two: spread against the leader and the covariance of the living
        // members about their own centroid.
        for (var index = 0; index < views.Count; index++)
        {
            var view = views[index];
            if (!view.IsAlive)
            {
                continue;
            }

            var slot = (view.FactionId * MaximumContingents) + view.ContingentId;
            var leaderIndex = leaderIndexes[slot];
            if (leaderIndex >= 0 && view.EntityId != leaderEntityIds[slot])
            {
                var memberSquared = SquaredDistance(view, views[leaderIndex]);
                if (memberSquared > spreadSquared[slot])
                {
                    spreadSquared[slot] = memberSquared;
                }
            }

            var deviationX = view.XRaw - (sumXRaw[slot] / living[slot]);
            var deviationY = view.YRaw - (sumYRaw[slot] / living[slot]);
            covarianceXx[slot] += deviationX * deviationX;
            covarianceYy[slot] += deviationY * deviationY;
            covarianceXy[slot] += deviationX * deviationY;
        }

        // Gate 5, the map-edge test, plus the trail base and the direction of
        // advance both the gate and the shape metric need.
        for (var slot = 0; slot < SlotCount; slot++)
        {
            if (living[slot] == 0)
            {
                continue;
            }

            everLived[slot] = true;

            var leader = views[leaderIndexes[slot]];
            var jitterRaw = FormationRules.ComputeContingentJitterRaw(
                bodyRadiusRaw,
                living[slot]);
            var trailRaw = FormationRules.ComputeContingentTrailRaw(
                bodyRadiusRaw,
                jitterRaw);

            long deltaXRaw = 0;
            long deltaYRaw = 0;
            long distanceRaw = 0;
            if (leader.TargetEntityId is { } targetId &&
                indexByEntityId.TryGetValue(targetId, out var targetIndex))
            {
                var target = views[targetIndex];
                deltaXRaw = (long)target.XRaw - leader.XRaw;
                deltaYRaw = (long)target.YRaw - leader.YRaw;
                distanceRaw = IntegerSquareRoot(
                    checked((deltaXRaw * deltaXRaw) + (deltaYRaw * deltaYRaw)));
            }

            if (distanceRaw == 0)
            {
                trailBaseXRaw[slot] = leader.XRaw;
                trailBaseYRaw[slot] = leader.YRaw;
                hasAdvanceDirection[slot] = false;
            }
            else
            {
                trailBaseXRaw[slot] = SaturateToInt32(checked(
                    leader.XRaw - (deltaXRaw * trailRaw / distanceRaw)));
                trailBaseYRaw[slot] = SaturateToInt32(checked(
                    leader.YRaw - (deltaYRaw * trailRaw / distanceRaw)));
                advanceDeltaXRaw[slot] = deltaXRaw;
                advanceDeltaYRaw[slot] = deltaYRaw;
                hasAdvanceDirection[slot] = true;
            }

            marginRaw[slot] = checked(jitterRaw + bodyRadiusRaw);
            squareFitsMap[slot] = FormationRules.IsCohesionSquareWithinBounds(
                trailBaseXRaw[slot],
                trailBaseYRaw[slot],
                jitterRaw,
                bodyRadiusRaw,
                mapWidthRaw,
                mapHeightRaw);
        }

        // Gate 6, the pairwise same-faction overlap test. The predicate is
        // symmetric, so both slots of an overlapping pair are marked.
        for (var faction = 0; faction < 2; faction++)
        {
            var baseSlot = faction * MaximumContingents;
            for (var outer = 0; outer < MaximumContingents; outer++)
            {
                var outerSlot = baseSlot + outer;
                if (living[outerSlot] == 0)
                {
                    continue;
                }

                for (var inner = outer + 1; inner < MaximumContingents; inner++)
                {
                    var innerSlot = baseSlot + inner;
                    if (living[innerSlot] == 0)
                    {
                        continue;
                    }

                    if (FormationRules.DoCohesionSquaresOverlap(
                        trailBaseXRaw[outerSlot],
                        trailBaseYRaw[outerSlot],
                        marginRaw[outerSlot],
                        trailBaseXRaw[innerSlot],
                        trailBaseYRaw[innerSlot],
                        marginRaw[innerSlot]))
                    {
                        squareOverlapsAnother[outerSlot] = true;
                        squareOverlapsAnother[innerSlot] = true;
                    }
                }
            }
        }

        // Tally.
        for (var slot = 0; slot < SlotCount; slot++)
        {
            if (living[slot] == 0)
            {
                previousStates[slot] = states[slot];
                previousLeaderEntityIds[slot] = 0;
                continue;
            }

            var state = states[slot];
            stateTicks[state] = stateTicks.GetValueOrDefault(state) + 1;

            var leaderChanged = previousLeaderEntityIds[slot] != 0 &&
                previousLeaderEntityIds[slot] != leaderEntityIds[slot];
            if (leaderChanged)
            {
                lastLeaderChangeTick[slot] = tick;
            }

            if (state == ContingentState.Close && firstCloseTick[slot] < 0)
            {
                firstCloseTick[slot] = tick;
            }

            var afterFirstClose = firstCloseTick[slot] >= 0;
            if (state == ContingentState.Hold)
            {
                if (afterFirstClose)
                {
                    holdTicksAfterFirstClose++;
                    heldAfterClose[slot] = true;
                }
                else
                {
                    holdTicksBeforeFirstClose++;
                }

                if (previousStates[slot] != ContingentState.Hold)
                {
                    if (afterFirstClose)
                    {
                        holdEpisodesAfterFirstClose++;
                    }
                    else
                    {
                        holdEpisodesBeforeFirstClose++;
                    }
                }
            }

            // Rule 5's hysteresis-banded gathering test, evaluated against the
            // state this tool saw on the previous tick, exactly as
            // MovementRules.ResolveContingentState evaluates it against the
            // state the simulation carried in.
            var qualifiesForHold = previousStates[slot] == ContingentState.Hold
                ? (Int128)spreadSquared[slot] * 16 > (Int128)9 * cohesionRadiusSquared
                : spreadSquared[slot] > cohesionRadiusSquared;

            // Attribution follows the transition rules' own priority order, so
            // a tick on which both a geometric gate denied and the duty-cycle
            // window was shut is attributed to the gate. "window-shut" is
            // therefore exact but conservative: it is only ever claimed when
            // both gates passed and the contingent would otherwise have been
            // told to Hold.
            var denialReason = state switch
            {
                ContingentState.Hold => "none-cohesion-granted",
                ContingentState.Close => "close-enemy-within-close-radius",
                ContingentState.Break => "break-attrition",
                ContingentState.Advance when !squareFitsMap[slot] => "gate5-map-edge",
                ContingentState.Advance when squareOverlapsAnother[slot] => "gate6-square-overlap",
                ContingentState.Advance when qualifiesForHold => "window-shut",
                ContingentState.Advance => "already-gathered",
                _ => "none-state",
            };

            denialTicks[denialReason] = denialTicks.GetValueOrDefault(denialReason) + 1;

            double aspect = double.NaN;
            double worldAxisDegrees = double.NaN;
            double advanceDegrees = double.NaN;

            if (living[slot] >= MinimumMembersForShape)
            {
                // Eigenvalues of the symmetric 2x2 covariance matrix
                // [[xx, xy], [xy, yy]], then the aspect ratio of the standard
                // deviations along the two principal axes.
                var xx = (double)covarianceXx[slot] / living[slot];
                var yy = (double)covarianceYy[slot] / living[slot];
                var xy = (double)covarianceXy[slot] / living[slot];
                var half = (xx + yy) / 2.0;
                var gap = Math.Sqrt((((xx - yy) / 2.0) * ((xx - yy) / 2.0)) + (xy * xy));
                var major = half + gap;
                var minor = half - gap;

                if (major > 0.0)
                {
                    // A degenerate minor axis means every member is collinear
                    // to the precision of a raw unit, which is the extreme of
                    // the very defect row 104 reports rather than a division to
                    // be silently skipped; it is reported as the sentinel below
                    // so it still shows up in the maximum and the histogram.
                    aspect = minor <= 0.0
                        ? double.PositiveInfinity
                        : Math.Sqrt(major / minor);
                    worldAxisDegrees = FoldToRightAngle(0.5 * Math.Atan2(2.0 * xy, xx - yy));

                    if (hasAdvanceDirection[slot])
                    {
                        var majorAxisRadians = 0.5 * Math.Atan2(2.0 * xy, xx - yy);
                        var advanceRadians = Math.Atan2(
                            advanceDeltaYRaw[slot],
                            advanceDeltaXRaw[slot]);
                        advanceDegrees = FoldToRightAngle(majorAxisRadians - advanceRadians);
                    }
                }

                if (!double.IsNaN(aspect))
                {
                    if (!shapeByState.TryGetValue(state, out var samples))
                    {
                        samples = [];
                        shapeByState[state] = samples;
                    }

                    samples.Add(new ShapeSample(aspect, worldAxisDegrees, advanceDegrees));

                    if (state is ContingentState.Hold or ContingentState.Advance)
                    {
                        var withinWindow = lastLeaderChangeTick[slot] != long.MinValue &&
                            tick - lastLeaderChangeTick[slot] <= LeaderChangeWindowTicks;
                        if (withinWindow)
                        {
                            aspectAfterLeaderChange.Add(aspect);
                        }
                        else
                        {
                            aspectAwayFromLeaderChange.Add(aspect);
                        }
                    }
                }
            }

            csv?.Append(CultureInfo.InvariantCulture, $"{seed},{tick},")
                .Append(CultureInfo.InvariantCulture, $"{slot / MaximumContingents},")
                .Append(CultureInfo.InvariantCulture, $"{slot % MaximumContingents},")
                .Append(CultureInfo.InvariantCulture, $"{state},{living[slot]},")
                .Append(CultureInfo.InvariantCulture, $"{leaderEntityIds[slot]},")
                .Append(CultureInfo.InvariantCulture, $"{lastLeaderChangeTick[slot]},")
                .Append(CultureInfo.InvariantCulture, $"{spreadSquared[slot]},")
                .Append(CultureInfo.InvariantCulture, $"{cohesionRadiusSquared},")
                .Append(CultureInfo.InvariantCulture, $"{squareFitsMap[slot]},")
                .Append(CultureInfo.InvariantCulture, $"{squareOverlapsAnother[slot]},")
                .Append(CultureInfo.InvariantCulture, $"{denialReason},")
                .Append(CultureInfo.InvariantCulture, $"{aspect:F4},")
                .Append(CultureInfo.InvariantCulture, $"{worldAxisDegrees:F2},")
                .AppendLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{advanceDegrees:F2}"));

            previousStates[slot] = state;
            previousLeaderEntityIds[slot] = leaderEntityIds[slot];
        }
    }

    for (var slot = 0; slot < SlotCount; slot++)
    {
        if (!everLived[slot])
        {
            continue;
        }

        contingentBattlesObserved++;
        if (firstCloseTick[slot] >= 0)
        {
            contingentBattlesReachingClose++;
        }

        if (heldAfterClose[slot])
        {
            contingentBattlesHoldingAfterClose++;
        }
    }

    Console.WriteLine(
        $"seed {seed}: outcome={simulation.Outcome} ticks={simulation.Tick}");
}

Console.WriteLine();
Console.WriteLine(
    $"=== preset={preset} agents={agentCount} seeds=1..{seedCount} tickLimit={ticks} ===");
Console.WriteLine(
    $"cohesionRadiusMultiplier={ruleset.CohesionRadiusMultiplier} " +
    $"closeRadiusMultiplier={ruleset.CloseRadiusMultiplier} " +
    $"cohesionCycleTicks={ruleset.CohesionCycleTicks} " +
    $"cohesionDutyTicks={ruleset.CohesionDutyTicks} " +
    $"minimumCohesiveMembers={ruleset.MinimumCohesiveMembers}");
Console.WriteLine();

if (memberStateDisagreements > 0)
{
    Console.WriteLine(
        $"WARNING: {memberStateDisagreements} agent-ticks carried a ContingentState " +
        "differing from another living member of the same contingent. Every number " +
        "below that keys on state is unreliable until that is explained.");
    Console.WriteLine();
}

Console.WriteLine("--- Contingent-state occupancy (row 114) ---");
Console.WriteLine("state       contingentTicks   share");
var totalStateTicks = 0L;
foreach (var count in stateTicks.Values)
{
    totalStateTicks += count;
}

foreach (var state in Enum.GetValues<ContingentState>())
{
    var count = stateTicks.GetValueOrDefault(state);
    var share = totalStateTicks == 0 ? 0.0 : (double)count / totalStateTicks;
    Console.WriteLine($"{state,-10}  {count,15}   {share,6:P2}");
}

Console.WriteLine();
Console.WriteLine("--- Why cohesion was denied, per contingent-tick (row 114) ---");
Console.WriteLine("reason                            contingentTicks   share");
foreach (var (reason, count) in denialTicks.OrderByDescending(pair => pair.Value))
{
    var share = totalStateTicks == 0 ? 0.0 : (double)count / totalStateTicks;
    Console.WriteLine($"{reason,-32}  {count,15}   {share,6:P2}");
}

Console.WriteLine();
Console.WriteLine("--- Gathering before and after first contact (row 114) ---");
Console.WriteLine(
    $"contingent-battles observed          : {contingentBattlesObserved}");
Console.WriteLine(
    $"  of those, ever reached Close       : {contingentBattlesReachingClose}");
Console.WriteLine(
    $"  of those, ever Held again after    : {contingentBattlesHoldingAfterClose}");
Console.WriteLine(
    $"Hold ticks before first Close        : {holdTicksBeforeFirstClose}");
Console.WriteLine(
    $"Hold ticks after first Close         : {holdTicksAfterFirstClose}");
Console.WriteLine(
    $"Hold episodes before first Close     : {holdEpisodesBeforeFirstClose}");
Console.WriteLine(
    $"Hold episodes after first Close      : {holdEpisodesAfterFirstClose}");
Console.WriteLine();

Console.WriteLine("--- Gathered shape (row 104) ---");
Console.WriteLine(
    $"Principal-axis aspect ratio of living members, contingents of " +
    $"{MinimumMembersForShape} or more. 1.00 is a disc; a rank or a file is well above it.");
Console.WriteLine(
    "state       samples   meanAspect   p50     p90     p99     max      " +
    "meanDegToWorldAxis   meanDegToAdvance");
foreach (var state in Enum.GetValues<ContingentState>())
{
    if (!shapeByState.TryGetValue(state, out var samples) || samples.Count == 0)
    {
        continue;
    }

    var aspects = new List<double>(samples.Count);
    var worldAngles = new List<double>(samples.Count);
    var advanceAngles = new List<double>(samples.Count);
    foreach (var sample in samples)
    {
        aspects.Add(sample.Aspect);
        if (!double.IsNaN(sample.WorldAxisDegrees))
        {
            worldAngles.Add(sample.WorldAxisDegrees);
        }

        if (!double.IsNaN(sample.AdvanceDegrees))
        {
            advanceAngles.Add(sample.AdvanceDegrees);
        }
    }

    aspects.Sort();
    Console.WriteLine(
        $"{state,-10}  {samples.Count,7}   {Mean(aspects),10:F3}   " +
        $"{Percentile(aspects, 0.50),5:F2}   {Percentile(aspects, 0.90),5:F2}   " +
        $"{Percentile(aspects, 0.99),5:F2}   {aspects[^1],6:F2}   " +
        $"{Mean(worldAngles),18:F2}   {Mean(advanceAngles),16:F2}");
}

Console.WriteLine();
Console.WriteLine("Aspect histogram, Hold state only:");
if (shapeByState.TryGetValue(ContingentState.Hold, out var holdSamples))
{
    double[] edges = [1.5, 2.0, 3.0, 5.0, double.PositiveInfinity];
    string[] labels = ["[1.0, 1.5)", "[1.5, 2.0)", "[2.0, 3.0)", "[3.0, 5.0)", "[5.0, inf]"];
    var buckets = new int[edges.Length];
    foreach (var sample in holdSamples)
    {
        for (var bucket = 0; bucket < edges.Length; bucket++)
        {
            if (sample.Aspect < edges[bucket] || bucket == edges.Length - 1)
            {
                buckets[bucket]++;
                break;
            }
        }
    }

    for (var bucket = 0; bucket < buckets.Length; bucket++)
    {
        var share = holdSamples.Count == 0 ? 0.0 : (double)buckets[bucket] / holdSamples.Count;
        Console.WriteLine($"{labels[bucket],-12}  {buckets[bucket],8}   {share,6:P2}");
    }
}
else
{
    Console.WriteLine("no Hold samples");
}

Console.WriteLine();
Console.WriteLine("--- Shape shortly after a leader change (row 104) ---");
Console.WriteLine(
    $"Hold and Advance samples, split on whether the contingent's leader changed " +
    $"within the last {LeaderChangeWindowTicks} ticks.");
Console.WriteLine(
    $"within {LeaderChangeWindowTicks} ticks of a leader change: " +
    $"samples={aspectAfterLeaderChange.Count} meanAspect={Mean(aspectAfterLeaderChange):F3}");
Console.WriteLine(
    $"otherwise                              : " +
    $"samples={aspectAwayFromLeaderChange.Count} meanAspect={Mean(aspectAwayFromLeaderChange):F3}");

if (csv is not null && csvPath is not null)
{
    File.WriteAllText(csvPath, csv.ToString());
    Console.WriteLine();
    Console.WriteLine($"per-tick rows written to {csvPath}");
}

record ShapeSample(double Aspect, double WorldAxisDegrees, double AdvanceDegrees);
