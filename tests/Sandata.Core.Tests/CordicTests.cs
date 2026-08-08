using Sandata.Core.Mathematics;

namespace Sandata.Core.Tests;

/// <summary>
/// Every expected value in this file is derived from the mathematical
/// definition of <c>atan2</c> independently of <see cref="Cordic"/> — either
/// the exact axis/diagonal angle, or <c>round(65536 * atan2(y, x) / (2 *
/// pi))</c> computed offline from double-precision floating point far outside
/// this project's own integer implementation. None of these numbers were read
/// back from a run of the implementation under test. The sweep comparison
/// uses a tolerance, not exact equality, because CORDIC is an approximation
/// by design; the tolerance of two BAM units matches the measured worst case
/// across a full sweep at a representative magnitude, slightly above the
/// design's roughly one-BAM-unit accuracy target.
/// </summary>
public sealed class CordicTests
{
    private const int ToleranceBamUnits = 2;

    [Fact]
    public void AtanTable_SixteenPinnedConstants_MatchTheMathematicalDefinition()
    {
        int[] expected =
        [
            8192, 4836, 2555, 1297, 651, 326, 163, 81, 41, 20, 10, 5, 3, 1, 1, 0,
        ];

        Assert.Equal(expected, Cordic.AtanTable);
    }

    [Theory]
    [InlineData(1, 0, (ushort)0)]
    [InlineData(0, 1, (ushort)16384)]
    [InlineData(-1, 0, (ushort)32768)]
    [InlineData(0, -1, (ushort)49152)]
    [InlineData(1, 1, (ushort)8192)]
    [InlineData(-1, 1, (ushort)24576)]
    [InlineData(-1, -1, (ushort)40960)]
    [InlineData(1, -1, (ushort)57344)]
    public void Atan2_EightAxisAndDiagonalDirections_ExactMatch(long x, long y, ushort expected)
    {
        Assert.Equal(expected, Cordic.Atan2(y, x));
    }

    [Theory]
    [InlineData(100000L, 0L, (ushort)0)]
    [InlineData(96593L, 25882L, (ushort)2731)]
    [InlineData(86603L, 50000L, (ushort)5461)]
    [InlineData(70711L, 70711L, (ushort)8192)]
    [InlineData(50000L, 86603L, (ushort)10923)]
    [InlineData(25882L, 96593L, (ushort)13653)]
    [InlineData(0L, 100000L, (ushort)16384)]
    [InlineData(-25882L, 96593L, (ushort)19115)]
    [InlineData(-50000L, 86603L, (ushort)21845)]
    [InlineData(-70711L, 70711L, (ushort)24576)]
    [InlineData(-86603L, 50000L, (ushort)27307)]
    [InlineData(-96593L, 25882L, (ushort)30037)]
    [InlineData(-100000L, 0L, (ushort)32768)]
    [InlineData(-96593L, -25882L, (ushort)35499)]
    [InlineData(-86603L, -50000L, (ushort)38229)]
    [InlineData(-70711L, -70711L, (ushort)40960)]
    [InlineData(-50000L, -86603L, (ushort)43691)]
    [InlineData(-25882L, -96593L, (ushort)46421)]
    [InlineData(0L, -100000L, (ushort)49152)]
    [InlineData(25882L, -96593L, (ushort)51883)]
    [InlineData(50000L, -86603L, (ushort)54613)]
    [InlineData(70711L, -70711L, (ushort)57344)]
    [InlineData(86603L, -50000L, (ushort)60075)]
    [InlineData(96593L, -25882L, (ushort)62805)]
    public void Atan2_PinnedSweepAtFifteenDegreeSteps_WithinTolerance(long x, long y, ushort expected)
    {
        var actual = Cordic.Atan2(y, x);

        var diff = (short)(actual - expected);
        var error = Math.Abs((int)diff);

        Assert.True(
            error <= ToleranceBamUnits,
            $"x={x}, y={y}: expected={expected}, actual={actual}, error={error} BAM units.");
    }
}
