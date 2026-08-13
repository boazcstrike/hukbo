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
    /// The zero-padded width of a variant index in a file name, e.g. the
    /// <c>01</c> in <c>attack-kampilan-skull-01.wav</c>. Matches the
    /// <c>{0:D2}</c> format <c>scripts/sfx.ps1</c> writes.
    /// </summary>
    public const int VariantIndexDigits = 2;

    /// <summary>
    /// Every slot, in a fixed order. The panel and the loader both walk this
    /// list, so the display order never depends on enum reflection order.
    /// </summary>
    public static IReadOnlyList<GameSoundId> AllSounds { get; } =
    [
        GameSoundId.AttackKampilan,
        GameSoundId.AttackWasay,
        GameSoundId.AttackKalis,
        GameSoundId.AttackItak,
        GameSoundId.Death,
        GameSoundId.VictoryBlue,
        GameSoundId.VictoryRed,
        GameSoundId.Draw,
        GameSoundId.UiClick,
        GameSoundId.ClashShieldKampilan,
        GameSoundId.ClashShieldWasay,
        GameSoundId.ClashShieldKalis,
        GameSoundId.ClashShieldItak,
        GameSoundId.ReleaseBangkaw,
        GameSoundId.ReleaseBusog,
        GameSoundId.ReleaseArquebus,
        GameSoundId.AttackBangkaw,
        GameSoundId.AttackBusog,
        GameSoundId.AttackArquebus,
        GameSoundId.ClashShieldBangkaw,
        GameSoundId.ClashShieldBusog,
        GameSoundId.ClashShieldArquebus,
        GameSoundId.MissBangkaw,
        GameSoundId.MissBusog,
        GameSoundId.MissArquebus,
        GameSoundId.MisfireArquebus,
    ];

    /// <summary>
    /// The file name, without extension, that backs a slot. Weapon slots are
    /// named for the weapon identity rather than its English descriptor, so
    /// <see cref="Hukbo.Core.Combat.WeaponId.Itak"/> appears here as
    /// <c>attack-itak</c>. These names are internal file-system keys and are
    /// never shown to a spectator; the player-facing label stays the pair form
    /// the historical accuracy policy requires, and it is built elsewhere.
    /// </summary>
    public static string GetBaseName(GameSoundId sound) =>
        sound switch
        {
            GameSoundId.AttackKampilan => "attack-kampilan",
            GameSoundId.AttackWasay => "attack-wasay",
            GameSoundId.AttackKalis => "attack-kalis",
            GameSoundId.AttackItak => "attack-itak",
            GameSoundId.Death => "death",
            GameSoundId.VictoryBlue => "victory-blue",
            GameSoundId.VictoryRed => "victory-red",
            GameSoundId.Draw => "draw",
            GameSoundId.UiClick => "ui-click",
            GameSoundId.ClashShieldKampilan => "clash-shield-kampilan",
            GameSoundId.ClashShieldWasay => "clash-shield-wasay",
            GameSoundId.ClashShieldKalis => "clash-shield-kalis",
            GameSoundId.ClashShieldItak => "clash-shield-itak",
            GameSoundId.ReleaseBangkaw => "release-bangkaw",
            GameSoundId.ReleaseBusog => "release-busog",
            GameSoundId.ReleaseArquebus => "release-arquebus",
            GameSoundId.AttackBangkaw => "attack-bangkaw",
            GameSoundId.AttackBusog => "attack-busog",
            GameSoundId.AttackArquebus => "attack-arquebus",
            GameSoundId.ClashShieldBangkaw => "clash-shield-bangkaw",
            GameSoundId.ClashShieldBusog => "clash-shield-busog",
            GameSoundId.ClashShieldArquebus => "clash-shield-arquebus",
            GameSoundId.MissBangkaw => "miss-bangkaw",
            GameSoundId.MissBusog => "miss-busog",
            GameSoundId.MissArquebus => "miss-arquebus",
            GameSoundId.MisfireArquebus => "misfire-arquebus",
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

    /// <summary>
    /// Whether a slot's variant is chosen by the event's hit location. Only
    /// the seven weapon attack slots carry combat context; every other
    /// slot's variant, if it has one, is chosen from a single slot-level
    /// list. This includes the three ranged attack- slots (the projectile's
    /// impact) but not the ranged release-, miss-, or misfire- slots, which
    /// have no meaningful hit location.
    /// </summary>
    public static bool IsHitLocationDriven(GameSoundId sound) =>
        sound is GameSoundId.AttackKampilan or
            GameSoundId.AttackWasay or
            GameSoundId.AttackKalis or
            GameSoundId.AttackItak or
            GameSoundId.AttackBangkaw or
            GameSoundId.AttackBusog or
            GameSoundId.AttackArquebus;

    /// <summary>
    /// Whether a slot is one of the four melee shield-clash slots that
    /// <see cref="MonoGameSoundPlayer"/> normalises in the sample domain at
    /// load. See sections 3 and 5 of the shield-clash audio legibility
    /// design of 2026-08-13 (archived): only these four slots are touched, and
    /// every other
    /// slot — including the three ranged clash slots — loads byte-identical
    /// to today.
    /// </summary>
    public static bool IsMeleeShieldClash(GameSoundId sound) =>
        sound is GameSoundId.ClashShieldKampilan or
            GameSoundId.ClashShieldWasay or
            GameSoundId.ClashShieldKalis or
            GameSoundId.ClashShieldItak;

    /// <summary>
    /// The file-name prefix shared by every numbered take of one hit class,
    /// e.g. <c>attack-kampilan-skull-</c>. Only valid for a slot where
    /// <see cref="IsHitLocationDriven"/> is <c>true</c>. Used both to build a
    /// full variant file name and, by <see cref="SoundLibrary"/>, to match one
    /// against the owner's folder.
    /// </summary>
    public static string GetVariantPrefix(GameSoundId sound, HitClass hitClass)
    {
        if (!IsHitLocationDriven(sound))
        {
            throw new ArgumentException(
                "Only a hit-location-driven slot has class variants.",
                nameof(sound));
        }

        return GetBaseName(sound) + "-" + HitClassCatalog.GetToken(hitClass) + "-";
    }

    /// <summary>
    /// The file-name prefix shared by every numbered take of a classless slot,
    /// e.g. <c>death-</c>.
    /// </summary>
    public static string GetSlotVariantPrefix(GameSoundId sound) =>
        GetBaseName(sound) + "-";

    /// <summary>
    /// The file name of one hit-class variant, e.g.
    /// <c>attack-kampilan-skull-01.wav</c>. Only valid for a slot where
    /// <see cref="IsHitLocationDriven"/> is <c>true</c>.
    /// </summary>
    public static string GetVariantFileName(
        GameSoundId sound,
        HitClass hitClass,
        int variantIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(variantIndex);

        return GetVariantPrefix(sound, hitClass) +
            variantIndex.ToString("D" + VariantIndexDigits) +
            SupportedExtension;
    }

    /// <summary>
    /// The file name of one classless slot-level variant, e.g.
    /// <c>death-01.wav</c>.
    /// </summary>
    public static string GetSlotVariantFileName(GameSoundId sound, int variantIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(variantIndex);

        return GetSlotVariantPrefix(sound) +
            variantIndex.ToString("D" + VariantIndexDigits) +
            SupportedExtension;
    }

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
