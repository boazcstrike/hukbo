using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Sandata.Core.Maps;

/// <summary>
/// Turns the file-order output of <see cref="MapTokenizer.Tokenize"/> into
/// the single canonical record sequence design section 12 defines, so that
/// two `.hkmap` files differing only in how their body records were typed —
/// line order, or which endpoint of a <c>WALL</c> or <c>DOOR</c> was written
/// first — canonicalise to the identical sequence and therefore, through
/// <see cref="MapContentHash"/>, to the identical hash.
///
/// <see cref="Canonicalize"/> assumes its input already satisfies the
/// structural contract <see cref="MapTokenizer.Tokenize"/> guarantees:
/// exactly one <see cref="HkmapRecord"/>, <see cref="NameRecord"/>, and
/// <see cref="GridRecord"/>, in that order, followed by zero or more body
/// records, followed by exactly one <see cref="EndRecord"/> as the last
/// element. It does not re-check that contract — only <see cref="MapTokenizer"/>
/// and <c>MapValidator</c> (task 25) own file-shape validation.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two steps, in this order.</b> First, every <see cref="WallRecord"/> and
/// <see cref="DoorRecord"/> has its endpoints normalised to lexicographic
/// ascending order — <c>(x1, y1)</c> is replaced with the lexicographically
/// smaller of the two written endpoints — so a segment drawn right-to-left
/// canonicalises identically to the same segment drawn left-to-right. Second,
/// the five body-record kinds (<see cref="MapRecordKind.Wall"/> through
/// <see cref="MapRecordKind.Objective"/>) are sorted ascending by
/// <c>(kindOrdinal, field1, field2, …)</c>, using exactly the field order
/// design section 12 lists for each kind and the ordinal
/// <see cref="MapRecordKind"/>'s own numeric values already carry.
/// </para>
/// <para>
/// <b>A <see cref="DoorRecord"/>'s <c>Hinge</c> and <c>State</c> fields are
/// never touched by normalisation</b> — only the four endpoint coordinates
/// swap. Design section 12 defines lexicographic endpoint ordering for
/// <c>WALL</c> and <c>DOOR</c> alike but says nothing about what a written
/// endpoint swap should do to <c>hinge</c>, which may or may not be intended
/// to track "the first-written endpoint". Left as a named gap in the return
/// report for whoever next touches door semantics (task 25 or the client
/// door renderer) to settle; changing it later is a canonicalisation-affecting
/// decision and needs its own test.
/// </para>
/// <para>
/// <b>Why a duplicate is a hard error here, not deferred to <c>MapValidator</c>.</b>
/// Design section 12 also assigns "no duplicate record of any kind" to
/// cross-record validation (task 25). This type enforces a narrower, earlier
/// version of that same rule for a different reason: two records that
/// compare equal under <c>(kindOrdinal, field1, field2, …)</c> are a tied key,
/// and <see cref="List{T}.Sort"/> is introsort, which is **not** stable — a
/// tied key's relative order after sorting is an implementation detail of
/// the algorithm, not a property of the input. Rejecting the tie outright is
/// what makes the comparator a **total** order in practice: with no ties
/// possible, any correct sort of the same input set produces the one same
/// sequence. Task 25's full "no duplicate record of any kind" rule is a
/// superset of this (it also has to see the header records) and stays
/// task 25's job; this check exists purely to keep this type's own sort
/// deterministic and is not a substitute for it.
/// </para>
/// </remarks>
public static class MapCanonicalizer
{
    /// <summary>
    /// The stable rule identifier <see cref="MapLoadException.Rule"/> carries
    /// when two body records canonicalise to the same
    /// <c>(kindOrdinal, field1, field2, …)</c> key. Not declared on
    /// <see cref="MapLoadException.Rules"/> because that class belongs to
    /// <see cref="MapTokenizer"/> (task 8); this rule is
    /// <see cref="MapCanonicalizer"/>'s own.
    /// </summary>
    public const string DuplicateRecordRule = "duplicate-canonical-record";

    /// <summary>
    /// Returns <paramref name="records"/> in canonical order: the leading
    /// header records unchanged, then every <see cref="WallRecord"/> and
    /// <see cref="DoorRecord"/> endpoint-normalised and every body record
    /// sorted by <c>(kindOrdinal, field1, field2, …)</c>, then the trailing
    /// <see cref="EndRecord"/> unchanged. Throws <see cref="MapLoadException"/>
    /// with <see cref="DuplicateRecordRule"/> if two body records canonicalise
    /// to the same key.
    /// </summary>
    public static ImmutableArray<MapRecord> Canonicalize(IReadOnlyList<MapRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        var header = ImmutableArray.CreateBuilder<MapRecord>();
        var body = new List<MapRecord>();
        MapRecord? end = null;

        foreach (var record in records)
        {
            switch (record)
            {
                case WallRecord wall:
                    body.Add(NormalizeWall(wall));
                    break;
                case DoorRecord door:
                    body.Add(NormalizeDoor(door));
                    break;
                case CoverRecord or SpawnRecord or ObjectiveRecord:
                    body.Add(record);
                    break;
                case EndRecord:
                    end = record;
                    break;
                default:
                    // HkmapRecord, NameRecord, GridRecord: exactly one each,
                    // already in the fixed relative order MapTokenizer
                    // enforces. Carried through unchanged.
                    header.Add(record);
                    break;
            }
        }

        body.Sort(CompareBody);
        DetectDuplicates(body);

        var canonical = header;
        canonical.AddRange(body);
        if (end is not null)
        {
            canonical.Add(end);
        }

        return canonical.ToImmutable();
    }

    private static WallRecord NormalizeWall(WallRecord wall) =>
        IsDescending(wall.X1, wall.Y1, wall.X2, wall.Y2)
            ? wall with { X1 = wall.X2, Y1 = wall.Y2, X2 = wall.X1, Y2 = wall.Y1 }
            : wall;

    private static DoorRecord NormalizeDoor(DoorRecord door) =>
        IsDescending(door.X1, door.Y1, door.X2, door.Y2)
            ? door with { X1 = door.X2, Y1 = door.Y2, X2 = door.X1, Y2 = door.Y1 }
            : door;

    /// <summary>
    /// True when the written endpoint <c>(x1, y1)</c> is lexicographically
    /// greater than <c>(x2, y2)</c> — <c>x1 &gt; x2</c>, or <c>x1 == x2</c> and
    /// <c>y1 &gt; y2</c> — meaning the pair is not yet in canonical ascending
    /// order and its endpoints must swap.
    /// </summary>
    private static bool IsDescending(int x1, int y1, int x2, int y2) =>
        x1 > x2 || (x1 == x2 && y1 > y2);

    /// <summary>
    /// Total order over body records: <c>(kindOrdinal, field1, field2, …)</c>
    /// ascending, using <see cref="MapRecordKind"/>'s own numeric value as the
    /// canonicalisation ordinal design section 12 assigns
    /// (<c>WALL = 1</c> through <c>OBJECTIVE = 5</c>) and the field order that
    /// section's record table lists for each kind. <see cref="MapRecord.LineNumber"/>
    /// never participates — two records that differ only in which source line
    /// they came from are the same canonical record.
    /// </summary>
    private static int CompareBody(MapRecord a, MapRecord b)
    {
        var kindCompare = ((int)a.Kind).CompareTo((int)b.Kind);
        if (kindCompare != 0)
        {
            return kindCompare;
        }

        var fieldsA = BodyFields(a);
        var fieldsB = BodyFields(b);
        for (var i = 0; i < fieldsA.Length; i++)
        {
            var fieldCompare = fieldsA[i].CompareTo(fieldsB[i]);
            if (fieldCompare != 0)
            {
                return fieldCompare;
            }
        }

        return 0;
    }

    /// <summary>
    /// Walks the already-sorted body list once, throwing the moment two
    /// adjacent records compare equal under <see cref="CompareBody"/>. Sorted
    /// order makes any duplicate pair adjacent, so one pass over the list is
    /// sufficient — no dictionary or hash set is needed to find it.
    /// </summary>
    private static void DetectDuplicates(List<MapRecord> sortedBody)
    {
        for (var i = 1; i < sortedBody.Count; i++)
        {
            var previous = sortedBody[i - 1];
            var current = sortedBody[i];
            if (CompareBody(previous, current) == 0)
            {
                throw new MapLoadException(
                    current.LineNumber,
                    DuplicateRecordRule,
                    $"line {current.LineNumber}: {current.Kind} duplicates the record on line " +
                    $"{previous.LineNumber} once endpoints are normalised and fields are compared.");
            }
        }
    }

    /// <summary>
    /// The integer fields of a body record, in exactly the order design
    /// section 12's record table lists them, after endpoint normalisation
    /// has already run. Shared by <see cref="CompareBody"/> and
    /// <see cref="MapContentHash"/> so the two can never drift apart.
    /// </summary>
    internal static ImmutableArray<int> BodyFields(MapRecord record) => record switch
    {
        WallRecord w => ImmutableArray.Create(w.X1, w.Y1, w.X2, w.Y2, w.Material),
        DoorRecord d => ImmutableArray.Create(d.X1, d.Y1, d.X2, d.Y2, d.Hinge, d.State),
        CoverRecord c => ImmutableArray.Create(c.MinX, c.MinY, c.MaxX, c.MaxY, c.ArcCentreBam, c.ArcHalfBam, c.Height),
        SpawnRecord s => ImmutableArray.Create(s.Faction, s.X, s.Y, s.FacingBam),
        ObjectiveRecord o => ImmutableArray.Create(o.Index, o.X, o.Y, o.RadiusWu),
        _ => throw new ArgumentOutOfRangeException(
            nameof(record),
            record.Kind,
            $"{record.Kind} is not one of the five body-record kinds."),
    };
}
