using Hukbo.Client.Audio;

namespace Hukbo.Client.Tests;

public sealed class SoundVoiceLedgerTests
{
    [Fact]
    public void SoundingVoices_StartsEmpty()
    {
        var ledger = new SoundVoiceLedger();

        Assert.Equal(0, ledger.SoundingVoices);
    }

    [Fact]
    public void Add_CountsAVoiceUntilItsClipEnds()
    {
        var ledger = new SoundVoiceLedger();

        ledger.Add(durationSeconds: 0.25);

        Assert.Equal(1, ledger.SoundingVoices);
    }

    [Fact]
    public void Advance_RetiresAVoiceOnlyOnceItsClipHasFinished()
    {
        var ledger = new SoundVoiceLedger();
        ledger.Add(durationSeconds: 0.25);

        ledger.Advance(elapsedSeconds: 0.10);
        Assert.Equal(1, ledger.SoundingVoices);

        ledger.Advance(elapsedSeconds: 0.10);
        Assert.Equal(1, ledger.SoundingVoices);

        ledger.Advance(elapsedSeconds: 0.10);
        Assert.Equal(0, ledger.SoundingVoices);
    }

    [Fact]
    public void Advance_RetiresAVoiceAtTheExactBoundary()
    {
        var ledger = new SoundVoiceLedger();
        ledger.Add(durationSeconds: 0.25);

        ledger.Advance(elapsedSeconds: 0.25);

        Assert.Equal(0, ledger.SoundingVoices);
    }

    [Fact]
    public void Advance_RetiresOnlyTheVoicesThatHaveExpired()
    {
        var ledger = new SoundVoiceLedger();
        ledger.Add(durationSeconds: 0.05);
        ledger.Add(durationSeconds: 0.50);

        ledger.Advance(elapsedSeconds: 0.10);

        Assert.Equal(1, ledger.SoundingVoices);
    }

    [Fact]
    public void Advance_IgnoresANonPositiveElapsedTime()
    {
        var ledger = new SoundVoiceLedger();
        ledger.Add(durationSeconds: 0.25);

        ledger.Advance(elapsedSeconds: 0);
        ledger.Advance(elapsedSeconds: -5);

        Assert.Equal(1, ledger.SoundingVoices);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Add_DoesNotTrackANonPositiveDuration(double durationSeconds)
    {
        var ledger = new SoundVoiceLedger();

        ledger.Add(durationSeconds);

        Assert.Equal(0, ledger.SoundingVoices);
    }

    [Fact]
    public void Add_StopsTrackingAtTheCeiling()
    {
        var ledger = new SoundVoiceLedger();

        for (var index = 0; index < SoundVoiceLedger.MaximumTrackedVoices + 50; index++)
        {
            ledger.Add(durationSeconds: 10);
        }

        Assert.Equal(SoundVoiceLedger.MaximumTrackedVoices, ledger.SoundingVoices);
    }

    [Fact]
    public void GetGainForNextCue_ReturnsTheBaseGainWhenNothingIsSounding()
    {
        var ledger = new SoundVoiceLedger();

        Assert.Equal(0.8f, ledger.GetGainForNextCue(0.8f));
    }

    [Fact]
    public void GetGainForNextCue_FallsAsTheSquareRootOfTheVoiceCount()
    {
        var ledger = new SoundVoiceLedger();
        ledger.Add(durationSeconds: 1);
        ledger.Add(durationSeconds: 1);
        ledger.Add(durationSeconds: 1);

        // Three sounding, so the fourth cue divides by the square root of four.
        Assert.Equal(0.4f, ledger.GetGainForNextCue(0.8f), tolerance: 0.0001f);
    }

    [Fact]
    public void GetGainForNextCue_RecoversOnceTheVoicesRetire()
    {
        var ledger = new SoundVoiceLedger();
        ledger.Add(durationSeconds: 0.25);
        ledger.Add(durationSeconds: 0.25);
        Assert.True(ledger.GetGainForNextCue(0.8f) < 0.8f);

        ledger.Advance(elapsedSeconds: 1);

        Assert.Equal(0.8f, ledger.GetGainForNextCue(0.8f));
    }

    [Fact]
    public void GetGainForNextCue_RejectsANegativeBaseGain()
    {
        var ledger = new SoundVoiceLedger();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => ledger.GetGainForNextCue(-0.1f));
    }

    [Fact]
    public void Clear_EmptiesTheLedgerAndResetsTheClock()
    {
        var ledger = new SoundVoiceLedger();
        ledger.Add(durationSeconds: 10);
        ledger.Advance(elapsedSeconds: 5);

        ledger.Clear();

        Assert.Equal(0, ledger.SoundingVoices);

        // A voice added after clearing must survive its full duration, which
        // it would not if the clock had kept the pre-clear offset.
        ledger.Add(durationSeconds: 1);
        ledger.Advance(elapsedSeconds: 0.5);
        Assert.Equal(1, ledger.SoundingVoices);
    }
}
