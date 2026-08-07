namespace Sandata.Core.Mathematics;

/// <summary>
/// Integer sine and cosine over the binary angular measurement (BAM) domain:
/// a full turn is 65,536 units, so the type wraps for free on <see cref="ushort"/>
/// arithmetic. Both functions return a raw value at scale 65,536 (that is,
/// <c>Sin</c> returns 65,536 for a quarter turn, not 1). Replaces
/// <c>Math.Sin</c> and <c>Math.Cos</c>, which are banned from this project
/// because a <c>double</c> transcendental carries no cross-version guarantee.
/// </summary>
/// <remarks>
/// The design bridges this type to a dedicated <c>Bam16</c> wrapper once that
/// type exists; until then the raw <see cref="ushort"/> is the angle
/// representation, matching the task instruction that this file must not take
/// a dependency on a sibling wave-2 task's type.
/// </remarks>
public static class Trig
{
    /// <summary>
    /// One quarter of a full sine wave, sampled at 257 points spaced 64 BAM
    /// units apart (256 intervals across the 16,384-unit quadrant). Entry
    /// <c>i</c> holds <c>round(65536 * sin(pi * i / 512))</c>, computed from
    /// the mathematical definition and pinned as a literal. This table is a
    /// hash contract: changing a single entry moves every angle-dependent
    /// result downstream of it.
    /// </summary>
    private static readonly int[] QuarterWaveSine =
    [
        0, 402, 804, 1206, 1608, 2010, 2412, 2814, 3216, 3617, 4019, 4420, 4821,
        5222, 5623, 6023, 6424, 6824, 7224, 7623, 8022, 8421, 8820, 9218, 9616,
        10014, 10411, 10808, 11204, 11600, 11996, 12391, 12785, 13180, 13573,
        13966, 14359, 14751, 15143, 15534, 15924, 16314, 16703, 17091, 17479,
        17867, 18253, 18639, 19024, 19409, 19792, 20175, 20557, 20939, 21320,
        21699, 22078, 22457, 22834, 23210, 23586, 23961, 24335, 24708, 25080,
        25451, 25821, 26190, 26558, 26925, 27291, 27656, 28020, 28383, 28745,
        29106, 29466, 29824, 30182, 30538, 30893, 31248, 31600, 31952, 32303,
        32652, 33000, 33347, 33692, 34037, 34380, 34721, 35062, 35401, 35738,
        36075, 36410, 36744, 37076, 37407, 37736, 38064, 38391, 38716, 39040,
        39362, 39683, 40002, 40320, 40636, 40951, 41264, 41576, 41886, 42194,
        42501, 42806, 43110, 43412, 43713, 44011, 44308, 44604, 44898, 45190,
        45480, 45769, 46056, 46341, 46624, 46906, 47186, 47464, 47741, 48015,
        48288, 48559, 48828, 49095, 49361, 49624, 49886, 50146, 50404, 50660,
        50914, 51166, 51417, 51665, 51911, 52156, 52398, 52639, 52878, 53114,
        53349, 53581, 53812, 54040, 54267, 54491, 54714, 54934, 55152, 55368,
        55582, 55794, 56004, 56212, 56418, 56621, 56823, 57022, 57219, 57414,
        57607, 57798, 57986, 58172, 58356, 58538, 58718, 58896, 59071, 59244,
        59415, 59583, 59750, 59914, 60075, 60235, 60392, 60547, 60700, 60851,
        60999, 61145, 61288, 61429, 61568, 61705, 61839, 61971, 62101, 62228,
        62353, 62476, 62596, 62714, 62830, 62943, 63054, 63162, 63268, 63372,
        63473, 63572, 63668, 63763, 63854, 63944, 64031, 64115, 64197, 64277,
        64354, 64429, 64501, 64571, 64639, 64704, 64766, 64827, 64884, 64940,
        64993, 65043, 65091, 65137, 65180, 65220, 65259, 65294, 65328, 65358,
        65387, 65413, 65436, 65457, 65476, 65492, 65505, 65516, 65525, 65531,
        65535, 65536,
    ];

    /// <summary>
    /// Sine of <paramref name="bam"/>, at scale 65,536. Reduces the full turn
    /// to a quadrant by inspecting the top two bits of the BAM value, reflects
    /// the odd quadrants by index arithmetic, and interpolates linearly
    /// between the two nearest pinned table entries.
    /// </summary>
    public static int Sin(ushort bam)
    {
        var quadrantIndex = bam & 0x3FFF;
        if ((bam & 0x4000) != 0)
        {
            quadrantIndex = 0x4000 - quadrantIndex;
        }

        var tableIndex = quadrantIndex >> 6;
        var fraction = quadrantIndex & 63;

        var lower = QuarterWaveSine[tableIndex];
        var upper = tableIndex < 256 ? QuarterWaveSine[tableIndex + 1] : lower;
        var magnitude = lower + (((upper - lower) * fraction) >> 6);

        return (bam & 0x8000) != 0 ? -magnitude : magnitude;
    }

    /// <summary>
    /// Cosine of <paramref name="bam"/>, at scale 65,536. Cosine is sine
    /// shifted a quarter turn ahead; the shift wraps for free because BAM
    /// arithmetic is <see cref="ushort"/> arithmetic.
    /// </summary>
    public static int Cos(ushort bam) => Sin(unchecked((ushort)(bam + 0x4000)));
}
