using Hukbo.Client.Settings;
using Hukbo.Client.UI;
using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

public sealed class AgentInspectorAccentMotionTests
{
    private static readonly Color FactionColor = new(10, 20, 30);
    private static readonly Color SelectionColor = new(200, 210, 220);
    private static readonly Rectangle Bounds = new(0, 0, 200, 200);

    [Fact]
    public void GetAccentColor_ReturnsFactionColorAtZero()
    {
        var color = AgentInspectorPanel.GetAccentColor(
            FactionColor,
            SelectionColor,
            pulseAmount: 0f);

        Assert.Equal(FactionColor, color);
    }

    [Fact]
    public void GetAccentColor_ReturnsSelectionColorAtOne()
    {
        var color = AgentInspectorPanel.GetAccentColor(
            FactionColor,
            SelectionColor,
            pulseAmount: 1f);

        Assert.Equal(SelectionColor, color);
    }

    [Fact]
    public void Update_SelectingDifferentAgentTriggersExactlyOnePulse()
    {
        var panel = new AgentInspectorPanel();
        var input = new InputEdges();

        panel.Update(
            input,
            CreateAgent(1),
            Bounds,
            TimeSpan.Zero,
            MotionIntensity.Full);

        Assert.Equal(1f, panel.AccentPulseAmount);

        panel.Update(
            input,
            CreateAgent(2),
            Bounds,
            TimeSpan.Zero,
            MotionIntensity.Full);

        Assert.Equal(1f, panel.AccentPulseAmount);
    }

    [Fact]
    public void Update_ReobservingSameAgentTriggersNoPulse()
    {
        var panel = new AgentInspectorPanel();
        var input = new InputEdges();

        panel.Update(
            input,
            CreateAgent(1),
            Bounds,
            TimeSpan.Zero,
            MotionIntensity.Full);
        panel.Update(
            input,
            CreateAgent(1),
            Bounds,
            TimeSpan.FromMilliseconds(160),
            MotionIntensity.Full);

        Assert.Equal(0f, panel.AccentPulseAmount);

        panel.Update(
            input,
            CreateAgent(1),
            Bounds,
            TimeSpan.Zero,
            MotionIntensity.Full);

        Assert.Equal(0f, panel.AccentPulseAmount);
    }

    [Fact]
    public void Update_DeselectionReturnsAmountToExactlyZero()
    {
        var panel = new AgentInspectorPanel();
        var input = new InputEdges();

        panel.Update(
            input,
            CreateAgent(1),
            Bounds,
            TimeSpan.Zero,
            MotionIntensity.Full);
        Assert.Equal(1f, panel.AccentPulseAmount);

        panel.Update(
            input,
            agent: null,
            Bounds,
            TimeSpan.Zero,
            MotionIntensity.Full);

        Assert.Equal(0f, panel.AccentPulseAmount);
    }

    [Fact]
    public void Update_MotionOffKeepsAmountAlwaysZero()
    {
        var panel = new AgentInspectorPanel();
        var input = new InputEdges();

        panel.Update(
            input,
            CreateAgent(1),
            Bounds,
            TimeSpan.Zero,
            MotionIntensity.Off);
        Assert.Equal(0f, panel.AccentPulseAmount);

        panel.Update(
            input,
            CreateAgent(2),
            Bounds,
            TimeSpan.Zero,
            MotionIntensity.Off);

        Assert.Equal(0f, panel.AccentPulseAmount);
    }

    private static AgentView CreateAgent(ulong entityId) =>
        new(
            entityId,
            FactionId: 0,
            XRaw: 0,
            YRaw: 0,
            HitPoints: 100,
            MaximumHitPoints: 100,
            TargetEntityId: null,
            Intent: AgentIntent.Idle,
            IsAlive: true,
            Loadout: new CombatLoadout(
                WeaponId.Kampilan,
                ArmorId.LightOrganic,
                ShieldId.TallHardwood));
}
