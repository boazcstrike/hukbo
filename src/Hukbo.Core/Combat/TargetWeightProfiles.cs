namespace Hukbo.Core.Combat;

/// <summary>
/// Shared builders for the hand-authored target-weight and multiplier tables
/// a combat preset declares. Each preset version is a frozen snapshot of its
/// own data — a later version never edits an earlier one's numbers — so only
/// the construction helpers are shared here, never the values themselves.
/// </summary>
internal static class TargetWeightProfiles
{
    /// <summary>
    /// The multiplier a body part carries when a profile does not override
    /// it: no change, expressed in basis points out of 1,000.
    /// </summary>
    internal const int DefaultMultiplierBasisPoints = 1_000;

    /// <summary>
    /// A target-weight profile that takes <paramref name="overrides"/> where
    /// they are given and <paramref name="fallback"/> everywhere else.
    /// </summary>
    internal static TargetWeightProfile FromOverrides(
        TargetWeightProfile fallback,
        IReadOnlyDictionary<BodyPart, int> overrides)
    {
        var entries = new List<(BodyPart Part, int Value)>(
            BodyPartCatalog.Ordered.Length);
        foreach (var part in BodyPartCatalog.Ordered)
        {
            entries.Add((
                part,
                overrides.TryGetValue(part, out var value)
                    ? value
                    : fallback.Get(part)));
        }

        return new TargetWeightProfile(entries);
    }

    /// <summary>
    /// A defense-multiplier profile that takes <paramref name="overrides"/>
    /// where they are given and
    /// <see cref="DefaultMultiplierBasisPoints"/> everywhere else.
    /// </summary>
    internal static TargetWeightProfile FromMultiplierOverrides(
        IReadOnlyDictionary<BodyPart, int> overrides)
    {
        var entries = new List<(BodyPart Part, int Value)>(
            BodyPartCatalog.Ordered.Length);
        foreach (var part in BodyPartCatalog.Ordered)
        {
            entries.Add((
                part,
                overrides.TryGetValue(part, out var value)
                    ? value
                    : DefaultMultiplierBasisPoints));
        }

        return new TargetWeightProfile(entries);
    }
}
