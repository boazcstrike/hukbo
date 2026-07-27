using System.Diagnostics;
using Hukbo.Client;
using Hukbo.Diagnostics;

var startTimestamp = Stopwatch.GetTimestamp();
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
