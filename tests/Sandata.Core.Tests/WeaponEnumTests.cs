using Sandata.Core.Weapons;

namespace Sandata.Core.Tests;

/// <summary>
/// Pins every numeric value declared on the five enumerations task 12 of
/// Sandata's scaffold plan introduces, and proves the
/// structural invariants the task table requires: <see cref="FirearmId"/> is
/// dense from zero with exactly 38 members, <see cref="CaliberFamily"/> has
/// exactly eight members, <see cref="MechanismGroup"/> has exactly four
/// members, and <see cref="FireModeSet"/>'s flag values are distinct powers
/// of two. None of these enumerations carries behaviour or stat data — task
/// 22 authors the 38-row catalog that gives them one.
/// </summary>
public sealed class WeaponEnumTests
{
    [Fact]
    public void FirearmId_IsDenseFromZeroWith38Members()
    {
        var values = Enum.GetValues<FirearmId>();

        Assert.Equal(38, values.Length);
        Assert.Equal(Enumerable.Range(0, 38), values.Select(v => (int)v).OrderBy(v => v));
    }

    [Theory]
    [InlineData(FirearmId.Ak47, 0)]
    [InlineData(FirearmId.Akm, 1)]
    [InlineData(FirearmId.Ak74M, 2)]
    [InlineData(FirearmId.Ak12, 3)]
    [InlineData(FirearmId.Ak122023, 4)]
    [InlineData(FirearmId.Ak15, 5)]
    [InlineData(FirearmId.M16A4, 6)]
    [InlineData(FirearmId.M4, 7)]
    [InlineData(FirearmId.M4A1, 8)]
    [InlineData(FirearmId.Mk18Mod1, 9)]
    [InlineData(FirearmId.M7, 10)]
    [InlineData(FirearmId.Xm8, 11)]
    [InlineData(FirearmId.Hk416A5, 12)]
    [InlineData(FirearmId.Hk416F, 13)]
    [InlineData(FirearmId.G36, 14)]
    [InlineData(FirearmId.Mk16ScarL, 15)]
    [InlineData(FirearmId.Mk17ScarH, 16)]
    [InlineData(FirearmId.AugA3, 17)]
    [InlineData(FirearmId.TavorX95, 18)]
    [InlineData(FirearmId.Qbz191, 19)]
    [InlineData(FirearmId.Qbz951, 20)]
    [InlineData(FirearmId.L85A3, 21)]
    [InlineData(FirearmId.CzBren2, 22)]
    [InlineData(FirearmId.BerettaArx160, 23)]
    [InlineData(FirearmId.Beretta92Fs, 24)]
    [InlineData(FirearmId.BerettaApxA1, 25)]
    [InlineData(FirearmId.Glock17Gen5, 26)]
    [InlineData(FirearmId.Glock19Gen5, 27)]
    [InlineData(FirearmId.SigM17, 28)]
    [InlineData(FirearmId.SigM18, 29)]
    [InlineData(FirearmId.SigP226, 30)]
    [InlineData(FirearmId.SmithWessonMp9M20, 31)]
    [InlineData(FirearmId.HkVp9, 32)]
    [InlineData(FirearmId.HkUsp, 33)]
    [InlineData(FirearmId.CzP10C, 34)]
    [InlineData(FirearmId.WaltherPdpFs4, 35)]
    [InlineData(FirearmId.Mp443Grach, 36)]
    [InlineData(FirearmId.Qsz92, 37)]
    public void FirearmId_NumericValueIsPinned(FirearmId id, int expected)
    {
        Assert.Equal(expected, (int)id);
    }

    [Fact]
    public void WeaponClass_HasExactlyTwoMembers()
    {
        Assert.Equal(2, Enum.GetValues<WeaponClass>().Length);
    }

    [Theory]
    [InlineData(WeaponClass.Rifle, 0)]
    [InlineData(WeaponClass.Pistol, 1)]
    public void WeaponClass_NumericValueIsPinned(WeaponClass value, int expected)
    {
        Assert.Equal(expected, (int)value);
    }

    [Fact]
    public void CaliberFamily_HasExactlyEightMembers()
    {
        Assert.Equal(8, Enum.GetValues<CaliberFamily>().Length);
    }

    [Theory]
    [InlineData(CaliberFamily.Cal762X39, 0)]
    [InlineData(CaliberFamily.Cal545X39, 1)]
    [InlineData(CaliberFamily.Cal556X45, 2)]
    [InlineData(CaliberFamily.Cal762X51, 3)]
    [InlineData(CaliberFamily.Cal68X51, 4)]
    [InlineData(CaliberFamily.Cal58X42, 5)]
    [InlineData(CaliberFamily.Cal9X19, 6)]
    [InlineData(CaliberFamily.Cal58X21, 7)]
    public void CaliberFamily_NumericValueIsPinned(CaliberFamily value, int expected)
    {
        Assert.Equal(expected, (int)value);
    }

    [Fact]
    public void MechanismGroup_HasExactlyFourMembers()
    {
        Assert.Equal(4, Enum.GetValues<MechanismGroup>().Length);
    }

    [Theory]
    [InlineData(MechanismGroup.Ak, 0)]
    [InlineData(MechanismGroup.Ar, 1)]
    [InlineData(MechanismGroup.Bullpup, 2)]
    [InlineData(MechanismGroup.Pistol, 3)]
    public void MechanismGroup_NumericValueIsPinned(MechanismGroup value, int expected)
    {
        Assert.Equal(expected, (int)value);
    }

    [Fact]
    public void FireModeSet_FlagValuesAreDistinctPowersOfTwo()
    {
        var values = Enum.GetValues<FireModeSet>().Select(v => (int)v).ToArray();

        foreach (var value in values)
        {
            Assert.True(value > 0 && (value & (value - 1)) == 0,
                $"{value} is not a power of two.");
        }

        Assert.Equal(values.Length, values.Distinct().Count());
    }

    [Theory]
    [InlineData(FireModeSet.Safe, 1)]
    [InlineData(FireModeSet.Single, 2)]
    [InlineData(FireModeSet.Burst2, 4)]
    [InlineData(FireModeSet.Burst3, 8)]
    [InlineData(FireModeSet.Auto, 16)]
    public void FireModeSet_NumericValueIsPinned(FireModeSet value, int expected)
    {
        Assert.Equal(expected, (int)value);
    }
}
