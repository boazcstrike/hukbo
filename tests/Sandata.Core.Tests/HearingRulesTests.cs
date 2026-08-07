using Sandata.Core.Sensing;

namespace Sandata.Core.Tests;

/// <summary>
/// Pins every <see cref="HearingRules"/> radius at its exact boundary and
/// proves the design's "breaking glass is louder than gunfire" ordering.
/// Mirrors <c>WeaponLoweredRulesTests</c>' exact-threshold /
/// one-world-unit-beyond pairing style.
/// </summary>
public sealed class HearingRulesTests
{
    [Theory]
    [InlineData(SoundKind.BoltCutter, HearingRules.BoltCutterRadiusWu)]
    [InlineData(SoundKind.Smoke, HearingRules.SmokeRadiusWu)]
    [InlineData(SoundKind.HammerOrCrowbar, HearingRules.HammerOrCrowbarRadiusWu)]
    [InlineData(SoundKind.BreacherShotgun, HearingRules.BreacherShotgunRadiusWu)]
    [InlineData(SoundKind.Gunfire, HearingRules.GunfireRadiusWu)]
    [InlineData(SoundKind.BreakingGlass, HearingRules.BreakingGlassRadiusWu)]
    [InlineData(SoundKind.DeathScream, HearingRules.DeathScreamRadiusWu)]
    public void RadiusWu_MatchesPublishedConstant(SoundKind kind, int expectedRadiusWu)
    {
        Assert.Equal(expectedRadiusWu, HearingRules.RadiusWu(kind));
    }

    [Theory]
    [InlineData(SoundKind.BoltCutter, HearingRules.BoltCutterRadiusWu)]
    [InlineData(SoundKind.Smoke, HearingRules.SmokeRadiusWu)]
    [InlineData(SoundKind.HammerOrCrowbar, HearingRules.HammerOrCrowbarRadiusWu)]
    [InlineData(SoundKind.BreacherShotgun, HearingRules.BreacherShotgunRadiusWu)]
    [InlineData(SoundKind.Gunfire, HearingRules.GunfireRadiusWu)]
    [InlineData(SoundKind.BreakingGlass, HearingRules.BreakingGlassRadiusWu)]
    [InlineData(SoundKind.DeathScream, HearingRules.DeathScreamRadiusWu)]
    public void ExactlyAtRadius_IsHeard(SoundKind kind, int radiusWu)
    {
        // Straight along the x axis so dx is exactly the radius and dy is
        // zero: the squared distance equals the squared radius exactly, with
        // no rounding from a diagonal.
        Assert.True(HearingRules.IsHeard(kind, dx: radiusWu, dy: 0));
    }

    [Theory]
    [InlineData(SoundKind.BoltCutter, HearingRules.BoltCutterRadiusWu)]
    [InlineData(SoundKind.Smoke, HearingRules.SmokeRadiusWu)]
    [InlineData(SoundKind.HammerOrCrowbar, HearingRules.HammerOrCrowbarRadiusWu)]
    [InlineData(SoundKind.BreacherShotgun, HearingRules.BreacherShotgunRadiusWu)]
    [InlineData(SoundKind.Gunfire, HearingRules.GunfireRadiusWu)]
    [InlineData(SoundKind.BreakingGlass, HearingRules.BreakingGlassRadiusWu)]
    [InlineData(SoundKind.DeathScream, HearingRules.DeathScreamRadiusWu)]
    public void OneWorldUnitBeyondRadius_IsNotHeard(SoundKind kind, int radiusWu)
    {
        Assert.False(HearingRules.IsHeard(kind, dx: radiusWu + 1, dy: 0));
    }

    [Fact]
    public void BreakingGlass_IsLouderThanGunfire()
    {
        // Design section 4: "breaking glass is louder than gunfire" — the one
        // qualitative ordering rule the design states outright among the
        // three provisional radii.
        Assert.True(HearingRules.BreakingGlassRadiusWu > HearingRules.GunfireRadiusWu);
    }

    [Fact]
    public void DeathScream_IsTheLoudestPublishedSound()
    {
        // Not stated as a numeric figure anywhere, but the research
        // consolidation's "death screams propagate and pull investigators"
        // is the widest-reaching effect named among the seven sound kinds,
        // so the provisional reconstruction makes it the loudest of all.
        Assert.True(HearingRules.DeathScreamRadiusWu > HearingRules.BreakingGlassRadiusWu);
        Assert.True(HearingRules.DeathScreamRadiusWu > HearingRules.BreacherShotgunRadiusWu);
    }

    [Fact]
    public void IsHeard_UsesSquaredDistanceOnADiagonal_NotEuclideanRounding()
    {
        // A 3-4-5 right triangle scaled by 80 lands exactly on
        // BreacherShotgunRadiusWu = 400: dx = 240, dy = 320, and
        // 240^2 + 320^2 = 57600 + 102400 = 160000 = 400^2.
        Assert.True(HearingRules.IsHeard(SoundKind.BreacherShotgun, dx: 240, dy: 320));

        // One world unit further out along the same diagonal direction is no
        // longer exactly on the boundary but is still further than the
        // radius, so it must not be heard: dx = 241, dy = 320 gives
        // 241^2 + 320^2 = 58081 + 102400 = 160481 > 160000.
        Assert.False(HearingRules.IsHeard(SoundKind.BreacherShotgun, dx: 241, dy: 320));
    }

    [Fact]
    public void RadiusSquaredWu_MatchesRadiusWuSquared()
    {
        Assert.Equal(
            (long)HearingRules.BreacherShotgunRadiusWu * HearingRules.BreacherShotgunRadiusWu,
            HearingRules.RadiusSquaredWu(SoundKind.BreacherShotgun));
    }

    [Fact]
    public void RadiusWu_UnrecognisedKind_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HearingRules.RadiusWu((SoundKind)999));
    }
}
