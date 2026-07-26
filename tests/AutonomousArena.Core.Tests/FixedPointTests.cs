using AutonomousArena.Core.Mathematics;

namespace AutonomousArena.Core.Tests;

public sealed class FixedPointTests
{
    [Fact]
    public void FromWhole_UsesExplicitScale()
    {
        var value = FixedPoint.FromWhole(2);

        Assert.Equal(2_048, value.RawValue);
        Assert.Equal(2.0, value.ToDouble());
    }

    [Fact]
    public void Operators_PreserveRawIntegerArithmetic()
    {
        var left = FixedPoint.FromWhole(4);
        var right = FixedPoint.FromRaw(512);

        Assert.Equal(4_608, (left + right).RawValue);
        Assert.Equal(3_584, (left - right).RawValue);
        Assert.Equal(2_304, FixedPoint.MultiplyRatio(left + right, 1, 2).RawValue);
    }

    [Fact]
    public void SquaredDistance_UsesWideCheckedArithmetic()
    {
        var distance = FixedPoint.SquaredDistance(
            FixedPoint.FromWhole(1),
            FixedPoint.FromWhole(2),
            FixedPoint.FromWhole(4),
            FixedPoint.FromWhole(6));

        Assert.Equal(25L * FixedPoint.Scale * FixedPoint.Scale, distance);
    }

    [Fact]
    public void MultiplyRatio_RejectsZeroDenominator()
    {
        Assert.Throws<DivideByZeroException>(
            () => FixedPoint.MultiplyRatio(FixedPoint.FromWhole(1), 1, 0));
    }
}
