namespace Sandata.Core.Rules;

/// <summary>
/// Identifies one Sandata ruleset preset. Numeric values are part of the
/// replay contract in exactly the sense <c>CLAUDE.md</c> section 5 states for
/// Hukbo: a saved replay, a golden expectation, and every recorded seed-1
/// baseline all name a preset by this numeric value, not by its member name.
/// </summary>
/// <remarks>
/// <b>Append-only.</b> A member's numeric value never changes once it has
/// shipped, a member is never reordered, and a retired value is never reused
/// for a different preset. Changing the tick rate, the millisecond-to-tick
/// conversion rule, a roster order, a weapon weight, or a hash mixer for an
/// existing preset requires a <em>new</em> preset value plus new golden
/// expectations — the same rule design section 4 states for Sandata and
/// <c>CLAUDE.md</c> section 5 states for Hukbo. A test in
/// <c>SandataRulesetTests</c> pins <see cref="ModernTacticalV1"/>'s numeric
/// value as a literal so an accidental renumbering fails loudly.
/// </remarks>
public enum SandataPresetId
{
    /// <summary>
    /// The first Sandata preset: the 50 Hz tick, the half-away-from-zero
    /// millisecond-to-tick conversion rule, and the v0.1 ruleset tunables
    /// declared on <see cref="SandataRuleset.ModernTacticalV1"/>. Design
    /// section 4.
    /// </summary>
    ModernTacticalV1 = 1,
}
