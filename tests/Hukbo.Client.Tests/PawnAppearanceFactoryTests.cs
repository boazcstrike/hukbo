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
    public void Create_MapsAllSevenWeaponIdsToDistinctSilhouettes()
    {
        var weaponIds = Enum.GetValues<WeaponId>();
        var roles = weaponIds
            .Select(weapon => PawnAppearanceFactory
                .Create(1, weapon, ShieldId.None)
                .WeaponRole)
            .ToHashSet();

        Assert.Equal(7, weaponIds.Length);
        Assert.Equal(weaponIds.Length, roles.Count);
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
        // to just "Kampilan". The Imported Arquebus is the one weapon that
        // makes no cultural-name claim at all (PawnAppearance.WeaponLabel's
        // own remark: "not a cultural identification, so the pair-form rule
        // does not engage") — it is exempted by an explicit, counted
        // allow-list rather than by silently falling through the split
        // check, so a future weapon added without a dash still fails loud.
        string[] weaponsWithNoCulturalNameClaim = ["Imported Arquebus"];
        var exemptedCount = 0;

        foreach (var weapon in Enum.GetValues<WeaponId>())
        {
            var label = PawnAppearanceFactory
                .Create(0, weapon, ShieldId.None)
                .WeaponLabel;

            if (weaponsWithNoCulturalNameClaim.Contains(label))
            {
                exemptedCount++;
                continue;
            }

            var parts = label.Split(" — ");
            Assert.Equal(2, parts.Length);
            Assert.NotEmpty(parts[0]);
            Assert.NotEmpty(parts[1]);
        }

        Assert.Equal(weaponsWithNoCulturalNameClaim.Length, exemptedCount);
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
    public void Create_AppearancePresetIdIsAlwaysAShippedPresetCompatibleWithTheAssignedBlockAndWeapon()
    {
        // VIS-018 pinned this as "always one of the five shipped levy
        // presets" back when the block-assignment table had a single
        // (Unscoped-generic) entry. VIS-022 grew that table to four
        // entries, and factionId=0/scenarioSeed=0 (this overload's own
        // defaults) now resolve the Visayan block — see
        // AppearancePresets.SelectBlock. Kalis is never Wasay, so the pool
        // excludes VIS-14, the Visayan block's own Wasay-only row.
        var allowedIds = AppearancePresetsVisayan.All
            .Where(preset => preset.LoadoutCompatibility != AppearancePresetLoadoutCompatibility.WasayOnly)
            .Select(preset => preset.Catalog.Id)
            .ToArray();

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
    public void Create_AppearancePresetSelectionIsStableAcrossNonWasayWeaponsAndShields()
    {
        // GetCompatiblePresets filters its pool only on whether the weapon
        // is Wasay, so every non-Wasay weapon role (Kampilan, Kalis, Itak)
        // shares one pool for a fixed block and must resolve the same
        // preset for a fixed entity ID; shield never enters the filter at
        // all. VIS-018 additionally asserted a Wasay-armed pawn resolves
        // the very same preset — true only while every shipped preset was
        // loadout "Any". VIS-022's Visayan block ships VIS-14
        // (Wasay-only), so a Wasay pool can differ from the shared
        // non-Wasay pool; see the dedicated Wasay test below.
        const ulong entityId = 91;

        var kalisNoShield = PawnAppearanceFactory.Create(entityId, WeaponId.Kalis, ShieldId.None);
        var itakNoShield = PawnAppearanceFactory.Create(entityId, WeaponId.Itak, ShieldId.None);
        var kampilanWithShield = PawnAppearanceFactory.Create(entityId, WeaponId.Kampilan, ShieldId.TallHardwood);

        Assert.Equal(kalisNoShield.AppearancePresetId, itakNoShield.AppearancePresetId);
        Assert.Equal(kalisNoShield.AppearancePresetId, kampilanWithShield.AppearancePresetId);
    }

    [Fact]
    public void Create_WasayArmedAppearancePresetSelectionResolvesAPresetFromTheAssignedBlock()
    {
        // A Wasay-armed pawn's preset pool can differ from the shared
        // non-Wasay pool (see the test above) once a block ships a
        // Wasay-only row, as the Visayan block does with VIS-14. This pins
        // only that the resolved preset still belongs to the assigned
        // block (Visayan, for this overload's default factionId/scenarioSeed),
        // not that it matches the non-Wasay selection.
        const ulong entityId = 91;

        var wasayWithShield = PawnAppearanceFactory.Create(entityId, WeaponId.Wasay, ShieldId.TallHardwood);

        Assert.Contains(
            wasayWithShield.AppearancePresetId,
            AppearancePresetsVisayan.All.Select(preset => preset.Catalog.Id));
    }

    [Fact]
    public void Create_GarmentBaseToneAlwaysEqualsSkinColorUntilDyedTorsoGarmentsAreWiredIntoRendering()
    {
        // PawnAppearanceFactory.Create still hard-codes garmentBaseTone to
        // the pawn's own skin tone unconditionally (see its own remarks at
        // the call site) — VIS-020/021 shipped presets with dyed torso
        // garments (D2/D3/D4) in their catalogs, but wiring the render
        // pipeline to resolve an actual dye tone for those is a later,
        // separate integration task's scope, not VIS-018's or VIS-022's.
        // This still holds for every entity ID today, regardless of which
        // preset or block resolves, precisely because the resolution is
        // unconditional rather than preset-driven.
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
    public void Create_AppearancePresetIdAlwaysResolvesToAShippedPresetAcrossEveryFactionAndSeed()
    {
        // VIS-018 pinned this as "always Unscoped-generic" back when the
        // block-assignment table had a single entry. VIS-022 grew the
        // table to four entries (Visayan, Tagalog, Cagayan, Unscoped-
        // generic), so the resolved block now genuinely varies by faction
        // and seed — this walks a broad faction/seed sample and pins only
        // that whatever block-and-preset stream resolves, it always lands
        // on one of the 53 shipped presets across all four blocks, never
        // an undeclared or malformed identifier.
        for (var factionId = 0; factionId < 4; factionId++)
        {
            for (ulong seed = 0; seed < 20; seed++)
            {
                var appearance = PawnAppearanceFactory.Create(
                    7,
                    WeaponId.Kalis,
                    ShieldId.None,
                    factionId: factionId,
                    scenarioSeed: seed);

                Assert.Contains(
                    appearance.AppearancePresetId,
                    AppearancePresets.All.Select(preset => preset.Catalog.Id));
            }
        }
    }

    [Fact]
    public void Create_LeaderAndNonLeaderResolveDifferentPresetsInTheVisayanBlockButShareEveryOtherField() =>
        AssertLeaderAndNonLeaderResolveDifferentPresetsButShareEveryOtherField(VisualScopeTag.Visayan);

    [Fact]
    public void Create_LeaderAndNonLeaderResolveDifferentPresetsInTheTagalogBlockButShareEveryOtherField() =>
        AssertLeaderAndNonLeaderResolveDifferentPresetsButShareEveryOtherField(VisualScopeTag.Tagalog);

    // Not itself a [Theory]: VisualScopeTag is an internal enum, and a public
    // Theory method taking it as a parameter fails to build (CS0051,
    // inconsistent accessibility) the moment InlineData forces the parameter
    // itself public. The two Facts above are the accessible surface; this is
    // the shared body.
    private static void AssertLeaderAndNonLeaderResolveDifferentPresetsButShareEveryOtherField(
        VisualScopeTag block)
    {
        // leader-character-design.md section 4.1: the Visayan and Tagalog
        // blocks each ship at least one Status.Leader row (Vis15, Tag13,
        // Tag15) held in a pool disjoint from Status.General, so isLeader
        // must change AppearancePresetId (and, because every shipped
        // preset resolves to recipe D1 — Bare-Chested, GarmentBaseTone
        // follows skin only, so it is unaffected) while leaving every
        // field the entity-ID-only rolls above SelectPreset produce
        // untouched — stature, build, skin, clothing, accent, head
        // treatment, weapon tint, and shield skin all read entityId
        // directly and never isLeader.
        var (factionId, scenarioSeed) = FindFactionAndSeedForBlock(block);

        for (ulong entityId = 0; entityId < 50; entityId++)
        {
            var nonLeader = PawnAppearanceFactory.Create(
                entityId,
                WeaponId.Kalis,
                ShieldId.None,
                isLeader: false,
                factionId: factionId,
                scenarioSeed: scenarioSeed);
            var leader = PawnAppearanceFactory.Create(
                entityId,
                WeaponId.Kalis,
                ShieldId.None,
                isLeader: true,
                factionId: factionId,
                scenarioSeed: scenarioSeed);

            Assert.NotEqual(nonLeader.AppearancePresetId, leader.AppearancePresetId);

            Assert.Equal(nonLeader.StatureMultiplier, leader.StatureMultiplier);
            Assert.Equal(nonLeader.BuildMultiplier, leader.BuildMultiplier);
            Assert.Equal(nonLeader.HeadTreatment, leader.HeadTreatment);
            Assert.Equal(nonLeader.ClothingColor, leader.ClothingColor);
            Assert.Equal(nonLeader.AccentColor, leader.AccentColor);
            Assert.Equal(nonLeader.SkinColor, leader.SkinColor);
            Assert.Equal(nonLeader.HeadTreatmentColor, leader.HeadTreatmentColor);
            Assert.Equal(nonLeader.WeaponTintId, leader.WeaponTintId);
            Assert.Equal(nonLeader.WeaponBladeColor, leader.WeaponBladeColor);
            Assert.Equal(nonLeader.WeaponGripColor, leader.WeaponGripColor);
            Assert.Equal(nonLeader.WeaponLashingBandColor, leader.WeaponLashingBandColor);
            Assert.Equal(nonLeader.ShieldSkinId, leader.ShieldSkinId);
            Assert.Equal(nonLeader.ShieldFaceColor, leader.ShieldFaceColor);
        }
    }

    /// <summary>
    /// Walks the same faction/seed space
    /// <see cref="Create_AppearancePresetIdAlwaysResolvesToAShippedPresetAcrossEveryFactionAndSeed"/>
    /// already covers and returns the first pair <see cref="AppearancePresets.SelectBlock"/>
    /// resolves to <paramref name="block"/>, so this test never hard-codes a
    /// faction/seed pair against the block-assignment stream's own internal
    /// mix.
    /// </summary>
    private static (int FactionId, ulong ScenarioSeed) FindFactionAndSeedForBlock(
        VisualScopeTag block)
    {
        for (var factionId = 0; factionId < 4; factionId++)
        {
            for (ulong seed = 0; seed < 50; seed++)
            {
                if (AppearancePresets.SelectBlock(seed, factionId) == block)
                {
                    return (factionId, seed);
                }
            }
        }

        throw new InvalidOperationException(
            $"No (factionId, scenarioSeed) pair in the searched range resolves {block}.");
    }
}
