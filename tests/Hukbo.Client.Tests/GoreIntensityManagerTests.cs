using Hukbo.Client.Settings;

namespace Hukbo.Client.Tests;

public sealed class GoreIntensityManagerTests
{
    [Fact]
    public void SelectChangesTheValueImmediatelyAndPersistsIt()
    {
        GoreIntensity? saved = null;
        var manager = new GoreIntensityManager(
            GoreIntensity.Stylized,
            value =>
            {
                saved = value;
                return true;
            });

        var changed = manager.TrySelect(GoreIntensity.Full);

        Assert.True(changed);
        Assert.Equal(GoreIntensity.Full, manager.Value);
        Assert.Equal(GoreIntensity.Full, saved);
    }

    [Fact]
    public void SelectingTheCurrentValueDoesNotChangeOrPersist()
    {
        var saveCount = 0;
        var manager = new GoreIntensityManager(
            GoreIntensity.Off,
            _ =>
            {
                saveCount++;
                return true;
            });

        Assert.False(manager.TrySelect(GoreIntensity.Off));
        Assert.Equal(GoreIntensity.Off, manager.Value);
        Assert.Equal(0, saveCount);
    }

    [Fact]
    public void SelectingAnUndefinedValueDoesNotChangeOrPersist()
    {
        var saveCount = 0;
        var manager = new GoreIntensityManager(
            GoreIntensity.Stylized,
            _ =>
            {
                saveCount++;
                return true;
            });

        Assert.False(manager.TrySelect((GoreIntensity)99));
        Assert.Equal(GoreIntensity.Stylized, manager.Value);
        Assert.Equal(0, saveCount);
    }

    [Fact]
    public void AnUndefinedInitialValueFallsBackToStylized()
    {
        var manager = new GoreIntensityManager(
            (GoreIntensity)99,
            _ => true);

        Assert.Equal(GoreIntensity.Stylized, manager.Value);
    }

    [Fact]
    public void AFailedPersistStillLeavesTheSelectedValueActive()
    {
        var manager = new GoreIntensityManager(
            GoreIntensity.Stylized,
            _ => false);

        Assert.True(manager.TrySelect(GoreIntensity.Off));
        Assert.Equal(GoreIntensity.Off, manager.Value);
    }
}
