using Hukbo.Client.Rendering;

namespace Hukbo.Client.Tests;

/// <summary>
/// Pins <see cref="PawnVisualStateResolver.Resolve"/>'s precedence over the
/// full alive/dead x hold-active/hold-expired x selected/hovered/neither grid
/// (corpse layer, the corpse placeholder design), which
/// is the precedence this test pins.
/// </summary>
public sealed class PawnVisualStateResolverTests
{
    private const ulong AgentId = 7UL;
    private const ulong OtherAgentId = 9UL;

    // --- Dead, lethal hold active: always Normal, regardless of selection/hover ---

    [Fact]
    public void Resolve_DeadWithActiveHold_NotSelectedNotHovered_IsNormal()
    {
        var state = PawnVisualStateResolver.Resolve(
            AgentId,
            selectedEntityId: null,
            hoveredEntityId: null,
            isAlive: false,
            isLethalHoldActive: true);

        Assert.Equal(PawnVisualState.Normal, state);
    }

    [Fact]
    public void Resolve_DeadWithActiveHold_MatchingSelectedId_IsStillNormal()
    {
        var state = PawnVisualStateResolver.Resolve(
            AgentId,
            selectedEntityId: AgentId,
            hoveredEntityId: null,
            isAlive: false,
            isLethalHoldActive: true);

        Assert.Equal(PawnVisualState.Normal, state);
    }

    [Fact]
    public void Resolve_DeadWithActiveHold_MatchingHoveredId_IsStillNormal()
    {
        var state = PawnVisualStateResolver.Resolve(
            AgentId,
            selectedEntityId: null,
            hoveredEntityId: AgentId,
            isAlive: false,
            isLethalHoldActive: true);

        Assert.Equal(PawnVisualState.Normal, state);
    }

    [Fact]
    public void Resolve_DeadWithActiveHold_MatchingBothSelectedAndHoveredId_IsStillNormal()
    {
        var state = PawnVisualStateResolver.Resolve(
            AgentId,
            selectedEntityId: AgentId,
            hoveredEntityId: AgentId,
            isAlive: false,
            isLethalHoldActive: true);

        Assert.Equal(PawnVisualState.Normal, state);
    }

    // --- Dead, lethal hold expired: always Dead, regardless of selection/hover ---

    [Fact]
    public void Resolve_DeadWithExpiredHold_NotSelectedNotHovered_IsDead()
    {
        var state = PawnVisualStateResolver.Resolve(
            AgentId,
            selectedEntityId: null,
            hoveredEntityId: null,
            isAlive: false,
            isLethalHoldActive: false);

        Assert.Equal(PawnVisualState.Dead, state);
    }

    [Fact]
    public void Resolve_DeadWithExpiredHold_MatchingSelectedId_IsStillDead()
    {
        var state = PawnVisualStateResolver.Resolve(
            AgentId,
            selectedEntityId: AgentId,
            hoveredEntityId: null,
            isAlive: false,
            isLethalHoldActive: false);

        Assert.Equal(PawnVisualState.Dead, state);
    }

    [Fact]
    public void Resolve_DeadWithExpiredHold_MatchingHoveredId_IsStillDead()
    {
        var state = PawnVisualStateResolver.Resolve(
            AgentId,
            selectedEntityId: null,
            hoveredEntityId: AgentId,
            isAlive: false,
            isLethalHoldActive: false);

        Assert.Equal(PawnVisualState.Dead, state);
    }

    [Fact]
    public void Resolve_DeadWithExpiredHold_MatchingBothSelectedAndHoveredId_IsStillDead()
    {
        var state = PawnVisualStateResolver.Resolve(
            AgentId,
            selectedEntityId: AgentId,
            hoveredEntityId: AgentId,
            isAlive: false,
            isLethalHoldActive: false);

        Assert.Equal(PawnVisualState.Dead, state);
    }

    // --- Alive: selection and hover, in precedence order ---

    [Fact]
    public void Resolve_Alive_NotSelectedNotHovered_IsNormal()
    {
        var state = PawnVisualStateResolver.Resolve(
            AgentId,
            selectedEntityId: null,
            hoveredEntityId: null,
            isAlive: true,
            isLethalHoldActive: false);

        Assert.Equal(PawnVisualState.Normal, state);
    }

    [Fact]
    public void Resolve_Alive_MatchingSelectedIdOnly_IsSelected()
    {
        var state = PawnVisualStateResolver.Resolve(
            AgentId,
            selectedEntityId: AgentId,
            hoveredEntityId: null,
            isAlive: true,
            isLethalHoldActive: false);

        Assert.Equal(PawnVisualState.Selected, state);
    }

    [Fact]
    public void Resolve_Alive_MatchingHoveredIdOnly_IsHovered()
    {
        var state = PawnVisualStateResolver.Resolve(
            AgentId,
            selectedEntityId: null,
            hoveredEntityId: AgentId,
            isAlive: true,
            isLethalHoldActive: false);

        Assert.Equal(PawnVisualState.Hovered, state);
    }

    [Fact]
    public void Resolve_Alive_MatchingBothSelectedAndHoveredId_SelectionWins()
    {
        var state = PawnVisualStateResolver.Resolve(
            AgentId,
            selectedEntityId: AgentId,
            hoveredEntityId: AgentId,
            isAlive: true,
            isLethalHoldActive: false);

        Assert.Equal(PawnVisualState.Selected, state);
    }

    [Fact]
    public void Resolve_Alive_SelectedIsSomeoneElse_HoveredIsThisAgent_IsHovered()
    {
        var state = PawnVisualStateResolver.Resolve(
            AgentId,
            selectedEntityId: OtherAgentId,
            hoveredEntityId: AgentId,
            isAlive: true,
            isLethalHoldActive: false);

        Assert.Equal(PawnVisualState.Hovered, state);
    }

    [Fact]
    public void Resolve_Alive_SelectedAndHoveredAreBothSomeoneElse_IsNormal()
    {
        var state = PawnVisualStateResolver.Resolve(
            AgentId,
            selectedEntityId: OtherAgentId,
            hoveredEntityId: OtherAgentId,
            isAlive: true,
            isLethalHoldActive: false);

        Assert.Equal(PawnVisualState.Normal, state);
    }

    // --- isLethalHoldActive is documented as ignored when isAlive is true ---

    [Fact]
    public void Resolve_Alive_WithLethalHoldActiveTrue_StillHonorsSelection()
    {
        // isLethalHoldActive is meaningful only when isAlive is false; an
        // alive agent must resolve purely on selection/hover regardless of
        // what this argument carries.
        var state = PawnVisualStateResolver.Resolve(
            AgentId,
            selectedEntityId: AgentId,
            hoveredEntityId: null,
            isAlive: true,
            isLethalHoldActive: true);

        Assert.Equal(PawnVisualState.Selected, state);
    }
}
