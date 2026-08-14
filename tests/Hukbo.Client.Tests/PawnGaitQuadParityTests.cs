using Hukbo.Client.Presentation;
using Hukbo.Client.Rendering;
using Hukbo.Core.Combat;
using Hukbo.Diagnostics;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

/// <summary>
/// PV-9. <c>RecordPawnQuads</c> (<c>ArenaGame.Rendering.cs</c>) used to pass a
/// hardcoded <c>gaitPose: null</c> to
/// <c>PawnGeometry.PoseBlindPrefix.CompleteAttackPosedLayout</c>, while
/// <c>DrawPawns</c> resolves and passes the real <see cref="GaitPoseResolver"/>
/// result for the same agent. <c>RecordPawnQuads</c> is a private method on a
/// live <c>ArenaGame</c> and is not called from here; every assertion below is
/// built from <see cref="PawnGeometry"/> and <see cref="PawnQuadCount"/>
/// directly — the same two pure, already-tested helpers both call sites
/// share — never by invoking the fixed method and comparing it against
/// itself.
/// </summary>
/// <remarks>
/// Measured directly below rather than assumed: at
/// <see cref="PawnDetailTier.Medium"/> and above,
/// <c>PawnQuadCount.CountLegs</c>/<c>CountFeet</c>
/// (<c>SubmissionCount.cs:149-161</c>) gate the four leg/foot quads purely on
/// <c>PawnLayout.LeftLegBounds</c>/.../<c>RightFootBounds</c> being non-empty,
/// and <c>PawnGeometry.CreateLegsAndFeet</c> (<c>PawnGeometry.cs:1691-1731</c>)
/// gates those four rectangles purely on <c>PawnDetailTier</c>, never on any
/// <see cref="GaitPose"/> field value. A <c>null</c> gait pose resolves to
/// <c>default(GaitPose)</c>, the documented "standing still" neutral
/// (<c>PawnGeometry.cs:1543</c>), and a real walking pose only moves those
/// four rectangles — it never empties or un-empties one. So the two poses
/// produce the identical <c>PawnQuadCount.Count</c> total at Medium tier; the
/// legs-plus-feet term is already unconditionally present either way, and the
/// only thing a hardcoded <c>null</c> ever got wrong was position, not count.
/// <see cref="TheFixDoesNotChangeQuadCountAtMediumTier_OnlyLegAndFootPosition"/>
/// records that measurement so a future reader does not have to re-derive it,
/// and it is the reason this file cannot honestly assert a quad-count delta
/// for the fix — doing so would misdescribe what the fix actually corrects.
/// </remarks>
public sealed class PawnGaitQuadParityTests
{
    private const float MediumTierZoom = 1f;

    /// <summary>
    /// A concrete walking pose: opposite-signed leg offsets and a lifted left
    /// foot, matching <see cref="GaitMode.Walk"/>'s documented shape.
    /// </summary>
    private static readonly GaitPose WalkingPose = new(
        GaitMode.Walk,
        PhaseTurns: 0.25f,
        LeftLegOffsetRatio: 0.4f,
        RightLegOffsetRatio: -0.4f,
        LeftFootLiftRatio: 0.6f,
        RightFootLiftRatio: 0f,
        TorsoLeanX: 0f,
        TorsoLeanY: 0f,
        DirectionSign: 1f);

    /// <summary>
    /// The real defect a hardcoded <c>gaitPose: null</c> produced: the
    /// recorded layout's leg and foot rectangles sit at the standing-pose
    /// location instead of the real mid-stride one, even though the total
    /// quad count PawnQuadCount.Count returns is unaffected. Both facts are
    /// asserted from the same two independently-built layouts, oracled
    /// through PawnQuadCount.Count — not through the fixed call site.
    /// </summary>
    [Fact]
    public void TheFixDoesNotChangeQuadCountAtMediumTier_OnlyLegAndFootPosition()
    {
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);
        var prefix = PawnGeometry.PoseBlindPrefix.Create(
            new Vector2(400.5f, 300.5f),
            MediumTierZoom,
            appearance);

        var neutralLayout = prefix.CompleteAttackPosedLayout(attackPose: null, gaitPose: null);
        var walkingLayout = prefix.CompleteAttackPosedLayout(
            attackPose: null,
            gaitPose: WalkingPose);

        Assert.Equal(PawnDetailTier.Medium, neutralLayout.DetailTier);
        Assert.False(neutralLayout.LeftLegBounds.IsEmpty);
        Assert.False(walkingLayout.LeftLegBounds.IsEmpty);

        var neutralCount = PawnQuadCount.Count(
            neutralLayout, appearance, PawnVisualState.Normal);
        var walkingCount = PawnQuadCount.Count(
            walkingLayout, appearance, PawnVisualState.Normal);

        Assert.Equal(neutralCount, walkingCount);

        Assert.NotEqual(neutralLayout.LeftLegBounds, walkingLayout.LeftLegBounds);
        Assert.NotEqual(neutralLayout.RightLegBounds, walkingLayout.RightLegBounds);
        Assert.NotEqual(neutralLayout.LeftFootBounds, walkingLayout.LeftFootBounds);
    }

    /// <summary>
    /// At <see cref="PawnDetailTier.Low"/> the gait channel is moot either
    /// way — <c>CreateLegsAndFeet</c> returns the default (empty) layout for
    /// every pose at that tier, so the legs-plus-feet term is zero for both a
    /// null and a walking pose. Recorded so the Medium-tier equality above is
    /// not mistaken for "gait pose never matters"; it is tier-gated
    /// unconditionally, in both directions.
    /// </summary>
    [Fact]
    public void LegsAndFeetStayEmptyAtLowTierRegardlessOfGaitPose()
    {
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);
        var prefix = PawnGeometry.PoseBlindPrefix.Create(
            new Vector2(400.5f, 300.5f),
            cameraZoom: 0.05f,
            appearance);

        var neutralLayout = prefix.CompleteAttackPosedLayout(attackPose: null, gaitPose: null);
        var walkingLayout = prefix.CompleteAttackPosedLayout(
            attackPose: null,
            gaitPose: WalkingPose);

        Assert.Equal(PawnDetailTier.Low, neutralLayout.DetailTier);
        Assert.Equal(Rectangle.Empty, neutralLayout.LeftLegBounds);
        Assert.Equal(Rectangle.Empty, walkingLayout.LeftLegBounds);
        Assert.Equal(
            PawnQuadCount.Count(neutralLayout, appearance, PawnVisualState.Normal),
            PawnQuadCount.Count(walkingLayout, appearance, PawnVisualState.Normal));
    }

    /// <summary>
    /// The fix itself: <see cref="GaitPoseResolver.TryGetPose"/> against the
    /// same store both <c>RecordPawnQuads</c> and <c>DrawPawns</c> read
    /// (<c>ArenaGame._gaitPoses</c>) resolves the identical pose for a
    /// tracked, moving entity id — the lookup <c>RecordPawnQuads</c> now
    /// performs instead of hardcoding <c>null</c>.
    /// </summary>
    [Fact]
    public void GaitPoseResolverReturnsTheSamePoseBothCallSitesNowResolve()
    {
        var store = new Dictionary<ulong, GaitPose> { [7UL] = WalkingPose };

        var found = GaitPoseResolver.TryGetPose(store, 7UL, out var resolved);

        Assert.True(found);
        Assert.Equal(WalkingPose, resolved);
        Assert.False(GaitPoseResolver.TryGetPose(store, 404UL, out var missing));
        Assert.Equal(default, missing);
    }

    /// <summary>
    /// The regression guard: the literal <c>gaitPose: null</c> must never
    /// reappear in <c>ArenaGame.Rendering.cs</c>. Same source-text-scan
    /// pattern <c>SourceHygieneTests</c> already uses elsewhere in this file
    /// set.
    /// </summary>
    [Fact]
    public void ArenaGameRenderingSourceNeverHardcodesANullGaitPose()
    {
        var root = GetRepositoryRoot();
        var path = Path.Combine(root, "src", "Hukbo.Client", "ArenaGame.Rendering.cs");

        Assert.DoesNotContain(
            "gaitPose: null",
            File.ReadAllText(path),
            StringComparison.Ordinal);
    }

    private static string GetRepositoryRoot()
    {
        var root = LogPaths.FindRepositoryRoot(AppContext.BaseDirectory);
        Assert.True(
            root is not null,
            "No ancestor of " + AppContext.BaseDirectory + " contains " +
            LogPaths.RepositoryMarkerFileName +
            ", so the source tree cannot be scanned.");
        return root!;
    }
}
