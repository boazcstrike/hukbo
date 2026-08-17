using Hukbo.Core.Movement;

namespace Hukbo.Client.Settings;

internal sealed record ClientSettings(
    int SchemaVersion,
    string SelectedThemeId,
    ArmyComposition Composition,
    ArmorComposition ArmorComposition,
    GoreIntensity GoreIntensity,
    MotionIntensity MotionIntensity,
    AutoCameraMode AutoCameraMode,
    UiScale UiScale,
    StartupDisplayMode StartupDisplayMode,
    MovementPresetId MovementPreset,
    UiChromeStyle UiChromeStyle = UiChromeStyle.Procedural,
    PawnVisualStyle PawnVisualStyle = PawnVisualStyle.Procedural,
    WeaponVisualStyle WeaponVisualStyle = WeaponVisualStyle.Procedural);

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
/// <para>
/// A fourth deliberate reset followed when the default moved off an even
/// four-way split onto the calibrated per-rank proportions recorded on
/// <see cref="ArmyComposition.Default"/>. The shape did not change and an
/// old file would still parse, but as with the second reset a saved
/// composition always wins over <see cref="Default"/>, so an existing file
/// would have pinned the army at the old even split forever. The schema
/// version bump discards it, on the same grounds as all three earlier
/// resets: a handful of settings re-entered in seconds, and no shipped
/// installs.
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
    /// Calibrated per-rank shares taken on 2026-08-14 — roughly 19% Datu,
    /// 19% Maharlika, 44% Timawa, 18% Aliping Namamahay — applied to a
    /// 250-unit team as 48 / 47 / 110 / 45, which sums to 250. This replaces
    /// an even four-way split: the previous default of 63 / 63 / 62 / 62 was
    /// chosen only so the four counts summed to 250 under the
    /// remainder-to-lowest-index rule in
    /// <see cref="UI.ArmyCompositionStepper.DistributeEvenly"/>, not because
    /// an even split was judged representative of anything, so Reset to
    /// Default and Distribute Evenly no longer agree on distribution, only
    /// on total. These counts are a gameplay tuning value with no
    /// evidentiary confidence behind them: PROVISIONAL under CLAUDE.md
    /// section 7, and never to be presented as a historical measurement of
    /// pre-colonial Philippine army composition.
    /// </summary>
    private const int DefaultDatuCount = 48;
    private const int DefaultMaharlikaCount = 47;
    private const int DefaultTimawaCount = 110;
    private const int DefaultAlipingNamamahayCount = 45;

    public static ArmyComposition Default { get; } = new(
        DefaultUnitsPerTeam,
        DefaultDatuCount,
        DefaultMaharlikaCount,
        DefaultTimawaCount,
        DefaultAlipingNamamahayCount);

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

/// <summary>
/// The spectator-chosen share of each armor baseline fielded by both
/// factions, expressed as five percentages that always sum to 100.
/// </summary>
/// <remarks>
/// Combat preset V7 is the first preset whose roster fields more than one
/// <see cref="Hukbo.Core.Combat.ArmorId"/> baseline per (weapon, shield,
/// rank) loadout; <c>ArenaGame.ExpandCompositionToRosterCounts</c>'s
/// three-argument overload is what spends these five percentages, splitting
/// each base loadout's <see cref="ArmyComposition"/>-derived count across
/// its five armor-variant roster rows in the same order this record lists
/// them: None, QuiltedCotton, HideCorselet, RigidCuirass, ImportedIron.
/// Every preset up to and including V6 fields exactly one armor baseline
/// per loadout, so this record has nothing to spend against those rosters,
/// and the two-argument overload — which never reads an
/// <see cref="ArmorComposition"/> at all — runs instead.
/// <para>
/// Both factions field the identical armor share: a scenario's roster
/// counts are one array shared by the whole scenario rather than one array
/// per faction, so the mirroring <see cref="ArmyComposition"/>'s own remarks
/// already describe for rank composition applies to armor composition for
/// the same structural reason.
/// </para>
/// <para>
/// <b>The default percentages below are a PROVISIONAL gameplay-tuning
/// choice under CLAUDE.md section 7, never a historical measurement.</b>
/// Question 11 of docs/research/HISTORICAL_1500s_ARMOR.md's open-questions
/// section states plainly that no source in the record gives a proportion
/// of fighters who wore body armor, and that both "armor was normal" and
/// "armor was rare" go beyond what any source actually says. These
/// percentages exist only so the game can field a mix of armor at all; they
/// carry no evidentiary weight and must never be presented as a historical
/// measurement of pre-colonial Philippine armor prevalence.
/// </para>
/// </remarks>
internal sealed record ArmorComposition(
    int NonePercent,
    int QuiltedCottonPercent,
    int HideCorseletPercent,
    int RigidCuirassPercent,
    int ImportedIronPercent)
{
    /// <summary>
    /// One entry per armor baseline this record dials: the five non-legacy
    /// armor baselines combat preset V7's roster fields.
    /// <see cref="Hukbo.Core.Combat.ArmorId.LightOrganic"/> is excluded for
    /// the same reason V7's own roster excludes it — see that preset's own
    /// remarks.
    /// </summary>
    public const int CategoryCount = 5;

    // PROVISIONAL, see the class remarks: no source states a real
    // proportion, so these five values are a gameplay-tuning starting point
    // rather than a historical claim. They sum to exactly 100.
    private const int DefaultNonePercent = 35;
    private const int DefaultQuiltedCottonPercent = 30;
    private const int DefaultHideCorseletPercent = 20;
    private const int DefaultRigidCuirassPercent = 12;
    private const int DefaultImportedIronPercent = 3;

    public static ArmorComposition Default { get; } = new(
        DefaultNonePercent,
        DefaultQuiltedCottonPercent,
        DefaultHideCorseletPercent,
        DefaultRigidCuirassPercent,
        DefaultImportedIronPercent);

    /// <summary>
    /// True when every category percentage is non-negative and the five
    /// percentages sum to exactly 100. A persisted composition that fails
    /// this is never trusted by the store.
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

        return CategorySum == 100;
    }

    /// <summary>
    /// The five percentages, in the same ascending
    /// <see cref="Hukbo.Core.Combat.ArmorId"/> order combat preset V7's
    /// roster repeats each base loadout's five armor-variant rows in: None,
    /// QuiltedCotton, HideCorselet, RigidCuirass, ImportedIron.
    /// </summary>
    public int[] CategoryCounts =>
    [
        NonePercent,
        QuiltedCottonPercent,
        HideCorseletPercent,
        RigidCuirassPercent,
        ImportedIronPercent,
    ];

    private int CategorySum =>
        NonePercent +
        QuiltedCottonPercent +
        HideCorseletPercent +
        RigidCuirassPercent +
        ImportedIronPercent;
}
