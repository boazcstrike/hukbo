using Hukbo.Core.Determinism;

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
/// All arithmetic is integer basis points out of
/// <see cref="ClashProfile.BasisPointScale"/>, with <see cref="long"/>
/// intermediates where a product could exceed <see cref="int"/>. No
/// fixed-point and no floating-point value enters this path.
/// </para>
/// </remarks>
internal static class ClashResolver
{
    /// <summary>
    /// The domain tag folded first by <see cref="MixClash"/>, ASCII
    /// <c>HKBO_CLS</c>. It separates this roll stream from the hit-location
    /// stream, which folds <c>HKBO_HIT</c> over an overlapping tuple: without
    /// distinct tags the same attack would derive its body part and its
    /// defensive resolution from correlated rolls.
    /// </summary>
    private const ulong ClashTag = 0x484B424F5F434C53UL;

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
    /// <remarks>
    /// Eight words are folded, in this fixed order: the domain tag, the seed,
    /// the tick, the source entity ID, the target entity ID, the attacking
    /// weapon, the defending weapon, and the defending shield. Each enum is
    /// folded as its declared numeric value, exactly as
    /// <see cref="HitLocationResolver.MixAttack"/> folds its weapon word.
    /// Dropping or reordering a word is invisible to any distribution test, so
    /// seven single-word isolation cases pin the dependence on each one.
    /// </remarks>
    internal static int MixClash(
        ulong seed,
        long tick,
        ulong sourceEntityId,
        ulong targetEntityId,
        WeaponId attackerWeapon,
        WeaponId defenderWeapon,
        ShieldId defenderShield)
    {
        var hash = Fnv1a.OffsetBasis;
        Fnv1a.Add(ref hash, ClashTag);
        Fnv1a.Add(ref hash, seed);
        Fnv1a.Add(ref hash, unchecked((ulong)tick));
        Fnv1a.Add(ref hash, sourceEntityId);
        Fnv1a.Add(ref hash, targetEntityId);
        Fnv1a.Add(ref hash, (ulong)attackerWeapon);
        Fnv1a.Add(ref hash, (ulong)defenderWeapon);
        Fnv1a.Add(ref hash, (ulong)defenderShield);
        return (int)(hash % ClashProfile.BasisPointScale);
    }

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

        var (shield, _, hard, soft, voidChannel) = ComputeChannels(
            profile,
            attackerWeapon,
            defenderWeapon,
            defenderShield);
        var roll = MixClash(
            seed,
            tick,
            sourceEntityId,
            targetEntityId,
            attackerWeapon,
            defenderWeapon,
            defenderShield);

        // One fixed interval order, walked exactly as
        // HitLocationResolver.Resolve walks BodyPartCatalog.Ordered. Every
        // comparison is strictly lower-exclusive so a zero-width channel is
        // stepped over rather than selected: with ShieldId.None the shield
        // interval is [0, 0), and `roll <= cumulative` would block a roll of
        // zero with a shield the warrior does not carry.
        var cumulative = shield;
        if (roll < cumulative)
        {
            return AttackResolution.ShieldBlocked;
        }

        cumulative += hard;
        if (roll < cumulative)
        {
            return AttackResolution.Parried;
        }

        cumulative += soft;
        if (roll < cumulative)
        {
            return AttackResolution.Deflected;
        }

        cumulative += voidChannel;
        if (roll < cumulative)
        {
            return AttackResolution.Evaded;
        }

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

        // Step five: the clamped hard share.
        var hardShare =
            (long)profile.ResolveHardShareBase(attackerWeapon) *
            profile.ResolveHardShareMultiplier(defenderWeapon) /
            ClashProfile.HardShareMultiplierScale;
        hardShare = Math.Clamp(
            hardShare,
            profile.MinimumHardShareBasisPoints,
            profile.MaximumHardShareBasisPoints);

        // Step six, against the channel the caller supplies. Soft is the
        // remainder rather than a second product, so truncation can neither
        // leak nor invent a basis point and hard + soft is exactly the channel.
        var hard = (int)((long)weaponChannel * hardShare / ClashProfile.BasisPointScale);
        return (hard, weaponChannel - hard);
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

        // Steps one to three.
        long shield = profile.ResolveShieldIntercept(defenderShield);
        long weapon = profile.ResolveWeaponIntercept(defenderWeapon, defenderShield, attackerWeapon);
        long voidChannel = profile.ResolveVoid(defenderWeapon, defenderShield);

        // Step four. Each channel is rescaled independently and each division
        // truncates toward zero, so the post-rescale total lands at or below
        // the ceiling with a small unallocated residue that becomes additional
        // Landed probability. Rescaling the three separately is what keeps
        // their proportions rather than charging the whole reduction to one.
        var total = shield + weapon + voidChannel;
        long ceiling = profile.MaximumInterceptionBasisPoints;
        if (total > ceiling)
        {
            shield = shield * ceiling / total;
            weapon = weapon * ceiling / total;
            voidChannel = voidChannel * ceiling / total;
        }

        // Steps five and six, against the post-rescale weapon channel. Design
        // section 3.3 states normatively that the split takes the rescaled
        // value: splitting the pre-rescale one would leave hard + soft unequal
        // to the weapon channel the interval walk uses, and the five intervals
        // would stop tiling the roll space.
        var (hard, soft) = SplitWeaponChannel(
            profile,
            (int)weapon,
            attackerWeapon,
            defenderWeapon);

        return ((int)shield, (int)weapon, hard, soft, (int)voidChannel);
    }
}
