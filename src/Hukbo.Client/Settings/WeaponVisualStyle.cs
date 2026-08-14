namespace Hukbo.Client.Settings;

/// <summary>
/// Spectator-selectable way an armed warrior's weapon and tall hardwood shield
/// are drawn: the existing procedural lines and quads, or one authored cell
/// from the weapon sprite atlas. Numeric values are part of the persisted
/// settings-file contract; do not renumber or reuse them, or a stored
/// preference can silently resolve to a different style after an upgrade.
/// </summary>
/// <remarks>
/// Only the weapon and the shield are affected either way. The bowstring, the
/// swing trail, the arms, the body, and every mark stay procedural under both
/// values — see the 2026-08-15 weapon sprite design, section 9. This is a
/// separate setting from <see cref="PawnVisualStyle"/>: the two packages sit on
/// independent branches, and comparing a sprite weapon against a procedural
/// body (or the reverse) is a thing an evaluator will want to do, so folding
/// them into one enum would make that impossible without a code change (design
/// section 12).
/// </remarks>
public enum WeaponVisualStyle
{
    /// <summary>
    /// Draw the weapon as three colinear lines and the shield as stacked
    /// quads, the look every warrior had before this setting existed.
    /// </summary>
    Procedural = 0,

    /// <summary>
    /// Draw the weapon and the shield as one cell each from the weapon sprite
    /// atlas, chosen per warrior from that warrior's entity identifier, at
    /// Medium detail tier and above only.
    /// </summary>
    Sprite = 1,
}
