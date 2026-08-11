namespace Sandata.Client.Audio;

/// <summary>
/// Discovery of Sandata's own audio files. Mirrors
/// <c>Hukbo.Client.Audio.SoundLibrary</c>'s answer for the melee side: a raw
/// WAV read at run time, never through the MonoGame content pipeline (see
/// <c>Hukbo.Client.Hukbo.Client.csproj</c>'s own <c>Content/Audio</c> copy
/// rule and its comment for why audio bypasses the pipeline). Sandata.Client
/// owns its own <c>Content</c> directory — a separate MonoGame project from
/// Hukbo.Client — so this folder never collides with the melee side's own
/// files on disk even though both are named <c>Content/Audio</c>.
/// </summary>
/// <remarks>
/// Path construction is kept pure and deliberately separate from the one
/// disk probe below: <see cref="GetDefaultDirectoryPath"/> and
/// <see cref="GetFilePath"/> do no I/O at all, so a caller — and a test — can
/// resolve exactly where a shot's file is expected to live without ever
/// touching a real directory. <see cref="FileExists"/> is the only method
/// here that reads the filesystem, and it never throws: a missing folder or
/// a missing file is data for the caller to report, not an exception to
/// catch further up.
/// </remarks>
internal static class SandataSoundLibrary
{
    /// <summary>
    /// The folder name under <c>Content</c> that holds Sandata's WAV files.
    /// </summary>
    public const string FolderName = "Audio";

    /// <summary>
    /// The folder Sandata reads sound files from at run time:
    /// <c>&lt;base directory&gt;/Content/Audio</c>. Matches the shape of
    /// <c>Hukbo.Client.Audio.SoundLibrary.GetDefaultDirectoryPath</c>.
    /// </summary>
    public static string GetDefaultDirectoryPath() =>
        Path.Combine(AppContext.BaseDirectory, "Content", FolderName);

    /// <summary>
    /// The absolute path of one resolved shot's variant file under
    /// <paramref name="directoryPath"/>, built from
    /// <see cref="SandataSoundCatalog.GetVariantFileName"/>. Pure — no I/O —
    /// which is what lets this half of resolution be tested without a real
    /// filesystem.
    /// </summary>
    public static string GetFilePath(string directoryPath, SoundSlot slot, int variantNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        var fileName = SandataSoundCatalog.GetVariantFileName(slot, variantNumber);
        return Path.Combine(directoryPath, fileName);
    }

    /// <summary>
    /// Whether <paramref name="filePath"/> exists as a readable file. The one
    /// disk probe in this type. An inaccessible path (a permissions error, a
    /// removed drive) resolves to <see langword="false"/> rather than
    /// throwing — an owner's audio folder being briefly unreadable must never
    /// be the reason a shot's playback attempt crashes.
    /// </summary>
    public static bool FileExists(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        try
        {
            return File.Exists(filePath);
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException)
        {
            return false;
        }
    }
}
