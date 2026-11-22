using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests;

public sealed class AgentIndexLookupTests
{
    private static readonly ulong[] DenseIds = [1, 2, 3, 5, 8];

    [Fact]
    public void ProductionShapedIdsUseTheFlatArray()
    {
        var map = AgentIndexMap.Create([1, 2, 3, 4]);

        Assert.True(map.IsDense);
        for (var index = 0; index < 4; index++)
        {
            Assert.True(map.TryGetIndex((ulong)(index + 1), out var found));
            Assert.Equal(index, found);
            Assert.Equal(index, map.IndexOf((ulong)(index + 1)));
        }
    }

    [Fact]
    public void IdZeroIsNotFoundWhenNoAgentOwnsIt()
    {
        var map = AgentIndexMap.Create(DenseIds);

        Assert.True(map.IsDense);
        Assert.False(map.TryGetIndex(0, out _));
        Assert.Throws<KeyNotFoundException>(() => map.IndexOf(0));
    }

    [Fact]
    public void IdZeroIsFoundWhenAnAgentOwnsIt()
    {
        var map = AgentIndexMap.Create([0, 1]);

        Assert.True(map.TryGetIndex(0, out var found));
        Assert.Equal(0, found);
    }

    [Fact]
    public void IdBeyondTheArrayIsNotFound()
    {
        var map = AgentIndexMap.Create(DenseIds);

        Assert.False(map.TryGetIndex(9, out _));
        Assert.False(map.TryGetIndex(ulong.MaxValue, out _));
        Assert.Throws<KeyNotFoundException>(() => map.IndexOf(9));
        Assert.Throws<KeyNotFoundException>(() => map.IndexOf(ulong.MaxValue));
    }

    [Fact]
    public void UnusedSlotInsideTheArrayIsNotFound()
    {
        var map = AgentIndexMap.Create(DenseIds);

        Assert.True(map.IsDense);
        foreach (ulong unused in new ulong[] { 4, 6, 7 })
        {
            Assert.False(map.TryGetIndex(unused, out _));
            Assert.Throws<KeyNotFoundException>(() => map.IndexOf(unused));
        }
    }

    [Fact]
    public void SparseIdsTakeTheDictionaryFallbackAndBehaveTheSame()
    {
        // Largest ID exceeds 4 * 3 + 16 = 28.
        ulong[] ids = [7, 1_000_000, 29];
        var map = AgentIndexMap.Create(ids);

        Assert.False(map.IsDense);
        for (var index = 0; index < ids.Length; index++)
        {
            Assert.True(map.TryGetIndex(ids[index], out var found));
            Assert.Equal(index, found);
            Assert.Equal(index, map.IndexOf(ids[index]));
        }

        Assert.False(map.TryGetIndex(0, out _));
        Assert.False(map.TryGetIndex(8, out _));
        Assert.Throws<KeyNotFoundException>(() => map.IndexOf(8));
    }

    [Fact]
    public void LargestIdAtTheDenseLimitStaysDenseAndOneMoreGoesSparse()
    {
        // Three agents: the limit is 4 * 3 + 16 = 28.
        Assert.True(AgentIndexMap.Create([1, 2, 28]).IsDense);
        Assert.False(AgentIndexMap.Create([1, 2, 29]).IsDense);
    }

    [Fact]
    public void EmptyAgentListBuildsAMapThatFindsNothing()
    {
        var map = AgentIndexMap.Create([]);

        Assert.False(map.TryGetIndex(1, out _));
        Assert.Throws<KeyNotFoundException>(() => map.IndexOf(1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DuplicateIdThrowsArgumentExceptionOnBothPaths(bool sparse)
    {
        ulong[] ids = sparse ? [5, 1_000_000, 5] : [1, 2, 2];

        var exception = Assert.Throws<ArgumentException>(
            () => AgentIndexMap.Create(ids));

        Assert.Equal("agents", exception.ParamName);
        Assert.Contains("Duplicate entity ID", exception.Message);
    }
}
