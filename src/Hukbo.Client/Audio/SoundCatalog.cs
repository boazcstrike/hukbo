namespace Hukbo.Client.Audio;

/// <summary>
/// The naming contract between the owner's audio folder and the game. Nothing
/// here touches MonoGame or the filesystem: it is the pure slot-to-file-name
/// mapping that both the loader and the sound panel read.
/// </summary>
internal static class SoundCatalog
{
    /// <summary>
    /// The only supported container. <c>SoundEffect.FromStream</c> reads
    /// uncompressed PCM WAV data and nothing else.
    /// </summary>
    public const string SupportedExtension = ".wav";

    /// <summary>
    /// The audio folder's name, resolved beneath the client's content folder.
    /// </summary>
    public const string FolderName = "Audio";

    /// <summary>
    /// Every slot, in a fixed order. The panel and the loader both walk this
    /// list, so the display order never depends on enum reflection order.
    /// </summary>
    public static IReadOnlyList<GameSoundId> AllSounds { get; } =
    [
        GameSoundId.AttackGreatBlade,
        GameSoundId.AttackHeavyChopper,
        GameSoundId.AttackThrustingBlade,
        GameSoundId.AttackWorkBlade,
        GameSoundId.Death,
        GameSoundId.VictoryBlue,
        GameSoundId.VictoryRed,
        GameSoundId.Draw,
        GameSoundId.UiClick,
    ];

    /// <summary>
    /// The file name, without extension, that backs a slot. Weapon slots use
    /// the player-facing descriptors required by the historical accuracy
    /// policy, so <see cref="Hukbo.Core.Combat.WeaponId.Bolo"/> appears here as
    /// <c>attack-work-blade</c>.
    /// </summary>
    public static string GetBaseName(GameSoundId sound) =>
        sound switch
        {
            GameSoundId.AttackGreatBlade => "attack-great-blade",
            GameSoundId.AttackHeavyChopper => "attack-heavy-chopper",
            GameSoundId.AttackThrustingBlade => "attack-thrusting-blade",
            GameSoundId.AttackWorkBlade => "attack-work-blade",
            GameSoundId.Death => "death",
            GameSoundId.VictoryBlue => "victory-blue",
            GameSoundId.VictoryRed => "victory-red",
            GameSoundId.Draw => "draw",
            GameSoundId.UiClick => "ui-click",
            _ => throw new ArgumentOutOfRangeException(
                nameof(sound),
                sound,
                "Every sound slot must declare a canonical file name."),
        };

    /// <summary>
    /// The exact file name the owner must use for a slot.
    /// </summary>
    public static string GetFileName(GameSoundId sound) =>
        GetBaseName(sound) + SupportedExtension;

    public static string GetStatusLabel(SoundBindingStatus status) =>
        status switch
        {
            SoundBindingStatus.Ready => "READY",
            SoundBindingStatus.Missing => "MISSING",
            SoundBindingStatus.LoadFailed => "FAILED",
            _ => throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                null),
        };

    public static int CountUnavailable(IReadOnlyList<SoundBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);

        var count = 0;
        for (var index = 0; index < bindings.Count; index++)
        {
            if (bindings[index].Status != SoundBindingStatus.Ready)
            {
                count++;
            }
        }

        return count;
    }
}
