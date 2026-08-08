using Hukbo.Client.Presentation;
using Hukbo.Client.Rendering;
using Hukbo.Core.Combat;

namespace Hukbo.Client.Tests;

public sealed class BloodGeometryTests
{
    private const float DefaultFitCameraZoom = 0.585f;

    [Fact]
    public void Create_RepeatedCallsReturnIdenticalDropletGeometry()
    {
        var burst = Burst(sequence: 12, sourceEntityId: 3, targetEntityId: 7);

        var first = BloodGeometry.Create(burst, cameraZoom: 1f);
        var second = BloodGeometry.Create(burst, cameraZoom: 1f);

        Assert.Equal(first, second);
        Assert.Equal(Droplets(first), Droplets(second));
    }

    [Fact]
    public void Create_SequenceSourceOrTargetChangesDropletGeometry()
    {
        var baseline = BloodGeometry.Create(
            Burst(sequence: 12, sourceEntityId: 3, targetEntityId: 7),
            cameraZoom: 1f);
        var changedSequence = BloodGeometry.Create(
            Burst(sequence: 13, sourceEntityId: 3, targetEntityId: 7),
            cameraZoom: 1f);
        var changedSource = BloodGeometry.Create(
            Burst(sequence: 12, sourceEntityId: 4, targetEntityId: 7),
            cameraZoom: 1f);
        var changedTarget = BloodGeometry.Create(
            Burst(sequence: 12, sourceEntityId: 3, targetEntityId: 8),
            cameraZoom: 1f);

        Assert.NotEqual(baseline.Seed, changedSequence.Seed);
        Assert.NotEqual(baseline.Seed, changedSource.Seed);
        Assert.NotEqual(baseline.Seed, changedTarget.Seed);
        Assert.NotEqual(Droplets(baseline), Droplets(changedSequence));
        Assert.NotEqual(Droplets(baseline), Droplets(changedSource));
        Assert.NotEqual(Droplets(baseline), Droplets(changedTarget));
    }

    [Fact]
    public void Create_SprayFollowsTheStoredAttackerToVictimDirection()
    {
        var rightward = BloodGeometry.Create(
            Burst(directionX: 1f, directionY: 0f),
            cameraZoom: 1f);
        var downward = BloodGeometry.Create(
            Burst(directionX: 0f, directionY: 1f),
            cameraZoom: 1f);

        Assert.Equal(0f, rightward.DirectionAngle, precision: 5);
        Assert.Equal(MathF.Tau / 4f, downward.DirectionAngle, precision: 5);
        Assert.All(
            Droplets(rightward),
            droplet => Assert.True(droplet.EndX > 0f));
        Assert.All(
            Droplets(downward),
            droplet => Assert.True(droplet.EndY > 0f));
    }

    [Fact]
    public void Create_WithoutADirectionFallsBackToASeededAngle()
    {
        var first = BloodGeometry.Create(
            Burst(sequence: 12, directionX: 0f, directionY: 0f),
            cameraZoom: 1f);
        var second = BloodGeometry.Create(
            Burst(sequence: 13, directionX: 0f, directionY: 0f),
            cameraZoom: 1f);

        Assert.InRange(first.DirectionAngle, 0f, MathF.Tau);
        Assert.NotEqual(first.DirectionAngle, second.DirectionAngle);
    }

    [Fact]
    public void Create_ShapeDiffersAcrossEveryWeaponClass()
    {
        var layouts = new[]
        {
            BloodGeometry.Create(Burst(weapon: WeaponId.Kampilan), 1f),
            BloodGeometry.Create(Burst(weapon: WeaponId.Wasay), 1f),
            BloodGeometry.Create(Burst(weapon: WeaponId.Kalis), 1f),
            BloodGeometry.Create(Burst(weapon: WeaponId.Itak), 1f),
        };

        Assert.Equal(4, layouts.Select(x => x.DropletCount).Distinct().Count());
        Assert.Equal(4, layouts.Select(x => x.SpreadAngle).Distinct().Count());
        Assert.Equal(4, layouts.Select(x => x.Travel).Distinct().Count());
    }

    [Fact]
    public void Create_LethalTierIsDistinctFromTheOrdinaryTier()
    {
        var ordinary = BloodGeometry.Create(Burst(isLethal: false), 1f);
        var lethal = BloodGeometry.Create(Burst(isLethal: true), 1f);

        Assert.True(lethal.DropletCount > ordinary.DropletCount);
        Assert.True(lethal.Travel > ordinary.Travel);
        Assert.True(
            Burst(isLethal: true).LifetimeSeconds >
            Burst(isLethal: false).LifetimeSeconds);
    }

    [Fact]
    public void Create_OverkillSeverityClampsToTheSeverityOneVisuals()
    {
        var full = BloodGeometry.Create(Burst(severityRatio: 1f), 1f);
        var overkill = BloodGeometry.Create(Burst(severityRatio: 4.5f), 1f);
        var negative = BloodGeometry.Create(Burst(severityRatio: -2f), 1f);
        var none = BloodGeometry.Create(Burst(severityRatio: 0f), 1f);

        Assert.Equal(full, overkill);
        Assert.Equal(Droplets(full), Droplets(overkill));
        Assert.Equal(none, negative);
        Assert.True(overkill.DropletCount > none.DropletCount);
    }

    [Fact]
    public void Create_HitLocationRaisesTheSprayOriginOnTheSilhouette()
    {
        var head = BloodGeometry.Create(Burst(hitLocation: BodyPart.Head), 1f);
        var chest = BloodGeometry.Create(Burst(hitLocation: BodyPart.Chest), 1f);
        var shin = BloodGeometry.Create(Burst(hitLocation: BodyPart.Shin), 1f);

        Assert.True(head.OriginOffsetY < chest.OriginOffsetY);
        Assert.True(chest.OriginOffsetY < shin.OriginOffsetY);
        Assert.True(shin.OriginOffsetY <= 0f);
    }

    [Fact]
    public void Create_AtTheDefaultFitViewEveryDropletStaysVisible()
    {
        var layout = BloodGeometry.Create(
            Burst(),
            cameraZoom: DefaultFitCameraZoom);

        Assert.True(layout.VisibleDropletCount > 0);
        Assert.Equal(layout.DropletCount, layout.VisibleDropletCount);
    }

    [Fact]
    public void Create_ScaleIsMonotonicAndClampedAtZoomExtremes()
    {
        var burst = Burst();
        var minimum = BloodGeometry.Create(burst, cameraZoom: 0.05f);
        var belowMinimum = BloodGeometry.Create(burst, cameraZoom: 0f);
        var normal = BloodGeometry.Create(burst, cameraZoom: 1f);
        var maximum = BloodGeometry.Create(burst, cameraZoom: 12f);
        var aboveMaximum = BloodGeometry.Create(burst, cameraZoom: 24f);

        Assert.Equal(belowMinimum.ApparentScale, minimum.ApparentScale);
        Assert.True(minimum.ApparentScale <= normal.ApparentScale);
        Assert.True(normal.ApparentScale <= maximum.ApparentScale);
        Assert.Equal(maximum.ApparentScale, aboveMaximum.ApparentScale);
    }

    [Fact]
    public void Create_LowDetailHalvesDropletsWithoutZeroingThem()
    {
        var burst = Burst();
        var lowDetail = BloodGeometry.Create(burst, cameraZoom: 0.05f);
        var normalDetail = BloodGeometry.Create(burst, cameraZoom: 1f);

        Assert.True(lowDetail.VisibleDropletCount > 0);
        Assert.True(lowDetail.VisibleDropletCount < lowDetail.DropletCount);
        Assert.Equal(
            normalDetail.DropletCount,
            normalDetail.VisibleDropletCount);
    }

    [Fact]
    public void Create_ClampsProgressAndAlpha()
    {
        var beforeStart = BloodGeometry.Create(Burst(ageSeconds: -1f), 1f);
        var afterEnd = BloodGeometry.Create(Burst(ageSeconds: 10f), 1f);

        Assert.Equal(0f, beforeStart.Progress);
        Assert.InRange(beforeStart.Alpha, 0f, 1f);
        Assert.Equal(1f, afterEnd.Progress);
        Assert.InRange(afterEnd.Alpha, 0f, 1f);
    }

    [Fact]
    public void Create_RejectsInvalidCameraZoom()
    {
        var burst = Burst();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => BloodGeometry.Create(burst, cameraZoom: -0.1f));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => BloodGeometry.Create(burst, cameraZoom: float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => BloodGeometry.Create(
                burst,
                cameraZoom: float.PositiveInfinity));
    }

    [Fact]
    public void GetDroplet_RejectsAnIndexOutsideTheVisibleRange()
    {
        var layout = BloodGeometry.Create(Burst(), cameraZoom: 1f);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => layout.GetDroplet(-1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => layout.GetDroplet(layout.VisibleDropletCount));
    }

    [Fact]
    public void CreateGroundMark_LethalMarkOutlivesThePairedBurst()
    {
        var lethalBurst = Burst(isLethal: true);
        var lethalMark = Mark(isLethal: true);
        var ordinaryMark = Mark(isLethal: false);

        Assert.True(lethalMark.LifetimeSeconds > lethalBurst.LifetimeSeconds);
        Assert.True(
            ordinaryMark.LifetimeSeconds > Burst(isLethal: false).LifetimeSeconds);
        Assert.True(lethalMark.LifetimeSeconds > ordinaryMark.LifetimeSeconds);
    }

    [Fact]
    public void CreateGroundMark_FadesSmoothlyInsteadOfPopping()
    {
        var lifetime = Mark(isLethal: true).LifetimeSeconds;
        var early = BloodGeometry.CreateGroundMark(
            Mark(isLethal: true, ageSeconds: lifetime * 0.1f),
            cameraZoom: 1f);
        var late = BloodGeometry.CreateGroundMark(
            Mark(isLethal: true, ageSeconds: lifetime * 0.9f),
            cameraZoom: 1f);
        var expired = BloodGeometry.CreateGroundMark(
            Mark(isLethal: true, ageSeconds: lifetime),
            cameraZoom: 1f);

        Assert.True(early.Alpha > late.Alpha);
        Assert.True(late.Alpha > 0f);
        Assert.Equal(0f, expired.Alpha);
    }

    [Fact]
    public void CreateGroundMark_DenseMarksLiveLongerAndAddBlots()
    {
        var ordinary = BloodGeometry.CreateGroundMark(
            Mark(isDense: false),
            cameraZoom: 1f);
        var dense = BloodGeometry.CreateGroundMark(
            Mark(isDense: true),
            cameraZoom: 1f);

        Assert.True(dense.BlotCount > ordinary.BlotCount);
        Assert.True(
            Mark(isDense: true).LifetimeSeconds >
            Mark(isDense: false).LifetimeSeconds);
    }

    [Fact]
    public void CreateGroundMark_KeepsAtLeastOneBlotAtLowDetail()
    {
        var lowDetail = BloodGeometry.CreateGroundMark(Mark(), 0.05f);
        var defaultFit = BloodGeometry.CreateGroundMark(
            Mark(),
            DefaultFitCameraZoom);

        Assert.True(lowDetail.VisibleBlotCount > 0);
        Assert.True(lowDetail.VisibleBlotCount <= lowDetail.BlotCount);
        Assert.Equal(defaultFit.BlotCount, defaultFit.VisibleBlotCount);
        Assert.Equal(
            new BloodBlot(0f, 0f, defaultFit.Radius, defaultFit.Alpha),
            defaultFit.GetBlot(0));
    }

    [Fact]
    public void CreateGroundMark_RejectsInvalidCameraZoom()
    {
        var mark = Mark();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => BloodGeometry.CreateGroundMark(mark, cameraZoom: -0.1f));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => BloodGeometry.CreateGroundMark(mark, cameraZoom: float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => BloodGeometry.CreateGroundMark(
                mark,
                cameraZoom: float.PositiveInfinity));
    }

    [Fact]
    public void CreateSpurt_IsDeterministicAndFollowsTheStoredDirection()
    {
        var spurt = Spurt(directionX: 1f, directionY: 0f);

        var first = BloodGeometry.CreateSpurt(spurt, cameraZoom: 1f);
        var second = BloodGeometry.CreateSpurt(spurt, cameraZoom: 1f);

        Assert.Equal(first, second);
        Assert.Equal(Strands(first), Strands(second));
        Assert.Equal(0f, first.DirectionAngle, precision: 5);
        Assert.All(Strands(first), strand => Assert.True(strand.EndX > 0f));
    }

    [Fact]
    public void CreateSpurt_RejectsInvalidCameraZoomAndOutOfRangeStrands()
    {
        var spurt = Spurt();
        var layout = BloodGeometry.CreateSpurt(spurt, cameraZoom: 1f);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => BloodGeometry.CreateSpurt(spurt, cameraZoom: float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => layout.GetStrand(layout.VisibleStrandCount));
    }

    /// <summary>
    /// The regression guard for the second half of the 2026-08-09 client
    /// crash. <c>GetSprayProfile</c> covered the melee four and threw
    /// <see cref="ArgumentOutOfRangeException"/> on every ranged hit that
    /// drew blood, which killed the client at tick 66 of a V5/V8 battle. The
    /// ranged package never touched this file; its plan does not mention
    /// blood at all. Every weapon in the roster must lay out a burst.
    /// </summary>
    [Theory]
    [InlineData(WeaponId.Kampilan)]
    [InlineData(WeaponId.Wasay)]
    [InlineData(WeaponId.Kalis)]
    [InlineData(WeaponId.Itak)]
    [InlineData(WeaponId.Bangkaw)]
    [InlineData(WeaponId.Busog)]
    [InlineData(WeaponId.Arquebus)]
    public void Create_LaysOutABurstForEveryWeaponInTheRoster(WeaponId weapon)
    {
        var layout = BloodGeometry.Create(Burst(weapon: weapon), cameraZoom: 1f);

        Assert.True(layout.DropletCount > 0);
        Assert.True(layout.Travel > 0f);
        Assert.True(layout.Thickness > 0f);
    }

    private static BloodBurst Burst(
        long sequence = 12,
        ulong sourceEntityId = 3,
        ulong targetEntityId = 7,
        float directionX = 1f,
        float directionY = 0f,
        WeaponId weapon = WeaponId.Kampilan,
        BodyPart hitLocation = BodyPart.Chest,
        float severityRatio = 0.5f,
        bool isLethal = false,
        float ageSeconds = 0.05f) =>
        new(
            sequence,
            sourceEntityId,
            targetEntityId,
            XRaw: 1200,
            YRaw: 3400,
            directionX,
            directionY,
            weapon,
            hitLocation,
            severityRatio,
            isLethal,
            ageSeconds);

    private static GroundMark Mark(
        long sequence = 12,
        bool isLethal = true,
        bool isDense = false,
        float ageSeconds = 0.05f) =>
        new(sequence, XRaw: 1200, YRaw: 3400, isLethal, isDense, ageSeconds);

    private static LethalSpurt Spurt(
        long sequence = 12,
        float directionX = 1f,
        float directionY = 0f,
        float ageSeconds = 0.05f) =>
        new(
            sequence,
            SourceEntityId: 3,
            TargetEntityId: 7,
            XRaw: 1200,
            YRaw: 3400,
            directionX,
            directionY,
            SeverityRatio: 0.5f,
            ageSeconds);

    private static BloodDroplet[] Droplets(BloodBurstLayout layout) =>
        Enumerable.Range(0, layout.VisibleDropletCount)
            .Select(layout.GetDroplet)
            .ToArray();

    private static BloodDroplet[] Strands(LethalSpurtLayout layout) =>
        Enumerable.Range(0, layout.VisibleStrandCount)
            .Select(layout.GetStrand)
            .ToArray();
}
