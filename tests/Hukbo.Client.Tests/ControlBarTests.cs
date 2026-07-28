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
    public void Update_LaysOutSixButtonsEntirelyInsideTheBar()
    {
        var controlBar = new ControlBar();

        controlBar.Update(
            new InputEdges(),
            AvailableBounds,
            isPlaying: false,
            isSoundLogVisible: false);

        Assert.Equal(6, controlBar.ButtonBounds.Count);
        foreach (var buttonBounds in controlBar.ButtonBounds)
        {
            Assert.True(
                controlBar.Bounds.Contains(buttonBounds),
                $"{buttonBounds} escaped bar bounds {controlBar.Bounds}.");
        }
    }

    [Fact]
    public void Update_MinimizeAndCloseAreReachableAtTheEndOfTheRow()
    {
        var controlBar = new ControlBar();

        controlBar.Update(
            new InputEdges(),
            AvailableBounds,
            isPlaying: false,
            isSoundLogVisible: false);

        var minimizeBounds = controlBar.ButtonBounds[4];
        var closeBounds = controlBar.ButtonBounds[5];

        Assert.Equal(
            ClientCommand.Minimize,
            controlBar.GetCommandAt(minimizeBounds.Center));
        Assert.Equal(
            ClientCommand.Exit,
            controlBar.GetCommandAt(closeBounds.Center));
    }
}
