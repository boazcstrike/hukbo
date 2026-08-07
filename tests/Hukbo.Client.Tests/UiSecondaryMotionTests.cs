using Hukbo.Client.Settings;
using Hukbo.Client.UI;

namespace Hukbo.Client.Tests;

public sealed class UiSecondaryMotionTests
{
    [Fact]
    public void NewEventDuration_IsInsidePublishedBand()
    {
        Assert.InRange(
            UiSecondaryMotion.NewEventDuration,
            TimeSpan.FromMilliseconds(160),
            TimeSpan.FromMilliseconds(220));
    }

    [Fact]
    public void SelectionAccentDuration_IsInsidePublishedBand()
    {
        Assert.InRange(
            UiSecondaryMotion.SelectionAccentDuration,
            TimeSpan.FromMilliseconds(140),
            TimeSpan.FromMilliseconds(180));
    }

    [Fact]
    public void SelectorMarkerDuration_IsInsidePublishedBand()
    {
        Assert.InRange(
            UiSecondaryMotion.SelectorMarkerDuration,
            TimeSpan.FromMilliseconds(90),
            TimeSpan.FromMilliseconds(140));
    }

    [Fact]
    public void ActiveStripDuration_IsInsidePublishedBand()
    {
        Assert.InRange(
            UiSecondaryMotion.ActiveStripDuration,
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(140));
    }

    [Fact]
    public void StatusBadgeDuration_IsInsidePublishedBand()
    {
        Assert.InRange(
            UiSecondaryMotion.StatusBadgeDuration,
            TimeSpan.FromMilliseconds(450),
            TimeSpan.FromMilliseconds(650));
    }

    [Theory]
    [InlineData(MotionIntensity.Off, false)]
    [InlineData(MotionIntensity.Reduced, true)]
    [InlineData(MotionIntensity.Full, true)]
    public void IsEnabled_ReflectsDefinedIntensity(
        MotionIntensity intensity,
        bool expected)
    {
        Assert.Equal(expected, UiSecondaryMotion.IsEnabled(intensity));
    }

    [Fact]
    public void IsEnabled_UndefinedValueNormalizesToDisabled()
    {
        var undefined = (MotionIntensity)999;

        Assert.False(UiSecondaryMotion.IsEnabled(undefined));
    }
}
