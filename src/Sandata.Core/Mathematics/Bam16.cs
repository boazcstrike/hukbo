using Hukbo.Core.Movement;

namespace Sandata.Core.Mathematics;

/// <summary>
/// A binary angular measurement: a full turn is exactly 65,536 raw units,
/// stored in an unsigned <see cref="ushort"/> so that wraparound is free —
/// <c>ushort</c> arithmetic already wraps modulo 65,536, which is exactly
/// the modulus a full turn needs. There is no <c>float</c>, no
/// <c>double</c>, and no trigonometric normalisation anywhere in this type;
/// it is a pure integer angle used to index the fine-grained math in
/// <c>Trig</c> and <c>Cordic</c> and to interoperate with the coarse,
/// pinned, append-only <see cref="Facing16"/> sectors.
/// </summary>
public readonly record struct Bam16(ushort Raw)
{
    /// <summary>The number of raw units in one full turn.</summary>
    public const int UnitsPerTurn = 65_536;

    /// <summary>
    /// The number of raw units in one <see cref="Facing16"/> sector. Sixteen
    /// sectors divide <see cref="UnitsPerTurn"/> exactly, so every sector
    /// boundary and every sector centre lands on an exact <see cref="Bam16"/>
    /// value with no rounding.
    /// </summary>
    public const int UnitsPerFacingSector = UnitsPerTurn / 16;

    /// <summary>
    /// Converts a coarse <see cref="Facing16"/> sector to the <see cref="Bam16"/>
    /// value at that sector's centre, by <c>sector * 4096</c>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="facing"/> is <see cref="Facing16.None"/>. <see cref="Bam16"/>
    /// has no "unresolved" value, so <see cref="Facing16.None"/> — which is
    /// deliberately numbered far outside the sector range so it can never be
    /// mistaken for one, see <see cref="Facing16"/> — has no angular equivalent.
    /// </exception>
    public static Bam16 FromFacing16(Facing16 facing)
    {
        if (facing == Facing16.None)
        {
            throw new ArgumentOutOfRangeException(
                nameof(facing),
                facing,
                $"{nameof(Facing16)}.{nameof(Facing16.None)} has no angular equivalent.");
        }

        return new Bam16((ushort)((int)facing * UnitsPerFacingSector));
    }

    /// <summary>
    /// Rounds this angle to the nearest <see cref="Facing16"/> sector, by
    /// <c>((Raw + 2048) &gt;&gt; 12) &amp; 15</c>.
    ///
    /// <para>
    /// This is round-to-nearest with the exact tie — 2,048 raw units past a
    /// sector's centre, exactly half a sector away from both neighbours —
    /// <b>pinned upward</b>, toward the higher-numbered (clockwise, per
    /// <see cref="Facing16"/>'s documented winding) sector. That is a
    /// deliberate, total tie-break rather than round-to-even or
    /// round-toward-zero: it needs no branch, and every value in the space
    /// of raw <see cref="ushort"/> inputs has exactly one answer, including
    /// the wrap tie at 63,488 (half a sector past sector 15's centre), which
    /// resolves to sector 0 by the same arithmetic that resolves every other
    /// tie, because <c>&amp; 15</c> folds the sector index modulo 16.
    /// </para>
    /// </summary>
    public Facing16 ToFacing16()
    {
        var sector = (Raw + (UnitsPerFacingSector / 2)) >> 12 & 15;
        return (Facing16)sector;
    }

    /// <summary>
    /// The signed shortest arc from <paramref name="from"/> to
    /// <paramref name="to"/>: a value in the range -32,768 through 32,767,
    /// positive turning the direction <see cref="Raw"/> increases (the
    /// direction <see cref="FromFacing16"/> also turns), with the smaller
    /// absolute magnitude of the two possible directions around the ring.
    ///
    /// <para>
    /// The implementation is exactly the <see cref="short"/> cast of
    /// <c>to.Raw - from.Raw</c> — no branch, no modulus, and no special case
    /// at the wrap. It works because both operands live on a ring of exactly
    /// 65,536 (2^16) values. The subtraction's low 16 bits already equal the
    /// unsigned difference modulo 65,536 — "how far to turn going the
    /// positive direction" — regardless of whether the intermediate int
    /// arithmetic went negative, because two's complement subtraction is
    /// itself modular arithmetic and truncating to the low 16 bits (the
    /// unchecked conversion this project's build performs by default, since
    /// <c>CheckForOverflowUnderflow</c> is not set) never touches those bits.
    /// Reinterpreting that same 16-bit pattern as a two's complement
    /// <see cref="short"/> is precisely the mod-65,536-to-signed-residue
    /// conversion: it maps the lower half of the ring (0 through 32,767) to
    /// itself and the upper half (32,768 through 65,535) to the negative
    /// shorts (-32,768 through -1), which is exactly "the same turn, but
    /// counted the other way because it is shorter". The one boundary case —
    /// an exact half turn, 32,768 raw units — lands on -32,768 by that same
    /// bit pattern, an arbitrary but consistent choice, since a half turn is
    /// equally short in both directions.
    /// </para>
    /// </summary>
    public static short ShortestArc(Bam16 from, Bam16 to) => (short)(to.Raw - from.Raw);
}
