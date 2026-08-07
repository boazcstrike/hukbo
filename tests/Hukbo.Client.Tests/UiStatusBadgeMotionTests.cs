using Hukbo.Client.Settings;
using Hukbo.Client.UI;
using Hukbo.Core.Simulation;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

public sealed class UiStatusBadgeMotionTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(16);

    [Fact]
    public void Observe_FirstObservationSeedsStateWithoutPulsing()
    {
        var motion = new UiStatusBadgeMotion();

        motion.Observe(
            BattleOutcome.Ongoing,
            isPlaying: true,
            Frame,
            MotionIntensity.Full);

        Assert.Equal(0f, motion.Amount);
    }

    [Fact]
    public void Observe_OutcomeChangeTriggersPulse()
    {
        var motion = new UiStatusBadgeMotion();
        motion.Observe(
            BattleOutcome.Ongoing,
            isPlaying: true,
            Frame,
            MotionIntensity.Full);

        motion.Observe(
            BattleOutcome.Faction0Victory,
            isPlaying: true,
            Frame,
            MotionIntensity.Full);

        Assert.True(motion.Amount > 0f);
    }

    [Fact]
    public void Observe_PlayingFlagChangeTriggersPulse()
    {
        var motion = new UiStatusBadgeMotion();
        motion.Observe(
            BattleOutcome.Ongoing,
            isPlaying: true,
            Frame,
            MotionIntensity.Full);

        motion.Observe(
            BattleOutcome.Ongoing,
            isPlaying: false,
            Frame,
            MotionIntensity.Full);

        Assert.True(motion.Amount > 0f);
    }

    [Fact]
    public void Observe_RepeatedIdenticalObservationsNeverTrigger()
    {
        var motion = new UiStatusBadgeMotion();
        motion.Observe(
            BattleOutcome.Ongoing,
            isPlaying: true,
            Frame,
            MotionIntensity.Full);

        for (var i = 0; i < 30; i++)
        {
            motion.Observe(
                BattleOutcome.Ongoing,
                isPlaying: true,
                Frame,
                MotionIntensity.Full);
        }

        Assert.Equal(0f, motion.Amount);
    }

    [Fact]
    public void Observe_AfterTriggerFiveSecondsOfFramesSettleExactlyAtZeroAndNeverRiseAgain()
    {
        var motion = new UiStatusBadgeMotion();
        motion.Observe(
            BattleOutcome.Ongoing,
            isPlaying: true,
            Frame,
            MotionIntensity.Full);
        motion.Observe(
            BattleOutcome.Faction0Victory,
            isPlaying: true,
            Frame,
            MotionIntensity.Full);
        Assert.True(motion.Amount > 0f);

        var previousAmount = motion.Amount;
        var frameCount = (int)(TimeSpan.FromSeconds(5).TotalMilliseconds / Frame.TotalMilliseconds);
        for (var i = 0; i < frameCount; i++)
        {
            motion.Observe(
                BattleOutcome.Faction0Victory,
                isPlaying: true,
                Frame,
                MotionIntensity.Full);

            Assert.True(motion.Amount <= previousAmount);
            previousAmount = motion.Amount;
        }

        Assert.Equal(0f, motion.Amount);

        motion.Observe(
            BattleOutcome.Faction0Victory,
            isPlaying: true,
            Frame,
            MotionIntensity.Full);

        Assert.Equal(0f, motion.Amount);
    }

    [Fact]
    public void Observe_MotionOffAmountAlwaysZero()
    {
        var motion = new UiStatusBadgeMotion();
        motion.Observe(
            BattleOutcome.Ongoing,
            isPlaying: true,
            Frame,
            MotionIntensity.Off);

        motion.Observe(
            BattleOutcome.Faction0Victory,
            isPlaying: false,
            Frame,
            MotionIntensity.Off);

        Assert.Equal(0f, motion.Amount);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.5f)]
    [InlineData(1f)]
    public void GetBarColor_LerpsFromStatusSurfaceToStatusInfo(float pulseAmount)
    {
        var statusSurface = new Color(10, 20, 30);
        var statusInfo = new Color(200, 210, 220);

        var color = UiStatusBadgeMotion.GetBarColor(statusSurface, statusInfo, pulseAmount);

        Assert.Equal(Color.Lerp(statusSurface, statusInfo, pulseAmount), color);
    }
}
