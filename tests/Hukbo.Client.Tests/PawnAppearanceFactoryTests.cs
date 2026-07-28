using Hukbo.Client.Presentation;
using Hukbo.Client.Presentation.Catalogs;
using Hukbo.Core.Combat;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

public sealed class PawnAppearanceFactoryTests
{
    [Fact]
    public void Create_ReturnsIdenticalAppearanceForStableEntityIdAndWeapon()
    {
        var first = PawnAppearanceFactory.Create(42, WeaponId.Kampilan, ShieldId.None);
        var second = PawnAppearanceFactory.Create(42, WeaponId.Kampilan, ShieldId.None);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Create_SameEntityIdDifferentWeaponKeepsBodyButChangesRole()
    {
        var kampilan = PawnAppearanceFactory.Create(42, WeaponId.Kampilan, ShieldId.None);
        var itak = PawnAppearanceFactory.Create(42, WeaponId.Itak, ShieldId.None);

        Assert.Equal(kampilan.StatureMultiplier, itak.StatureMultiplier);
        Assert.Equal(kampilan.BuildMultiplier, itak.BuildMultiplier);
        Assert.Equal(kampilan.HeadTreatment, itak.HeadTreatment);
        Assert.Equal(kampilan.ClothingColor, itak.ClothingColor);
        Assert.Equal(kampilan.AccentColor, itak.AccentColor);
        Assert.Equal(kampilan.SkinColor, itak.SkinColor);
        Assert.Equal(kampilan.HeadTreatmentColor, itak.HeadTreatmentColor);
        Assert.NotEqual(kampilan.WeaponRole, itak.WeaponRole);
    }

    [Theory]
    [InlineData(WeaponId.Kampilan)]
    [InlineData(WeaponId.Wasay)]
    [InlineData(WeaponId.Kalis)]
    [InlineData(WeaponId.Itak)]
    public void Create_MapsEveryCoreWeaponIdToOneExplicitSilhouette(
        WeaponId weapon)
    {
        var appearance = PawnAppearanceFactory.Create(1, weapon, ShieldId.None);

        Assert.Equal(
            Enum.Parse<PawnWeaponRole>(weapon.ToString()),
            appearance.WeaponRole);
    }

    [Fact]
    public void Create_MapsAllFourWeaponIdsToDistinctSilhouettes()
    {
        var roles = Enum.GetValues<WeaponId>()
            .Select(weapon => PawnAppearanceFactory
                .Create(1, weapon, ShieldId.None)
                .WeaponRole)
            .ToHashSet();

        Assert.Equal(Enum.GetValues<WeaponId>().Length, roles.Count);
    }

    [Fact]
    public void Create_NeverDerivesWeaponRoleFromEntityIdAlone()
    {
        for (ulong entityId = 0; entityId < 20; entityId++)
        {
            foreach (var weapon in Enum.GetValues<WeaponId>())
            {
                var appearance = PawnAppearanceFactory.Create(entityId, weapon, ShieldId.None);

                Assert.Equal(
                    Enum.Parse<PawnWeaponRole>(weapon.ToString()),
                    appearance.WeaponRole);
            }
        }
    }

    [Fact]
    public void Create_UsesOnlyApprovedBodyMultipliers()
    {
        float[] allowedStatures = [0.90f, 1.00f, 1.10f];
        float[] allowedBuilds = [0.86f, 1.00f, 1.18f];

        for (ulong entityId = 0; entityId < 128; entityId++)
        {
            var appearance = PawnAppearanceFactory.Create(
                entityId,
                WeaponId.Kampilan, ShieldId.None);

            Assert.Contains(appearance.StatureMultiplier, allowedStatures);
            Assert.Contains(appearance.BuildMultiplier, allowedBuilds);
        }
    }

    [Fact]
    public void WeaponLabels_UseThePairForm()
    {
        string[] labels =
        [
            PawnAppearanceFactory
                .Create(0, WeaponId.Kampilan, ShieldId.None).WeaponLabel,
            PawnAppearanceFactory
                .Create(0, WeaponId.Wasay, ShieldId.None).WeaponLabel,
            PawnAppearanceFactory
                .Create(0, WeaponId.Kalis, ShieldId.None).WeaponLabel,
            PawnAppearanceFactory
                .Create(0, WeaponId.Itak, ShieldId.None).WeaponLabel,
        ];

        Assert.Equal(
            [
                "Kampilan — Great Blade",
                "Wasay — War Axe",
                "Kalis — Thrusting Blade",
                "Itak — Work Blade",
            ],
            labels);
    }

    [Fact]
    public void WeaponLabels_NeverCarryACulturalNameWithoutItsDescriptor()
    {
        // The pair form is what CLAUDE.md section 7 permits: a cultural
        // identification appears only alongside a plain English descriptor,
        // never bare. This is the assertion that catches a label regressing
        // to just "Kampilan".
        foreach (var weapon in Enum.GetValues<WeaponId>())
        {
            var label = PawnAppearanceFactory
                .Create(0, weapon, ShieldId.None)
                .WeaponLabel;

            var parts = label.Split(" — ");
            Assert.Equal(2, parts.Length);
            Assert.NotEmpty(parts[0]);
            Assert.NotEmpty(parts[1]);
        }
    }

    [Fact]
    public void WeaponLabels_NeverUseTheRejectedPanabasName()
    {
        // The panabas is first documented in nineteenth-century accounts,
        // roughly three centuries after the depicted period. The hundred-year
        // attestation rule excludes it outright rather than badging it
        // provisional, and this test is what keeps that rule load-bearing.
        foreach (var weapon in Enum.GetValues<WeaponId>())
        {
            Assert.DoesNotContain(
                "panabas",
                PawnAppearanceFactory
                    .Create(0, weapon, ShieldId.None)
                    .WeaponLabel,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void EvidenceTier_MatchesTheResearchDocument()
    {
        // Kalis is the only one a contemporary source names directly:
        // Pigafetta recorded calis in 1521. Kampilan and Wasay have an
        // attested weapon class but no period attestation of the name, and
        // Itak's is a reasoned reconstruction.
        AssertTier(WeaponId.Kalis, WeaponEvidenceTier.Documented);
        AssertTier(
            WeaponId.Kampilan,
            WeaponEvidenceTier.DocumentedFormUncertain);
        AssertTier(WeaponId.Wasay, WeaponEvidenceTier.DocumentedFormUncertain);
        AssertTier(
            WeaponId.Itak,
            WeaponEvidenceTier.ProvisionalReconstruction);

        static void AssertTier(WeaponId weapon, WeaponEvidenceTier expected)
        {
            var appearance = PawnAppearanceFactory.Create(
                0,
                weapon,
                ShieldId.None);

            Assert.Equal(expected, appearance.EvidenceTier);
            Assert.NotEmpty(appearance.EvidenceTierLabel);
        }
    }

    [Fact]
    public void EveryWeaponCarriesAnEvidenceNote()
    {
        // A name shown to a spectator always says how far its evidence
        // reaches. There is no weapon whose note may be blank.
        foreach (var weapon in Enum.GetValues<WeaponId>())
        {
            Assert.NotEmpty(
                PawnAppearanceFactory
                    .Create(0, weapon, ShieldId.None)
                    .EvidenceNote);
        }
    }

    [Fact]
    public void ShieldRoleComesFromTheAuthoritativeLoadout()
    {
        AssertRole(ShieldId.None, PawnShieldRole.None, carriesShield: false);
        AssertRole(
            ShieldId.TallHardwood,
            PawnShieldRole.TallHardwood,
            carriesShield: true);

        static void AssertRole(
            ShieldId shield,
            PawnShieldRole expectedRole,
            bool carriesShield)
        {
            var appearance = PawnAppearanceFactory.Create(
                7,
                WeaponId.Kalis,
                shield);

            Assert.Equal(expectedRole, appearance.ShieldRole);
            Assert.Equal(carriesShield, appearance.CarriesShield);
        }
    }

    [Fact]
    public void ShieldRoleIsNeverDerivedFromTheEntityId()
    {
        // Same reasoning as the weapon role: equipment identity is
        // authoritative Core state, and only stature, build, clothing, skin,
        // and head treatment may vary with the entity ID.
        for (ulong entityId = 1; entityId <= 64; entityId++)
        {
            Assert.Equal(
                PawnShieldRole.None,
                PawnAppearanceFactory
                    .Create(entityId, WeaponId.Kalis, ShieldId.None)
                    .ShieldRole);
        }
    }

    [Fact]
    public void Create_IsIndependentOfPresentationContext()
    {
        const ulong entityId = 73;

        var first = PawnAppearanceFactory.Create(entityId, WeaponId.Itak, ShieldId.None);
        var second = PawnAppearanceFactory.Create(entityId, WeaponId.Itak, ShieldId.None);
        var third = PawnAppearanceFactory.Create(entityId, WeaponId.Itak, ShieldId.None);

        Assert.Equal(first, second);
        Assert.Equal(second, third);
    }

    // --- VIS-010: weapon-tint stream (weapon-visuals-design.md, R-W1.3) ---

    [Fact]
    public void Create_ReturnsIdenticalWeaponTintForStableEntityIdAndWeapon()
    {
        var first = PawnAppearanceFactory.Create(51, WeaponId.Kalis, ShieldId.None);
        var second = PawnAppearanceFactory.Create(51, WeaponId.Kalis, ShieldId.None);

        Assert.Equal(first.WeaponTintId, second.WeaponTintId);
        Assert.Equal(first.WeaponBladeColor, second.WeaponBladeColor);
        Assert.Equal(first.WeaponGripColor, second.WeaponGripColor);
    }

    [Fact]
    public void Create_KalisTintSelectionNeverChangesWeaponRoleAcrossEntityIds()
    {
        // The tint stream (WeaponTintSalt) and the role itself are entirely
        // independent: no matter which of the two Kalis tints an entity ID
        // happens to select, the drawn silhouette identity — WeaponRole,
        // and therefore the geometry PawnGeometry.CreateWeaponLayout and
        // PawnRenderer.DrawWeapon key off — never moves. This is the
        // acceptance criterion "no variant stream ever changes which
        // weapon silhouette is drawn" made concrete.
        for (ulong entityId = 0; entityId < 200; entityId++)
        {
            var appearance = PawnAppearanceFactory.Create(entityId, WeaponId.Kalis, ShieldId.None);

            Assert.Equal(PawnWeaponRole.Kalis, appearance.WeaponRole);
        }
    }

    [Fact]
    public void Create_KalisWeaponTintIdIsAlwaysOneOfTheTwoCatalogedTints()
    {
        string[] allowedIds =
        [
            WeaponVisualCatalog.KalisTintFreshIron.Catalog.Id,
            WeaponVisualCatalog.KalisTintDarkHilt.Catalog.Id,
        ];

        for (ulong entityId = 0; entityId < 200; entityId++)
        {
            var appearance = PawnAppearanceFactory.Create(entityId, WeaponId.Kalis, ShieldId.None);

            Assert.Contains(appearance.WeaponTintId, allowedIds);
        }
    }

    [Fact]
    public void Create_KalisTintNeverResolvesToEitherInspectorOnlySilhouette()
    {
        // l2 and l3 are inspector/armory-card-only catalog entries
        // (R-W1.4); they must never surface through the resolved tint
        // identifier a live pawn carries.
        string[] excludedIds =
        [
            WeaponVisualCatalog.KalisL2.Catalog.Id,
            WeaponVisualCatalog.KalisL3.Catalog.Id,
        ];

        for (ulong entityId = 0; entityId < 200; entityId++)
        {
            var appearance = PawnAppearanceFactory.Create(entityId, WeaponId.Kalis, ShieldId.None);

            Assert.DoesNotContain(appearance.WeaponTintId, excludedIds);
        }
    }

    [Fact]
    public void Create_KalisWeaponBladeColorIsAlwaysIronBlueBlackRegardlessOfTint()
    {
        // Both Kalis tints share the exact same blade color — only the grip
        // differs — which is what "drawn blade geometry identical to today
        // for both tints (only color differs)" and the R-X.3 classification
        // invariance both require in this specific catalog.
        for (ulong entityId = 0; entityId < 200; entityId++)
        {
            var appearance = PawnAppearanceFactory.Create(entityId, WeaponId.Kalis, ShieldId.None);

            Assert.Equal(DyePalette.IronBlueBlack, appearance.WeaponBladeColor);
        }
    }

    [Fact]
    public void Create_KalisWeaponTintSelectsBothTintsAcrossManyEntityIds()
    {
        // Not a distribution test — just proof the stream is not
        // accidentally constant. Two hundred entity IDs is comfortably
        // enough to observe both grip colors from a fair modulo-2 split.
        var grips = new HashSet<Color>();
        for (ulong entityId = 0; entityId < 200; entityId++)
        {
            grips.Add(PawnAppearanceFactory.Create(entityId, WeaponId.Kalis, ShieldId.None).WeaponGripColor);
        }

        Assert.Equal(2, grips.Count);
    }

    // --- VIS-011: Kampilan, Wasay, and Itak now ship their own tint
    // catalogs too; Create must resolve each to its own weapon's tints, not
    // to ModelCategoryDefaultTint (which is unreachable for any defined
    // PawnWeaponRole as of this task). ---

    [Theory]
    [InlineData(WeaponId.Kampilan)]
    [InlineData(WeaponId.Wasay)]
    [InlineData(WeaponId.Itak)]
    public void Create_NeverResolvesToTheModelCategoryDefaultTintForAnyWeaponAsOfVIS011(
        WeaponId weapon)
    {
        for (ulong entityId = 0; entityId < 50; entityId++)
        {
            var appearance = PawnAppearanceFactory.Create(entityId, weapon, ShieldId.None);

            Assert.NotEqual(
                WeaponVisualCatalog.ModelCategoryDefaultTint.Catalog.Id,
                appearance.WeaponTintId);
        }
    }

    [Fact]
    public void Create_KampilanWeaponTintIdIsAlwaysOneOfTheThreeCatalogedTints()
    {
        string[] allowedIds =
        [
            WeaponVisualCatalog.KampilanTintFreshIron.Catalog.Id,
            WeaponVisualCatalog.KampilanTintWornIron.Catalog.Id,
            WeaponVisualCatalog.KampilanTintOchreHilt.Catalog.Id,
        ];

        for (ulong entityId = 0; entityId < 200; entityId++)
        {
            var appearance = PawnAppearanceFactory.Create(entityId, WeaponId.Kampilan, ShieldId.None);

            Assert.Contains(appearance.WeaponTintId, allowedIds);
            Assert.Equal(PawnWeaponRole.Kampilan, appearance.WeaponRole);
        }
    }

    [Fact]
    public void Create_KampilanTintNeverResolvesToTheInspectorOnlyK2()
    {
        for (ulong entityId = 0; entityId < 200; entityId++)
        {
            var appearance = PawnAppearanceFactory.Create(entityId, WeaponId.Kampilan, ShieldId.None);

            Assert.NotEqual(WeaponVisualCatalog.KampilanK2.Catalog.Id, appearance.WeaponTintId);
        }
    }

    [Fact]
    public void Create_WasayWeaponTintIdIsAlwaysOneOfTheThreeCatalogedTints()
    {
        string[] allowedIds =
        [
            WeaponVisualCatalog.WasayTintOchreHaft.Catalog.Id,
            WeaponVisualCatalog.WasayTintCharredHaft.Catalog.Id,
            WeaponVisualCatalog.WasayTintLashedWorn.Catalog.Id,
        ];

        for (ulong entityId = 0; entityId < 200; entityId++)
        {
            var appearance = PawnAppearanceFactory.Create(entityId, WeaponId.Wasay, ShieldId.None);

            Assert.Contains(appearance.WeaponTintId, allowedIds);
            Assert.Equal(PawnWeaponRole.Wasay, appearance.WeaponRole);
        }
    }

    [Fact]
    public void Create_ItakWeaponTintIdIsAlwaysOneOfTheTwoCatalogedTints()
    {
        string[] allowedIds =
        [
            WeaponVisualCatalog.ItakTintPlainOchre.Catalog.Id,
            WeaponVisualCatalog.ItakTintWornField.Catalog.Id,
        ];

        for (ulong entityId = 0; entityId < 200; entityId++)
        {
            var appearance = PawnAppearanceFactory.Create(entityId, WeaponId.Itak, ShieldId.None);

            Assert.Contains(appearance.WeaponTintId, allowedIds);
            Assert.Equal(PawnWeaponRole.Itak, appearance.WeaponRole);
        }
    }

    // --- VIS-011: the Wasay lashing band accent field ---

    [Fact]
    public void Create_ReturnsIdenticalWeaponLashingBandColorForStableEntityIdAndWeapon()
    {
        var first = PawnAppearanceFactory.Create(51, WeaponId.Wasay, ShieldId.None);
        var second = PawnAppearanceFactory.Create(51, WeaponId.Wasay, ShieldId.None);

        Assert.Equal(first.WeaponLashingBandColor, second.WeaponLashingBandColor);
    }

    [Fact]
    public void Create_WasayWeaponLashingBandColorIsOnlyEverNullOrRattanLashingTone()
    {
        for (ulong entityId = 0; entityId < 200; entityId++)
        {
            var appearance = PawnAppearanceFactory.Create(entityId, WeaponId.Wasay, ShieldId.None);

            Assert.True(
                appearance.WeaponLashingBandColor is null ||
                appearance.WeaponLashingBandColor.Value.Equals(WeaponVisualCatalog.RattanLashingTone));
        }
    }

    [Fact]
    public void Create_WasayWeaponLashingBandColorIsSetWheneverTheLashedWornTintIsSelected()
    {
        var observedBoth = false;
        for (ulong entityId = 0; entityId < 200; entityId++)
        {
            var appearance = PawnAppearanceFactory.Create(entityId, WeaponId.Wasay, ShieldId.None);
            var isLashedWorn =
                appearance.WeaponTintId == WeaponVisualCatalog.WasayTintLashedWorn.Catalog.Id;
            var carriesLashingBandColor =
                appearance.WeaponLashingBandColor is not null &&
                appearance.WeaponLashingBandColor.Value.Equals(WeaponVisualCatalog.RattanLashingTone);

            Assert.Equal(isLashedWorn, carriesLashingBandColor);

            observedBoth |= isLashedWorn;
        }

        // Proof the loop actually exercised the lashedWorn branch at least
        // once across 200 entity IDs from a fair modulo-3 split, not just
        // that the implication above vacuously held.
        Assert.True(observedBoth);
    }

    [Theory]
    [InlineData(WeaponId.Kampilan)]
    [InlineData(WeaponId.Kalis)]
    [InlineData(WeaponId.Itak)]
    public void Create_WeaponLashingBandColorIsAlwaysNullForWeaponsWithNoAccentTint(
        WeaponId weapon)
    {
        for (ulong entityId = 0; entityId < 100; entityId++)
        {
            var appearance = PawnAppearanceFactory.Create(entityId, weapon, ShieldId.None);

            Assert.Null(appearance.WeaponLashingBandColor);
        }
    }

    // --- VIS-013: shield-skin stream (shield-visuals-design.md, R-W2.3) ---

    [Fact]
    public void Create_ReturnsIdenticalShieldSkinForStableEntityIdAndShield()
    {
        var first = PawnAppearanceFactory.Create(51, WeaponId.Kalis, ShieldId.TallHardwood);
        var second = PawnAppearanceFactory.Create(51, WeaponId.Kalis, ShieldId.TallHardwood);

        Assert.Equal(first.ShieldSkinId, second.ShieldSkinId);
        Assert.Equal(first.ShieldFaceColor, second.ShieldFaceColor);
    }

    [Fact]
    public void Create_ShieldSkinSelectionNeverChangesShieldRoleAcrossEntityIds()
    {
        // The skin stream (ShieldSkinSalt) and shield presence itself are
        // entirely independent: no matter which skin an entity ID happens to
        // select, ShieldRole — and therefore whether PawnGeometry draws a
        // shield block at all — never moves.
        for (ulong entityId = 0; entityId < 200; entityId++)
        {
            var appearance = PawnAppearanceFactory.Create(entityId, WeaponId.Kalis, ShieldId.TallHardwood);

            Assert.Equal(PawnShieldRole.TallHardwood, appearance.ShieldRole);
            Assert.True(appearance.CarriesShield);
        }
    }

    [Fact]
    public void Create_TallHardwoodShieldSkinIdIsAlwaysOneOfTheFourCatalogedSkins()
    {
        // The stream's modulus grew from 1 to 4 in VIS-014 (OD-10), exactly
        // as this test's own predecessor comment anticipated. See
        // ShieldVisualCatalogTests.SelectSkin_ForTallHardwoodAlwaysReturnsOneOfTheFourCatalogedSkins
        // for the equivalent pin directly against ShieldVisualCatalog.
        string[] allowed =
        [
            ShieldVisualCatalog.MactanThin.Catalog.Id,
            ShieldVisualCatalog.MorgaFullBody.Catalog.Id,
            ShieldVisualCatalog.BoxerCagayan.Catalog.Id,
            ShieldVisualCatalog.VisayanKalasag.Catalog.Id,
        ];

        for (ulong entityId = 0; entityId < 200; entityId++)
        {
            var appearance = PawnAppearanceFactory.Create(entityId, WeaponId.Kalis, ShieldId.TallHardwood);

            Assert.Contains(appearance.ShieldSkinId, allowed);
        }
    }

    [Fact]
    public void Create_TallHardwoodShieldFaceColorIsAlwaysOneOfTheFourCatalogedSkinColors()
    {
        Color[] allowed =
        [
            ShieldVisualCatalog.PalmWoodPale,
            ShieldVisualCatalog.LightHardwoodTan,
            WeaponVisualCatalog.CharredWoodBrown,
            ShieldVisualCatalog.ResinBrownTone,
        ];

        for (ulong entityId = 0; entityId < 200; entityId++)
        {
            var appearance = PawnAppearanceFactory.Create(entityId, WeaponId.Kalis, ShieldId.TallHardwood);

            Assert.Contains(appearance.ShieldFaceColor, allowed);
        }
    }

    [Fact]
    public void Create_UnshieldedWarriorStillResolvesADeterministicShieldSkinFieldThatIsNeverDrawn()
    {
        // PawnShieldRole.None warriors still carry a resolved
        // ShieldSkinId/ShieldFaceColor pair — resolution stays total, exactly
        // as the weapon tint stream stays total for uncataloged weapons —
        // but PawnRenderer.DrawShield never draws it because ShieldBounds is
        // empty for an unshielded warrior. This pins that Create never
        // throws for ShieldId.None.
        for (ulong entityId = 0; entityId < 50; entityId++)
        {
            var appearance = PawnAppearanceFactory.Create(entityId, WeaponId.Kalis, ShieldId.None);

            Assert.Equal(PawnShieldRole.None, appearance.ShieldRole);
            Assert.False(appearance.CarriesShield);
            Assert.NotEmpty(appearance.ShieldSkinId);
        }
    }

    // --- VIS-018: appearance-preset selection stream (warrior-appearance-design.md, R-W3.5) ---

    [Fact]
    public void Create_ReturnsIdenticalAppearancePresetForStableEntityId()
    {
        var first = PawnAppearanceFactory.Create(51, WeaponId.Kalis, ShieldId.None);
        var second = PawnAppearanceFactory.Create(51, WeaponId.Kalis, ShieldId.None);

        Assert.Equal(first.AppearancePresetId, second.AppearancePresetId);
        Assert.Equal(first.GarmentBaseTone, second.GarmentBaseTone);
    }

    [Fact]
    public void Create_AppearancePresetIdIsAlwaysOneOfTheFiveShippedLevyPresets()
    {
        string[] allowedIds =
        [
            AppearancePresets.Lev01.Catalog.Id,
            AppearancePresets.Lev02.Catalog.Id,
            AppearancePresets.Lev03.Catalog.Id,
            AppearancePresets.Lev04.Catalog.Id,
            AppearancePresets.Lev09.Catalog.Id,
        ];

        for (ulong entityId = 0; entityId < 200; entityId++)
        {
            var appearance = PawnAppearanceFactory.Create(entityId, WeaponId.Kalis, ShieldId.None);

            Assert.Contains(appearance.AppearancePresetId, allowedIds);
        }
    }

    [Fact]
    public void Create_AppearancePresetSelectionReachesMoreThanOneShippedPresetAcrossManyEntityIds()
    {
        // Not a distribution test — just proof the stream is not
        // accidentally constant, mirroring the weapon-tint and shield-skin
        // stream tests above.
        var ids = new HashSet<string>();
        for (ulong entityId = 0; entityId < 200; entityId++)
        {
            ids.Add(PawnAppearanceFactory.Create(entityId, WeaponId.Kalis, ShieldId.None).AppearancePresetId);
        }

        Assert.True(ids.Count > 1);
    }

    [Fact]
    public void Create_AppearancePresetSelectionIsStableAcrossDifferentWeaponsAndShields()
    {
        // Every preset shipped this milestone is loadout-compatible "Any",
        // so the loadout-filtered pool never shrinks across weapons or
        // shields for a fixed entity ID — the same preset resolves either
        // way. This is the "no loadout filter edge case" the plan records
        // for the milestone's own five presets, made concrete.
        const ulong entityId = 91;

        var kalisNoShield = PawnAppearanceFactory.Create(entityId, WeaponId.Kalis, ShieldId.None);
        var wasayWithShield = PawnAppearanceFactory.Create(entityId, WeaponId.Wasay, ShieldId.TallHardwood);
        var itakNoShield = PawnAppearanceFactory.Create(entityId, WeaponId.Itak, ShieldId.None);

        Assert.Equal(kalisNoShield.AppearancePresetId, wasayWithShield.AppearancePresetId);
        Assert.Equal(kalisNoShield.AppearancePresetId, itakNoShield.AppearancePresetId);
    }

    [Fact]
    public void Create_GarmentBaseToneAlwaysEqualsSkinColorForEveryShippedLevyPreset()
    {
        // Every shipped preset recipes D1 (Bare-Chested), which renders as
        // the base skin-tone torso fill — see PawnAppearanceFactory's own
        // remarks at the ResolveGarmentBaseTone call site.
        for (ulong entityId = 0; entityId < 100; entityId++)
        {
            var appearance = PawnAppearanceFactory.Create(entityId, WeaponId.Kalis, ShieldId.None);

            Assert.Equal(appearance.SkinColor, appearance.GarmentBaseTone);
        }
    }

    [Fact]
    public void Create_FactionIdAndScenarioSeedDefaultToMatchTheThreeArgumentOverload()
    {
        var withoutBlockInputs = PawnAppearanceFactory.Create(33, WeaponId.Kampilan, ShieldId.None);
        var withExplicitDefaults = PawnAppearanceFactory.Create(
            33,
            WeaponId.Kampilan,
            ShieldId.None,
            factionId: 0,
            scenarioSeed: 0);

        Assert.Equal(withoutBlockInputs, withExplicitDefaults);
    }

    [Fact]
    public void Create_ExistingThreeTraitFieldsAreUnaffectedByFactionIdOrScenarioSeed()
    {
        // The appearance-preset streams read factionId/scenarioSeed through
        // their own new salts only; every field the original three-trait
        // rolls (PawnBodySalt/PawnClothingSalt/PawnDetailSalt) produce is
        // computed identically no matter what block-assignment inputs are
        // supplied (R-W6.2 salt independence).
        const ulong entityId = 12;

        var baseline = PawnAppearanceFactory.Create(entityId, WeaponId.Kalis, ShieldId.None);
        var differentFaction = PawnAppearanceFactory.Create(
            entityId, WeaponId.Kalis, ShieldId.None, factionId: 7, scenarioSeed: 0);
        var differentSeed = PawnAppearanceFactory.Create(
            entityId, WeaponId.Kalis, ShieldId.None, factionId: 0, scenarioSeed: 999999);
        var differentBoth = PawnAppearanceFactory.Create(
            entityId, WeaponId.Kalis, ShieldId.None, factionId: 3, scenarioSeed: 424242);

        foreach (var appearance in new[] { differentFaction, differentSeed, differentBoth })
        {
            Assert.Equal(baseline.StatureMultiplier, appearance.StatureMultiplier);
            Assert.Equal(baseline.BuildMultiplier, appearance.BuildMultiplier);
            Assert.Equal(baseline.HeadTreatment, appearance.HeadTreatment);
            Assert.Equal(baseline.ClothingColor, appearance.ClothingColor);
            Assert.Equal(baseline.AccentColor, appearance.AccentColor);
            Assert.Equal(baseline.SkinColor, appearance.SkinColor);
            Assert.Equal(baseline.HeadTreatmentColor, appearance.HeadTreatmentColor);
        }
    }

    [Fact]
    public void Create_AppearanceBlockAssignmentIsAlwaysUnscopedGenericThisMilestone()
    {
        // The block-assignment table has exactly one entry this milestone
        // (VIS-018) — degenerate but real, per warrior-appearance-design.md.
        // Every shipped preset's own Block/Catalog.ScopeTag therefore reads
        // Unscoped-generic no matter which faction or seed is supplied.
        for (var factionId = 0; factionId < 4; factionId++)
        {
            for (ulong seed = 0; seed < 20; seed++)
            {
                var appearance = PawnAppearanceFactory.Create(
                    7, WeaponId.Kalis, ShieldId.None, factionId, seed);

                Assert.Contains(
                    appearance.AppearancePresetId,
                    AppearancePresets.All.Select(preset => preset.Catalog.Id));
            }
        }
    }
}
