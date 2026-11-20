using System.Globalization;

namespace Hukbo.Core.Tests;

/// <summary>
/// Measures whether a warm window allocates anything, and tells a sim
/// allocation apart from a runtime one by repeating the window on fresh state.
/// </summary>
/// <remarks>
/// <para>
/// A single measured window cannot make that distinction. Run alongside other
/// test classes in one process, a warm-tick window at a thousand agents
/// sometimes reports exactly 1,056 bytes, in one tick, at a random tick (sim
/// ticks 88, 92 and 105 were seen) of a random battle, while the battles before
/// and after it report zero. It disappears with <c>DOTNET_TieredPGO=0</c> and
/// with <c>DOTNET_TieredCompilation=0</c>, so it is the runtime's tiered JIT
/// with dynamic PGO allocating on the test thread at an arbitrary moment, not a
/// per-tick allocation in the simulation.
/// </para>
/// <para>
/// A simulation allocation is deterministic: it lands on the same tick of every
/// fresh battle, so it shows up in every attempt and the check still fails.
/// Runtime noise does not repeat <see cref="MaximumAttempts"/> times in a row.
/// The zero-byte bar is unchanged; only the evidence for failing it is.
/// </para>
/// </remarks>
internal static class WarmAllocationWindow
{
    /// <summary>How many fresh windows are tried before a non-zero result is believed.</summary>
    internal const int MaximumAttempts = 5;

    /// <summary>
    /// Runs attempts until one allocates nothing or <see cref="MaximumAttempts"/>
    /// is reached, and returns the bytes each attempt allocated, in order.
    /// <paramref name="prepareAttempt"/> builds fresh state, warms it, and
    /// returns the action to measure; both it and the action it returns are
    /// created outside the measured span.
    /// </summary>
    internal static long[] Measure(Func<Action> prepareAttempt)
    {
        var attemptBytes = new long[MaximumAttempts];
        var attempts = 0;
        while (attempts < MaximumAttempts)
        {
            var measured = prepareAttempt();
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            measured();
            var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            attemptBytes[attempts++] = allocatedBytes;
            if (allocatedBytes == 0)
            {
                break;
            }
        }

        return attemptBytes[..attempts];
    }

    /// <summary>
    /// Runs <paramref name="runAttempt"/> until <paramref name="failureOf"/>
    /// accepts its measurement or <see cref="MaximumAttempts"/> is reached, for
    /// bars that are a ceiling rather than zero. Each call of
    /// <paramref name="runAttempt"/> must build fresh state, warm it, measure
    /// and return the measurement, so a runtime allocation that lands in one
    /// window is not carried into the next. <paramref name="failureOf"/>
    /// returns <see langword="null"/> when every bar holds and the text of the
    /// violated bar otherwise; the failure message lists every attempt's text.
    /// </summary>
    internal static void AssertEventually<T>(
        Func<T> runAttempt,
        Func<T, string?> failureOf,
        string description)
    {
        var failures = new List<string>(MaximumAttempts);
        while (failures.Count < MaximumAttempts)
        {
            var failure = failureOf(runAttempt());
            if (failure is null)
            {
                return;
            }

            failures.Add(failure);
        }

        var perAttempt = string.Join(
            Environment.NewLine,
            failures.Select((text, index) => string.Create(
                CultureInfo.InvariantCulture,
                $"attempt {index + 1}: {text}")));
        Assert.Fail(
            $"{description} failed in all {failures.Count} attempts:" +
            $"{Environment.NewLine}{perAttempt}");
    }

    /// <summary>
    /// Fails unless an attempt allocated nothing. The message names the bytes of
    /// every attempt so a deterministic allocation is visibly repeated.
    /// </summary>
    internal static void AssertAllocatesNothing(Func<Action> prepareAttempt, string description)
    {
        var attemptBytes = Measure(prepareAttempt);
        if (attemptBytes[^1] == 0)
        {
            return;
        }

        var perAttempt = string.Join(
            "; ",
            attemptBytes.Select((bytes, index) => string.Create(
                CultureInfo.InvariantCulture,
                $"attempt {index + 1}: {bytes:N0} bytes")));
        Assert.Fail(
            $"{description} allocated in all {attemptBytes.Length} attempts " +
            $"({perAttempt}); expected none.");
    }
}
