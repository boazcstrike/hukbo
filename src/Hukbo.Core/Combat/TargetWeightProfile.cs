namespace Hukbo.Core.Combat;

/// <summary>
/// Immutable, fully covered per-<see cref="BodyPart"/> integer value table.
/// Used both for general/weapon target weights and for shield defense
/// multipliers (basis points). Every canonical body part must be present
/// exactly once with a nonnegative value; the constructor validates this
/// eagerly so invalid configuration fails at construction, not during an
/// attack.
/// </summary>
public sealed class TargetWeightProfile
{
    private readonly int[] _valuesByPart;

    public TargetWeightProfile(IReadOnlyList<(BodyPart Part, int Value)> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (entries.Count != BodyPartCatalog.Ordered.Length)
        {
            throw new ArgumentException(
                "A target weight profile must define exactly " +
                $"{BodyPartCatalog.Ordered.Length} body parts, but " +
                $"{entries.Count} were provided.",
                nameof(entries));
        }

        var buffer = new int[BodyPartCatalog.Ordered.Length];
        var seen = new bool[BodyPartCatalog.Ordered.Length];

        foreach (var (part, value) in entries)
        {
            var index = IndexOf(part);
            if (seen[index])
            {
                throw new ArgumentException(
                    $"Duplicate target weight entry for body part {part}.",
                    nameof(entries));
            }

            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(entries),
                    value,
                    $"Target weight for {part} must be nonnegative.");
            }

            buffer[index] = value;
            seen[index] = true;
        }

        for (var index = 0; index < seen.Length; index++)
        {
            if (!seen[index])
            {
                throw new ArgumentException(
                    $"Target weight profile is missing body part " +
                    $"{BodyPartCatalog.Ordered[index]}.",
                    nameof(entries));
            }
        }

        _valuesByPart = buffer;
    }

    public int Get(BodyPart part) => _valuesByPart[IndexOf(part)];

    private static int IndexOf(BodyPart part)
    {
        var index = (int)part - 1;
        if (index < 0 || index >= BodyPartCatalog.Ordered.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(part),
                part,
                "Unknown body part.");
        }

        return index;
    }
}
