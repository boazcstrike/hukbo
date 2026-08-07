using Sandata.Core.Weapons;

namespace Sandata.Core.Combat;

/// <summary>
/// Design section 9's "fire mode by range band" — an ordered, total rule with
/// no ties that turns a firearm's selector options and the current
/// engagement range into exactly one fire mode, or into no engagement at all.
/// </summary>
/// <remarks>
/// <para>
/// <b>The rule, exactly as design section 9 states it:</b>
/// </para>
/// <code>
/// if range &lt;= AutoBandMaxWu   and Modes has Auto              -&gt; Auto
/// else if range &lt;= BurstBandMaxWu and Modes has Burst3        -&gt; Burst3
/// else if range &lt;= BurstBandMaxWu and Modes has Burst2        -&gt; Burst2
/// else if range &lt;= SingleBandMaxWu and Modes has Single       -&gt; Single
/// else                                                        -&gt; no engagement
/// </code>
/// <para>
/// <see cref="FireModeSet.Burst3"/> is tested before
/// <see cref="FireModeSet.Burst2"/> so a weapon that carried both would still
/// resolve deterministically — design section 9 records that no row in the
/// current 38-weapon roster carries both, so that ordering is not load-bearing
/// against any shipped weapon today, but <c>FireModeSelectionTests</c>
/// still exercises it directly against a synthetic mode set, per the same
/// section's own reasoning: the rule must never become load-bearing silently.
/// </para>
/// <para>
/// Every branch tests a range against a band ceiling with <c>&lt;=</c>, so a
/// range exactly on a boundary belongs to the lower, more permissive mode
/// (<see cref="FireModeSet.Auto"/> at exactly <c>AutoBandMaxWu</c>, not the
/// next mode down). The design's own pseudocode already fixes this reading —
/// this type introduces no new interpretation of it. A weapon whose
/// <c>AutoBandMaxWu</c> and <c>BurstBandMaxWu</c> are both <c>0</c> (design
/// section 9's documented placeholder for a pistol row, which never carries
/// <see cref="FireModeSet.Auto"/> or a burst flag in the first place) never
/// observably reaches either of the first two branches: those branches'
/// range comparisons can be true at <c>range == 0</c>, but the corresponding
/// <c>Modes</c> flag is always absent on such a row, so the rule falls
/// through to the <see cref="FireModeSet.Single"/> branch exactly as it would
/// if those two fields carried any other value. Nothing in this type treats
/// <c>0</c> as a special sentinel; the placeholder's inertness is a property
/// of the roster's data, not of this rule.
/// </para>
/// </remarks>
public static class FireModeSelection
{
    /// <summary>
    /// Applies the ordered, total fire-mode band-selection rule described in
    /// this type's remarks.
    /// </summary>
    /// <param name="modes">
    /// The firearm's selector options, <see cref="FirearmDefinition.Modes"/>.
    /// <see cref="FireModeSet.Safe"/> plays no part in this rule; it is
    /// tested only for the four firing modes.
    /// </param>
    /// <param name="rangeWu">
    /// The current engagement range, in world units. Never negative.
    /// </param>
    /// <param name="autoBandMaxWu"><see cref="FirearmDefinition.AutoBandMaxWu"/>.</param>
    /// <param name="burstBandMaxWu"><see cref="FirearmDefinition.BurstBandMaxWu"/>.</param>
    /// <param name="singleBandMaxWu"><see cref="FirearmDefinition.SingleBandMaxWu"/>.</param>
    /// <returns>
    /// The single fire mode the rule selects — always exactly one of
    /// <see cref="FireModeSet.Auto"/>, <see cref="FireModeSet.Burst3"/>,
    /// <see cref="FireModeSet.Burst2"/>, or <see cref="FireModeSet.Single"/>,
    /// never a combination and never <see cref="FireModeSet.Safe"/> — or
    /// <see langword="null"/> when <paramref name="rangeWu"/> is beyond
    /// <paramref name="singleBandMaxWu"/>, meaning no engagement is produced
    /// at all.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="rangeWu"/> is negative.</exception>
    public static FireModeSet? SelectMode(
        FireModeSet modes,
        int rangeWu,
        int autoBandMaxWu,
        int burstBandMaxWu,
        int singleBandMaxWu)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rangeWu);

        if (rangeWu <= autoBandMaxWu && modes.HasFlag(FireModeSet.Auto))
        {
            return FireModeSet.Auto;
        }

        if (rangeWu <= burstBandMaxWu && modes.HasFlag(FireModeSet.Burst3))
        {
            return FireModeSet.Burst3;
        }

        if (rangeWu <= burstBandMaxWu && modes.HasFlag(FireModeSet.Burst2))
        {
            return FireModeSet.Burst2;
        }

        if (rangeWu <= singleBandMaxWu && modes.HasFlag(FireModeSet.Single))
        {
            return FireModeSet.Single;
        }

        return null;
    }
}
