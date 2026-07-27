using System.Reflection;
using System.Text.RegularExpressions;
using Hukbo.Diagnostics;

namespace Hukbo.Client.Tests;

/// <summary>
/// The <c>ev</c> vocabulary is a machine key, so it has to be unique, stably
/// shaped, and declared in one place. These assertions are what keep it that
/// way once a dozen features have each added an event.
/// </summary>
public sealed partial class LogEventCatalogTests
{
    [Fact]
    public void EveryIdentifierIsUnique()
    {
        var identifiers = GetIdentifiers();
        var duplicates = identifiers
            .GroupBy(entry => entry.Value, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        Assert.Empty(duplicates);
    }

    /// <summary>
    /// <c>noun.verb</c>, lowercase first segment, at least one dot. A value or a
    /// count in an identifier makes it unfilterable, and this is the cheapest
    /// place to catch that.
    /// </summary>
    [Fact]
    public void EveryIdentifierIsLowercaseDotted()
    {
        var offenders = GetIdentifiers()
            .Where(entry => !IdentifierPattern().IsMatch(entry.Value))
            .Select(entry => $"{entry.Name} = '{entry.Value}'")
            .ToArray();

        Assert.Empty(offenders);
    }

    /// <summary>
    /// An identifier's first segment names the channel it belongs to, which is
    /// what makes <c>ev</c> and <c>ch</c> agree on every line and what lets a
    /// reader filter on either one.
    /// </summary>
    /// <remarks>
    /// Declaration order is deliberately not asserted: reflection does not
    /// promise to return fields in source order, and a flaky test is worse than
    /// an unenforced tidiness rule. Keeping each group sorted stays a review
    /// matter.
    /// </remarks>
    [Fact]
    public void EveryIdentifierPrefixNamesADeclaredChannel()
    {
        var validPrefixes = Enum.GetValues<LogChannel>()
            .Where(channel => channel is not (LogChannel.None or LogChannel.All))
            .Select(LogChannels.ToWireName)
            .ToHashSet(StringComparer.Ordinal);

        var offenders = GetIdentifiers()
            .Where(entry => !validPrefixes.Contains(GetPrefix(entry)))
            .Select(entry => $"{entry.Name} = '{entry.Value}'")
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void EveryChannelExceptTheReservedOneHasAtLeastOneIdentifier()
    {
        var prefixes = GetIdentifiers()
            .Select(GetPrefix)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var channel in Enum.GetValues<LogChannel>())
        {
            if (channel is LogChannel.None or LogChannel.All or LogChannel.Ui)
            {
                continue;
            }

            Assert.Contains(LogChannels.ToWireName(channel), prefixes);
        }
    }

    private static string GetPrefix((string Name, string Value) entry) =>
        entry.Value[..entry.Value.IndexOf('.', StringComparison.Ordinal)];

    private static (string Name, string Value)[] GetIdentifiers() =>
        typeof(LogEvents)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (
                field.Name,
                Value: (string)field.GetRawConstantValue()!))
            .ToArray();

    [GeneratedRegex(@"^[a-z]+(\.[a-zA-Z]+)+$")]
    private static partial Regex IdentifierPattern();
}
