using Hukbo.Client.Diagnostics;

namespace Hukbo.Client.Tests;

/// <summary>
/// The frame-timing summary is the evidence a "the game went laggy" report is
/// settled with, so what it reports has to be exactly what happened: a window
/// that closes early, or averages the wrong frames, or loses a starved frame,
/// answers the question wrongly and confidently.
/// </summary>
public sealed class FrameTimingAggregatorTests
{
    [Fact]
    public void NoWindowClosesBeforeAWindowOfTimeHasPassed()
    {
        var aggregator = new FrameTimingAggregator();

        for (var frame = 0; frame < 59; frame++)
        {
            aggregator.Add(16d, 4d, 8d, 1, simulationStarved: false);
        }

        Assert.False(aggregator.TryTakeWindow(out _));
        Assert.Equal(59, aggregator.PendingFrames);
    }

    [Fact]
    public void NoWindowClosesWithoutFrames()
    {
        var aggregator = new FrameTimingAggregator();

        Assert.False(aggregator.TryTakeWindow(out _));
    }

    [Fact]
    public void AClosedWindowReportsTheFramesItObserved()
    {
        var aggregator = new FrameTimingAggregator();

        // Sixty-two frames of 16.2ms is 1,004.4ms: the first sample past the
        // window boundary, which is where a real frame loop crosses it.
        for (var frame = 0; frame < 62; frame++)
        {
            aggregator.Add(16.2d, 4d, 8d, 1, simulationStarved: false);
        }

        Assert.True(aggregator.TryTakeWindow(out var window));
        Assert.Equal(62, window.Frames);
        Assert.Equal(1004.4d, window.ElapsedMilliseconds, 6);
        Assert.Equal(16.2d, window.MeanMilliseconds, 6);
        Assert.Equal(16.2d, window.WorstMilliseconds, 6);
        Assert.Equal(62, window.SimulationTicks);
        Assert.Equal(0, window.StarvedFrames);
    }

    [Fact]
    public void TheWorstFrameSurvivesTheAverage()
    {
        var aggregator = new FrameTimingAggregator();

        aggregator.Add(500d, 400d, 60d, 10, simulationStarved: true);
        for (var frame = 0; frame < 32; frame++)
        {
            aggregator.Add(16d, 4d, 8d, 1, simulationStarved: false);
        }

        Assert.True(aggregator.TryTakeWindow(out var window));
        Assert.Equal(500d, window.WorstMilliseconds, 6);
        Assert.Equal(400d, window.WorstUpdateMilliseconds, 6);
        Assert.Equal(60d, window.WorstDrawMilliseconds, 6);
        Assert.Equal(1, window.StarvedFrames);

        // The finding is that one frame took half a second, and a mean of
        // ~30ms would read as a healthy 33fps window if the worst frame were
        // not carried beside it.
        Assert.True(window.MeanMilliseconds < 40d);
    }

    /// <summary>
    /// The worst update and the worst draw are not promised to come from the
    /// worst frame. A window whose bad frame was spent outside this class's
    /// own code — in present, in the driver, in another process — must report
    /// exactly that rather than blaming whichever span happened to be largest.
    /// </summary>
    [Fact]
    public void AStallOutsideUpdateAndDrawIsVisibleAsSuch()
    {
        var aggregator = new FrameTimingAggregator();

        aggregator.Add(900d, 3d, 5d, 10, simulationStarved: true);
        aggregator.Add(120d, 20d, 90d, 2, simulationStarved: false);

        Assert.True(aggregator.TryTakeWindow(out var window));
        Assert.Equal(900d, window.WorstMilliseconds, 6);
        Assert.Equal(20d, window.WorstUpdateMilliseconds, 6);
        Assert.Equal(90d, window.WorstDrawMilliseconds, 6);
    }

    [Fact]
    public void EveryStarvedFrameIsCounted()
    {
        var aggregator = new FrameTimingAggregator();

        for (var frame = 0; frame < 4; frame++)
        {
            aggregator.Add(300d, 250d, 40d, 10, simulationStarved: true);
        }

        Assert.True(aggregator.TryTakeWindow(out var window));
        Assert.Equal(4, window.StarvedFrames);
        Assert.Equal(40, window.SimulationTicks);
    }

    [Fact]
    public void TakingAWindowStartsTheNextOneEmpty()
    {
        var aggregator = new FrameTimingAggregator();

        aggregator.Add(1_200d, 900d, 200d, 24, simulationStarved: true);
        Assert.True(aggregator.TryTakeWindow(out _));

        Assert.Equal(0, aggregator.PendingFrames);
        aggregator.Add(16d, 4d, 8d, 1, simulationStarved: false);
        Assert.False(aggregator.TryTakeWindow(out _));

        aggregator.Add(1_000d, 4d, 8d, 20, simulationStarved: false);
        Assert.True(aggregator.TryTakeWindow(out var window));
        Assert.Equal(2, window.Frames);
        Assert.Equal(0, window.StarvedFrames);
        Assert.Equal(21, window.SimulationTicks);
    }

    [Fact]
    public void ResetDropsTheOpenWindow()
    {
        var aggregator = new FrameTimingAggregator();

        aggregator.Add(800d, 700d, 60d, 16, simulationStarved: true);
        aggregator.Reset();

        Assert.Equal(0, aggregator.PendingFrames);
        Assert.False(aggregator.TryTakeWindow(out _));

        aggregator.Add(1_100d, 10d, 20d, 22, simulationStarved: false);
        Assert.True(aggregator.TryTakeWindow(out var window));
        Assert.Equal(1, window.Frames);
        Assert.Equal(0, window.StarvedFrames);
        Assert.Equal(1_100d, window.WorstMilliseconds, 6);
    }

    /// <summary>
    /// A diagnostic may not throw inside the loop it measures, and MonoGame
    /// reports an occasional zero or absurd elapsed time around a window
    /// resize, a device reset, or a debugger break.
    /// </summary>
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-5d)]
    public void ANonsenseDurationIsTreatedAsZeroRatherThanThrowing(
        double milliseconds)
    {
        var aggregator = new FrameTimingAggregator();

        aggregator.Add(milliseconds, milliseconds, milliseconds, -3, false);

        Assert.Equal(1, aggregator.PendingFrames);
        Assert.False(aggregator.TryTakeWindow(out _));

        aggregator.Add(1_000d, 10d, 20d, 20, simulationStarved: false);
        Assert.True(aggregator.TryTakeWindow(out var window));
        Assert.Equal(2, window.Frames);
        Assert.Equal(1_000d, window.ElapsedMilliseconds, 6);
        Assert.Equal(20, window.SimulationTicks);
    }
}
