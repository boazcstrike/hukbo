namespace Hukbo.Core.Movement;

/// <summary>
/// Pure, integer-only threat-radius arithmetic for the ranged retreat rung of
/// the <c>BattlefieldRealismV10</c> preset (design section 5.3): deriving a
/// melee-threat radius from a shooter's existing
/// <see cref="Hukbo.Core.Combat.WeaponProfile.StandoffDistanceRaw"/> and
/// deciding whether an observed nearest melee enemy sits inside it. Every
/// method reads only its own arguments — no agent array, no simulation, no
/// tick pipeline — matching the testability shape of
/// <see cref="MovementRouteRules"/>. Division truncates toward zero
/// everywhere, and nothing here touches floating point. Not yet wired into
/// <c>BattleSimulation</c>.
/// </summary>
internal static class RangedRetreatRules
{
    /// <summary>
    /// The basis-point denominator of the threat-radius model, matching
    /// <see cref="MovementRouteRules"/>'s own basis-point scale.
    /// </summary>
    private const long BasisPointDenominator = 10_000;

    /// <summary>
    /// PROVISIONAL RECONSTRUCTION — gameplay tuning under <c>CLAUDE.md</c>
    /// section 7, not a historical measurement. The fraction of a shooter's
    /// own <see cref="Hukbo.Core.Combat.WeaponProfile.StandoffDistanceRaw"/>
    /// that becomes the melee-threat radius (design section 5.3): half the
    /// standoff distance, so a shooter that backs off the threat clears the
    /// radius while still inside its own attack range and can resume firing
    /// immediately rather than having to walk back. No new field is added to
    /// <c>WeaponProfile</c>, <c>CombatRuleset</c>, <c>MovementRuleset</c>, or
    /// <c>AgentState</c> to carry this value — design section 3.2 explains
    /// why folding it as a ruleset field would move
    /// <c>MovementRuleset.ContentHash</c> for every registered preset,
    /// including the seven with pinned literals, for no payoff a named
    /// constant does not already give.
    /// </summary>
    internal const long ThreatRadiusBasisPoints = 5_000;

    /// <summary>
    /// Derives the melee-threat radius from a shooter's own
    /// <paramref name="standoffDistanceRaw"/> — <c>standoffDistanceRaw *
    /// ThreatRadiusBasisPoints / BasisPointDenominator</c>, truncating toward
    /// zero. <see cref="ThreatRadiusBasisPoints"/> is at most the
    /// denominator, so the result is always bounded to
    /// <c>[0, standoffDistanceRaw]</c>; a zero standoff distance — a melee
    /// weapon, per <see cref="Hukbo.Core.Combat.WeaponProfile.ValidateRangedFields"/>
    /// — yields a zero radius. The multiply widens to <see langword="long"/>
    /// before scaling, so an <see cref="int.MaxValue"/> standoff distance
    /// cannot overflow.
    /// </summary>
    internal static int ThreatRadiusRaw(int standoffDistanceRaw)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(standoffDistanceRaw);
        var scaled = checked((long)standoffDistanceRaw * ThreatRadiusBasisPoints) /
            BasisPointDenominator;
        return (int)scaled;
    }

    /// <summary>
    /// Whether the nearest closing melee enemy, at squared distance
    /// <paramref name="nearestMeleeSquared"/> (design section 5.3's
    /// per-agent scratch value, or <see cref="long.MaxValue"/> for none),
    /// sits at or inside <paramref name="threatRadiusRaw"/> — the same
    /// at-or-inside convention <c>RangedStandoffV8</c>'s own hold comparison
    /// uses at <c>BattleSimulation.cs</c> line 1731. The boundary is
    /// inclusive: an enemy exactly at the radius counts as threatened.
    /// </summary>
    internal static bool IsThreatened(long nearestMeleeSquared, int threatRadiusRaw)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(threatRadiusRaw);
        var radiusSquared = checked((long)threatRadiusRaw * threatRadiusRaw);
        return nearestMeleeSquared <= radiusSquared;
    }
}
