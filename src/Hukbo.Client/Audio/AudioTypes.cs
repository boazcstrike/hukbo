namespace Hukbo.Client.Audio;

/// <summary>
/// One addressable sound slot. Each member maps to exactly one canonical file
/// name through <see cref="SoundCatalog"/>, so dropping a correctly named file
/// into the audio folder is the only step needed to give a slot a sound.
/// </summary>
internal enum GameSoundId
{
    AttackGreatBlade = 0,
    AttackWarAxe = 1,
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

    /// <summary>
    /// The player was asked to play the cue and declined. On the MonoGame
    /// backend this means the instance pool or the OpenAL source list was
    /// exhausted. Distinct from <see cref="Suppressed"/>, which is this game's
    /// own budget deciding not to ask at all.
    /// </summary>
    Refused = 5,
}

/// <summary>
/// One class's raw, pre-fallback file count for a hit-location-driven slot.
/// This is the count an owner uses to see which class still needs a take —
/// showing the fallback-covered count would hide a real gap.
/// </summary>
internal readonly record struct SoundClassCount(
    HitClass HitClass,
    int Count,
    SoundBindingStatus Status);

/// <summary>
/// One slot paired with the files that back it. <see cref="ClassCounts"/> is
/// empty for a slot that is not hit-location driven.
/// <see cref="VariantCount"/> is the raw count of every file discovered for
/// the slot — across every class plus the bare single, before any fallback
/// substitution.
/// </summary>
internal readonly record struct SoundBinding(
    GameSoundId Sound,
    string FileName,
    IReadOnlyList<SoundClassCount> ClassCounts,
    int VariantCount,
    SoundBindingStatus Status);

/// <summary>
/// One resolved, fallback-substituted variant list for a slot, optionally
/// scoped to one hit class. <see cref="FileNames"/> is ordered ascending by
/// the file's variant index. A classless slot (death, the outcome cues, the
/// UI click) carries a <c>null</c> hit class.
/// </summary>
internal readonly record struct SoundVariantList(
    GameSoundId Sound,
    HitClass? HitClass,
    IReadOnlyList<string> FileNames,
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
