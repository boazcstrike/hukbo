using Hukbo.Client.Presentation;
using Hukbo.Client.Rendering;

namespace Hukbo.Client.Tests;

public sealed class HitEffectGeometryTests
{
    [Fact]
    public void Create_RepeatedCallsReturnIdenticalRingAndShardGeometry()
    {
        var effect = Effect(sequence: 12, targetEntityId: 7);

        var first = HitEffectGeometry.Create(effect, cameraZoom: 1f);
        var second = HitEffectGeometry.Create(effect, cameraZoom: 1f);

        Assert.Equal(first, second);
        Assert.Equal(Shards(first), Shards(second));
    }

    [Fact]
    public void Create_SequenceOrTargetChangesStartingAngle()
    {
        var baseline = HitEffectGeometry.Create(
            Effect(sequence: 12, targetEntityId: 7),
            cameraZoom: 1f);
        var changedSequence = HitEffectGeometry.Create(
            Effect(sequence: 13, targetEntityId: 7),
            cameraZoom: 1f);
        var changedTarget = HitEffectGeometry.Create(
            Effect(sequence: 12, targetEntityId: 8),
            cameraZoom: 1f);

        Assert.NotEqual(baseline.StartAngle, changedSequence.StartAngle);
        Assert.NotEqual(baseline.StartAngle, changedTarget.StartAngle);
    }

    [Fact]
    public void Create_OrdinaryEffectsHaveOneRingAndFourToSixShards()
    {
        var layout = HitEffectGeometry.Create(
            Effect(isLethal: false),
            cameraZoom: 1f);

        Assert.Equal(1, layout.RingCount);
        Assert.InRange(layout.ShardCount, 4, 6);
        Assert.Equal(layout.ShardCount, layout.VisibleShardCount);
    }

    [Fact]
    public void Create_LethalEffectsHaveTwoRingsAndTwelveLongerShards()
    {
        var ordinary = HitEffectGeometry.Create(
            Effect(isLethal: false),
            cameraZoom: 1f);
        var lethal = HitEffectGeometry.Create(
            Effect(isLethal: true),
            cameraZoom: 1f);

        Assert.Equal(2, lethal.RingCount);
        Assert.Equal(12, lethal.ShardCount);
        Assert.Equal(12, lethal.VisibleShardCount);
        Assert.True(lethal.ShardTravel > ordinary.ShardTravel);
        Assert.True(lethal.ShardLength > ordinary.ShardLength);
    }

    [Fact]
    public void Create_ScaleIsMonotonicAndClampedAtZoomExtremes()
    {
        var effect = Effect();
        var minimum = HitEffectGeometry.Create(effect, cameraZoom: 0.05f);
        var belowMinimum = HitEffectGeometry.Create(effect, cameraZoom: 0f);
        var normal = HitEffectGeometry.Create(effect, cameraZoom: 1f);
        var maximum = HitEffectGeometry.Create(effect, cameraZoom: 12f);
        var aboveMaximum = HitEffectGeometry.Create(effect, cameraZoom: 24f);

        Assert.Equal(belowMinimum.ApparentScale, minimum.ApparentScale);
        Assert.True(minimum.ApparentScale <= normal.ApparentScale);
        Assert.True(normal.ApparentScale <= maximum.ApparentScale);
        Assert.Equal(maximum.ApparentScale, aboveMaximum.ApparentScale);
    }

    [Fact]
    public void Create_LowDetailSuppressesSecondaryOrdinaryShardsButKeepsRing()
    {
        var effect = Effect(isLethal: false);
        var lowDetail = HitEffectGeometry.Create(effect, cameraZoom: 0.05f);
        var normalDetail = HitEffectGeometry.Create(effect, cameraZoom: 1f);

        Assert.Equal(1, lowDetail.RingCount);
        Assert.InRange(lowDetail.VisibleShardCount, 2, 3);
        Assert.True(lowDetail.VisibleShardCount < lowDetail.ShardCount);
        Assert.Equal(normalDetail.ShardCount, normalDetail.VisibleShardCount);
    }

    [Fact]
    public void Create_ClampsProgressAlphaAndPulse()
    {
        var beforeStart = HitEffectGeometry.Create(
            Effect(ageSeconds: -1f),
            cameraZoom: 1f);
        var afterEnd = HitEffectGeometry.Create(
            Effect(ageSeconds: 10f),
            cameraZoom: 1f);

        Assert.Equal(0f, beforeStart.Progress);
        Assert.InRange(beforeStart.Alpha, 0f, 1f);
        Assert.InRange(beforeStart.Pulse, 0f, 1f);
        Assert.Equal(1f, afterEnd.Progress);
        Assert.InRange(afterEnd.Alpha, 0f, 1f);
        Assert.InRange(afterEnd.Pulse, 0f, 1f);
    }

    [Fact]
    public void Create_RejectsInvalidCameraZoom()
    {
        var effect = Effect();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => HitEffectGeometry.Create(effect, cameraZoom: -0.1f));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => HitEffectGeometry.Create(effect, cameraZoom: float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => HitEffectGeometry.Create(
                effect,
                cameraZoom: float.PositiveInfinity));
    }

    private static HitEffect Effect(
        long sequence = 12,
        ulong targetEntityId = 7,
        bool isLethal = false,
        float ageSeconds = 0.05f) =>
        new(
            sequence,
            targetEntityId,
            XRaw: 1200,
            YRaw: 3400,
            Damage: 18,
            isLethal,
            ageSeconds);

    private static HitEffectShard[] Shards(HitEffectLayout layout) =>
        Enumerable.Range(0, layout.VisibleShardCount)
            .Select(layout.GetShard)
            .ToArray();
}
