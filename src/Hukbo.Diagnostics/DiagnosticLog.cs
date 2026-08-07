using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Hukbo.Diagnostics;

/// <summary>
/// The debug log every subsystem writes through.
/// </summary>
/// <remarks>
/// <para>
/// A development and testing facility, on by default in <c>Debug</c> and off in
/// <c>Release</c>. It exists so an agent asked to investigate a session has a
/// durable record of what the game did, instead of a description of what
/// someone saw.
/// </para>
/// <para>
/// <c>Hukbo.Core</c> does not reference this assembly and never will. The
/// simulation is forbidden the filesystem and the wall clock, and this class
/// needs both. Every simulation observation is made from outside, by a caller
/// reading state it already holds.
/// </para>
/// <para>
/// Debug-level call sites run inside the frame loop, so a disabled call must
/// cost nothing measurable. Every <c>Write</c> overload tests the level and
/// channel first and returns before touching the builder, and payload values
/// travel as <see cref="LogValue"/> structs so an integer field never boxes.
/// </para>
/// </remarks>
public sealed class DiagnosticLog : IDisposable
{
    /// <summary>
    /// Tick written when an event has no simulation context, such as anything
    /// during startup. Negative so it can never collide with a real tick.
    /// </summary>
    public const long NoTick = -1;

    private readonly Lock _gate = new();
    private readonly StringBuilder _builder = new(256);
    private readonly JsonlLogSink? _sink;
    private readonly long _startTimestamp;
    private long _sequence;
    private long _tick = NoTick;
    private bool _isDisposed;

    private DiagnosticLog(LogOptions options, JsonlLogSink? sink)
    {
        Options = options;
        _sink = sink;
        _startTimestamp = Stopwatch.GetTimestamp();
    }

    /// <summary>
    /// A log that discards everything. The default for every constructor
    /// parameter in the game, so an existing test never has to supply one.
    /// </summary>
    public static DiagnosticLog Disabled { get; } =
        new(LogOptions.Disabled, sink: null);

    /// <summary>The configuration this log resolved to.</summary>
    public LogOptions Options { get; }

    /// <summary>
    /// The absolute path being written, or an empty string when logging is off.
    /// Reported on the first line so there is never a question about where a
    /// log went.
    /// </summary>
    public string FilePath => _sink?.FilePath ?? string.Empty;

    /// <summary>Whether anything at all will be written.</summary>
    public bool IsEnabled => _sink is not null && Options.Level != LogLevel.Off;

    /// <summary>
    /// Opens a log for <paramref name="options"/>. Returns
    /// <see cref="Disabled"/> when the level is <see cref="LogLevel.Off"/>, and
    /// also when the destination cannot be opened — a diagnostics facility must
    /// never be the reason the game fails to start.
    /// </summary>
    /// <param name="warningWriter">Receives one line if the file cannot be opened.</param>
    public static DiagnosticLog Create(
        LogOptions options,
        TextWriter? warningWriter = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Level == LogLevel.Off ||
            options.Channels == LogChannel.None)
        {
            return Disabled;
        }

        try
        {
            var directory = LogPaths.ResolveDirectory(options.DirectoryPath);
            Directory.CreateDirectory(directory);
            var path = Path.Combine(
                directory,
                LogPaths.BuildFileName(
                    DateTime.UtcNow,
                    Environment.ProcessId));
            var sink = new JsonlLogSink(path);
            LogPaths.ApplyRetention(directory);
            return new DiagnosticLog(options, sink);
        }
        catch (Exception exception) when (
            exception is IOException or
                UnauthorizedAccessException or
                NotSupportedException or
                ArgumentException)
        {
            warningWriter?.WriteLine(
                $"Debug logging disabled: {exception.Message}");
            return Disabled;
        }
    }

    /// <summary>
    /// Opens a log writing to <paramref name="writer"/>. For tests, which need
    /// the lines without touching the filesystem.
    /// </summary>
    public static DiagnosticLog CreateForWriter(
        LogOptions options,
        TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(writer);

        return options.Level == LogLevel.Off ||
            options.Channels == LogChannel.None
            ? Disabled
            : new DiagnosticLog(options, new JsonlLogSink(writer));
    }

    /// <summary>
    /// Sets the tick stamped on subsequent lines, so no call site has to thread
    /// the tick through to reach the log. Called once per advanced tick by
    /// whoever drives the simulation.
    /// </summary>
    public void SetTick(long tick)
    {
        // Called once per tick whether or not anything is enabled, so the
        // disabled path must not take the lock.
        if (_sink is null)
        {
            return;
        }

        lock (_gate)
        {
            _tick = tick;
        }
    }

    /// <summary>
    /// Whether a line at this level and channel would be written. Test this
    /// before doing work whose only purpose is to produce a payload value.
    /// </summary>
    public bool IsEnabledFor(LogLevel level, LogChannel channel) =>
        _sink is not null &&
        level != LogLevel.Off &&
        level <= Options.Level &&
        (Options.Channels & channel) != LogChannel.None;

    /// <summary>Pushes buffered lines to the destination.</summary>
    public void Flush()
    {
        if (_sink is null)
        {
            return;
        }

        lock (_gate)
        {
            _sink.Flush();
        }
    }

    public void Write(LogLevel level, LogChannel channel, string eventId)
    {
        if (!IsEnabledFor(level, channel))
        {
            return;
        }

        lock (_gate)
        {
            BeginLine(level, channel, eventId);
            EndLine(level);
        }
    }

    public void Write(
        LogLevel level,
        LogChannel channel,
        string eventId,
        string name1,
        LogValue value1)
    {
        if (!IsEnabledFor(level, channel))
        {
            return;
        }

        lock (_gate)
        {
            BeginLine(level, channel, eventId);
            AppendField(name1, value1);
            EndLine(level);
        }
    }

    public void Write(
        LogLevel level,
        LogChannel channel,
        string eventId,
        string name1,
        LogValue value1,
        string name2,
        LogValue value2)
    {
        if (!IsEnabledFor(level, channel))
        {
            return;
        }

        lock (_gate)
        {
            BeginLine(level, channel, eventId);
            AppendField(name1, value1);
            AppendField(name2, value2);
            EndLine(level);
        }
    }

    public void Write(
        LogLevel level,
        LogChannel channel,
        string eventId,
        string name1,
        LogValue value1,
        string name2,
        LogValue value2,
        string name3,
        LogValue value3)
    {
        if (!IsEnabledFor(level, channel))
        {
            return;
        }

        lock (_gate)
        {
            BeginLine(level, channel, eventId);
            AppendField(name1, value1);
            AppendField(name2, value2);
            AppendField(name3, value3);
            EndLine(level);
        }
    }

    public void Write(
        LogLevel level,
        LogChannel channel,
        string eventId,
        string name1,
        LogValue value1,
        string name2,
        LogValue value2,
        string name3,
        LogValue value3,
        string name4,
        LogValue value4)
    {
        if (!IsEnabledFor(level, channel))
        {
            return;
        }

        lock (_gate)
        {
            BeginLine(level, channel, eventId);
            AppendField(name1, value1);
            AppendField(name2, value2);
            AppendField(name3, value3);
            AppendField(name4, value4);
            EndLine(level);
        }
    }

    public void Write(
        LogLevel level,
        LogChannel channel,
        string eventId,
        string name1,
        LogValue value1,
        string name2,
        LogValue value2,
        string name3,
        LogValue value3,
        string name4,
        LogValue value4,
        string name5,
        LogValue value5)
    {
        if (!IsEnabledFor(level, channel))
        {
            return;
        }

        lock (_gate)
        {
            BeginLine(level, channel, eventId);
            AppendField(name1, value1);
            AppendField(name2, value2);
            AppendField(name3, value3);
            AppendField(name4, value4);
            AppendField(name5, value5);
            EndLine(level);
        }
    }

    public void Write(
        LogLevel level,
        LogChannel channel,
        string eventId,
        string name1,
        LogValue value1,
        string name2,
        LogValue value2,
        string name3,
        LogValue value3,
        string name4,
        LogValue value4,
        string name5,
        LogValue value5,
        string name6,
        LogValue value6)
    {
        if (!IsEnabledFor(level, channel))
        {
            return;
        }

        lock (_gate)
        {
            BeginLine(level, channel, eventId);
            AppendField(name1, value1);
            AppendField(name2, value2);
            AppendField(name3, value3);
            AppendField(name4, value4);
            AppendField(name5, value5);
            AppendField(name6, value6);
            EndLine(level);
        }
    }

    public void Write(
        LogLevel level,
        LogChannel channel,
        string eventId,
        string name1,
        LogValue value1,
        string name2,
        LogValue value2,
        string name3,
        LogValue value3,
        string name4,
        LogValue value4,
        string name5,
        LogValue value5,
        string name6,
        LogValue value6,
        string name7,
        LogValue value7)
    {
        if (!IsEnabledFor(level, channel))
        {
            return;
        }

        lock (_gate)
        {
            BeginLine(level, channel, eventId);
            AppendField(name1, value1);
            AppendField(name2, value2);
            AppendField(name3, value3);
            AppendField(name4, value4);
            AppendField(name5, value5);
            AppendField(name6, value6);
            AppendField(name7, value7);
            EndLine(level);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_isDisposed || _sink is null)
        {
            return;
        }

        _isDisposed = true;
        lock (_gate)
        {
            _sink.Dispose();
        }
    }

    /// <summary>
    /// Opens the object and writes the six leading fields, always in this
    /// order. Their order is part of the contract: a reader that skips parsing
    /// can still slice a line by eye.
    /// </summary>
    private void BeginLine(LogLevel level, LogChannel channel, string eventId)
    {
        var elapsed = Stopwatch.GetElapsedTime(_startTimestamp);
        _builder.Clear();
        _builder
            .Append("{\"seq\":")
            .Append((++_sequence).ToString(CultureInfo.InvariantCulture))
            .Append(",\"t\":")
            .Append(_tick.ToString(CultureInfo.InvariantCulture))
            .Append(",\"ms\":")
            .Append(
                ((long)elapsed.TotalMilliseconds)
                    .ToString(CultureInfo.InvariantCulture))
            .Append(",\"lvl\":\"")
            .Append(LogLevels.ToWireName(level))
            .Append("\",\"ch\":\"")
            .Append(LogChannels.ToWireName(channel))
            .Append("\",\"ev\":");
        JsonText.AppendQuoted(_builder, eventId);
    }

    private void AppendField(string name, LogValue value)
    {
        _builder.Append(',');
        JsonText.AppendQuoted(_builder, name);
        _builder.Append(':');
        value.AppendTo(_builder);
    }

    /// <summary>
    /// Closes the object, hands it to the sink, and flushes when the line is
    /// one a reader would be unhappy to lose to a crash.
    /// </summary>
    private void EndLine(LogLevel level)
    {
        _builder.Append('}');
        _sink!.WriteLine(_builder);
        if (level <= LogLevel.Warning)
        {
            _sink.Flush();
        }
    }
}
