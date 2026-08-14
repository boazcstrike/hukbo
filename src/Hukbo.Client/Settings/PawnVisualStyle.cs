namespace Hukbo.Client.Settings;

/// <summary>
/// Spectator-selectable way a warrior's head and torso are drawn: the existing
/// procedural quads, or one authored cell from the pawn body atlas. Numeric
/// values are part of the persisted settings-file contract; do not renumber or
/// reuse them, or a stored preference can silently resolve to a different
/// style after an upgrade.
/// </summary>
/// <remarks>
/// Only the head and torso are affected either way. Legs, feet, arms, the
/// weapon line, the shield, and every mark stay procedural under both values,
/// because they are gait-animated or directional and a single authored cell
/// cannot carry either — see the 2026-08-15 pawn sprite body design, section 3.
/// </remarks>
public enum PawnVisualStyle
{
    /// <summary>
    /// Draw the head and torso as filled quads, the look every warrior had
    /// before this setting existed.
    /// </summary>
    Procedural = 0,

    /// <summary>
    /// Draw the head and torso as one cell from the pawn body atlas, chosen per
    /// warrior from that warrior's entity identifier, and tinted so the two
    /// factions stay distinguishable.
    /// </summary>
    SpriteBody = 1,
}
