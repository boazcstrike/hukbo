using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Core.Movement.Profiles;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// Identity wiring of the two Itak movement rows under
/// <see cref="MovementPresetId.EquipmentRelativeFootworkV6"/>: the solo row
/// declared by <see cref="ItakMovementProfile.Row"/> is the registered
/// canonical index 3 instance, the shielded row declared by
/// <see cref="TallHardwoodMovementProfiles.ItakRow"/> is the registered
/// canonical index 5 instance, resolution is rank-independent for both, and
/// <see cref="MovementRouteRules.EffectivePreferredDistanceRaw"/> applies
/// every opponent offset cell of both rows exactly. The row literals
/// themselves are pinned by <see cref="MovementProfileRegistrationTests"/>
/// and are not re-asserted here. Every value is a provisional
/// reconstruction — gameplay tuning, not a historical measurement
/// (docs/research/movement/itak.md, section 7).
/// </summary>
public sealed class ItakMovementProfileTests
{
    /// <summary>
    /// The canonical scenario attack range of five world units, matching
    /// the movement pipeline fixtures.
    /// </summary>
    private const int AttackRangeRaw = 5 * FixedPoint.Scale;

    private static MovementRuleset V6 =>
        MovementPresetRegistry.Get(MovementPresetId.EquipmentRelativeFootworkV6);

    private static CombatLoadout SoloItak(RankId rank = RankId.Timawa) =>
        new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.None, rank);

    private static CombatLoadout ShieldedItak(RankId rank = RankId.Timawa) =>
        new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.TallHardwood, rank);

    // ----- Identity wiring (design 4.1, canonical order KP WA KA IT KS IS) --

    [Fact]
    public void TheSoloItakRowIsTheRegisteredCanonicalIndexThreeInstance()
    {
        Assert.Same(ItakMovementProfile.Row, V6.LoadoutMovementProfiles[3]);
    }

    [Fact]
    public void TheShieldedItakRowIsTheRegisteredCanonicalIndexFiveInstance()
    {
        Assert.Same(
            TallHardwoodMovementProfiles.ItakRow,
            V6.LoadoutMovementProfiles[5]);
    }

    [Fact]
    public void ResolveLoadoutProfileReturnsTheSoloItakRowInstance()
    {
        Assert.Same(
            ItakMovementProfile.Row,
            V6.ResolveLoadoutProfile(
                new CombatLoadout(
                    WeaponId.Itak, ArmorId.LightOrganic, ShieldId.None)));
    }

    [Fact]
    public void ResolveLoadoutProfileReturnsTheShieldedItakRowInstance()
    {
        Assert.Same(
            TallHardwoodMovementProfiles.ItakRow,
            V6.ResolveLoadoutProfile(
                new CombatLoadout(
                    WeaponId.Itak,
                    ArmorId.LightOrganic,
                    ShieldId.TallHardwood)));
    }

    // ----- Rank independence (design 4.1: the key is equipment only) -----

    /// <summary>
    /// Every rank, including both non-default extremes, resolves the same
    /// solo Itak row instance: the profile key drops the rank component.
    /// </summary>
    [Theory]
    [InlineData(RankId.Datu)]
    [InlineData(RankId.Maharlika)]
    [InlineData(RankId.Timawa)]
    [InlineData(RankId.AlipingNamamahay)]
    [InlineData(RankId.Ayuey)]
    public void EveryRankResolvesTheSameSoloItakRowInstance(RankId rank)
    {
        Assert.Same(
            ItakMovementProfile.Row,
            V6.ResolveLoadoutProfile(SoloItak(rank)));
    }

    /// <summary>
    /// Every rank resolves the same shielded Itak row instance as well, so
    /// rank independence holds on both sides of the shield split.
    /// </summary>
    [Theory]
    [InlineData(RankId.Datu)]
    [InlineData(RankId.Maharlika)]
    [InlineData(RankId.Timawa)]
    [InlineData(RankId.AlipingNamamahay)]
    [InlineData(RankId.Ayuey)]
    public void EveryRankResolvesTheSameShieldedItakRowInstance(RankId rank)
    {
        Assert.Same(
            TallHardwoodMovementProfiles.ItakRow,
            V6.ResolveLoadoutProfile(ShieldedItak(rank)));
    }

    // ----- Effective preferred distance (design 4.4) -----

    /// <summary>
    /// The solo Itak row against every canonical opponent column, at the
    /// canonical five-unit attack range (5 * 1024 = 5120 raw). The row's
    /// preferred distance is 11,000 basis points with offsets
    /// [-750, -500, -250, 0, 0, 250], so the effective values are
    /// 5120 * (11000 + offset) / 10000 — computed by hand per column.
    /// Provisional reconstruction: gameplay tuning; no historical
    /// measurement.
    /// </summary>
    [Theory]
    [InlineData(0, 5_248L)] // KP: 5120 * 10250 / 10000
    [InlineData(1, 5_376L)] // WA: 5120 * 10500 / 10000
    [InlineData(2, 5_504L)] // KA: 5120 * 10750 / 10000
    [InlineData(3, 5_632L)] // IT: 5120 * 11000 / 10000
    [InlineData(4, 5_632L)] // KS: 5120 * 11000 / 10000
    [InlineData(5, 5_760L)] // IS: 5120 * 11250 / 10000
    public void TheSoloItakEffectivePreferredDistanceCoversEveryOpponentColumn(
        int opponentCanonicalIndex, long expectedRaw)
    {
        Assert.Equal(
            expectedRaw,
            MovementRouteRules.EffectivePreferredDistanceRaw(
                AttackRangeRaw,
                ItakMovementProfile.Row,
                opponentCanonicalIndex));
    }

    /// <summary>
    /// The shielded Itak row against every canonical opponent column, at the
    /// same canonical attack range. The row's preferred distance is 10,000
    /// basis points with offsets [-500, -250, 0, 250, -250, 0], so the
    /// effective values are 5120 * (10000 + offset) / 10000 — computed by
    /// hand per column. Provisional reconstruction: gameplay tuning; no
    /// historical measurement.
    /// </summary>
    [Theory]
    [InlineData(0, 4_864L)] // KP: 5120 * 9500 / 10000
    [InlineData(1, 4_992L)] // WA: 5120 * 9750 / 10000
    [InlineData(2, 5_120L)] // KA: 5120 * 10000 / 10000
    [InlineData(3, 5_248L)] // IT: 5120 * 10250 / 10000
    [InlineData(4, 4_992L)] // KS: 5120 * 9750 / 10000
    [InlineData(5, 5_120L)] // IS: 5120 * 10000 / 10000
    public void TheShieldedItakEffectivePreferredDistanceCoversEveryOpponentColumn(
        int opponentCanonicalIndex, long expectedRaw)
    {
        Assert.Equal(
            expectedRaw,
            MovementRouteRules.EffectivePreferredDistanceRaw(
                AttackRangeRaw,
                TallHardwoodMovementProfiles.ItakRow,
                opponentCanonicalIndex));
    }
}
