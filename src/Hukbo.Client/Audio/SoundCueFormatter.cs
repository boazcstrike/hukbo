namespace Hukbo.Client.Audio;

/// <summary>
/// Row text for the sound log. Pure string work, mirroring
/// <c>BattleEventFormatter</c>.
/// </summary>
internal static class SoundCueFormatter
{
    public static string Format(SoundCue cue) =>
        cue.Count > 1
            ? $"T{cue.Tick:00000}  {SoundCatalog.GetBaseName(cue.Sound)}  " +
                $"{GetStatusLabel(cue.Status)} x{cue.Count}"
            : $"T{cue.Tick:00000}  {SoundCatalog.GetBaseName(cue.Sound)}  " +
                GetStatusLabel(cue.Status);

    public static string GetStatusLabel(SoundCueStatus status) =>
        status switch
        {
            SoundCueStatus.Played => "PLAYED",
            SoundCueStatus.Missing => "NO FILE",
            SoundCueStatus.LoadFailed => "FAILED",
            SoundCueStatus.Muted => "MUTED",
            SoundCueStatus.Suppressed => "LIMITED",
            _ => throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                null),
        };

    /// <summary>
    /// The one-line summary shown in the panel header when the folder is not
    /// fully populated.
    /// </summary>
    public static string FormatAvailability(int unavailableCount, int totalCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(unavailableCount);
        ArgumentOutOfRangeException.ThrowIfNegative(totalCount);

        return unavailableCount == 0
            ? $"ALL {totalCount} READY"
            : $"MISSING {unavailableCount}/{totalCount}";
    }
}
