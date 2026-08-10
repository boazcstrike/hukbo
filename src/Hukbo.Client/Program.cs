using System.Diagnostics;
using Hukbo.Client;
using Hukbo.Client.Settings;
using Hukbo.Diagnostics;

var startTimestamp = Stopwatch.GetTimestamp();

// Must happen before anything touches graphics: DPI awareness is process-wide
// state and Windows locks it in once the first window exists. ArenaGame builds
// its GraphicsDeviceManager in its constructor, so the only place this can go
// is above it. See docs/plans/2026-08-11-display-dpi-awareness-design.md.
var dpiAwareness = ProcessDpiAwareness.Apply();
var options = LogOptions.FromEnvironment(Console.Error);
using var log = DiagnosticLog.Create(options, Console.Error);

log.Write(
    LogLevel.Information,
    LogChannel.Boot,
    LogEvents.BootStarted,
    "configuration",
    LogOptions.ConfigurationName,
    "level",
    LogLevels.ToWireName(options.Level),
    "channels",
    options.Channels.ToString(),
    "path",
    log.FilePath);

if (dpiAwareness.Succeeded || !dpiAwareness.Attempted)
{
    log.Write(
        LogLevel.Information,
        LogChannel.Boot,
        LogEvents.BootDpiAwareness,
        "state",
        dpiAwareness.State);
}
else
{
    // A failure here is survivable: the game runs, but on a display with
    // Windows scaling other than 100% the operating system upscales the
    // finished frame and text stops being crisp. Say so, because the next
    // person to read the log will be reading it about exactly that.
    log.Write(
        LogLevel.Warning,
        LogChannel.Boot,
        LogEvents.BootDpiAwareness,
        "state",
        dpiAwareness.State,
        "win32",
        dpiAwareness.Win32ErrorCode,
        "msg",
        "Per-monitor DPI awareness was refused; text will be upscaled by the "
            + "operating system on a scaled display.");
}

try
{
    using var game = new ArenaGame(log);
    game.Run();

    log.Write(
        LogLevel.Information,
        LogChannel.Boot,
        LogEvents.BootStopped,
        "reason",
        "exit",
        "uptimeMs",
        (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds);
}
catch (Exception exception)
{
    // The log line carries the type and message for filtering; the standard
    // error line keeps the full stack trace a person needs in the terminal.
    log.Write(
        LogLevel.Error,
        LogChannel.Boot,
        LogEvents.BootCrashed,
        "reason",
        exception.GetType().Name,
        "msg",
        exception.Message,
        "uptimeMs",
        (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds);
    Console.Error.WriteLine($"Hukbo failed to start: {exception}");
    Environment.ExitCode = 1;
}
finally
{
    log.Flush();
}
