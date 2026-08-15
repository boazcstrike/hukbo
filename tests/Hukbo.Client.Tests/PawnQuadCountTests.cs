using Hukbo.Client.Presentation;
using Hukbo.Client.Presentation.Catalogs;
using Hukbo.Client.Rendering;
using Hukbo.Client.Settings;
using Hukbo.Core.Combat;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

/// <summary>
/// Pins <see cref="PawnQuadCount.Count"/> against
/// <c>PawnRenderer.Draw</c>'s real draw order (VIS-034, amendment A-1): every
/// expected value below was derived by walking that renderer method by
/// method, not by running the renderer itself (it needs a graphics device).
/// Any task that adds a primitive to <c>PawnRenderer</c> must update these
/// pins deliberately, in the same diff, with the budget arithmetic in the
/// commit message (the anti-density-creep rule).
/// </summary>
public sealed class PawnQuadCountTests
{
    /// <summary>
    /// Camera zooms matching <c>PawnGeometryTests</c>'s own convention for
    /// exercising all three detail tiers.
    /// </summary>
    private const float LowTierZoom = 0.05f;

    private const float MediumTierZoom = 1f;

    private const float HighTierZoom = 3f;

    [Fact]
    public void Count_PinsTheLowTierUnshieldedUnarmoredNormalPawn()
    {
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);
        var layout = PawnGeometry.Create(Vector2.Zero, LowTierZoom, appearance);

        Assert.Equal(PawnDetailTier.Low, layout.DetailTier);
        Assert.Equal(
            17,
            PawnQuadCount.Count(layout, appearance, PawnVisualState.Normal));
    }

    [Fact]
    public void Count_PinsTheMediumTierUnshieldedUnarmoredNormalPawn()
    {
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);
        var layout = PawnGeometry.Create(Vector2.Zero, MediumTierZoom, appearance);

        Assert.Equal(PawnDetailTier.Medium, layout.DetailTier);
        // movement-gait-animation T4: +4 over the pre-gait pin of 19 (a left
        // and a right leg quad, a left and a right foot quad — none of the
        // four rectangles is Rectangle.Empty at Medium tier).
        Assert.Equal(
            23,
            PawnQuadCount.Count(layout, appearance, PawnVisualState.Normal));
    }

    [Fact]
    public void Count_PinsTheHighTierUnshieldedUnarmoredNormalPawn()
    {
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);
        var layout = PawnGeometry.Create(Vector2.Zero, HighTierZoom, appearance);

        Assert.Equal(PawnDetailTier.High, layout.DetailTier);
        // movement-gait-animation T4: +4 over the pre-gait pin of 20, same
        // arithmetic as the Medium-tier pin above.
        Assert.Equal(
            24,
            PawnQuadCount.Count(layout, appearance, PawnVisualState.Normal));
    }

    /// <summary>
    /// RU-23: the three ranged weapon roles this package added
    /// (<c>PawnWeaponRole.Bangkaw</c>, <c>Busog</c>, <c>Arquebus</c>) at the
    /// same High-tier, unshielded, unarmored baseline as
    /// <see cref="Count_PinsTheHighTierUnshieldedUnarmoredNormalPawn"/>.
    /// Bangkaw and Arquebus draw no secondary rectangle
    /// (<c>PawnGeometry.CreateSecondaryBounds</c>'s catch-all arm) and their
    /// <c>DrawWeapon</c> arm is a single <c>DrawBlade</c> call like every
    /// melee role, so both match the 24-quad baseline exactly. Busog draws
    /// its nocked arrow through the same <c>SecondaryEquipmentBounds</c> slot
    /// the Wasay's axe head already occupies (RU-22's one new rectangle, +1),
    /// and RU-42 gave its <c>DrawWeapon</c> arm a second call —
    /// <c>DrawBowstring</c>, two stroked segments so the string can bend with
    /// <c>RangedPose.DrawTension</c> — over the single <c>DrawBlade</c> call
    /// every other role uses (+2). Busog therefore pins three quads higher
    /// than the baseline, at 27, superseding the RU-23 plan row's 25 (which
    /// predates RU-42's bowstring).
    /// </summary>
    [Theory]
    [InlineData(WeaponId.Bangkaw, 24)]
    [InlineData(WeaponId.Busog, 27)]
    [InlineData(WeaponId.Arquebus, 24)]
    public void Count_PinsTheHighTierUnshieldedUnarmoredRangedPawn(
        WeaponId weaponId,
        int expectedQuads)
    {
        var appearance = PawnAppearanceFactory.Create(0, weaponId, ShieldId.None);
        var layout = PawnGeometry.Create(Vector2.Zero, HighTierZoom, appearance);

        Assert.Equal(PawnDetailTier.High, layout.DetailTier);
        Assert.Equal(
            expectedQuads,
            PawnQuadCount.Count(layout, appearance, PawnVisualState.Normal));
    }

    /// <summary>
    /// movement-gait-animation design section 9: a Low-tier layout produces
    /// empty leg and foot rectangles, so <c>PawnRenderer.DrawLegs</c> and
    /// <c>DrawFeet</c> submit nothing and the Low-tier pin above
    /// (unchanged at 17) is exactly the pre-gait count.
    /// </summary>
    [Fact]
    public void Count_LegsAndFeetContributeNothingAtLowTier()
    {
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);
        var layout = PawnGeometry.Create(Vector2.Zero, LowTierZoom, appearance);

        Assert.Equal(PawnDetailTier.Low, layout.DetailTier);
        Assert.Equal(Rectangle.Empty, layout.LeftLegBounds);
        Assert.Equal(Rectangle.Empty, layout.RightLegBounds);
        Assert.Equal(Rectangle.Empty, layout.LeftFootBounds);
        Assert.Equal(Rectangle.Empty, layout.RightFootBounds);
    }

    /// <summary>
    /// The High-tier worst case: every optional layer filled with its
    /// densest option at once — a Wasay with its rattan lashing band, a
    /// <c>boxerCagayan</c>-skinned shield (the only skin whose curved face
    /// adds two extra quads over the plain block), full armor widening, a
    /// sash, both adornment accent marks, and a selected-state outline. Not
    /// every one of these can occur together on a single loadout in the
    /// shipped game (armor/sash/adornments are VIS-023 layers with their own
    /// resolution rules); this is the counting seam's own worst-case ceiling,
    /// exercising every conditional branch <c>PawnQuadCount.Count</c> has at
    /// once, matching the design's "pin the worst case" instruction rather
    /// than one specific in-game appearance.
    /// </summary>
    [Fact]
    public void Count_PinsTheHighTierFullyLoadedSelectedPawn()
    {
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Wasay, ShieldId.TallHardwood)
            with
        {
            WeaponLashingBandColor = Color.White,
            ShieldSkinId = ShieldVisualCatalog.BoxerCagayan.Catalog.Id,
        };
        var layout = PawnGeometry.Create(
            Vector2.Zero,
            HighTierZoom,
            appearance,
            scaleMultiplier: 1f,
            swingPose: null,
            armorWidthFactor: AppearanceComponentCatalog.MaxArmorWidthFactor,
            hasSash: true,
            adornmentAccentMarkCount: AppearanceComponentCatalog.MaxAccentMarksPerPawn);

        Assert.Equal(PawnDetailTier.High, layout.DetailTier);
        Assert.False(layout.ArmorBounds.IsEmpty);
        Assert.False(layout.SashBounds.IsEmpty);
        Assert.False(layout.AdornmentAccentSecondaryBounds.IsEmpty);
        // movement-gait-animation T4: +4 over the pre-gait pin of 40, same
        // leg/foot arithmetic as the plain High-tier pin above — this High
        // Selected layout also carries four non-empty leg/foot rectangles.
        // 2026-08-11: +1 more, from 44, because DrawArmor now fills two flank
        // bars in place of one torso-covering slab
        // (the armor bulk, adornment accents, and trample legibility design,
        // section 2). 2026-08-13: +4 more, from 45, because each flank bar
        // now costs three quads at this High tier — the fill plus an
        // outer-edge outline column plus an inner-edge darkened column —
        // instead of one, per row 128's second failure ("not bulky enough").
        Assert.Equal(
            49,
            PawnQuadCount.Count(layout, appearance, PawnVisualState.Selected));
    }

    /// <summary>
    /// <c>PawnRenderer.DrawSelectionMark</c> draws four corners of two
    /// rectangles each — a fixed +8 over the same layout's normal-state
    /// count, independent of every other conditional in
    /// <see cref="PawnQuadCount.Count"/>. A differential assertion rather
    /// than a second absolute pin, so it stays correct even if some other
    /// element's count changes.
    /// </summary>
    [Theory]
    [InlineData(nameof(PawnVisualState.Hovered))]
    [InlineData(nameof(PawnVisualState.Selected))]
    public void Count_AddsExactlyEightForTheSelectionMark(string stateName)
    {
        var state = Enum.Parse<PawnVisualState>(stateName);
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Kalis, ShieldId.None);
        var layout = PawnGeometry.Create(Vector2.Zero, HighTierZoom, appearance);

        var normal = PawnQuadCount.Count(layout, appearance, PawnVisualState.Normal);
        var marked = PawnQuadCount.Count(layout, appearance, state);

        Assert.Equal(normal + 8, marked);
    }

    /// <summary>
    /// <c>PawnRenderer.DrawDeadMark</c> draws two crossed lines — a fixed +2
    /// over the same layout's normal-state count — but only at
    /// <c>PawnDetailTier.Low</c> since the 2026-08-14 death collapse, where
    /// the prone silhouette that now carries the read is not resolvable.
    /// </summary>
    [Fact]
    public void Count_AddsExactlyTwoForTheDeadMarkAtLowTier()
    {
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Kalis, ShieldId.None);
        var layout = PawnGeometry.Create(Vector2.Zero, LowTierZoom, appearance);

        Assert.Equal(PawnDetailTier.Low, layout.DetailTier);

        var normal = PawnQuadCount.Count(layout, appearance, PawnVisualState.Normal);
        var dead = PawnQuadCount.Count(layout, appearance, PawnVisualState.Dead);

        Assert.Equal(normal + 2, dead);
    }

    /// <summary>
    /// The other side of the same rule: at Medium and High a corpse costs
    /// exactly what the same warrior cost alive. The collapse itself is free —
    /// a rotated quad is one quad — so this is not merely "two fewer than
    /// before", it is level with the living pawn.
    /// </summary>
    [Theory]
    [InlineData(MediumTierZoom)]
    [InlineData(HighTierZoom)]
    public void Count_AddsNothingForADeadPawnAboveLowTier(float zoom)
    {
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Kalis, ShieldId.None);
        var layout = PawnGeometry.Create(Vector2.Zero, zoom, appearance);

        Assert.NotEqual(PawnDetailTier.Low, layout.DetailTier);

        var normal = PawnQuadCount.Count(layout, appearance, PawnVisualState.Normal);
        var dead = PawnQuadCount.Count(layout, appearance, PawnVisualState.Dead);

        Assert.Equal(normal, dead);
    }

    /// <summary>
    /// A collapsing body submits exactly as many quads as the same body
    /// standing. The rotation reaches the layout as a transform rather than as
    /// geometry, so no rectangle on the layout moves and
    /// <c>SubmissionCount</c> is blind to it by construction — this pins that
    /// claim rather than leaving it to the reader.
    /// </summary>
    [Fact]
    public void Count_IsUnchangedByTheCollapseRotation()
    {
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Kalis, ShieldId.None);
        var upright = PawnGeometry.Create(Vector2.Zero, HighTierZoom, appearance);
        var fallen = PawnGeometry.Create(
            Vector2.Zero,
            HighTierZoom,
            appearance,
            collapseRotationRadians: CollapsePose.ProneRotationRadians);

        Assert.False(fallen.Collapse.IsIdentity);
        Assert.Equal(
            PawnQuadCount.Count(upright, appearance, PawnVisualState.Dead),
            PawnQuadCount.Count(fallen, appearance, PawnVisualState.Dead));
    }

    /// <summary>
    /// Every weapon role draws through the same three-line
    /// <c>DrawBlade</c> call — geometry differs by role, quad count does
    /// not. Restricted to the two roles whose
    /// <c>SecondaryEquipmentBounds</c> is empty at every tier (Kampilan and
    /// Kalis; Wasay and Itak each add their own secondary-equipment quads,
    /// covered separately above), and to the same entity ID for both calls,
    /// so stature/build/head-treatment — themselves entity-derived, not
    /// weapon-derived — cannot be the reason two totals match.
    /// </summary>
    [Fact]
    public void Count_TheWeaponAlwaysContributesTheSameQuadsRegardlessOfRole()
    {
        var kampilan = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);
        var kalis = PawnAppearanceFactory.Create(0, WeaponId.Kalis, ShieldId.None);
        var kampilanLayout = PawnGeometry.Create(Vector2.Zero, HighTierZoom, kampilan);
        var kalisLayout = PawnGeometry.Create(Vector2.Zero, HighTierZoom, kalis);

        Assert.True(kampilanLayout.SecondaryEquipmentBounds.IsEmpty);
        Assert.True(kalisLayout.SecondaryEquipmentBounds.IsEmpty);
        Assert.Equal(
            PawnQuadCount.Count(kampilanLayout, kampilan, PawnVisualState.Normal),
            PawnQuadCount.Count(kalisLayout, kalis, PawnVisualState.Normal));
    }

    /// <summary>
    /// <c>PawnRenderer.DrawLeaderMark</c> (leader rank plan L4, wired through
    /// <c>PawnRenderer.Draw</c>'s <c>isLeader</c> branch): a filled base band
    /// plus two stroked rising arms, per <c>GetLeaderMarkGlyph</c>'s own doc
    /// comment — "The three quads <c>DrawLeaderMark</c> submits" — a fixed +3
    /// over the same layout's non-leader count, at every detail tier, since
    /// the leader mark has no tier gate of its own. A differential assertion,
    /// exercised at all three tiers, rather than a fourth absolute pin, so it
    /// stays correct even if some other element's count changes.
    /// </summary>
    [Theory]
    [InlineData(LowTierZoom)]
    [InlineData(MediumTierZoom)]
    [InlineData(HighTierZoom)]
    public void Count_AddsExactlyThreeForTheLeaderMark(float zoom)
    {
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);
        var layout = PawnGeometry.Create(Vector2.Zero, zoom, appearance);

        var nonLeader = PawnQuadCount.Count(
            layout,
            appearance,
            PawnVisualState.Normal,
            isLeader: false);
        var leader = PawnQuadCount.Count(
            layout,
            appearance,
            PawnVisualState.Normal,
            isLeader: true);

        Assert.Equal(nonLeader + 3, leader);
    }

    [Fact]
    public void Count_TorsoPlaceholderDrawsExactlyOneQuadInsteadOfTheTorsoCapsule()
    {
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);
        var layout = PawnGeometry.Create(Vector2.Zero, HighTierZoom, appearance);

        var normalTorso = PawnQuadCount.Count(
            layout,
            appearance,
            PawnVisualState.Normal,
            VisualFallbackStep.ModelCategoryDefault);
        var placeholderTorso = PawnQuadCount.Count(
            layout,
            appearance,
            PawnVisualState.Normal,
            VisualFallbackStep.DiagnosticPlaceholder);

        Assert.False(layout.PlaceholderBounds.IsEmpty);
        // The placeholder (1 quad) replaces the torso capsule (7 quads at
        // High tier: 3 outline + 3 fill + 1 belt), a difference of 6.
        Assert.Equal(normalTorso - 6, placeholderTorso);
    }

    /// <summary>
    /// The design's active-pawn ceiling, counted rather than asserted: an
    /// attack adds at most four arm quads to a Medium or High pawn, and none
    /// at all at Low, where the arms are not drawn.
    /// </summary>
    [Theory]
    [InlineData(WeaponId.Kampilan, ShieldId.None)]
    [InlineData(WeaponId.Wasay, ShieldId.None)]
    [InlineData(WeaponId.Kalis, ShieldId.None)]
    [InlineData(WeaponId.Kalis, ShieldId.TallHardwood)]
    [InlineData(WeaponId.Itak, ShieldId.None)]
    [InlineData(WeaponId.Itak, ShieldId.TallHardwood)]
    public void Count_ArmsAddAtMostFourQuadsAndNoneAtLowTier(
        WeaponId weapon,
        ShieldId shield)
    {
        var appearance = PawnAppearanceFactory.Create(11, weapon, shield);
        var pose = AttackPoseResolver.Resolve(
            AttackGeometryTests.Animation(weapon, shield: shield));

        foreach (var zoom in new[] { 0.2f, 1.0f, 1.6f })
        {
            var prefix = PawnGeometry.PoseBlindPrefix.Create(
                new Vector2(400.5f, 300.5f),
                zoom,
                appearance);
            var neutral = prefix.CompleteAttackPosedLayout(attackPose: null);
            var posed = prefix.CompleteAttackPosedLayout(pose);

            var armQuads =
                PawnQuadCount.Count(posed, appearance, PawnVisualState.Normal) -
                PawnQuadCount.Count(neutral, appearance, PawnVisualState.Normal) -
                (posed.SwingTrail.IsEmpty ? 0 : PawnQuadCount.SwingTrailSegments);

            if (posed.DetailTier == PawnDetailTier.Low)
            {
                Assert.Equal(0, armQuads);
                continue;
            }

            Assert.InRange(armQuads, 2, 4);
        }
    }

    /// <summary>
    /// The 2026-08-15 weapon sprite design (task 19):
    /// <see cref="WeaponVisualStyle.Sprite"/> replaces every role's
    /// three-line procedural weapon (five for a Busog) with one textured
    /// quad at Medium tier and above, a fixed reduction of exactly two quads
    /// regardless of role — the Busog's bowstring stays procedural in both
    /// modes, so its own two segments cancel out of the delta.
    /// </summary>
    [Theory]
    [InlineData(WeaponId.Kampilan, MediumTierZoom)]
    [InlineData(WeaponId.Kampilan, HighTierZoom)]
    [InlineData(WeaponId.Wasay, MediumTierZoom)]
    [InlineData(WeaponId.Wasay, HighTierZoom)]
    [InlineData(WeaponId.Kalis, MediumTierZoom)]
    [InlineData(WeaponId.Kalis, HighTierZoom)]
    [InlineData(WeaponId.Itak, MediumTierZoom)]
    [InlineData(WeaponId.Itak, HighTierZoom)]
    [InlineData(WeaponId.Bangkaw, MediumTierZoom)]
    [InlineData(WeaponId.Bangkaw, HighTierZoom)]
    [InlineData(WeaponId.Busog, MediumTierZoom)]
    [InlineData(WeaponId.Busog, HighTierZoom)]
    [InlineData(WeaponId.Arquebus, MediumTierZoom)]
    [InlineData(WeaponId.Arquebus, HighTierZoom)]
    public void Count_SpriteModeReducesTheWeaponByExactlyTwoQuadsAtMediumAndHighTier(
        WeaponId weaponId,
        float zoom)
    {
        var appearance = PawnAppearanceFactory.Create(0, weaponId, ShieldId.None);
        var layout = PawnGeometry.Create(Vector2.Zero, zoom, appearance);

        Assert.NotEqual(PawnDetailTier.Low, layout.DetailTier);

        var procedural = PawnQuadCount.Count(
            layout,
            appearance,
            PawnVisualState.Normal,
            weaponVisualStyle: WeaponVisualStyle.Procedural);
        var sprite = PawnQuadCount.Count(
            layout,
            appearance,
            PawnVisualState.Normal,
            weaponVisualStyle: WeaponVisualStyle.Sprite);

        Assert.Equal(procedural - 2, sprite);
    }

    /// <summary>
    /// Design section 10: <c>DrawWeapon</c> never draws from the atlas below
    /// Medium tier, so <see cref="WeaponVisualStyle.Sprite"/> must leave the
    /// Low-tier count exactly as the procedural path already produced it —
    /// this is what keeps <c>Count_PinsTheLowTierUnshieldedUnarmoredNormalPawn</c>
    /// true regardless of which style a caller passes.
    /// </summary>
    [Theory]
    [InlineData(WeaponId.Kampilan)]
    [InlineData(WeaponId.Wasay)]
    [InlineData(WeaponId.Kalis)]
    [InlineData(WeaponId.Itak)]
    [InlineData(WeaponId.Bangkaw)]
    [InlineData(WeaponId.Busog)]
    [InlineData(WeaponId.Arquebus)]
    public void Count_SpriteModeDoesNotChangeTheWeaponAtLowTier(WeaponId weaponId)
    {
        var appearance = PawnAppearanceFactory.Create(0, weaponId, ShieldId.None);
        var layout = PawnGeometry.Create(Vector2.Zero, LowTierZoom, appearance);

        Assert.Equal(PawnDetailTier.Low, layout.DetailTier);

        var procedural = PawnQuadCount.Count(
            layout,
            appearance,
            PawnVisualState.Normal,
            weaponVisualStyle: WeaponVisualStyle.Procedural);
        var sprite = PawnQuadCount.Count(
            layout,
            appearance,
            PawnVisualState.Normal,
            weaponVisualStyle: WeaponVisualStyle.Sprite);

        Assert.Equal(procedural, sprite);
    }

    /// <summary>
    /// The 2026-08-15 weapon sprite design (task 18):
    /// <see cref="WeaponVisualStyle.Sprite"/> replaces the shield's
    /// skin-dependent procedural quads (one to six, per
    /// <see cref="PawnQuadCountTests.Count_PinsTheHighTierFullyLoadedSelectedPawn"/>'s
    /// own boxerCagayan worst case) with exactly one textured quad, at
    /// Medium tier and above, for both authored shield skins. Isolated by
    /// differencing a shielded pawn against the same pawn unshielded, under
    /// each style separately, so nothing about the weapon's own delta
    /// (covered above) leaks into the shield's.
    /// </summary>
    [Theory]
    [InlineData(MediumTierZoom)]
    [InlineData(HighTierZoom)]
    public void Count_SpriteModeReducesTheBoxerCagayanShieldAtMediumAndHighTier(float zoom)
    {
        var shielded = PawnAppearanceFactory.Create(0, WeaponId.Kalis, ShieldId.TallHardwood)
            with
        {
            ShieldSkinId = ShieldVisualCatalog.BoxerCagayan.Catalog.Id,
        };
        AssertShieldSpriteReduction(shielded, zoom);
    }

    [Theory]
    [InlineData(MediumTierZoom)]
    [InlineData(HighTierZoom)]
    public void Count_SpriteModeReducesTheVisayanKalasagShieldAtMediumAndHighTier(float zoom)
    {
        var shielded = PawnAppearanceFactory.Create(0, WeaponId.Kalis, ShieldId.TallHardwood)
            with
        {
            ShieldSkinId = ShieldVisualCatalog.VisayanKalasag.Catalog.Id,
        };
        AssertShieldSpriteReduction(shielded, zoom);
    }

    private static void AssertShieldSpriteReduction(PawnAppearance shielded, float zoom)
    {
        var unshielded = shielded with { ShieldRole = PawnShieldRole.None };
        var shieldedLayout = PawnGeometry.Create(Vector2.Zero, zoom, shielded);
        var unshieldedLayout = PawnGeometry.Create(Vector2.Zero, zoom, unshielded);

        Assert.NotEqual(PawnDetailTier.Low, shieldedLayout.DetailTier);
        Assert.False(shieldedLayout.ShieldBounds.IsEmpty);

        var proceduralShielded = PawnQuadCount.Count(
            shieldedLayout,
            shielded,
            PawnVisualState.Normal,
            weaponVisualStyle: WeaponVisualStyle.Procedural);
        var proceduralUnshielded = PawnQuadCount.Count(
            unshieldedLayout,
            unshielded,
            PawnVisualState.Normal,
            weaponVisualStyle: WeaponVisualStyle.Procedural);
        var spriteShielded = PawnQuadCount.Count(
            shieldedLayout,
            shielded,
            PawnVisualState.Normal,
            weaponVisualStyle: WeaponVisualStyle.Sprite);
        var spriteUnshielded = PawnQuadCount.Count(
            unshieldedLayout,
            unshielded,
            PawnVisualState.Normal,
            weaponVisualStyle: WeaponVisualStyle.Sprite);

        var proceduralShieldQuads = proceduralShielded - proceduralUnshielded;
        var spriteShieldQuads = spriteShielded - spriteUnshielded;

        Assert.Equal(1, spriteShieldQuads);
        Assert.True(
            spriteShieldQuads < proceduralShieldQuads,
            $"sprite shield ({spriteShieldQuads}) did not reduce the " +
            $"procedural shield ({proceduralShieldQuads}).");
    }

    /// <summary>
    /// Design section 10's shield-side counterpart to
    /// <see cref="Count_SpriteModeDoesNotChangeTheWeaponAtLowTier"/>:
    /// <c>DrawShield</c> never draws from the atlas below Medium tier either,
    /// so the Low-tier shield count is identical under both styles.
    /// </summary>
    [Theory]
    [InlineData(ShieldId.TallHardwood)]
    public void Count_SpriteModeDoesNotChangeTheShieldAtLowTier(ShieldId shieldId)
    {
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Kalis, shieldId)
            with
        {
            ShieldSkinId = ShieldVisualCatalog.BoxerCagayan.Catalog.Id,
        };
        var layout = PawnGeometry.Create(Vector2.Zero, LowTierZoom, appearance);

        Assert.Equal(PawnDetailTier.Low, layout.DetailTier);

        var procedural = PawnQuadCount.Count(
            layout,
            appearance,
            PawnVisualState.Normal,
            weaponVisualStyle: WeaponVisualStyle.Procedural);
        var sprite = PawnQuadCount.Count(
            layout,
            appearance,
            PawnVisualState.Normal,
            weaponVisualStyle: WeaponVisualStyle.Sprite);

        Assert.Equal(procedural, sprite);
    }
}
