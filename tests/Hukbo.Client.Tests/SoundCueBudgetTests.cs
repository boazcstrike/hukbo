using Hukbo.Client.Audio;

namespace Hukbo.Client.Tests;

public sealed class SoundCueBudgetTests
{
    [Fact]
    public void Constructor_RejectsNonPositiveLimits()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SoundCueBudget(maximumPerSound: 0, maximumTotal: 8));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SoundCueBudget(maximumPerSound: 3, maximumTotal: 0));
    }

    [Fact]
    public void TryConsume_StopsAtThePerSoundLimit()
    {
        var budget = new SoundCueBudget(maximumPerSound: 2, maximumTotal: 8);

        Assert.True(budget.TryConsume(GameSoundId.Death));
        Assert.True(budget.TryConsume(GameSoundId.Death));
        Assert.False(budget.TryConsume(GameSoundId.Death));
        Assert.True(budget.TryConsume(GameSoundId.AttackGreatBlade));
    }

    [Fact]
    public void TryConsume_StopsAtTheTotalLimitAcrossSlots()
    {
        var budget = new SoundCueBudget(maximumPerSound: 5, maximumTotal: 2);

        Assert.True(budget.TryConsume(GameSoundId.Death));
        Assert.True(budget.TryConsume(GameSoundId.AttackGreatBlade));
        Assert.False(budget.TryConsume(GameSoundId.AttackWarAxe));
    }

    [Fact]
    public void BeginFrame_ClearsBothCounters()
    {
        var budget = new SoundCueBudget(maximumPerSound: 1, maximumTotal: 1);
        Assert.True(budget.TryConsume(GameSoundId.Death));
        Assert.False(budget.TryConsume(GameSoundId.Death));

        budget.BeginFrame();

        Assert.True(budget.TryConsume(GameSoundId.Death));
    }

    [Fact]
    public void TryConsume_RejectsASlotThatIsNotInTheCatalog()
    {
        var budget = new SoundCueBudget();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => budget.TryConsume((GameSoundId)999));
    }

    [Fact]
    public void DefaultLimits_AllowAtMostThreeOfOneSlotAndEightInTotal()
    {
        var budget = new SoundCueBudget();
        var perSound = 0;
        while (budget.TryConsume(GameSoundId.Death))
        {
            perSound++;
        }

        var total = perSound;
        foreach (var sound in SoundCatalog.AllSounds)
        {
            while (budget.TryConsume(sound))
            {
                total++;
            }
        }

        Assert.Equal(SoundCueBudget.DefaultMaximumPerSound, perSound);
        Assert.Equal(SoundCueBudget.DefaultMaximumTotal, total);
    }
}
