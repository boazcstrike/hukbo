namespace Hukbo.Core.Combat;

/// <summary>
/// How an accepted attack was resolved against the defender's shield, weapon,
/// and footwork. Exactly one value is produced per accepted attack, and the
/// five values are mutually exclusive and jointly exhaustive.
/// </summary>
/// <remarks>
/// <para>
/// Numeric values are part of the deterministic replay contract: the
/// resolution is carried on every attack event and folded into the headless
/// event hash, so renumbering or reordering these members changes that hash
/// and requires a new combat preset version plus new golden expectations.
/// </para>
/// <para>
/// The three contact outcomes describe which defensive surface took the blow.
/// <see cref="ShieldBlocked"/> is the only one with sixteenth-century
/// documentary support; <see cref="Parried"/> and <see cref="Deflected"/> are
/// the more speculative weapon-on-weapon channel, argued from European
/// forte-wear archaeology rather than from any account of Philippine combat.
/// See docs/research/WEAPON_CLASH_1500s.md.
/// </para>
/// </remarks>
public enum AttackResolution
{
    /// <summary>
    /// The blow reached the defender. The only resolution that applies damage.
    /// </summary>
    Landed = 0,

    /// <summary>
    /// The defender's shield took the blow. Not a parry: no hard or soft split
    /// applies to a shield intercept.
    /// </summary>
    ShieldBlocked = 1,

    /// <summary>
    /// The defender's weapon hard-arrested the blow.
    /// </summary>
    Parried = 2,

    /// <summary>
    /// The defender's weapon brushed the blow aside without arresting it.
    /// </summary>
    Deflected = 3,

    /// <summary>
    /// The defender stepped off the line and the blow met empty air. Carries
    /// no sound and no contact effect; the absence is the signal.
    /// </summary>
    Evaded = 4,
}
