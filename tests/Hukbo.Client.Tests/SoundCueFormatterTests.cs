using Hukbo.Client.Audio;

namespace Hukbo.Client.Tests;

public sealed class SoundCueFormatterTests
{
    [Fact]
    public void Format_ShowsTickFileBaseNameAndStatus() =>
        Assert.Equal(
            "T00042  death  PLAYED",
            SoundCueFormatter.Format(
                new SoundCue(42, GameSoundId.Death, SoundCueStatus.Played, 1)));

    [Fact]
    public void Format_AppendsARepeatCountOnlyWhenItIsGreaterThanOne()
    {
        Assert.Equal(
            "T00007  attack-itak  LIMITED x12",
            SoundCueFormatter.Format(
                new SoundCue(
                    7,
                    GameSoundId.AttackItak,
                    SoundCueStatus.Suppressed,
                    12)));
        Assert.DoesNotContain(
            "x1",
            SoundCueFormatter.Format(
                new SoundCue(7, GameSoundId.Draw, SoundCueStatus.Muted, 1)));
    }

    // The status parameter is an int because xunit requires public test
    // methods and SoundCueStatus is internal to Hukbo.Client.
    [Theory]
    [InlineData(0, "PLAYED")]
    [InlineData(1, "NO FILE")]
    [InlineData(2, "FAILED")]
    [InlineData(3, "MUTED")]
    [InlineData(4, "LIMITED")]
    public void GetStatusLabel_NamesEveryCueStatus(
        int status,
        string expected) =>
        Assert.Equal(
            expected,
            SoundCueFormatter.GetStatusLabel((SoundCueStatus)status));

    [Fact]
    public void GetStatusLabel_RejectsAnUndeclaredStatus() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SoundCueFormatter.GetStatusLabel((SoundCueStatus)99));

    [Fact]
    public void FormatAvailability_DistinguishesAFullFolderFromAGappedOne()
    {
        Assert.Equal("ALL 9 READY", SoundCueFormatter.FormatAvailability(0, 9));
        Assert.Equal(
            "MISSING 4/9",
            SoundCueFormatter.FormatAvailability(4, 9));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SoundCueFormatter.FormatAvailability(-1, 9));
    }
}
