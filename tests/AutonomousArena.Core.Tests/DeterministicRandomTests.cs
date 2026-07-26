using AutonomousArena.Core.Determinism;

namespace AutonomousArena.Core.Tests;

public sealed class DeterministicRandomTests
{
    [Fact]
    public void NextUInt64_MatchesSplitMix64ReferenceVector()
    {
        var random = new SplitMix64(1);

        Assert.Equal(0x910A2DEC89025CC1UL, random.NextUInt64());
        Assert.Equal(0xBEEB8DA1658EEC67UL, random.NextUInt64());
        Assert.Equal(0xF893A2EEFB32555EUL, random.NextUInt64());
    }

    [Fact]
    public void ZeroSeed_IsValidAndRepeatable()
    {
        var first = new SplitMix64(0);
        var second = new SplitMix64(0);

        var firstOutput = first.NextUInt64();
        var secondOutput = second.NextUInt64();

        Assert.Equal(0xE220A8397B1DCDAFUL, firstOutput);
        Assert.Equal(firstOutput, secondOutput);
    }

    [Fact]
    public void NextInt_StaysInsideExclusiveUpperBound()
    {
        var random = new SplitMix64(42);

        for (var index = 0; index < 1_000; index++)
        {
            var value = random.NextInt(7);
            Assert.InRange(value, 0, 6);
        }
    }
}
