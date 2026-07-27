using System.Globalization;
using System.Text;

namespace Hukbo.Diagnostics;

/// <summary>
/// The one place JSON string escaping happens. Small enough to own outright,
/// which keeps the sink free of a serializer dependency and keeps the escaping
/// rules readable in a single screen.
/// </summary>
internal static class JsonText
{
    /// <summary>
    /// Appends <paramref name="value"/> as a quoted, escaped JSON string.
    /// Escapes the two mandatory characters, the five shorthand control
    /// characters, and every remaining control character as <c>\u00XX</c>.
    /// </summary>
    public static void AppendQuoted(StringBuilder builder, string value)
    {
        builder.Append('"');
        foreach (var character in value)
        {
            switch (character)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (character < ' ')
                    {
                        builder.Append("\\u")
                            .Append(((int)character).ToString(
                                "x4",
                                CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(character);
                    }

                    break;
            }
        }

        builder.Append('"');
    }
}
