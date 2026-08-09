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
/// <param name="ComboOpenChanceBasisPoints">
/// Basis points out of <see cref="ClashProfile.BasisPointScale"/> that a
/// landed blow, not already part of a chain, opens an attack combination.
/// Must be between <c>0</c> and <see cref="ClashProfile.BasisPointScale"/>
/// inclusive. <c>0</c> is a valid, deliberate no-op — see
/// <c>PhilippineCombatPresetV2</c>, whose profiles all set this to zero so
/// that preset never opens a chain.
/// </param>
/// <param name="ComboContinueChanceBasisPoints">
/// Basis points out of <see cref="ClashProfile.BasisPointScale"/> that an
/// active chain survives past a blow that just landed. Must be between
/// <c>0</c> and <see cref="ClashProfile.BasisPointScale"/> inclusive.
/// </param>
/// <param name="ComboMaxSteps">
/// The ceiling this weapon imposes on a chain's length, independent of the
/// attacker's level. Must be positive. The chain's actual maximum is the
/// lesser of this and the attacker's level.
/// </param>
/// <param name="ComboCooldownTicks">
/// Ticks between one blow and the next while a chain is active, used instead
/// of <see cref="AttackCooldownTicks"/>. Must be positive, same rule as
/// <see cref="AttackCooldownTicks"/>.
/// </param>
/// <param name="ProjectileSpeedRaw">
/// PROVISIONAL gameplay tuning, not a historical measurement — none of it may
/// be cited back into docs/research/HISTORICAL_1500s_WEAPONS.md. A thrown or
/// loosed projectile's speed as a raw fixed-point value — world units per
/// tick multiplied by <see cref="Hukbo.Core.Mathematics.FixedPoint.Scale"/>.
/// Zero for a melee weapon; <see cref="CombatRuleset"/> requires a ranged
/// weapon to declare this alongside <see cref="StandoffDistanceRaw"/> and
/// <see cref="FlightTickCeiling"/>, all three or none.
/// </param>
/// <param name="StandoffDistanceRaw">
/// PROVISIONAL gameplay tuning, not a historical measurement — none of it may
/// be cited back into docs/research/HISTORICAL_1500s_WEAPONS.md. The distance
/// a ranged warrior tries to hold from its target, as a raw fixed-point
/// value, before releasing a shot. Zero for a melee weapon.
/// <see cref="CombatRuleset"/> requires this to sit strictly inside this same
/// profile's own <see cref="AttackRangeRaw"/>: a warrior standing beyond its
/// own reach can never shoot, and one standing exactly at its reach is one
/// collision nudge from being unable to.
/// </param>
/// <param name="FlightTickCeiling">
/// PROVISIONAL gameplay tuning, not a historical measurement — none of it may
/// be cited back into docs/research/HISTORICAL_1500s_WEAPONS.md. The most
/// ticks this weapon's projectile is ever in flight before it is forced to
/// resolve. Zero for a melee weapon; <see cref="CombatRuleset"/> requires a
/// ranged weapon to declare this alongside <see cref="ProjectileSpeedRaw"/>
/// and <see cref="StandoffDistanceRaw"/>, all three or none.
/// </param>
public readonly record struct WeaponProfile(
    int DamagePerAttack,
    int AttackRangeRaw,
    int AttackCooldownTicks,
    int ComboOpenChanceBasisPoints = 0,
    int ComboContinueChanceBasisPoints = 0,
    int ComboMaxSteps = 1,
    int ComboCooldownTicks = 1,
    int ProjectileSpeedRaw = 0,
    int StandoffDistanceRaw = 0,
    int FlightTickCeiling = 0)
{
    /// <summary>
    /// Throws when any attribute is not positive, or when a combo chance is
    /// outside <c>[0, ClashProfile.BasisPointScale]</c>. Called by
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
        ValidateBasisPoints(
            ComboOpenChanceBasisPoints,
            $"{parameterName}.{nameof(ComboOpenChanceBasisPoints)}");
        ValidateBasisPoints(
            ComboContinueChanceBasisPoints,
            $"{parameterName}.{nameof(ComboContinueChanceBasisPoints)}");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            ComboMaxSteps,
            $"{parameterName}.{nameof(ComboMaxSteps)}");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            ComboCooldownTicks,
            $"{parameterName}.{nameof(ComboCooldownTicks)}");
        ValidateRangedFields(parameterName);
    }

    /// <summary>
    /// Enforces the ranged-field construction invariants: a weapon is either
    /// melee, declaring all three ranged fields zero, or ranged, declaring
    /// all three positive with its standoff distance strictly inside its own
    /// <see cref="AttackRangeRaw"/>. Called by <see cref="Validate"/>, which
    /// <see cref="CombatRuleset"/> runs for every declared profile, so a
    /// misconfigured ranged weapon fails loudly at construction rather than
    /// producing a warrior that can never shoot.
    /// </summary>
    private void ValidateRangedFields(string parameterName)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(
            ProjectileSpeedRaw,
            $"{parameterName}.{nameof(ProjectileSpeedRaw)}");
        ArgumentOutOfRangeException.ThrowIfNegative(
            StandoffDistanceRaw,
            $"{parameterName}.{nameof(StandoffDistanceRaw)}");
        ArgumentOutOfRangeException.ThrowIfNegative(
            FlightTickCeiling,
            $"{parameterName}.{nameof(FlightTickCeiling)}");

        var isRangedDeclaration = ProjectileSpeedRaw != 0 ||
            StandoffDistanceRaw != 0 ||
            FlightTickCeiling != 0;
        if (!isRangedDeclaration)
        {
            return;
        }

        if (ProjectileSpeedRaw == 0 ||
            StandoffDistanceRaw == 0 ||
            FlightTickCeiling == 0)
        {
            throw new ArgumentException(
                $"{parameterName} declares a ranged weapon — one of " +
                $"{nameof(ProjectileSpeedRaw)}, {nameof(StandoffDistanceRaw)}, " +
                $"or {nameof(FlightTickCeiling)} is non-zero — but does not " +
                "declare all three. A ranged profile must declare all three " +
                "positive; a melee profile must declare all three zero.",
                parameterName);
        }

        if (StandoffDistanceRaw >= AttackRangeRaw)
        {
            throw new ArgumentOutOfRangeException(
                $"{parameterName}.{nameof(StandoffDistanceRaw)}",
                StandoffDistanceRaw,
                $"{parameterName} standoff distance {StandoffDistanceRaw} " +
                $"raw is at or beyond its own reach of {AttackRangeRaw} raw. " +
                "A warrior standing beyond its own reach can never shoot, " +
                "and one standing exactly at its reach is one collision " +
                "nudge from being unable to.");
        }
    }

    private static void ValidateBasisPoints(int value, string parameterName)
    {
        if (value < 0 || value > ClashProfile.BasisPointScale)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"{parameterName} must be between 0 and " +
                $"{ClashProfile.BasisPointScale} inclusive.");
        }
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
