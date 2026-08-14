using System;
using System.IO;
using System.Linq;
using Hukbo.Diagnostics;

namespace Sandata.Client.Tests;

/// <summary>
/// Decision D3 of the 2026-08-14 lowered-weapon and automatic-fire design: the
/// weapon-lowered transition reaches the debug log, at <c>dbg</c>, from the
/// client.
/// </summary>
/// <remarks>
/// <para>
/// <b>What these tests do not bind.</b> The writer itself,
/// <c>SandataGame.LogWeaponStateTransitionsOn</c>, is private on a type that
/// owns a window and a graphics device, and a client presentation test may not
/// construct either. So the payload's cost is measured directly against a
/// disabled log, and the guard that keeps a disabled run from walking the
/// event feed at all is asserted by reading the source. Neither is a
/// substitute for the smoke row: only a person watching a Debug run can say
/// the line appears when the weapon actually lowers.
/// </para>
/// </remarks>
public sealed class WeaponStateLoggingTests
{
    /// <summary>
    /// The event identifier is a stable dotted machine key under the
    /// <c>sandata</c> namespace, carrying no value and no count, as the
    /// debug-logging standard requires of every <c>ev</c>.
    /// </summary>
    [Fact]
    public void TheEventIdentifierIsAStableDottedKey()
    {
        Assert.Equal("sim.sandata.weaponState", LogEvents.SimSandataWeaponState);
        Assert.DoesNotContain(' ', LogEvents.SimSandataWeaponState);
        Assert.DoesNotContain(LogEvents.SimSandataWeaponState, char.IsDigit);
    }

    /// <summary>
    /// The disabled call allocates nothing, measured the same way
    /// <c>Hukbo.Client.Tests.DiagnosticLogTests.ADisabledWriteAllocatesNothing</c>
    /// measures it, with this line's own two payload pairs.
    /// </summary>
    [Fact]
    public void ADisabledWriteOfThisLineAllocatesNothing()
    {
        var log = DiagnosticLog.Disabled;

        // Warm the JIT so the measurement covers the call and not its first
        // compilation.
        for (var index = 0; index < 100; index++)
        {
            WriteWeaponState(log, index);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 1_000; index++)
        {
            WriteWeaponState(log, index);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    /// <summary>
    /// The writer tests the level and channel before it touches the event
    /// feed, so a <c>Release</c> run does no work at all rather than walking
    /// a feed and discarding every line at the end.
    /// </summary>
    [Fact]
    public void TheWriterTestsTheLevelBeforeItWalksTheEventFeed()
    {
        var root = LogPaths.FindRepositoryRoot(AppContext.BaseDirectory);
        Assert.True(
            root is not null,
            "No ancestor of " + AppContext.BaseDirectory + " contains " +
            LogPaths.RepositoryMarkerFileName + ", so the source cannot be read.");

        var source = File.ReadAllLines(
            Path.Combine(root!, "src", "Sandata.Client", "SandataGame.cs"));

        var methodIndex = Array.FindIndex(
            source,
            line => line.Contains(
                "private void LogWeaponStateTransitionsOn(", StringComparison.Ordinal));
        Assert.True(methodIndex >= 0, "LogWeaponStateTransitionsOn was not found.");

        var body = source.Skip(methodIndex).Take(12).ToArray();
        var guardIndex = Array.FindIndex(
            body, line => line.Contains("IsEnabledFor(LogLevel.Debug", StringComparison.Ordinal));
        var feedIndex = Array.FindIndex(
            body, line => line.Contains("EventFeed", StringComparison.Ordinal));

        Assert.True(guardIndex >= 0, "The level guard was not found in the method's first lines.");
        Assert.True(feedIndex >= 0, "The event-feed read was not found in the method's first lines.");
        Assert.True(guardIndex < feedIndex, "The event feed is read before the level is tested.");
    }

    private static void WriteWeaponState(DiagnosticLog log, int index) =>
        log.Write(
            LogLevel.Debug, LogChannel.Simulation, LogEvents.SimSandataWeaponState,
            "entityId", index,
            "lowered", index % 2 == 0);
}
