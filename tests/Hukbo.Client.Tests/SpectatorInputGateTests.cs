using Hukbo.Client;
using Hukbo.Client.UI;

namespace Hukbo.Client.Tests;

public sealed class SpectatorInputGateTests
{
    [Fact]
    public void AllowsEverySpectatorBindingWhenTheEventPanelHasNoFocus()
    {
        var gate = SpectatorInputGate.Resolve(
            BattleEventKeyboardFocusTarget.None);

        Assert.True(gate.AllowSpectatorCommands);
        Assert.True(gate.AllowSpeedShortcuts);
        Assert.Equal(SpectatorPanInput.All, gate.PanInput);
    }

    [Fact]
    public void SuppressesEveryTypedBindingWhileTheSearchBoxHasFocus()
    {
        var gate = SpectatorInputGate.Resolve(
            BattleEventKeyboardFocusTarget.Search);

        Assert.False(gate.AllowSpectatorCommands);
        Assert.False(gate.AllowSpeedShortcuts);
        Assert.Equal(SpectatorPanInput.None, gate.PanInput);
    }

    [Fact]
    public void KeepsPlaybackAndSpeedReachableWhileTheEventListHasFocus()
    {
        var gate = SpectatorInputGate.Resolve(
            BattleEventKeyboardFocusTarget.List);

        Assert.True(gate.AllowSpectatorCommands);
        Assert.True(gate.AllowSpeedShortcuts);
    }

    [Fact]
    public void YieldsOnlyTheArrowKeysWhileTheEventListHasFocus()
    {
        var gate = SpectatorInputGate.Resolve(
            BattleEventKeyboardFocusTarget.List);

        Assert.Equal(SpectatorPanInput.Letters, gate.PanInput);
        Assert.True(gate.PanInput.HasFlag(SpectatorPanInput.Letters));
        Assert.False(gate.PanInput.HasFlag(SpectatorPanInput.Arrows));
    }
}
