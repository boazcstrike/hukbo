namespace Hukbo.Core.Mathematics;

/// <summary>
/// Signed Q22.10-style world value. One logical unit equals 1,024 raw units.
/// </summary>
public readonly record struct FixedPoint : IComparable<FixedPoint>
{
    public const int Scale = 1_024;

    private FixedPoint(int rawValue)
    {
        RawValue = rawValue;
    }

    public int RawValue { get; }

    public static FixedPoint Zero => default;

    public static FixedPoint FromRaw(int rawValue) => new(rawValue);

    public static FixedPoint FromWhole(int value) => new(checked(value * Scale));

    public double ToDouble() => (double)RawValue / Scale;

    public int CompareTo(FixedPoint other) => RawValue.CompareTo(other.RawValue);

    public static FixedPoint MultiplyRatio(
        FixedPoint value,
        int numerator,
        int denominator)
    {
        if (denominator == 0)
        {
            throw new DivideByZeroException();
        }

        var result = checked((long)value.RawValue * numerator / denominator);
        return FromRaw(checked((int)result));
    }

    public static long SquaredDistance(
        FixedPoint leftX,
        FixedPoint leftY,
        FixedPoint rightX,
        FixedPoint rightY)
    {
        var deltaX = (long)rightX.RawValue - leftX.RawValue;
        var deltaY = (long)rightY.RawValue - leftY.RawValue;
        return checked((deltaX * deltaX) + (deltaY * deltaY));
    }

    /// <summary>
    /// The largest non-negative integer whose square does not exceed
    /// <paramref name="value"/>, by the bitwise restoring algorithm, so the
    /// result is exact and identical on every platform. Rejects a negative
    /// input through the checked unsigned cast. Lifted verbatim from the
    /// former private helper on <c>BattleSimulation</c> so route arithmetic
    /// outside that type can share it; the lift is hash-neutral because this
    /// type has no content hash of its own.
    /// </summary>
    internal static long IntegerSquareRoot(long value)
    {
        var remainder = checked((ulong)value);
        ulong root = 0;
        var bit = 1UL << 62;

        while (bit > remainder)
        {
            bit >>= 2;
        }

        while (bit != 0)
        {
            if (remainder >= root + bit)
            {
                remainder -= root + bit;
                root = (root >> 1) + bit;
            }
            else
            {
                root >>= 1;
            }

            bit >>= 2;
        }

        return checked((long)root);
    }

    public static FixedPoint operator +(FixedPoint left, FixedPoint right) =>
        FromRaw(checked(left.RawValue + right.RawValue));

    public static FixedPoint operator -(FixedPoint left, FixedPoint right) =>
        FromRaw(checked(left.RawValue - right.RawValue));

    public static bool operator <(FixedPoint left, FixedPoint right) =>
        left.RawValue < right.RawValue;

    public static bool operator >(FixedPoint left, FixedPoint right) =>
        left.RawValue > right.RawValue;

    public static bool operator <=(FixedPoint left, FixedPoint right) =>
        left.RawValue <= right.RawValue;

    public static bool operator >=(FixedPoint left, FixedPoint right) =>
        left.RawValue >= right.RawValue;
}
