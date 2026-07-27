using System.Text.Json;
using Hukbo.Diagnostics;

namespace Hukbo.Client.Tests;

/// <summary>
/// The wire contract: field order, escaping, filtering, and the promise that a
/// disabled call costs nothing.
/// </summary>
public sealed class DiagnosticLogTests
{
    [Fact]
    public void ALineCarriesTheSixLeadingFieldsInOrderThenItsPayload()
    {
        var writer = new StringWriter();
        using var log = DiagnosticLog.CreateForWriter(
            new LogOptions(LogLevel.Debug, LogChannel.All, null),
            writer);

        log.SetTick(1420);
        log.Write(
            LogLevel.Debug,
            LogChannel.Audio,
            LogEvents.AudioCue,
            "slot",
            "Death",
            "voices",
            11);

        var line = Assert.Single(ReadLines(writer));
        var fields = ReadFieldNames(line);
        Assert.Equal(
            ["seq", "t", "ms", "lvl", "ch", "ev", "slot", "voices"],
            fields);

        using var document = JsonDocument.Parse(line);
        var root = document.RootElement;
        Assert.Equal(1, root.GetProperty("seq").GetInt64());
        Assert.Equal(1420, root.GetProperty("t").GetInt64());
        Assert.Equal("dbg", root.GetProperty("lvl").GetString());
        Assert.Equal("audio", root.GetProperty("ch").GetString());
        Assert.Equal(LogEvents.AudioCue, root.GetProperty("ev").GetString());
        Assert.Equal("Death", root.GetProperty("slot").GetString());
        Assert.Equal(11, root.GetProperty("voices").GetInt32());
    }

    [Fact]
    public void TheSequenceCounterIncrementsOncePerWrittenLine()
    {
        var writer = new StringWriter();
        using var log = DiagnosticLog.CreateForWriter(
            new LogOptions(LogLevel.Debug, LogChannel.All, null),
            writer);

        log.Write(LogLevel.Information, LogChannel.Boot, LogEvents.BootStarted);
        log.Write(LogLevel.Trace, LogChannel.Boot, LogEvents.BootStopped);
        log.Write(LogLevel.Information, LogChannel.Boot, LogEvents.BootStopped);

        var lines = ReadLines(writer);
        Assert.Equal(2, lines.Length);
        Assert.Equal([1, 2], lines.Select(GetSequence).ToArray());
    }

    [Fact]
    public void ALevelBelowTheThresholdIsDropped()
    {
        var writer = new StringWriter();
        using var log = DiagnosticLog.CreateForWriter(
            new LogOptions(LogLevel.Information, LogChannel.All, null),
            writer);

        log.Write(LogLevel.Debug, LogChannel.Boot, LogEvents.BootStarted);
        log.Write(LogLevel.Trace, LogChannel.Boot, LogEvents.BootStarted);
        log.Write(LogLevel.Warning, LogChannel.Boot, LogEvents.BootStarted);

        var line = Assert.Single(ReadLines(writer));
        Assert.Contains("\"lvl\":\"warn\"", line, StringComparison.Ordinal);
    }

    [Fact]
    public void AChannelOutsideTheMaskIsDropped()
    {
        var writer = new StringWriter();
        using var log = DiagnosticLog.CreateForWriter(
            new LogOptions(LogLevel.Trace, LogChannel.Audio, null),
            writer);

        log.Write(LogLevel.Information, LogChannel.Input, LogEvents.InputKey);
        log.Write(LogLevel.Information, LogChannel.Audio, LogEvents.AudioFrame);

        var line = Assert.Single(ReadLines(writer));
        Assert.Contains("\"ch\":\"audio\"", line, StringComparison.Ordinal);
    }

    /// <summary>
    /// Windows paths are full of backslashes and every one of them has to
    /// survive the round trip, or a reader parsing the boot line gets a path
    /// that does not exist.
    /// </summary>
    [Fact]
    public void StringPayloadsAreEscapedAndRoundTrip()
    {
        var writer = new StringWriter();
        using var log = DiagnosticLog.CreateForWriter(
            new LogOptions(LogLevel.Debug, LogChannel.All, null),
            writer);

        const string awkward = "C:\\logs\\\"quoted\"\n\tvalue\u0001";
        log.Write(
            LogLevel.Debug,
            LogChannel.Boot,
            LogEvents.BootStarted,
            "path",
            awkward);

        var line = Assert.Single(ReadLines(writer));
        using var document = JsonDocument.Parse(line);
        Assert.Equal(
            awkward,
            document.RootElement.GetProperty("path").GetString());
    }

    [Fact]
    public void PayloadValuesKeepTheirJsonTypes()
    {
        var writer = new StringWriter();
        using var log = DiagnosticLog.CreateForWriter(
            new LogOptions(LogLevel.Debug, LogChannel.All, null),
            writer);

        log.Write(
            LogLevel.Debug,
            LogChannel.Audio,
            LogEvents.AudioCue,
            "count",
            42,
            "gain",
            0.65f,
            "muted",
            false,
            "hitClass",
            (string?)null);

        var line = Assert.Single(ReadLines(writer));
        using var document = JsonDocument.Parse(line);
        var root = document.RootElement;
        Assert.Equal(JsonValueKind.Number, root.GetProperty("count").ValueKind);
        Assert.Equal(JsonValueKind.Number, root.GetProperty("gain").ValueKind);
        Assert.Equal(JsonValueKind.False, root.GetProperty("muted").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("hitClass").ValueKind);
        Assert.Equal(42, root.GetProperty("count").GetInt32());
        Assert.Equal(0.65f, root.GetProperty("gain").GetSingle());
    }

    /// <summary>
    /// Debug call sites run inside the frame loop. If a disabled call allocates,
    /// the per-tick allocation budget is being spent on diagnostics nobody
    /// asked for.
    /// </summary>
    [Fact]
    public void ADisabledWriteAllocatesNothing()
    {
        var log = DiagnosticLog.Disabled;

        // Warm the JIT so the measurement covers the call and not its first
        // compilation.
        for (var index = 0; index < 100; index++)
        {
            WriteSixPairs(log, index);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 1_000; index++)
        {
            WriteSixPairs(log, index);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void ADisabledLogReportsItselfAsDisabledAndWritesNoPath()
    {
        Assert.False(DiagnosticLog.Disabled.IsEnabled);
        Assert.Equal(string.Empty, DiagnosticLog.Disabled.FilePath);
        Assert.False(DiagnosticLog.Disabled.IsEnabledFor(
            LogLevel.Error,
            LogChannel.Boot));
    }

    [Fact]
    public void CreateReturnsTheDisabledLogWhenTheLevelIsOff()
    {
        var log = DiagnosticLog.Create(LogOptions.Disabled);
        Assert.Same(DiagnosticLog.Disabled, log);
    }

    private static void WriteSixPairs(DiagnosticLog log, int index) =>
        log.Write(
            LogLevel.Debug,
            LogChannel.Audio,
            LogEvents.AudioCue,
            "tick",
            index,
            "slot",
            "Death",
            "hitClass",
            "Torso",
            "status",
            "Played",
            "variant",
            index,
            "gain",
            0.65f);

    private static long GetSequence(string line)
    {
        using var document = JsonDocument.Parse(line);
        return document.RootElement.GetProperty("seq").GetInt64();
    }

    private static string[] ReadFieldNames(string line)
    {
        using var document = JsonDocument.Parse(line);
        return document.RootElement
            .EnumerateObject()
            .Select(property => property.Name)
            .ToArray();
    }

    private static string[] ReadLines(StringWriter writer) =>
        writer.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
}
