using System.Globalization;

namespace Hukbo.Client.Audio;

/// <summary>
/// Discovery of the owner's audio files. <see cref="Resolve"/> and
/// <see cref="ResolveVariants"/> are pure — they take the folder's file names
/// as data — so the resolution and fallback rules are testable without
/// touching a real directory.
/// </summary>
internal static class SoundLibrary
{
    /// <summary>
    /// The folder the game reads sound files from:
    /// <c>&lt;base directory&gt;/Content/Audio</c>.
    /// </summary>
    public static string GetDefaultDirectoryPath() =>
        Path.Combine(
            AppContext.BaseDirectory,
            "Content",
            SoundCatalog.FolderName);

    /// <summary>
    /// Lists the file names directly inside <paramref name="directoryPath"/>.
    /// A folder that does not exist, or that cannot be read, yields an empty
    /// list: an absent audio folder is a silent game, not an error.
    /// </summary>
    public static IReadOnlyList<string> ListFileNames(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        try
        {
            if (!Directory.Exists(directoryPath))
            {
                return [];
            }

            var paths = Directory.GetFiles(directoryPath);
            var fileNames = new string[paths.Length];
            for (var index = 0; index < paths.Length; index++)
            {
                fileNames[index] = Path.GetFileName(paths[index]);
            }

            return fileNames;
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>
    /// One row per slot, aggregating <see cref="SoundBinding.ClassCounts"/>
    /// and <see cref="SoundBinding.VariantCount"/> from the raw (pre-fallback)
    /// file matches. A slot's <see cref="SoundBindingStatus"/> is
    /// <see cref="SoundBindingStatus.Missing"/> only when literally no file —
    /// no class variant and no bare single — was found for it anywhere; any
    /// other slot is reported ready, because the fallback chain in
    /// <see cref="ResolveVariants"/> guarantees it can still play something.
    /// </summary>
    public static IReadOnlyList<SoundBinding> Resolve(
        string directoryPath,
        IReadOnlyList<string> fileNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentNullException.ThrowIfNull(fileNames);

        var sortedFileNames = NormalizeAndSort(fileNames);
        var (rawMatches, bareMatches) = BuildRawMatches(sortedFileNames);

        var bindings = new SoundBinding[SoundCatalog.AllSounds.Count];
        for (var index = 0; index < SoundCatalog.AllSounds.Count; index++)
        {
            var sound = SoundCatalog.AllSounds[index];
            bindings[index] = BuildBinding(sound, rawMatches, bareMatches[sound]);
        }

        return bindings;
    }

    /// <summary>
    /// One fully fallback-substituted variant list per (slot, class) pair —
    /// once per <see cref="HitClassCatalog.All"/> entry for a hit-location
    /// driven slot, once with a <c>null</c> class for every other slot. This
    /// is the list the sound director and the audio loader actually play
    /// from; <see cref="Resolve"/> reports the raw, pre-fallback picture
    /// instead, because that is what tells an owner which file is missing.
    /// </summary>
    public static IReadOnlyList<SoundVariantList> ResolveVariants(
        string directoryPath,
        IReadOnlyList<string> fileNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentNullException.ThrowIfNull(fileNames);

        var sortedFileNames = NormalizeAndSort(fileNames);
        var (rawMatches, bareMatches) = BuildRawMatches(sortedFileNames);

        var results = new List<SoundVariantList>();
        foreach (var sound in SoundCatalog.AllSounds)
        {
            var bareMatch = bareMatches[sound];
            if (SoundCatalog.IsHitLocationDriven(sound))
            {
                foreach (var hitClass in HitClassCatalog.All)
                {
                    results.Add(
                        ResolveClassVariant(sound, hitClass, rawMatches, bareMatch));
                }

                continue;
            }

            var numbered = rawMatches[(sound, null)];
            List<string> fileNamesForSlot;
            if (numbered.Count > 0)
            {
                fileNamesForSlot = numbered.ConvertAll(match => match.FileName);
            }
            else if (bareMatch is { } bare)
            {
                fileNamesForSlot = [bare];
            }
            else
            {
                fileNamesForSlot = [];
            }

            results.Add(
                new SoundVariantList(
                    sound,
                    HitClass: null,
                    fileNamesForSlot,
                    fileNamesForSlot.Count > 0
                        ? SoundBindingStatus.Ready
                        : SoundBindingStatus.Missing));
        }

        return results;
    }

    private static SoundBinding BuildBinding(
        GameSoundId sound,
        Dictionary<(GameSoundId Sound, HitClass? HitClass), List<(int Index, string FileName)>> rawMatches,
        string? bareMatch)
    {
        var fileName = SoundCatalog.GetFileName(sound);

        if (!SoundCatalog.IsHitLocationDriven(sound))
        {
            var numberedCount = rawMatches[(sound, null)].Count;
            var variantCount = numberedCount > 0
                ? numberedCount
                : bareMatch is not null ? 1 : 0;
            return new SoundBinding(
                sound,
                fileName,
                ClassCounts: [],
                variantCount,
                variantCount > 0
                    ? SoundBindingStatus.Ready
                    : SoundBindingStatus.Missing);
        }

        var classCounts = new SoundClassCount[HitClassCatalog.All.Count];
        var totalCount = 0;
        for (var index = 0; index < HitClassCatalog.All.Count; index++)
        {
            var hitClass = HitClassCatalog.All[index];
            var count = rawMatches[(sound, hitClass)].Count;
            totalCount += count;
            classCounts[index] = new SoundClassCount(
                hitClass,
                count,
                count > 0 ? SoundBindingStatus.Ready : SoundBindingStatus.Missing);
        }

        var hasBare = bareMatch is not null;
        var slotVariantCount = totalCount + (hasBare ? 1 : 0);
        return new SoundBinding(
            sound,
            fileName,
            classCounts,
            slotVariantCount,
            slotVariantCount > 0
                ? SoundBindingStatus.Ready
                : SoundBindingStatus.Missing);
    }

    private static SoundVariantList ResolveClassVariant(
        GameSoundId sound,
        HitClass hitClass,
        Dictionary<(GameSoundId Sound, HitClass? HitClass), List<(int Index, string FileName)>> rawMatches,
        string? bareMatch)
    {
        var direct = rawMatches[(sound, hitClass)];
        if (direct.Count > 0)
        {
            return new SoundVariantList(
                sound,
                hitClass,
                direct.ConvertAll(match => match.FileName),
                SoundBindingStatus.Ready);
        }

        foreach (var fallbackClass in HitClassCatalog.GetFallbackChain(hitClass))
        {
            var fallback = rawMatches[(sound, fallbackClass)];
            if (fallback.Count > 0)
            {
                return new SoundVariantList(
                    sound,
                    hitClass,
                    fallback.ConvertAll(match => match.FileName),
                    SoundBindingStatus.Ready);
            }
        }

        if (bareMatch is { } bare)
        {
            return new SoundVariantList(sound, hitClass, [bare], SoundBindingStatus.Ready);
        }

        return new SoundVariantList(sound, hitClass, [], SoundBindingStatus.Missing);
    }

    /// <summary>
    /// Builds two lookups shared by <see cref="Resolve"/> and
    /// <see cref="ResolveVariants"/>: the raw, un-substituted matches for
    /// every (slot, class-or-null) key, and the bare <c>&lt;slot&gt;.wav</c>
    /// single match for every slot.
    /// </summary>
    private static (
        Dictionary<(GameSoundId Sound, HitClass? HitClass), List<(int Index, string FileName)>> RawMatches,
        Dictionary<GameSoundId, string?> BareMatches) BuildRawMatches(string[] sortedFileNames)
    {
        var rawMatches =
            new Dictionary<(GameSoundId, HitClass?), List<(int Index, string FileName)>>();
        var bareMatches = new Dictionary<GameSoundId, string?>();

        foreach (var sound in SoundCatalog.AllSounds)
        {
            bareMatches[sound] = FindExactMatch(
                sortedFileNames,
                SoundCatalog.GetFileName(sound));

            if (SoundCatalog.IsHitLocationDriven(sound))
            {
                foreach (var hitClass in HitClassCatalog.All)
                {
                    rawMatches[(sound, hitClass)] = FindNumberedMatches(
                        sortedFileNames,
                        SoundCatalog.GetVariantPrefix(sound, hitClass));
                }
            }
            else
            {
                rawMatches[(sound, null)] = FindNumberedMatches(
                    sortedFileNames,
                    SoundCatalog.GetSlotVariantPrefix(sound));
            }
        }

        return (rawMatches, bareMatches);
    }

    private static string[] NormalizeAndSort(IReadOnlyList<string> fileNames)
    {
        var sortedFileNames = new string[fileNames.Count];
        for (var index = 0; index < fileNames.Count; index++)
        {
            sortedFileNames[index] = fileNames[index] ?? string.Empty;
        }

        Array.Sort(sortedFileNames, StringComparer.Ordinal);
        return sortedFileNames;
    }

    private static string? FindExactMatch(
        string[] sortedFileNames,
        string expectedFileName)
    {
        foreach (var fileName in sortedFileNames)
        {
            if (string.Equals(
                    fileName,
                    expectedFileName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return fileName;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds every file matching <c>&lt;prefix&gt;NN.wav</c> (case-insensitive),
    /// where <c>NN</c> is exactly <see cref="SoundCatalog.VariantIndexDigits"/>
    /// digits and one-based. Returned ascending by index; ties (two files
    /// differing only by case at the same index) keep the ordinal order of
    /// <paramref name="sortedFileNames"/>, because <c>OrderBy</c> is a stable
    /// sort.
    /// </summary>
    private static List<(int Index, string FileName)> FindNumberedMatches(
        string[] sortedFileNames,
        string prefix)
    {
        var matches = new List<(int Index, string FileName)>();
        foreach (var fileName in sortedFileNames)
        {
            if (TryParseVariantIndex(fileName, prefix, out var index))
            {
                matches.Add((index, fileName));
            }
        }

        return matches.OrderBy(match => match.Index).ToList();
    }

    private static bool TryParseVariantIndex(
        string fileName,
        string prefix,
        out int index)
    {
        index = 0;

        if (!fileName.EndsWith(
                SoundCatalog.SupportedExtension,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var withoutExtension = fileName[..^SoundCatalog.SupportedExtension.Length];
        if (!withoutExtension.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var suffix = withoutExtension[prefix.Length..];
        return suffix.Length == SoundCatalog.VariantIndexDigits &&
            int.TryParse(
                suffix,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out index) &&
            index > 0;
    }
}
