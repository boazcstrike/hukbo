using Hukbo.Client.Audio;

namespace Hukbo.Client.Tests;

public sealed class SoundVariantSelectorTests
{
    [Fact]
    public void Select_AlwaysReturnsZeroForACountOfZero() =>
        Assert.Equal(0, SoundVariantSelector.Select(tick: 5, sourceEntityId: 3, 0));

    [Fact]
    public void Select_AlwaysReturnsZeroForACountOfOne() =>
        Assert.Equal(0, SoundVariantSelector.Select(tick: 999, sourceEntityId: 42, 1));

    [Fact]
    public void Select_RejectsANegativeCount() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SoundVariantSelector.Select(tick: 1, sourceEntityId: 1, variantCount: -1));

    [Fact]
    public void Select_IsWithinBoundsForEveryInput()
    {
        for (long tick = 0; tick < 50; tick++)
        {
            for (ulong entityId = 0; entityId < 20; entityId++)
            {
                var index = SoundVariantSelector.Select(tick, entityId, variantCount: 10);
                Assert.InRange(index, 0, 9);
            }
        }
    }

    [Fact]
    public void Select_IsDeterministicForTheSameInputs()
    {
        var first = SoundVariantSelector.Select(tick: 123, sourceEntityId: 7, 10);
        var second = SoundVariantSelector.Select(tick: 123, sourceEntityId: 7, 10);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Select_SpreadsSelectionAcrossDifferentSourceEntitiesAtTheSameTick()
    {
        var seenIndexes = new HashSet<int>();
        for (ulong entityId = 1; entityId <= 30; entityId++)
        {
            seenIndexes.Add(SoundVariantSelector.Select(tick: 7, entityId, variantCount: 10));
        }

        Assert.True(
            seenIndexes.Count > 1,
            "Thirty different source entities at one tick should not all select the " +
                "same variant.");
    }

    [Fact]
    public void Select_SpreadsSelectionAcrossDifferentTicksForTheSameEntity()
    {
        var seenIndexes = new HashSet<int>();
        for (long tick = 1; tick <= 30; tick++)
        {
            seenIndexes.Add(SoundVariantSelector.Select(tick, sourceEntityId: 11, 10));
        }

        Assert.True(
            seenIndexes.Count > 1,
            "Thirty different ticks for one entity should not all select the same " +
                "variant.");
    }
}
