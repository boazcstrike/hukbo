namespace Hukbo.Core.Combat;

/// <summary>
/// One weapon's authoritative combat attributes for a single grip: the damage
/// a blow deals, the reach it strikes from, and the ticks it takes to recover.
/// </summary>
/// <remarks>
/// These are provisional gameplay tuning values, not measurements. Nothing
/// here may be cited back into docs/research/HISTORICAL_1500s_WEAPONS.md or
/// shown to a spectator as a historical fact: what justifies a number is the
/// physical character of the object — its length, where its mass sits, how
/// many hands it takes — not any source on how hard a sixteenth-century blade
/// hit.
/// <para>
/// Profiles are hand-authored in full rather than derived from a base row
/// plus a delta, matching the explicit-data convention the presets already
/// follow. A delta would need clamping, underflow checks, and its own reach
/// floor validation; six explicit rows are cheaper to reason about than four
/// rows and a rule.
/// </para>
/// </remarks>
/// <param name="DamagePerAttack">
/// Hit points removed by one landed blow. Must be positive.
/// </param>
/// <param name="AttackRangeRaw">
/// Reach as a raw fixed-point value — world units multiplied by
/// <see cref="Hukbo.Core.Mathematics.FixedPoint.Scale"/>. Must be positive,
/// and <see cref="CombatRuleset"/> additionally enforces the reach floor: a
/// profile at or below two body radii could never strike a warrior it stands
/// against.
/// </param>
/// <param name="AttackCooldownTicks">
/// Ticks between one blow and the next. Must be positive; zero would be an
/// infinite attack rate.
/// </param>
public readonly record struct WeaponProfile(
    int DamagePerAttack,
    int AttackRangeRaw,
    int AttackCooldownTicks)
{
    /// <summary>
    /// Throws when any attribute is not positive. Called by
    /// <see cref="CombatRuleset"/> for every declared profile, so a
    /// misconfigured preset fails loudly at construction rather than
    /// producing a battle that quietly cannot happen.
    /// </summary>
    public void Validate(string parameterName)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            DamagePerAttack,
            $"{parameterName}.{nameof(DamagePerAttack)}");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            AttackRangeRaw,
            $"{parameterName}.{nameof(AttackRangeRaw)}");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            AttackCooldownTicks,
            $"{parameterName}.{nameof(AttackCooldownTicks)}");
    }
}

/// <summary>
/// Everything a preset declares about one weapon beyond its targeting
/// weights: how many hands it occupies, and the attribute profile it exposes
/// for each grip it supports.
/// </summary>
/// <remarks>
/// A one-handed blade fought with a shield is not the same weapon as the same
/// blade fought with the off hand free. The free hand lengthens the stroke,
/// lets the fighter commit weight into the blow, and removes the shield's
/// mass from the recovery — at the cost of every defensive benefit the shield
/// was providing. That difference is the whole reason a fighter would choose
/// to drop the shield.
/// <para>
/// <see cref="Paired"/> is a base rather than a final value. It is correct as
/// authored while exactly one shield exists; when a later preset adds a
/// second and third shield, this is the row those shields modify, which is
/// what keeps the configuration from growing as weapons times shields.
/// </para>
/// </remarks>
/// <param name="Grip">Whether the weapon occupies one hand or both.</param>
/// <param name="Solo">
/// Attributes with the off hand free. Every weapon declares this.
/// </param>
/// <param name="Paired">
/// Attributes when carrying a shield. Required for
/// <see cref="WeaponGrip.OneHanded"/> and forbidden for
/// <see cref="WeaponGrip.TwoHanded"/>; <see cref="CombatRuleset"/> throws on
/// either mistake rather than silently falling back to <see cref="Solo"/>.
/// </param>
public readonly record struct WeaponAttributes(
    WeaponGrip Grip,
    WeaponProfile Solo,
    WeaponProfile? Paired)
{
    /// <summary>
    /// A two-handed weapon, which declares a solo profile and no other.
    /// </summary>
    public static WeaponAttributes TwoHanded(WeaponProfile solo) =>
        new(WeaponGrip.TwoHanded, solo, null);

    /// <summary>
    /// A one-handed weapon, which declares both profiles.
    /// </summary>
    public static WeaponAttributes OneHanded(
        WeaponProfile solo,
        WeaponProfile paired) =>
        new(WeaponGrip.OneHanded, solo, paired);
}
