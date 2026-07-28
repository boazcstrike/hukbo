using Hukbo.Client.Settings;

namespace Hukbo.Client.Tests;

public sealed class MotionIntensityManagerTests
{
    [Fact]
    public void SelectChangesTheValueImmediatelyAndPersistsIt()
    {
        MotionIntensity? saved = null;
        var manager = new MotionIntensityManager(
            MotionIntensity.Full,
            value =>
            {
                saved = value;
                return true;
            });

        var changed = manager.TrySelect(MotionIntensity.Off);

        Assert.True(changed);
        Assert.Equal(MotionIntensity.Off, manager.Value);
        Assert.Equal(MotionIntensity.Off, saved);
    }

    [Fact]
    public void SelectingTheCurrentValueDoesNotChangeOrPersist()
    {
        var saveCount = 0;
        var manager = new MotionIntensityManager(
            MotionIntensity.Off,
            _ =>
            {
                saveCount++;
                return true;
            });

        Assert.False(manager.TrySelect(MotionIntensity.Off));
        Assert.Equal(MotionIntensity.Off, manager.Value);
        Assert.Equal(0, saveCount);
    }

    [Fact]
    public void SelectingAnUndefinedValueDoesNotChangeOrPersist()
    {
        var saveCount = 0;
        var manager = new MotionIntensityManager(
            MotionIntensity.Full,
            _ =>
            {
                saveCount++;
                return true;
            });

        Assert.False(manager.TrySelect((MotionIntensity)99));
        Assert.Equal(MotionIntensity.Full, manager.Value);
        Assert.Equal(0, saveCount);
    }

    [Fact]
    public void AnUndefinedInitialValueFallsBackToFull()
    {
        var manager = new MotionIntensityManager(
            (MotionIntensity)99,
            _ => true);

        Assert.Equal(MotionIntensity.Full, manager.Value);
    }

    [Fact]
    public void AFailedPersistStillLeavesTheSelectedValueActive()
    {
        var manager = new MotionIntensityManager(
            MotionIntensity.Full,
            _ => false);

        Assert.True(manager.TrySelect(MotionIntensity.Off));
        Assert.Equal(MotionIntensity.Off, manager.Value);
    }

    // The numeric values are part of the persisted settings-file contract:
    // renumbering or reordering them would silently resolve a stored
    // preference to a different level after an upgrade.
    [Fact]
    public void NumericValuesArePinned()
    {
        Assert.Equal(0, (int)MotionIntensity.Off);
        Assert.Equal(1, (int)MotionIntensity.Reduced);
        Assert.Equal(2, (int)MotionIntensity.Full);
        Assert.Equal(3, Enum.GetValues<MotionIntensity>().Length);
    }
}
