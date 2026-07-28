using Hukbo.Client.Presentation;
using Hukbo.Client.Presentation.Catalogs;
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

    // ===== VIS-012: weapon-variant inspector lines =====

    // The tint's identifier, not the internal WeaponTintEntry record itself,
    // crosses the [Theory]/[MemberData] boundary: WeaponTintEntry is
    // internal, and a public test method's parameter (or a public
    // TheoryData<T>'s T) may never be less accessible than the method itself
    // (CS0051/CS0050) while xunit's own analyzer (xUnit1000) requires the
    // test class, and therefore its test methods, to stay public — the same
    // discipline AppearanceComponentCatalogTests.ArmorEntriesWithAWidthFactor
    // already records for its own internal record type.
    public static TheoryData<PawnWeaponRole, string> AllWeaponTintIds()
    {
        var data = new TheoryData<PawnWeaponRole, string>();
        foreach (var weapon in Enum.GetValues<PawnWeaponRole>())
        {
            foreach (var tint in WeaponVisualCatalog.GetTints(weapon))
            {
                data.Add(weapon, tint.Catalog.Id);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllWeaponTintIds))]
    public void BuildWeaponVariantLines_ForEveryShippedTint_ShowsItsOwnTierAndNote(
        PawnWeaponRole weapon,
        string tintId)
    {
        var tint = WeaponVisualCatalog.GetTints(weapon)
            .Single(entry => entry.Catalog.Id == tintId);

        var lines = AgentInspectorContent.BuildWeaponVariantLines(
            weapon,
            tintId);

        Assert.Contains(
            AgentInspectorContent.FormatVariantTierLine(tint.Catalog.EvidenceTier),
            lines);
        Assert.Contains(
            AgentInspectorContent.FormatVariantNoteLine(tint.Catalog.Notes),
            lines);
    }

    [Theory]
    [InlineData(
        VisualEvidenceTier.Documented,
        "Documented")]
    [InlineData(
        VisualEvidenceTier.DocumentedFormUncertain,
        "Documented, form uncertain")]
    [InlineData(
        VisualEvidenceTier.ProvisionalReconstruction,
        "Provisional reconstruction")]
    [InlineData(
        VisualEvidenceTier.PresentationOnly,
        "presentation-only, no historical claim")]
    public void FormatVisualEvidenceTierLabel_RendersEveryDefinedTier(
        VisualEvidenceTier tier,
        string expected)
    {
        var label = AgentInspectorContent.FormatVisualEvidenceTierLabel(tier);

        Assert.Equal(expected, label);
    }

    [Fact]
    public void BuildWeaponVariantLines_ForKampilan_CarriesK2AsALaterOrProvisionalForm()
    {
        var lines = AgentInspectorContent.BuildWeaponVariantLines(
            PawnWeaponRole.Kampilan,
            WeaponVisualCatalog.KampilanTintFreshIron.Catalog.Id);

        Assert.Contains(
            AgentInspectorContent.FormatLaterFormLine(
                WeaponVisualCatalog.KampilanK2.Catalog),
            lines);
        Assert.Contains(
            lines,
            line => line.Contains(
                "later or provisional form",
                StringComparison.Ordinal));
    }

    [Fact]
    public void BuildWeaponVariantLines_ForKalis_CarriesBothL2AndL3AsLaterOrProvisionalForms()
    {
        var lines = AgentInspectorContent.BuildWeaponVariantLines(
            PawnWeaponRole.Kalis,
            WeaponVisualCatalog.KalisTintFreshIron.Catalog.Id);

        Assert.Contains(
            AgentInspectorContent.FormatLaterFormLine(
                WeaponVisualCatalog.KalisL2.Catalog),
            lines);
        Assert.Contains(
            AgentInspectorContent.FormatLaterFormLine(
                WeaponVisualCatalog.KalisL3.Catalog),
            lines);
    }

    [Theory]
    [InlineData(PawnWeaponRole.Kampilan, 3)]
    [InlineData(PawnWeaponRole.Wasay, 2)]
    [InlineData(PawnWeaponRole.Kalis, 4)]
    [InlineData(PawnWeaponRole.Itak, 2)]
    public void BuildWeaponVariantLines_LineCountMatchesTierNoteAndLaterFormCount(
        PawnWeaponRole weapon,
        int expectedCount)
    {
        var tintId = WeaponVisualCatalog.GetTints(weapon)[0].Catalog.Id;

        var lines = AgentInspectorContent.BuildWeaponVariantLines(
            weapon,
            tintId);

        Assert.Equal(expectedCount, lines.Count);
    }

    [Theory]
    [InlineData(PawnWeaponRole.Wasay)]
    [InlineData(PawnWeaponRole.Itak)]
    public void BuildWeaponVariantLines_ForWasayAndItak_NeverCarriesALaterOrProvisionalForm(
        PawnWeaponRole weapon)
    {
        var tintId = WeaponVisualCatalog.GetTints(weapon)[0].Catalog.Id;

        var lines = AgentInspectorContent.BuildWeaponVariantLines(
            weapon,
            tintId);

        Assert.DoesNotContain(
            lines,
            line => line.Contains(
                "later or provisional form",
                StringComparison.Ordinal));
    }

    [Fact]
    public void BuildWeaponVariantLines_UnresolvedTintId_OmitsVariantLinesButKeepsLaterForms()
    {
        var lines = AgentInspectorContent.BuildWeaponVariantLines(
            PawnWeaponRole.Kampilan,
            "weapon.kampilan.tint.doesNotExist");

        Assert.DoesNotContain(
            lines,
            line => line.StartsWith(
                "        Variant evidence:",
                StringComparison.Ordinal));
        Assert.Contains(
            AgentInspectorContent.FormatLaterFormLine(
                WeaponVisualCatalog.KampilanK2.Catalog),
            lines);
    }

    [Fact]
    public void BuildWeaponVariantLines_UnresolvedTintIdAndNoLaterForms_ReturnsEmpty()
    {
        var lines = AgentInspectorContent.BuildWeaponVariantLines(
            PawnWeaponRole.Wasay,
            "weapon.wasay.tint.doesNotExist");

        Assert.Empty(lines);
    }

    [Theory]
    [InlineData(
        PawnWeaponRole.Kampilan,
        "Kampilan",
        "Kampilan — Great Blade")]
    [InlineData(
        PawnWeaponRole.Wasay,
        "Wasay",
        "Wasay — War Axe")]
    [InlineData(
        PawnWeaponRole.Kalis,
        "Kalis",
        "Kalis — Thrusting Blade")]
    [InlineData(
        PawnWeaponRole.Itak,
        "Itak",
        "Itak — Work Blade")]
    public void BuildWeaponVariantLines_NeverShowsABareFilipinoTermWithoutItsDescriptor(
        PawnWeaponRole weapon,
        string bareTerm,
        string pairForm)
    {
        var tintId = WeaponVisualCatalog.GetTints(weapon)[0].Catalog.Id;

        var lines = AgentInspectorContent.BuildWeaponVariantLines(
            weapon,
            tintId);

        Assert.All(
            lines.Where(line => line.Contains(
                bareTerm,
                StringComparison.Ordinal)),
            line => Assert.Contains(
                pairForm,
                line,
                StringComparison.Ordinal));
    }

    // ===== VIS-016: shield-variant inspector lines =====

    public static TheoryData<string> AllShieldSkinIds()
    {
        var data = new TheoryData<string>();
        foreach (var skin in ShieldVisualCatalog.TallHardwoodSkins)
        {
            data.Add(skin.Catalog.Id);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllShieldSkinIds))]
    public void BuildShieldVariantLines_ForEveryShippedSkin_ShowsItsOwnLabelTierAndNote(
        string skinId)
    {
        var skin = ShieldVisualCatalog.TallHardwoodSkins
            .Single(entry => entry.Catalog.Id == skinId);

        var lines = AgentInspectorContent.BuildShieldVariantLines(
            PawnShieldRole.TallHardwood,
            skinId);

        Assert.Contains(
            AgentInspectorContent.FormatShieldSkinLabelLine(skin.Catalog.DisplayLabel),
            lines);
        Assert.Contains(
            AgentInspectorContent.FormatShieldVariantTierLine(skin.Catalog.EvidenceTier),
            lines);
        Assert.Contains(
            AgentInspectorContent.FormatShieldVariantNoteLine(skin.Catalog.Notes),
            lines);
    }

    [Theory]
    [MemberData(nameof(AllShieldSkinIds))]
    public void BuildShieldVariantLines_ForEveryShippedSkin_AlwaysAppendsThePalisayResearchNote(
        string skinId)
    {
        var lines = AgentInspectorContent.BuildShieldVariantLines(
            PawnShieldRole.TallHardwood,
            skinId);

        Assert.Contains(
            AgentInspectorContent.FormatShieldResearchNoteLine(
                AgentInspectorContent.PalisayResearchNote),
            lines);
    }

    [Theory]
    [MemberData(nameof(AllShieldSkinIds))]
    public void BuildShieldVariantLines_LineCountIsLabelTierNotePlusResearchNote(
        string skinId)
    {
        var lines = AgentInspectorContent.BuildShieldVariantLines(
            PawnShieldRole.TallHardwood,
            skinId);

        Assert.Equal(4, lines.Count);
    }

    [Fact]
    public void BuildShieldVariantLines_ForAnUnshieldedPawn_ReturnsEmpty()
    {
        var lines = AgentInspectorContent.BuildShieldVariantLines(
            PawnShieldRole.None,
            ShieldVisualCatalog.MactanThin.Catalog.Id);

        Assert.Empty(lines);
    }

    [Fact]
    public void BuildShieldVariantLines_UnresolvedSkinId_OmitsSkinLinesButKeepsTheResearchNote()
    {
        var lines = AgentInspectorContent.BuildShieldVariantLines(
            PawnShieldRole.TallHardwood,
            "shield.tallHardwood.doesNotExist");

        Assert.DoesNotContain(
            lines,
            line => line.StartsWith(
                "        Shield skin:",
                StringComparison.Ordinal));
        Assert.DoesNotContain(
            lines,
            line => line.StartsWith(
                "        Shield evidence:",
                StringComparison.Ordinal));
        var onlyLine = Assert.Single(lines);
        Assert.Equal(
            AgentInspectorContent.FormatShieldResearchNoteLine(
                AgentInspectorContent.PalisayResearchNote),
            onlyLine);
    }

    // ===== VIS-016: shield name negative tests (the point of this task) =====

    private static readonly string[] ForbiddenShieldNames =
    [
        "Kalasag",
        "Palisay",
        "Taming",
        "Salakot",
        "Panabas",
    ];

    private static IEnumerable<string> AllShieldPlayerFacingLabels()
    {
        foreach (var shieldId in Enum.GetValues<ShieldId>())
        {
            yield return AgentInspectorContent.GetShieldLabel(shieldId);
            yield return AgentInspectorContent.FormatShieldLine(shieldId);

            var appearance = PawnAppearanceFactory.Create(
                1UL,
                WeaponId.Kalis,
                shieldId);
            yield return appearance.ShieldLabel;
        }

        foreach (var skin in ShieldVisualCatalog.TallHardwoodSkins)
        {
            yield return skin.Catalog.DisplayLabel;

            var labelLine = AgentInspectorContent.BuildShieldVariantLines(
                    PawnShieldRole.TallHardwood,
                    skin.Catalog.Id)
                .First(line => line.StartsWith(
                    "        Shield skin:",
                    StringComparison.Ordinal));
            yield return labelLine;
        }

        yield return ShieldVisualCatalog.Default.Catalog.DisplayLabel;
        yield return ShieldVisualCatalog.ModelCategoryDefault.Catalog.DisplayLabel;
    }

    private static IEnumerable<string> AllShieldInspectorText()
    {
        foreach (var label in AllShieldPlayerFacingLabels())
        {
            yield return label;
        }

        foreach (var skin in ShieldVisualCatalog.TallHardwoodSkins)
        {
            yield return skin.Catalog.Id;
            yield return skin.Catalog.Notes;

            foreach (var line in AgentInspectorContent.BuildShieldVariantLines(
                PawnShieldRole.TallHardwood,
                skin.Catalog.Id))
            {
                yield return line;
            }
        }

        yield return ShieldVisualCatalog.Default.Catalog.Id;
        yield return ShieldVisualCatalog.Default.Catalog.Notes;
        yield return ShieldVisualCatalog.ModelCategoryDefault.Catalog.Id;
        yield return ShieldVisualCatalog.ModelCategoryDefault.Catalog.Notes;
        yield return AgentInspectorContent.PalisayResearchNote;
    }

    [Fact]
    public void NoShieldPlayerFacingLabelContainsAnUnverifiedOrExcludedShieldName()
    {
        foreach (var label in AllShieldPlayerFacingLabels())
        {
            foreach (var forbidden in ForbiddenShieldNames)
            {
                Assert.DoesNotContain(
                    forbidden,
                    label,
                    StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void KalasagNameAppearsOnlyInsideTheVisayanKalasagSkinsFlaggedPendingNote()
    {
        Assert.Contains(
            "kalasag",
            ShieldVisualCatalog.VisayanKalasag.Catalog.Notes,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "pending",
            ShieldVisualCatalog.VisayanKalasag.Catalog.Notes,
            StringComparison.OrdinalIgnoreCase);

        foreach (var skin in ShieldVisualCatalog.TallHardwoodSkins.Where(
            entry => entry.Catalog.Id !=
                ShieldVisualCatalog.VisayanKalasag.Catalog.Id))
        {
            Assert.DoesNotContain(
                "kalasag",
                skin.Catalog.Notes,
                StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain(
            "kalasag",
            AgentInspectorContent.PalisayResearchNote,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "kalasag",
            ShieldVisualCatalog.Default.Catalog.Notes,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "kalasag",
            ShieldVisualCatalog.ModelCategoryDefault.Catalog.Notes,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PalisayNameAppearsOnlyInsideTheFlaggedPendingResearchNote()
    {
        Assert.Contains(
            "palisay",
            AgentInspectorContent.PalisayResearchNote,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "attestation-pending",
            AgentInspectorContent.PalisayResearchNote,
            StringComparison.OrdinalIgnoreCase);

        foreach (var skin in ShieldVisualCatalog.TallHardwoodSkins)
        {
            Assert.DoesNotContain(
                "palisay",
                skin.Catalog.Notes,
                StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain(
            "palisay",
            ShieldVisualCatalog.Default.Catalog.Notes,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "palisay",
            ShieldVisualCatalog.ModelCategoryDefault.Catalog.Notes,
            StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Taming")]
    [InlineData("Salakot")]
    [InlineData("Panabas")]
    public void ExcludedShieldNamesNeverAppearAnywhereInShieldInspectorText(
        string excludedName)
    {
        foreach (var text in AllShieldInspectorText())
        {
            Assert.DoesNotContain(
                excludedName,
                text,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    // ===== VIS-024: appearance-preset inspector lines =====

    [Fact]
    public void BuildAppearancePresetLines_ForLev01_ShowsNameScopeTierAndComponents()
    {
        var lines = AgentInspectorContent.BuildAppearancePresetLines(
            "appearance.presetLevy.lev01");

        Assert.Contains(
            AgentInspectorContent.FormatAppearancePresetNameLine("Levy Warrior"),
            lines);
        Assert.Contains(
            AgentInspectorContent.FormatAppearanceScopeLine(VisualScopeTag.UnscopedGeneric),
            lines);
        Assert.Contains(
            AgentInspectorContent.FormatAppearancePresetTierLine(
                VisualEvidenceTier.DocumentedFormUncertain),
            lines);
        Assert.Contains(
            AgentInspectorContent.FormatAppearanceComponentLine(
                AppearanceComponentCategory.Hair,
                AppearanceComponentCatalog.HairB1LongHairKnotted.Catalog),
            lines);
        Assert.Contains(
            AgentInspectorContent.FormatAppearanceComponentNoteLine(
                AppearanceComponentCatalog.HairB1LongHairKnotted.Catalog.Notes),
            lines);
        Assert.Contains(
            AgentInspectorContent.FormatAppearanceComponentLine(
                AppearanceComponentCategory.Condition,
                AppearanceComponentCatalog.ConditionK1Clean.Catalog),
            lines);
        Assert.Equal(
            AgentInspectorContent.FormatAppearanceFlavorNoteLine(
                AgentInspectorContent.AppearanceNonRenderableFlavorNote),
            lines[^1]);
    }

    public static TheoryData<string> AllAppearancePresetIds()
    {
        var data = new TheoryData<string>();
        foreach (var preset in AppearancePresets.All)
        {
            data.Add(preset.Catalog.Id);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllAppearancePresetIds))]
    public void BuildAppearancePresetLines_ForEveryShippedPreset_ShowsItsOwnNameScopeAndTier(
        string presetId)
    {
        var preset = Assert.Single(AppearancePresets.All, p => p.Catalog.Id == presetId);

        var lines = AgentInspectorContent.BuildAppearancePresetLines(presetId);

        Assert.Contains(
            AgentInspectorContent.FormatAppearancePresetNameLine(preset.Catalog.DisplayLabel),
            lines);
        Assert.Contains(
            AgentInspectorContent.FormatAppearanceScopeLine(preset.Block),
            lines);
        Assert.Contains(
            AgentInspectorContent.FormatAppearancePresetTierLine(preset.Catalog.EvidenceTier),
            lines);
        Assert.Equal(
            AgentInspectorContent.FormatAppearanceFlavorNoteLine(
                AgentInspectorContent.AppearanceNonRenderableFlavorNote),
            lines[^1]);
    }

    [Theory]
    [MemberData(nameof(AllAppearancePresetIds))]
    public void BuildAppearancePresetLines_ForEveryShippedPreset_ScopeTagIsNeverNotApplicable(
        string presetId)
    {
        var preset = Assert.Single(AppearancePresets.All, p => p.Catalog.Id == presetId);

        Assert.NotEqual(VisualScopeTag.NotApplicable, preset.Block);
        Assert.Contains(
            AgentInspectorContent.FormatAppearanceScopeLine(preset.Block),
            AgentInspectorContent.BuildAppearancePresetLines(presetId));
    }

    [Fact]
    public void BuildAppearancePresetLines_ForVis12_ShowsBaroteKanditAndBatukPendingLines()
    {
        var lines = AgentInspectorContent.BuildAppearancePresetLines(
            AppearancePresetsVisayan.Vis12.Catalog.Id);

        Assert.Contains(
            lines,
            line => line.Contains("barote", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            lines,
            line => line.Contains("kandit", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            lines,
            line => line.Contains("batuk", StringComparison.OrdinalIgnoreCase));
        Assert.All(
            lines.Where(line => line.Contains("barote", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("kandit", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("batuk", StringComparison.OrdinalIgnoreCase)),
            line => Assert.Contains(
                "pending",
                line,
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildAppearancePresetLines_ForVis14_ShowsPanikaAndKamagiPendingLines()
    {
        var lines = AgentInspectorContent.BuildAppearancePresetLines(
            AppearancePresetsVisayan.Vis14.Catalog.Id);

        Assert.Contains(
            lines,
            line => line.Contains("panika", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            lines,
            line => line.Contains("kamagi", StringComparison.OrdinalIgnoreCase));
        Assert.All(
            lines.Where(line => line.Contains("panika", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("kamagi", StringComparison.OrdinalIgnoreCase)),
            line => Assert.Contains(
                "pending",
                line,
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildAppearancePresetLines_AlwaysAppendsTheNonRenderableFlavorNoteContainingKolombiga()
    {
        var lines = AgentInspectorContent.BuildAppearancePresetLines(
            "appearance.presetLevy.lev01");

        Assert.Contains(
            AgentInspectorContent.FormatAppearanceFlavorNoteLine(
                AgentInspectorContent.AppearanceNonRenderableFlavorNote),
            lines);
        Assert.Contains("kolombiga", AgentInspectorContent.AppearanceNonRenderableFlavorNote,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pending", AgentInspectorContent.AppearanceNonRenderableFlavorNote,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildAppearancePresetLines_UnresolvedPresetId_OmitsPresetLinesButKeepsTheFlavorNote()
    {
        var lines = AgentInspectorContent.BuildAppearancePresetLines(
            "appearance.presetLevy.doesNotExist");

        Assert.DoesNotContain(
            lines,
            line => line.StartsWith("Appearance preset:", StringComparison.Ordinal));
        Assert.DoesNotContain(
            lines,
            line => line.StartsWith("        Scope:", StringComparison.Ordinal));
        var onlyLine = Assert.Single(lines);
        Assert.Equal(
            AgentInspectorContent.FormatAppearanceFlavorNoteLine(
                AgentInspectorContent.AppearanceNonRenderableFlavorNote),
            onlyLine);
    }

    [Theory]
    [InlineData(
        nameof(VisualScopeTag.NotApplicable),
        "Not applicable")]
    [InlineData(
        nameof(VisualScopeTag.Visayan),
        "Visayan")]
    [InlineData(
        nameof(VisualScopeTag.Tagalog),
        "Tagalog")]
    [InlineData(
        nameof(VisualScopeTag.Cagayan),
        "Cagayan")]
    [InlineData(
        nameof(VisualScopeTag.UnscopedGeneric),
        "Unscoped-generic")]
    public void FormatVisualScopeTagLabel_RendersEveryDefinedScope(
        string scopeName,
        string expected)
    {
        var scope = Enum.Parse<VisualScopeTag>(scopeName);
        var label = AgentInspectorContent.FormatVisualScopeTagLabel(scope);

        Assert.Equal(expected, label);
    }

    // ===== VIS-024: appearance pending/forbidden-term negative tests =====

    private static readonly string[] ForbiddenAppearanceTerms =
    [
        "barote",
        "kandit",
        "panika",
        "kamagi",
        "batuk",
        "kolombiga",
    ];

    private static IEnumerable<string> AllAppearancePlayerFacingLabels()
    {
        foreach (var component in AppearanceComponentCatalog.All)
        {
            yield return component.Catalog.DisplayLabel;
        }

        foreach (var preset in AppearancePresets.All)
        {
            yield return preset.Catalog.DisplayLabel;
        }
    }

    private static IEnumerable<string> AllAppearanceInspectorText()
    {
        foreach (var label in AllAppearancePlayerFacingLabels())
        {
            yield return label;
        }

        foreach (var component in AppearanceComponentCatalog.All)
        {
            yield return component.Catalog.Id;
            yield return component.Catalog.Notes;
        }

        foreach (var preset in AppearancePresets.All)
        {
            yield return preset.Catalog.Id;
            yield return preset.Catalog.Notes;

            foreach (var line in AgentInspectorContent.BuildAppearancePresetLines(
                preset.Catalog.Id))
            {
                yield return line;
            }
        }

        yield return AgentInspectorContent.AppearanceNonRenderableFlavorNote;
    }

    [Fact]
    public void NoAppearancePlayerFacingLabelContainsAPendingOrExcludedAppearanceTerm()
    {
        foreach (var label in AllAppearancePlayerFacingLabels())
        {
            foreach (var forbidden in ForbiddenAppearanceTerms)
            {
                Assert.DoesNotContain(
                    forbidden,
                    label,
                    StringComparison.OrdinalIgnoreCase);
            }

            Assert.DoesNotContain(
                "salakot",
                label,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData("barote")]
    [InlineData("kandit")]
    [InlineData("panika")]
    [InlineData("kamagi")]
    [InlineData("batuk")]
    [InlineData("kolombiga")]
    public void PendingAppearanceTermsAppearOnlyInsideFlaggedPendingInspectorText(
        string pendingTerm)
    {
        var matchingLines = AllAppearanceInspectorText()
            .Where(text => text.Contains(pendingTerm, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.NotEmpty(matchingLines);
        Assert.All(
            matchingLines,
            line => Assert.Contains(
                "pending",
                line,
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SalakotNeverAppearsAnywhereInAppearanceInspectorText()
    {
        foreach (var text in AllAppearanceInspectorText())
        {
            Assert.DoesNotContain(
                "salakot",
                text,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData(ContingentState.Advance, "Contingent: 2 — Advancing")]
    [InlineData(ContingentState.Hold, "Contingent: 2 — Holding")]
    [InlineData(ContingentState.Close, "Contingent: 2 — Closing")]
    [InlineData(ContingentState.Break, "Contingent: 2 — Broken")]
    public void FormatContingentLineLabelsEveryNonNoneState(
        ContingentState state,
        string expected)
    {
        var line = AgentInspectorContent.FormatContingentLine(2, state);

        Assert.Equal(expected, line);
    }

    [Fact]
    public void FormatContingentLineIsNullWhenStateIsNone()
    {
        var line = AgentInspectorContent.FormatContingentLine(
            2,
            ContingentState.None);

        Assert.Null(line);
    }

    [Theory]
    [InlineData(ContingentState.Advance)]
    [InlineData(ContingentState.Hold)]
    [InlineData(ContingentState.Close)]
    [InlineData(ContingentState.Break)]
    public void LowerLinesIncludeTheContingentRowImmediatelyAfterIntent(
        ContingentState state)
    {
        var lines = AgentInspectorContent.BuildLowerLines(
            CreateAgentView(
                WeaponId.Kalis,
                ShieldId.TallHardwood,
                contingentId: 3,
                contingentState: state),
            "Kalis — Thrusting Blade",
            "Documented");

        var list = lines.ToList();
        var intentIndex = list.FindIndex(
            line => line.StartsWith("Intent:", StringComparison.Ordinal));
        var contingentIndex = list.FindIndex(
            line => line.StartsWith("Contingent:", StringComparison.Ordinal));

        Assert.True(intentIndex >= 0);
        Assert.Equal(intentIndex + 1, contingentIndex);
        Assert.Equal(
            $"Contingent: 3 — " +
            AgentInspectorContent.GetContingentStateLabel(state),
            list[contingentIndex]);
    }

    [Fact]
    public void LowerLinesOmitTheContingentRowWhenStateIsNone()
    {
        var lines = AgentInspectorContent.BuildLowerLines(
            CreateAgentView(
                WeaponId.Kalis,
                ShieldId.TallHardwood,
                contingentId: 3,
                contingentState: ContingentState.None),
            "Kalis — Thrusting Blade",
            "Documented");

        Assert.DoesNotContain(
            lines,
            line => line.StartsWith("Contingent:", StringComparison.Ordinal));
    }

    [Fact]
    public void LowerLinesWithAContingentRowNeverExceedTheRowBudget()
    {
        var count = AgentInspectorContent.BuildLowerLines(
            CreateAgentView(
                WeaponId.Kalis,
                ShieldId.TallHardwood,
                contingentId: 3,
                contingentState: ContingentState.Hold),
            "Kalis — Thrusting Blade",
            "Documented").Count;

        Assert.True(
            count <= AgentInspectorContent.MaximumLowerRowCount,
            $"A shielded warrior with a contingent row produced {count} " +
            $"lower rows against a budget of " +
            $"{AgentInspectorContent.MaximumLowerRowCount}.");
    }

    [Theory]
    [InlineData(RankId.Datu, "Rank: Datu — Chief")]
    [InlineData(RankId.Maharlika, "Rank: Maharlika — Sworn Freeman")]
    [InlineData(RankId.Timawa, "Rank: Timawa — Bound Freeman")]
    [InlineData(RankId.AlipingNamamahay, "Rank: Aliping Namamahay — Householder")]
    [InlineData(RankId.Ayuey, "Rank: Ayuey — Household Dependent")]
    public void FormatRankLine_RendersEveryRankAsAPairFormLabel(
        RankId rank,
        string expected)
    {
        var line = AgentInspectorContent.FormatRankLine(rank);

        Assert.Equal(expected, line);
    }

    [Theory]
    [InlineData(RankId.Datu)]
    [InlineData(RankId.Maharlika)]
    [InlineData(RankId.Timawa)]
    [InlineData(RankId.Ayuey)]
    public void LowerLinesOmitTheRankReconstructionNoteRowForEveryRankExceptAlipingNamamahay(
        RankId rank)
    {
        var lines = AgentInspectorContent.BuildLowerLines(
            CreateAgentView(WeaponId.Kalis, ShieldId.TallHardwood, rank: rank),
            "Kalis — Thrusting Blade",
            "Documented");

        Assert.Contains(AgentInspectorContent.FormatRankLine(rank), lines);
        Assert.DoesNotContain(
            lines,
            line => line.Contains("Reconstruction:", StringComparison.Ordinal));
    }

    [Fact]
    public void LowerLinesShowTheRankReconstructionNoteRowOnlyForAlipingNamamahay()
    {
        var lines = AgentInspectorContent.BuildLowerLines(
            CreateAgentView(
                WeaponId.Itak,
                ShieldId.None,
                rank: RankId.AlipingNamamahay),
            "Itak — Work Blade",
            "Provisional reconstruction");

        var rankLine = AgentInspectorContent.FormatRankLine(RankId.AlipingNamamahay);
        var reconstructionNote =
            RankLabelCatalog.Get(RankId.AlipingNamamahay).ReconstructionNote;
        Assert.NotNull(reconstructionNote);
        var expectedNoteLine =
            AgentInspectorContent.FormatRankReconstructionNoteLine(reconstructionNote!);

        var list = lines.ToList();
        var rankIndex = list.FindIndex(line => line == rankLine);
        var noteIndex = list.FindIndex(line => line == expectedNoteLine);

        Assert.True(rankIndex >= 0);
        Assert.Equal(rankIndex + 1, noteIndex);
    }

    [Fact]
    public void LowerLinesWithAContingentRowAndTheRankReconstructionNoteNeverExceedTheRowBudget()
    {
        var count = AgentInspectorContent.BuildLowerLines(
            CreateAgentView(
                WeaponId.Kalis,
                ShieldId.TallHardwood,
                contingentId: 3,
                contingentState: ContingentState.Hold,
                rank: RankId.AlipingNamamahay),
            "Kalis — Thrusting Blade",
            "Documented").Count;

        Assert.True(
            count <= AgentInspectorContent.MaximumLowerRowCount,
            $"A shielded warrior with a contingent row and the rank " +
            $"reconstruction note produced {count} lower rows against a " +
            $"budget of {AgentInspectorContent.MaximumLowerRowCount}.");
    }

    private static int BuildLowerLineCount(WeaponId weapon, ShieldId shield) =>
        AgentInspectorContent.BuildLowerLines(
            CreateAgentView(weapon, shield),
            "label",
            "tier").Count;

    private static AgentView CreateAgentView(
        WeaponId weapon,
        ShieldId shield,
        int level = 1,
        int contingentId = 0,
        ContingentState contingentState = ContingentState.None,
        RankId rank = RankId.Timawa) =>
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
            Loadout: new CombatLoadout(weapon, ArmorId.LightOrganic, shield, rank),
            MovementResolution: MovementResolution.Moved,
            Level: level,
            ContingentId: contingentId,
            ContingentState: contingentState,
            Rank: rank);

    private static Func<string, float> FixedWidthMeasure(
        float pixelsPerCharacter) =>
        text => text.Length * pixelsPerCharacter;

    // ===== Warrior personal-name inspector lines =====

    // Same accessibility discipline as the weapon-tint theories above:
    // WarriorNameEntry is internal, so the identifier crosses the
    // [Theory]/[MemberData] boundary and the entry is looked up inside the
    // test body.
    public static TheoryData<string> AllWarriorNameIds()
    {
        var data = new TheoryData<string>();
        foreach (var name in WarriorNameCatalog.All)
        {
            data.Add(name.Id);
        }

        return data;
    }

    [Fact]
    public void FormatWarriorNameLine_ShowsTheNameAndTheEntityIdentifier()
    {
        var name = WarriorNameCatalog.Tagalog1589[0];

        Assert.Equal(
            $"Name: {name.DisplayForm} #42",
            AgentInspectorContent.FormatWarriorNameLine(name, 42));
    }

    [Theory]
    [MemberData(nameof(AllWarriorNameIds))]
    public void BuildWarriorNameLines_ForEveryShippedName_ShowsItsOwnProvenance(
        string nameId)
    {
        var name = WarriorNameCatalog.All.Single(entry => entry.Id == nameId);

        var lines = AgentInspectorContent.BuildWarriorNameLines(name);

        Assert.Contains(
            lines,
            line => line.Contains(
                AgentInspectorContent.FormatVisualEvidenceTierLabel(
                    name.EvidenceTier),
                StringComparison.Ordinal));
        Assert.Contains(
            lines,
            line => line.Contains(name.SourceCitation, StringComparison.Ordinal));
        Assert.Contains(
            lines,
            line => line.Contains(
                WarriorNameCatalog.GetRegionLabel(name.Region),
                StringComparison.Ordinal));
        Assert.Contains(
            lines,
            line => line.Contains(name.ReuseNote, StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(AllWarriorNameIds))]
    public void BuildWarriorNameLines_AlwaysAppendsBothStandaloneResearchNotes(
        string nameId)
    {
        var name = WarriorNameCatalog.All.Single(entry => entry.Id == nameId);

        var lines = AgentInspectorContent.BuildWarriorNameLines(name);

        Assert.Contains(
            lines,
            line => line.Contains(
                WarriorNameCatalog.ParenthoodResearchNote,
                StringComparison.Ordinal));
        Assert.Contains(
            lines,
            line => line.Contains(
                WarriorNameCatalog.WomensNamesResearchNote,
                StringComparison.Ordinal));
    }

    /// <summary>
    /// The recorded spelling is shown only where it differs from the
    /// displayed one, so a row that would merely repeat the name above it
    /// never takes up a line in a panel this tight.
    /// </summary>
    [Fact]
    public void BuildWarriorNameLines_ShowsTheRecordedSpellingOnlyWhenItDiffers()
    {
        var differing = WarriorNameCatalog.All.First(
            entry => !string.Equals(
                entry.RecordedForm,
                entry.DisplayForm,
                StringComparison.Ordinal));
        var identical = WarriorNameCatalog.All.First(
            entry => string.Equals(
                entry.RecordedForm,
                entry.DisplayForm,
                StringComparison.Ordinal));

        Assert.Contains(
            AgentInspectorContent.BuildWarriorNameLines(differing),
            line => line.Contains("Recorded as:", StringComparison.Ordinal));
        Assert.DoesNotContain(
            AgentInspectorContent.BuildWarriorNameLines(identical),
            line => line.Contains("Recorded as:", StringComparison.Ordinal));
    }

    /// <summary>
    /// The gender row reports what the source records, never what the warrior
    /// on screen is: the catalog holds no evidence of a restriction, and the
    /// line must not imply one.
    /// </summary>
    [Fact]
    public void FormatWarriorNameGenderLine_ReportsTheSourceNotTheWarrior()
    {
        Assert.Equal(
            "        Recorded gender in the source: a man",
            AgentInspectorContent.FormatWarriorNameGenderLine(
                WarriorNameGenderEvidence.RecordedMan));
        Assert.Equal(
            "        Recorded gender in the source: a woman",
            AgentInspectorContent.FormatWarriorNameGenderLine(
                WarriorNameGenderEvidence.RecordedWoman));
        Assert.Equal(
            "        Recorded gender in the source: not specified",
            AgentInspectorContent.FormatWarriorNameGenderLine(
                WarriorNameGenderEvidence.Unspecified));
    }

    /// <summary>
    /// The panel's own height budget must actually cover the rows
    /// <see cref="AgentInspectorContent.BuildWarriorNameLines"/> always
    /// produces before the long standalone notes begin.
    /// </summary>
    [Fact]
    public void WarriorNameReservedLineCount_CoversTheShortProvenanceRows()
    {
        foreach (var name in WarriorNameCatalog.All)
        {
            var shortRowCount = AgentInspectorContent.BuildWarriorNameLines(name)
                .Count - 2;

            Assert.True(
                shortRowCount <= AgentInspectorContent.WarriorNameReservedLineCount,
                $"{name.Id} needs {shortRowCount} provenance rows, budget is " +
                $"{AgentInspectorContent.WarriorNameReservedLineCount}.");
        }
    }
}
