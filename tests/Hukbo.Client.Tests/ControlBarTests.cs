using Hukbo.Client;
using Hukbo.Client.Presentation;
using Hukbo.Client.UI;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

public sealed class ControlBarTests
{
    // Wide enough that ControlBar's own preferred width, rather than the
    // available-bounds clamp, decides the bar's actual width.
    private static readonly Rectangle AvailableBounds = new(0, 0, 2000, 200);

    [Fact]
    public void Update_LaysOutSevenButtonsEntirelyInsideTheBar()
    {
        var controlBar = new ControlBar();

        controlBar.Update(
            new InputEdges(),
            AvailableBounds,
            isPlaying: false,
            isSoundLogVisible: false);

        Assert.Equal(7, controlBar.ButtonBounds.Count);
        foreach (var buttonBounds in controlBar.ButtonBounds)
        {
            Assert.True(
                controlBar.Bounds.Contains(buttonBounds),
                $"{buttonBounds} escaped bar bounds {controlBar.Bounds}.");
        }
    }

    /// <summary>
    /// The rightmost button is the one a too-narrow bar clips, and it is the
    /// quit control, so it gets its own assertion rather than relying on the
    /// containment loop above.
    /// </summary>
    /// <remarks>
    /// This is the shape of test that would have caught the 544-wide bar once
    /// proposed for six buttons: that arithmetic omitted the bar's own
    /// ten-pixel left padding, so the last button ended past the right edge.
    /// </remarks>
    [Fact]
    public void Update_LeavesTheRightmostButtonFullyInsideTheBar()
    {
        var controlBar = new ControlBar();

        controlBar.Update(
            new InputEdges(),
            AvailableBounds,
            isPlaying: false,
            isSoundLogVisible: false);

        var rightmost = controlBar.ButtonBounds[^1];

        Assert.True(
            rightmost.Right <= controlBar.Bounds.Right,
            $"Rightmost button {rightmost} was clipped by bar " +
            $"{controlBar.Bounds}.");
        Assert.Equal(
            ClientCommand.RequestExit,
            controlBar.GetCommandAt(rightmost.Center));
    }

    [Fact]
    public void Update_WindowChromeButtonsAreReachableAtTheEndOfTheRow()
    {
        var controlBar = new ControlBar();

        controlBar.Update(
            new InputEdges(),
            AvailableBounds,
            isPlaying: false,
            isSoundLogVisible: false);

        Assert.Equal(
            ClientCommand.Minimize,
            controlBar.GetCommandAt(controlBar.ButtonBounds[4].Center));
        Assert.Equal(
            ClientCommand.ToggleMaximize,
            controlBar.GetCommandAt(controlBar.ButtonBounds[5].Center));

        // RequestExit, not Exit: the bar asks, and only the confirmation
        // prompt's confirm button acts.
        Assert.Equal(
            ClientCommand.RequestExit,
            controlBar.GetCommandAt(controlBar.ButtonBounds[6].Center));
    }

    /// <summary>
    /// No control-bar button may issue <see cref="ClientCommand.Exit"/>
    /// directly. If one ever does, quitting is a single unconfirmed click again
    /// and the confirmation prompt has been bypassed without anything failing
    /// to compile.
    /// </summary>
    [Fact]
    public void Update_NoButtonQuitsWithoutConfirmation()
    {
        var controlBar = new ControlBar();

        controlBar.Update(
            new InputEdges(),
            AvailableBounds,
            isPlaying: false,
            isSoundLogVisible: false);

        foreach (var buttonBounds in controlBar.ButtonBounds)
        {
            Assert.NotEqual(
                ClientCommand.Exit,
                controlBar.GetCommandAt(buttonBounds.Center));
        }
    }
}
