using Hukbo.Core.Mathematics;

namespace Hukbo.Core.Tests;

/// <summary>
/// Golden vectors for <see cref="FixedPoint"/>'s multiply, divide, and square
/// root, added by 2026-08-07-sandata-scaffold task 2. Every expected value
/// here was computed independently of the implementation, by hand-tracing
/// the algorithm in the design document, not read back from the code under
/// test.
/// </summary>
public sealed class FixedPointArithmeticTests
{
    // --- Multiply ------------------------------------------------------

    [Fact]
    public void Multiply_PositiveTimesPositive_ScalesRawProductByScale()
    {
        // 2.0 * 3.0 = 6.0, raw: 2_048 * 3_072 = 6_291_456, / 1_024 = 6_144.
        var left = FixedPoint.FromRaw(2_048);
        var right = FixedPoint.FromRaw(3_072);

        Assert.Equal(6_144, (left * right).RawValue);
    }

    [Fact]
    public void Multiply_NegativeTimesNegative_IsPositive()
    {
        var left = FixedPoint.FromRaw(-2_048);
        var right = FixedPoint.FromRaw(-3_072);

        Assert.Equal(6_144, (left * right).RawValue);
    }

    [Fact]
    public void Multiply_PositiveTimesNegative_IsNegative()
    {
        var left = FixedPoint.FromRaw(2_048);
        var right = FixedPoint.FromRaw(-3_072);

        Assert.Equal(-6_144, (left * right).RawValue);
    }

    [Fact]
    public void Multiply_ExactHalf_TruncatesTowardZero_Positive()
    {
        // Raw product 3 * 512 = 1_536. 1_536 / 1_024 = 1.5 exactly, which
        // truncates to 1, not rounds to 2.
        var left = FixedPoint.FromRaw(3);
        var right = FixedPoint.FromRaw(512);

        Assert.Equal(1, (left * right).RawValue);
    }

    [Fact]
    public void Multiply_ExactHalf_TruncatesTowardZero_Negative()
    {
        // Raw product -3 * 512 = -1_536. -1_536 / 1_024 = -1.5 exactly, which
        // truncates to -1, not -2.
        var left = FixedPoint.FromRaw(-3);
        var right = FixedPoint.FromRaw(512);

        Assert.Equal(-1, (left * right).RawValue);
    }

    [Fact]
    public void Multiply_AtTheIntBoundary_DoesNotThrow()
    {
        // int.MaxValue raw units times exactly 1.0 (raw 1_024) divides back
        // out to int.MaxValue exactly, with no overflow.
        var left = FixedPoint.FromRaw(int.MaxValue);
        var right = FixedPoint.FromRaw(1_024);

        Assert.Equal(int.MaxValue, (left * right).RawValue);
    }

    [Fact]
    public void Multiply_OneRawUnitBeyondTheIntBoundary_Throws()
    {
        // int.MaxValue raw units times one raw unit more than 1.0 pushes the
        // truncated quotient to 2_149_580_798, which does not fit in int.
        var left = FixedPoint.FromRaw(int.MaxValue);
        var right = FixedPoint.FromRaw(1_025);

        Assert.Throws<OverflowException>(() => left * right);
    }

    // --- Divide ----------------------------------------------------------

    [Fact]
    public void Divide_PositiveByPositive_ScalesNumeratorFirst()
    {
        // 0.5 / 1.0 = 0.5, raw: (512 * 1_024) / 1_024 = 512.
        var left = FixedPoint.FromRaw(512);
        var right = FixedPoint.FromRaw(1_024);

        Assert.Equal(512, (left / right).RawValue);
    }

    [Fact]
    public void Divide_NegativeByPositive_IsNegative()
    {
        var left = FixedPoint.FromRaw(-512);
        var right = FixedPoint.FromRaw(1_024);

        Assert.Equal(-512, (left / right).RawValue);
    }

    [Fact]
    public void Divide_NonExactQuotient_TruncatesTowardZero_Positive()
    {
        // Scaled numerator 1 * 1_024 = 1_024. 1_024 / 3 = 341.33..., which
        // truncates to 341, not rounds to 341 or 342.
        var left = FixedPoint.FromRaw(1);
        var right = FixedPoint.FromRaw(3);

        Assert.Equal(341, (left / right).RawValue);
    }

    [Fact]
    public void Divide_NonExactQuotient_TruncatesTowardZero_Negative()
    {
        // Scaled numerator -1 * 1_024 = -1_024. -1_024 / 3 = -341.33...,
        // which truncates to -341, not -342.
        var left = FixedPoint.FromRaw(-1);
        var right = FixedPoint.FromRaw(3);

        Assert.Equal(-341, (left / right).RawValue);
    }

    [Fact]
    public void Divide_ByZero_Throws()
    {
        var left = FixedPoint.FromWhole(1);
        var right = FixedPoint.Zero;

        Assert.Throws<DivideByZeroException>(() => left / right);
    }

    [Fact]
    public void Divide_AtTheIntBoundary_DoesNotThrow()
    {
        // int.MaxValue raw units divided by exactly 1.0 (raw 1_024) returns
        // int.MaxValue exactly, with no overflow.
        var left = FixedPoint.FromRaw(int.MaxValue);
        var right = FixedPoint.FromRaw(1_024);

        Assert.Equal(int.MaxValue, (left / right).RawValue);
    }

    [Fact]
    public void Divide_OneRawUnitBeyondTheIntBoundary_Throws()
    {
        // int.MaxValue raw units divided by one raw unit less than 1.0 pushes
        // the truncated quotient to 2_149_582_849, which does not fit in int.
        var left = FixedPoint.FromRaw(int.MaxValue);
        var right = FixedPoint.FromRaw(1_023);

        Assert.Throws<OverflowException>(() => left / right);
    }

    // --- Sqrt --------------------------------------------------------------

    [Fact]
    public void Sqrt_OfZero_IsZero()
    {
        Assert.Equal(0, FixedPoint.Sqrt(FixedPoint.Zero).RawValue);
    }

    [Fact]
    public void Sqrt_OfAPerfectSquare_IsExact()
    {
        // Sqrt(1.0) == 1.0 exactly: (1_024 * 1_024) is a perfect square.
        var value = FixedPoint.FromRaw(1_024);

        Assert.Equal(1_024, FixedPoint.Sqrt(value).RawValue);
    }

    [Fact]
    public void Sqrt_OfALargerPerfectSquare_IsExact()
    {
        // Sqrt(4.0) == 2.0 exactly: (4_096 * 1_024) is a perfect square.
        var value = FixedPoint.FromRaw(4_096);

        Assert.Equal(2_048, FixedPoint.Sqrt(value).RawValue);
    }

    [Fact]
    public void Sqrt_OfANonPerfectSquare_FloorsToTheNearestRawUnit()
    {
        // Sqrt(2.0): (2_048 * 1_024) = 2_097_152, whose exact integer square
        // root is 1_448 (1_448^2 = 2_096_704 <= 2_097_152 < 2_099_601 = 1_449^2).
        var value = FixedPoint.FromRaw(2_048);

        Assert.Equal(1_448, FixedPoint.Sqrt(value).RawValue);
    }

    [Fact]
    public void Sqrt_OfTheLargestRepresentableInput_DoesNotOverflow()
    {
        // (int.MaxValue * 1_024) = 2_199_023_254_528, whose exact integer
        // square root is 1_482_910, comfortably inside int range.
        var value = FixedPoint.FromRaw(int.MaxValue);

        Assert.Equal(1_482_910, FixedPoint.Sqrt(value).RawValue);
    }

    [Fact]
    public void Sqrt_OfANegativeValue_Throws()
    {
        var value = FixedPoint.FromRaw(-1);

        Assert.Throws<OverflowException>(() => FixedPoint.Sqrt(value));
    }
}
