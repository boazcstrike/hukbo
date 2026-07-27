using Hukbo.Core.Combat;

namespace Hukbo.Core.Tests;

/// <summary>
/// The reference oracle for defensive resolution. It is a deliberately plain
/// transcription of the six-step pipeline in design section 3.3 whose only job
/// is to be obviously correct, so that the production resolver can be compared
/// against it.
/// </summary>
/// <remarks>
/// <para>
/// This type must never call the production clash resolver, or any other
/// production helper. The resolver is not even named here, so that a plain
/// search of this file for its name returns nothing and the independence rule
/// is mechanically checkable rather than a matter of reading.
/// Its independence from the code under test is the entire reason it
/// exists, so the clamp, the rescale, and the interval walk are written out
/// inline here even though the simulation has its own. Do not optimise this
/// class; it is a correctness oracle, not a hot path. It reads the same
/// <see cref="ClashProfile"/> the resolver reads, which is data rather than
/// behaviour, and takes the roll as an input so that the mixer is pinned
/// separately rather than being re-derived here.
/// </para>
/// <para>
/// The ordering rule this oracle is written from, quoted verbatim from design
/// section 3.3 because an oracle is only independent if the specification it
/// was written from is unambiguous:
/// </para>
/// <para>
/// "Step 6 splits the post-rescale weapon channel, never the pre-rescale one.
/// This sentence is normative and the naive oracle is required to cite it.
/// Step 4 may reduce <c>weapon</c>, and if step 6 split the pre-rescale value
/// then <c>hard + soft</c> would no longer equal the <c>weapon</c> the
/// interval walk uses, and the five intervals would stop tiling the roll
/// space. Splitting the rescaled value keeps <c>hard + soft</c> exactly equal
/// to <c>weapon</c>, so truncation can neither leak nor invent a basis point."
/// </para>
/// <para>
/// Every intermediate is a <see cref="long"/> and every division truncates
/// toward zero, which for these non-negative values is ordinary integer
/// division.
/// </para>
/// </remarks>
internal static class NaiveClashResolution
{
    /// <summary>
    /// Steps one through six: the three channels, the rescale, the clamped
    /// hard share, and the split of the post-rescale weapon channel.
    /// </summary>
    internal static (long Shield, long Weapon, long Hard, long Soft, long Void) ComputeChannels(
        ClashProfile profile,
        WeaponId attackerWeapon,
        WeaponId defenderWeapon,
        ShieldId defenderShield)
    {
        // Step 1.
        long shield = profile.ResolveShieldIntercept(defenderShield);

        // Step 2.
        long weapon = profile.ResolveWeaponIntercept(
            defenderWeapon,
            defenderShield,
            attackerWeapon);

        // Step 3.
        long voidChannel = profile.ResolveVoid(defenderWeapon, defenderShield);

        // Step 4. Each channel is rescaled independently and each division
        // truncates, so the post-rescale total lands at or below the ceiling
        // with a small unallocated residue that becomes additional Landed
        // probability.
        long total = shield + weapon + voidChannel;
        long ceiling = profile.MaximumInterceptionBasisPoints;
        if (total > ceiling)
        {
            shield = shield * ceiling / total;
            weapon = weapon * ceiling / total;
            voidChannel = voidChannel * ceiling / total;
        }

        // Step 5.
        long hardShare =
            (long)profile.ResolveHardShareBase(attackerWeapon) *
            profile.ResolveHardShareMultiplier(defenderWeapon) /
            ClashProfile.HardShareMultiplierScale;
        if (hardShare < profile.MinimumHardShareBasisPoints)
        {
            hardShare = profile.MinimumHardShareBasisPoints;
        }

        if (hardShare > profile.MaximumHardShareBasisPoints)
        {
            hardShare = profile.MaximumHardShareBasisPoints;
        }

        // Step 6, against the post-rescale weapon channel.
        long hard = weapon * hardShare / ClashProfile.BasisPointScale;
        long soft = weapon - hard;

        return (shield, weapon, hard, soft, voidChannel);
    }

    /// <summary>
    /// Walks the cumulative interval in the one fixed order, with strictly
    /// lower-exclusive comparisons so a zero-width channel can never be
    /// selected.
    /// </summary>
    /// <param name="profile">The tuning profile.</param>
    /// <param name="roll">
    /// The roll, in <c>[0, <see cref="ClashProfile.BasisPointScale"/>)</c>.
    /// </param>
    /// <param name="attackerWeapon">The attacking weapon.</param>
    /// <param name="defenderWeapon">The defending weapon.</param>
    /// <param name="defenderShield">The defending shield.</param>
    internal static AttackResolution Resolve(
        ClashProfile profile,
        int roll,
        WeaponId attackerWeapon,
        WeaponId defenderWeapon,
        ShieldId defenderShield)
    {
        var (shield, _, hard, soft, voidChannel) = ComputeChannels(
            profile,
            attackerWeapon,
            defenderWeapon,
            defenderShield);

        long cumulative = shield;
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
}
