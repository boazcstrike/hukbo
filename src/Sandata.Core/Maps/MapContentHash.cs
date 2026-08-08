using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Sandata.Core.Determinism;

namespace Sandata.Core.Maps;

/// <summary>
/// The map's own content hash, design section 12: FNV-1a, folded through
/// <see cref="SandataHash"/>, over the <b>canonicalised</b> record stream
/// <see cref="MapCanonicalizer.Canonicalize"/> produces — never over the raw
/// file text. Comments and blank lines never reach a <see cref="MapRecord"/>
/// at all (<see cref="MapTokenizer"/> strips them before parsing), file line
/// order is erased by canonical sorting, and <see cref="MapRecord.LineNumber"/>
/// itself is never folded, so none of those three can move this hash. A
/// single coordinate, one field on one record, always does.
///
/// <b>Why this matters.</b> <c>MapContentHash</c> folds into the mission
/// content hash alongside <c>SandataRuleset.ContentHash</c> (design section 12),
/// so editing one wall coordinate moves the state hash and forces a new golden
/// expectation, exactly as <c>CLAUDE.md</c> section 5 requires for any change
/// that would otherwise move gameplay silently. Without this hash, a map edit
/// would invalidate every recorded replay of a mission that uses that map with
/// no signal at all — the replay would simply diverge from a state hash that
/// never knew the map had changed underneath it.
/// </summary>
/// <remarks>
/// The byte encoding <see cref="Encode"/> and <see cref="Compute"/> share is:
/// for every record in the given sequence, one byte carrying
/// <c>(byte)(int)record.Kind</c>, followed by each of that record's integer
/// fields — in exactly the order design section 12's record table lists them —
/// as four big-endian bytes. <see cref="NameRecord"/>'s only field,
/// <see cref="NameRecord.Id"/>, is a string, not an integer field, so nothing
/// beyond its kind byte is folded for it; a map's identifier is not part of
/// its spatial content and design section 12 only ever specifies "each integer
/// field", never a string encoding. <see cref="EndRecord"/> carries no fields
/// either. Every other kind's fields are exactly the fields
/// <see cref="MapCanonicalizer.BodyFields"/> already extracts for
/// <see cref="HkmapRecord"/> and <see cref="GridRecord"/>, or is itself a body
/// kind that method covers directly.
/// </remarks>
public static class MapContentHash
{
    /// <summary>
    /// FNV-1a over <see cref="Encode"/>'s byte stream, folded one byte at a
    /// time through <see cref="SandataHash.Fold(ref ulong, long)"/> so the
    /// one canonical FNV-1a fold in this codebase stays the only place that
    /// algorithm is written, exactly as that type's own documentation
    /// requires of every Sandata content hash.
    /// </summary>
    public static ulong Compute(IReadOnlyList<MapRecord> canonicalRecords)
    {
        ArgumentNullException.ThrowIfNull(canonicalRecords);

        var hash = SandataHash.Begin();
        foreach (var value in Encode(canonicalRecords))
        {
            SandataHash.Fold(ref hash, value);
        }

        return hash;
    }

    /// <summary>
    /// The exact byte sequence <see cref="Compute"/> folds: one kind-ordinal
    /// byte per record, followed by that record's integer fields as four
    /// big-endian bytes each. Exposed on its own so a test can assert two
    /// differently-ordered inputs canonicalise to a byte-identical stream
    /// without going through the hash — a direct check that does not depend
    /// on FNV-1a happening to avoid a collision.
    /// </summary>
    public static ImmutableArray<byte> Encode(IReadOnlyList<MapRecord> canonicalRecords)
    {
        ArgumentNullException.ThrowIfNull(canonicalRecords);

        var bytes = ImmutableArray.CreateBuilder<byte>();
        foreach (var record in canonicalRecords)
        {
            bytes.Add((byte)(int)record.Kind);
            foreach (var field in FieldsOf(record))
            {
                bytes.Add((byte)(field >> 24));
                bytes.Add((byte)(field >> 16));
                bytes.Add((byte)(field >> 8));
                bytes.Add((byte)field);
            }
        }

        return bytes.ToImmutable();
    }

    /// <summary>
    /// The integer fields folded for one record, in design section 12's
    /// declared field order. The five body kinds delegate to
    /// <see cref="MapCanonicalizer.BodyFields"/> so the sort comparator and
    /// the content hash can never see a different field list for the same
    /// record.
    /// </summary>
    private static IReadOnlyList<int> FieldsOf(MapRecord record) => record switch
    {
        HkmapRecord h => [h.Version],
        GridRecord g => [g.WidthWu, g.HeightWu, g.CellWu],
        NameRecord => [],
        EndRecord => [],
        WallRecord or DoorRecord or CoverRecord or SpawnRecord or ObjectiveRecord =>
            MapCanonicalizer.BodyFields(record),
        _ => throw new ArgumentOutOfRangeException(nameof(record), record.Kind, "Unknown MapRecord kind."),
    };
}
