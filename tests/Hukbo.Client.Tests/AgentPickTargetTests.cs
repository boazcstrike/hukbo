using Hukbo.Client.Presentation;
using Hukbo.Client.Rendering;
using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Simulation;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

/// <summary>
/// Smoke row <c>V2-3</c>'s regression suite. The row was `BLOCKED` on
/// 2026-08-11 because a person could not select a warrior at all; these
/// assertions are what stops the click target drifting back below the drawn
/// body. Nothing here touches a graphics device, a sprite batch, or a window.
/// </summary>
public sealed class AgentPickTargetTests
{
    /// <summary>
    /// The whole zoom range <c>SpectatorCamera</c> clamps to, plus the
    /// interesting points between: the minimum, a fitted-battle zoom, unit
    /// zoom, the apparent-scale ceiling, and the maximum.
    /// </summary>
    public static TheoryData<float> CameraZooms()
    {
        var data = new TheoryData<float>();
        foreach (var zoom in new[] { 0.05f, 0.2f, 0.53f, 1f, 1.78f, 3f, 8f, 12f })
        {
            data.Add(zoom);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(CameraZooms))]
    public void RadiusPixels_IsNeverSmallerThanTheMinimumTarget(float zoom)
    {
        Assert.True(
            AgentPickTarget.RadiusPixels(zoom) >=
                AgentPickTarget.MinimumRadiusPixels,
            $"Radius at zoom {zoom} was " +
            $"{AgentPickTarget.RadiusPixels(zoom)} pixels.");
    }

    [Theory]
    [MemberData(nameof(CameraZooms))]
    public void RadiusPixels_ExceedsTheTargetThatBlockedV23(float zoom)
    {
        // The former target: MathF.Max(5f / zoom, 1.5f) world units, which is
        // five screen pixels everywhere except above zoom 3.33.
        var formerWorldUnits = MathF.Max(5f / zoom, 1.5f);
        var formerPixels = formerWorldUnits * zoom;

        Assert.True(
            AgentPickTarget.RadiusPixels(zoom) > formerPixels,
            $"Radius at zoom {zoom} was " +
            $"{AgentPickTarget.RadiusPixels(zoom)} pixels against the former " +
            $"{formerPixels}.");
    }

    [Theory]
    [MemberData(nameof(CameraZooms))]
    public void RadiusPixels_CoversHalfTheDrawnBodyWheneverThatExceedsTheFloor(
        float zoom)
    {
        var bodyHalfHeight =
            AgentPickTarget.BodyHeightUnits / 2f *
            PawnGeometry.ResolveApparentScale(zoom);

        Assert.Equal(
            MathF.Max(bodyHalfHeight, AgentPickTarget.MinimumRadiusPixels),
            AgentPickTarget.RadiusPixels(zoom),
            precision: 4);
    }

    [Theory]
    [MemberData(nameof(CameraZooms))]
    public void SamplePoint_ShiftsDownToTheFootAnchorAndLeavesXAlone(float zoom)
    {
        var pointer = new Vector2(37f, 11f);

        var sample = AgentPickTarget.SamplePoint(pointer, zoom);

        Assert.Equal(pointer.X, sample.X);
        Assert.True(
            sample.Y > pointer.Y,
            $"Sample Y {sample.Y} did not move below pointer Y {pointer.Y}.");
        Assert.Equal(
            pointer.Y + (AgentPickTarget.BodyCentrePixels(zoom) / zoom),
            sample.Y,
            precision: 4);
    }

    /// <summary>
    /// The V2-3 case itself, end to end: a click on the warrior's chest — the
    /// part of it a spectator aims at — selects that warrior.
    /// </summary>
    [Theory]
    [MemberData(nameof(CameraZooms))]
    public void ClickOnTheDrawnChest_SelectsTheWarrior(float zoom)
    {
        var selection = new AgentSelection();
        AgentView[] agents = [CreateAgent(1, xRaw: 0, yRaw: 0)];

        Select(selection, agents, BodyCentre(zoom), zoom);

        Assert.Equal(1UL, selection.SelectedEntityId);
    }

    /// <summary>
    /// The feet, the waist, the chest, and the head are all inside the target,
    /// so the whole drawn silhouette is clickable rather than one band of it.
    /// </summary>
    [Theory]
    [MemberData(nameof(CameraZooms))]
    public void ClickAnywhereOnTheDrawnBody_SelectsTheWarrior(float zoom)
    {
        var selection = new AgentSelection();
        AgentView[] agents = [CreateAgent(1, xRaw: 0, yRaw: 0)];
        var headTop = MathF.Max(0f, (BodyCentre(zoom) * 2f) - 1f);

        foreach (var pixelsAboveAnchor in new[]
        {
            0f,
            BodyCentre(zoom) / 2f,
            BodyCentre(zoom),
            headTop,
        })
        {
            selection.Clear();

            Select(selection, agents, pixelsAboveAnchor, zoom);

            Assert.Equal(1UL, selection.SelectedEntityId);
        }
    }

    /// <summary>
    /// The target still has an edge: a click a long way past the head misses.
    /// A pick radius that never misses would make empty ground select
    /// whichever warrior happened to be nearest.
    /// </summary>
    [Theory]
    [MemberData(nameof(CameraZooms))]
    public void ClickWellClearOfTheBody_SelectsNothing(float zoom)
    {
        var selection = new AgentSelection();
        AgentView[] agents = [CreateAgent(1, xRaw: 0, yRaw: 0)];

        Select(
            selection,
            agents,
            BodyCentre(zoom) + AgentPickTarget.RadiusPixels(zoom) + 2f,
            zoom);

        Assert.Null(selection.SelectedEntityId);
    }

    [Theory]
    [MemberData(nameof(CameraZooms))]
    public void MaximumDistanceSquared_StaysInsideALong(float zoom)
    {
        // The multiplication ArenaGame.SelectAtPointer performs, checked, so
        // an overflow at either extreme of the zoom range fails here first.
        var maximumDistanceSquared = checked(RadiusRaw(zoom) * RadiusRaw(zoom));

        Assert.True(maximumDistanceSquared > 0);
    }

    private static float BodyCentre(float zoom) =>
        AgentPickTarget.BodyCentrePixels(zoom);

    private static long RadiusRaw(float zoom) =>
        (long)Math.Ceiling(
            AgentPickTarget.RadiusWorldUnits(zoom) * FixedPoint.Scale);

    /// <summary>
    /// Runs the exact sequence <c>ArenaGame.SelectAtPointer</c> runs, for a
    /// pointer sitting <paramref name="pixelsAboveAnchor"/> screen pixels
    /// above a warrior standing at the world origin. Screen Y grows downward
    /// and a pawn draws upward from its anchor, so above the anchor is a
    /// negative world Y.
    /// </summary>
    private static void Select(
        AgentSelection selection,
        AgentView[] agents,
        float pixelsAboveAnchor,
        float zoom)
    {
        var pointerWorld = new Vector2(0f, -pixelsAboveAnchor / zoom);
        var sample = AgentPickTarget.SamplePoint(pointerWorld, zoom);
        var radiusRaw = RadiusRaw(zoom);

        selection.SelectNearest(
            agents,
            ToRaw(sample.X),
            ToRaw(sample.Y),
            radiusRaw * radiusRaw);
    }

    private static int ToRaw(float worldCoordinate) =>
        (int)Math.Round(
            (double)worldCoordinate * FixedPoint.Scale,
            MidpointRounding.AwayFromZero);

    private static AgentView CreateAgent(
        ulong entityId,
        int xRaw,
        int yRaw) =>
        new(
            entityId,
            FactionId: 0,
            xRaw,
            yRaw,
            HitPoints: 100,
            MaximumHitPoints: 100,
            TargetEntityId: null,
            Intent: AgentIntent.Idle,
            IsAlive: true,
            Loadout: new CombatLoadout(
                WeaponId.Kampilan,
                ArmorId.LightOrganic,
                ShieldId.TallHardwood));
}
