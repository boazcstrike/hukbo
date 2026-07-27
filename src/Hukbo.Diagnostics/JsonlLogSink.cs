using System.Text;

namespace Hukbo.Diagnostics;

/// <summary>
/// Writes finished log lines to a JSON Lines destination.
/// </summary>
/// <remarks>
/// Takes a <see cref="StringBuilder"/> rather than a <see cref="string"/> so a
/// line costs no string allocation: <see cref="TextWriter.Write(StringBuilder)"/>
/// copies straight out of the builder's chunks.
/// </remarks>
public sealed class JsonlLogSink : IDisposable
{
    private readonly TextWriter _writer;
    private readonly bool _ownsWriter;
    private bool _isDisposed;

    /// <summary>
    /// Opens <paramref name="filePath"/> for writing, creating the parent
    /// directory if needed. UTF-8 without a byte order mark and <c>\n</c> line
    /// endings, so the file is identical on every platform and every tool reads
    /// it the same way.
    /// </summary>
    public JsonlLogSink(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        FilePath = Path.GetFullPath(filePath);
        _writer = new StreamWriter(
            FilePath,
            append: false,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = false,
            NewLine = "\n",
        };
        _ownsWriter = true;
    }

    /// <summary>
    /// Writes to a caller-supplied writer, which the sink does not own or
    /// dispose. Used by tests to capture lines in memory.
    /// </summary>
    public JsonlLogSink(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        _writer = writer;
        _ownsWriter = false;
        FilePath = string.Empty;
    }

    /// <summary>
    /// The absolute path being written, or an empty string when the sink wraps
    /// a caller-supplied writer.
    /// </summary>
    public string FilePath { get; }

    /// <summary>Appends one finished line and a newline.</summary>
    public void WriteLine(StringBuilder line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (_isDisposed)
        {
            return;
        }

        _writer.Write(line);
        _writer.Write('\n');
    }

    /// <summary>Pushes buffered lines to the destination.</summary>
    public void Flush()
    {
        if (!_isDisposed)
        {
            _writer.Flush();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _writer.Flush();
        if (_ownsWriter)
        {
            _writer.Dispose();
        }
    }
}
