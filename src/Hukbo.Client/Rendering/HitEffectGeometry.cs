using Hukbo.Client.Presentation;

namespace Hukbo.Client.Rendering;

internal readonly record struct HitEffectShard(
    float StartX,
    float StartY,
    float EndX,
    float EndY,
    float Thickness,
    float Alpha);

internal readonly record struct HitEffectLayout(
    int RingCount,
    int ShardCount,
    int VisibleShardCount,
    float ApparentScale,
    float Progress,
    float Alpha,
    float Pulse,
    float PrimaryRingRadius,
    float SecondaryRingRadius,
    float RingThickness,
    float StartAngle,
    float ShardTravel,
    float ShardLength)
{
    public HitEffectShard GetShard(int visibleIndex)
    {
        if ((uint)visibleIndex >= (uint)VisibleShardCount)
        {
            throw new ArgumentOutOfRangeException(nameof(visibleIndex));
        }

        var shardIndex = VisibleShardCount == ShardCount
            ? visibleIndex
            : visibleIndex * 2;
        var angle = StartAngle +
            (MathF.Tau * shardIndex / ShardCount);
        var directionX = MathF.Cos(angle);
        var directionY = MathF.Sin(angle);
        var startDistance = (PrimaryRingRadius * 0.45f) +
            (ShardTravel * Progress);
        var currentLength = ShardLength * (1f - (Progress * 0.25f));
        var endDistance = startDistance + currentLength;

        return new HitEffectShard(
            directionX * startDistance,
            directionY * startDistance,
            directionX * endDistance,
            directionY * endDistance,
            RingThickness,
            Alpha);
    }
}

internal static class HitEffectGeometry
{
    private const float MinimumApparentScale = 0.72f;
    private const float MaximumApparentScale = 2.40f;
    private const float ZoomScale = 1.35f;
    private const float LowDetailScale = 0.95f;

    public static HitEffectLayout Create(
        HitEffect effect,
        float cameraZoom)
    {
        if (!float.IsFinite(cameraZoom) || cameraZoom < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(cameraZoom));
        }

        var apparentScale = Math.Clamp(
            cameraZoom * ZoomScale,
            MinimumApparentScale,
            MaximumApparentScale);
        var progress = Math.Clamp(
            effect.AgeSeconds / effect.LifetimeSeconds,
            0f,
            1f);
        var alpha = Math.Clamp(1f - progress, 0f, 1f);
        var pulse = Math.Clamp(1f - (progress * 2f), 0f, 1f);
        var seed = CreateSeed(effect.Sequence, effect.TargetEntityId);
        var shardCount = effect.IsLethal
            ? 8
            : 4 + (int)(seed % 3);
        var visibleShardCount = !effect.IsLethal &&
            apparentScale < LowDetailScale
            ? (shardCount + 1) / 2
            : shardCount;
        var ringStartRadius = (effect.IsLethal ? 8f : 6f) * apparentScale;
        var ringTravel = (effect.IsLethal ? 18f : 11f) * apparentScale;
        var primaryRingRadius = ringStartRadius + (ringTravel * progress);
        var secondaryRingRadius = effect.IsLethal
            ? (ringStartRadius * 0.65f) + (ringTravel * progress * 0.72f)
            : 0f;
        var startAngle = SeedAngle(seed);

        return new HitEffectLayout(
            RingCount: effect.IsLethal ? 2 : 1,
            shardCount,
            visibleShardCount,
            apparentScale,
            progress,
            alpha,
            pulse,
            primaryRingRadius,
            secondaryRingRadius,
            RingThickness: MathF.Max(
                1f,
                (effect.IsLethal ? 1.35f : 1f) * apparentScale),
            startAngle,
            ShardTravel: (effect.IsLethal ? 24f : 14f) * apparentScale,
            ShardLength: (effect.IsLethal ? 8f : 5f) * apparentScale);
    }

    private static ulong CreateSeed(long sequence, ulong targetEntityId) =>
        Mix((ulong)sequence + 0x9E3779B97F4A7C15UL) ^
        Mix(targetEntityId + 0xD1B54A32D192ED03UL);

    private static float SeedAngle(ulong seed)
    {
        const float UnitScale = 1f / 16777216f;
        var unit = (seed & 0xFFFFFFUL) * UnitScale;
        return (float)unit * MathF.Tau;
    }

    private static ulong Mix(ulong value)
    {
        value ^= value >> 30;
        value *= 0xBF58476D1CE4E5B9UL;
        value ^= value >> 27;
        value *= 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }
}
