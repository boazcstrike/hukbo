using Hukbo.Client.Audio;

namespace Hukbo.Client.Tests;

public sealed class SoundVoicingTests
{
    // Literal expected values throughout: a test that reads SoundVoicing.Get
    // for the value it then asserts against moves in lockstep with the table
    // under test and proves nothing about it.

    [Fact]
    public void Get_ClashShieldWasay_IsHeaviestAndLowestPitched()
    {
        var (level, pitch) = SoundVoicing.Get(GameSoundId.ClashShieldWasay);

        Assert.Equal(1.00f, level);
        Assert.Equal(-0.35f, pitch);
    }

    [Fact]
    public void Get_ClashShieldKampilan_IsSecondHeaviestAndSecondLowestPitched()
    {
        var (level, pitch) = SoundVoicing.Get(GameSoundId.ClashShieldKampilan);

        Assert.Equal(0.95f, level);
        Assert.Equal(-0.20f, pitch);
    }

    [Fact]
    public void Get_ClashShieldKalis_IsMiddleLevelAndMiddlePitch()
    {
        var (level, pitch) = SoundVoicing.Get(GameSoundId.ClashShieldKalis);

        Assert.Equal(0.80f, level);
        Assert.Equal(0.10f, pitch);
    }

    [Fact]
    public void Get_ClashShieldItak_IsLightestAndHighestPitched()
    {
        var (level, pitch) = SoundVoicing.Get(GameSoundId.ClashShieldItak);

        Assert.Equal(0.60f, level);
        Assert.Equal(0.35f, pitch);
    }

    [Fact]
    public void Get_FourClashSlots_OrderByDescendingLevel()
    {
        var wasay = SoundVoicing.Get(GameSoundId.ClashShieldWasay).Level;
        var kampilan = SoundVoicing.Get(GameSoundId.ClashShieldKampilan).Level;
        var kalis = SoundVoicing.Get(GameSoundId.ClashShieldKalis).Level;
        var itak = SoundVoicing.Get(GameSoundId.ClashShieldItak).Level;

        Assert.True(wasay > kampilan);
        Assert.True(kampilan > kalis);
        Assert.True(kalis > itak);
    }

    [Fact]
    public void Get_FourClashSlots_OrderByAscendingPitch()
    {
        var wasay = SoundVoicing.Get(GameSoundId.ClashShieldWasay).Pitch;
        var kampilan = SoundVoicing.Get(GameSoundId.ClashShieldKampilan).Pitch;
        var kalis = SoundVoicing.Get(GameSoundId.ClashShieldKalis).Pitch;
        var itak = SoundVoicing.Get(GameSoundId.ClashShieldItak).Pitch;

        Assert.True(wasay < kampilan);
        Assert.True(kampilan < kalis);
        Assert.True(kalis < itak);
    }

    [Fact]
    public void Get_EveryNonClashSlot_ReturnsIdentityVoicing()
    {
        // GameSoundId is internal, so a [Theory]/[InlineData] parameter of
        // that type is an accessibility error; a single [Fact] looping over
        // the catalog covers the same ground.
        var clashSlots = new[]
        {
            GameSoundId.ClashShieldKampilan,
            GameSoundId.ClashShieldWasay,
            GameSoundId.ClashShieldKalis,
            GameSoundId.ClashShieldItak,
        };

        foreach (var sound in Enum.GetValues<GameSoundId>().Except(clashSlots))
        {
            var (level, pitch) = SoundVoicing.Get(sound);

            Assert.Equal(1.0f, level);
            Assert.Equal(0.0f, pitch);
        }
    }

    [Fact]
    public void Get_EveryDeclaredSound_ReturnsPitchInsideMonoGamesRange()
    {
        foreach (var sound in Enum.GetValues<GameSoundId>())
        {
            var (_, pitch) = SoundVoicing.Get(sound);

            Assert.InRange(pitch, -1f, 1f);
        }
    }

    [Fact]
    public void Get_EveryNonClashSlot_CoversTheWholeCatalogExceptTheFourClashSlots()
    {
        var clashSlots = new[]
        {
            GameSoundId.ClashShieldKampilan,
            GameSoundId.ClashShieldWasay,
            GameSoundId.ClashShieldKalis,
            GameSoundId.ClashShieldItak,
        };

        var nonClashSlots = Enum.GetValues<GameSoundId>()
            .Except(clashSlots)
            .ToArray();

        // 22 non-clash slots is the whole 26-member catalog minus the 4
        // voiced ones; if a future slot is added without updating this test,
        // this count assertion is the tripwire.
        Assert.Equal(22, nonClashSlots.Length);
    }
}
