using Hukbo.Client.Rendering;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Presentation;

/// <summary>
/// Where a click has to land for it to select a warrior, and how close it has
/// to be.
/// </summary>
/// <remarks>
/// <para>
/// The click target used to be a disc of about five screen pixels centred on
/// the agent's own world position, computed from the camera zoom alone
/// (<c>MathF.Max(5f / zoom, 1.5f)</c> world units). That had two independent
/// problems, and smoke row <c>V2-3</c> is the record of a person hitting both
/// at once and giving up: the disc was far smaller than the drawn warrior at
/// every zoom, and it sat at the warrior's feet while the part of the warrior
/// a spectator aims at — the torso and the head — is drawn entirely above
/// that point. Clicking what you can see selected nothing.
/// </para>
/// <para>
/// Both halves are fixed here by deriving the target from the same geometry
/// the renderer draws. <see cref="PawnGeometry.ResolveApparentScale"/> is the
/// scale every pawn layout length is multiplied by, so a target expressed in
/// the same layout units tracks the drawn body through the whole zoom range
/// instead of drifting away from it.
/// </para>
/// <para>
/// This is presentation only. Selection changes what the inspector shows and
/// nothing else; no simulation state, hash, or event depends on it.
/// </para>
/// </remarks>
internal static class AgentPickTarget
{
    /// <summary>
    /// The neutral height of a drawn pawn above its foot anchor, in layout
    /// units at unit apparent scale. The sum of
    /// <c>PawnGeometry</c>'s own vertical stack: <c>7.5</c> units of leg
    /// (the torso's bottom gap), <c>8</c> units of torso, <c>1</c> unit of
    /// head gap, and <c>7</c> units of head. Deliberately excludes the
    /// weapon line, which reaches much further and would make a click on
    /// empty ground beside a warrior select it.
    /// </summary>
    internal const float BodyHeightUnits = 23.5f;

    /// <summary>
    /// The smallest the target may get, in screen pixels, however far the
    /// camera is pulled back. At minimum apparent scale the body-derived
    /// radius is only about <c>8.5</c> pixels, and a target that small is
    /// the failure this type exists to fix.
    /// </summary>
    internal const float MinimumRadiusPixels = 10f;

    /// <summary>
    /// How far above the foot anchor the drawn body's centre sits, in screen
    /// pixels at <paramref name="cameraZoom"/>.
    /// </summary>
    internal static float BodyCentrePixels(float cameraZoom) =>
        BodyHeightUnits / 2f * PawnGeometry.ResolveApparentScale(cameraZoom);

    /// <summary>
    /// The radius of the click target, in screen pixels at
    /// <paramref name="cameraZoom"/>. Half the drawn body's height, so a disc
    /// centred on <see cref="BodyCentrePixels"/> spans foot to head, floored
    /// at <see cref="MinimumRadiusPixels"/>.
    /// </summary>
    internal static float RadiusPixels(float cameraZoom) =>
        MathF.Max(
            BodyHeightUnits / 2f * PawnGeometry.ResolveApparentScale(cameraZoom),
            MinimumRadiusPixels);

    /// <summary>
    /// <see cref="RadiusPixels"/> in world units, which is what
    /// <see cref="AgentSelection.SelectNearest"/> measures in.
    /// </summary>
    internal static float RadiusWorldUnits(float cameraZoom) =>
        RadiusPixels(cameraZoom) / cameraZoom;

    /// <summary>
    /// The world point to measure agent distances from, given the world point
    /// the pointer is actually over. Shifted down by the body-centre offset
    /// so that a click on a warrior's chest measures as a click on that
    /// warrior's foot anchor, which is the position
    /// <c>AgentView.XRaw</c>/<c>YRaw</c> carries. Screen Y grows downward and
    /// a pawn draws upward from its anchor, so the shift is positive.
    /// </summary>
    internal static Vector2 SamplePoint(Vector2 pointerWorld, float cameraZoom) =>
        new(
            pointerWorld.X,
            pointerWorld.Y + (BodyCentrePixels(cameraZoom) / cameraZoom));
}
