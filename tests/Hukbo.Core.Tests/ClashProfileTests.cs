using Hukbo.Core.Combat;

namespace Hukbo.Core.Tests;

public sealed class ClashProfileTests
{
    [Fact]
    public void Constructor_RoundTripsEveryTable()
    {
        // Every cell is given a distinct value, so a table wired to the wrong
        // accessor, or a matrix transposed between defender and attacker (or
        // between the defender's weapon and shield), fails here rather than
        // surviving as a plausible-looking number.
        var weapons = Enum.GetValues<WeaponId>();
        var shields = Enum.GetValues<ShieldId>();

        var matrix = new Dictionary<
            (WeaponId Defender, ShieldId DefenderShield, WeaponId Attacker), int>();
        foreach (var defender in weapons)
        {
            foreach (var defenderShield in shields)
            {
                foreach (var attacker in weapons)
                {
                    matrix[(defender, defenderShield, attacker)] =
                        MatrixCell(defender, defenderShield, attacker);
                }
            }
        }

        var voidChannel = new Dictionary<(WeaponId Weapon, ShieldId Shield), int>();
        foreach (var weapon in weapons)
        {
            foreach (var shield in shields)
            {
                voidChannel[(weapon, shield)] = VoidCell(weapon, shield);
            }
        }

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
            foreach (var defenderShield in shields)
            {
                foreach (var attacker in weapons)
                {
                    Assert.Equal(
                        MatrixCell(defender, defenderShield, attacker),
                        profile.ResolveWeaponIntercept(defender, defenderShield, attacker));
                }

                Assert.Equal(
                    VoidCell(defender, defenderShield),
                    profile.ResolveVoid(defender, defenderShield));
            }

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
    public void Neutral_ReportsZeroInterceptionForEveryWeaponAndShieldCombination()
    {
        // Neutral resolves any key to zero -- it does not throw for an
        // undeclared cell the way a real profile does, and it needs no
        // roster to answer for every weapon and shield. Iterating the
        // declared enums rather than a shipped roster keeps this case
        // independent of any later roster or preset change.
        foreach (var defenderWeapon in Enum.GetValues<WeaponId>())
        {
            foreach (var defenderShield in Enum.GetValues<ShieldId>())
            {
                Assert.Equal(
                    0,
                    ClashProfile.Neutral.ResolveVoid(defenderWeapon, defenderShield));

                foreach (var attackerWeapon in Enum.GetValues<WeaponId>())
                {
                    Assert.Equal(
                        0,
                        ClashProfile.Neutral.ResolveWeaponIntercept(
                            defenderWeapon,
                            defenderShield,
                            attackerWeapon));
                }
            }

            Assert.Equal(0, ClashProfile.Neutral.ResolveHardShareBase(defenderWeapon));
            Assert.Equal(0, ClashProfile.Neutral.ResolveHardShareMultiplier(defenderWeapon));
        }

        foreach (var defenderShield in Enum.GetValues<ShieldId>())
        {
            Assert.Equal(
                0,
                ClashProfile.Neutral.ResolveShieldIntercept(defenderShield));
        }

        Assert.Equal(0, ClashProfile.Neutral.ShieldInterceptBasisPoints);
    }

    /// <summary>
    /// D4: coverage of the roster is no longer this type's job, so a profile
    /// declaring no cells at all -- unlike <see cref="ClashProfile.Neutral"/>
    /// -- throws rather than resolving to zero.
    /// </summary>
    [Fact]
    public void ConstructorWithNoCells_ThrowsOnResolveRatherThanReturningZero()
    {
        var empty = new ClashProfile(
            weaponIntercept: new Dictionary<
                (WeaponId Defender, ShieldId DefenderShield, WeaponId Attacker), int>(),
            shieldIntercept: 0,
            voidChannel: new Dictionary<(WeaponId Weapon, ShieldId Shield), int>(),
            hardShareBases: new Dictionary<WeaponId, int>(),
            hardShareMultipliers: new Dictionary<WeaponId, int>(),
            minimumHardShareBasisPoints: 0,
            maximumHardShareBasisPoints: ClashProfile.BasisPointScale,
            maximumInterceptionBasisPoints: ClashProfile.BasisPointScale);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            empty.ResolveWeaponIntercept(WeaponId.Kalis, ShieldId.None, WeaponId.Kalis));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            empty.ResolveVoid(WeaponId.Kalis, ShieldId.None));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            empty.ResolveHardShareBase(WeaponId.Kalis));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            empty.ResolveHardShareMultiplier(WeaponId.Kalis));
    }

    /// <summary>
    /// Every declared range is inclusive at both ends and rejects one step
    /// outside. The interception ceiling is the one field whose lower bound is
    /// one rather than zero: a ceiling of zero would rescale every channel to
    /// nothing and make the whole type inert while still validating.
    /// </summary>
    [Fact]
    public void Constructor_AcceptsTheExactBoundsAndRejectsOneStepOutside()
    {
        const int Low = 0;
        const int High = ClashProfile.BasisPointScale;

        // Accepted: each field at both of its exact bounds.
        BuildBoundedProfile(matrixCell: Low);
        BuildBoundedProfile(matrixCell: High);
        BuildBoundedProfile(shieldIntercept: Low);
        BuildBoundedProfile(shieldIntercept: High);
        BuildBoundedProfile(voidChannel: Low);
        BuildBoundedProfile(voidChannel: High);
        BuildBoundedProfile(hardShareBase: Low);
        BuildBoundedProfile(hardShareBase: High);
        BuildBoundedProfile(hardShareMultiplier: Low);
        BuildBoundedProfile(hardShareMultiplier: High);
        BuildBoundedProfile(minimumHardShare: Low);
        BuildBoundedProfile(minimumHardShare: High, maximumHardShare: High);
        BuildBoundedProfile(minimumHardShare: Low, maximumHardShare: Low);
        BuildBoundedProfile(maximumHardShare: High);
        BuildBoundedProfile(maximumInterception: 1);
        BuildBoundedProfile(maximumInterception: High);

        // Rejected: one step below every lower bound.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => BuildBoundedProfile(matrixCell: Low - 1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => BuildBoundedProfile(shieldIntercept: Low - 1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => BuildBoundedProfile(voidChannel: Low - 1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => BuildBoundedProfile(hardShareBase: Low - 1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => BuildBoundedProfile(hardShareMultiplier: Low - 1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => BuildBoundedProfile(minimumHardShare: Low - 1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => BuildBoundedProfile(maximumHardShare: Low - 1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => BuildBoundedProfile(maximumInterception: 0));

        // Rejected: one step above every upper bound.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => BuildBoundedProfile(matrixCell: High + 1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => BuildBoundedProfile(shieldIntercept: High + 1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => BuildBoundedProfile(voidChannel: High + 1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => BuildBoundedProfile(hardShareBase: High + 1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => BuildBoundedProfile(hardShareMultiplier: High + 1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => BuildBoundedProfile(
                minimumHardShare: High + 1,
                maximumHardShare: High + 1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => BuildBoundedProfile(maximumHardShare: High + 1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => BuildBoundedProfile(maximumInterception: High + 1));

        // Rejected: inverted clamp bounds, which is a plain ArgumentException
        // rather than a range violation because both values are individually
        // legal.
        Assert.Throws<ArgumentException>(
            () => BuildBoundedProfile(
                minimumHardShare: 600,
                maximumHardShare: 599));
    }

    private static ClashProfile BuildBoundedProfile(
        int matrixCell = 0,
        int shieldIntercept = 0,
        int voidChannel = 0,
        int hardShareBase = 0,
        int hardShareMultiplier = 0,
        int minimumHardShare = 0,
        int maximumHardShare = ClashProfile.BasisPointScale,
        int maximumInterception = ClashProfile.BasisPointScale)
    {
        var weapons = Enum.GetValues<WeaponId>();
        var shields = Enum.GetValues<ShieldId>();

        var matrix = new Dictionary<
            (WeaponId Defender, ShieldId DefenderShield, WeaponId Attacker), int>();
        foreach (var defender in weapons)
        {
            foreach (var defenderShield in shields)
            {
                foreach (var attacker in weapons)
                {
                    matrix[(defender, defenderShield, attacker)] = matrixCell;
                }
            }
        }

        var voidTable = new Dictionary<(WeaponId Weapon, ShieldId Shield), int>();
        foreach (var weapon in weapons)
        {
            foreach (var shield in shields)
            {
                voidTable[(weapon, shield)] = voidChannel;
            }
        }

        return new ClashProfile(
            matrix,
            shieldIntercept,
            voidTable,
            weapons.ToDictionary(weapon => weapon, _ => hardShareBase),
            weapons.ToDictionary(weapon => weapon, _ => hardShareMultiplier),
            minimumHardShare,
            maximumHardShare,
            maximumInterception);
    }

    private static int MatrixCell(WeaponId defender, ShieldId defenderShield, WeaponId attacker) =>
        ((int)defender * 1_000) + ((int)defenderShield * 100) + ((int)attacker * 10);

    private static int VoidCell(WeaponId weapon, ShieldId shield) =>
        900 + ((int)weapon * 10) + (int)shield;

    private static int HardShareBaseCell(WeaponId attacker) => 1_200 + (int)attacker;

    private static int HardShareMultiplierCell(WeaponId defender) =>
        700 + (int)defender;
}
