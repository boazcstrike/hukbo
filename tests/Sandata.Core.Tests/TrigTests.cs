using Sandata.Core.Mathematics;

namespace Sandata.Core.Tests;

/// <summary>
/// Every expected value in this file is derived from the mathematical
/// definition of sine independently of <see cref="Trig"/> — either
/// <c>round(65536 * sin(pi * i / 512))</c> for a table-aligned angle, or the
/// same literal linear-interpolation formula the design specifies, applied by
/// hand to the independently computed table entries for a non-aligned angle.
/// None of these numbers were read back from a run of the implementation
/// under test.
/// </summary>
public sealed class TrigTests
{
    [Fact]
    public void Sin_ExactEndpoints_ZeroAndFullScale()
    {
        Assert.Equal(0, Trig.Sin(0));
        Assert.Equal(65536, Trig.Sin(16384));
    }

    [Fact]
    public void Sin_QuadrantBoundaries_MatchTheFourCardinalValues()
    {
        Assert.Equal(0, Trig.Sin(0));
        Assert.Equal(65536, Trig.Sin(16384));
        Assert.Equal(0, Trig.Sin(32768));
        Assert.Equal(-65536, Trig.Sin(49152));
    }

    [Fact]
    public void Sin_FirstQuadrantTableEntries_AreMonotonicallyNonDecreasing()
    {
        var previous = Trig.Sin(0);

        for (var i = 1; i <= 256; i++)
        {
            var bam = (ushort)(i * 64);
            var current = Trig.Sin(bam);

            Assert.True(
                current >= previous,
                $"Sin regressed between table entries: bam={bam}, previous={previous}, current={current}.");

            previous = current;
        }

        Assert.Equal(65536, previous);
    }

    [Theory]
    [InlineData((ushort)100, 628)]
    [InlineData((ushort)20000, 61636)]
    public void Sin_InterpolatedMidEntries_MatchTheHandAppliedFormula(ushort bam, int expected)
    {
        Assert.Equal(expected, Trig.Sin(bam));
    }

    [Fact]
    public void Cos_IsSineShiftedAQuarterTurn()
    {
        Assert.Equal(65536, Trig.Cos(0));
        Assert.Equal(0, Trig.Cos(16384));
        Assert.Equal(-65536, Trig.Cos(32768));
        Assert.Equal(0, Trig.Cos(49152));
    }
}
