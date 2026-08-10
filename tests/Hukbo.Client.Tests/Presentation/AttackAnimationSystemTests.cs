using Hukbo.Client.Presentation;
using Hukbo.Client.Settings;
using Hukbo.Core.Combat;

namespace Hukbo.Client.Tests;

public sealed class AttackAnimationSystemTests
{
    [Fact]
    public void Ingest_BakesTheCompleteContactAtTimelineOrigin()
    {
        var system = new AttackAnimationSystem(capacity: 4);
        var contact = Contact(
            sequence: 17,
            tick: 23,
            attacker: 2,
            defender: 7,
            weapon: WeaponId.Kalis,
            shield: ShieldId.TallHardwood,
            resolution: AttackResolution.Parried,
            comboPosition: 3);

        system.Ingest(
            contact,
            directionX: 0.6f,
            directionY: 0.8f,
            MotionIntensity.Reduced);

        Assert.True(system.TryGetAnimation(2, out var animation));
        Assert.Equal(17, animation.Sequence);
        Assert.Equal(23, animation.Tick);
        Assert.Equal(2UL, animation.AttackerEntityId);
        Assert.Equal(7UL, animation.DefenderEntityId);
        Assert.Equal(WeaponId.Kalis, animation.Weapon);
        Assert.Equal(ShieldId.TallHardwood, animation.AttackerShield);
        Assert.Equal(BodyPart.Chest, animation.HitLocation);
        Assert.Equal(AttackResolution.Parried, animation.Resolution);
        Assert.Equal(3, animation.ComboPosition);
        Assert.Equal(0.6f, animation.DirectionX);
        Assert.Equal(0.8f, animation.DirectionY);
        Assert.Equal(MotionIntensity.Reduced, animation.MotionIntensity);
        Assert.Equal(AttackMotionCatalog.Resolve(WeaponId.Kalis), animation.MotionProfile);
        Assert.Equal(AttackAnimationPhase.Contact, animation.Phase);
        Assert.Equal(0f, animation.AgeSeconds);
        Assert.Equal(0f, animation.RecoveryProgress);
        Assert.True(animation.AwaitingDrawAcknowledgement);
    }

    [Fact]
    public void Advance_CannotConsumeContactBeforeItsDrawAcknowledgement()
    {
        var system = new AttackAnimationSystem(capacity: 1);
        system.Ingest(
            Contact(sequence: 1, tick: 1),
            directionX: 1f,
            directionY: 0f,
            MotionIntensity.Full);

        system.Advance(elapsedSeconds: 4f, speedMultiplier: 4f);

        Assert.True(system.TryGetAnimation(2, out var contact));
        Assert.Equal(AttackAnimationPhase.Contact, contact.Phase);
        Assert.Equal(0f, contact.AgeSeconds);
        Assert.True(contact.AwaitingDrawAcknowledgement);

        Assert.True(system.AcknowledgeDraw(attackerEntityId: 2, sequence: 1));
        Assert.False(system.AcknowledgeDraw(attackerEntityId: 2, sequence: 1));
    }

    [Fact]
    public void Advance_EasesRecoveryIntoReadiness()
    {
        var system = new AttackAnimationSystem(capacity: 1);
        system.Ingest(
            Contact(sequence: 1, tick: 1, weapon: WeaponId.Wasay),
            directionX: 1f,
            directionY: 0f,
            MotionIntensity.Full);
        Assert.True(system.AcknowledgeDraw(2, 1));
        var recoverySeconds = AttackMotionCatalog.Resolve(WeaponId.Wasay).RecoverySeconds;

        system.Advance(recoverySeconds * 0.25f);

        Assert.True(system.TryGetAnimation(2, out var recovering));
        Assert.Equal(AttackAnimationPhase.Recovery, recovering.Phase);
        Assert.Equal(0.15625f, recovering.RecoveryProgress, precision: 5);

        system.Advance(recoverySeconds);

        Assert.True(system.TryGetAnimation(2, out var ready));
        Assert.Equal(AttackAnimationPhase.Readiness, ready.Phase);
        Assert.Equal(1f, ready.RecoveryProgress);
    }

    [Fact]
    public void Ingest_ComboContactInstallsFreshContactAndRejectsAStaleAcknowledgement()
    {
        var system = new AttackAnimationSystem(capacity: 1);
        system.Ingest(
            Contact(sequence: 10, tick: 40, comboPosition: 1),
            directionX: 1f,
            directionY: 0f,
            MotionIntensity.Full);
        Assert.True(system.AcknowledgeDraw(2, 10));
        system.Advance(0.08f);

        system.Ingest(
            Contact(sequence: 11, tick: 42, comboPosition: 2),
            directionX: -1f,
            directionY: 0f,
            MotionIntensity.Full);

        Assert.True(system.TryGetAnimation(2, out var combo));
        Assert.Equal(11, combo.Sequence);
        Assert.Equal(42, combo.Tick);
        Assert.Equal(2, combo.ComboPosition);
        Assert.Equal(-1f, combo.DirectionX);
        Assert.Equal(AttackAnimationPhase.Contact, combo.Phase);
        Assert.Equal(0f, combo.AgeSeconds);
        Assert.True(combo.AwaitingDrawAcknowledgement);
        Assert.False(system.AcknowledgeDraw(2, sequence: 10));
        Assert.True(system.TryGetAnimation(2, out var stillLatched));
        Assert.True(stillLatched.AwaitingDrawAcknowledgement);
    }

    [Fact]
    public void Advance_LethalContactRetainsAShortHoldAfterAcknowledgement()
    {
        var system = new AttackAnimationSystem(capacity: 1);
        system.Ingest(
            Contact(sequence: 1, tick: 1, isLethal: true),
            directionX: 1f,
            directionY: 0f,
            MotionIntensity.Full);
        Assert.True(system.AcknowledgeDraw(2, 1));

        system.Advance(AttackAnimation.LethalHoldSeconds * 0.5f);

        Assert.True(system.TryGetAnimation(2, out var held));
        Assert.Equal(AttackAnimationPhase.LethalHold, held.Phase);
        Assert.Equal(0f, held.RecoveryProgress);

        system.Advance(AttackAnimation.LethalHoldSeconds * 0.5f);

        Assert.True(system.TryGetAnimation(2, out var recovering));
        Assert.Equal(AttackAnimationPhase.Recovery, recovering.Phase);
    }

    [Fact]
    public void Advance_ScalesNormalAndComboCadenceAtEveryPlaybackSpeed()
    {
        // One attack interval of real time, played at each speed. Up to
        // AttackAnimationSystem.MaximumAnimationSpeedMultiplier the timeline
        // ages by exactly one interval however fast the battle runs, which is
        // the playback-independence this test was written for. Past the
        // ceiling it deliberately ages by less — at 4x, half an interval — so
        // the swing is drawn for twice as many frames as a faithful timeline
        // would give it. The expected value is written as the general form
        // rather than three literals so both regimes read as one rule.
        //
        // The two cadences are the Itak's attack and combo cooldowns under the
        // shipped combat preset, nine and five ticks at 20 Hz. They are
        // illustrative constants, not values read from the preset; a cadence
        // retune makes them stale prose rather than a failing assertion.
        const float NormalCadencePresentationSeconds = 9f / 20f;
        const float ComboCadencePresentationSeconds = 5f / 20f;

        foreach (var speed in new[] { 1f, 2f, 4f })
        {
            var animationSpeed =
                AttackAnimationSystem.ResolveAnimationSpeedMultiplier(speed);

            var system = new AttackAnimationSystem(capacity: 1);
            system.Ingest(
                Contact(sequence: 1, tick: 10, weapon: WeaponId.Itak),
                directionX: 1f,
                directionY: 0f,
                MotionIntensity.Full);
            Assert.True(system.AcknowledgeDraw(2, 1));

            system.Advance(
                NormalCadencePresentationSeconds / speed,
                speed);

            Assert.True(system.TryGetAnimation(2, out var afterNormalCadence));
            Assert.Equal(
                NormalCadencePresentationSeconds * animationSpeed / speed,
                afterNormalCadence.AgeSeconds,
                precision: 5);

            system.Ingest(
                Contact(
                    sequence: 2,
                    tick: 12,
                    weapon: WeaponId.Itak,
                    comboPosition: 2),
                directionX: 0f,
                directionY: 1f,
                MotionIntensity.Full);
            Assert.True(system.AcknowledgeDraw(2, 2));

            system.Advance(
                ComboCadencePresentationSeconds / speed,
                speed);

            Assert.True(system.TryGetAnimation(2, out var afterComboCadence));
            Assert.Equal(
                ComboCadencePresentationSeconds * animationSpeed / speed,
                afterComboCadence.AgeSeconds,
                precision: 5);
        }
    }

    [Fact]
    public void ContactTiming_IsFrameRateIndependentAcrossTheCompletePolicyMatrix()
    {
        var frameRates = new[] { 30, 60, 120 };
        var speeds = new[] { 1f, 2f, 4f };
        var intensities = Enum.GetValues<MotionIntensity>();
        var matrixCells = 0;

        foreach (var frameRate in frameRates)
        {
            foreach (var speed in speeds)
            {
                foreach (var intensity in intensities)
                {
                    matrixCells++;
                    var system = new AttackAnimationSystem(capacity: 1);
                    system.Ingest(
                        Contact(sequence: 1, tick: 1),
                        directionX: 0.6f,
                        directionY: 0.8f,
                        intensity);

                    for (var frame = 0; frame < frameRate / 10; frame++)
                    {
                        system.Advance(1f / frameRate, speed);
                    }

                    Assert.True(system.TryGetAnimation(2, out var latched));
                    Assert.Equal(AttackAnimationPhase.Contact, latched.Phase);
                    Assert.Equal(0f, latched.AgeSeconds);
                    Assert.Equal(intensity, latched.MotionIntensity);
                    Assert.True(system.AcknowledgeDraw(2, 1));

                    for (var frame = 0; frame < frameRate / 10; frame++)
                    {
                        system.Advance(1f / frameRate, speed);
                    }

                    // Frame-rate independence is the property under test, so
                    // the expected age is compared against the multiplier the
                    // system actually ages by rather than the requested one.
                    // Those are the same number at 1x and 2x and differ at 4x,
                    // where MaximumAnimationSpeedMultiplier holds the timeline
                    // down so a swing stays readable — see that constant.
                    Assert.True(system.TryGetAnimation(2, out var advanced));
                    Assert.Equal(
                        0.1f *
                        AttackAnimationSystem.ResolveAnimationSpeedMultiplier(
                            speed),
                        advanced.AgeSeconds,
                        precision: 4);
                    Assert.Equal(intensity, advanced.MotionIntensity);
                }
            }
        }

        Assert.Equal(27, matrixCells);
    }

    [Theory]
    // Requested playback speed, and the multiplier a timeline is aged by.
    [InlineData(0.25f, 0.25f)]
    [InlineData(1f, 1f)]
    [InlineData(2f, 2f)]
    [InlineData(4f, 2f)]
    [InlineData(8f, 2f)]
    [InlineData(1_000f, 2f)]
    public void AnimationSpeed_IsFaithfulUpToTheCeilingAndHeldAboveIt(
        float requested,
        float expected)
    {
        // Below and at the ceiling the animation is exactly faithful to the
        // simulation; above it the swing stops compressing further. This is
        // the 4x half of the CL-7 legibility failure: at 4x an unclamped
        // timeline drew the Itak's 0.17-second recovery in roughly two frames.
        Assert.Equal(
            expected,
            AttackAnimationSystem.ResolveAnimationSpeedMultiplier(requested));
    }

    [Fact]
    public void AnimationSpeed_IsBoundedAboveForEverySpeedTheCallerCanPass()
    {
        // The property, rather than the six sampled points: no multiplier
        // produces a timeline that ages faster than the ceiling allows.
        foreach (var requested in new[]
        {
            float.Epsilon, 0.5f, 1f, 1.5f, 2f, 3f, 4f, 16f, float.MaxValue,
        })
        {
            var resolved =
                AttackAnimationSystem.ResolveAnimationSpeedMultiplier(
                    requested);

            Assert.True(
                resolved <=
                AttackAnimationSystem.MaximumAnimationSpeedMultiplier,
                $"Speed {requested} resolved to {resolved}, above the " +
                "ceiling.");
            Assert.True(resolved > 0f);
            Assert.True(resolved <= requested);
        }
    }

    [Fact]
    public void Advance_DrawsAFourTimesSwingForTwiceAsLongAsAnUnclampedOne()
    {
        // The end-to-end statement of the same thing, through the system
        // rather than the helper: one second of real time at 4x playback ages
        // an acknowledged timeline by two seconds, not four.
        var system = new AttackAnimationSystem(capacity: 1);
        system.Ingest(
            Contact(sequence: 1, tick: 1),
            directionX: 1f,
            directionY: 0f,
            MotionIntensity.Full);
        Assert.True(system.AcknowledgeDraw(attackerEntityId: 2, sequence: 1));

        system.Advance(elapsedSeconds: 1f, speedMultiplier: 4f);

        Assert.True(system.TryGetAnimation(2, out var animation));
        Assert.Equal(2f, animation.AgeSeconds, precision: 4);
    }

    [Fact]
    public void CallerPauseAndClear_PreserveThenRemoveTimelineState()
    {
        var system = new AttackAnimationSystem(capacity: 2);
        system.Ingest(
            Contact(sequence: 1, tick: 1),
            directionX: 1f,
            directionY: 0f,
            MotionIntensity.Off);
        Assert.True(system.AcknowledgeDraw(2, 1));
        system.Advance(0.04f);
        Assert.True(system.TryGetAnimation(2, out var beforePause));

        // Playback pause is owned by the caller: it freezes this system by not
        // calling Advance. No wall-clock source exists inside the timeline.
        Assert.True(system.TryGetAnimation(2, out var duringPause));
        Assert.Equal(beforePause, duringPause);

        system.Clear();

        Assert.Empty(system.ActiveAnimations.ToArray());
        Assert.False(system.TryGetAnimation(2, out _));
    }

    [Fact]
    public void Ingest_KeepsAtMostOneTimelinePerAttacker()
    {
        var system = new AttackAnimationSystem(capacity: 2);
        system.Ingest(
            Contact(sequence: 1, tick: 1),
            directionX: 1f,
            directionY: 0f,
            MotionIntensity.Full);
        system.Ingest(
            Contact(sequence: 2, tick: 2),
            directionX: 0f,
            directionY: 1f,
            MotionIntensity.Full);

        var only = Assert.Single(system.ActiveAnimations.ToArray());
        Assert.Equal(2, only.Sequence);
        Assert.Equal(2UL, only.AttackerEntityId);
    }

    private static AttackContactBundle Contact(
        long sequence,
        long tick,
        ulong attacker = 2,
        ulong defender = 7,
        WeaponId weapon = WeaponId.Kampilan,
        ShieldId shield = ShieldId.None,
        AttackResolution resolution = AttackResolution.Landed,
        int? comboPosition = null,
        bool isLethal = false) =>
        new(
            sequence,
            tick,
            attacker,
            defender,
            Damage: resolution == AttackResolution.Landed ? 10 : 0,
            FactionId: 0,
            weapon,
            AttackerShield: shield,
            HitLocation: BodyPart.Chest,
            resolution,
            comboPosition,
            isLethal);
}
