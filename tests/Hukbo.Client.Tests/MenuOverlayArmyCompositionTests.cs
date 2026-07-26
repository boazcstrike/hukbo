using Hukbo.Client.Presentation;

namespace Hukbo.Client.Tests;

public sealed class MenuOverlayArmyCompositionTests
{
    [Fact]
    public void TheArmyCompositionButtonReturnsItsOwnCommand()
    {
        var definition = MenuOverlay.ButtonDefinitions[3];

        Assert.Equal("Army Composition", definition.Label);
        Assert.Equal(ClientCommand.OpenArmyComposition, definition.Command);
    }

    [Fact]
    public void TheExistingButtonOrderIsPreservedAroundTheInsertion()
    {
        Assert.Equal(
            new[]
            {
                "Play",
                "Pause",
                "Next Round",
                "Army Composition",
                "Full Reset",
                "Exit Game",
            },
            MenuOverlay.ButtonDefinitions
                .Select(definition => definition.Label)
                .ToArray());
    }
}
