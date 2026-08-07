using Hukbo.Core.Movement;
using Sandata.Core.Mathematics;

namespace Sandata.Core.Tests;

public sealed class Bam16Tests
{
    [Theory]
    [InlineData(Facing16.East)]
    [InlineData(Facing16.EastSouthEast)]
    [InlineData(Facing16.SouthEast)]
    [InlineData(Facing16.SouthSouthEast)]
    [InlineData(Facing16.South)]
    [InlineData(Facing16.SouthSouthWest)]
    [InlineData(Facing16.SouthWest)]
    [InlineData(Facing16.WestSouthWest)]
    [InlineData(Facing16.West)]
    [InlineData(Facing16.WestNorthWest)]
    [InlineData(Facing16.NorthWest)]
    [InlineData(Facing16.NorthNorthWest)]
    [InlineData(Facing16.North)]
    [InlineData(Facing16.NorthNorthEast)]
    [InlineData(Facing16.NorthEast)]
    [InlineData(Facing16.EastNorthEast)]
    public void FromFacing16_ThenToFacing16_RoundTripsEverySector(Facing16 facing)
    {
        var angle = Bam16.FromFacing16(facing);

        Assert.Equal(facing, angle.ToFacing16());
    }

    [Theory]
    [InlineData(Facing16.East, 0)]
    [InlineData(Facing16.EastSouthEast, 4_096)]
    [InlineData(Facing16.SouthEast, 8_192)]
    [InlineData(Facing16.SouthSouthEast, 12_288)]
    [InlineData(Facing16.South, 16_384)]
    [InlineData(Facing16.SouthSouthWest, 20_480)]
    [InlineData(Facing16.SouthWest, 24_576)]
    [InlineData(Facing16.WestSouthWest, 28_672)]
    [InlineData(Facing16.West, 32_768)]
    [InlineData(Facing16.WestNorthWest, 36_864)]
    [InlineData(Facing16.NorthWest, 40_960)]
    [InlineData(Facing16.NorthNorthWest, 45_056)]
    [InlineData(Facing16.North, 49_152)]
    [InlineData(Facing16.NorthNorthEast, 53_248)]
    [InlineData(Facing16.NorthEast, 57_344)]
    [InlineData(Facing16.EastNorthEast, 61_440)]
    public void FromFacing16_UsesFourThousandNinetySixUnitsPerSector(Facing16 facing, ushort expectedRaw)
    {
        var angle = Bam16.FromFacing16(facing);

        Assert.Equal(expectedRaw, angle.Raw);
    }

    [Fact]
    public void FromFacing16_RejectsNone()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Bam16.FromFacing16(Facing16.None));
    }

    [Theory]
    [InlineData(2_048, Facing16.EastSouthEast)] // exact half-way between East (0) and EastSouthEast (4096)
    [InlineData(63_488, Facing16.East)] // exact half-way between EastNorthEast (61440) and East, wrapping past 65536
    public void ToFacing16_PinsTheExactHalfTieUpward(ushort raw, Facing16 expected)
    {
        var angle = new Bam16(raw);

        Assert.Equal(expected, angle.ToFacing16());
    }

    [Theory]
    [InlineData(2_047, Facing16.East)] // one unit short of the East/EastSouthEast tie: stays at East
    [InlineData(63_487, Facing16.EastNorthEast)] // one unit short of the wrap tie: stays at EastNorthEast
    public void ToFacing16_OneUnitBelowTheHalfTieRoundsDown(ushort raw, Facing16 expected)
    {
        var angle = new Bam16(raw);

        Assert.Equal(expected, angle.ToFacing16());
    }

    [Theory]
    [InlineData(16_000, 16_800, 800)] // crosses the 16,384 quadrant boundary
    [InlineData(32_500, 33_100, 600)] // crosses the 32,768 quadrant boundary
    [InlineData(49_000, 49_400, 400)] // crosses the 49,152 quadrant boundary
    public void ShortestArc_CrossesEachInteriorQuadrantBoundary(ushort from, ushort to, short expectedArc)
    {
        Assert.Equal(expectedArc, Bam16.ShortestArc(new Bam16(from), new Bam16(to)));
    }

    [Fact]
    public void ShortestArc_WrapsForwardThroughZero()
    {
        // 65,400 to 200 the short way is +336, wrapping forward through the 0/65,536 boundary.
        var arc = Bam16.ShortestArc(new Bam16(65_400), new Bam16(200));

        Assert.Equal((short)336, arc);
    }

    [Fact]
    public void ShortestArc_WrapsBackwardThroughZero()
    {
        // 200 to 65,400 the short way is -336, wrapping backward through the 0/65,536 boundary —
        // the mirror image of ShortestArc_WrapsForwardThroughZero, proving both wrap directions.
        var arc = Bam16.ShortestArc(new Bam16(200), new Bam16(65_400));

        Assert.Equal((short)(-336), arc);
    }

    [Fact]
    public void ShortestArc_AtExactHalfTurnLandsOnNegativeBoundary()
    {
        var arc = Bam16.ShortestArc(new Bam16(0), new Bam16(32_768));

        Assert.Equal(short.MinValue, arc);
    }

    [Fact]
    public void ShortestArc_IsZeroForIdenticalAngles()
    {
        var arc = Bam16.ShortestArc(new Bam16(12_345), new Bam16(12_345));

        Assert.Equal((short)0, arc);
    }
}
