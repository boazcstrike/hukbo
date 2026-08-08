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

    /// <summary>
    /// Multiplies two fixed-point values: the wide raw product is divided by
    /// <see cref="Scale"/> and cast back to <see langword="int"/>.
    /// <para>
    /// Behavioural contract: the division by <see cref="Scale"/> is ordinary
    /// C# integer division, which truncates toward zero rather than rounding.
    /// A fractional raw result of exactly one half truncates toward zero the
    /// same as any other fraction — for example a raw product of 1,536 over a
    /// scale of 1,024 truncates to 1, and a raw product of -1,536 truncates to
    /// -1, not -2.
    /// </para>
    /// <para>
    /// The whole expression is <see langword="checked"/>, so a result whose
    /// magnitude does not fit in <see langword="int"/> throws
    /// <see cref="OverflowException"/> rather than wrapping silently.
    /// </para>
    /// </summary>
    public static FixedPoint operator *(FixedPoint left, FixedPoint right) =>
        FromRaw(checked((int)((long)left.RawValue * right.RawValue / Scale)));

    /// <summary>
    /// Divides two fixed-point values: the left raw value is scaled up by
    /// <see cref="Scale"/> before dividing by the right raw value, so the
    /// result stays in the same fixed-point representation.
    /// <para>
    /// Behavioural contract: the division is ordinary C# integer division,
    /// which truncates toward zero rather than rounding. A quotient whose
    /// exact value would end in one half truncates toward zero the same as
    /// any other fraction — for example scaled numerator 1,024 divided by 3
    /// truncates to 341, and -1,024 divided by 3 truncates to -341, not -342.
    /// </para>
    /// <para>
    /// The whole expression is <see langword="checked"/>, so a result whose
    /// magnitude does not fit in <see langword="int"/> throws
    /// <see cref="OverflowException"/>. Dividing by a <paramref name="right"/>
    /// whose <see cref="RawValue"/> is zero throws
    /// <see cref="DivideByZeroException"/> — the CLR's own integer division
    /// raises this on its own, and it is never silently clamped or treated as
    /// infinity.
    /// </para>
    /// </summary>
    public static FixedPoint operator /(FixedPoint left, FixedPoint right) =>
        FromRaw(checked((int)(((long)left.RawValue * Scale) / right.RawValue)));

    /// <summary>
    /// The non-negative square root of <paramref name="value"/>, exact to the
    /// nearest raw unit. Wraps <see cref="IntegerSquareRoot"/> — the same
    /// exact bitwise restoring algorithm the collision code already relies
    /// on — pre-multiplying by <see cref="Scale"/> first so the returned
    /// value stays in the same Q22.10 representation rather than losing all
    /// of its fractional precision.
    /// <para>
    /// A negative <paramref name="value"/> is rejected: the pre-multiplied
    /// input is negative, and the checked unsigned cast inside
    /// <see cref="IntegerSquareRoot"/> throws <see cref="OverflowException"/>
    /// rather than returning a meaningless result.
    /// </para>
    /// </summary>
    public static FixedPoint Sqrt(FixedPoint value) =>
        FromRaw(checked((int)IntegerSquareRoot((long)value.RawValue * Scale)));
}
