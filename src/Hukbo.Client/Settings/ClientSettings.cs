namespace Hukbo.Client.Settings;

internal sealed record ClientSettings(
    int SchemaVersion,
    string SelectedThemeId,
    ArmyComposition Composition,
    GoreIntensity GoreIntensity);

/// <summary>
/// A spectator-chosen army composition for both factions: the total units per
/// team and the count fielded from each of the four weapon categories in
/// declared roster-index order (Great Blade, Heavy Chopper, Thrusting Blade,
/// Work Blade). Persisted only; consumed by the next Full Reset.
/// </summary>
internal sealed record ArmyComposition(
    int UnitsPerTeam,
    int GreatBladeCount,
    int HeavyChopperCount,
    int ThrustingBladeCount,
    int WorkBladeCount)
{
    private const int DefaultUnitsPerTeam = 100;
    private const int DefaultCategoryCount = 25;

    public static ArmyComposition Default { get; } = new(
        DefaultUnitsPerTeam,
        DefaultCategoryCount,
        DefaultCategoryCount,
        DefaultCategoryCount,
        DefaultCategoryCount);

    /// <summary>
    /// True when every count is non-negative and the four category counts sum
    /// exactly to <see cref="UnitsPerTeam"/>. A persisted composition that
    /// fails this must never be trusted by the store.
    /// </summary>
    public bool IsValid()
    {
        if (UnitsPerTeam < 0 ||
            GreatBladeCount < 0 ||
            HeavyChopperCount < 0 ||
            ThrustingBladeCount < 0 ||
            WorkBladeCount < 0)
        {
            return false;
        }

        var sum = GreatBladeCount +
            HeavyChopperCount +
            ThrustingBladeCount +
            WorkBladeCount;
        return sum == UnitsPerTeam;
    }
}
