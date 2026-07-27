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

    // Rajdhani SemiBold's real per-character advance at the Body rung's 14px
    // bake is narrower than the Default.spritefont Arial-18-at-0.64 legacy
    // estimate this suite used to hardcode. Testing across 5, 6, 7, and 8
    // pixels per character brackets the plausible real value instead of
    // pinning to one now-obsolete measurement.
    [Theory]
    [InlineData(5f)]
    [InlineData(6f)]
    [InlineData(7f)]
    [InlineData(8f)]
    public void WrapText_NoReturnedLineExceedsWidthBudget(
        float pixelsPerCharacter)
    {
        const string evidenceNote =
            "PROVISIONAL: comparable to Spanish-era accounts of the " +
            "kampilan.";
        const float maxWidthPx = 277f;
        var measure = FixedWidthMeasure(pixelsPerCharacter);

        var lines = AgentInspectorContent.WrapText(
            evidenceNote,
            maxWidthPx,
            measure);

        Assert.All(
            lines,
            line => Assert.True(measure(line) <= maxWidthPx));
    }

    [Theory]
    [InlineData(5f)]
    [InlineData(6f)]
    [InlineData(7f)]
    [InlineData(8f)]
    public void WrapText_OversizedSingleWord_HardSplitsWithinBudget(
        float pixelsPerCharacter)
    {
        var oversizedWord = new string('x', 80);
        const float maxWidthPx = 60f;
        var measure = FixedWidthMeasure(pixelsPerCharacter);

        var lines = AgentInspectorContent.WrapText(
            oversizedWord,
            maxWidthPx,
            measure);

        Assert.True(lines.Count > 1);
        Assert.All(
            lines,
            line => Assert.True(measure(line) <= maxWidthPx));
    }

    [Theory]
    [InlineData(5f)]
    [InlineData(6f)]
    [InlineData(7f)]
    [InlineData(8f)]
    public void WrapText_WrappedContentFitsWithinReservedPanelHeight(
        float pixelsPerCharacter)
    {
        const string evidenceNote =
            "PROVISIONAL: comparable to Spanish-era accounts of the " +
            "kampilan.";

        var lines = AgentInspectorContent.WrapText(
            evidenceNote,
            maxWidthPx: 277f,
            measureWidth: FixedWidthMeasure(pixelsPerCharacter));

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

    [Theory]
    [InlineData(MovementResolution.None, "Movement: Holding")]
    [InlineData(MovementResolution.Moved, "Movement: Moving")]
    [InlineData(MovementResolution.Truncated, "Movement: Crowded")]
    [InlineData(MovementResolution.Slid, "Movement: Sliding")]
    [InlineData(MovementResolution.Blocked, "Movement: Blocked")]
    [InlineData(MovementResolution.Separated, "Movement: Pushed apart")]
    public void FormatMovementLineLabelsEveryResolution(
        MovementResolution resolution,
        string expected)
    {
        var line = AgentInspectorContent.FormatMovementLine(resolution);

        Assert.Equal(expected, line);
    }

    [Fact]
    public void EveryMovementResolutionHasADistinctSpectatorLabel()
    {
        var labels = Enum.GetValues<MovementResolution>()
            .Select(AgentInspectorContent.GetMovementLabel)
            .ToArray();

        Assert.Equal(labels.Length, labels.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void MovementLineOccupiesItsOwnReservedLowerRow()
    {
        // The panel draws the movement line at row 6, so the lower block must
        // reserve seven rows before evidence wrapping begins.
        Assert.True(AgentInspectorContent.LowerRowCount >= 7);
    }

    private static Func<string, float> FixedWidthMeasure(
        float pixelsPerCharacter) =>
        text => text.Length * pixelsPerCharacter;
}
