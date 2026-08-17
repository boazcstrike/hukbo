using Hukbo.Core.Combat;
using Hukbo.Core.Movement;

namespace Hukbo.Core.Tests;

/// <summary>
/// Armor is not part of any canonical movement loadout key.
/// </summary>
/// <remarks>
/// Three sites in the movement layer resolve a warrior's row from its
/// equipment — <c>MovementRuleset.ResolveLoadoutProfile</c>,
/// <see cref="MovementRouteRules.CanonicalOpponentIndex"/>, and
/// <see cref="LoadoutCompositionCounts.Add"/>. Until 2026-08-16 all three
/// pattern-matched the full <c>(WeaponId, ArmorId, ShieldId)</c> triple with
/// <see cref="ArmorId.LightOrganic"/> hardcoded in every arm and threw for
/// anything else. That guard was correct while one armor existed and became a
/// crash the moment a preset fielded a second, because the client's own
/// movement-preset selector offers all three equipment-relative presets.
/// <para>
/// Armor now reaches movement through
/// <see cref="ArmorWeightClasses.ScaleMovementSpeed"/>, applied once per
/// warrior at spawn, so these keys read weapon and shield only. These tests
/// pin that: they fail if anybody reintroduces armor into one of the three
/// keys, which would restore the crash.
/// </para>
/// </remarks>
public sealed class ArmorMovementKeyTests
{
    private static readonly ArmorId[] EveryArmor = Enum.GetValues<ArmorId>();

    /// <summary>
    /// A warrior's movement profile is the same row whatever armor it wears.
    /// </summary>
    [Theory]
    [InlineData(MovementPresetId.EquipmentRelativeFootworkV6)]
    [InlineData(MovementPresetId.EquipmentRelativeFootworkV7)]
    [InlineData(MovementPresetId.MonotoneAllyClearanceV9)]
    public void ResolveLoadoutProfileIgnoresArmor(MovementPresetId preset)
    {
        var rules = MovementPresetRegistry.Get(preset);
        Assert.True(rules.UsesEquipmentRelativeFootwork);

        (WeaponId Weapon, ShieldId Shield)[] canonicalPairs =
        [
            (WeaponId.Kampilan, ShieldId.None),
            (WeaponId.Wasay, ShieldId.None),
            (WeaponId.Kalis, ShieldId.None),
            (WeaponId.Itak, ShieldId.None),
            (WeaponId.Kalis, ShieldId.TallHardwood),
            (WeaponId.Itak, ShieldId.TallHardwood),
        ];

        foreach (var (weapon, shield) in canonicalPairs)
        {
            var expected = rules.ResolveLoadoutProfile(
                new CombatLoadout(weapon, ArmorId.LightOrganic, shield));

            foreach (var armor in EveryArmor)
            {
                var actual = rules.ResolveLoadoutProfile(
                    new CombatLoadout(weapon, armor, shield));

                Assert.Equal(expected, actual);
            }
        }
    }

    /// <summary>
    /// The opponent-distance offset row is the same row whatever armor the
    /// opponent wears.
    /// </summary>
    [Fact]
    public void CanonicalOpponentIndexIgnoresArmor()
    {
        (WeaponId Weapon, ShieldId Shield, int Expected)[] cases =
        [
            (WeaponId.Kampilan, ShieldId.None, 0),
            (WeaponId.Wasay, ShieldId.None, 1),
            (WeaponId.Kalis, ShieldId.None, 2),
            (WeaponId.Itak, ShieldId.None, 3),
            (WeaponId.Kalis, ShieldId.TallHardwood, 4),
            (WeaponId.Itak, ShieldId.TallHardwood, 5),
        ];

        foreach (var (weapon, shield, expected) in cases)
        {
            foreach (var armor in EveryArmor)
            {
                Assert.Equal(
                    expected,
                    MovementRouteRules.CanonicalOpponentIndex(
                        new CombatLoadout(weapon, armor, shield)));
            }
        }
    }

    /// <summary>
    /// A warrior falls in the same composition bucket whatever armor it wears.
    /// </summary>
    [Fact]
    public void CompositionBucketingIgnoresArmor()
    {
        foreach (var armor in EveryArmor)
        {
            var counts = default(LoadoutCompositionCounts)
                .Add(new CombatLoadout(WeaponId.Kampilan, armor, ShieldId.None))
                .Add(new CombatLoadout(WeaponId.Kalis, armor, ShieldId.TallHardwood));

            Assert.Equal(1, counts.Kampilan);
            Assert.Equal(1, counts.KalisShield);
            Assert.Equal(0, counts.Wasay);
            Assert.Equal(0, counts.Itak);
        }
    }

    /// <summary>
    /// The weight class, not the loadout key, is what makes armor slow a
    /// warrior down, and it is a no-op for the legacy baseline every preset
    /// before version 7 fields.
    /// </summary>
    /// <remarks>
    /// The pace figures are provisional gameplay tuning under CLAUDE.md
    /// section 7. No source in the period states what any armor weighed or how
    /// much it slowed its wearer.
    /// </remarks>
    [Fact]
    public void WeightClassScalesMovementSpeedAndIsANoOpForTheLegacyBaseline()
    {
        const int Unencumbered = 10_000;

        Assert.Equal(
            Unencumbered,
            ArmorWeightClasses.ScaleMovementSpeed(Unencumbered, ArmorId.LightOrganic));
        Assert.Equal(
            Unencumbered,
            ArmorWeightClasses.ScaleMovementSpeed(Unencumbered, ArmorId.None));
        Assert.Equal(
            Unencumbered,
            ArmorWeightClasses.ScaleMovementSpeed(Unencumbered, ArmorId.QuiltedCotton));

        Assert.Equal(
            9_700,
            ArmorWeightClasses.ScaleMovementSpeed(Unencumbered, ArmorId.HideCorselet));
        Assert.Equal(
            9_400,
            ArmorWeightClasses.ScaleMovementSpeed(Unencumbered, ArmorId.RigidCuirass));
        Assert.Equal(
            9_400,
            ArmorWeightClasses.ScaleMovementSpeed(Unencumbered, ArmorId.ImportedIron));
    }

    /// <summary>
    /// Every armor identity maps to exactly one weight class, and an undeclared
    /// value throws rather than silently inheriting the light class and its
    /// absent penalty.
    /// </summary>
    [Fact]
    public void EveryArmorResolvesAWeightClassAndAnUndeclaredValueThrows()
    {
        foreach (var armor in EveryArmor)
        {
            var weightClass = ArmorWeightClasses.Resolve(armor);
            Assert.True(Enum.IsDefined(weightClass));
        }

        Assert.Throws<ArgumentOutOfRangeException>(
            () => ArmorWeightClasses.Resolve((ArmorId)999));
    }
}
