using Hukbo.Client.Presentation;
using Hukbo.Client.Rendering;
using Hukbo.Core.Combat;

namespace Hukbo.Client.Tests;

/// <summary>
/// Six cases. Five are RED against the Phase 0 stub, which reports the first
/// phase and a neutral pose whatever the swing, and one is GUARD because a
/// pose that never changes is continuous everywhere.
/// </summary>
/// <remarks>
/// <para>
/// The four phase shares are read from <see cref="SwingGeometry"/> rather than
/// repeated here as literals. They are design data — the timing table in
/// design section 6.1 — not a value derived from the code under test, so
/// reading them back does not make this file an assertion against itself.
/// </para>
/// <para>
/// Every sample sits a thousandth of the total duration inside the phase it
/// belongs to, because a progress value converted to seconds and back does not
/// land exactly on a boundary in single precision.
/// </para>
/// </remarks>
public sealed class SwingGeometryTests
{
    private const float AnticipationEnd = SwingGeometry.AnticipationShare;
    private const float StrikeEnd = AnticipationEnd + SwingGeometry.StrikeShare;
    private const float ImpactHoldEnd = StrikeEnd + SwingGeometry.ImpactHoldShare;
    private const float Inset = 0.001f;

    /// <summary>
    /// RED. The stub reports <see cref="SwingPhase.Anticipation"/> for every
    /// progress value, so only one phase is ever observed.
    /// </summary>
    [Fact]
    public void ResolvePhase_VisitsTheFourPhasesInOrder()
    {
        var observed = new List<SwingPhase>();

        for (var step = 0; step <= 1000; step++)
        {
            var phase = SwingGeometry.ResolvePhase(step / 1000f);
            if (observed.Count == 0 || observed[^1] != phase)
            {
                observed.Add(phase);
            }
        }

        Assert.Equal(
            [
                SwingPhase.Anticipation,
                SwingPhase.Strike,
                SwingPhase.ImpactHold,
                SwingPhase.Recovery,
            ],
            observed);
    }

    /// <summary>
    /// RED. The pose carries no direction of its own, so the swing direction
    /// has to be folded into the weapon rotation and the torso lean here or a
    /// draw loop cannot recover it.
    /// </summary>
    [Fact]
    public void ResolvePose_SwingsTowardTheTarget()
    {
        var atContact = StrikeEnd + Inset;

        var rightward = SwingGeometry.ResolvePose(
            SwingAt(atContact, AttackResolution.Landed, 1f, 0f));
        var leftward = SwingGeometry.ResolvePose(
            SwingAt(atContact, AttackResolution.Landed, -1f, 0f));
        var downward = SwingGeometry.ResolvePose(
            SwingAt(atContact, AttackResolution.Landed, 0f, 1f));

        Assert.True(
            rightward.TorsoLeanX > 0f,
            $"Leaning right gave {rightward.TorsoLeanX}.");
        Assert.True(
            leftward.TorsoLeanX < 0f,
            $"Leaning left gave {leftward.TorsoLeanX}.");
        Assert.Equal(rightward.TorsoLeanX, -leftward.TorsoLeanX, precision: 4);
        Assert.Equal(0f, rightward.TorsoLeanY, precision: 4);
        Assert.True(
            downward.TorsoLeanY > 0f,
            $"Leaning down gave {downward.TorsoLeanY}.");
        Assert.Equal(0f, downward.TorsoLeanX, precision: 4);
        Assert.True(
            rightward.WeaponAngleRadians > 0f,
            $"Rotating right gave {rightward.WeaponAngleRadians}.");
        Assert.Equal(
            rightward.WeaponAngleRadians,
            -leftward.WeaponAngleRadians,
            precision: 4);
    }

    /// <summary>
    /// RED. A contact outcome is the only one of the three pose classes whose
    /// weapon travels back toward the attacker during the impact hold.
    /// </summary>
    [Fact]
    public void ResolvePose_RecoilsOnAContactOutcome()
    {
        AttackResolution[] contactOutcomes =
        [
            AttackResolution.ShieldBlocked,
            AttackResolution.Parried,
            AttackResolution.Deflected,
        ];

        foreach (var resolution in contactOutcomes)
        {
            var atContact = SwingGeometry.ResolvePose(
                SwingAt(StrikeEnd + Inset, resolution, 1f, 0f));
            var afterRecoil = SwingGeometry.ResolvePose(
                SwingAt(ImpactHoldEnd - Inset, resolution, 1f, 0f));

            Assert.True(
                atContact.ExtensionRatio > 0f,
                $"{resolution} reached {atContact.ExtensionRatio} at contact.");
            Assert.True(
                afterRecoil.ExtensionRatio < atContact.ExtensionRatio,
                $"{resolution} held {afterRecoil.ExtensionRatio} rather than " +
                $"recoiling from {atContact.ExtensionRatio}.");
        }
    }

    /// <summary>
    /// RED. Without a branch of its own a landed blow is the same motion as a
    /// void, and the animation cannot name the fifth outcome at all.
    /// </summary>
    [Fact]
    public void ResolvePose_StopsOnTheTargetForALandedBlow()
    {
        var atContact = SwingGeometry.ResolvePose(
            SwingAt(StrikeEnd + Inset, AttackResolution.Landed, 1f, 0f));
        var atHoldEnd = SwingGeometry.ResolvePose(
            SwingAt(ImpactHoldEnd - Inset, AttackResolution.Landed, 1f, 0f));

        Assert.True(
            atContact.ExtensionRatio > 0f,
            $"A landed blow reached {atContact.ExtensionRatio} at contact.");
        Assert.Equal(
            atContact.ExtensionRatio,
            atHoldEnd.ExtensionRatio,
            precision: 4);
        Assert.Equal(
            atContact.WeaponAngleRadians,
            atHoldEnd.WeaponAngleRadians,
            precision: 4);
    }

    /// <summary>
    /// RED. The void is the only outcome whose weapon passes the point where a
    /// landed blow stops.
    /// </summary>
    [Fact]
    public void ResolvePose_FollowsThroughPastTheTargetForAVoid()
    {
        var atHoldEnd = ImpactHoldEnd - Inset;

        var evaded = SwingGeometry.ResolvePose(
            SwingAt(atHoldEnd, AttackResolution.Evaded, 1f, 0f));
        var landed = SwingGeometry.ResolvePose(
            SwingAt(atHoldEnd, AttackResolution.Landed, 1f, 0f));
        var parried = SwingGeometry.ResolvePose(
            SwingAt(atHoldEnd, AttackResolution.Parried, 1f, 0f));

        Assert.True(
            evaded.ExtensionRatio > landed.ExtensionRatio,
            $"A void reached {evaded.ExtensionRatio} against a landed " +
            $"{landed.ExtensionRatio}.");
        Assert.True(
            landed.ExtensionRatio > parried.ExtensionRatio,
            $"A landed blow reached {landed.ExtensionRatio} against a " +
            $"parried {parried.ExtensionRatio}.");
    }

    /// <summary>
    /// GUARD, satisfied by a neutral pose that never changes. It walks the
    /// whole duration in fine steps rather than testing named boundaries,
    /// which covers every boundary without repeating where they sit.
    /// <see cref="SwingPose.Phase"/> and <see cref="SwingPose.PhaseProgress"/>
    /// are excluded deliberately: phase progress resets to zero at each
    /// boundary by construction and is a coordinate, not a drawn quantity.
    /// </summary>
    [Fact]
    public void ResolvePose_IsContinuousAcrossEveryPhaseBoundary()
    {
        const int Steps = 2000;
        const float Tolerance = 0.02f;

        foreach (var resolution in Enum.GetValues<AttackResolution>())
        {
            var previous = SwingGeometry.ResolvePose(
                SwingAt(0f, resolution, 1f, 0f));

            for (var step = 1; step <= Steps; step++)
            {
                var progress = step / (float)Steps;
                var current = SwingGeometry.ResolvePose(
                    SwingAt(progress, resolution, 1f, 0f));

                AssertClose(
                    previous.WeaponAngleRadians,
                    current.WeaponAngleRadians,
                    Tolerance,
                    resolution,
                    progress,
                    nameof(SwingPose.WeaponAngleRadians));
                AssertClose(
                    previous.TorsoLeanX,
                    current.TorsoLeanX,
                    Tolerance,
                    resolution,
                    progress,
                    nameof(SwingPose.TorsoLeanX));
                AssertClose(
                    previous.TorsoLeanY,
                    current.TorsoLeanY,
                    Tolerance,
                    resolution,
                    progress,
                    nameof(SwingPose.TorsoLeanY));
                AssertClose(
                    previous.ExtensionRatio,
                    current.ExtensionRatio,
                    Tolerance,
                    resolution,
                    progress,
                    nameof(SwingPose.ExtensionRatio));
                AssertClose(
                    previous.TrailStrength,
                    current.TrailStrength,
                    Tolerance,
                    resolution,
                    progress,
                    nameof(SwingPose.TrailStrength));

                previous = current;
            }
        }
    }

    private static void AssertClose(
        float previous,
        float current,
        float tolerance,
        AttackResolution resolution,
        float progress,
        string field)
    {
        Assert.True(
            MathF.Abs(current - previous) <= tolerance,
            $"{field} jumped from {previous} to {current} at progress " +
            $"{progress} for {resolution}.");
    }

    private static SwingAnimation SwingAt(
        float progress,
        AttackResolution resolution,
        float directionX,
        float directionY) =>
        new(
            Sequence: 1,
            AttackerEntityId: 2,
            TargetEntityId: 7,
            directionX,
            directionY,
            resolution,
            progress * SwingAnimation.TotalSeconds);
}
