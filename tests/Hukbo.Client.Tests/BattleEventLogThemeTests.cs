using Hukbo.Client.Theming;
using Hukbo.Client.UI;

namespace Hukbo.Client.Tests;

public sealed class BattleEventLogThemeTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void SelectedOrHoveredRowsUseValidatedInverseText(
        bool isSelected,
        bool isHovered)
    {
        var theme = LoadCatalog().GetRequired("command");

        var colors = BattleEventLogPanel.GetRowForegrounds(
            theme,
            isSelected,
            isHovered,
            factionId: 0);

        Assert.Equal(theme.Colors.TextInverse, colors.Tick);
        Assert.Equal(theme.Colors.TextInverse, colors.Actor);
        Assert.Equal(theme.Colors.TextInverse, colors.Action);
    }

    [Fact]
    public void NormalRowsRetainSemanticForegrounds()
    {
        var theme = LoadCatalog().GetRequired("command");

        var colors = BattleEventLogPanel.GetRowForegrounds(
            theme,
            isSelected: false,
            isHovered: false,
            factionId: 1);

        Assert.Equal(theme.Colors.TextDisabled, colors.Tick);
        Assert.Equal(theme.Colors.TeamB, colors.Actor);
        Assert.Equal(theme.Colors.TextSecondary, colors.Action);
    }

    private static UiThemeCatalog LoadCatalog()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Content",
            "Themes",
            "ui-theme-standards.json");
        return UiThemeCatalog.Load(path);
    }
}
