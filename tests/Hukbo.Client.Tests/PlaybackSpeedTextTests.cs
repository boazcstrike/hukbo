using Hukbo.Client.Presentation;

namespace Hukbo.Client.Tests;

/// <summary>
/// A battle that quietly plays slower than asked fails the spectator-discovery
/// test, so the status line names both speeds while the governor holds one back.
/// </summary>
public sealed class PlaybackSpeedTextTests
{
    [Theory]
    [InlineData(1, 1d, "1x")]
    [InlineData(2, 2d, "2x")]
    [InlineData(4, 4d, "4x")]
    public void AnUngovernedSpeedShowsOnlyTheRequestedSpeed(
        int requested,
        double effective,
        string expected) =>
        Assert.Equal(
            expected,
            PlaybackSpeedText.Format(requested, effective, isGoverned: false));

    [Theory]
    [InlineData(4, 2.6d, "4x (holding 2.6x)")]
    [InlineData(2, 0.26d, "2x (holding 0.3x)")]
    [InlineData(4, 3.04d, "4x (holding 3.0x)")]
    public void AGovernedSpeedShowsBothSpeeds(
        int requested,
        double effective,
        string expected) =>
        Assert.Equal(
            expected,
            PlaybackSpeedText.Format(requested, effective, isGoverned: true));

    [Fact]
    public void TheHeldSpeedIsFormattedWithAPointWhateverTheCulture()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture =
                new System.Globalization.CultureInfo("de-DE");

            Assert.Equal(
                "4x (holding 2.6x)",
                PlaybackSpeedText.Format(4, 2.6d, isGoverned: true));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }
}
