using Hukbo.Client.Settings;

namespace Hukbo.Client.Tests;

/// <summary>
/// Covers the pure decisions around the DPI awareness declaration.
/// </summary>
/// <remarks>
/// Nothing here calls <see cref="ProcessDpiAwareness.Apply"/>. Awareness is
/// process-wide state that cannot be undone once set, so a test that invoked
/// it would either fight the test host's own awareness level or leak into
/// every other test in the assembly, and it would be asserting on Windows
/// rather than on Hukbo. The absence is deliberate; see section 6 of
/// docs/plans/2026-08-11-display-dpi-awareness-design.md. What is testable is
/// the platform gate and the shape of the evidence the run leaves behind, and
/// both are below.
/// </remarks>
public sealed class ProcessDpiAwarenessTests
{
    [Fact]
    public void TheDeclarationIsAttemptedOnWindowsOnly()
    {
        Assert.True(ProcessDpiAwareness.ShouldAttempt(isWindows: true));
        Assert.False(ProcessDpiAwareness.ShouldAttempt(isWindows: false));
    }

    [Fact]
    public void ASkippedDeclarationCarriesNoErrorCode()
    {
        var outcome = ProcessDpiAwareness.DescribeOutcome(
            attempted: false,
            succeeded: false,
            win32ErrorCode: 5);

        Assert.False(outcome.Attempted);
        Assert.False(outcome.Succeeded);
        Assert.Equal(0, outcome.Win32ErrorCode);
        Assert.Equal("skipped", outcome.State);
    }

    [Fact]
    public void ASuccessfulDeclarationReportsApplied()
    {
        var outcome = ProcessDpiAwareness.DescribeOutcome(
            attempted: true,
            succeeded: true,
            win32ErrorCode: 0);

        Assert.True(outcome.Attempted);
        Assert.True(outcome.Succeeded);
        Assert.Equal(0, outcome.Win32ErrorCode);
        Assert.Equal("applied", outcome.State);
    }

    [Fact]
    public void AFailedDeclarationKeepsTheErrorCode()
    {
        var outcome = ProcessDpiAwareness.DescribeOutcome(
            attempted: true,
            succeeded: false,
            win32ErrorCode: 5);

        Assert.True(outcome.Attempted);
        Assert.False(outcome.Succeeded);
        Assert.Equal(5, outcome.Win32ErrorCode);
        Assert.Equal("failed", outcome.State);
    }

    /// <summary>
    /// The three states are distinct machine keys. A log reader filtering on
    /// <c>state</c> has to be able to tell a refused declaration from one that
    /// was never attempted, because the two mean different things: refused is
    /// a Windows build or host problem worth reporting, skipped is simply not
    /// Windows.
    /// </summary>
    [Fact]
    public void EveryOutcomeStateIsDistinct()
    {
        var states = new[]
        {
            ProcessDpiAwareness.DescribeOutcome(false, false, 0).State,
            ProcessDpiAwareness.DescribeOutcome(true, true, 0).State,
            ProcessDpiAwareness.DescribeOutcome(true, false, 5).State,
        };

        Assert.Equal(states.Length, states.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// A success never carries an error code and a failure always does, so the
    /// payload can never claim both.
    /// </summary>
    [Fact]
    public void SuccessAndAnErrorCodeAreMutuallyExclusive()
    {
        var succeeded = ProcessDpiAwareness.DescribeOutcome(true, true, 0);
        var failed = ProcessDpiAwareness.DescribeOutcome(true, false, 1400);

        Assert.Equal(0, succeeded.Win32ErrorCode);
        Assert.NotEqual(0, failed.Win32ErrorCode);
    }
}
