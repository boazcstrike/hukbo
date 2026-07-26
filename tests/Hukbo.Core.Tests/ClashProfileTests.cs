using Hukbo.Core.Combat;

namespace Hukbo.Core.Tests;

public sealed class ClashProfileTests
{
    [Fact]
    public void Constructor_RoundTripsEveryTable()
    {
        // Every cell is given a distinct value, so a table wired to the wrong
        // accessor, or a matrix transposed between defender and attacker,
        // fails here rather than surviving as a plausible-looking number.
        var weapons = Enum.GetValues<WeaponId>();
        var matrix = new Dictionary<(WeaponId Defender, WeaponId Attacker), int>();
        foreach (var defender in weapons)
        {
            foreach (var attacker in weapons)
            {
                matrix[(defender, attacker)] = MatrixCell(defender, attacker);
            }
        }

        var voidChannel = weapons.ToDictionary(weapon => weapon, VoidCell);
        var hardShareBases = weapons.ToDictionary(weapon => weapon, HardShareBaseCell);
        var hardShareMultipliers =
            weapons.ToDictionary(weapon => weapon, HardShareMultiplierCell);

        var profile = new ClashProfile(
            weaponIntercept: matrix,
            shieldIntercept: 2_400,
            voidChannel: voidChannel,
            hardShareBases: hardShareBases,
            hardShareMultipliers: hardShareMultipliers,
            minimumHardShareBasisPoints: 500,
            maximumHardShareBasisPoints: 6_000,
            maximumInterceptionBasisPoints: 5_500);

        foreach (var defender in weapons)
        {
            foreach (var attacker in weapons)
            {
                Assert.Equal(
                    MatrixCell(defender, attacker),
                    profile.ResolveWeaponIntercept(defender, attacker));
            }

            Assert.Equal(VoidCell(defender), profile.ResolveVoid(defender));
            Assert.Equal(
                HardShareBaseCell(defender),
                profile.ResolveHardShareBase(defender));
            Assert.Equal(
                HardShareMultiplierCell(defender),
                profile.ResolveHardShareMultiplier(defender));
        }

        Assert.Equal(2_400, profile.ShieldInterceptBasisPoints);
        Assert.Equal(2_400, profile.ResolveShieldIntercept(ShieldId.TallHardwood));
        Assert.Equal(0, profile.ResolveShieldIntercept(ShieldId.None));
        Assert.Equal(500, profile.MinimumHardShareBasisPoints);
        Assert.Equal(6_000, profile.MaximumHardShareBasisPoints);
        Assert.Equal(5_500, profile.MaximumInterceptionBasisPoints);
    }

    [Fact]
    public void Neutral_ReportsZeroInterceptionForEveryRosterPair()
    {
        // Iterating the declared enums rather than the shipped roster: it
        // covers every roster pairing by construction and keeps this case
        // independent of any later roster or preset change.
        foreach (var defenderWeapon in Enum.GetValues<WeaponId>())
        {
            Assert.Equal(0, ClashProfile.Neutral.ResolveVoid(defenderWeapon));

            foreach (var attackerWeapon in Enum.GetValues<WeaponId>())
            {
                Assert.Equal(
                    0,
                    ClashProfile.Neutral.ResolveWeaponIntercept(
                        defenderWeapon,
                        attackerWeapon));
            }
        }

        foreach (var defenderShield in Enum.GetValues<ShieldId>())
        {
            Assert.Equal(
                0,
                ClashProfile.Neutral.ResolveShieldIntercept(defenderShield));
        }

        Assert.Equal(0, ClashProfile.Neutral.ShieldInterceptBasisPoints);
    }

    private static int MatrixCell(WeaponId defender, WeaponId attacker) =>
        (((int)defender * 10) + (int)attacker) * 10;

    private static int VoidCell(WeaponId defender) => 900 + (int)defender;

    private static int HardShareBaseCell(WeaponId attacker) => 1_200 + (int)attacker;

    private static int HardShareMultiplierCell(WeaponId defender) =>
        700 + (int)defender;
}
