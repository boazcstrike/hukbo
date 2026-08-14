using Hukbo.Core.Movement;

namespace Hukbo.Client.Settings;

internal sealed record ClientSettings(
    int SchemaVersion,
    string SelectedThemeId,
    ArmyComposition Composition,
    GoreIntensity GoreIntensity,
    MotionIntensity MotionIntensity,
    AutoCameraMode AutoCameraMode,
    UiScale UiScale,
    StartupDisplayMode StartupDisplayMode,
    MovementPresetId MovementPreset,
    UiChromeStyle UiChromeStyle = UiChromeStyle.Procedural);

/// <summary>
/// A spectator-chosen army composition for both factions: the total units per
/// team and the count fielded from each of four military ranks — Datu,
/// Maharlika, Timawa, Aliping Namamahay — regardless of which combat preset
/// is active. Persisted only; consumed by the next Full Reset.
/// </summary>
/// <remarks>
/// A category is a rank, not a roster row: under V4 every roster row is
/// exactly one rank's loadout, so a category and a roster row happen to
/// coincide too. That stopped being true when V5 and then RU-45 gave Timawa
/// five roster rows and Aliping Namamahay two;
/// <c>ArenaGame.ExpandCompositionToRosterCounts</c> (RU-43) is what spreads
/// one rank's slider count across every roster row that carries that rank,
/// using the calibrated per-row weights RU-24 and RU-45 measured, so the
/// four sliders below still mean something under a preset whose roster
/// outgrew them. That was not true under V2 either, whose six categories were
/// one-handed-weapon grip variants (solo/shielded) rather than ranks — see
/// the first reset below.
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
    /// One entry per rank the spectator can dial, not one entry per
    /// combat-preset roster row: four ranks, always, regardless of how many
    /// roster rows a preset fields per rank. Under V4 a rank and a roster row
    /// happen to coincide 1:1, which is what the pinning test below checks;
    /// under V5 a rank can span several roster rows (RU-43), and
    /// <c>ArenaGame.ExpandCompositionToRosterCounts</c> is what spreads one
    /// rank's count across all of that rank's rows. A test pins this constant
    /// against V4's roster length, the one preset where the two counts are
    /// equal.
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
    /// The four counts in rank order — Datu, Maharlika, Timawa, Aliping
    /// Namamahay. Under V4 this also happens to be roster-index order; under
    /// V5 it is not, and <c>ArenaGame.ExpandCompositionToRosterCounts</c>
    /// (RU-43) is what maps each of these four counts onto that preset's own
    /// roster rows.
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
