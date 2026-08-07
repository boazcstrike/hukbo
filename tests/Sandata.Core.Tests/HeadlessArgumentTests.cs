using Hukbo.Diagnostics;
using Sandata.Headless;

namespace Sandata.Core.Tests;

/// <summary>
/// The argument-parsing, <c>--help</c>, and exit-code contract of
/// <c>Sandata.Headless.Program</c> (plan task 14 of
/// docs/plans/2026-08-07-sandata-scaffold.md), which mirrors
/// <c>Hukbo.Headless.HeadlessRunner</c>'s shape.
/// </summary>
public sealed class HeadlessArgumentTests
{
    [Fact]
    public void HelpReturnsSuccessAndWritesUsageToStandardOutput()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = Program.Run(["--help"], output, error);

        Assert.Equal(Program.ExitSuccess, exitCode);
        Assert.Contains("Usage:", output.ToString(), StringComparison.Ordinal);
        Assert.Empty(error.ToString());
    }

    [Fact]
    public void HelpWinsEvenAlongsideOtherArguments()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = Program.Run(
            ["--log-level", "not-a-level", "--help"], output, error);

        Assert.Equal(Program.ExitSuccess, exitCode);
        Assert.Contains("Usage:", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownFlagReturnsTheArgumentErrorExitCode()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = Program.Run(["--not-a-real-flag", "value"], output, error);

        Assert.Equal(Program.ExitArgumentError, exitCode);
        Assert.Contains(
            "Unsupported argument", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void MissingValueReturnsTheArgumentErrorExitCode()
    {
        var error = new StringWriter();

        var exitCode = Program.Run(["--log-level"], new StringWriter(), error);

        Assert.Equal(Program.ExitArgumentError, exitCode);
        Assert.Contains("requires a value", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateArgumentReturnsTheArgumentErrorExitCode()
    {
        var error = new StringWriter();

        var exitCode = Program.Run(
            ["--log-level", "dbg", "--log-level", "trc"],
            new StringWriter(),
            error);

        Assert.Equal(Program.ExitArgumentError, exitCode);
        Assert.Contains("more than once", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ASuccessfulRunLogsOffReturnsSuccessWithoutTouchingTheFilesystem()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = Program.Run(["--log-level", "off"], output, error);

        Assert.Equal(Program.ExitSuccess, exitCode);
        Assert.Empty(error.ToString());
    }

    [Fact]
    public void TryParseArgumentsAcceptsEachSupportedFlag()
    {
        var parsed = Program.TryParseArguments(
            ["--log-level", "trc", "--log-channels", "boot", "--log-dir", "somewhere"],
            out var options,
            out var error);

        Assert.True(parsed);
        Assert.Equal(string.Empty, error);
        Assert.Equal(LogLevel.Trace, options.LogLevel);
        Assert.Equal(LogChannel.Boot, options.LogChannels);
        Assert.Equal("somewhere", options.LogDirectory);
    }

    [Fact]
    public void UnknownLogChannelIsRejected()
    {
        var parsed = Program.TryParseArguments(
            ["--log-channels", "not-a-channel"], out _, out var error);

        Assert.False(parsed);
        Assert.Contains("unknown channel", error, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyArgumentsParseToAllDefaultsUnset()
    {
        var parsed = Program.TryParseArguments([], out var options, out var error);

        Assert.True(parsed);
        Assert.Equal(string.Empty, error);
        Assert.Null(options.LogLevel);
        Assert.Null(options.LogChannels);
        Assert.Null(options.LogDirectory);
    }

    /// <summary>
    /// The three new <c>ev</c> identifiers plan task 14 adds. Pinned as
    /// literals so a later rename is caught here rather than only by the
    /// broader, unmodifiable
    /// <c>Hukbo.Client.Tests.LogEventCatalogTests</c> catalog facts, which
    /// this test complements rather than replaces: those already enforce
    /// uniqueness, the lowercase-dotted shape, and that every identifier's
    /// leading segment names a declared <see cref="LogChannel"/>.
    /// </summary>
    [Fact]
    public void NewSandataBootEventsArePinnedStableDottedIdentifiers()
    {
        Assert.Equal("boot.sandata.started", LogEvents.BootSandataStarted);
        Assert.Equal("boot.sandata.stopped", LogEvents.BootSandataStopped);
        Assert.Equal("boot.sandata.crashed", LogEvents.BootSandataCrashed);
    }
}
