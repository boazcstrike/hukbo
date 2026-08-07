using Hukbo.Client.Presentation;
using Hukbo.Client.Rendering;
using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

/// <summary>
/// Pins the placement math behind <c>PawnRenderer</c>'s above-head marks —
/// channel 1 of the spectator explanation in pressure-interrupt design
/// section 3, question 8.
/// </summary>
/// <remarks>
/// Nothing here constructs an <c>ArenaGame</c>, a <c>GraphicsDevice</c>, a
/// <c>SpriteBatch</c>, or a window. Every assertion runs against the pure
/// geometry helpers the renderer itself calls — <see cref="PawnRenderer"/>'s
/// <c>GetLeaderMarkBounds</c>, <c>GetBreakOffMarkBounds</c>,
/// <c>TryGetBreakOffMarkBounds</c>, and <c>GetSelectionMarkCornerBounds</c> —
/// over layouts <see cref="PawnGeometry.Create"/> builds, which is the same
/// GPU-independent route <c>PawnGeometryTests</c> and
/// <c>PawnQuadCountTests</c> already take. Those helpers have no second
/// implementation: <c>PawnRenderer.DrawLayout</c> holds no copy of the
/// arithmetic, so the rectangles asserted below are the rectangles drawn.
/// </remarks>
public sealed class PawnRendererTests
{
    /// <summary>
    /// Camera zooms matching <c>PawnGeometryTests</c>'s own convention for
    /// exercising all three detail tiers.
    /// </summary>
    private static readonly float[] TierZooms = [0.05f, 0.5f, 1f, 3f, 12f];

    private static readonly WeaponId[] Weapons =
    [
        WeaponId.Kampilan,
        WeaponId.Wasay,
        WeaponId.Kalis,
        WeaponId.Itak,
    ];

    private static readonly ShieldId[] Shields =
    [
        ShieldId.None,
        ShieldId.TallHardwood,
    ];

    /// <summary>
    /// Head rectangles spanning the sizes <see cref="PawnGeometry"/> produces
    /// from the lowest to the highest zoom, plus the degenerate one-pixel head
    /// that every <c>Math.Max</c> floor in the placement math exists for.
    /// </summary>
    private static readonly Rectangle[] HeadBoundsGrid =
    [
        new(0, 0, 1, 1),
        new(0, 0, 2, 2),
        new(-7, -3, 3, 4),
        new(11, 40, 6, 7),
        new(100, 250, 12, 14),
        new(-40, -120, 25, 30),
        new(999, 1, 64, 80),
    ];

    private static readonly (bool Right, bool Bottom)[] Corners =
    [
        (false, false),
        (true, false),
        (false, true),
        (true, true),
    ];

    private static IEnumerable<PawnLayout> EnumerateLayouts()
    {
        var entityId = 0UL;
        foreach (var weapon in Weapons)
        {
            foreach (var shield in Shields)
            {
                foreach (var zoom in TierZooms)
                {
                    var appearance = PawnAppearanceFactory.Create(
                        entityId++,
                        weapon,
                        shield);
                    yield return PawnGeometry.Create(
                        new Vector2(320f, 240f),
                        zoom,
                        appearance);
                }
            }
        }
    }

    // ------------------------------------------------------------------
    // The view flag is false: nothing is drawn.
    // ------------------------------------------------------------------

    [Fact]
    public void TryGetBreakOffMarkBounds_DrawsNothingWhenTheViewFlagIsFalse()
    {
        foreach (var headBounds in HeadBoundsGrid)
        {
            Assert.False(
                PawnRenderer.TryGetBreakOffMarkBounds(
                    brokeOffUnderPressure: false,
                    headBounds,
                    out var bounds));
            Assert.Equal(Rectangle.Empty, bounds);
        }
    }

    [Fact]
    public void TryGetBreakOffMarkBounds_DrawsNothingForAnyRealLayoutWhenFalse()
    {
        foreach (var layout in EnumerateLayouts())
        {
            Assert.False(
                PawnRenderer.TryGetBreakOffMarkBounds(
                    brokeOffUnderPressure: false,
                    layout.HeadBounds,
                    out var bounds));
            Assert.Equal(Rectangle.Empty, bounds);
        }
    }

    /// <summary>
    /// The other half of "no mark under a preset that does not apply the
    /// interrupt": a view built without naming the member carries
    /// <see langword="false"/>, and that is the value the arena render path
    /// forwards to the renderer.
    /// </summary>
    [Fact]
    public void AgentView_DefaultsBrokeOffUnderPressureToFalse()
    {
        var view = new AgentView(
            EntityId: 1,
            FactionId: 0,
            XRaw: 0,
            YRaw: 0,
            HitPoints: 10,
            MaximumHitPoints: 10,
            TargetEntityId: null,
            Intent: AgentIntent.Idle,
            IsAlive: true,
            Loadout: default);

        Assert.False(view.BrokeOffUnderPressure);
        Assert.False(
            PawnRenderer.TryGetBreakOffMarkBounds(
                view.BrokeOffUnderPressure,
                new Rectangle(0, 0, 12, 14),
                out _));
    }

    // ------------------------------------------------------------------
    // The view flag is true: one mark, above the head.
    // ------------------------------------------------------------------

    [Fact]
    public void TryGetBreakOffMarkBounds_ReturnsThePlacementGeometryWhenTrue()
    {
        foreach (var headBounds in HeadBoundsGrid)
        {
            Assert.True(
                PawnRenderer.TryGetBreakOffMarkBounds(
                    brokeOffUnderPressure: true,
                    headBounds,
                    out var bounds));
            Assert.Equal(PawnRenderer.GetBreakOffMarkBounds(headBounds), bounds);
            Assert.True(bounds.Width > 0);
            Assert.True(bounds.Height > 0);
        }
    }

    [Fact]
    public void GetBreakOffMarkBounds_SitsWhollyAboveTheHeadAndIsCentredOnIt()
    {
        foreach (var headBounds in HeadBoundsGrid)
        {
            var mark = PawnRenderer.GetBreakOffMarkBounds(headBounds);

            Assert.True(
                mark.Bottom <= headBounds.Top,
                $"break-off mark {mark} overlapped head {headBounds}");
            Assert.False(mark.Intersects(headBounds));
            Assert.Equal(headBounds.Center.X, mark.Center.X);
        }
    }

    /// <summary>
    /// The break-off slot is reserved above the leader slot whether or not
    /// this warrior leads, so the rectangle is a function of the head alone.
    /// That is the property every no-collision case below rests on.
    /// </summary>
    [Fact]
    public void GetBreakOffMarkBounds_SitsInTheSlotAboveTheLeaderSlot()
    {
        foreach (var headBounds in HeadBoundsGrid)
        {
            var breakOff = PawnRenderer.GetBreakOffMarkBounds(headBounds);
            var leader = PawnRenderer.GetLeaderMarkBounds(headBounds);

            Assert.True(
                breakOff.Bottom < leader.Top,
                $"break-off {breakOff} was not clear of leader slot {leader}");
        }
    }

    // ------------------------------------------------------------------
    // No collision: break-off mark, leader mark, selection ring.
    // ------------------------------------------------------------------

    [Fact]
    public void GetBreakOffMarkBounds_NeverOverlapsTheLeaderMark()
    {
        foreach (var headBounds in HeadBoundsGrid)
        {
            var breakOff = PawnRenderer.GetBreakOffMarkBounds(headBounds);
            var leader = PawnRenderer.GetLeaderMarkBounds(headBounds);

            Assert.False(
                breakOff.Intersects(leader),
                $"break-off {breakOff} overlapped leader {leader} for head {headBounds}");
        }
    }

    [Fact]
    public void GetBreakOffMarkBounds_NeverOverlapsTheLeaderMarkOnARealLayout()
    {
        foreach (var layout in EnumerateLayouts())
        {
            var breakOff = PawnRenderer.GetBreakOffMarkBounds(layout.HeadBounds);
            var leader = PawnRenderer.GetLeaderMarkBounds(layout.HeadBounds);

            Assert.False(
                breakOff.Intersects(leader),
                $"break-off {breakOff} overlapped leader {leader}");
        }
    }

    [Fact]
    public void GetBreakOffMarkBounds_NeverOverlapsTheSelectionRing()
    {
        foreach (var layout in EnumerateLayouts())
        {
            var breakOff = PawnRenderer.GetBreakOffMarkBounds(layout.HeadBounds);

            foreach (var (right, bottom) in Corners)
            {
                var corner = PawnRenderer.GetSelectionMarkCornerBounds(
                    layout.SelectionBounds,
                    right,
                    bottom);

                Assert.False(
                    breakOff.Intersects(corner),
                    $"break-off {breakOff} overlapped selection corner {corner} " +
                    $"(right: {right}, bottom: {bottom}) of {layout.SelectionBounds}");
            }
        }
    }

    /// <summary>
    /// The case the plan names explicitly: one warrior that is leading,
    /// selected, and breaking off under pressure on the same tick. All three
    /// marks draw, and no pair of them shares a pixel.
    /// </summary>
    [Fact]
    public void AllThreeMarksOnOneWarriorNeverOverlapEachOther()
    {
        foreach (var layout in EnumerateLayouts())
        {
            Assert.True(
                PawnRenderer.TryGetBreakOffMarkBounds(
                    brokeOffUnderPressure: true,
                    layout.HeadBounds,
                    out var breakOff));
            var leader = PawnRenderer.GetLeaderMarkBounds(layout.HeadBounds);

            Assert.False(
                breakOff.Intersects(leader),
                $"break-off {breakOff} met leader {leader}");

            foreach (var (right, bottom) in Corners)
            {
                var corner = PawnRenderer.GetSelectionMarkCornerBounds(
                    layout.SelectionBounds,
                    right,
                    bottom);

                Assert.False(
                    breakOff.Intersects(corner),
                    $"break-off {breakOff} met selection corner {corner}");
                Assert.False(
                    leader.Intersects(corner),
                    $"leader {leader} met selection corner {corner}");
            }
        }
    }

    // ------------------------------------------------------------------
    // The extracted helpers still describe what was already drawn.
    // ------------------------------------------------------------------

    /// <summary>
    /// Pins <c>GetLeaderMarkBounds</c> against the arithmetic
    /// <c>DrawLeaderMark</c> carried inline before this task extracted it, so
    /// the extraction cannot have moved the existing leader marker.
    /// </summary>
    [Theory]
    // width/2, height/6, gap height/8, floored at 2/1/1 and centred on the
    // head, exactly as the inline body did: e.g. a 6x7 head at (100, 250)
    // gives width 3, height 1, gap 1, centre x 103, so x = 103 - 3/2 = 102
    // and y = 250 - 1 - 1 = 248.
    [InlineData(0, 0, 12, 14, 3, -3, 6, 2)]
    [InlineData(100, 250, 6, 7, 102, 248, 3, 1)]
    [InlineData(-40, -120, 25, 30, -34, -128, 12, 5)]
    [InlineData(0, 0, 1, 1, -1, -2, 2, 1)]
    public void GetLeaderMarkBounds_MatchesTheInlineArithmeticItReplaced(
        int headX,
        int headY,
        int headWidth,
        int headHeight,
        int expectedX,
        int expectedY,
        int expectedWidth,
        int expectedHeight)
    {
        var headBounds = new Rectangle(headX, headY, headWidth, headHeight);

        Assert.Equal(
            new Rectangle(expectedX, expectedY, expectedWidth, expectedHeight),
            PawnRenderer.GetLeaderMarkBounds(headBounds));
    }

    /// <summary>
    /// Pins <c>GetSelectionMarkCornerBounds</c> against the corner origins
    /// <c>DrawSelectionMark</c> computed inline before this task extracted
    /// them: the left and top brackets hang off the bounds' own edges, and the
    /// right and bottom ones end on the last pixel inside the bounds.
    /// </summary>
    [Fact]
    public void GetSelectionMarkCornerBounds_HangsOffEachCornerOfTheBounds()
    {
        var bounds = new Rectangle(10, 20, 48, 64);
        var length = PawnRenderer.GetSelectionMarkCornerLength(bounds);

        Assert.Equal(10, length);
        Assert.Equal(
            new Rectangle(10, 20, 10, 10),
            PawnRenderer.GetSelectionMarkCornerBounds(bounds, right: false, bottom: false));
        Assert.Equal(
            new Rectangle(48, 20, 10, 10),
            PawnRenderer.GetSelectionMarkCornerBounds(bounds, right: true, bottom: false));
        Assert.Equal(
            new Rectangle(10, 74, 10, 10),
            PawnRenderer.GetSelectionMarkCornerBounds(bounds, right: false, bottom: true));
        Assert.Equal(
            new Rectangle(48, 74, 10, 10),
            PawnRenderer.GetSelectionMarkCornerBounds(bounds, right: true, bottom: true));
    }

    /// <summary>
    /// The selection ring must be pixel-identical after the corner math moved
    /// into <c>GetSelectionMarkCornerBounds</c>. This reproduces the exact
    /// quads <c>DrawSelectionMark</c>'s old inline <c>DrawCorner</c> local
    /// function submitted — signed origins at the bounds' extreme pixels,
    /// arms grown back toward the centre — and asserts the quads the helper
    /// now produces are the same rectangles, over both thicknesses the
    /// renderer ever passes (2 for selected, 1 for hovered) and over every
    /// real layout.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void GetSelectionMarkCornerBounds_ReproducesTheInlineCornerQuads(int thickness)
    {
        foreach (var layout in EnumerateLayouts())
        {
            var bounds = layout.SelectionBounds;
            var cornerLength = Math.Clamp(
                Math.Min(bounds.Width, bounds.Height) / 4,
                4,
                10);
            var lastRight = bounds.Right - 1;
            var lastBottom = bounds.Bottom - 1;

            AssertCorner(bounds.Left, bounds.Top, 1, 1, right: false, bottom: false);
            AssertCorner(lastRight, bounds.Top, -1, 1, right: true, bottom: false);
            AssertCorner(bounds.Left, lastBottom, 1, -1, right: false, bottom: true);
            AssertCorner(lastRight, lastBottom, -1, -1, right: true, bottom: true);

            void AssertCorner(
                int x,
                int y,
                int horizontal,
                int vertical,
                bool right,
                bool bottom)
            {
                var expectedHorizontal = new Rectangle(
                    horizontal > 0 ? x : x - cornerLength + 1,
                    vertical > 0 ? y : y - thickness + 1,
                    cornerLength,
                    thickness);
                var expectedVertical = new Rectangle(
                    horizontal > 0 ? x : x - thickness + 1,
                    vertical > 0 ? y : y - cornerLength + 1,
                    thickness,
                    cornerLength);

                var corner = PawnRenderer.GetSelectionMarkCornerBounds(
                    bounds,
                    right,
                    bottom);
                var actualHorizontal = new Rectangle(
                    corner.Left,
                    bottom ? corner.Bottom - thickness : corner.Top,
                    corner.Width,
                    thickness);
                var actualVertical = new Rectangle(
                    right ? corner.Right - thickness : corner.Left,
                    corner.Top,
                    thickness,
                    corner.Height);

                Assert.Equal(expectedHorizontal, actualHorizontal);
                Assert.Equal(expectedVertical, actualVertical);
            }
        }
    }

    [Fact]
    public void GetSelectionMarkCornerLength_ClampsToTheDocumentedRange()
    {
        Assert.Equal(4, PawnRenderer.GetSelectionMarkCornerLength(new Rectangle(0, 0, 1, 1)));
        Assert.Equal(4, PawnRenderer.GetSelectionMarkCornerLength(new Rectangle(0, 0, 20, 16)));
        Assert.Equal(5, PawnRenderer.GetSelectionMarkCornerLength(new Rectangle(0, 0, 20, 21)));
        Assert.Equal(10, PawnRenderer.GetSelectionMarkCornerLength(new Rectangle(0, 0, 400, 400)));
    }

    [Fact]
    public void GetSelectionMarkCornerBounds_StaysInsideTheSelectionBounds()
    {
        foreach (var layout in EnumerateLayouts())
        {
            foreach (var (right, bottom) in Corners)
            {
                var corner = PawnRenderer.GetSelectionMarkCornerBounds(
                    layout.SelectionBounds,
                    right,
                    bottom);

                Assert.True(
                    layout.SelectionBounds.Contains(corner),
                    $"corner {corner} escaped {layout.SelectionBounds}");
            }
        }
    }

    // ------------------------------------------------------------------
    // movement-gait-animation T4: legs and feet, between the ground ring
    // and the torso. PawnRenderer.DrawLayout calls DrawLegs and DrawFeet
    // immediately after DrawGroundBase and before DrawTorso/DrawPlaceholder
    // (see that method's own comment); no SpriteBatch is constructed here
    // to observe that call order directly, so these assert the geometric
    // fact the ordering exists to preserve — the torso stays on top and
    // unobstructed because legs never draw inside its footprint, and always
    // sit in the pose-invariant band directly beneath it.
    // ------------------------------------------------------------------

    [Fact]
    public void LegsAndFeet_AreEmptyAtLowTierAcrossEveryRealLayout()
    {
        var sawALowTierLayout = false;

        foreach (var layout in EnumerateLayouts())
        {
            if (layout.DetailTier != PawnDetailTier.Low)
            {
                continue;
            }

            sawALowTierLayout = true;
            Assert.Equal(Rectangle.Empty, layout.LeftLegBounds);
            Assert.Equal(Rectangle.Empty, layout.RightLegBounds);
            Assert.Equal(Rectangle.Empty, layout.LeftFootBounds);
            Assert.Equal(Rectangle.Empty, layout.RightFootBounds);
        }

        Assert.True(sawALowTierLayout, "no Low-tier layout in the enumerated grid");
    }

    /// <summary>
    /// At the neutral pose <see cref="PawnGeometry.Create"/> builds when no
    /// gait pose is passed, both legs hang from exactly the torso's own
    /// bottom edge — the band <c>PawnRenderer.DrawLegs</c> draws into
    /// between <c>DrawGroundBase</c> and <c>DrawTorso</c>. Combined with
    /// <see cref="TorsoNeverOverlapsALegOrFootRectangle"/>, this is the
    /// geometric proxy for "legs draw before the torso, and the torso stays
    /// unobstructed": the torso is drawn after the legs in every case, and
    /// the two footprints never share a pixel regardless of draw order.
    /// </summary>
    [Fact]
    public void Legs_HangFromExactlyTheTorsosBottomEdgeAtNeutralPose()
    {
        var sawANonLowTierLayout = false;

        foreach (var layout in EnumerateLayouts())
        {
            if (layout.DetailTier == PawnDetailTier.Low)
            {
                continue;
            }

            sawANonLowTierLayout = true;
            Assert.Equal(layout.TorsoBounds.Bottom, layout.LeftLegBounds.Top);
            Assert.Equal(layout.TorsoBounds.Bottom, layout.RightLegBounds.Top);
        }

        Assert.True(sawANonLowTierLayout, "no Medium/High-tier layout in the enumerated grid");
    }

    [Fact]
    public void TorsoNeverOverlapsALegOrFootRectangle()
    {
        foreach (var layout in EnumerateLayouts())
        {
            if (layout.DetailTier == PawnDetailTier.Low)
            {
                continue;
            }

            Assert.False(layout.TorsoBounds.Intersects(layout.LeftLegBounds));
            Assert.False(layout.TorsoBounds.Intersects(layout.RightLegBounds));
            Assert.False(layout.TorsoBounds.Intersects(layout.LeftFootBounds));
            Assert.False(layout.TorsoBounds.Intersects(layout.RightFootBounds));
        }
    }

    /// <summary>
    /// <c>PawnQuadCount.Count</c> is the renderer-agnostic seam
    /// (<c>SubmissionCount.cs</c>'s own class doc) that mirrors
    /// <c>PawnRenderer.DrawLayout</c>'s draw order exactly; this asserts the
    /// leg/foot term of that equality directly rather than only through the
    /// fixed pins in <c>PawnQuadCountTests</c>: adding a leg or a foot
    /// rectangle changes the count by exactly one, for every non-empty
    /// rectangle in a real layout, at both tiers that draw them.
    /// </summary>
    [Theory]
    [InlineData(1f)]
    [InlineData(3f)]
    public void QuadCount_RisesByExactlyOnePerNonEmptyLegOrFootRectangle(float zoom)
    {
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Kalis, ShieldId.None);
        var layout = PawnGeometry.Create(Vector2.Zero, zoom, appearance);
        Assert.NotEqual(PawnDetailTier.Low, layout.DetailTier);

        var nonEmptyLegAndFootRectangles = new[]
        {
            layout.LeftLegBounds,
            layout.RightLegBounds,
            layout.LeftFootBounds,
            layout.RightFootBounds,
        }.Count(bounds => !bounds.IsEmpty);

        var withLegsAndFeet = PawnQuadCount.Count(layout, appearance, PawnVisualState.Normal);
        var withoutLegsAndFeet = PawnQuadCount.Count(
            layout with
            {
                LeftLegBounds = Rectangle.Empty,
                RightLegBounds = Rectangle.Empty,
                LeftFootBounds = Rectangle.Empty,
                RightFootBounds = Rectangle.Empty,
            },
            appearance,
            PawnVisualState.Normal);

        Assert.Equal(nonEmptyLegAndFootRectangles, withLegsAndFeet - withoutLegsAndFeet);
    }
}
