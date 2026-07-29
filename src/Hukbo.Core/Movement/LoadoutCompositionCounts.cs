using Hukbo.Core.Combat;

namespace Hukbo.Core.Movement;

/// <summary>
/// One immutable bundle of per-loadout headcounts, one counter per canonical
/// loadout bucket in canonical <c>KP, WA, KA, IT, KS, IS</c> order. Derived
/// scratch for the weapon-relative movement design's local context (design
/// section 7) and per-faction surviving totals (design section 7.5): never
/// hashed, never snapshotted, never persisted, recomputed every tick.
/// </summary>
/// <remarks>
/// The three role-presence flags and <see cref="RoleCoverage"/> implement
/// design section 7.5. Presence affects only the contested posture tie-break
/// of design section 8 — it never adds virtual warriors, never changes pace,
/// and never changes a headcount. The role groupings are gameplay-design
/// buckets, not historical claims.
/// </remarks>
public readonly record struct LoadoutCompositionCounts(
    int Kampilan,
    int Wasay,
    int Kalis,
    int Itak,
    int KalisShield,
    int ItakShield)
{
    /// <summary>
    /// Whether at least one long-clearance warrior — Kampilan or Wasay — is
    /// counted.
    /// </summary>
    public bool HasLongClearanceRole => Kampilan + Wasay > 0;

    /// <summary>
    /// Whether at least one mobile-blade warrior — solo Kalis or solo Itak —
    /// is counted.
    /// </summary>
    public bool HasMobileBladeRole => Kalis + Itak > 0;

    /// <summary>
    /// Whether at least one shield-support warrior — shielded Kalis or
    /// shielded Itak — is counted.
    /// </summary>
    public bool HasShieldSupportRole => KalisShield + ItakShield > 0;

    /// <summary>
    /// The number of distinct roles present, an integer in <c>[0, 3]</c>
    /// (design section 7.5).
    /// </summary>
    public int RoleCoverage =>
        (HasLongClearanceRole ? 1 : 0) +
        (HasMobileBladeRole ? 1 : 0) +
        (HasShieldSupportRole ? 1 : 0);

    /// <summary>
    /// The sum of all six counters.
    /// </summary>
    public int Total =>
        checked(Kampilan + Wasay + Kalis + Itak + KalisShield + ItakShield);

    /// <summary>
    /// Returns a new value with <paramref name="loadout"/>'s canonical bucket
    /// incremented by one. The bucket key is the equipment triple —
    /// <see cref="CombatLoadout.Rank"/> is social standing with no movement
    /// meaning and is never read. Throws for an equipment triple outside the
    /// six canonical loadouts rather than counting it silently, so a future
    /// weapon, armor, or shield fails loudly here instead of vanishing from
    /// every composition-driven decision.
    /// </summary>
    public LoadoutCompositionCounts Add(CombatLoadout loadout) =>
        (loadout.Weapon, loadout.Armor, loadout.Shield) switch
        {
            (WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None) =>
                this with { Kampilan = checked(Kampilan + 1) },
            (WeaponId.Wasay, ArmorId.LightOrganic, ShieldId.None) =>
                this with { Wasay = checked(Wasay + 1) },
            (WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.None) =>
                this with { Kalis = checked(Kalis + 1) },
            (WeaponId.Itak, ArmorId.LightOrganic, ShieldId.None) =>
                this with { Itak = checked(Itak + 1) },
            (WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.TallHardwood) =>
                this with { KalisShield = checked(KalisShield + 1) },
            (WeaponId.Itak, ArmorId.LightOrganic, ShieldId.TallHardwood) =>
                this with { ItakShield = checked(ItakShield + 1) },
            _ => throw new ArgumentOutOfRangeException(
                nameof(loadout),
                loadout,
                "No canonical composition bucket exists for this equipment " +
                "triple."),
        };
}
