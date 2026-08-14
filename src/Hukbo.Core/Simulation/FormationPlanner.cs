using Hukbo.Core.Determinism;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;

namespace Hukbo.Core.Simulation;

/// <summary>
/// Plans the starting positions of one faction as a set of contingents, so
/// that a battle opens with several readable groups instead of one random
/// cloud. Both factions are planned from the same rules and the same map half,
/// so the two armies deploy in the same shape, mirrored across the vertical
/// centre line, and neither begins better massed or better spread than the
/// other.
/// </summary>
/// <remarks>
/// <para>
/// Historical position. The deep-past evidence reviewed in
/// <c>docs/research/battles/03-deep-past-formations-and-tactics.md</c> supports
/// only two things this planner relies on: that an army arrives as several
/// practical groups (people who share a boat, a leader, or a kin network), and
/// that spacing inside such a group is irregular. Regular files, ranks, fixed
/// frontage, depth doctrine, shield walls and named formations are explicitly
/// <em>not attested</em> and are not claimed here. Under
/// <c>MovementPresetId.BattlefieldRealismV10</c>, the deployment pipeline
/// deliberately builds two of these unattested shapes anyway — weapon-homogeneous
/// contingents and shield bearers held to their contingent's forward-most slots —
/// as a labelled Provisional reconstruction / gameplay model, never as a
/// historical claim; see
/// the battlefield realism design, section 2.2.
/// </para>
/// <para>
/// The lattice below is an engineering device for guaranteeing that no two
/// bodies overlap before the first tick. It is not a reconstruction of how
/// anyone stood, and it is not itself carried forward. Contingent
/// <em>membership</em> is: <see cref="PlanFactionDeployment"/> returns each
/// warrior's <c>ContingentId</c> alongside its spawn position, and that
/// membership is written once onto <c>AgentState</c> and consumed by the
/// movement preset for as long as the warrior is alive.
/// </para>
/// <para>
/// Determinism. Up to two draws per warrior, taken from the caller's stream in
/// ascending faction-local index, integer arithmetic throughout, and no
/// dependence on any unordered collection. The number of draws depends only on
/// the scenario, never on anything observed during placement.
/// </para>
/// </remarks>
internal static class FormationPlanner
{
    /// <summary>
    /// Upper bound on contingents per faction. A larger army makes each
    /// contingent bigger rather than adding more of them, which keeps the
    /// opening frame readable at any population.
    /// </summary>
    /// <remarks>
    /// Widened from <see langword="private"/> to <see langword="internal"/>
    /// by the persistent-contingent movement preset
    /// (the formation and movement realism design
    /// section 3.4), which uses this same constant for its
    /// <c>FactionId * MaximumContingents + ContingentId</c> slot arithmetic
    /// and its cross-contingent pair-scan bound rather than declaring a
    /// second <c>8</c> that could drift from this one.
    /// </remarks>
    internal const int MaximumContingents = 8;

    /// <summary>
    /// Preferred lattice spacing as a multiple of the body radius. Three body
    /// diameters leaves a group visibly airy; the fit rules below reduce it
    /// when the map cannot afford that much room.
    /// </summary>
    private const int PreferredSpacingRadiusMultiple = 6;

    /// <summary>
    /// Plans every warrior of one faction, in faction-local index order, inside
    /// the left half of the map. The caller uses the result directly for the
    /// left faction and mirrors it for the right one.
    /// </summary>
    /// <remarks>
    /// Called once per battle. Everything here is a pure function of the
    /// scenario, <paramref name="fieldedChiefCount"/>, and the caller's
    /// stream, so mirroring the single result is what makes the two armies
    /// equivalent. Planning each faction separately was measured and
    /// rejected: independent per-warrior jitter changed which bodies stood
    /// where without changing which faction won, so it bought nothing and
    /// cost the exact symmetry.
    /// </remarks>
    /// <param name="scenario">The scenario to plan a deployment for.</param>
    /// <param name="fieldedChiefCount">
    /// How many Datu-rank warriors the faction fielded, counted by the
    /// caller from its own already-resolved loadouts. This planner never
    /// resolves a loadout and never learns what a Datu is (contingent-shape
    /// design section 2) -- the count arrives pre-computed and is consulted
    /// only under <see cref="MovementPresetId.ContingentShapeV12"/> with no
    /// authored <see cref="Scenario.ContingentSizes"/>, where it decides the
    /// contingent count (tasks 6 and 7 of
    /// the contingent shape task plan). Every earlier preset, and
    /// every V12 scenario that authors its own sizes, ignores this parameter
    /// entirely, so a caller planning one of those may pass any value,
    /// including zero.
    /// </param>
    /// <param name="random">The caller's determinism stream.</param>
    internal static (int XRaw, int YRaw, int ContingentId)[] PlanFactionDeployment(
        Scenario scenario,
        int fieldedChiefCount,
        ref SplitMix64 random)
    {
        var radiusRaw = (long)scenario.BodyRadiusRaw;
        var mapWidthRaw = checked((long)scenario.MapWidth * FixedPoint.Scale);
        var mapHeightRaw = checked((long)scenario.MapHeight * FixedPoint.Scale);
        var warriorCount = scenario.AgentsPerFaction;

        var region = ResolveRegion(mapWidthRaw, mapHeightRaw, radiusRaw);
        var contingentSizes = ResolveContingentSizes(scenario, fieldedChiefCount);
        var laneSpan = (region.MaxY - region.MinY) / contingentSizes.Length;
        var largestContingent = contingentSizes[0];
        for (var index = 1; index < contingentSizes.Length; index++)
        {
            if (contingentSizes[index] > largestContingent)
            {
                largestContingent = contingentSizes[index];
            }
        }

        var lattice = ResolveLattice(largestContingent, laneSpan, region, radiusRaw);

        if (!lattice.Fits(laneSpan, region))
        {
            return PlanDenseBlock(warriorCount, contingentSizes.Length, lattice.Spacing, region);
        }

        var positions = new (int XRaw, int YRaw, int ContingentId)[warriorCount];
        var placedPerContingent = new int[contingentSizes.Length];
        var contingent = 0;

        // Warriors are dealt round-robin rather than in contiguous runs.
        // RosterCounts groups one weapon category into a contiguous run of
        // faction-local indices, so contiguous contingents would come out
        // weapon-homogeneous - a stronger claim than the evidence supports.
        // Under MovementPresetId.BattlefieldRealismV10, this constraint is
        // deliberately overridden downstream by CohortDeploymentAssignment,
        // which reassigns contingent membership so each contingent reads as
        // one weapon cohort - a labelled Provisional reconstruction / gameplay
        // model, not a historical claim; see
        // the battlefield realism design section 2.2.
        //
        // The cursor advances one contingent per warrior and wraps, skipping
        // any contingent that has already reached its own declared size. For
        // the square-root split every contingent's size already equals the
        // count plain `localIndex % contingentSizes.Length` dealing would
        // give it, so no contingent is ever full ahead of its own natural
        // turn and this is byte-identical to that simpler rule. Under
        // MovementPresetId.ContingentShapeV12's authored, unequal sizes that
        // is no longer true in general, and honouring each contingent's own
        // declared size - rather than an even split of the same count of
        // groups - is the point of authoring them.
        for (var localIndex = 0; localIndex < warriorCount; localIndex++)
        {
            while (placedPerContingent[contingent] >= contingentSizes[contingent])
            {
                contingent = (contingent + 1) % contingentSizes.Length;
            }

            var memberIndex = placedPerContingent[contingent]++;

            var (xRaw, yRaw) = PlaceMember(
                contingent,
                memberIndex,
                contingentSizes[contingent],
                lattice,
                laneSpan,
                region,
                mapWidthRaw,
                ref random);
            positions[localIndex] = (xRaw, yRaw, contingent);
            contingent = (contingent + 1) % contingentSizes.Length;
        }

        return positions;
    }

    /// <summary>
    /// The left half of the map, inset by one body radius so that a planned
    /// centre is a legal centre.
    /// </summary>
    /// <remarks>
    /// A map narrower than one body collapses the horizontal range onto a
    /// single legal coordinate. That coordinate is clamped into
    /// <c>[radius, mapWidth - radius]</c> rather than left at a quarter of the
    /// map width, because a centre below the radius would be clamped up by the
    /// caller's repair pass while its mirror was clamped down, and the two
    /// corrections do not cancel.
    /// </remarks>
    private static DeploymentRegion ResolveRegion(
        long mapWidthRaw,
        long mapHeightRaw,
        long radiusRaw)
    {
        var minX = radiusRaw;
        var maxX = (mapWidthRaw / 2) - radiusRaw;
        var minY = radiusRaw;
        var maxY = mapHeightRaw - radiusRaw;

        if (maxX < minX)
        {
            minX = Math.Clamp(
                mapWidthRaw / 4,
                Math.Min(radiusRaw, mapWidthRaw / 2),
                Math.Max(mapWidthRaw - radiusRaw, mapWidthRaw / 2));
            maxX = minX;
        }

        return new DeploymentRegion(minX, maxX, minY, maxY);
    }

    /// <summary>
    /// The faction's contingent sizes: the scenario's own authored array under
    /// <see cref="MovementPresetId.ContingentShapeV12"/> when one is present,
    /// the chief-derived count under V12 when one is not, or the square-root
    /// split every earlier preset uses.
    /// </summary>
    /// <remarks>
    /// Gated on preset identity first: any preset other than V12 takes the
    /// square-root path regardless of <paramref name="fieldedChiefCount"/> or
    /// of whether <c>ContingentSizes</c> happens to carry a value -- including
    /// a scenario built for <see cref="MovementPresetId.LastStandEngagementV11"/>
    /// that populates either field -- so this method, and therefore every
    /// preset from V1 through V11, stays byte-identical to its behaviour
    /// before tasks 6 and 7 of the contingent shape task plan.
    /// Only once the preset is V12 does the authored array, then the
    /// chief-derived count, come into play.
    /// </remarks>
    private static int[] ResolveContingentSizes(Scenario scenario, int fieldedChiefCount)
    {
        if (scenario.MovementPreset != MovementPresetId.ContingentShapeV12)
        {
            return ResolveContingentSizesBySquareRoot(scenario.AgentsPerFaction);
        }

        if (!scenario.ContingentSizes.IsDefaultOrEmpty)
        {
            return [.. scenario.ContingentSizes];
        }

        return ResolveContingentSizesByChiefCount(fieldedChiefCount, scenario.AgentsPerFaction);
    }

    /// <summary>
    /// Splits the faction into contingents of as near equal size as possible,
    /// the remainder going to the earliest contingents.
    /// </summary>
    private static int[] ResolveContingentSizesBySquareRoot(int warriorCount)
    {
        var contingentCount = Math.Clamp(
            IntegerSquareRoot(warriorCount) / 2,
            1,
            Math.Min(MaximumContingents, warriorCount));

        return SplitEvenly(contingentCount, warriorCount);
    }

    /// <summary>
    /// One contingent per fielded Datu-rank warrior, floored at one so a
    /// chiefless faction still forms a single body rather than producing zero
    /// contingents, and capped at <see cref="MaximumContingents"/> so a
    /// heavily led faction does not fragment past the lattice's own limit
    /// (contingent-shape design section 2). The chief is not privileged in
    /// placement: this method only decides how many contingents there are and
    /// how big each one is, the same even split
    /// <see cref="ResolveContingentSizesBySquareRoot"/> already uses, and the
    /// round-robin dealing loop in <see cref="PlanFactionDeployment"/> (never
    /// modified by this task) is what actually seats every warrior, chief or
    /// not, in turn.
    /// </summary>
    /// <remarks>
    /// The round-robin dealing loop deals contingent
    /// <c>localIndex % contingentSizes.Length</c> to every faction-local
    /// index whenever every contingent's declared size already equals what
    /// that plain modulus would give it -- true here because
    /// <see cref="SplitEvenly"/> only ever varies sizes by one, the same
    /// argument that already lets the square-root split use it (see the
    /// dealing loop's own remarks). One contingent per chief therefore
    /// guarantees every contingent receives at least one chief whenever
    /// <paramref name="fieldedChiefCount"/> is at least the resulting
    /// contingent count: the chiefs occupy faction-local indices whose
    /// residues mod the contingent count cover every value from zero up to
    /// one less than the chief count, and the contingent count here never
    /// exceeds the chief count except when the floor at one raises it above a
    /// chief count of zero.
    /// </remarks>
    private static int[] ResolveContingentSizesByChiefCount(
        int fieldedChiefCount,
        int warriorCount)
    {
        var contingentCount = Math.Clamp(
            fieldedChiefCount,
            1,
            Math.Min(MaximumContingents, warriorCount));

        return SplitEvenly(contingentCount, warriorCount);
    }

    /// <summary>
    /// Splits <paramref name="warriorCount"/> warriors into
    /// <paramref name="contingentCount"/> contingents of as near equal size as
    /// possible, the remainder going to the earliest contingents. Shared by
    /// both contingent-count rules so a lattice, a fit test, or a dealing
    /// argument that depends only on sizes differing by at most one never has
    /// to be proven twice.
    /// </summary>
    private static int[] SplitEvenly(int contingentCount, int warriorCount)
    {
        var sizes = new int[contingentCount];
        var baseSize = warriorCount / contingentCount;
        var remainder = warriorCount % contingentCount;

        for (var index = 0; index < contingentCount; index++)
        {
            sizes[index] = baseSize + (index < remainder ? 1 : 0);
        }

        return sizes;
    }

    /// <summary>
    /// The single lattice every contingent is laid out on: the largest spacing
    /// that still fits the tallest contingent inside its lane and the widest
    /// inside the region, never below <see cref="MinimumSeparation"/>.
    /// </summary>
    /// <remarks>
    /// The lane constraint is <c>spacing &lt;= laneSpan / rows</c> rather than
    /// <c>laneSpan / (rows - 1)</c> on purpose: the extra row of slack is the
    /// gap that keeps two neighbouring contingents from touching, and it is
    /// what makes the overlap argument in the design document hold across
    /// contingents as well as inside one. One lattice serves every contingent,
    /// so the fit test and the placement cannot disagree about how wide a
    /// contingent is.
    /// </remarks>
    private static Lattice ResolveLattice(
        int largestContingent,
        long laneSpan,
        DeploymentRegion region,
        long radiusRaw)
    {
        var columns = CeilingSquareRoot(largestContingent);
        var rows = (largestContingent + columns - 1) / columns;
        var spacing = checked(PreferredSpacingRadiusMultiple * radiusRaw);

        spacing = Math.Min(spacing, laneSpan / rows);
        spacing = Math.Min(spacing, (region.MaxX - region.MinX) / columns);
        spacing = Math.Max(spacing, MinimumSeparation(radiusRaw));

        return new Lattice(
            columns,
            rows,
            spacing,
            (spacing - MinimumSeparation(radiusRaw)) / 2);
    }

    /// <summary>
    /// The crowded-map fallback: one plain lattice covering the whole region,
    /// centred, unstaggered and unjittered. Separated contingents need room the
    /// map does not have here, and a body that overlaps is a worse outcome than
    /// a body in a dull arrangement. Spacing is at
    /// <see cref="MinimumSeparation"/> whenever this path runs, so the jitter
    /// would be zero regardless and the random stream is left untouched.
    /// Membership still deals round-robin the same way the lattice path does,
    /// because a contingent is a fact about who a warrior fights alongside, not
    /// about which placement path the map forced.
    /// </summary>
    /// <remarks>
    /// The block is shaped to the region: columns come from the available
    /// width, and if the resulting rows would run past the top or bottom of the
    /// region the block is widened instead. A population that fits neither way
    /// is denser than any arrangement can hold, and the caller's repair pass
    /// owns that case.
    /// </remarks>
    private static (int XRaw, int YRaw, int ContingentId)[] PlanDenseBlock(
        int warriorCount,
        int contingentCount,
        long spacing,
        DeploymentRegion region)
    {
        var columns = (int)Math.Clamp(
            ((region.MaxX - region.MinX) / spacing) + 1,
            1,
            warriorCount);
        var rows = (warriorCount + columns - 1) / columns;
        var availableRows = ((region.MaxY - region.MinY) / spacing) + 1;

        if (rows > availableRows)
        {
            columns = (int)Math.Clamp(
                (warriorCount + availableRows - 1) / availableRows,
                1,
                warriorCount);
            rows = (warriorCount + columns - 1) / columns;
        }

        var originX = ((region.MinX + region.MaxX) / 2) -
            (((columns - 1) * spacing) / 2);
        var originY = ((region.MinY + region.MaxY) / 2) -
            (((rows - 1) * spacing) / 2);
        var positions = new (int XRaw, int YRaw, int ContingentId)[warriorCount];

        for (var index = 0; index < warriorCount; index++)
        {
            positions[index] = (
                checked((int)(originX + ((index % columns) * spacing))),
                checked((int)(originY + ((index / columns) * spacing))),
                index % contingentCount);
        }

        return positions;
    }

    /// <summary>
    /// One raw unit beyond the body diameter. The spawn repair pass counts a
    /// tangent pair as contact, so a lattice that only reached tangency would
    /// send every body through the repair scan and break the mirror; one unit
    /// of clearance keeps that pass a no-op.
    /// </summary>
    private static long MinimumSeparation(long radiusRaw) => (2 * radiusRaw) + 1;

    /// <summary>
    /// Places one member on its contingent's staggered lattice and applies the
    /// bounded jitter that keeps the group irregular. Two members are always at
    /// least one <see cref="Lattice.Spacing"/> apart on one axis, and the
    /// jitter can erode that by at most twice
    /// <see cref="Lattice.JitterLimit"/>, so the closest two bodies can come is
    /// one raw unit clear of tangency.
    /// </summary>
    private static (int XRaw, int YRaw) PlaceMember(
        int contingent,
        int memberIndex,
        int contingentSize,
        Lattice lattice,
        long laneSpan,
        DeploymentRegion region,
        long mapWidthRaw,
        ref SplitMix64 random)
    {
        var rows = (contingentSize + lattice.Columns - 1) / lattice.Columns;
        var row = memberIndex / lattice.Columns;
        var column = memberIndex % lattice.Columns;

        var stagger = (row & 1) == 1 ? lattice.Spacing / 2 : 0;
        var offsetX = (column * lattice.Spacing) -
            (((lattice.Columns - 1) * lattice.Spacing) / 2) +
            stagger;
        var offsetY = (row * lattice.Spacing) -
            (((rows - 1) * lattice.Spacing) / 2);

        var anchorX = ResolveAnchorX(contingent, lattice, region, mapWidthRaw);
        var anchorY = region.MinY + (laneSpan * contingent) + (laneSpan / 2);

        return (
            checked((int)(anchorX + offsetX + NextJitter(lattice.JitterLimit, ref random))),
            checked((int)(anchorY + offsetY + NextJitter(lattice.JitterLimit, ref random))));
    }

    /// <summary>
    /// Centres a contingent in depth. Even-numbered contingents deploy forward,
    /// towards the enemy half, and odd-numbered ones deploy back, so the front
    /// edge of an army is ragged rather than a single straight line. The
    /// preferred band keeps the opening separation between the two armies close
    /// to what the earlier random band produced.
    /// </summary>
    /// <remarks>
    /// The anchor is pulled back inside the region whenever the preferred depth
    /// would push a member past an edge. <see cref="Lattice.Fits"/> has already
    /// established that the contingent, jitter included, is narrower than the
    /// region, so the clamp always has somewhere to put it.
    /// </remarks>
    private static long ResolveAnchorX(
        int contingent,
        Lattice lattice,
        DeploymentRegion region,
        long mapWidthRaw)
    {
        var bandMinX = Math.Clamp(mapWidthRaw / 8, region.MinX, region.MaxX);
        var bandMaxX = Math.Clamp((mapWidthRaw * 3) / 8, region.MinX, region.MaxX);
        var bandDepth = bandMaxX - bandMinX;
        var isForward = (contingent & 1) == 0;
        var anchorX = bandMinX + ((bandDepth * (isForward ? 5 : 2)) / 8);

        return Math.Clamp(
            anchorX,
            region.MinX + lattice.RequiredHalfWidth,
            region.MaxX - lattice.RequiredHalfWidth);
    }

    private static long NextJitter(long jitterLimit, ref SplitMix64 random)
    {
        if (jitterLimit <= 0)
        {
            return 0;
        }

        var range = checked((int)((2 * jitterLimit) + 1));
        return random.NextInt(range) - jitterLimit;
    }

    /// <summary>
    /// Floor of the square root, by digit-by-digit binary restoring division.
    /// A local integer routine rather than <see cref="Math.Sqrt(double)"/>:
    /// no floating-point value may reach a hashed position.
    /// </summary>
    private static int IntegerSquareRoot(int value)
    {
        if (value <= 0)
        {
            return 0;
        }

        var root = 0;
        var remainder = value;
        var bit = 1 << 30;

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

        return root;
    }

    /// <summary>Smallest integer whose square is at least <paramref name="value"/>.</summary>
    private static int CeilingSquareRoot(int value)
    {
        var root = IntegerSquareRoot(value);
        return root * root >= value ? Math.Max(root, 1) : root + 1;
    }

    /// <summary>
    /// The inclusive raw-coordinate rectangle a faction may deploy inside,
    /// already inset by one body radius on every side.
    /// </summary>
    private readonly record struct DeploymentRegion(
        long MinX,
        long MaxX,
        long MinY,
        long MaxY);

    /// <summary>
    /// The shared grid every contingent is laid out on. One value, computed
    /// once: the fit test and the placement must agree about how much room a
    /// contingent needs, and the only way to guarantee that is to ask the same
    /// object.
    /// </summary>
    private readonly record struct Lattice(
        int Columns,
        int Rows,
        long Spacing,
        long JitterLimit)
    {
        /// <summary>
        /// Half the width a contingent consumes, jitter included: half the
        /// column span, plus the half-cell offset applied to odd rows, plus the
        /// jitter reach.
        /// </summary>
        internal long RequiredHalfWidth =>
            (((Columns - 1) * Spacing) / 2) + (Spacing / 2) + JitterLimit;

        /// <summary>
        /// True when a contingent fits its lane with a full row of clearance
        /// above and below, and fits the region horizontally. This fails only
        /// when a crowded map forced the spacing up to
        /// <see cref="MinimumSeparation"/> against the fit rules, and the
        /// caller then falls back to a dense block.
        /// </summary>
        internal bool Fits(long laneSpan, DeploymentRegion region) =>
            Rows * Spacing <= laneSpan &&
            2 * RequiredHalfWidth <= region.MaxX - region.MinX;
    }
}
