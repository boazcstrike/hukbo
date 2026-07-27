using System.Globalization;
using System.Text;

namespace Hukbo.Diagnostics;

/// <summary>The JSON type a <see cref="LogValue"/> serializes as.</summary>
public enum LogValueKind
{
    /// <summary>Serializes as JSON <c>null</c>.</summary>
    Null = 0,

    /// <summary>Serializes as a JSON number without a fractional part.</summary>
    Integer = 1,

    /// <summary>Serializes as a JSON number with a fractional part.</summary>
    Real = 2,

    /// <summary>Serializes as JSON <c>true</c> or <c>false</c>.</summary>
    Boolean = 3,

    /// <summary>Serializes as an escaped JSON string.</summary>
    Text = 4,
}

/// <summary>
/// One payload value on a log line: a discriminated union over the four JSON
/// scalar types.
/// </summary>
/// <remarks>
/// A struct, and deliberately not <c>object</c>. Debug-level call sites run
/// inside the frame loop, and boxing an integer per field per cue would spend
/// the per-tick allocation budget on diagnostics. The implicit conversions keep
/// call sites reading like ordinary arguments while every numeric path stays on
/// the stack.
/// </remarks>
public readonly struct LogValue
{
    private readonly long _integer;
    private readonly double _real;
    private readonly string? _text;

    private LogValue(LogValueKind kind, long integer, double real, string? text)
    {
        Kind = kind;
        _integer = integer;
        _real = real;
        _text = text;
    }

    /// <summary>The JSON type this value serializes as.</summary>
    public LogValueKind Kind { get; }

    /// <summary>A JSON <c>null</c> payload value.</summary>
    public static LogValue Null { get; } =
        new(LogValueKind.Null, 0, 0, null);

    public static implicit operator LogValue(int value) =>
        new(LogValueKind.Integer, value, 0, null);

    public static implicit operator LogValue(long value) =>
        new(LogValueKind.Integer, value, 0, null);

    public static implicit operator LogValue(ulong value) =>
        new(LogValueKind.Integer, unchecked((long)value), 0, null);

    public static implicit operator LogValue(float value) =>
        new(LogValueKind.Real, 0, value, null);

    public static implicit operator LogValue(double value) =>
        new(LogValueKind.Real, 0, value, null);

    public static implicit operator LogValue(bool value) =>
        new(LogValueKind.Boolean, value ? 1 : 0, 0, null);

    public static implicit operator LogValue(string? value) =>
        value is null
            ? Null
            : new LogValue(LogValueKind.Text, 0, 0, value);

    /// <summary>
    /// Named alternative to the <c>int</c> conversion operator, for callers
    /// that prefer an explicit conversion. Required by CA2225.
    /// </summary>
    public static LogValue FromInt32(int value) => value;

    /// <inheritdoc cref="FromInt32" />
    public static LogValue FromInt64(long value) => value;

    /// <inheritdoc cref="FromInt32" />
    public static LogValue FromUInt64(ulong value) => value;

    /// <inheritdoc cref="FromInt32" />
    public static LogValue FromSingle(float value) => value;

    /// <inheritdoc cref="FromInt32" />
    public static LogValue FromDouble(double value) => value;

    /// <inheritdoc cref="FromInt32" />
    public static LogValue FromBoolean(bool value) => value;

    /// <inheritdoc cref="FromInt32" />
    public static LogValue FromString(string? value) => value;

    /// <summary>
    /// Appends this value as JSON. A real number is written with round-trip
    /// precision under the invariant culture, so a log written on a machine
    /// with a comma decimal separator still parses.
    /// </summary>
    internal void AppendTo(StringBuilder builder)
    {
        switch (Kind)
        {
            case LogValueKind.Integer:
                builder.Append(
                    _integer.ToString(CultureInfo.InvariantCulture));
                break;

            case LogValueKind.Real:
                if (double.IsFinite(_real))
                {
                    builder.Append(
                        _real.ToString("R", CultureInfo.InvariantCulture));
                }
                else
                {
                    // JSON has no infinity or NaN literal. A quoted token keeps
                    // the line parseable and keeps the anomaly visible instead
                    // of rounding it away to zero.
                    builder.Append('"')
                        .Append(
                            _real.ToString("R", CultureInfo.InvariantCulture))
                        .Append('"');
                }

                break;

            case LogValueKind.Boolean:
                builder.Append(_integer != 0 ? "true" : "false");
                break;

            case LogValueKind.Text:
                JsonText.AppendQuoted(builder, _text!);
                break;

            default:
                builder.Append("null");
                break;
        }
    }
}
