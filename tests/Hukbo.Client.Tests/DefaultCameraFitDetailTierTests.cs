using Hukbo.Client.Presentation;
using Hukbo.Client.Presentation.Catalogs;
using Hukbo.Client.Rendering;
using Hukbo.Core.Combat;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

/// <summary>
/// Pins the default-window detail-tier fix from
/// docs/plans/2026-08-13-gait-default-visibility.md section 2. The arena
/// panel dimensions below are literals rather than a call into
/// <c>ArenaGame.ComputeLayout</c>, because that method is <c>private</c> and
/// not reachable from this test project. They are the same numbers the plan
/// document derives by hand from <c>ArenaGame</c>'s layout constants
/// (<c>StatusBarHeight = 68</c>, <c>EventPanelWidth = 420</c>,
/// <c>LayoutMargin = 12</c>, <c>LayoutGap = 10</c>):
///
/// For a 1600x900 window:
///   contentHeight = 900 - 68 - 12 = 820
///   eventWidth = min(420, 1600 / 3) = 420
///   eventBounds.Left = 1600 - 420 - 12 = 1168
///   arenaRight = max(12, 1168 - 10) = 1158
///   arenaBounds = 1146 x 820
///
/// For the 1024x720 minimum window the plan document records the same
/// arithmetic resolving to an arena panel of 649 x 640.
/// </summary>
public sealed class DefaultCameraFitDetailTierTests
{
    private const int MapWidth = 1280;
    private const int MapHeight = 720;

    [Fact]
    public void DefaultWindowFitResolvesMediumDetailTier()
    {
        var camera = new SpectatorCamera(MapWidth, MapHeight);
        camera.Fit(new Rectangle(0, 0, 1146, 820));

        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);
        var layout = PawnGeometry.Create(Vector2.Zero, camera.Zoom, appearance);

        Assert.Equal(PawnDetailTier.Medium, layout.DetailTier);
    }

    [Fact]
    public void MinimumWindowFitStillResolvesLowDetailTier()
    {
        var camera = new SpectatorCamera(MapWidth, MapHeight);
        camera.Fit(new Rectangle(0, 0, 649, 640));

        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, ShieldId.None);
        var layout = PawnGeometry.Create(Vector2.Zero, camera.Zoom, appearance);

        Assert.Equal(PawnDetailTier.Low, layout.DetailTier);
    }
}
