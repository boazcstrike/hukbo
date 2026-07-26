using Hukbo.Client.Audio;
using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Tests;

public sealed class SoundCatalogTests
{
    [Fact]
    public void AllSounds_ListsEveryDeclaredSlotExactlyOnce()
    {
        var declared = Enum.GetValues<GameSoundId>();

        Assert.Equal(declared.Length, SoundCatalog.AllSounds.Count);
        Assert.Equal(
            declared.Length,
            SoundCatalog.AllSounds.Distinct().Count());
        foreach (var sound in declared)
        {
            Assert.Contains(sound, SoundCatalog.AllSounds);
        }
    }

    [Fact]
    public void GetFileName_IsUniqueLowercaseKebabWavForEverySlot()
    {
        var fileNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var sound in Enum.GetValues<GameSoundId>())
        {
            var fileName = SoundCatalog.GetFileName(sound);

            Assert.EndsWith(
                SoundCatalog.SupportedExtension,
                fileName,
                StringComparison.Ordinal);
            Assert.Equal(fileName.ToLowerInvariant(), fileName);
            Assert.DoesNotContain(' ', fileName);
            Assert.DoesNotContain('_', fileName);
            Assert.True(
                fileNames.Add(fileName),
                $"{fileName} is claimed by more than one sound slot.");
        }
    }

    [Fact]
    public void GetBaseName_RejectsAnUndeclaredSlot() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SoundCatalog.GetBaseName((GameSoundId)999));

    [Fact]
    public void EveryDefinedWeapon_HasAnAttackSlot()
    {
        // The safety net for a newly added weapon: SoundCueMapper returns null
        // rather than throwing for an unmapped weapon, so this test is what
        // fails when a weapon arrives without a sound.
        foreach (var weapon in Enum.GetValues<WeaponId>())
        {
            var attackEvent = BattleEvent.Attack(
                sequence: 1,
                tick: 1,
                sourceEntityId: 1,
                targetEntityId: 2,
                damage: 5,
                factionId: 0,
                weapon,
                BodyPart.Chest);

            Assert.NotNull(SoundCueMapper.Map(attackEvent));
        }
    }

    // The status parameter is an int because xunit requires public test
    // methods and SoundBindingStatus is internal to Hukbo.Client.
    [Theory]
    [InlineData(0, "READY")]
    [InlineData(1, "MISSING")]
    [InlineData(2, "FAILED")]
    public void GetStatusLabel_NamesEveryBindingStatus(
        int status,
        string expected) =>
        Assert.Equal(
            expected,
            SoundCatalog.GetStatusLabel((SoundBindingStatus)status));

    [Fact]
    public void CountUnavailable_CountsEverythingThatIsNotReady()
    {
        SoundBinding[] bindings =
        [
            new(GameSoundId.Death, "death.wav", "/a/death.wav", SoundBindingStatus.Ready),
            new(GameSoundId.Draw, "draw.wav", null, SoundBindingStatus.Missing),
            new(
                GameSoundId.UiClick,
                "ui-click.wav",
                "/a/ui-click.wav",
                SoundBindingStatus.LoadFailed),
        ];

        Assert.Equal(2, SoundCatalog.CountUnavailable(bindings));
        Assert.Equal(0, SoundCatalog.CountUnavailable([]));
        Assert.Throws<ArgumentNullException>(
            () => SoundCatalog.CountUnavailable(null!));
    }
}
