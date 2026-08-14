using Hukbo.Client;
using Hukbo.Client.Presentation;
using Hukbo.Client.Settings;
using Hukbo.Client.Theming;
using Hukbo.Client.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Hukbo.Client.Tests;

[CollectionDefinition("UI scale context", DisableParallelization = true)]
public sealed class UiScaleContextCollection
{
    public const string Name = "UI scale context";
}

[Collection(UiScaleContextCollection.Name)]
public sealed class ControlBarTests
{
    // Wide enough that ControlBar's own preferred width, rather than the
    // available-bounds clamp, decides the bar's actual width.
    private static readonly Rectangle AvailableBounds = new(0, 0, 2000, 200);

    [Fact]
    public void Update_AtOneHundredPercentPreservesBaselineGeometry()
    {
        AtScale(UiScale.Percent100, () =>
        {
            var controlBar = new ControlBar();

            controlBar.Update(
                new InputEdges(),
                AvailableBounds,
                isPlaying: false,
                isSoundLogVisible: false,
                isEventLogVisible: false);

            Assert.Equal(new Rectangle(1330, 10, 660, 48), controlBar.Bounds);
            Assert.Equal(
                new Rectangle(1340, 17, 72, 34),
                controlBar.ButtonBounds[0]);
            Assert.Equal(
                new Rectangle(1900, 17, 72, 34),
                controlBar.ButtonBounds[^1]);
        });
    }

    [Theory]
    [InlineData(UiScale.Percent125)]
    [InlineData(UiScale.Percent150)]
    [InlineData(UiScale.Percent200)]
    public void Update_ScaledButtonsRemainInsideAvailableBounds(UiScale scale)
    {
        AtScale(scale, () =>
        {
            var available = new Rectangle(0, 0, 1280, 720);
            var controlBar = new ControlBar();

            controlBar.Update(
                new InputEdges(),
                available,
                isPlaying: false,
                isSoundLogVisible: false,
                isEventLogVisible: false);

            Assert.True(available.Contains(controlBar.Bounds));
            Assert.All(
                controlBar.ButtonBounds,
                bounds =>
                {
                    Assert.True(controlBar.Bounds.Contains(bounds));
                    Assert.True(bounds.Width > 0);
                    Assert.True(bounds.Height >= 44);
                });
        });
    }

    [Fact]
    public void Update_LaysOutEightButtonsEntirelyInsideTheBar()
    {
        var controlBar = new ControlBar();

        controlBar.Update(
            new InputEdges(),
            AvailableBounds,
            isPlaying: false,
            isSoundLogVisible: false,
            isEventLogVisible: false);

        Assert.Equal(8, controlBar.ButtonBounds.Count);
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
            isSoundLogVisible: false,
            isEventLogVisible: false);

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
            isSoundLogVisible: false,
            isEventLogVisible: false);

        Assert.Equal(
            ClientCommand.Minimize,
            controlBar.GetCommandAt(controlBar.ButtonBounds[5].Center));
        Assert.Equal(
            ClientCommand.ToggleMaximize,
            controlBar.GetCommandAt(controlBar.ButtonBounds[6].Center));

        // RequestExit, not Exit: the bar asks, and only the confirmation
        // prompt's confirm button acts.
        Assert.Equal(
            ClientCommand.RequestExit,
            controlBar.GetCommandAt(controlBar.ButtonBounds[7].Center));
    }

    /// <summary>
    /// The "Events" button reports active only while the event log is
    /// actually visible, mirroring how the "Sounds" button already tracks
    /// <c>isSoundLogVisible</c>.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Update_EventsButtonIsActiveOnlyWhenEventLogVisible(
        bool isEventLogVisible)
    {
        var controlBar = new ControlBar();

        controlBar.Update(
            new InputEdges(),
            AvailableBounds,
            isPlaying: false,
            isSoundLogVisible: false,
            isEventLogVisible: isEventLogVisible);

        var eventsButtonIndex = Array.FindIndex(
            controlBar.ButtonBounds.ToArray(),
            bounds => controlBar.GetCommandAt(bounds.Center)
                == ClientCommand.ToggleEventLog);

        Assert.Equal(
            isEventLogVisible,
            controlBar.ButtonActiveStates[eventsButtonIndex]);
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
            isSoundLogVisible: false,
            isEventLogVisible: false);

        foreach (var buttonBounds in controlBar.ButtonBounds)
        {
            Assert.NotEqual(
                ClientCommand.Exit,
                controlBar.GetCommandAt(buttonBounds.Center));
        }
    }

    [Fact]
    public void TimedUpdate_AdvancesButtonMotionWithoutMovingHitTargets()
    {
        var controlBar = new ControlBar();
        controlBar.Update(
            new InputEdges(),
            AvailableBounds,
            isPlaying: false,
            isSoundLogVisible: false,
            isEventLogVisible: false);
        var originalBounds = controlBar.ButtonBounds.ToArray();
        var pointer = originalBounds[0].Center;
        var released = new MouseState(
            pointer.X,
            pointer.Y,
            0,
            ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released);
        var input = new InputEdges(
            new KeyboardState(),
            new KeyboardState(),
            released,
            released);

        controlBar.Update(
            input,
            AvailableBounds,
            isPlaying: false,
            isSoundLogVisible: false,
            isEventLogVisible: false,
            TimeSpan.FromMilliseconds(30),
            MotionIntensity.Reduced);

        Assert.InRange(controlBar.ButtonHoverAmounts[0], 0.01f, 0.99f);
        Assert.Equal(originalBounds, controlBar.ButtonBounds);
    }

    private static void AtScale(UiScale scale, Action action)
    {
        var previous = UiScaleContext.ActiveScale;
        try
        {
            UiScaleContext.Set(scale);
            action();
        }
        finally
        {
            UiScaleContext.Set(previous);
        }
    }
}
