using Hukbo.Client.Presentation;
using Hukbo.Core.Combat;

namespace Hukbo.Client.Rendering;

/// <summary>
/// One droplet or spurt strand, expressed as a line segment relative to the
/// spray origin. Both spray forms share this shape because both are drawn as a
/// single stretched one-pixel quad.
/// </summary>
internal readonly record struct BloodDroplet(
    float StartX,
    float StartY,
    float EndX,
    float EndY,
    float Thickness,
    float Alpha);

/// <summary>
/// One filled circle of a ground mark, positioned relative to the mark centre.
/// </summary>
internal readonly record struct BloodBlot(
    float OffsetX,
    float OffsetY,
    float Radius,
    float Alpha);

/// <summary>
/// The complete draw-time expansion of a single <see cref="BloodBurst"/>.
/// Nothing beyond this record is stored per droplet: <see cref="GetDroplet"/>
/// re-derives each one from <see cref="Seed"/>, so the buffer cost stays one
/// record per blow.
/// </summary>
internal readonly record struct BloodBurstLayout(
    int DropletCount,
    int VisibleDropletCount,
    float ApparentScale,
    float Progress,
    float Alpha,
    float DirectionAngle,
    float SpreadAngle,
    float Travel,
    float DropletLength,
    float Thickness,
    float OriginOffsetY,
    float BoundingRadius,
    ulong Seed)
{
    private const float MinimumSpeedFactor = 0.62f;
    private const float SpeedSeedRange = 0.38f;
    private const float StartDistanceFactor = 0.14f;
    private const float LengthDecayFactor = 0.45f;
    private const int SpeedSeedShift = 24;

    public BloodDroplet GetDroplet(int visibleIndex)
    {
        if ((uint)visibleIndex >= (uint)VisibleDropletCount)
        {
            throw new ArgumentOutOfRangeException(nameof(visibleIndex));
        }

        var dropletIndex = VisibleDropletCount == DropletCount
            ? visibleIndex
            : visibleIndex * 2;
        var seed = BloodGeometry.MixDroplet(Seed, dropletIndex);
        var angle = DirectionAngle +
            ((BloodGeometry.SeedUnit(seed) - 0.5f) * SpreadAngle);
        var speed = MinimumSpeedFactor +
            (SpeedSeedRange * BloodGeometry.SeedUnit(seed >> SpeedSeedShift));
        var directionX = MathF.Cos(angle);
        var directionY = MathF.Sin(angle);
        var startDistance = (Travel * StartDistanceFactor) +
            (Travel * speed * Progress);
        var currentLength = DropletLength *
            (1f - (Progress * LengthDecayFactor));
        var endDistance = startDistance + currentLength;

        return new BloodDroplet(
            directionX * startDistance,
            directionY * startDistance,
            directionX * endDistance,
            directionY * endDistance,
            Thickness,
            Alpha);
    }
}

/// <summary>
/// The draw-time expansion of a single <see cref="GroundMark"/> into at most
/// three blots.
/// </summary>
internal readonly record struct GroundMarkLayout(
    int BlotCount,
    int VisibleBlotCount,
    float ApparentScale,
    float Progress,
    float Alpha,
    float Radius,
    float BoundingRadius,
    ulong Seed)
{
    private const float SatelliteDistanceFactor = 0.85f;
    private const float SatelliteRadiusFactor = 0.55f;
    private const float SatelliteAlphaFactor = 0.78f;

    public BloodBlot GetBlot(int visibleIndex)
    {
        if ((uint)visibleIndex >= (uint)VisibleBlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(visibleIndex));
        }

        if (visibleIndex == 0)
        {
            return new BloodBlot(0f, 0f, Radius, Alpha);
        }

        var seed = BloodGeometry.MixDroplet(Seed, visibleIndex);
        var angle = BloodGeometry.SeedUnit(seed) * MathF.Tau;
        var distance = Radius * SatelliteDistanceFactor;

        return new BloodBlot(
            MathF.Cos(angle) * distance,
            MathF.Sin(angle) * distance,
            Radius * SatelliteRadiusFactor,
            Alpha * SatelliteAlphaFactor);
    }
}

/// <summary>
/// The draw-time expansion of a single <see cref="LethalSpurt"/>. The pulse is
/// a cosine of the record's own progress, never of the wall clock, so two
/// playbacks of the same blow pulse identically.
/// </summary>
internal readonly record struct LethalSpurtLayout(
    int StrandCount,
    int VisibleStrandCount,
    float ApparentScale,
    float Progress,
    float Alpha,
    float DirectionAngle,
    float SpreadAngle,
    float Length,
    float Thickness,
    float BoundingRadius,
    ulong Seed)
{
    private const float PulseCycles = 3f;
    private const float MinimumPulse = 0.55f;
    private const float PulseRange = 0.45f;
    private const float StartDistanceFactor = 0.18f;
    private const float MinimumLengthFactor = 0.55f;
    private const float LengthSeedRange = 0.45f;
    private const int LengthSeedShift = 24;

    public BloodDroplet GetStrand(int visibleIndex)
    {
        if ((uint)visibleIndex >= (uint)VisibleStrandCount)
        {
            throw new ArgumentOutOfRangeException(nameof(visibleIndex));
        }

        var strandIndex = VisibleStrandCount == StrandCount
            ? visibleIndex
            : visibleIndex * 2;
        var seed = BloodGeometry.MixDroplet(Seed, strandIndex);
        var angle = DirectionAngle +
            ((BloodGeometry.SeedUnit(seed) - 0.5f) * SpreadAngle);
        var pulse = MinimumPulse +
            (PulseRange * MathF.Cos(Progress * MathF.Tau * PulseCycles));
        var lengthFactor = MinimumLengthFactor +
            (LengthSeedRange *
             BloodGeometry.SeedUnit(seed >> LengthSeedShift));
        var directionX = MathF.Cos(angle);
        var directionY = MathF.Sin(angle);
        var startDistance = Length * StartDistanceFactor;
        var endDistance = startDistance + (Length * lengthFactor * pulse);

        return new BloodDroplet(
            directionX * startDistance,
            directionY * startDistance,
            directionX * endDistance,
            directionY * endDistance,
            Thickness,
            Alpha);
    }
}

/// <summary>
/// Pure layout computation for every blood record. Every value is a function of
/// the record's authoritative identifiers and the camera zoom only: no
/// <c>System.Random</c>, no wall clock, no frame counter, and no
/// <c>Hukbo.Core</c> RNG.
/// </summary>
internal static class BloodGeometry
{
    private const float MinimumApparentScale = 0.72f;
    private const float MaximumApparentScale = 2.40f;
    private const float ZoomScale = 1.35f;

    /// <summary>
    /// Blood's own low-detail threshold. Deliberately below
    /// <c>HitEffectGeometry</c>'s 0.95 so the default fit view, whose apparent
    /// scale sits near 0.79 at 1280x720, still draws every droplet. Below this
    /// threshold droplet and blot counts halve; they never reach zero.
    /// </summary>
    private const float LowDetailScale = 0.76f;

    private const int MaximumDropletCount = 8;
    private const int LethalDropletBonus = 1;
    private const int SeverityDropletReduction = 3;
    private const float LethalTravelMultiplier = 1.35f;
    private const float LethalThicknessMultiplier = 1.25f;

    private const int OrdinaryBlotCount = 2;
    private const int DenseBlotCount = 3;
    private const float OrdinaryMarkRadius = 3.4f;
    private const float LethalMarkRadius = 5.6f;
    private const float OrdinaryMarkAlpha = 0.62f;
    private const float LethalMarkAlpha = 0.85f;
    private const float MarkGrowthFactor = 0.28f;
    private const float MarkBoundingFactor = 2.2f;

    private const int SpurtStrandCount = 8;
    private const float SpurtSpreadAngle = 0.55f;
    private const float SpurtLength = 17f;
    private const float SpurtThickness = 1.8f;
    private const float SpurtSeverityFloor = 0.6f;
    private const float SpurtSeveritySpan = 0.4f;

    private const ulong SequenceKey = 0x9E3779B97F4A7C15UL;
    private const ulong TargetKey = 0xD1B54A32D192ED03UL;
    private const ulong SourceKey = 0xA24BAED4963EE407UL;
    private const ulong DropletKey = 0x2545F4914F6CDD1DUL;
    private const ulong GroundMarkKey = 0x9FB21C651E98DF25UL;
    private const ulong SpurtKey = 0xC2B2AE3D27D4EB4FUL;

    public static BloodBurstLayout Create(BloodBurst burst, float cameraZoom)
    {
        var apparentScale = CreateApparentScale(cameraZoom);
        var progress = Math.Clamp(
            burst.AgeSeconds / burst.LifetimeSeconds,
            0f,
            1f);
        var severity = Math.Clamp(burst.SeverityRatio, 0f, 1f);
        var profile = GetSprayProfile(burst.Weapon);
        var seed = CreateBurstSeed(
            burst.Sequence,
            burst.SourceEntityId,
            burst.TargetEntityId);
        var dropletCount = Math.Clamp(
            profile.DropletCount -
                (int)MathF.Round(
                    SeverityDropletReduction * (1f - severity),
                    MidpointRounding.AwayFromZero) +
                (burst.IsLethal ? LethalDropletBonus : 0),
            1,
            MaximumDropletCount);
        var visibleDropletCount = apparentScale < LowDetailScale
            ? (dropletCount + 1) / 2
            : dropletCount;
        var travel = profile.Travel * apparentScale *
            (burst.IsLethal ? LethalTravelMultiplier : 1f);
        var dropletLength = profile.DropletLength * apparentScale;
        var thickness = MathF.Max(
            1f,
            profile.Thickness * apparentScale *
                (burst.IsLethal ? LethalThicknessMultiplier : 1f));

        return new BloodBurstLayout(
            dropletCount,
            visibleDropletCount,
            apparentScale,
            progress,
            Alpha: Math.Clamp(1f - progress, 0f, 1f),
            DirectionAngle: CreateDirectionAngle(
                burst.DirectionX,
                burst.DirectionY,
                seed),
            profile.SpreadAngle,
            travel,
            dropletLength,
            thickness,
            OriginOffsetY: GetOriginHeight(burst.HitLocation) * apparentScale,
            BoundingRadius: travel + dropletLength + thickness,
            seed);
    }

    public static GroundMarkLayout CreateGroundMark(
        GroundMark mark,
        float cameraZoom)
    {
        var apparentScale = CreateApparentScale(cameraZoom);
        var progress = Math.Clamp(
            mark.AgeSeconds / mark.LifetimeSeconds,
            0f,
            1f);
        var blotCount = mark.IsDense ? DenseBlotCount : OrdinaryBlotCount;
        var radius = (mark.IsLethal ? LethalMarkRadius : OrdinaryMarkRadius) *
            apparentScale *
            (1f + (MarkGrowthFactor * progress));

        return new GroundMarkLayout(
            blotCount,
            VisibleBlotCount: apparentScale < LowDetailScale
                ? (blotCount + 1) / 2
                : blotCount,
            apparentScale,
            progress,
            Alpha: Math.Clamp(1f - progress, 0f, 1f) *
                (mark.IsLethal ? LethalMarkAlpha : OrdinaryMarkAlpha),
            radius,
            BoundingRadius: radius * MarkBoundingFactor,
            Seed: Mix((ulong)mark.Sequence + GroundMarkKey));
    }

    public static LethalSpurtLayout CreateSpurt(
        LethalSpurt spurt,
        float cameraZoom)
    {
        var apparentScale = CreateApparentScale(cameraZoom);
        var progress = Math.Clamp(
            spurt.AgeSeconds / spurt.LifetimeSeconds,
            0f,
            1f);
        var severity = Math.Clamp(spurt.SeverityRatio, 0f, 1f);
        var seed = Mix(
            CreateBurstSeed(
                spurt.Sequence,
                spurt.SourceEntityId,
                spurt.TargetEntityId) + SpurtKey);
        var length = SpurtLength * apparentScale *
            (SpurtSeverityFloor + (SpurtSeveritySpan * severity));
        var thickness = MathF.Max(1f, SpurtThickness * apparentScale);

        return new LethalSpurtLayout(
            SpurtStrandCount,
            VisibleStrandCount: apparentScale < LowDetailScale
                ? (SpurtStrandCount + 1) / 2
                : SpurtStrandCount,
            apparentScale,
            progress,
            Alpha: Math.Clamp(1f - progress, 0f, 1f),
            DirectionAngle: CreateDirectionAngle(
                spurt.DirectionX,
                spurt.DirectionY,
                seed),
            SpurtSpreadAngle,
            length,
            thickness,
            BoundingRadius: length + thickness,
            seed);
    }

    /// <summary>
    /// Derives one droplet, blot, or strand seed from its burst seed. Shared
    /// with the layout records, which re-derive each primitive at draw time
    /// instead of storing it.
    /// </summary>
    public static ulong MixDroplet(ulong burstSeed, int index) =>
        Mix(burstSeed + ((ulong)(uint)index * DropletKey));

    /// <summary>
    /// Projects a seed onto the unit interval. Shared with the layout records
    /// for the same reason as <see cref="MixDroplet"/>.
    /// </summary>
    public static float SeedUnit(ulong seed)
    {
        const float UnitScale = 1f / 16777216f;
        return (seed & 0xFFFFFFUL) * UnitScale;
    }

    private static float CreateApparentScale(float cameraZoom)
    {
        if (!float.IsFinite(cameraZoom) || cameraZoom < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(cameraZoom));
        }

        return Math.Clamp(
            cameraZoom * ZoomScale,
            MinimumApparentScale,
            MaximumApparentScale);
    }

    private static float CreateDirectionAngle(
        float directionX,
        float directionY,
        ulong seed) =>
        directionX == 0f && directionY == 0f
            ? SeedUnit(seed) * MathF.Tau
            : MathF.Atan2(directionY, directionX);

    /// <summary>
    /// How far above the victim's foot anchor a spray starts, in unscaled
    /// pixels. Hit location shifts the origin on the silhouette and nothing
    /// else: it never produces mutilation, per the design's §3.8.
    /// </summary>
    private static float GetOriginHeight(BodyPart hitLocation) =>
        hitLocation switch
        {
            BodyPart.Head or BodyPart.Face or BodyPart.Neck => -17f,
            BodyPart.Shoulder => -13f,
            BodyPart.WeaponArm or BodyPart.ShieldArm => -12f,
            BodyPart.Chest => -11f,
            BodyPart.Hands => -9f,
            BodyPart.Abdomen => -8f,
            BodyPart.Thigh => -5f,
            BodyPart.Knee => -3f,
            BodyPart.Shin => -2f,
            BodyPart.Feet => -0.5f,
            _ => throw new ArgumentOutOfRangeException(
                nameof(hitLocation),
                hitLocation,
                null),
        };

    private static WeaponSprayProfile GetSprayProfile(WeaponId weapon) =>
        weapon switch
        {
            // A great blade throws a broad even arc; the chopper's mass throws
            // the widest, heaviest, shortest spray; the thrusting blade throws
            // a narrow far one; the work blade throws a short modest one.
            WeaponId.Kampilan => new WeaponSprayProfile(6, 1.15f, 15f, 5f, 1.6f),
            WeaponId.Wasay =>
                new WeaponSprayProfile(7, 1.55f, 12f, 6.5f, 2.1f),
            WeaponId.Kalis =>
                new WeaponSprayProfile(4, 0.45f, 20f, 7f, 1.2f),
            WeaponId.Itak => new WeaponSprayProfile(5, 0.95f, 11f, 4f, 1.4f),

            // PROVISIONAL tuning, not a historical or medical measurement.
            // All three are punctures delivered from a distance rather than
            // cuts delivered in contact, so they read narrower than any melee
            // arc; the arquebus is the exception, because a heavy lead ball
            // is the most destructive impact in the roster. The thrown spear
            // sits between the Kalis and the Kampilan, the arrow is the
            // smallest spray in the game, and the ball is the widest and
            // heaviest.
            //
            // These arms exist at all because the switch had none: it
            // covered the melee four and threw
            // ArgumentOutOfRangeException on every ranged hit that drew
            // blood, which crashed the client at tick 66 of a V5/V8 battle.
            // The ranged package never touched this file — its plan does not
            // mention blood anywhere.
            WeaponId.Bangkaw =>
                new WeaponSprayProfile(4, 0.55f, 18f, 6.5f, 1.3f),
            WeaponId.Busog => new WeaponSprayProfile(3, 0.4f, 16f, 5.5f, 1f),
            WeaponId.Arquebus =>
                new WeaponSprayProfile(7, 1.5f, 14f, 7f, 2.2f),
            _ => throw new ArgumentOutOfRangeException(
                nameof(weapon),
                weapon,
                null),
        };

    private static ulong CreateBurstSeed(
        long sequence,
        ulong sourceEntityId,
        ulong targetEntityId) =>
        Mix((ulong)sequence + SequenceKey) ^
        Mix(targetEntityId + TargetKey) ^
        Mix(sourceEntityId + SourceKey);

    /// <summary>
    /// Delegates to <see cref="PresentationHash.Mix"/>, which is this exact
    /// finalizer lifted out when embedded projectiles became its second
    /// consumer. Same value in, same value out — every burst, spurt, and
    /// ground mark draws exactly as it did before the lift.
    /// </summary>
    private static ulong Mix(ulong value) => PresentationHash.Mix(value);

    private readonly record struct WeaponSprayProfile(
        int DropletCount,
        float SpreadAngle,
        float Travel,
        float DropletLength,
        float Thickness);
}
