namespace Hukbo.Client.Audio;

/// <summary>
/// One addressable sound slot. Each member maps to exactly one canonical file
/// name through <see cref="SoundCatalog"/>, so dropping a correctly named file
/// into the audio folder is the only step needed to give a slot a sound.
/// </summary>
internal enum GameSoundId
{
    AttackGreatBlade = 0,
    AttackHeavyChopper = 1,
    AttackThrustingBlade = 2,
    AttackWorkBlade = 3,
    Death = 4,
    VictoryBlue = 5,
    VictoryRed = 6,
    Draw = 7,
    UiClick = 8,
}

/// <summary>
/// The state of one slot's backing file after discovery and loading.
/// </summary>
internal enum SoundBindingStatus
{
    /// <summary>A file was found and loaded.</summary>
    Ready = 0,

    /// <summary>No file with the slot's canonical name is present.</summary>
    Missing = 1,

    /// <summary>
    /// A file is present but could not be loaded as uncompressed PCM WAV data.
    /// </summary>
    LoadFailed = 2,
}

/// <summary>
/// Why one candidate cue did or did not reach the audio device.
/// </summary>
internal enum SoundCueStatus
{
    Played = 0,
    Missing = 1,
    LoadFailed = 2,
    Muted = 3,
    Suppressed = 4,
}

/// <summary>
/// One slot paired with the file that backs it. <see cref="FilePath"/> is
/// <c>null</c> when <see cref="Status"/> is
/// <see cref="SoundBindingStatus.Missing"/>.
/// </summary>
internal readonly record struct SoundBinding(
    GameSoundId Sound,
    string FileName,
    string? FilePath,
    SoundBindingStatus Status);

/// <summary>
/// One row of sound evidence. Consecutive cues sharing
/// <see cref="Tick"/>, <see cref="Sound"/>, and <see cref="Status"/> collapse
/// into a single row whose <see cref="Count"/> increments.
/// </summary>
internal readonly record struct SoundCue(
    long Tick,
    GameSoundId Sound,
    SoundCueStatus Status,
    int Count);
