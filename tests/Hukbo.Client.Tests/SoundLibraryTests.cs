using Hukbo.Client.Audio;

namespace Hukbo.Client.Tests;

public sealed class SoundLibraryTests
{
    private const string AudioDirectory = "/game/Content/Audio";

    [Fact]
    public void Resolve_ReturnsOneBindingPerSlotInCatalogOrder()
    {
        var bindings = SoundLibrary.Resolve(AudioDirectory, []);

        Assert.Equal(SoundCatalog.AllSounds.Count, bindings.Count);
        for (var index = 0; index < bindings.Count; index++)
        {
            Assert.Equal(SoundCatalog.AllSounds[index], bindings[index].Sound);
            Assert.Equal(
                SoundCatalog.GetFileName(SoundCatalog.AllSounds[index]),
                bindings[index].FileName);
        }
    }

    [Fact]
    public void Resolve_MarksEverySlotMissingForAnEmptyFolder()
    {
        var bindings = SoundLibrary.Resolve(AudioDirectory, []);

        Assert.All(
            bindings,
            binding =>
            {
                Assert.Equal(SoundBindingStatus.Missing, binding.Status);
                Assert.Equal(0, binding.VariantCount);
            });
    }

    [Fact]
    public void Resolve_BindsAnExactMatchAndLeavesOtherSlotsMissing()
    {
        var bindings = SoundLibrary.Resolve(AudioDirectory, ["death.wav"]);

        var death = Single(bindings, GameSoundId.Death);
        Assert.Equal(SoundBindingStatus.Ready, death.Status);
        Assert.Equal(1, death.VariantCount);
        Assert.Equal(
            SoundCatalog.AllSounds.Count - 1,
            SoundCatalog.CountUnavailable(bindings));
    }

    [Fact]
    public void Resolve_MatchesFileNameCaseInsensitively()
    {
        var bindings = SoundLibrary.Resolve(AudioDirectory, ["Death.WAV"]);

        var death = Single(bindings, GameSoundId.Death);
        Assert.Equal(SoundBindingStatus.Ready, death.Status);
        Assert.Equal(1, death.VariantCount);
    }

    [Fact]
    public void
        ResolveVariants_ClasslessBareMatchPicksTheOrdinallyFirstCandidateWhenCaseCollides()
    {
        var first = SoundLibrary.ResolveVariants(
            AudioDirectory,
            ["death.wav", "DEATH.WAV"]);
        var reversed = SoundLibrary.ResolveVariants(
            AudioDirectory,
            ["DEATH.WAV", "death.wav"]);

        Assert.Equal(
            Single(first, GameSoundId.Death, hitClass: null).FileNames,
            Single(reversed, GameSoundId.Death, hitClass: null).FileNames);
        Assert.Equal(
            ["DEATH.WAV"],
            Single(first, GameSoundId.Death, hitClass: null).FileNames);
    }

    [Fact]
    public void Resolve_CountsNumberedVariantsForAClasslessSlot()
    {
        var bindings = SoundLibrary.Resolve(
            AudioDirectory,
            ["death-01.wav", "death-02.wav", "death-03.wav"]);

        var death = Single(bindings, GameSoundId.Death);
        Assert.Equal(SoundBindingStatus.Ready, death.Status);
        Assert.Equal(3, death.VariantCount);
    }

    [Fact]
    public void Resolve_IgnoresUnsupportedExtensionsAndUnknownNames()
    {
        var bindings = SoundLibrary.Resolve(
            AudioDirectory,
            ["death.ogg", "death.mp3", "death", "footsteps.wav", "README.md"]);

        Assert.All(
            bindings,
            binding =>
                Assert.Equal(SoundBindingStatus.Missing, binding.Status));
    }

    [Fact]
    public void Resolve_RejectsInvalidArguments()
    {
        Assert.Throws<ArgumentNullException>(
            () => SoundLibrary.Resolve(AudioDirectory, null!));
        Assert.Throws<ArgumentException>(
            () => SoundLibrary.Resolve(" ", []));
    }

    [Fact]
    public void Resolve_ReportsRawPerClassCountsForAHitLocationDrivenSlot()
    {
        var bindings = SoundLibrary.Resolve(
            AudioDirectory,
            [
                "attack-great-blade-skull-01.wav",
                "attack-great-blade-skull-02.wav",
                "attack-great-blade-neck-01.wav",
            ]);

        var greatBlade = Single(bindings, GameSoundId.AttackGreatBlade);
        Assert.Equal(6, greatBlade.ClassCounts.Count);
        Assert.Equal(3, greatBlade.VariantCount);
        Assert.Equal(SoundBindingStatus.Ready, greatBlade.Status);

        var skull = ClassCount(greatBlade, HitClass.Skull);
        Assert.Equal(2, skull.Count);
        Assert.Equal(SoundBindingStatus.Ready, skull.Status);

        var limb = ClassCount(greatBlade, HitClass.Limb);
        Assert.Equal(0, limb.Count);
        Assert.Equal(SoundBindingStatus.Missing, limb.Status);
    }

    [Fact]
    public void Resolve_IsReadyWhenAnyFileExistsAnywhereForTheSlot()
    {
        // Only one class populated, everything else empty: the slot as a
        // whole is still reported ready, because the fallback chain in
        // ResolveVariants guarantees every class can play something.
        var bindings = SoundLibrary.Resolve(
            AudioDirectory,
            ["attack-work-blade-ribcage-01.wav"]);

        var workBlade = Single(bindings, GameSoundId.AttackWorkBlade);
        Assert.Equal(SoundBindingStatus.Ready, workBlade.Status);
        Assert.Equal(1, workBlade.VariantCount);
    }

    [Fact]
    public void ResolveVariants_ReturnsOneEntryPerClassForAHitLocationDrivenSlot()
    {
        var variants = SoundLibrary.ResolveVariants(AudioDirectory, []);

        var greatBladeEntries = variants
            .Where(v => v.Sound == GameSoundId.AttackGreatBlade)
            .ToList();
        Assert.Equal(HitClassCatalog.All.Count, greatBladeEntries.Count);
        foreach (var hitClass in HitClassCatalog.All)
        {
            Assert.Contains(greatBladeEntries, v => v.HitClass == hitClass);
        }
    }

    [Fact]
    public void ResolveVariants_ReturnsOneNullClassEntryForAClasslessSlot()
    {
        var variants = SoundLibrary.ResolveVariants(AudioDirectory, []);

        var deathEntries = variants.Where(v => v.Sound == GameSoundId.Death).ToList();
        var death = Assert.Single(deathEntries);
        Assert.Null(death.HitClass);
    }

    [Fact]
    public void ResolveVariants_OrdersFilesAscendingByIndex()
    {
        var variants = SoundLibrary.ResolveVariants(
            AudioDirectory,
            [
                "attack-great-blade-skull-03.wav",
                "attack-great-blade-skull-01.wav",
                "attack-great-blade-skull-02.wav",
            ]);

        var skull = Single(variants, GameSoundId.AttackGreatBlade, HitClass.Skull);
        Assert.Equal(
            [
                "attack-great-blade-skull-01.wav",
                "attack-great-blade-skull-02.wav",
                "attack-great-blade-skull-03.wav",
            ],
            skull.FileNames);
    }

    [Fact]
    public void ResolveVariants_IgnoresAFileWhoseIndexIsNotExactlyTwoDigits()
    {
        var variants = SoundLibrary.ResolveVariants(
            AudioDirectory,
            [
                "attack-great-blade-skull-1.wav",
                "attack-great-blade-skull-001.wav",
                "attack-great-blade-skull-01.wav",
            ]);

        var skull = Single(variants, GameSoundId.AttackGreatBlade, HitClass.Skull);
        Assert.Equal(["attack-great-blade-skull-01.wav"], skull.FileNames);
    }

    [Fact]
    public void ResolveVariants_ExtremityFallsBackToLimbThenRibcage()
    {
        var withLimb = SoundLibrary.ResolveVariants(
            AudioDirectory,
            ["attack-heavy-chopper-limb-01.wav"]);
        var withRibcageOnly = SoundLibrary.ResolveVariants(
            AudioDirectory,
            ["attack-heavy-chopper-ribcage-01.wav"]);

        var extremityViaLimb = Single(
            withLimb,
            GameSoundId.AttackHeavyChopper,
            HitClass.Extremity);
        Assert.Equal(
            SoundBindingStatus.Ready,
            extremityViaLimb.Status);
        Assert.Equal(
            ["attack-heavy-chopper-limb-01.wav"],
            extremityViaLimb.FileNames);

        var extremityViaRibcage = Single(
            withRibcageOnly,
            GameSoundId.AttackHeavyChopper,
            HitClass.Extremity);
        Assert.Equal(
            ["attack-heavy-chopper-ribcage-01.wav"],
            extremityViaRibcage.FileNames);
    }

    [Fact]
    public void ResolveVariants_SkullFallsBackToNeckThenRibcage()
    {
        var withNeck = SoundLibrary.ResolveVariants(
            AudioDirectory,
            ["attack-work-blade-neck-01.wav"]);
        var withRibcageOnly = SoundLibrary.ResolveVariants(
            AudioDirectory,
            ["attack-work-blade-ribcage-01.wav"]);

        Assert.Equal(
            ["attack-work-blade-neck-01.wav"],
            Single(withNeck, GameSoundId.AttackWorkBlade, HitClass.Skull).FileNames);
        Assert.Equal(
            ["attack-work-blade-ribcage-01.wav"],
            Single(withRibcageOnly, GameSoundId.AttackWorkBlade, HitClass.Skull).FileNames);
    }

    // The hit-class parameter is an int because xunit requires public test
    // methods and HitClass is internal to Hukbo.Client.
    [Theory]
    [InlineData((int)HitClass.Neck)]
    [InlineData((int)HitClass.Gut)]
    [InlineData((int)HitClass.Limb)]
    public void ResolveVariants_FallsBackDirectlyToRibcage(int hitClass)
    {
        var variants = SoundLibrary.ResolveVariants(
            AudioDirectory,
            ["attack-thrusting-blade-ribcage-01.wav"]);

        var resolved = Single(
            variants,
            GameSoundId.AttackThrustingBlade,
            (HitClass)hitClass);
        Assert.Equal(SoundBindingStatus.Ready, resolved.Status);
        Assert.Equal(
            ["attack-thrusting-blade-ribcage-01.wav"],
            resolved.FileNames);
    }

    [Fact]
    public void ResolveVariants_RibcageFallsBackToTheBareSingle()
    {
        var variants = SoundLibrary.ResolveVariants(
            AudioDirectory,
            ["attack-great-blade.wav"]);

        var ribcage = Single(variants, GameSoundId.AttackGreatBlade, HitClass.Ribcage);
        Assert.Equal(SoundBindingStatus.Ready, ribcage.Status);
        Assert.Equal(["attack-great-blade.wav"], ribcage.FileNames);
    }

    [Fact]
    public void ResolveVariants_StaysSilentWhenNoFileExistsAnywhereForTheClass()
    {
        var variants = SoundLibrary.ResolveVariants(AudioDirectory, []);

        var ribcage = Single(variants, GameSoundId.AttackGreatBlade, HitClass.Ribcage);
        Assert.Equal(SoundBindingStatus.Missing, ribcage.Status);
        Assert.Empty(ribcage.FileNames);
    }

    [Fact]
    public void ResolveVariants_HandlesAPartiallyPopulatedSet()
    {
        // Three of ten takes present for one class: the class still resolves
        // to exactly those three, not padded or duplicated.
        var variants = SoundLibrary.ResolveVariants(
            AudioDirectory,
            [
                "attack-great-blade-gut-01.wav",
                "attack-great-blade-gut-02.wav",
                "attack-great-blade-gut-03.wav",
            ]);

        var gut = Single(variants, GameSoundId.AttackGreatBlade, HitClass.Gut);
        Assert.Equal(3, gut.FileNames.Count);
        Assert.Equal(SoundBindingStatus.Ready, gut.Status);
    }

    [Fact]
    public void ResolveVariants_ClasslessSlotPrefersNumberedFilesOverTheBareSingle()
    {
        var variants = SoundLibrary.ResolveVariants(
            AudioDirectory,
            ["death.wav", "death-01.wav", "death-02.wav"]);

        var death = Single(variants, GameSoundId.Death, hitClass: null);
        Assert.Equal(["death-01.wav", "death-02.wav"], death.FileNames);
    }

    [Fact]
    public void ResolveVariants_ClasslessSlotFallsBackToTheBareSingle()
    {
        var variants = SoundLibrary.ResolveVariants(AudioDirectory, ["death.wav"]);

        var death = Single(variants, GameSoundId.Death, hitClass: null);
        Assert.Equal(["death.wav"], death.FileNames);
    }

    [Fact]
    public void ResolveVariants_RejectsInvalidArguments()
    {
        Assert.Throws<ArgumentNullException>(
            () => SoundLibrary.ResolveVariants(AudioDirectory, null!));
        Assert.Throws<ArgumentException>(
            () => SoundLibrary.ResolveVariants(" ", []));
    }

    [Fact]
    public void ListFileNames_ReturnsEmptyForAFolderThatDoesNotExist()
    {
        var absentPath = Path.Combine(
            Path.GetTempPath(),
            $"hukbo-audio-absent-{Guid.NewGuid():N}");

        Assert.Empty(SoundLibrary.ListFileNames(absentPath));
    }

    [Fact]
    public void ListFileNames_ReturnsBareFileNamesForAFolderThatExists()
    {
        var directoryPath = Path.Combine(
            Path.GetTempPath(),
            $"hukbo-audio-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directoryPath);
        try
        {
            File.WriteAllText(Path.Combine(directoryPath, "death.wav"), "x");

            var fileNames = SoundLibrary.ListFileNames(directoryPath);

            Assert.Equal("death.wav", Assert.Single(fileNames));
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public void GetDefaultDirectoryPath_PointsAtTheContentAudioFolder()
    {
        var path = SoundLibrary.GetDefaultDirectoryPath();

        Assert.Equal(SoundCatalog.FolderName, Path.GetFileName(path));
        Assert.Equal(
            "Content",
            Path.GetFileName(Path.GetDirectoryName(path)));
    }

    private static SoundBinding Single(
        IReadOnlyList<SoundBinding> bindings,
        GameSoundId sound)
    {
        foreach (var binding in bindings)
        {
            if (binding.Sound == sound)
            {
                return binding;
            }
        }

        throw new InvalidOperationException(
            $"No binding was resolved for {sound}.");
    }

    private static SoundClassCount ClassCount(SoundBinding binding, HitClass hitClass)
    {
        foreach (var classCount in binding.ClassCounts)
        {
            if (classCount.HitClass == hitClass)
            {
                return classCount;
            }
        }

        throw new InvalidOperationException(
            $"No class count was resolved for {hitClass}.");
    }

    private static SoundVariantList Single(
        IReadOnlyList<SoundVariantList> variants,
        GameSoundId sound,
        HitClass? hitClass)
    {
        foreach (var variant in variants)
        {
            if (variant.Sound == sound && variant.HitClass == hitClass)
            {
                return variant;
            }
        }

        throw new InvalidOperationException(
            $"No variant list was resolved for {sound}/{hitClass}.");
    }
}
