using Hukbo.Client.Settings;

namespace Hukbo.Client.Tests;

public sealed class StartupDisplayPolicyTests
{
    [Fact]
    public void WindowedUsesTheConfiguredClientSize()
    {
        var resolved = Resolve(StartupDisplayMode.Windowed);

        Assert.Equal(1280, resolved.BackBufferWidth);
        Assert.Equal(720, resolved.BackBufferHeight);
        Assert.False(resolved.IsFullScreen);
        Assert.False(resolved.HardwareModeSwitch);
    }

    [Fact]
    public void FullscreenUsesTheDesktopModeWithoutAHardwareSwitch()
    {
        var resolved = Resolve(StartupDisplayMode.Fullscreen);

        Assert.Equal(2560, resolved.BackBufferWidth);
        Assert.Equal(1440, resolved.BackBufferHeight);
        Assert.True(resolved.IsFullScreen);
        Assert.False(resolved.HardwareModeSwitch);
    }

    [Fact]
    public void RenderProbeAlwaysUsesItsPinnedWindow()
    {
        var resolved = StartupDisplayPolicy.Resolve(
            StartupDisplayMode.Fullscreen,
            1280,
            720,
            2560,
            1440,
            renderProbeEnabled: true,
            renderProbeWidth: 1920,
            renderProbeHeight: 1080);

        Assert.Equal(1920, resolved.BackBufferWidth);
        Assert.Equal(1080, resolved.BackBufferHeight);
        Assert.False(resolved.IsFullScreen);
    }

    [Fact]
    public void WindowedClampsToASmallerDisplay()
    {
        var resolved = StartupDisplayPolicy.Resolve(
            StartupDisplayMode.Windowed,
            1600,
            900,
            1366,
            768,
            renderProbeEnabled: false,
            renderProbeWidth: 1920,
            renderProbeHeight: 1080);

        Assert.Equal(1366, resolved.BackBufferWidth);
        Assert.Equal(768, resolved.BackBufferHeight);
        Assert.False(resolved.IsFullScreen);
        Assert.False(resolved.HardwareModeSwitch);
    }

    [Fact]
    public void WindowedLeavesTheWindowUntouchedOnALargerDisplay()
    {
        var resolved = StartupDisplayPolicy.Resolve(
            StartupDisplayMode.Windowed,
            1600,
            900,
            2560,
            1440,
            renderProbeEnabled: false,
            renderProbeWidth: 1920,
            renderProbeHeight: 1080);

        Assert.Equal(1600, resolved.BackBufferWidth);
        Assert.Equal(900, resolved.BackBufferHeight);
        Assert.False(resolved.IsFullScreen);
        Assert.False(resolved.HardwareModeSwitch);
    }

    private static StartupGraphicsConfiguration Resolve(
        StartupDisplayMode mode) =>
        StartupDisplayPolicy.Resolve(
            mode,
            1280,
            720,
            2560,
            1440,
            renderProbeEnabled: false,
            renderProbeWidth: 1920,
            renderProbeHeight: 1080);
}
