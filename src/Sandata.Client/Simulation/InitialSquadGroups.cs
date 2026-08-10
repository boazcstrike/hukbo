using System;
using System.Collections.Immutable;
using Hukbo.Core.Mathematics;
using Sandata.Core.Maps;
using Sandata.Core.Navigation;
using Sandata.Core.Simulation;

namespace Sandata.Client.Simulation;

/// <summary>
/// Builds the <c>MissionState.Groups</c> array a mission starts with, so that
/// an assaulting squad has somewhere to go the moment the first tick runs.
/// This is Sandata's autonomous destination source, and until this type
/// existed there was none: <c>SandataSimulation.AdvancePathService</c>'s own
/// remarks say so, and a client that started with an empty <c>Groups</c> array
/// watched stage 7's whole search-and-publish machinery run every tick with
/// nothing to act on.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is a stepping stone and not the answer to design section 15's open
/// question</b> — "what an autonomous squad wants and how a destination is
/// chosen". The intended model is per-area clearing with noise attraction,
/// recorded in <c>docs/plans/2026-08-10-sandata-playable-client.md</c>. What
/// this type does is the smallest rule that makes the game demonstrate itself:
/// each assaulting squad walks to a map objective.
/// </para>
/// <para>
/// <b>It lives in the client rather than in <c>Sandata.Core</c> on purpose.</b>
/// A destination source inside the simulation would also fire for the
/// 200-operator seed-1 headless workload, whose <c>Groups</c> array is empty
/// today; those operators would begin pathing, and the recorded
/// <c>stateHash</c> would move — which under <c>CLAUDE.md</c> section 5
/// requires a new preset version and new golden expectations. Keeping the
/// source here leaves <c>Sandata.Core</c> and every recorded digest untouched.
/// </para>
/// <para>
/// <b>The union rule below is a second copy of
/// <c>Sandata.Core.Squads.SquadGrouping</c>'s rule</b>, which is `internal` to
/// that assembly and visible only to <c>Sandata.Core.Tests</c>. The copy is
/// exact rather than approximate: <c>SquadGrouping</c>'s own remarks state
/// that a candidate pair list "can only narrow, never widen", and
/// <c>SandataSimulation</c>'s stage 3 draws its list from a
/// <c>RebuildWithinRange</c> call sized to <c>GroupCohesionRadiusWu</c>, so
/// every pair within the cohesion radius is already a candidate there. An
/// all-pairs scan gated on the same radius therefore reaches the same
/// components, and the group ids this type writes are the ones stage 6 will
/// derive on tick zero. If that rule ever changes, this copy has to change
/// with it.
/// </para>
/// </remarks>
internal static class InitialSquadGroups
{
    /// <summary>
    /// Builds one <c>GroupPathState</c> per assaulting squad, ascending by
    /// group id — the order <c>MissionState.Groups</c> is documented to hold.
    /// </summary>
    /// <param name="operators">
    /// The mission's initial roster, strictly ascending by
    /// <c>OperatorState.EntityId</c>.
    /// </param>
    /// <param name="objectives">The map's objective records, in map order.</param>
    /// <param name="navGrid">The baked grid the goal cells are indices into.</param>
    /// <param name="groupCohesionRadiusWu">
    /// <c>SandataRuleset.GroupCohesionRadiusWu</c>, in world units. Passed
    /// rather than read, matching the convention <c>SquadGrouping</c>,
    /// <c>PathService</c>, and <c>WeaponLoweredRules</c> already follow for
    /// their own ruleset constants.
    /// </param>
    /// <param name="assaultingFaction">
    /// The faction whose squads seek objectives. Every other faction holds,
    /// which is what <c>PathReasonCode.NoDestinationRequested</c> already
    /// means for a group no <c>Groups</c> entry names.
    /// </param>
    /// <returns>
    /// The groups array, empty when there is no roster, no objective, or no
    /// assaulting operator.
    /// </returns>
    internal static ImmutableArray<GroupPathState> Build(
        ImmutableArray<OperatorState> operators,
        ImmutableArray<ObjectiveRecord> objectives,
        NavGrid navGrid,
        int groupCohesionRadiusWu,
        int assaultingFaction)
    {
        ArgumentNullException.ThrowIfNull(navGrid);
        ArgumentOutOfRangeException.ThrowIfNegative(groupCohesionRadiusWu);

        if (operators.IsDefaultOrEmpty || objectives.IsDefaultOrEmpty)
        {
            return ImmutableArray<GroupPathState>.Empty;
        }

        var count = operators.Length;
        var parent = new int[count];
        for (var index = 0; index < count; index++)
        {
            parent[index] = index;
        }

        // Squared once, outside the pair loop, and in raw fixed-point units,
        // so the comparison stays integer with no square root — the same shape
        // SquadGrouping uses. Widened to long because a raw coordinate at a
        // scale of 1,024 squares well past int on a large map.
        var radiusRaw = (long)groupCohesionRadiusWu * FixedPoint.Scale;
        var squaredRadiusRaw = radiusRaw * radiusRaw;

        for (var low = 0; low < count; low++)
        {
            for (var high = low + 1; high < count; high++)
            {
                if (operators[low].Faction != operators[high].Faction)
                {
                    continue;
                }

                var deltaX = (long)operators[high].PositionX.RawValue - operators[low].PositionX.RawValue;
                var deltaY = (long)operators[high].PositionY.RawValue - operators[low].PositionY.RawValue;

                // Inclusive, matching SquadGrouping: exact tangency at the
                // radius is a union.
                if ((deltaX * deltaX) + (deltaY * deltaY) <= squaredRadiusRaw)
                {
                    Union(parent, low, high);
                }
            }
        }

        // In ascending entity-id order, so the first index reached in a
        // component carries that component's lowest entity id — its identity
        // under design section 8, and also its leader at tick zero, when every
        // operator is alive.
        var componentRoots = new int[count];
        var componentCount = 0;

        for (var index = 0; index < count; index++)
        {
            if (operators[index].Faction != assaultingFaction)
            {
                continue;
            }

            var root = Find(parent, index);
            var alreadySeen = false;
            for (var seen = 0; seen < componentCount; seen++)
            {
                if (componentRoots[seen] == root)
                {
                    alreadySeen = true;
                    break;
                }
            }

            if (!alreadySeen)
            {
                componentRoots[componentCount] = root;
                componentCount++;
            }
        }

        if (componentCount == 0)
        {
            return ImmutableArray<GroupPathState>.Empty;
        }

        var sortedObjectives = SortByIndex(objectives);
        var builder = ImmutableArray.CreateBuilder<GroupPathState>(componentCount);

        for (var rank = 0; rank < componentCount; rank++)
        {
            var leader = operators[FirstIndexOfRoot(parent, componentRoots[rank], count)];
            var objective = sortedObjectives[rank % sortedObjectives.Length];

            if (!TryResolveCell(navGrid, WorldUnits(leader.PositionX), WorldUnits(leader.PositionY), out var startCell) ||
                !TryResolveCell(navGrid, objective.X, objective.Y, out var goalCell))
            {
                // An objective or a spawn with no reachable cell anywhere on
                // the grid produces no entry at all rather than an entry
                // naming a cell the search would reject.
                continue;
            }

            builder.Add(new GroupPathState(
                GroupId: leader.EntityId,
                DestinationCellIndex: goalCell,
                HasOutstandingRequest: true,
                StartCellIndex: startCell,
                GoalCellIndex: goalCell,
                RequestTick: 0));
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// The world-unit value of a fixed-point coordinate, truncated toward
    /// zero. Cell resolution is a 4 world-unit quantization anyway
    /// (<c>NavGrid.CellSizeWu</c>), so the sub-unit fraction cannot change
    /// which cell is chosen except exactly on a boundary.
    /// </summary>
    private static int WorldUnits(FixedPoint value) => value.RawValue / FixedPoint.Scale;

    /// <summary>
    /// The index of <paramref name="cellIndex"/>'s nearest non-blocked cell:
    /// the cell containing (<paramref name="worldX"/>, <paramref name="worldY"/>)
    /// when that one is already open, otherwise the nearest one found by
    /// widening square rings around it. Ring order is deterministic — rows
    /// ascending, columns ascending within a row — so two runs of the same
    /// map always pick the same replacement.
    /// </summary>
    private static bool TryResolveCell(NavGrid navGrid, int worldX, int worldY, out int cellIndex)
    {
        cellIndex = -1;

        var centerX = NavGrid.WorldToCellCoordinate(worldX);
        var centerY = NavGrid.WorldToCellCoordinate(worldY);

        var maximumRadius = navGrid.Width > navGrid.Height ? navGrid.Width : navGrid.Height;

        for (var radius = 0; radius <= maximumRadius; radius++)
        {
            for (var offsetY = -radius; offsetY <= radius; offsetY++)
            {
                for (var offsetX = -radius; offsetX <= radius; offsetX++)
                {
                    // Only the ring itself: every cell strictly inside it was
                    // already tested by a smaller radius.
                    var onRing = offsetY == -radius || offsetY == radius ||
                        offsetX == -radius || offsetX == radius;
                    if (!onRing)
                    {
                        continue;
                    }

                    if (!navGrid.TryGetCellIndex(centerX + offsetX, centerY + offsetY, out var candidate))
                    {
                        continue;
                    }

                    if (navGrid.Passability[candidate] != NavCellFlags.Blocked)
                    {
                        cellIndex = candidate;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static ObjectiveRecord[] SortByIndex(ImmutableArray<ObjectiveRecord> objectives)
    {
        var sorted = new ObjectiveRecord[objectives.Length];
        objectives.CopyTo(sorted);

        // Insertion sort: the objective count on a hand-authored map is a
        // handful, and an insertion sort is stable, so two objectives sharing
        // an index keep their map order rather than swapping between runs.
        for (var index = 1; index < sorted.Length; index++)
        {
            var current = sorted[index];
            var scan = index - 1;
            while (scan >= 0 && sorted[scan].Index > current.Index)
            {
                sorted[scan + 1] = sorted[scan];
                scan--;
            }

            sorted[scan + 1] = current;
        }

        return sorted;
    }

    private static int FirstIndexOfRoot(int[] parent, int root, int count)
    {
        for (var index = 0; index < count; index++)
        {
            if (Find(parent, index) == root)
            {
                return index;
            }
        }

        return root;
    }

    private static int Find(int[] parent, int index)
    {
        while (parent[index] != index)
        {
            parent[index] = parent[parent[index]];
            index = parent[index];
        }

        return index;
    }

    private static void Union(int[] parent, int left, int right)
    {
        var leftRoot = Find(parent, left);
        var rightRoot = Find(parent, right);

        if (leftRoot == rightRoot)
        {
            return;
        }

        // Attach the higher root to the lower one, which keeps the surviving
        // root at the component's lowest index.
        //
        // <b>Nothing depends on that, and the claim that something does was
        // tested and disproved.</b> Inverting this comparison changes which
        // index ends up as the root and changes no group id at all, because
        // FirstIndexOfRoot scans ascending and reports the component's lowest
        // member whichever index happens to be its root. A break here fails no
        // test; the break that does bind group identity is reversing that
        // scan. The direction is kept anyway because a root that is also the
        // component's identity is easier to reason about in a debugger, not
        // because it is load-bearing.
        if (leftRoot < rightRoot)
        {
            parent[rightRoot] = leftRoot;
        }
        else
        {
            parent[leftRoot] = rightRoot;
        }
    }
}
