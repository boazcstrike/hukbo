using Sandata.Core.Mathematics;

namespace Sandata.Core.Geometry;

/// <summary>
/// The pinned integer boundary-vector table that <see cref="VisionCone"/>
/// reads its two edge directions from.
/// </summary>
/// <remarks>
/// <para>
/// This table exists because the direction vectors already sitting in
/// <c>Hukbo.Core</c>'s <c>FacingRules</c> cannot be trusted for a cone test.
/// <c>FacingRules</c>' sixteen sector vectors are scaled to 1024 but are not
/// unit length: for example 946² + 392² is 1,048,580 against 1024²'s
/// 1,048,576 (off by 4), while 724² doubled is 1,048,352 (off by 224). The
/// error is a different size for every sector. A cone test written as a
/// cosine comparison — <c>dot(direction, boundary) &gt;= cos(halfWidth) *
/// |direction| * |boundary|</c> — implicitly assumes <c>|boundary|</c> is the
/// same constant for every sector, and silently produces a different cone
/// shape depending on which way the unit faces once that assumption is
/// false. It would also need to multiply two squared magnitudes together to
/// normalise, which overflows <see cref="long"/> at this game's map scale.
/// </para>
/// <para>
/// This table avoids the whole problem by never normalising anything and
/// never taking a dot product. <see cref="BoundaryVector"/> returns a
/// direction vector whose two components come from an independently pinned
/// 257-entry quarter-turn table — literal data computed from the
/// mathematical definition of sine, exactly the discipline
/// <c>Sandata.Core.Mathematics.Trig</c>'s own table follows, but declared
/// here as this file's own data rather than a call into that class. Nothing
/// downstream of this table ever needs the vector's length, only its
/// direction, so an interpolation rounding error of a few raw units changes
/// nothing about which side of a boundary a point falls on except in a band
/// of a handful of raw units either side of the boundary itself, which is
/// exactly the region <see cref="VisionCone"/>'s own boundary tests are
/// written to probe.
/// </para>
/// </remarks>
public static class ConeBoundaryTable
{
    /// <summary>
    /// One quarter of a full sine wave, sampled at 257 points spaced 64 BAM
    /// units apart (256 intervals across the 16,384-unit quadrant). Entry
    /// <c>i</c> holds <c>round(65536 * sin(pi * i / 512))</c>, computed from
    /// the mathematical definition and pinned as a literal — the same
    /// definition <c>Trig.QuarterWaveSine</c> is built from, kept as a
    /// separate array so that <see cref="VisionCone"/>'s boundary vectors
    /// never depend on, and can never be perturbed by, a change to
    /// <c>Trig</c>. Changing a single entry here moves every cone boundary
    /// downstream of it.
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
    /// The direction vector for <paramref name="angle"/>, as <c>(cosine,
    /// sine)</c> at scale 65,536. Angle zero points along <c>(+1, 0)</c>;
    /// increasing <paramref name="angle"/> rotates toward <c>(0, +1)</c> at
    /// the quarter turn, matching this project's <c>+Y</c>-is-screen-down
    /// convention. Only the direction this vector points is a stable
    /// contract — <see cref="VisionCone"/> never reads its magnitude, so an
    /// interpolation rounding error of a few raw units never changes which
    /// side of a boundary a point falls on outside a band of a handful of
    /// raw units either side of the boundary itself.
    /// </summary>
    public static (long X, long Y) BoundaryVector(Bam16 angle) =>
        (CosineComponent(angle.Raw), SineComponent(angle.Raw));

    /// <summary>
    /// Sine of <paramref name="bam"/> at scale 65,536, by the same
    /// quadrant-reflection-and-interpolate technique <c>Trig.Sin</c> uses,
    /// over this file's own independently pinned <see cref="QuarterWaveSine"/>.
    /// </summary>
    private static int SineComponent(ushort bam)
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
    /// Cosine of <paramref name="bam"/> at scale 65,536: sine shifted a
    /// quarter turn ahead, wrapping for free on <see cref="ushort"/>
    /// arithmetic.
    /// </summary>
    private static int CosineComponent(ushort bam) => SineComponent(unchecked((ushort)(bam + 0x4000)));
}
