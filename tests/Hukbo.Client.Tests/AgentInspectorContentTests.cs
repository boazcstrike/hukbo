using Hukbo.Client.Presentation;
using Hukbo.Client.UI;
using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
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
            WeaponId.Kampilan,
            ArmorId.LightOrganic,
            ShieldId.TallHardwood));

    [Fact]
    public void FormatWeaponLine_UsesLabelDerivedFromLoadoutWeapon()
    {
        var appearance = PawnAppearanceFactory.Create(
            SampleAgent.EntityId,
            SampleAgent.Loadout.Weapon,
            SampleAgent.Loadout.Shield);

        var line = AgentInspectorContent.FormatWeaponLine(appearance.WeaponLabel);

        Assert.Equal("Weapon: Kampilan — Great Blade", line);
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
    public void EveryLowerLineFitsInsideTheReservedRowBudget()
    {
        // ComputeRequiredHeight sizes the panel from MaximumLowerRowCount, so
        // a line added to BuildLowerLines without raising that constant would
        // be drawn past the panel bounds and silently dropped.
        var shielded = BuildLowerLineCount(
            WeaponId.Kalis,
            ShieldId.TallHardwood);
        var twoHanded = BuildLowerLineCount(
            WeaponId.Kampilan,
            ShieldId.None);

        Assert.True(
            shielded <= AgentInspectorContent.MaximumLowerRowCount,
            $"A shielded warrior produced {shielded} lower rows against a " +
            $"budget of {AgentInspectorContent.MaximumLowerRowCount}.");
        Assert.True(
            twoHanded <= AgentInspectorContent.MaximumLowerRowCount,
            $"A two-handed warrior produced {twoHanded} lower rows against " +
            $"a budget of {AgentInspectorContent.MaximumLowerRowCount}.");

        // The grip row is the only optional one, so a two-handed warrior
        // draws exactly one row fewer than a one-handed one.
        Assert.Equal(shielded - 1, twoHanded);
    }

    [Fact]
    public void LowerLinesCarryTheWeaponEvidenceTierAndAttributes()
    {
        var lines = AgentInspectorContent.BuildLowerLines(
            CreateAgentView(WeaponId.Kalis, ShieldId.TallHardwood),
            "Kalis — Thrusting Blade",
            "Documented");

        Assert.Contains(
            lines,
            line => line.Contains(
                "Kalis — Thrusting Blade",
                StringComparison.Ordinal));
        Assert.Contains(
            lines,
            line => line.Contains("Documented", StringComparison.Ordinal));

        // The paired Kalis profile: 10 damage, 12 world units, 5 ticks.
        Assert.Contains(
            lines,
            line => line.Contains("10 dmg", StringComparison.Ordinal) &&
                line.Contains("12 reach", StringComparison.Ordinal) &&
                line.Contains("5 tick recovery", StringComparison.Ordinal));
        Assert.Contains(
            lines,
            line => line.Contains("shielded", StringComparison.Ordinal));
    }

    [Fact]
    public void FormatLevelLineRendersTheRawLevelValue()
    {
        var line = AgentInspectorContent.FormatLevelLine(3);

        Assert.Equal("Level: 3", line);
    }

    [Fact]
    public void FormatComboAttributeLineRendersAllFourComboFieldsAsPercentagesAndCounts()
    {
        // Kalis solo, PhilippineCombatPresetV3.Profile's exact authored
        // values: ComboOpenChanceBasisPoints 3,500,
        // ComboContinueChanceBasisPoints 4,500, ComboMaxSteps 4,
        // ComboCooldownTicks 3.
        var profile = new WeaponProfile(
            DamagePerAttack: 11,
            AttackRangeRaw: 13 * FixedPoint.Scale,
            AttackCooldownTicks: 5,
            ComboOpenChanceBasisPoints: 3_500,
            ComboContinueChanceBasisPoints: 4_500,
            ComboMaxSteps: 4,
            ComboCooldownTicks: 3);

        var line = AgentInspectorContent.FormatComboAttributeLine(profile);

        Assert.Equal(
            "        35% combo open / 45% combo continue / " +
            "4 max steps / 3 tick combo cooldown",
            line);
    }

    [Fact]
    public void LowerLinesCarryTheAgentLevelAndComboAttributesWhenAProfileResolves()
    {
        var lines = AgentInspectorContent.BuildLowerLines(
            CreateAgentView(WeaponId.Kalis, ShieldId.TallHardwood, level: 3),
            "Kalis — Thrusting Blade",
            "Documented");

        Assert.Contains(lines, line => line == "Level: 3");

        // The paired Kalis profile (the first registered preset with
        // weapon profiles that TryResolveProfile resolves is V2, whose
        // combo fields are a true no-op per
        // docs/plans/2026-07-27-combat-preset-v3-combos.md section 5):
        // ComboOpenChanceBasisPoints 0, ComboContinueChanceBasisPoints
        // 4,500, ComboMaxSteps 4, ComboCooldownTicks 3 — rendered here
        // exactly as authored, and immediately below the V2 attribute
        // line and the level line.
        var attributeLineIndex = lines.ToList().FindIndex(
            line => line.Contains("10 dmg", StringComparison.Ordinal));
        var levelLineIndex = lines.ToList().FindIndex(
            line => line == "Level: 3");
        var comboLineIndex = lines.ToList().FindIndex(
            line => line ==
                "        0% combo open / 45% combo continue / " +
                "4 max steps / 3 tick combo cooldown");

        Assert.True(attributeLineIndex >= 0);
        Assert.Equal(attributeLineIndex + 1, levelLineIndex);
        Assert.Equal(levelLineIndex + 1, comboLineIndex);
    }

    [Fact]
    public void LowerLinesOmitTheGripRowForATwoHandedWeapon()
    {
        var lines = AgentInspectorContent.BuildLowerLines(
            CreateAgentView(WeaponId.Kampilan, ShieldId.None),
            "Kampilan — Great Blade",
            "Documented, form uncertain");

        Assert.DoesNotContain(
            lines,
            line => line.StartsWith("Grip:", StringComparison.Ordinal));
    }

    private static int BuildLowerLineCount(WeaponId weapon, ShieldId shield) =>
        AgentInspectorContent.BuildLowerLines(
            CreateAgentView(weapon, shield),
            "label",
            "tier").Count;

    private static AgentView CreateAgentView(
        WeaponId weapon,
        ShieldId shield,
        int level = 1) =>
        new(
            EntityId: 1,
            FactionId: 0,
            XRaw: 0,
            YRaw: 0,
            HitPoints: 10,
            MaximumHitPoints: 10,
            TargetEntityId: null,
            Intent: AgentIntent.Idle,
            IsAlive: true,
            Loadout: new CombatLoadout(weapon, ArmorId.LightOrganic, shield),
            MovementResolution: MovementResolution.Moved,
            Level: level);

    private static Func<string, float> FixedWidthMeasure(
        float pixelsPerCharacter) =>
        text => text.Length * pixelsPerCharacter;
}
