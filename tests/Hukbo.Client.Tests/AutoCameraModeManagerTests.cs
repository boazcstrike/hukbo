using Hukbo.Client.Settings;

namespace Hukbo.Client.Tests;

public sealed class AutoCameraModeManagerTests
{
    [Fact]
    public void SelectChangesTheValueImmediatelyAndPersistsIt()
    {
        AutoCameraMode? saved = null;
        var manager = new AutoCameraModeManager(
            AutoCameraMode.Assisted,
            value =>
            {
                saved = value;
                return true;
            });

        var changed = manager.TrySelect(AutoCameraMode.Off);

        Assert.True(changed);
        Assert.Equal(AutoCameraMode.Off, manager.Value);
        Assert.Equal(AutoCameraMode.Off, saved);
    }

    [Fact]
    public void SelectingTheCurrentValueDoesNotChangeOrPersist()
    {
        var saveCount = 0;
        var manager = new AutoCameraModeManager(
            AutoCameraMode.Off,
            _ =>
            {
                saveCount++;
                return true;
            });

        Assert.False(manager.TrySelect(AutoCameraMode.Off));
        Assert.Equal(AutoCameraMode.Off, manager.Value);
        Assert.Equal(0, saveCount);
    }

    [Fact]
    public void SelectingAnUndefinedValueDoesNotChangeOrPersist()
    {
        var saveCount = 0;
        var manager = new AutoCameraModeManager(
            AutoCameraMode.Assisted,
            _ =>
            {
                saveCount++;
                return true;
            });

        Assert.False(manager.TrySelect((AutoCameraMode)99));
        Assert.Equal(AutoCameraMode.Assisted, manager.Value);
        Assert.Equal(0, saveCount);
    }

    [Fact]
    public void AnUndefinedInitialValueFallsBackToAssisted()
    {
        var manager = new AutoCameraModeManager(
            (AutoCameraMode)99,
            _ => true);

        Assert.Equal(AutoCameraMode.Assisted, manager.Value);
    }

    [Fact]
    public void AFailedPersistStillLeavesTheSelectedValueActive()
    {
        var manager = new AutoCameraModeManager(
            AutoCameraMode.Assisted,
            _ => false);

        Assert.True(manager.TrySelect(AutoCameraMode.Follow));
        Assert.Equal(AutoCameraMode.Follow, manager.Value);
    }

    // The numeric values are part of the persisted settings-file contract:
    // renumbering or reordering them would silently resolve a stored
    // preference to a different mode after an upgrade.
    [Fact]
    public void NumericValuesArePinned()
    {
        Assert.Equal(0, (int)AutoCameraMode.Off);
        Assert.Equal(1, (int)AutoCameraMode.Assisted);
        Assert.Equal(2, (int)AutoCameraMode.Follow);
        Assert.Equal(3, Enum.GetValues<AutoCameraMode>().Length);
    }
}
