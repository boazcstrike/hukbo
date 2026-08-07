using Hukbo.Core.Determinism;
using Sandata.Core.Navigation;

namespace Sandata.Core.Tests;

/// <summary>
/// Property tests over <see cref="NavComparer.Compare"/>, closing the risk
/// register row "The A* open set is ordered by something less than a total
/// key": "Task 21 includes a property test over generated triples asserting
/// antisymmetry, transitivity, and totality." The triples are generated from
/// a fixed <see cref="SplitMix64"/> seed rather than <c>System.Random</c>
/// (banned by <c>CLAUDE.md</c> section 5 and the Sandata banned-token scan),
/// so a failure here is exactly reproducible.
/// </summary>
public sealed class NavComparerTests
{
    private const ulong Seed = 0x4E61_7643_6F6D_7061UL; // "NavCompa" as bytes, arbitrary but fixed
    private const int KeyCount = 60;
    private const int TripleKeyCount = 25;

    private readonly record struct Key(int F, int H, int NodeIndex);

    [Fact]
    public void Compare_IsAntisymmetric_AcrossEveryGeneratedKeyPair()
    {
        var keys = GenerateKeys(KeyCount);

        foreach (var a in keys)
        {
            foreach (var b in keys)
            {
                var forward = Compare(a, b);
                var backward = Compare(b, a);

                Assert.Equal(Math.Sign(forward), -Math.Sign(backward));
            }
        }
    }

    [Fact]
    public void Compare_IsTransitive_AcrossEveryGeneratedKeyTriple()
    {
        var keys = GenerateKeys(TripleKeyCount);

        foreach (var a in keys)
        {
            foreach (var b in keys)
            {
                foreach (var c in keys)
                {
                    var ab = Compare(a, b);
                    var bc = Compare(b, c);
                    var ac = Compare(a, c);

                    if (ab <= 0 && bc <= 0)
                    {
                        Assert.True(ac <= 0, $"{a} <= {b} and {b} <= {c} but {a} > {c}.");
                    }

                    if (ab >= 0 && bc >= 0)
                    {
                        Assert.True(ac >= 0, $"{a} >= {b} and {b} >= {c} but {a} < {c}.");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Totality: for any two keys, comparing them one way or the other always
    /// produces an answer — there is no pair for which both directions come
    /// back "greater", which is what a correct three-way comparator can never
    /// produce.
    /// </summary>
    [Fact]
    public void Compare_IsTotal_NeitherDirectionOfAnyPairEverReportsBothGreater()
    {
        var keys = GenerateKeys(KeyCount);

        foreach (var a in keys)
        {
            foreach (var b in keys)
            {
                var forward = Compare(a, b);
                var backward = Compare(b, a);

                Assert.True(
                    forward <= 0 || backward <= 0,
                    $"Neither {a} <= {b} nor {b} <= {a} held.");
            }
        }
    }

    /// <summary>
    /// The property that makes the key total rather than merely a preorder:
    /// because <c>nodeIndex</c> is unique per cell in real use, two keys that
    /// agree on <c>f</c> and <c>h</c> but differ only in <c>nodeIndex</c> must
    /// never compare equal.
    /// </summary>
    [Fact]
    public void Compare_NeverReturnsZero_ForTwoKeysThatShareFAndHButDifferInNodeIndex()
    {
        var keys = GenerateKeys(KeyCount);

        foreach (var key in keys)
        {
            var sameFAndHDifferentIndex = key with { NodeIndex = key.NodeIndex + 1 };

            Assert.NotEqual(0, Compare(key, sameFAndHDifferentIndex));
        }
    }

    [Fact]
    public void Compare_ReturnsZero_OnlyForTwoIdenticalKeys()
    {
        var keys = GenerateKeys(KeyCount);

        foreach (var a in keys)
        {
            Assert.Equal(0, Compare(a, a));

            foreach (var b in keys)
            {
                if (Compare(a, b) == 0)
                {
                    Assert.Equal(a, b);
                }
            }
        }
    }

    [Theory]
    [InlineData(1, 5, 0, 2, 5, 0)] // lower f wins regardless of h or nodeIndex
    [InlineData(5, 1, 0, 5, 2, 0)] // equal f: lower h wins
    [InlineData(5, 5, 1, 5, 5, 2)] // equal f and h: lower nodeIndex wins
    public void Compare_OrdersByFThenHThenNodeIndex_OnHandPickedBoundaryCases(
        int fA, int hA, int indexA, int fB, int hB, int indexB)
    {
        Assert.True(NavComparer.Compare(fA, hA, indexA, fB, hB, indexB) < 0);
        Assert.True(NavComparer.Compare(fB, hB, indexB, fA, hA, indexA) > 0);
    }

    private static int Compare(Key a, Key b) =>
        NavComparer.Compare(a.F, a.H, a.NodeIndex, b.F, b.H, b.NodeIndex);

    private static Key[] GenerateKeys(int count)
    {
        var rng = new SplitMix64(Seed);
        var keys = new Key[count];

        for (var i = 0; i < count; i++)
        {
            // Deliberately overlapping ranges across f, h, and nodeIndex so
            // the generated set actually exercises every tie-break rung
            // (equal f, equal f and h, fully distinct) rather than almost
            // always landing on "f alone decides it".
            var f = rng.NextInt(20);
            var h = rng.NextInt(20);
            var nodeIndex = rng.NextInt(20);
            keys[i] = new Key(f, h, nodeIndex);
        }

        return keys;
    }
}
