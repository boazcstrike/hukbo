namespace Hukbo.Client.Settings;

internal readonly record struct StartupGraphicsConfiguration(
    int BackBufferWidth,
    int BackBufferHeight,
    bool IsFullScreen,
    bool HardwareModeSwitch);

/// <summary>
/// Pure startup policy kept outside the hardware-backed game class so both
/// normal and render-probe behavior are pinned by tests.
/// </summary>
internal static class StartupDisplayPolicy
{
    public static StartupGraphicsConfiguration Resolve(
        StartupDisplayMode preferredMode,
        int windowWidth,
        int windowHeight,
        int displayWidth,
        int displayHeight,
        bool renderProbeEnabled,
        int renderProbeWidth,
        int renderProbeHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowHeight);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(displayWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(displayHeight);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(renderProbeWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(renderProbeHeight);

        if (renderProbeEnabled)
        {
            return new StartupGraphicsConfiguration(
                renderProbeWidth,
                renderProbeHeight,
                IsFullScreen: false,
                HardwareModeSwitch: false);
        }

        var isFullScreen = preferredMode == StartupDisplayMode.Fullscreen;
        return new StartupGraphicsConfiguration(
            isFullScreen ? displayWidth : Math.Min(windowWidth, displayWidth),
            isFullScreen ? displayHeight : Math.Min(windowHeight, displayHeight),
            isFullScreen,
            HardwareModeSwitch: false);
    }
}
