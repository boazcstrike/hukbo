using Sandata.Core.Mathematics;

namespace Sandata.Core.Tests;

public sealed class IntegerMathTests
{
    // Every case below is hand-computed against the floor-division definition
    // value == divisor * FloorDiv(value, divisor) + FloorMod(value, divisor),
    // with FloorMod carrying the sign of the divisor (or zero) — never the
    // sign of the dividend the way C#'s built-in '%' does.
    [Theory]
    [InlineData(7, 3, 2, 1)] // positive over positive: matches truncating division exactly
    [InlineData(-3, 2, -2, 1)] // the canonical trap: C# truncates -3 / 2 to -1, floor is -2
    [InlineData(-7, 3, -3, 2)] // negative dividend, positive divisor
    [InlineData(7, -3, -3, -2)] // positive dividend, negative divisor
    [InlineData(-7, -3, 2, -1)] // negative over negative: signs match, so it again equals truncating division
    [InlineData(6, -4, -2, -2)] // negative divisor, nonzero remainder needing correction
    public void FloorDiv_And_FloorMod_MatchTheFloorDefinition_ForNegatives(
        int value, int divisor, int expectedFloorDiv, int expectedFloorMod)
    {
        Assert.Equal(expectedFloorDiv, IntegerMath.FloorDiv(value, divisor));
        Assert.Equal(expectedFloorMod, IntegerMath.FloorMod(value, divisor));

        // The defining identity holds regardless of sign.
        Assert.Equal(value, (divisor * expectedFloorDiv) + expectedFloorMod);
    }

    [Theory]
    [InlineData(8, -4, -2)] // exact multiple with a negative divisor: the case a naive sign-only mask gets wrong
    [InlineData(-8, 4, -2)] // exact multiple with a negative dividend
    [InlineData(9, 3, 3)] // exact multiple, both positive
    [InlineData(-9, -3, 3)] // exact multiple, both negative
    public void FloorDiv_ExactMultiples_HaveZeroRemainderRegardlessOfSign(int value, int divisor, int expectedFloorDiv)
    {
        Assert.Equal(expectedFloorDiv, IntegerMath.FloorDiv(value, divisor));
        Assert.Equal(0, IntegerMath.FloorMod(value, divisor));
    }

    [Theory]
    [InlineData(5, 1, 5)]
    [InlineData(-5, 1, -5)]
    [InlineData(5, -1, -5)]
    [InlineData(-5, -1, 5)]
    [InlineData(0, 1, 0)]
    public void FloorDiv_DivisorOfOneOrMinusOne_IsExactWithZeroRemainder(int value, int divisor, int expectedFloorDiv)
    {
        Assert.Equal(expectedFloorDiv, IntegerMath.FloorDiv(value, divisor));
        Assert.Equal(0, IntegerMath.FloorMod(value, divisor));
    }

    [Fact]
    public void FloorDiv_ThrowsOnZeroDivisor()
    {
        Assert.Throws<DivideByZeroException>(() => IntegerMath.FloorDiv(1, 0));
    }

    [Fact]
    public void FloorMod_ThrowsOnZeroDivisor()
    {
        Assert.Throws<DivideByZeroException>(() => IntegerMath.FloorMod(1, 0));
    }
}
