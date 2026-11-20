using Xunit.Sdk;

namespace Hukbo.Core.Tests;

/// <summary>
/// Proves <see cref="WarmAllocationWindow"/> can fail and can pass, with
/// literal actions and no simulation, so the retry cannot hide a real allocation.
/// </summary>
public sealed class WarmAllocationWindowTests
{
    private const int AllocatedBytesFloor = 64;

    /// <summary>A static field keeps the JIT from eliding the allocation.</summary>
    private static byte[]? _sink;

    [Fact]
    public void AnActionThatAllocatesEveryAttemptFailsAndNamesAllAttempts()
    {
        var factoryCalls = 0;

        var failure = Assert.Throws<FailException>(() =>
            WarmAllocationWindow.AssertAllocatesNothing(
                () =>
                {
                    factoryCalls++;
                    return () => _sink = new byte[AllocatedBytesFloor];
                },
                "literal window"));

        Assert.Equal(WarmAllocationWindow.MaximumAttempts, factoryCalls);
        for (var attempt = 1; attempt <= WarmAllocationWindow.MaximumAttempts; attempt++)
        {
            Assert.Contains($"attempt {attempt}:", failure.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void EveryRecordedAttemptReportsAtLeastTheAllocatedSize()
    {
        var attemptBytes = WarmAllocationWindow.Measure(
            () => () => _sink = new byte[AllocatedBytesFloor]);

        Assert.Equal(WarmAllocationWindow.MaximumAttempts, attemptBytes.Length);
        Assert.All(attemptBytes, bytes => Assert.True(bytes >= AllocatedBytesFloor));
    }

    [Fact]
    public void AnActionThatAllocatesOnlyOnTheFirstAttemptPassesAndStopsAtTheSecond()
    {
        var factoryCalls = 0;

        var attemptBytes = WarmAllocationWindow.Measure(() =>
        {
            factoryCalls++;
            var shouldAllocate = factoryCalls == 1;
            return () =>
            {
                if (shouldAllocate)
                {
                    _sink = new byte[AllocatedBytesFloor];
                }
            };
        });

        Assert.Equal(2, attemptBytes.Length);
        Assert.True(attemptBytes[0] >= AllocatedBytesFloor);
        Assert.Equal(0, attemptBytes[1]);
        Assert.Equal(2, factoryCalls);
    }

    [Fact]
    public void AnActionThatNeverAllocatesPassesAtTheFirstAttemptAndBuildsOnce()
    {
        var factoryCalls = 0;

        WarmAllocationWindow.AssertAllocatesNothing(
            () =>
            {
                factoryCalls++;
                return () => { };
            },
            "literal window");

        Assert.Equal(1, factoryCalls);
        Assert.Equal(0, WarmAllocationWindow.Measure(() => () => { }).Single());
    }
}
