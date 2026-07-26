using Hukbo.Client.Presentation;
using Hukbo.Client.UI;
using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Tests;

public sealed class AgentInspectorContentTests
{
    private static readonly AgentView SampleAgent = new(
        EntityId: 7,
        FactionId: 0,
        XRaw: 0,
        YRaw: 0,
        HitPoints: 10,
        MaximumHitPoints: 10,
        TargetEntityId: null,
        Intent: AgentIntent.Idle,
        IsAlive: true,
        Loadout: new CombatLoadout(
            WeaponId.GreatBlade,
            ArmorId.LightOrganic,
            ShieldId.TallHardwood));

    [Fact]
    public void FormatWeaponLine_UsesLabelDerivedFromLoadoutWeapon()
    {
        var appearance = PawnAppearanceFactory.Create(
            SampleAgent.EntityId,
            SampleAgent.Loadout.Weapon);

        var line = AgentInspectorContent.FormatWeaponLine(appearance.WeaponLabel);

        Assert.Equal("Weapon: Great Blade", line);
    }

    [Fact]
    public void FormatArmorLine_RendersLightOrganicLabel()
    {
        var line = AgentInspectorContent.FormatArmorLine(
            SampleAgent.Loadout.Armor);

        Assert.Equal("Armor: Light Organic", line);
    }

    [Theory]
    [InlineData(ShieldId.None, "Shield: None")]
    [InlineData(ShieldId.TallHardwood, "Shield: Tall Hardwood")]
    public void FormatShieldLine_RendersShieldLabel(
        ShieldId shield,
        string expected)
    {
        var line = AgentInspectorContent.FormatShieldLine(shield);

        Assert.Equal(expected, line);
    }

    [Fact]
    public void WrapText_NullEvidenceNote_YieldsNoLines()
    {
        var lines = AgentInspectorContent.WrapText(
            text: null,
            maxWidthPx: 277f,
            measureWidth: FixedWidthMeasure(6f));

        Assert.Empty(lines);
    }

    [Fact]
    public void WrapText_EmptyEvidenceNote_YieldsNoLines()
    {
        var lines = AgentInspectorContent.WrapText(
            text: string.Empty,
            maxWidthPx: 277f,
            measureWidth: FixedWidthMeasure(6f));

        Assert.Empty(lines);
    }

    [Fact]
    public void WrapText_LongEvidenceNote_WrapsAcrossMultipleLines()
    {
        const string evidenceNote =
            "PROVISIONAL: comparable to Spanish-era accounts of the " +
            "kampilan.";

        var lines = AgentInspectorContent.WrapText(
            evidenceNote,
            maxWidthPx: 277f,
            measureWidth: FixedWidthMeasure(6f));

        Assert.True(lines.Count > 1);
        Assert.Equal(
            evidenceNote,
            string.Join(' ', lines));
    }

    [Fact]
    public void WrapText_NoReturnedLineExceedsWidthBudget()
    {
        const string evidenceNote =
            "PROVISIONAL: comparable to Spanish-era accounts of the " +
            "kampilan.";
        const float maxWidthPx = 277f;
        var measure = FixedWidthMeasure(6f);

        var lines = AgentInspectorContent.WrapText(
            evidenceNote,
            maxWidthPx,
            measure);

        Assert.All(
            lines,
            line => Assert.True(measure(line) <= maxWidthPx));
    }

    [Fact]
    public void WrapText_OversizedSingleWord_HardSplitsWithinBudget()
    {
        var oversizedWord = new string('x', 80);
        const float maxWidthPx = 60f;
        var measure = FixedWidthMeasure(6f);

        var lines = AgentInspectorContent.WrapText(
            oversizedWord,
            maxWidthPx,
            measure);

        Assert.True(lines.Count > 1);
        Assert.All(
            lines,
            line => Assert.True(measure(line) <= maxWidthPx));
    }

    [Fact]
    public void WrapText_WrappedContentFitsWithinReservedPanelHeight()
    {
        const string evidenceNote =
            "PROVISIONAL: comparable to Spanish-era accounts of the " +
            "kampilan.";

        var lines = AgentInspectorContent.WrapText(
            evidenceNote,
            maxWidthPx: 277f,
            measureWidth: FixedWidthMeasure(6f));

        var requiredHeight = AgentInspectorContent.ComputeRequiredHeight(
            lines.Count);
        var reservedHeight = AgentInspectorContent.ComputeRequiredHeight(
            AgentInspectorContent.EvidenceReservedLineCount);

        Assert.True(lines.Count <= AgentInspectorContent.EvidenceReservedLineCount);
        Assert.True(requiredHeight <= reservedHeight);
    }

    [Fact]
    public void ComputeRequiredHeight_GrowsWithEvidenceLineCount()
    {
        var zeroLines = AgentInspectorContent.ComputeRequiredHeight(0);
        var twoLines = AgentInspectorContent.ComputeRequiredHeight(2);

        Assert.True(twoLines > zeroLines);
        Assert.Equal(
            2 * AgentInspectorContent.LineHeight,
            twoLines - zeroLines);
    }

    [Fact]
    public void ComputeContentWidthBudget_SubtractsPaddingAndAccent()
    {
        var budget = AgentInspectorContent.ComputeContentWidthBudget(310);

        Assert.Equal(
            310
                - (AgentInspectorContent.Padding * 2)
                - AgentInspectorContent.AccentWidth,
            budget);
    }

    [Fact]
    public void ComputeContentWidthBudget_NeverGoesNegative()
    {
        var budget = AgentInspectorContent.ComputeContentWidthBudget(0);

        Assert.Equal(0, budget);
    }

    private static Func<string, float> FixedWidthMeasure(
        float pixelsPerCharacter) =>
        text => text.Length * pixelsPerCharacter;
}
