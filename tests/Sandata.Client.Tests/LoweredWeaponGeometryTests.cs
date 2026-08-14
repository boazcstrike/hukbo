using Microsoft.Xna.Framework;
using Sandata.Client.Rendering;
using Sandata.Core.Mathematics;
using Sandata.Core.Weapons;

namespace Sandata.Client.Tests;

/// <summary>
/// Smoke row <c>SD-4</c>'s rendering half, from the 2026-08-12 Sandata
/// order and combat legibility plan: a lowered
/// weapon has to look different from a raised one. Design section 9 makes the
/// weapon-lowered rule the mechanical core of the product — a pistol beats a
/// rifle in a doorway because the rifle has to come back up — and until this
/// change nothing on screen distinguished the two states, so the row could
/// not be judged even by a person watching the exact doorway it names.
/// </summary>
/// <remarks>
/// Nothing here constructs a <c>GraphicsDevice</c>, a <c>SpriteBatch</c>, or a
/// window: <see cref="OperatorGeometry.Create"/> is a pure function of plain
/// values, the same discipline <c>OperatorGeometryTests</c> follows.
/// </remarks>
public sealed class LoweredWeaponGeometryTests
{
    private static readonly Vector2 RootPosition = new(100f, 100f);
    private const float ApparentScale = 1f;

    private static OperatorLayout Create(bool isWeaponLowered, WeaponClass weaponClass = WeaponClass.Rifle) =>
        OperatorGeometry.Create(
            rootPosition: RootPosition,
            apparentScale: ApparentScale,
            detailTier: OperatorDetailTier.High,
            weaponAimBam: new Bam16(0),
            previousDisplayRotationRawUnits: 0f,
            smoothingFactor: 1f,
            isFiring: false,
            isSelected: false,
            isFriendly: true,
            weaponClass: weaponClass,
            isWeaponLowered: isWeaponLowered);

    /// <summary>
    /// The default is byte-identical to the layout this class produced before
    /// the parameter existed, which is what lets every pinned rectangle in
    /// <c>OperatorGeometryTests</c> stay pinned.
    /// </summary>
    [Fact]
    public void Create_NotLowered_MatchesTheLayoutWithNoLoweredArgumentAtAll()
    {
        var explicitlyRaised = Create(isWeaponLowered: false);

        var byOmission = OperatorGeometry.Create(
            rootPosition: RootPosition,
            apparentScale: ApparentScale,
            detailTier: OperatorDetailTier.High,
            weaponAimBam: new Bam16(0),
            previousDisplayRotationRawUnits: 0f,
            smoothingFactor: 1f,
            isFiring: false,
            isSelected: false);

        Assert.Equal(byOmission, explicitlyRaised);
    }

    /// <summary>
    /// The muzzle is the layer a spectator actually tracks, so it is the one
    /// that has to move: a lowered weapon's muzzle anchor leaves the aim line
    /// entirely rather than merely shortening along it.
    /// </summary>
    [Fact]
    public void Create_Lowered_SwingsTheMuzzleOffTheAimLine()
    {
        var raised = Create(isWeaponLowered: false);
        var lowered = Create(isWeaponLowered: true);

        Assert.Equal(raised.WeaponGripAnchor, lowered.WeaponGripAnchor);
        Assert.NotEqual(raised.WeaponMuzzleAnchor, lowered.WeaponMuzzleAnchor);

        // The raised muzzle sits on the grip's own Y, because the aim angle is
        // zero. The lowered one must not, or the weapon has only shortened.
        Assert.Equal(raised.WeaponGripAnchor.Y, raised.WeaponMuzzleAnchor.Y);
        Assert.NotEqual(raised.WeaponGripAnchor.Y, lowered.WeaponMuzzleAnchor.Y);
    }

    /// <summary>
    /// And it has to shorten as well as turn: rotation alone still reads as a
    /// weapon pointing somewhere at the zoom a spectator plays at, which is
    /// the reason <see cref="OperatorGeometry.LoweredWeaponLengthScale"/>
    /// exists alongside the rotation.
    /// </summary>
    [Fact]
    public void Create_Lowered_ShortensTheWeaponBody()
    {
        var raised = Create(isWeaponLowered: false);
        var lowered = Create(isWeaponLowered: true);

        var raisedLongestSide = Math.Max(raised.WeaponBodyBounds.Width, raised.WeaponBodyBounds.Height);
        var loweredLongestSide = Math.Max(lowered.WeaponBodyBounds.Width, lowered.WeaponBodyBounds.Height);

        Assert.True(
            loweredLongestSide < raisedLongestSide,
            $"a lowered weapon body's longest side was {loweredLongestSide}, " +
            $"not shorter than the raised {raisedLongestSide}");
    }

    /// <summary>
    /// A lowered weapon does not fire, so its muzzle flash layer cannot draw
    /// even when the caller reports the operator as firing. Without this a
    /// flash could be rendered at a muzzle pointing at the floor.
    /// </summary>
    [Fact]
    public void Create_LoweredAndFiring_DrawsNoMuzzleFlash()
    {
        var layout = OperatorGeometry.Create(
            rootPosition: RootPosition,
            apparentScale: ApparentScale,
            detailTier: OperatorDetailTier.High,
            weaponAimBam: new Bam16(0),
            previousDisplayRotationRawUnits: 0f,
            smoothingFactor: 1f,
            isFiring: true,
            isSelected: false,
            isFriendly: true,
            weaponClass: WeaponClass.Rifle,
            isWeaponLowered: true);

        Assert.Equal(Rectangle.Empty, layout.MuzzleFlashBounds);
    }

    /// <summary>
    /// The body layers are untouched: only the weapon comes down. A change
    /// that moved the operator itself would read as a different pose rather
    /// than as a lowered weapon.
    /// </summary>
    [Fact]
    public void Create_Lowered_LeavesEveryBodyLayerWhereItWas()
    {
        var raised = Create(isWeaponLowered: false);
        var lowered = Create(isWeaponLowered: true);

        Assert.Equal(raised.GroundRingBounds, lowered.GroundRingBounds);
        Assert.Equal(raised.BootsBounds, lowered.BootsBounds);
        Assert.Equal(raised.LegsBounds, lowered.LegsBounds);
        Assert.Equal(raised.TorsoBounds, lowered.TorsoBounds);
        Assert.Equal(raised.HeadBounds, lowered.HeadBounds);
        Assert.Equal(raised.HeadPipBounds, lowered.HeadPipBounds);
        Assert.Equal(raised.HelmetBounds, lowered.HelmetBounds);
    }

    /// <summary>
    /// The pistol path lowers too when it is asked to. The simulation never
    /// asks — <c>FirearmDefinition.ExemptFromLoweredRule</c> is true for every
    /// pistol row, which is exactly the asymmetry <c>SD-4</c> puts a person in
    /// front of — but the geometry must not silently ignore the flag, or a
    /// future exempt-rule change would be invisible.
    /// </summary>
    [Fact]
    public void Create_LoweredPistol_AlsoSwingsOffTheAimLine()
    {
        var raised = Create(isWeaponLowered: false, weaponClass: WeaponClass.Pistol);
        var lowered = Create(isWeaponLowered: true, weaponClass: WeaponClass.Pistol);

        Assert.NotEqual(raised.WeaponMuzzleAnchor, lowered.WeaponMuzzleAnchor);
    }
}
