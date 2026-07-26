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
                Assert.Null(binding.FilePath);
            });
    }

    [Fact]
    public void Resolve_BindsAnExactMatchAndLeavesOtherSlotsMissing()
    {
        var bindings = SoundLibrary.Resolve(AudioDirectory, ["death.wav"]);

        var death = Single(bindings, GameSoundId.Death);
        Assert.Equal(SoundBindingStatus.Ready, death.Status);
        Assert.Equal(
            Path.Combine(AudioDirectory, "death.wav"),
            death.FilePath);
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
        Assert.Equal(
            Path.Combine(AudioDirectory, "Death.WAV"),
            death.FilePath);
    }

    [Fact]
    public void Resolve_PicksTheOrdinallyFirstCandidateWhenCaseCollides()
    {
        var first = SoundLibrary.Resolve(
            AudioDirectory,
            ["death.wav", "DEATH.WAV"]);
        var reversed = SoundLibrary.Resolve(
            AudioDirectory,
            ["DEATH.WAV", "death.wav"]);

        Assert.Equal(
            Single(first, GameSoundId.Death).FilePath,
            Single(reversed, GameSoundId.Death).FilePath);
        Assert.Equal(
            Path.Combine(AudioDirectory, "DEATH.WAV"),
            Single(first, GameSoundId.Death).FilePath);
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
}
