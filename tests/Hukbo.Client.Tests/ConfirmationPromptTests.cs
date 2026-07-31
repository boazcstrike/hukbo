using Hukbo.Client.Presentation;
using Hukbo.Client.Settings;
using Hukbo.Client.Theming;
using Hukbo.Client.UI;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

[Collection(UiScaleContextCollection.Name)]
public sealed class ConfirmationPromptTests
{
    private static readonly Rectangle AvailableBounds = new(0, 0, 1280, 720);

    private static ConfirmationPrompt CreatePrompt() =>
        new("Quit Hukbo?", "Quit", ClientCommand.Exit);

    [Fact]
    public void CalculateLayout_AtOneHundredPercentPreservesBaselineGeometry()
    {
        AtScale(UiScale.Percent100, () =>
        {
            var layout = ConfirmationPrompt.CalculateLayout(AvailableBounds);

            Assert.Equal(
                new Rectangle(410, 265, 460, 190),
                layout.PanelBounds);
            Assert.Equal(
                new Rectangle(434, 299, 412, 60),
                layout.MessageBounds);
            Assert.Equal(
                new Rectangle(446, 387, 186, 44),
                layout.CancelBounds);
            Assert.Equal(
                new Rectangle(648, 387, 186, 44),
                layout.ConfirmBounds);
        });
    }

    [Theory]
    [InlineData(UiScale.Percent125)]
    [InlineData(UiScale.Percent150)]
    [InlineData(UiScale.Percent200)]
    public void CalculateLayout_ScalesAndContainsEveryPart(UiScale scale)
    {
        AtScale(scale, () =>
        {
            var available = new Rectangle(0, 0, 3840, 2160);
            var layout = ConfirmationPrompt.CalculateLayout(available);

            Assert.Equal(
                UiScaleContext.Pixels(ConfirmationPrompt.PanelWidth),
                layout.PanelBounds.Width);
            Assert.Equal(
                UiScaleContext.Pixels(ConfirmationPrompt.PanelHeight),
                layout.PanelBounds.Height);
            Assert.True(available.Contains(layout.PanelBounds));
            Assert.True(layout.PanelBounds.Contains(layout.MessageBounds));
            Assert.True(layout.PanelBounds.Contains(layout.CancelBounds));
            Assert.True(layout.PanelBounds.Contains(layout.ConfirmBounds));
        });
    }

    [Fact]
    public void CalculateLayout_KeepsEveryPartInsideThePanel()
    {
        var layout = ConfirmationPrompt.CalculateLayout(AvailableBounds);

        Assert.True(
            AvailableBounds.Contains(layout.PanelBounds),
            $"Panel {layout.PanelBounds} escaped {AvailableBounds}.");
        Assert.True(
            layout.PanelBounds.Contains(layout.MessageBounds),
            $"Message {layout.MessageBounds} escaped {layout.PanelBounds}.");
        Assert.True(
            layout.PanelBounds.Contains(layout.CancelBounds),
            $"Cancel {layout.CancelBounds} escaped {layout.PanelBounds}.");
        Assert.True(
            layout.PanelBounds.Contains(layout.ConfirmBounds),
            $"Confirm {layout.ConfirmBounds} escaped {layout.PanelBounds}.");
    }

    [Fact]
    public void CalculateLayout_DoesNotOverlapTheTwoButtons()
    {
        var layout = ConfirmationPrompt.CalculateLayout(AvailableBounds);

        Assert.False(
            layout.CancelBounds.Intersects(layout.ConfirmBounds),
            "Cancel and confirm overlap, so a click could hit both.");

        // Confirm sits to the right of cancel, furthest from where the cursor
        // rests after the click that opened the prompt.
        Assert.True(layout.ConfirmBounds.Left >= layout.CancelBounds.Right);
    }

    /// <summary>
    /// A viewport smaller than the panel's preferred size must clamp rather
    /// than overflow, the same way the match summary panel clamps.
    /// </summary>
    [Fact]
    public void CalculateLayout_ClampsToATinyViewport()
    {
        AtScale(UiScale.Percent200, () =>
        {
            var tiny = new Rectangle(0, 0, 200, 120);

            var layout = ConfirmationPrompt.CalculateLayout(tiny);

            Assert.True(tiny.Contains(layout.PanelBounds));
            Assert.True(layout.PanelBounds.Contains(layout.MessageBounds));
            Assert.True(layout.PanelBounds.Contains(layout.CancelBounds));
            Assert.True(layout.PanelBounds.Contains(layout.ConfirmBounds));
            Assert.False(
                layout.CancelBounds.Intersects(layout.ConfirmBounds));
        });
    }

    [Fact]
    public void Open_FocusesCancelSoAReflexiveEnterDoesNotQuit()
    {
        var prompt = CreatePrompt();

        prompt.Open();

        Assert.True(prompt.IsVisible);
        Assert.False(
            prompt.IsConfirmFocused,
            "Cancel must hold focus when the prompt opens: the action is " +
            "unrecoverable, so Enter must not complete it.");
    }

    [Fact]
    public void ClosedPromptConsumesNothing()
    {
        var prompt = CreatePrompt();

        var interaction = prompt.Update(new InputEdges(), AvailableBounds);

        Assert.Equal(ClientCommand.None, interaction.Command);
        Assert.False(interaction.PointerConsumed);
    }

    /// <summary>
    /// While open the prompt is modal, so it reports every frame's pointer as
    /// consumed even when the cursor is nowhere near the panel. A click that
    /// fell through to the battle underneath would be worse than no prompt,
    /// because the spectator would believe it had been swallowed.
    /// </summary>
    [Fact]
    public void OpenPromptConsumesThePointerEvenWhenTheCursorMissesIt()
    {
        var prompt = CreatePrompt();
        prompt.Open();

        var interaction = prompt.Update(new InputEdges(), AvailableBounds);

        Assert.True(interaction.PointerConsumed);
        Assert.Equal(ClientCommand.None, interaction.Command);
    }

    [Fact]
    public void GetCommandAt_DistinguishesConfirmFromCancelAndFromNeither()
    {
        var prompt = CreatePrompt();
        prompt.Open();
        prompt.Update(new InputEdges(), AvailableBounds);

        var layout = ConfirmationPrompt.CalculateLayout(AvailableBounds);

        Assert.Equal(
            ClientCommand.Exit,
            prompt.GetCommandAt(layout.ConfirmBounds.Center));
        Assert.Equal(
            ClientCommand.None,
            prompt.GetCommandAt(layout.CancelBounds.Center));
        Assert.Null(prompt.GetCommandAt(new Point(-50, -50)));
    }

    [Fact]
    public void Close_HidesThePromptWithoutIssuingACommand()
    {
        var prompt = CreatePrompt();
        prompt.Open();

        prompt.Close();

        Assert.False(prompt.IsVisible);
        Assert.False(prompt.IsConfirmFocused);
    }

    [Fact]
    public void EntranceMotion_PreservesFinalHitGeometryAndOffSnaps()
    {
        var prompt = CreatePrompt();
        prompt.Open();

        prompt.Update(
            new InputEdges(),
            AvailableBounds,
            TimeSpan.FromMilliseconds(40),
            MotionIntensity.Reduced);
        var layout = ConfirmationPrompt.CalculateLayout(AvailableBounds);

        Assert.InRange(prompt.ScrimOpacity, 0.001f, 0.999f);
        Assert.InRange(prompt.EntranceOpacity, 0.001f, 0.999f);
        Assert.Equal(
            ClientCommand.Exit,
            prompt.GetCommandAt(layout.ConfirmBounds.Center));

        prompt.Update(
            new InputEdges(),
            AvailableBounds,
            TimeSpan.Zero,
            MotionIntensity.Off);

        Assert.Equal(1f, prompt.ScrimOpacity);
        Assert.Equal(1f, prompt.EntranceOpacity);
        Assert.Equal(layout.PanelBounds, prompt.Bounds);
    }

    [Fact]
    public void Constructor_RejectsAConfirmCommandThatCouldNeverAct()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ConfirmationPrompt("Quit?", "Quit", ClientCommand.None));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsAnEmptyMessage(string message)
    {
        Assert.Throws<ArgumentException>(
            () => new ConfirmationPrompt(message, "Quit", ClientCommand.Exit));
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
