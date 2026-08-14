using Sandata.Core.Navigation;

namespace Sandata.Core.Combat;

/// <summary>
/// Design section 9's "one conditional that generates the whole game": an
/// operator's weapon is forced lowered while standing within
/// <c>SandataRuleset.LoweredWallDistanceWu</c> of any wall segment, or inside
/// a closed door's cell, unless the carried weapon is
/// <c>FirearmDefinition.ExemptFromLoweredRule</c> (pistols, per design
/// section 9). Evaluated in tick stage 11 against the position committed in
/// stage 10 (design section 5's fourteen-stage table), using the wall bucket
/// index for the proximity query and the <see cref="NavCellFlags.Door"/> tag
/// for the doorway test — exactly the two data structures design section 9
/// names.
/// </summary>
/// <remarks>
/// <para>
/// This type computes only the boolean condition; it holds no state of its
/// own and performs no mutation. The result is meant to feed directly into
/// <c>Sandata.Core.Weapons.WeaponChain.Advance</c>'s <c>forceLowered</c>
/// parameter, whose own remarks describe how a forced lower unconditionally
/// cancels an in-progress shot and, once <c>forceLowered</c> goes false again
/// and a raise is requested, re-imposes the full <c>ReadyTicks</c> wait
/// before the chain can reach <c>Aiming</c>. Emitting the authoritative
/// transition event design section 9 requires, and folding
/// <c>OperatorState.WeaponLowered</c> into the state hash, both belong to
/// whichever future task wires this rule into the tick pipeline and the
/// hasher — this type is the pure predicate those callers evaluate against.
/// </para>
/// <para>
/// <b>The door cell test.</b> <see cref="NavCellFlags.Door"/> is written only
/// for a <em>closed</em> door's rasterised footprint — <c>NavBake.Bake</c>
/// rasterises an open door's cells as <see cref="NavCellFlags.Open"/> instead
/// — so this rule only forces a weapon lowered while the door an operator
/// stands in is actually shut. An operator who has pushed on through an
/// opened door is no longer inside a doorway by the same rasterisation the
/// planner and the mover already agree on.
/// </para>
/// <para>
/// <b>The wall-distance test.</b> "Within" is inclusive of the exact
/// threshold: a wall at precisely <c>loweredWallDistanceWu</c> forces the
/// weapon lowered, and a wall one world unit farther does not. The test never
/// computes a square root: the perpendicular distance from the operator's
/// position to a candidate segment is compared against the threshold by
/// cross-multiplying both sides of the inequality by the segment's squared
/// length, which turns "distance &lt;= threshold" into a single integer
/// comparison of two squared magnitudes — see
/// <see cref="IsWithinDistanceOfSegment"/>.
/// </para>
/// <para>
/// <b>The engaging-a-target exemption.</b> An operator who is engaging a
/// hostile it has identified this tick is never forced lowered, regardless of
/// its distance to a wall or whether it stands in a door cell — the same
/// early-out already given to an exempt weapon. The lowered muzzle this rule
/// otherwise imposes is a movement discipline: a carried weapon points at the
/// ground while its operator moves through a tight space, so a stray round
/// during transit cannot go anywhere dangerous. That discipline was found to
/// force a rifleman lowered for the entire time it stood inside a corridor
/// narrower than twice <c>loweredWallDistanceWu</c> — which, against angle-house's
/// roughly 32 world-unit corridors and this rule's own 24 world-unit
/// threshold, is every corridor in that map — even while the operator had
/// already identified a hostile and was actively engaging it, making an
/// indoor rifle permanently unable to fire. Once an operator has a target, it
/// is no longer merely transiting; it is doing the thing the weapon is for,
/// and the movement-discipline rationale no longer applies.
/// </para>
/// </remarks>
public static class WeaponLoweredRules
{
    /// <summary>
    /// Whether design section 9's weapon-lowered rule forces the weapon
    /// lowered this tick for an operator standing at
    /// (<paramref name="positionX"/>, <paramref name="positionY"/>).
    /// </summary>
    /// <param name="positionX">
    /// The operator's X position, in whole world units, committed this tick
    /// (design section 5 stage 10) — the same coordinate system
    /// <paramref name="grid"/> and <paramref name="wallBuckets"/> already use.
    /// </param>
    /// <param name="positionY">The operator's Y position, in whole world units, committed this tick.</param>
    /// <param name="grid">
    /// The baked navigation grid whose <see cref="NavGrid.Passability"/>
    /// supplies the door cell tag. Must be baked (<c>NavBake.Bake</c>) from
    /// the same map <paramref name="wallBuckets"/> was built over.
    /// </param>
    /// <param name="wallBuckets">
    /// The wall segment index built over the same map <paramref name="grid"/>
    /// is baked from. Must hold only <c>WALL</c> records — a closed door's
    /// segment is tested separately by the door cell tag, and registering it
    /// here as well would double the wall-proximity test against the same
    /// physical boundary.
    /// </param>
    /// <param name="loweredWallDistanceWu">
    /// <c>SandataRuleset.LoweredWallDistanceWu</c>: the inclusive distance, in
    /// world units, within which a wall forces the weapon lowered. Never
    /// negative.
    /// </param>
    /// <param name="exemptFromLoweredRule">
    /// <c>FirearmDefinition.ExemptFromLoweredRule</c> for the carried
    /// weapon — <see langword="true"/> for a pistol. An exempt weapon is
    /// never forced lowered by this rule, regardless of position, and the
    /// wall and door geometry are not even evaluated in that case.
    /// </param>
    /// <param name="engagingIdentifiedHostile">
    /// <see langword="true"/> when this operator is engaging a hostile it has
    /// identified this tick — in practice, the caller's own target-acquisition
    /// step found a live contact for a raising operator this tick. When
    /// <see langword="true"/>, the weapon is never forced lowered by this
    /// rule, regardless of position, and the wall and door geometry are not
    /// even evaluated in that case — the same early out already given to an
    /// exempt weapon. See the type remarks' "engaging-a-target exemption" for
    /// why.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the weapon must be forced to
    /// <c>WeaponChainPhase.Lowered</c> this tick.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="grid"/> or <paramref name="wallBuckets"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="loweredWallDistanceWu"/> is negative.</exception>
    public static bool IsForcedLowered(
        long positionX,
        long positionY,
        NavGrid grid,
        WallBuckets wallBuckets,
        int loweredWallDistanceWu,
        bool exemptFromLoweredRule,
        bool engagingIdentifiedHostile)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(wallBuckets);
        ArgumentOutOfRangeException.ThrowIfNegative(loweredWallDistanceWu);

        if (exemptFromLoweredRule || engagingIdentifiedHostile)
        {
            return false;
        }

        return IsInsideDoorCell(positionX, positionY, grid) ||
            IsWithinWallDistance(positionX, positionY, grid, wallBuckets, loweredWallDistanceWu);
    }

    /// <summary>
    /// True when (<paramref name="positionX"/>, <paramref name="positionY"/>)
    /// falls inside a cell <see cref="NavGrid.Passability"/> tags
    /// <see cref="NavCellFlags.Door"/> — a closed door's rasterised footprint.
    /// A position outside the grid's bounds is never inside a door cell.
    /// </summary>
    private static bool IsInsideDoorCell(long positionX, long positionY, NavGrid grid)
    {
        var cellX = NavGrid.WorldToCellCoordinate(positionX);
        var cellY = NavGrid.WorldToCellCoordinate(positionY);

        return grid.TryGetCellIndex(cellX, cellY, out var cellIndex) &&
            grid.Passability[cellIndex] == NavCellFlags.Door;
    }

    /// <summary>
    /// True when some wall segment registered in <paramref name="wallBuckets"/>
    /// lies within <paramref name="loweredWallDistanceWu"/>, inclusive, of
    /// (<paramref name="positionX"/>, <paramref name="positionY"/>).
    /// </summary>
    /// <remarks>
    /// Searches every cell in the square box of half-width
    /// <see cref="CellSearchRadius"/> around the position's own cell. That
    /// margin is always wide enough to catch every segment whose closest
    /// point could fall within the threshold: <see cref="WallBuckets"/>
    /// registers a segment for every cell its own supercover walk passes
    /// through (see that type's remarks), so the cell holding a segment's
    /// true closest point to the query — which always lies on the segment
    /// itself — is always among the cells this method visits, once the
    /// search radius accounts for both the threshold distance itself and the
    /// query position's own offset inside its cell.
    /// </remarks>
    private static bool IsWithinWallDistance(
        long positionX,
        long positionY,
        NavGrid grid,
        WallBuckets wallBuckets,
        int loweredWallDistanceWu)
    {
        var thresholdSquared = checked((long)loweredWallDistanceWu * loweredWallDistanceWu);

        var centerCellX = NavGrid.WorldToCellCoordinate(positionX);
        var centerCellY = NavGrid.WorldToCellCoordinate(positionY);
        var cellRadius = CellSearchRadius(loweredWallDistanceWu);

        var minCellX = Math.Max(0, centerCellX - cellRadius);
        var maxCellX = Math.Min(grid.Width - 1, centerCellX + cellRadius);
        var minCellY = Math.Max(0, centerCellY - cellRadius);
        var maxCellY = Math.Min(grid.Height - 1, centerCellY + cellRadius);

        for (var cellY = minCellY; cellY <= maxCellY; cellY++)
        {
            for (var cellX = minCellX; cellX <= maxCellX; cellX++)
            {
                var candidates = wallBuckets.SegmentsInCell(grid.CellIndex(cellX, cellY));

                for (var candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
                {
                    var segmentIndex = candidates[candidateIndex];

                    if (IsWithinDistanceOfSegment(
                        positionX, positionY,
                        wallBuckets.SegmentAX(segmentIndex), wallBuckets.SegmentAY(segmentIndex),
                        wallBuckets.SegmentBX(segmentIndex), wallBuckets.SegmentBY(segmentIndex),
                        thresholdSquared))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// The half-width, in cells, of the square search box
    /// <see cref="IsWithinWallDistance"/> scans around the query position's
    /// own cell: enough cells to cover <paramref name="distanceWu"/> itself,
    /// rounded up to a whole cell, plus one further cell of margin for the
    /// query position's own worst-case offset inside its cell. Deliberately
    /// generous rather than trimmed to the tightest possible bound — this
    /// method runs once per operator per tick against a handful of nearby
    /// cells, not against the whole map.
    /// </summary>
    private static int CellSearchRadius(int distanceWu)
    {
        var cellSize = NavGrid.CellSizeWu;
        return ((distanceWu + cellSize - 1) / cellSize) + 1;
    }

    /// <summary>
    /// True when the perpendicular distance from point
    /// (<paramref name="pointX"/>, <paramref name="pointY"/>) to the closest
    /// point on segment (<paramref name="segmentAX"/>, <paramref name="segmentAY"/>)-(<paramref name="segmentBX"/>, <paramref name="segmentBY"/>)
    /// is at most the square root of <paramref name="thresholdSquared"/>,
    /// decided entirely in integer arithmetic with no square root and no
    /// division.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The projection of the point onto the segment's infinite line is
    /// tested against the segment's own span using only the sign of
    /// <c>dot</c> against <c>0</c> and <c>lengthSquared</c> — never the
    /// projection's actual position — so clamping the closest point to an
    /// endpoint never requires dividing by <c>lengthSquared</c>:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <c>dot &lt;= 0</c>: the projection falls at or before A, so A is the
    /// closest point and the answer is the plain squared distance to A
    /// compared against the threshold.
    /// </description></item>
    /// <item><description>
    /// <c>dot &gt;= lengthSquared</c>: the projection falls at or beyond B,
    /// so B is the closest point, tested the same way.
    /// </description></item>
    /// <item><description>
    /// Otherwise the closest point lies in the segment's interior, and the
    /// true squared perpendicular distance is the classic
    /// <c>cross^2 / lengthSquared</c> — but rather than dividing to compute
    /// it, both sides of the comparison
    /// <c>cross^2 / lengthSquared &lt;= thresholdSquared</c> are multiplied
    /// through by the positive <c>lengthSquared</c>, giving the
    /// division-free, sqrt-free integer comparison
    /// <c>cross^2 &lt;= thresholdSquared * lengthSquared</c> this method
    /// actually evaluates.
    /// </description></item>
    /// </list>
    /// <para>
    /// A degenerate segment (<c>lengthSquared == 0</c>, A and B coincide) has
    /// no interior and no projection to speak of, so it is treated as the
    /// single point A before either branch above runs.
    /// </para>
    /// </remarks>
    private static bool IsWithinDistanceOfSegment(
        long pointX,
        long pointY,
        long segmentAX,
        long segmentAY,
        long segmentBX,
        long segmentBY,
        long thresholdSquared)
    {
        checked
        {
            var abx = segmentBX - segmentAX;
            var aby = segmentBY - segmentAY;
            var apx = pointX - segmentAX;
            var apy = pointY - segmentAY;

            var lengthSquared = (abx * abx) + (aby * aby);

            if (lengthSquared == 0)
            {
                var pointDistanceSquared = (apx * apx) + (apy * apy);
                return pointDistanceSquared <= thresholdSquared;
            }

            var dot = (apx * abx) + (apy * aby);

            if (dot <= 0)
            {
                var distanceSquared = (apx * apx) + (apy * apy);
                return distanceSquared <= thresholdSquared;
            }

            if (dot >= lengthSquared)
            {
                var bpx = pointX - segmentBX;
                var bpy = pointY - segmentBY;
                var distanceSquared = (bpx * bpx) + (bpy * bpy);
                return distanceSquared <= thresholdSquared;
            }

            var cross = (apx * aby) - (apy * abx);
            return (cross * cross) <= (thresholdSquared * lengthSquared);
        }
    }
}
