namespace Hukbo.Client.Settings;

internal sealed record ClientSettings(
    int SchemaVersion,
    string SelectedThemeId,
    ArmyComposition Composition,
    GoreIntensity GoreIntensity);

/// <summary>
/// A spectator-chosen army composition for both factions: the total units per
/// team and the count fielded from each of the six roster entries, in declared
/// roster-index order — Kampilan, Wasay, solo Kalis, shielded Kalis, solo
/// Itak, shielded Itak. Persisted only; consumed by the next Full Reset.
/// </summary>
/// <remarks>
/// A category is a roster entry, not a weapon: combat preset V2 fields a solo
/// and a shielded loadout for each one-handed weapon, and the two fight
/// differently, so each needs its own count.
/// <para>
/// The shape changed twice over when V2 landed — four counts became six, and
/// their names changed — so no existing settings file can be read forward
/// under any interpretation. The schema version bump makes the store discard
/// the old file and start from <see cref="Default"/>. That is a deliberate
/// reset rather than a migration: the counts are six small integers a
/// spectator re-enters in seconds, there are no shipped installs, and the
/// shape changes again when the shield system arrives.
/// </para>
/// </remarks>
internal sealed record ArmyComposition(
    int UnitsPerTeam,
    int KampilanCount,
    int WasayCount,
    int KalisSoloCount,
    int KalisShieldedCount,
    int ItakSoloCount,
    int ItakShieldedCount)
{
    /// <summary>
    /// One entry per roster index in combat preset V2. A test pins this
    /// against the preset's actual roster length.
    /// </summary>
    public const int CategoryCount = 6;

    private const int DefaultUnitsPerTeam = 102;
    private const int DefaultCategoryCount = 17;

    public static ArmyComposition Default { get; } = new(
        DefaultUnitsPerTeam,
        DefaultCategoryCount,
        DefaultCategoryCount,
        DefaultCategoryCount,
        DefaultCategoryCount,
        DefaultCategoryCount,
        DefaultCategoryCount);

    /// <summary>
    /// True when every count is non-negative and the six category counts sum
    /// exactly to <see cref="UnitsPerTeam"/>. A persisted composition that
    /// fails this must never be trusted by the store.
    /// </summary>
    public bool IsValid()
    {
        foreach (var count in CategoryCounts)
        {
            if (count < 0)
            {
                return false;
            }
        }

        return UnitsPerTeam >= 0 && CategorySum == UnitsPerTeam;
    }

    /// <summary>
    /// The six counts in declared roster-index order.
    /// </summary>
    public int[] CategoryCounts =>
    [
        KampilanCount,
        WasayCount,
        KalisSoloCount,
        KalisShieldedCount,
        ItakSoloCount,
        ItakShieldedCount,
    ];

    private int CategorySum =>
        KampilanCount +
        WasayCount +
        KalisSoloCount +
        KalisShieldedCount +
        ItakSoloCount +
        ItakShieldedCount;
}
