using Hukbo.Client.Presentation;
using Hukbo.Client.Presentation.Catalogs;
using Hukbo.Client.Rendering;
using Hukbo.Core.Combat;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

/// <summary>
/// GPU-015. Proves that <see cref="ConservativePawnCull"/>'s radius is a
/// genuine upper bound on <c>PawnRenderer.GetBounds</c> over every appearance
/// the catalogs can produce, and records what the resulting pre-cull actually
/// admits at each of the three camera stations.
/// </summary>
public sealed class ConservativePawnCullTests
{
    // Two of the three stations named in docs/development/testing.md's review
    // protocol: the camera's own clamp values (SpectatorCamera). The third,
    // default fit, is not a constant — it is whatever SpectatorCamera.Fit
    // resolves for the panel, so it is obtained by calling Fit rather than
    // written down.
    private const float MinimumZoom = 0.05f;
    private const float MaximumZoom = 12f;

    /// <summary>
    /// Stands in for the default-fit station in an <c>InlineData</c> row,
    /// which cannot call <c>SpectatorCamera.Fit</c> itself. Zero is safe as a
    /// sentinel because it is not a zoom the camera can ever hold: its own
    /// floor is <see cref="MinimumZoom"/>.
    /// </summary>
    private const float FitStationSentinelZoom = 0f;

    // Scenario's own default map, in world units.
    private const int MapWidth = 1_280;
    private const int MapHeight = 720;

    // The arena panel at the resolution the tracked Phase 1 render baseline
    // was captured at (docs/development/render-baselines/
    // render-matrix-2026-07-29.json, fingerprint 1920x1080). Derived from
    // ArenaGame.ComputeLayout's own constants, which are private: content top
    // is the 68-pixel status bar, content height is 1080 - 68 - 12, the event
    // column is 420 wide with a 12-pixel margin and a 10-pixel gap, and the
    // arena takes what is left after a 12-pixel left margin. That gives
    // (12, 68, 1466, 1000). Written out here because this file may not reach
    // into ArenaGame, and stated as a derivation rather than a measurement.
    private static readonly Rectangle BaselineArenaBounds =
        new(12, 68, 1_466, 1_000);

    /// <summary>
    /// Zooms that between them cover both clamp ends, all three detail-tier
    /// boundaries, and the three camera stations.
    /// </summary>
    private static readonly float[] ZoomSamples =
    [
        0f,
        MinimumZoom,
        0.4f,
        0.5333333f,   // apparent scale exactly at the 0.72 clamp floor
        0.6f,
        0.7037037f,   // apparent scale 0.95, the Low/Medium boundary
        0.8f,
        1.0078751f,   // the default-fit station at 1920x1080
        1.3333334f,   // apparent scale 1.80, the Medium/High boundary
        1.6f,
        1.7777778f,   // apparent scale exactly at the 2.40 clamp ceiling
        2.5f,
        MaximumZoom,
        100f,
    ];

    /// <summary>
    /// Foot anchors covering every quarter-pixel phase, plus one far from the
    /// origin so float precision at real screen coordinates is exercised.
    /// </summary>
    private static readonly Vector2[] AnchorSamples =
    [
        new(0f, 0f),
        new(0.5f, 0.5f),
        new(0.25f, 0.75f),
        new(960.3f, 540.8f),
    ];

    /// <summary>
    /// The optional <c>PawnGeometry.Create</c> layers the render path leaves
    /// at their no-op defaults today. Covered anyway, at both ends of every
    /// bound, so the radius stays valid if a later task starts passing them.
    /// </summary>
    private static readonly float[] ArmorWidthFactorSamples =
    [
        1f,
        1.09f,
        AppearanceComponentCatalog.MaxArmorWidthFactor,
    ];

    private static readonly bool[] SashSamples = [false, true];

    private static readonly int[] AccentMarkCountSamples =
    [
        0,
        1,
        AppearanceComponentCatalog.MaxAccentMarksPerPawn,
    ];

    [Fact]
    public void ApparentScale_MatchesPawnGeometry()
    {
        var appearance = PawnAppearanceFactory.Create(
            0,
            WeaponId.Kampilan,
            ShieldId.TallHardwood);

        foreach (var zoom in ZoomSamples)
        {
            var expected = PawnGeometry.Create(
                Vector2.Zero,
                zoom,
                appearance).ApparentScale;

            Assert.Equal(
                (double)expected,
                (double)ConservativePawnCull.ApparentScale(zoom),
                5);
        }
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(-0.001f)]
    public void ApparentScale_RejectsWhatPawnGeometryRejects(float zoom)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ConservativePawnCull.ApparentScale(zoom));
    }

    /// <summary>
    /// The axis lists this file's cross-product is built from are not assumed.
    /// This walks a wide span of entity IDs through
    /// <see cref="PawnAppearanceFactory"/> for every loadout and asserts that
    /// the distinct geometry-relevant appearances it can produce are exactly
    /// the ones <see cref="GeometryAppearances"/> enumerates.
    /// </summary>
    [Fact]
    public void GeometryAppearances_CoverEverythingTheFactoryCanProduce()
    {
        var produced = new HashSet<(PawnWeaponRole, PawnShieldRole, float, float, string)>();

        foreach (var weapon in Enum.GetValues<WeaponId>())
        {
            foreach (var shield in Enum.GetValues<ShieldId>())
            {
                for (ulong entityId = 0; entityId < 4_096; entityId++)
                {
                    var appearance = PawnAppearanceFactory.Create(
                        entityId,
                        weapon,
                        shield);
                    produced.Add((
                        appearance.WeaponRole,
                        appearance.ShieldRole,
                        appearance.StatureMultiplier,
                        appearance.BuildMultiplier,
                        appearance.ShieldSkinId));
                }
            }
        }

        // Every PawnWeaponRole — derived from the enum rather than
        // hardcoded, because a literal 4 here already rotted silently once,
        // the moment RU-35 grew the enum to seven members — times three
        // statures, times three builds, times the skin count every shield
        // role contributes. produced.Count comes from
        // PawnAppearanceFactory.Create, a source independent of the enum
        // itself, so a role the factory silently stopped producing would
        // still break this equality instead of vacuously satisfying it.
        //
        // The skin factor is summed from the catalog rather than written as
        // the literal (4 + 1) it used to be — four tall-hardwood skins plus
        // the single model-category default an unshielded warrior falls
        // through to. That literal rotted the moment the shield-size package
        // added PawnShieldRole.NarrowBreastHigh with a skin of its own, which
        // is the same failure mode the weapon factor above was converted to
        // avoid. A role with no declared skins still contributes one, because
        // SelectSkin falls through to ModelCategoryDefault for it.
        var shieldSkinTotal = Enum.GetValues<PawnShieldRole>()
            .Sum(role => Math.Max(ShieldVisualCatalog.GetSkins(role).Count, 1));

        Assert.Equal(
            Enum.GetValues<PawnWeaponRole>().Length * 3 * 3 * shieldSkinTotal,
            produced.Count);

        var enumerated = new HashSet<(PawnWeaponRole, PawnShieldRole, float, float, string)>(
            GeometryAppearances().Select(appearance => (
                appearance.WeaponRole,
                appearance.ShieldRole,
                appearance.StatureMultiplier,
                appearance.BuildMultiplier,
                appearance.ShieldSkinId)));

        Assert.Empty(produced.Except(enumerated));
    }

    /// <summary>
    /// The upper-bound proof. For every appearance the catalogs can produce,
    /// every optional layer setting, every zoom sample, and every sub-pixel
    /// anchor phase, the conservative rectangle contains the exact pose-blind
    /// visual bounds on all four sides.
    /// </summary>
    [Fact]
    public void Bounds_ContainEveryExactVisualBoundsTheCatalogsCanProduce()
    {
        var cases = 0;

        foreach (var appearance in GeometryAppearances())
        {
            foreach (var armorWidthFactor in ArmorWidthFactorSamples)
            {
                foreach (var hasSash in SashSamples)
                {
                    foreach (var accentMarkCount in AccentMarkCountSamples)
                    {
                        foreach (var zoom in ZoomSamples)
                        {
                            foreach (var anchor in AnchorSamples)
                            {
                                var exact = PawnGeometry.Create(
                                    anchor,
                                    zoom,
                                    appearance,
                                    scaleMultiplier: 1f,
                                    swingPose: null,
                                    armorWidthFactor,
                                    hasSash,
                                    accentMarkCount).VisualBounds;
                                var conservative = ConservativePawnCull.Bounds(
                                    anchor,
                                    zoom);

                                AssertContains(conservative, exact, appearance, zoom, anchor);
                                cases++;
                            }
                        }
                    }
                }
            }
        }

        // The 432 in the old literal was every PawnWeaponRole (hardcoded 4,
        // now derived from the enum for the same reason as above) x every
        // PawnShieldRole (hardcoded 2, now also derived from the enum —
        // RU-41, the identical rot RU-22 repaired for PawnWeaponRole) x 3
        // statures x 3 builds x the shield-skin count — GeometryAppearances'
        // own cross-product — times 3 armor factors x 2 sash states x 3
        // accent counts x 14 zooms x 4 anchors. Asserted so a silently
        // shrunken axis list cannot pass this test by covering less.
        //
        // The shield-skin factor was a literal 6 and rotted the third time
        // this file learned the same lesson, when the shield-size package
        // declared a skin for PawnShieldRole.NarrowBreastHigh. It now reads
        // the length of the very array GeometryAppearances sweeps.
        Assert.Equal(
            (Enum.GetValues<PawnWeaponRole>().Length *
                Enum.GetValues<PawnShieldRole>().Length * 3 * 3 *
                GeometryShieldSkinIds.Length) * 3 * 2 * 3 * 14 * 4,
            cases);
    }

    /// <summary>
    /// The cross-product above samples zoom at fourteen points. This sweeps it
    /// continuously across the whole scale-varying range, on the appearances
    /// that reach furthest in each direction, so a coefficient that is right
    /// at the sampled points and wrong between them cannot survive.
    /// </summary>
    [Fact]
    public void Bounds_ContainExactVisualBounds_AcrossAContinuousZoomSweep()
    {
        var extremes = GeometryAppearances()
            .Where(appearance =>
                appearance.StatureMultiplier > 1.05f &&
                appearance.BuildMultiplier > 1.15f)
            .ToArray();

        Assert.NotEmpty(extremes);

        for (var step = 0; step <= 400; step++)
        {
            var zoom = step * 0.01f;

            foreach (var appearance in extremes)
            {
                foreach (var anchor in AnchorSamples)
                {
                    var exact = PawnGeometry.Create(
                        anchor,
                        zoom,
                        appearance,
                        scaleMultiplier: 1f,
                        swingPose: null,
                        AppearanceComponentCatalog.MaxArmorWidthFactor,
                        hasSash: true,
                        AppearanceComponentCatalog.MaxAccentMarksPerPawn).VisualBounds;

                    AssertContains(
                        ConservativePawnCull.Bounds(anchor, zoom),
                        exact,
                        appearance,
                        zoom,
                        anchor);
                }
            }
        }
    }

    /// <summary>
    /// The other half of the upper-bound question: the radius is above every
    /// exact extent, but by how much? This measures the true worst-case
    /// extent over the whole cross-product at each of the three stations and
    /// pins the slack, so the bound cannot quietly become generous.
    /// </summary>
    [Theory]
    [InlineData(MinimumZoom)]
    [InlineData(1.0078751f)]
    [InlineData(MaximumZoom)]
    public void Radius_ExceedsTheWorstCaseExtentByAFlatFewPixels(float zoom)
    {
        var anchor = new Vector2(0.5f, 0.5f);
        var worst = 0f;

        foreach (var appearance in GeometryAppearances())
        {
            foreach (var armorWidthFactor in ArmorWidthFactorSamples)
            {
                var exact = PawnGeometry.Create(
                    anchor,
                    zoom,
                    appearance,
                    scaleMultiplier: 1f,
                    swingPose: null,
                    armorWidthFactor,
                    hasSash: true,
                    AppearanceComponentCatalog.MaxAccentMarksPerPawn).VisualBounds;

                worst = MathF.Max(worst, Extent(exact, anchor));
            }
        }

        // The posed half of the same question. Sixty-four headings rather than
        // the containment proof's sixteen, because this row is what pins the
        // coefficient and an under-sampled sweep would pin it too low.
        foreach (var weapon in Enum.GetValues<WeaponId>())
        {
            foreach (var shield in Enum.GetValues<ShieldId>())
            {
                var appearance = PawnAppearanceFactory.Create(0, weapon, shield);

                foreach (var resolution in Enum.GetValues<AttackResolution>())
                {
                    for (var step = 0; step < 64; step++)
                    {
                        var angle = step * (MathF.Tau / 64f);
                        var exact = PosedVisualBounds(
                            appearance,
                            weapon,
                            shield,
                            resolution,
                            MathF.Cos(angle),
                            MathF.Sin(angle),
                            anchor,
                            zoom);

                        worst = MathF.Max(worst, Extent(exact, anchor));
                    }
                }
            }
        }

        var slack = ConservativePawnCull.RadiusPixels(zoom) - worst;

        // Measured against the posed worst case, reaction lean included.
        // Flat, not proportional —
        // the radius does not get looser as the spectator zooms in, which is
        // exactly where a cull has to be tight.
        Assert.InRange(slack, 0f, 3f);
    }

    private static float Extent(Rectangle bounds, Vector2 anchor) =>
        MathF.Max(
            MathF.Max(anchor.X - bounds.Left, bounds.Right - anchor.X),
            MathF.Max(anchor.Y - bounds.Top, bounds.Bottom - anchor.Y));

    [Fact]
    public void IsPotentiallyVisible_AgreesWithTheBoundingRectangle()
    {
        var arenaBounds = BaselineArenaBounds;

        foreach (var zoom in ZoomSamples)
        {
            var radius = ConservativePawnCull.RadiusPixels(zoom);

            for (var x = -200; x <= 1_900; x += 7)
            {
                for (var y = -200; y <= 1_300; y += 11)
                {
                    var anchor = new Vector2(x + 0.5f, y + 0.25f);

                    Assert.Equal(
                        ConservativePawnCull.Bounds(anchor, zoom)
                            .Intersects(arenaBounds),
                        ConservativePawnCull.IsPotentiallyVisible(
                            anchor,
                            radius,
                            arenaBounds));
                }
            }
        }
    }

    /// <summary>
    /// The property the reordering in GPU-016 would depend on, stated the way
    /// the renderer would use it: no pawn the exact cull draws is ever
    /// rejected by the pre-cull, so the drawn set cannot change.
    /// </summary>
    [Fact]
    public void PreCull_NeverRejectsAPawnTheExactCullDraws()
    {
        var arenaBounds = BaselineArenaBounds;
        var appearances = GeometryAppearances().ToArray();

        foreach (var zoom in ZoomSamples)
        {
            var radius = ConservativePawnCull.RadiusPixels(zoom);
            var index = 0;

            for (var x = -180; x <= 1_900; x += 9)
            {
                for (var y = -180; y <= 1_300; y += 13)
                {
                    var anchor = new Vector2(x + 0.5f, y + 0.75f);
                    var appearance = appearances[index++ % appearances.Length];
                    var exact = PawnRenderer.GetBounds(anchor, zoom, appearance);

                    if (!arenaBounds.Intersects(exact))
                    {
                        continue;
                    }

                    Assert.True(
                        ConservativePawnCull.IsPotentiallyVisible(
                            anchor,
                            radius,
                            arenaBounds),
                        $"Pre-cull rejected a drawn pawn at {anchor} zoom {zoom}.");
                }
            }
        }
    }

    /// <summary>
    /// Design open question 4, answered with numbers. Records what fraction of
    /// a uniformly spread army the pre-cull admits at each of the three camera
    /// stations, against what fraction the exact cull actually draws.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The three rows say the same thing in two different ways. At minimum
    /// zoom and at default fit the whole map is on the panel, so the exact
    /// cull already draws every pawn and the pre-cull cannot skip anything —
    /// both admit all of them, and a pre-cull buys exactly nothing there. At
    /// maximum zoom the exact cull draws 1.22 percent of the field and the
    /// pre-cull admits 1.52 percent, so it rejects 98.48 percent of the army
    /// while overshooting the drawn set by 24.6 percent of itself. The
    /// conservative bound is therefore not too generous to be useful: where a
    /// cull can help at all, it is within a third of a percentage point of
    /// the exact answer.
    /// </para>
    /// <para>
    /// The maximum-zoom row was 1.32 percent while the radius bounded neutral
    /// geometry only. Task 7 widened it to bound an attacking pawn's true
    /// heading, extension, arms, and trail as well
    /// (<see cref="ConservativePawnCull"/>), which costs two tenths of a
    /// percentage point of the field — at 200 warriors, one extra pawn — and
    /// buys the guarantee that a warrior striking at the edge of the panel is
    /// never culled while its weapon is on screen.
    /// </para>
    /// <para>
    /// This is a model, not a measurement: agents are spread evenly over the
    /// default 1280x720 map and the camera sits at map centre, which is where
    /// <see cref="SpectatorCamera"/> puts it before any pan. A real battle
    /// clusters, so a real maximum-zoom frame aimed at a melee admits more
    /// than the maximum-zoom row below and a frame aimed anywhere else admits
    /// less. The tracked Phase 1 render baseline
    /// (docs/development/render-baselines/render-matrix-2026-07-29.json)
    /// measured the drawn count directly at 1 000 units and agrees with the
    /// shape of this model: all 1 000 pawns drawn at minimum zoom and at
    /// default fit, none at all at maximum zoom.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData("minimum zoom", MinimumZoom, 1.0, 1.0)]
    [InlineData("default fit", FitStationSentinelZoom, 1.0, 1.0)]
    [InlineData("maximum zoom", MaximumZoom, 0.0122, 0.0152)]
    public void AdmittedFraction_IsRecordedForEachCameraStation(
        string stationName,
        float zoom,
        double expectedExactFraction,
        double expectedPreCullFraction)
    {
        var camera = new SpectatorCamera(MapWidth, MapHeight);
        if (zoom == FitStationSentinelZoom)
        {
            camera.Fit(BaselineArenaBounds);
        }
        else
        {
            camera.SetZoom(zoom);
        }

        var (exactFraction, preCullFraction) = MeasureAdmittedFractions(camera);

        Assert.Equal(expectedExactFraction, exactFraction, 4);
        Assert.Equal(expectedPreCullFraction, preCullFraction, 4);
        Assert.True(
            preCullFraction >= exactFraction,
            $"{stationName}: the pre-cull must never admit less than the " +
            "exact cull draws.");
    }

    private static (double ExactFraction, double PreCullFraction) MeasureAdmittedFractions(
        SpectatorCamera camera)
    {
        var arenaBounds = BaselineArenaBounds;
        var radius = ConservativePawnCull.RadiusPixels(camera.Zoom);
        var appearances = GeometryAppearances().ToArray();
        var index = 0;
        var sampled = 0;
        var exactCount = 0;
        var preCullCount = 0;

        for (var worldX = 0; worldX < MapWidth; worldX += 4)
        {
            for (var worldY = 0; worldY < MapHeight; worldY += 4)
            {
                var anchor = camera.WorldToScreen(
                    new Vector2(worldX, worldY),
                    arenaBounds);
                var appearance = appearances[index++ % appearances.Length];

                sampled++;
                if (arenaBounds.Intersects(
                        PawnRenderer.GetBounds(anchor, camera.Zoom, appearance)))
                {
                    exactCount++;
                }

                if (ConservativePawnCull.IsPotentiallyVisible(
                        anchor,
                        radius,
                        arenaBounds))
                {
                    preCullCount++;
                }
            }
        }

        return ((double)exactCount / sampled, (double)preCullCount / sampled);
    }

    /// <summary>
    /// The same upper-bound proof, for an actively attacking pawn. An attack
    /// aims the weapon at a true heading and extends it, so the drawn line can
    /// reach further from the foot anchor than any neutral appearance does,
    /// and the arms, the axe head, the trail, and the shield guard all travel
    /// with it (attack-animation-v2 design section 11).
    /// </summary>
    /// <remarks>
    /// The radius stays pose-blind: it is still a function of zoom alone. What
    /// changes is that it is now derived from the largest extent a posed pawn
    /// can reach rather than the largest a neutral one can, which is what
    /// keeps a warrior striking at the edge of the panel from being culled
    /// while its weapon would have been on screen.
    /// </remarks>
    [Fact]
    public void Bounds_ContainEveryPosedVisualBoundsAnAttackCanProduce()
    {
        var cases = 0;

        foreach (var weapon in Enum.GetValues<WeaponId>())
        {
            foreach (var shield in Enum.GetValues<ShieldId>())
            {
                var appearance = PawnAppearanceFactory.Create(0, weapon, shield);

                foreach (var resolution in Enum.GetValues<AttackResolution>())
                {
                    foreach (var (directionX, directionY) in PosedHeadings)
                    {
                        foreach (var zoom in ZoomSamples)
                        {
                            foreach (var anchor in AnchorSamples)
                            {
                                var exact = PosedVisualBounds(
                                    appearance,
                                    weapon,
                                    shield,
                                    resolution,
                                    directionX,
                                    directionY,
                                    anchor,
                                    zoom);

                                AssertContains(
                                    ConservativePawnCull.Bounds(anchor, zoom),
                                    exact,
                                    appearance,
                                    zoom,
                                    anchor);
                                cases++;
                            }
                        }
                    }
                }
            }
        }

        // Every weapon x every shield x 5 resolutions x 16 headings x 14
        // zooms x 4 anchors. Asserted so a silently shrunken axis list cannot
        // pass this test by covering less.
        //
        // Both the weapon and the shield factor are read from their enums
        // rather than written as literals. The weapon factor was a literal 4,
        // authored when the roster was four weapons, and the ranged three
        // turned it into a merge failure that said nothing about what this
        // test protects — every containment assertion above passed for all
        // seven. The shield factor was a literal 2 and rotted the same way
        // the moment the shield-size package appended
        // ShieldId.NarrowBreastHigh.
        Assert.Equal(
            Enum.GetValues<WeaponId>().Length
                * Enum.GetValues<ShieldId>().Length
                * 5 * 16 * 14 * 4,
            cases);
    }

    /// <summary>
    /// A struck defender leans away from the contact, which moves its torso,
    /// head, legs, and arms without moving its planted feet. The radius has to
    /// contain that too, and it is measured against the largest reaction any
    /// resolution can produce rather than against a chosen one.
    /// </summary>
    [Fact]
    public void Bounds_ContainAStruckDefendersReactionLean()
    {
        var appearance = PawnAppearanceFactory.Create(
            0,
            WeaponId.Kalis,
            ShieldId.TallHardwood);
        var pose = AttackPoseResolver.Resolve(
            AttackGeometryTests.Animation(WeaponId.Kalis, directionX: 1f));

        foreach (var resolution in Enum.GetValues<AttackResolution>())
        {
            foreach (var isLethal in new[] { false, true })
            {
                for (var step = 0; step < 16; step++)
                {
                    var angle = step * (MathF.Tau / 16f);
                    var reaction = new DefenderReaction(
                        Sequence: 1,
                        AttackerEntityId: 2,
                        DefenderEntityId: 7,
                        XRaw: 0,
                        YRaw: 0,
                        DirectionX: MathF.Cos(angle),
                        DirectionY: MathF.Sin(angle),
                        resolution,
                        isLethal,
                        AgeSeconds: 0f);

                    foreach (var zoom in ZoomSamples)
                    {
                        foreach (var anchor in AnchorSamples)
                        {
                            var exact = PawnGeometry.PoseBlindPrefix
                                .Create(
                                    anchor,
                                    zoom,
                                    appearance,
                                    scaleMultiplier: 1f,
                                    AppearanceComponentCatalog.MaxArmorWidthFactor,
                                    hasSash: true,
                                    AppearanceComponentCatalog.MaxAccentMarksPerPawn)
                                .CompleteAttackPosedLayout(
                                    pose,
                                    gaitPose: null,
                                    reaction.ResolveOffset())
                                .VisualBounds;

                            AssertContains(
                                ConservativePawnCull.Bounds(anchor, zoom),
                                exact,
                                appearance,
                                zoom,
                                anchor);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Sixteen headings rather than the eight cardinal and intercardinal ones
    /// the pose tests use: the extent is largest between the axes, so the
    /// off-axis samples are the ones that actually constrain the radius.
    /// </summary>
    private static IEnumerable<(float X, float Y)> PosedHeadings
    {
        get
        {
            for (var step = 0; step < 16; step++)
            {
                var angle = step * (MathF.Tau / 16f);
                yield return (MathF.Cos(angle), MathF.Sin(angle));
            }
        }
    }

    /// <summary>
    /// The exact visual bounds of a pawn mid-contact, taken through the same
    /// two-stage path the render loop uses. Contact is the widest moment: the
    /// weapon is fully extended, the trail is at full strength, and the stance
    /// is fully planted.
    /// </summary>
    private static Rectangle PosedVisualBounds(
        PawnAppearance appearance,
        WeaponId weapon,
        ShieldId shield,
        AttackResolution resolution,
        float directionX,
        float directionY,
        Vector2 anchor,
        float zoom)
    {
        var pose = AttackPoseResolver.Resolve(
            AttackGeometryTests.Animation(
                weapon,
                resolution,
                shield: shield,
                directionX: directionX,
                directionY: directionY));

        // The draw path passes a reaction offset alongside the pose
        // (ArenaGame.ResolveReactionOffset), so the containment proof has to
        // carry one too. The largest a reaction can be is a lethal landed
        // blow at contact, which is what is used here.
        var reaction = new DefenderReaction(
            Sequence: 1,
            AttackerEntityId: 2,
            DefenderEntityId: 7,
            XRaw: 0,
            YRaw: 0,
            directionX,
            directionY,
            AttackResolution.Landed,
            IsLethal: true,
            AgeSeconds: 0f);

        return PawnGeometry.PoseBlindPrefix
            .Create(
                anchor,
                zoom,
                appearance,
                scaleMultiplier: 1f,
                AppearanceComponentCatalog.MaxArmorWidthFactor,
                hasSash: true,
                AppearanceComponentCatalog.MaxAccentMarksPerPawn)
            .CompleteAttackPosedLayout(pose, gaitPose: null, reaction.ResolveOffset())
            .VisualBounds;
    }

    private static void AssertContains(
        Rectangle conservative,
        Rectangle exact,
        PawnAppearance appearance,
        float zoom,
        Vector2 anchor)
    {
        if (conservative.Left <= exact.Left &&
            conservative.Top <= exact.Top &&
            conservative.Right >= exact.Right &&
            conservative.Bottom >= exact.Bottom)
        {
            return;
        }

        Assert.Fail(
            $"Conservative bound {conservative} does not contain exact bound " +
            $"{exact} at zoom {zoom}, anchor {anchor}, weapon " +
            $"{appearance.WeaponRole}, shield {appearance.ShieldRole}, " +
            $"skin {appearance.ShieldSkinId}, stature " +
            $"{appearance.StatureMultiplier}, build {appearance.BuildMultiplier}.");
    }

    /// <summary>
    /// Every geometry-relevant appearance: the four weapon roles, both shield
    /// roles, all three statures, all three builds, and every shield skin the
    /// catalog declares — the four rolled tall-hardwood skins plus the two
    /// fallback entries, which
    /// <c>PawnGeometry.ShieldProportionDelta</c> also has to classify even
    /// though <c>SelectSkin</c> cannot reach them today.
    /// </summary>
    /// <summary>
    /// Every shield skin identifier <see cref="GeometryAppearances"/> sweeps:
    /// each tall-hardwood skin, the family default, the model-category
    /// default, and every skin declared for a shield role added since — today
    /// the single narrow breast-high board. Hoisted to a field so the
    /// cardinality assertions can multiply by its real length instead of a
    /// literal, which is the same rot this file already repaired twice for
    /// the weapon-role and shield-role factors.
    /// </summary>
    private static readonly string[] GeometryShieldSkinIds =
        ShieldVisualCatalog.TallHardwoodSkins
            .Select(skin => skin.Catalog.Id)
            .Append(ShieldVisualCatalog.Default.Catalog.Id)
            .Append(ShieldVisualCatalog.ModelCategoryDefault.Catalog.Id)
            .Concat(ShieldVisualCatalog
                .GetSkins(PawnShieldRole.NarrowBreastHigh)
                .Select(skin => skin.Catalog.Id))
            .ToArray();

    private static IEnumerable<PawnAppearance> GeometryAppearances()
    {
        var seed = PawnAppearanceFactory.Create(
            0,
            WeaponId.Kampilan,
            ShieldId.TallHardwood);

        var shieldSkinIds = GeometryShieldSkinIds;

        foreach (var weaponRole in Enum.GetValues<PawnWeaponRole>())
        {
            foreach (var shieldRole in Enum.GetValues<PawnShieldRole>())
            {
                foreach (var stature in new[] { 0.90f, 1.00f, 1.10f })
                {
                    foreach (var build in new[] { 0.86f, 1.00f, 1.18f })
                    {
                        foreach (var shieldSkinId in shieldSkinIds)
                        {
                            yield return seed with
                            {
                                WeaponRole = weaponRole,
                                ShieldRole = shieldRole,
                                StatureMultiplier = stature,
                                BuildMultiplier = build,
                                ShieldSkinId = shieldSkinId,
                            };
                        }
                    }
                }
            }
        }
    }
}
