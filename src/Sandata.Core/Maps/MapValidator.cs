using System;
using System.Collections.Generic;
using System.Linq;
using Sandata.Core.Geometry;

namespace Sandata.Core.Maps;

/// <summary>
/// Cross-record validation for a parsed `.hkmap` record stream (design
/// section 12, task 25). <see cref="MapTokenizer"/> (task 8) already
/// validates every field of every record on its own line, and
/// <see cref="MapCanonicalizer.Canonicalize"/> (task 15) already throws on
/// two body records whose canonical fields tie exactly. Neither of those
/// can see across the whole file, which is what design section 12's
/// "Cross-record validation" list needs: at least one <see cref="SpawnRecord"/>
/// per faction, no two spawns closer than one body diameter, a flood fill
/// from outside the bounding box that never reaches a spawn, every
/// faction-0 spawn able to reach every <see cref="ObjectiveRecord"/> with
/// every door treated as passable, and the one duplicate shape the
/// canonicaliser's field-equality comparator cannot see — two objectives
/// that share an <see cref="ObjectiveRecord.Index"/> at two different
/// coordinates. <see cref="Validate"/> throws <see cref="MapLoadException"/>
/// with one of <see cref="Rules"/> the first time it finds a violation; it
/// does not accumulate a full report.
///
/// <see cref="Validate"/> takes whatever <see cref="IReadOnlyList{MapRecord}"/>
/// the caller holds — canonical or the tokenizer's raw output — because
/// every check below re-derives what it needs directly from the record set
/// (faction counts, coordinates, index values, wall/door geometry) rather
/// than trusting position in the list. Calling it on canonical order is not
/// required for correctness, only conventional, since it is the form
/// <see cref="MapContentHash.Compute"/> also expects.
/// </summary>
/// <remarks>
/// <para>
/// <b>Decision 1 — does a map's name reach the content hash?</b>
/// <see cref="MapContentHash"/> (task 15) already answers no: a
/// <see cref="NameRecord"/> contributes only its kind byte, never
/// <see cref="NameRecord.Id"/>. That is the right rule, kept unchanged here.
/// A map's identifier is a label a designer picks for a file path and a
/// debug print, not a piece of the geometry, spawns, or objectives a replay
/// depends on. Renaming <c>angle-house.hkmap</c> to <c>angle-house-v2.hkmap</c>
/// must not silently invalidate every recorded golden hash and every replay
/// that cites the old one — only an edit to the file's spatial content
/// should do that. <see cref="MapValidatorTests.MapNameDoesNotReachTheContentHash_TwoMapsDifferingOnlyByNameHashIdentically"/>
/// pins this: two otherwise-identical maps differing only by
/// <c>NAME</c> hash identically.
/// </para>
/// <para>
/// <b>Decision 2 — does canonicalising a door move its hinge?</b>
/// <see cref="MapCanonicalizer.Canonicalize"/> normalises a door's
/// endpoints to lexicographic-ascending order but never touches
/// <see cref="DoorRecord.Hinge"/> or <see cref="DoorRecord.State"/> (see
/// that method's own doc comment, and
/// <c>MapCanonicalizerTests.DoorNormalisationSwapsEndpointsButLeavesHingeAndStateUntouched</c>,
/// both task 15, both out of this task's file scope). That is only safe if
/// <see cref="DoorRecord.Hinge"/>'s meaning is <b>absolute</b>: hinge 0
/// names the endpoint that sorts lexicographically smaller — the one that
/// becomes <see cref="DoorRecord.X1"/>/<see cref="DoorRecord.Y1"/> after
/// normalisation, regardless of which order the map's author typed the
/// four coordinates — and hinge 1 names the larger one. Read this way, the
/// hinge value for a given physical door never depends on authoring order,
/// so leaving it untouched during the endpoint swap is correct by
/// construction, and two authorings of the same physical door (endpoints
/// listed either way, hinge written against the physical point per this
/// rule) canonicalise byte-identically.
/// </para>
/// <para>
/// The rejected alternative is a <b>relative</b> reading: hinge 0 names
/// whichever endpoint the author typed first, hinge 1 the one typed second.
/// Under that reading, leaving hinge untouched while swapping endpoints is
/// a bug — it silently changes which physical point the door hinges on
/// whenever the author happened to type the endpoints in descending order,
/// with no error and no visible symptom beyond the door swinging the wrong
/// way in play. This task cannot edit <c>MapCanonicalizer.cs</c> (outside
/// its file scope) to add a compensating flip even if the relative reading
/// were chosen, which is one more reason the absolute reading — needing no
/// change to already-shipped, already-tested behaviour — is the one this
/// type adopts and documents.
/// </para>
/// <para>
/// <see cref="MapValidatorTests.DoorHingeIsAbsoluteToCanonicalEndpointOrder_NotRelativeToAuthoredOrder"/>
/// is the fixture that pins this and would fail under the wrong reading: it
/// authors the same physical door twice, endpoints in opposite order, hinge
/// written against the absolute rule both times, and asserts the two
/// canonicalise to byte-identical output. If <see cref="MapCanonicalizer"/>
/// were ever changed to flip <see cref="DoorRecord.Hinge"/> on an endpoint
/// swap — the "fix" a relative reading would call for — that flip would
/// make the descending authoring's canonical hinge disagree with the
/// ascending authoring's, and this test would go red.
/// </para>
/// </remarks>
public static class MapValidator
{
    /// <summary>
    /// Sandata's common humanoid body radius, matching
    /// <c>Hukbo.Core.Simulation.CollisionRules.DefaultBodyRadiusRaw</c>'s
    /// numeric value — 4.25 world units — exactly. Declared again rather
    /// than referenced: <c>Sandata.Core</c> holds a project reference only
    /// to <c>Hukbo.Shared.Core</c>, never to <c>Hukbo.Core</c>, and design
    /// section 4 records the two figures as "unchanged" rather than
    /// "shared". Expressed as quarters of a world unit (<c>17 / 4</c>) so
    /// <see cref="ValidateSpawnSeparation"/> can compare squared distances
    /// with exact integer arithmetic instead of a fractional radius —
    /// <c>double</c> is banned in this project.
    /// </summary>
    private const long BodyRadiusQuarterWu = 17;

    /// <summary>
    /// One body diameter, squared, in quarter-world-unit units:
    /// <c>(2 * BodyRadiusQuarterWu)^2 = 34^2 = 1156</c>. Two spawns violate
    /// separation when <c>4 * distanceSquaredWu &lt; 1156</c>. Multiplying
    /// the plain squared distance by four before comparing is exactly
    /// equivalent to comparing the un-multiplied distance against
    /// <c>8.5 wu</c> squared, without ever representing <c>8.5</c> or
    /// <c>4.25</c> as anything other than an integer ratio.
    /// </summary>
    private const long BodyDiameterQuarterWuSquared = (2 * BodyRadiusQuarterWu) * (2 * BodyRadiusQuarterWu);

    /// <summary>Stable, machine-checkable identifiers for every cross-record rule this type enforces.</summary>
    public static class Rules
    {
        /// <summary>No <see cref="SpawnRecord"/> has <see cref="SpawnRecord.Faction"/> 0.</summary>
        public const string MissingFaction0Spawn = "missing-faction-0-spawn";

        /// <summary>No <see cref="SpawnRecord"/> has <see cref="SpawnRecord.Faction"/> 1.</summary>
        public const string MissingFaction1Spawn = "missing-faction-1-spawn";

        /// <summary>Two <see cref="SpawnRecord"/>s are closer than one body diameter.</summary>
        public const string SpawnsTooClose = "spawns-too-close";

        /// <summary>A flood fill seeded from outside the map's bounding box reaches a spawn cell.</summary>
        public const string MapNotFullyEnclosed = "map-not-fully-enclosed";

        /// <summary>
        /// <see cref="ObjectiveRecord.Index"/> values, sorted, are not exactly <c>0, 1, 2, ...</c> with
        /// no gap and no repeat — the one duplicate shape <see cref="MapCanonicalizer"/>'s
        /// field-equality comparator cannot see, since two objectives sharing an index
        /// at two different coordinates have different fields.
        /// </summary>
        public const string ObjectiveIndicesNotDenseFromZero = "objective-indices-not-dense-from-zero";

        /// <summary>A faction-0 spawn cannot reach an objective with every door treated as passable.</summary>
        public const string ObjectiveUnreachableFromFaction0 = "objective-unreachable-from-faction-0";
    }

    /// <summary>
    /// Runs every cross-record rule in order — spawn presence, spawn
    /// separation, objective index density, full enclosure, then faction-0
    /// reachability — and throws <see cref="MapLoadException"/> naming the
    /// first one <paramref name="records"/> breaks. Returns normally if the
    /// map satisfies all five.
    /// </summary>
    public static void Validate(IReadOnlyList<MapRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        var grid = records.OfType<GridRecord>().Single();
        var walls = records.OfType<WallRecord>().ToArray();
        var doors = records.OfType<DoorRecord>().ToArray();
        var spawns = records.OfType<SpawnRecord>().ToArray();
        var objectives = records.OfType<ObjectiveRecord>().ToArray();

        ValidateSpawnPresence(spawns, grid.LineNumber);
        ValidateSpawnSeparation(spawns);
        ValidateObjectiveIndexDensity(objectives);

        var cellsX = grid.WidthWu / grid.CellWu;
        var cellsY = grid.HeightWu / grid.CellWu;
        var cellWu = grid.CellWu;

        var wallSegments = walls.Select(w => ((long)w.X1, (long)w.Y1, (long)w.X2, (long)w.Y2));
        var doorSegments = doors.Select(d => ((long)d.X1, (long)d.Y1, (long)d.X2, (long)d.Y2));

        // Enclosure treats a door as structural: an intentional, controlled
        // opening is not a hole in the shell. Reachability treats a door as
        // always passable, per design section 12's own wording for this
        // rule, regardless of its authored open/closed State.
        var wallOnlyBlocked = RasterizeBlockedCells(cellsX, cellsY, cellWu, wallSegments);
        var wallAndDoorBlocked = RasterizeBlockedCells(cellsX, cellsY, cellWu, wallSegments.Concat(doorSegments));

        ValidateEnclosure(wallAndDoorBlocked, cellsX, cellsY, cellWu, spawns);
        ValidateReachability(wallOnlyBlocked, cellsX, cellsY, cellWu, spawns, objectives);
    }

    private static void ValidateSpawnPresence(IReadOnlyList<SpawnRecord> spawns, int fallbackLineNumber)
    {
        if (!spawns.Any(s => s.Faction == 0))
        {
            throw new MapLoadException(
                fallbackLineNumber,
                Rules.MissingFaction0Spawn,
                "The map has no SPAWN record for faction 0.");
        }

        if (!spawns.Any(s => s.Faction == 1))
        {
            throw new MapLoadException(
                fallbackLineNumber,
                Rules.MissingFaction1Spawn,
                "The map has no SPAWN record for faction 1.");
        }
    }

    private static void ValidateSpawnSeparation(IReadOnlyList<SpawnRecord> spawns)
    {
        for (var i = 0; i < spawns.Count; i++)
        {
            for (var j = i + 1; j < spawns.Count; j++)
            {
                var dx = (long)spawns[i].X - spawns[j].X;
                var dy = (long)spawns[i].Y - spawns[j].Y;
                var distanceSquared = checked((dx * dx) + (dy * dy));

                if (checked(4L * distanceSquared) < BodyDiameterQuarterWuSquared)
                {
                    throw new MapLoadException(
                        spawns[j].LineNumber,
                        Rules.SpawnsTooClose,
                        "SPAWN records on lines " + spawns[i].LineNumber + " and " + spawns[j].LineNumber +
                        " are closer than one body diameter (17 quarter-world-units of radius each).");
                }
            }
        }
    }

    private static void ValidateObjectiveIndexDensity(IReadOnlyList<ObjectiveRecord> objectives)
    {
        var sorted = objectives.OrderBy(o => o.Index).ToArray();

        for (var expectedIndex = 0; expectedIndex < sorted.Length; expectedIndex++)
        {
            if (sorted[expectedIndex].Index != expectedIndex)
            {
                throw new MapLoadException(
                    sorted[expectedIndex].LineNumber,
                    Rules.ObjectiveIndicesNotDenseFromZero,
                    "OBJECTIVE indices must be dense from 0 with no gap and no repeat; expected index " +
                    expectedIndex + " at this position in ascending order but found " + sorted[expectedIndex].Index + ".");
            }
        }
    }

    private static void ValidateEnclosure(
        bool[] enclosureBlocked, int cellsX, int cellsY, int cellWu, IReadOnlyList<SpawnRecord> spawns)
    {
        var visited = new bool[cellsX * cellsY];
        var queue = new Queue<int>();

        void Seed(int x, int y)
        {
            if (x < 0 || x >= cellsX || y < 0 || y >= cellsY)
            {
                return;
            }

            var index = (y * cellsX) + x;
            if (visited[index] || enclosureBlocked[index])
            {
                return;
            }

            visited[index] = true;
            queue.Enqueue(index);
        }

        for (var x = 0; x < cellsX; x++)
        {
            Seed(x, 0);
            Seed(x, cellsY - 1);
        }

        for (var y = 0; y < cellsY; y++)
        {
            Seed(0, y);
            Seed(cellsX - 1, y);
        }

        while (queue.Count > 0)
        {
            var index = queue.Dequeue();
            var x = index % cellsX;
            var y = index / cellsX;
            Seed(x + 1, y);
            Seed(x - 1, y);
            Seed(x, y + 1);
            Seed(x, y - 1);
        }

        foreach (var spawn in spawns)
        {
            var cellIndex = CellIndexOf(spawn.X, spawn.Y, cellsX, cellsY, cellWu);
            if (visited[cellIndex])
            {
                throw new MapLoadException(
                    spawn.LineNumber,
                    Rules.MapNotFullyEnclosed,
                    "SPAWN at (" + spawn.X + ", " + spawn.Y +
                    ") is reachable by a flood fill seeded from outside the map's bounding box; the map is not fully enclosed.");
            }
        }
    }

    private static void ValidateReachability(
        bool[] movementBlocked,
        int cellsX,
        int cellsY,
        int cellWu,
        IReadOnlyList<SpawnRecord> spawns,
        IReadOnlyList<ObjectiveRecord> objectives)
    {
        if (objectives.Count == 0)
        {
            return;
        }

        var visited = new bool[cellsX * cellsY];
        var queue = new Queue<int>();

        void Seed(int x, int y)
        {
            if (x < 0 || x >= cellsX || y < 0 || y >= cellsY)
            {
                return;
            }

            var index = (y * cellsX) + x;
            if (visited[index] || movementBlocked[index])
            {
                return;
            }

            visited[index] = true;
            queue.Enqueue(index);
        }

        foreach (var spawn in spawns.Where(s => s.Faction == 0))
        {
            var cellX = Math.Min(cellsX - 1, spawn.X / cellWu);
            var cellY = Math.Min(cellsY - 1, spawn.Y / cellWu);
            Seed(cellX, cellY);
        }

        while (queue.Count > 0)
        {
            var index = queue.Dequeue();
            var x = index % cellsX;
            var y = index / cellsX;
            Seed(x + 1, y);
            Seed(x - 1, y);
            Seed(x, y + 1);
            Seed(x, y - 1);
        }

        foreach (var objective in objectives)
        {
            var cellIndex = CellIndexOf(objective.X, objective.Y, cellsX, cellsY, cellWu);
            if (!visited[cellIndex])
            {
                throw new MapLoadException(
                    objective.LineNumber,
                    Rules.ObjectiveUnreachableFromFaction0,
                    "OBJECTIVE " + objective.Index + " at (" + objective.X + ", " + objective.Y +
                    ") cannot be reached from any faction-0 SPAWN with every door treated as passable.");
            }
        }
    }

    private static int CellIndexOf(int worldX, int worldY, int cellsX, int cellsY, int cellWu)
    {
        var cellX = Math.Min(cellsX - 1, worldX / cellWu);
        var cellY = Math.Min(cellsY - 1, worldY / cellWu);
        return (cellY * cellsX) + cellX;
    }

    /// <summary>
    /// Rasterises a set of axis-independent segments into a per-cell
    /// blocked flag array sized <c>cellsX * cellsY</c>. Used once for
    /// walls alone (movement passability, doors excluded) and once for
    /// walls plus doors (the enclosure shell). No baked nav grid exists in
    /// this task's dependency set (tasks 18/20/21 are parallel, not
    /// prerequisite), so this builds its own directly from
    /// <see cref="GridRecord"/>'s own <see cref="GridRecord.CellWu"/> rather
    /// than <c>Navigation.NavGrid.CellSizeWu</c>'s unrelated fixed constant.
    /// </summary>
    private static bool[] RasterizeBlockedCells(
        int cellsX, int cellsY, int cellWu, IEnumerable<(long X1, long Y1, long X2, long Y2)> segments)
    {
        var blocked = new bool[cellsX * cellsY];

        foreach (var (x1, y1, x2, y2) in segments)
        {
            MarkBlockedCells(blocked, cellsX, cellsY, cellWu, x1, y1, x2, y2);
        }

        return blocked;
    }

    private static void MarkBlockedCells(bool[] blocked, int cellsX, int cellsY, int cellWu, long x1, long y1, long x2, long y2)
    {
        // Clamp both ends of each axis: a wall sitting exactly on the far
        // boundary (X or Y equal to the map's own width/height) computes a
        // raw min-cell equal to cellsX/cellsY, one past the last valid
        // index. Clamping only the max (as an earlier revision did) leaves
        // minCell > maxCell for that wall and the loop below never runs,
        // silently failing to block the very cells that close the shell.
        var minCellX = Math.Clamp((int)(Math.Min(x1, x2) / cellWu), 0, cellsX - 1);
        var maxCellX = Math.Clamp((int)(Math.Max(x1, x2) / cellWu), 0, cellsX - 1);
        var minCellY = Math.Clamp((int)(Math.Min(y1, y2) / cellWu), 0, cellsY - 1);
        var maxCellY = Math.Clamp((int)(Math.Max(y1, y2) / cellWu), 0, cellsY - 1);

        for (var cellY = minCellY; cellY <= maxCellY; cellY++)
        {
            var boxMinY = (long)cellY * cellWu;
            var boxMaxY = boxMinY + cellWu;

            for (var cellX = minCellX; cellX <= maxCellX; cellX++)
            {
                var boxMinX = (long)cellX * cellWu;
                var boxMaxX = boxMinX + cellWu;

                if (SegmentIntersectsBox(x1, y1, x2, y2, boxMinX, boxMinY, boxMaxX, boxMaxY))
                {
                    blocked[(cellY * cellsX) + cellX] = true;
                }
            }
        }
    }

    /// <summary>
    /// Exact, integer-only segment-vs-closed-box intersection test built on
    /// <see cref="ExactPredicates.ClassifySegments"/>. Rejects on bounding
    /// boxes first, then accepts if either endpoint lies inside the closed
    /// box, then falls back to testing the segment against each of the
    /// box's four edges — the only remaining case is a segment that clips a
    /// corner or passes fully through without either endpoint inside.
    /// </summary>
    private static bool SegmentIntersectsBox(
        long ax, long ay, long bx, long by, long boxMinX, long boxMinY, long boxMaxX, long boxMaxY)
    {
        if (Math.Max(ax, bx) < boxMinX || Math.Min(ax, bx) > boxMaxX ||
            Math.Max(ay, by) < boxMinY || Math.Min(ay, by) > boxMaxY)
        {
            return false;
        }

        if (IsInsideClosedBox(ax, ay, boxMinX, boxMinY, boxMaxX, boxMaxY) ||
            IsInsideClosedBox(bx, by, boxMinX, boxMinY, boxMaxX, boxMaxY))
        {
            return true;
        }

        return
            SegmentsMeet(ax, ay, bx, by, boxMinX, boxMinY, boxMaxX, boxMinY) ||
            SegmentsMeet(ax, ay, bx, by, boxMaxX, boxMinY, boxMaxX, boxMaxY) ||
            SegmentsMeet(ax, ay, bx, by, boxMaxX, boxMaxY, boxMinX, boxMaxY) ||
            SegmentsMeet(ax, ay, bx, by, boxMinX, boxMaxY, boxMinX, boxMinY);
    }

    private static bool IsInsideClosedBox(long x, long y, long minX, long minY, long maxX, long maxY) =>
        x >= minX && x <= maxX && y >= minY && y <= maxY;

    private static bool SegmentsMeet(long ax, long ay, long bx, long by, long cx, long cy, long dx, long dy) =>
        ExactPredicates.ClassifySegments(ax, ay, bx, by, cx, cy, dx, dy) != SegmentRelation.Disjoint;
}
