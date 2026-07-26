namespace Hukbo.Core.Combat;

/// <summary>
/// Stateless deterministic defensive resolution for one accepted attack. The
/// same profile, loadouts, seed, tick, and entity-ID tuple always resolves to
/// the same <see cref="AttackResolution"/>; no <see cref="System.Random"/>,
/// wall-clock time, or mutable state is used, and no draw is taken from any
/// other generator, so adding this stage shifts no existing behaviour.
/// </summary>
/// <remarks>
/// <para>
/// <b>Neutral stub.</b> Every member here reports the pre-change behaviour:
/// no interception, and every accepted attack landing. The whole surface
/// exists before any assertion is written so that a test referencing it fails
/// on an assertion rather than failing the assembly to compile and taking
/// every unrelated case down with it.
/// </para>
/// <para>
/// All arithmetic is integer basis points out of
/// <see cref="ClashProfile.BasisPointScale"/>, with <see cref="long"/>
/// intermediates where a product could exceed <see cref="int"/>. No
/// fixed-point and no floating-point value enters this path.
/// </para>
/// </remarks>
internal static class ClashResolver
{
    /// <summary>
    /// Computes the deterministic roll for one attack tuple, in
    /// <c>[0, <see cref="ClashProfile.BasisPointScale"/>)</c>. Internal rather
    /// than private so pinned vectors can be verified independently of the
    /// interval walk that consumes them.
    /// </summary>
    /// <param name="seed">The scenario seed.</param>
    /// <param name="tick">The current authoritative tick.</param>
    /// <param name="sourceEntityId">The attacking entity's ID.</param>
    /// <param name="targetEntityId">The defending entity's ID.</param>
    /// <param name="attackerWeapon">The attacking weapon.</param>
    /// <param name="defenderWeapon">The defending weapon.</param>
    /// <param name="defenderShield">The defending shield.</param>
    internal static int MixClash(
        ulong seed,
        long tick,
        ulong sourceEntityId,
        ulong targetEntityId,
        WeaponId attackerWeapon,
        WeaponId defenderWeapon,
        ShieldId defenderShield) => 0;

    /// <summary>
    /// Resolves how the defender met one accepted attack.
    /// </summary>
    /// <param name="profile">The tuning profile.</param>
    /// <param name="seed">The scenario seed.</param>
    /// <param name="tick">The current authoritative tick.</param>
    /// <param name="sourceEntityId">The attacking entity's ID.</param>
    /// <param name="targetEntityId">The defending entity's ID.</param>
    /// <param name="attackerWeapon">The attacking weapon.</param>
    /// <param name="defenderWeapon">The defending weapon.</param>
    /// <param name="defenderShield">The defending shield.</param>
    internal static AttackResolution Resolve(
        ClashProfile profile,
        ulong seed,
        long tick,
        ulong sourceEntityId,
        ulong targetEntityId,
        WeaponId attackerWeapon,
        WeaponId defenderWeapon,
        ShieldId defenderShield)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return AttackResolution.Landed;
    }

    /// <summary>
    /// Splits a weapon channel into the hard arrest and the soft brush.
    /// </summary>
    /// <param name="profile">The tuning profile.</param>
    /// <param name="weaponChannel">
    /// The <b>post-rescale</b> weapon channel, in basis points. Splitting the
    /// pre-rescale value would leave <c>hard + soft</c> unequal to the channel
    /// the interval walk uses, and the five intervals would stop tiling the
    /// roll space.
    /// </param>
    /// <param name="attackerWeapon">The attacking weapon.</param>
    /// <param name="defenderWeapon">The defending weapon.</param>
    internal static (int Hard, int Soft) SplitWeaponChannel(
        ClashProfile profile,
        int weaponChannel,
        WeaponId attackerWeapon,
        WeaponId defenderWeapon)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return (0, 0);
    }

    /// <summary>
    /// Computes the three interception channels for one loadout pairing, plus
    /// the hard and soft halves of the weapon channel.
    /// </summary>
    /// <param name="profile">The tuning profile.</param>
    /// <param name="attackerWeapon">The attacking weapon.</param>
    /// <param name="defenderWeapon">The defending weapon.</param>
    /// <param name="defenderShield">The defending shield.</param>
    /// <returns>
    /// The shield channel, the weapon channel, its hard and soft halves, and
    /// the void channel, all in basis points. The returned weapon channel is
    /// the <b>post-rescale</b> value, so <c>Hard + Soft == Weapon</c> compares
    /// the split against the channel the interval walk actually uses. Without
    /// that member the split invariant would reduce to
    /// <c>hard + soft == hard + soft</c>, which passes on zeros and can never
    /// fail.
    /// </returns>
    internal static (int Shield, int Weapon, int Hard, int Soft, int Void) ComputeChannels(
        ClashProfile profile,
        WeaponId attackerWeapon,
        WeaponId defenderWeapon,
        ShieldId defenderShield)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return (0, 0, 0, 0, 0);
    }
}
