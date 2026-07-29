namespace Hukbo.Client.Settings;

internal sealed record ClientSettings(
    int SchemaVersion,
    string SelectedThemeId,
    ArmyComposition Composition,
    GoreIntensity GoreIntensity,
    MotionIntensity MotionIntensity,
    AutoCameraMode AutoCameraMode);

/// <summary>
/// A spectator-chosen army composition for both factions: the total units per
/// team and the count fielded from each of the four ranks combat preset V4
/// rosters, in declared roster-index order — Datu, Maharlika, Timawa, Aliping
/// Namamahay. Persisted only; consumed by the next Full Reset.
/// </summary>
/// <remarks>
/// A category is a roster entry: under V4 every roster entry is exactly one
/// rank's loadout, so a category and a rank now coincide. That was not true
/// under V2, whose six categories were one-handed-weapon grip variants
/// (solo/shielded) rather than ranks — see the first reset below.
/// <para>
/// The shape changed twice over when V2 landed — four counts became six, and
/// their names changed — so no existing settings file can be read forward
/// under any interpretation. The schema version bump makes the store discard
/// the old file and start from <see cref="Default"/>. That is a deliberate
/// reset rather than a migration: the counts are six small integers a
/// spectator re-enters in seconds, there are no shipped installs, and the
/// shape changes again when the shield system arrives.
/// </para>
/// <para>
/// A second deliberate reset followed when the default rose to 250 per team.
/// The shape did not change that time and an old file would still parse, but a
/// saved composition always wins over <see cref="Default"/>, so any existing
/// file would have pinned the army at its old size forever. The schema version
/// bump discards it. The cost is that the theme, gore, motion, and camera
/// choices saved alongside it reset too — accepted on the same grounds as the
/// first reset: a handful of settings re-entered in seconds, and no shipped
/// installs.
/// </para>
/// <para>
/// A third deliberate reset followed when the shipped default combat preset
/// moved from V2 to V4 and the panel's categories moved from six roster
/// entries (grip variants of a weapon) to four ranks. The shape changed again
/// — six counts became four, and every count was renamed from a weapon/grip
/// pair to a rank — so, exactly as with the first reset, no existing settings
/// file can be read forward under any interpretation. Accepted on the same
/// grounds as both earlier resets: a handful of settings re-entered in
/// seconds, and no shipped installs.
/// </para>
/// </remarks>
internal sealed record ArmyComposition(
    int UnitsPerTeam,
    int DatuCount,
    int MaharlikaCount,
    int TimawaCount,
    int AlipingNamamahayCount)
{
    /// <summary>
    /// One entry per roster index in combat preset V4. A test pins this
    /// against the preset's actual roster length.
    /// </summary>
    public const int CategoryCount = 4;

    /// <summary>
    /// 250 per team is 500 units on the field in total, which is the ceiling
    /// <see cref="UI.ArmyCompositionStepper.MaximumUnitsPerTeam"/> allows and
    /// the largest total <c>benchmark.ps1 -Agents 500</c> has measured.
    /// </summary>
    private const int DefaultUnitsPerTeam = 250;

    /// <summary>
    /// 250 does not divide evenly by <see cref="CategoryCount"/>: 250 / 4 is
    /// 62 with a remainder of 2, so the first two roster entries carry one
    /// extra unit each. This matches the remainder-to-lowest-index rule in
    /// <see cref="UI.ArmyCompositionStepper.DistributeEvenly"/>, so Reset to
    /// Default and Distribute Evenly agree on the same total.
    /// </summary>
    private const int DefaultLargerCategoryCount = 63;
    private const int DefaultSmallerCategoryCount = 62;

    public static ArmyComposition Default { get; } = new(
        DefaultUnitsPerTeam,
        DefaultLargerCategoryCount,
        DefaultLargerCategoryCount,
        DefaultSmallerCategoryCount,
        DefaultSmallerCategoryCount);

    /// <summary>
    /// True when every count is non-negative and the four category counts sum
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
    /// The four counts in declared roster-index order.
    /// </summary>
    public int[] CategoryCounts =>
    [
        DatuCount,
        MaharlikaCount,
        TimawaCount,
        AlipingNamamahayCount,
    ];

    private int CategorySum =>
        DatuCount +
        MaharlikaCount +
        TimawaCount +
        AlipingNamamahayCount;
}
