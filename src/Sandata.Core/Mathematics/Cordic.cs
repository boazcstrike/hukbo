namespace Sandata.Core.Mathematics;

/// <summary>
/// Integer CORDIC in vectoring mode, computing the binary angular measurement
/// (BAM) equivalent of <c>Math.Atan2</c>. Sixteen iterations, shifts and adds
/// only, no division and no multiplication of two variables. Replaces
/// <c>Math.Atan2</c>, which is banned from this project because a
/// <c>double</c> transcendental carries no cross-version guarantee. Accuracy
/// is roughly 0.0055 degrees (about one BAM unit) at magnitudes large enough
/// that the sixteen shift-and-add steps have bits to operate on; at very
/// small integer magnitudes (single-digit <paramref name="y"/>/<paramref
/// name="x"/>) the error grows because there is less precision to shift out
/// of the input in the first place, not because the algorithm is wrong.
/// </summary>
public static class Cordic
{
    /// <summary>
    /// The sixteen pinned arctangent constants, <c>round(65536 * atan(2^-i) /
    /// (2 * pi))</c> for <c>i</c> from 0 to 15, computed from the mathematical
    /// definition and pinned as literals. This table is a hash contract.
    /// </summary>
    internal static readonly int[] AtanTable =
    [
        8192, 4836, 2555, 1297, 651, 326, 163, 81, 41, 20, 10, 5, 3, 1, 1, 0,
    ];

    /// <summary>
    /// The binary angular measurement of the vector <c>(x, y)</c>, at scale
    /// 65,536 per turn. <c>Atan2(0, 0)</c> is defined as zero by convention,
    /// matching the usual handling of the degenerate origin case.
    /// </summary>
    public static ushort Atan2(long y, long x)
    {
        if (x == 0 && y == 0)
        {
            return 0;
        }

        var xNegative = x < 0;
        var yNegative = y < 0;
        var absX = xNegative ? -x : x;
        var absY = yNegative ? -y : y;

        var swapped = absY > absX;
        var primary = swapped ? absY : absX;
        var secondary = swapped ? absX : absY;

        var phi = Vector(primary, secondary);

        var octant = (xNegative ? 4 : 0) | (yNegative ? 2 : 0) | (swapped ? 1 : 0);
        var (baseAngle, sign) = OctantBase(octant);
        var angle = baseAngle + (sign * phi);

        return unchecked((ushort)angle);
    }

    /// <summary>
    /// Runs the sixteen-iteration CORDIC vectoring core over a vector already
    /// reduced to the first octant, so <paramref name="primary"/> is
    /// non-negative and at least as large as <paramref name="secondary"/>,
    /// which is also non-negative. Returns the angle from the primary axis in
    /// BAM units, in the range 0 to 8,192 inclusive.
    /// </summary>
    private static int Vector(long primary, long secondary)
    {
        var x = primary;
        var y = secondary;
        var angle = 0;

        for (var i = 0; i < 16; i++)
        {
            if (y > 0)
            {
                var nextX = x + (y >> i);
                var nextY = y - (x >> i);
                x = nextX;
                y = nextY;
                angle += AtanTable[i];
            }
            else if (y < 0)
            {
                var nextX = x - (y >> i);
                var nextY = y + (x >> i);
                x = nextX;
                y = nextY;
                angle -= AtanTable[i];
            }
        }

        return angle;
    }

    /// <summary>
    /// The base angle and sign for each of the eight octants, so the final
    /// angle is <c>baseAngle + sign * phi</c> with <c>phi</c> the first-octant
    /// angle from <see cref="Vector"/>. The octant number packs the three
    /// reduction decisions as bits: 4 for a negative <c>x</c>, 2 for a
    /// negative <c>y</c>, 1 for the primary/secondary swap.
    /// </summary>
    private static (int BaseAngle, int Sign) OctantBase(int octant) => octant switch
    {
        0 => (0, 1),
        1 => (16384, -1),
        2 => (0, -1),
        3 => (49152, 1),
        4 => (32768, -1),
        5 => (16384, 1),
        6 => (32768, 1),
        7 => (49152, -1),
        _ => throw new ArgumentOutOfRangeException(nameof(octant)),
    };
}
