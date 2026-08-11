using Sandata.Client.Audio;
using Sandata.Core.Weapons;

namespace Sandata.Client.Tests;

/// <summary>
/// Covers the sd-audio task's test bar for <see cref="SandataSoundLibrary"/>:
/// a filename round-trips through <see cref="SandataSoundCatalog"/> for a
/// rifle slot and a pistol slot, and a path is built under the expected
/// directory with no filesystem access.
/// </summary>
public sealed class SandataSoundLibraryTests
{
    [Fact]
    public void DefaultDirectoryPathEndsInContentAudioUnderBaseDirectory()
    {
        var path = SandataSoundLibrary.GetDefaultDirectoryPath();

        Assert.Equal(
            Path.Combine(AppContext.BaseDirectory, "Content", "Audio"),
            path);
    }

    [Fact]
    public void FilePathRoundTripsThroughTheCatalogForARifleSlot()
    {
        // Same tuple SandataSoundCatalogTests pins to
        // "gun-556x45-single-indoor-03.wav".
        var slot = SandataSoundCatalog.Find(
            SoundFamily.GunReport,
            (int)CaliberFamily.Cal556X45,
            FireMode.Single,
            SoundEnvironment.IndoorTail);

        var directoryPath = Path.Combine("C:", "game", "Content", "Audio");
        var filePath = SandataSoundLibrary.GetFilePath(directoryPath, slot, variantNumber: 3);

        Assert.Equal(
            Path.Combine(directoryPath, "gun-556x45-single-indoor-03.wav"),
            filePath);
    }

    [Fact]
    public void FilePathRoundTripsThroughTheCatalogForAPistolSlot()
    {
        // Same tuple SandataSoundCatalogTests pins to
        // "dry-9x19-none-none-02.wav". Cal9X19 is the pistol caliber the
        // dry-fire row family keys on.
        var slot = SandataSoundCatalog.Find(
            SoundFamily.Dry,
            (int)CaliberFamily.Cal9X19,
            FireMode.None,
            SoundEnvironment.None);

        var directoryPath = Path.Combine("C:", "game", "Content", "Audio");
        var filePath = SandataSoundLibrary.GetFilePath(directoryPath, slot, variantNumber: 2);

        Assert.Equal(
            Path.Combine(directoryPath, "dry-9x19-none-none-02.wav"),
            filePath);
    }

    [Fact]
    public void GetFilePathBuildsUnderTheGivenDirectoryWithNoDiskAccess()
    {
        // A directory that provably does not exist. GetFilePath must still
        // return a path string, never touching the filesystem.
        var directoryPath = Path.Combine(
            Path.GetTempPath(),
            "sandata-sound-library-tests-" + Guid.NewGuid().ToString("N"));

        var slot = SandataSoundCatalog.Find(
            SoundFamily.Impact, (int)ImpactSurface.Concrete, FireMode.None, SoundEnvironment.None);

        var filePath = SandataSoundLibrary.GetFilePath(directoryPath, slot, variantNumber: 1);

        Assert.Equal(
            Path.Combine(directoryPath, "impact-concrete-none-none-01.wav"),
            filePath);
        Assert.False(Directory.Exists(directoryPath));
    }

    [Fact]
    public void FileExistsReturnsFalseForAnAbsentFileRatherThanThrowing()
    {
        var directoryPath = Path.Combine(
            Path.GetTempPath(),
            "sandata-sound-library-tests-" + Guid.NewGuid().ToString("N"));
        var filePath = Path.Combine(directoryPath, "gun-556x45-single-indoor-03.wav");

        var exists = SandataSoundLibrary.FileExists(filePath);

        Assert.False(exists);
    }

    [Fact]
    public void FileExistsReturnsTrueForAFileThatIsActuallyThere()
    {
        var directoryPath = Path.Combine(
            Path.GetTempPath(),
            "sandata-sound-library-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);
        var filePath = Path.Combine(directoryPath, "gun-556x45-single-indoor-03.wav");
        File.WriteAllBytes(filePath, [0x52, 0x49, 0x46, 0x46]);

        try
        {
            Assert.True(SandataSoundLibrary.FileExists(filePath));
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }
}

/// <summary>
/// Covers <see cref="BoundedCache{TKey,TValue}"/> directly, with plain
/// reference values rather than a <c>SoundEffect</c> — this generic cache is
/// exactly what makes <see cref="MonoGameSandataSoundOutput"/>'s eviction and
/// hit behavior testable without a real audio device.
/// </summary>
public sealed class BoundedCacheTests
{
    [Fact]
    public void TryGetReturnsTheSameInstanceOnEveryHit()
    {
        var cache = new BoundedCache<string, object>(capacity: 4);
        var value = new object();

        cache.Set("a", value);

        Assert.True(cache.TryGet("a", out var first));
        Assert.True(cache.TryGet("a", out var second));
        Assert.Same(value, first);
        Assert.Same(first, second);
    }

    [Fact]
    public void TryGetReturnsFalseForAKeyNeverSet()
    {
        var cache = new BoundedCache<string, object>(capacity: 4);

        Assert.False(cache.TryGet("missing", out _));
    }

    [Fact]
    public void SetBeyondCapacityEvictsTheLeastRecentlyUsedEntry()
    {
        var cache = new BoundedCache<string, int>(capacity: 2);

        cache.Set("a", 1);
        cache.Set("b", 2);
        cache.Set("c", 3); // "a" was least recently used; must be evicted.

        Assert.False(cache.TryGet("a", out _));
        Assert.True(cache.TryGet("b", out var b));
        Assert.True(cache.TryGet("c", out var c));
        Assert.Equal(2, b);
        Assert.Equal(3, c);
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void TouchingAnEntryProtectsItFromEviction()
    {
        var cache = new BoundedCache<string, int>(capacity: 2);

        cache.Set("a", 1);
        cache.Set("b", 2);
        cache.TryGet("a", out _); // "a" is now most recently used; "b" is now oldest.
        cache.Set("c", 3);

        Assert.True(cache.TryGet("a", out _));
        Assert.False(cache.TryGet("b", out _));
        Assert.True(cache.TryGet("c", out _));
    }

    [Fact]
    public void SetBeyondCapacityInvokesOnEvictedWithTheDroppedValue()
    {
        var evicted = new List<int>();
        var cache = new BoundedCache<string, int>(capacity: 1, onEvicted: value => evicted.Add(value));

        cache.Set("a", 1);
        cache.Set("b", 2);

        Assert.Equal([1], evicted);
    }

    [Fact]
    public void ClearDropsEveryEntryWithoutInvokingOnEvicted()
    {
        var evicted = new List<int>();
        var cache = new BoundedCache<string, int>(capacity: 4, onEvicted: value => evicted.Add(value));

        cache.Set("a", 1);
        cache.Set("b", 2);
        cache.Clear();

        Assert.Equal(0, cache.Count);
        Assert.Empty(evicted);
        Assert.False(cache.TryGet("a", out _));
    }
}

/// <summary>
/// Covers <see cref="MonoGameSandataSoundOutput"/>'s no-crash contract for an
/// absent file. Everything here resolves before a real
/// <c>SoundEffect.FromStream</c> call would happen, so none of it needs a
/// <c>GraphicsDevice</c> or audio hardware.
/// </summary>
public sealed class MonoGameSandataSoundOutputTests
{
    private static SoundSlot RifleSingleIndoorSlot() =>
        SandataSoundCatalog.Find(
            SoundFamily.GunReport,
            (int)CaliberFamily.Cal556X45,
            FireMode.Single,
            SoundEnvironment.IndoorTail);

    [Fact]
    public void GetStatusIsUnknownBeforeAnythingIsRequested()
    {
        var directoryPath = Path.Combine(
            Path.GetTempPath(),
            "sandata-sound-output-tests-" + Guid.NewGuid().ToString("N"));
        using var output = new MonoGameSandataSoundOutput(directoryPath);

        var status = output.GetStatus(RifleSingleIndoorSlot(), variantNumber: 3);

        Assert.Equal(SandataSoundCueStatus.Unknown, status);
    }

    [Fact]
    public void PlayOnAnAbsentFileReturnsFalseRatherThanThrowing()
    {
        var directoryPath = Path.Combine(
            Path.GetTempPath(),
            "sandata-sound-output-tests-" + Guid.NewGuid().ToString("N"));
        using var output = new MonoGameSandataSoundOutput(directoryPath);

        var played = output.Play(RifleSingleIndoorSlot(), variantNumber: 3, shooterEntityId: 7);

        Assert.False(played);
    }

    [Fact]
    public void PlayOnAnAbsentFileRecordsTheMissingStatus()
    {
        var directoryPath = Path.Combine(
            Path.GetTempPath(),
            "sandata-sound-output-tests-" + Guid.NewGuid().ToString("N"));
        using var output = new MonoGameSandataSoundOutput(directoryPath);
        var slot = RifleSingleIndoorSlot();

        output.Play(slot, variantNumber: 3, shooterEntityId: 7);

        Assert.Equal(SandataSoundCueStatus.Missing, output.GetStatus(slot, variantNumber: 3));
    }

    [Fact]
    public void PlayAfterDisposeReturnsFalse()
    {
        var directoryPath = Path.Combine(
            Path.GetTempPath(),
            "sandata-sound-output-tests-" + Guid.NewGuid().ToString("N"));
        var output = new MonoGameSandataSoundOutput(directoryPath);
        output.Dispose();

        var played = output.Play(RifleSingleIndoorSlot(), variantNumber: 3, shooterEntityId: 7);

        Assert.False(played);
    }

    [Fact]
    public void DisposeIsIdempotent()
    {
        var directoryPath = Path.Combine(
            Path.GetTempPath(),
            "sandata-sound-output-tests-" + Guid.NewGuid().ToString("N"));
        var output = new MonoGameSandataSoundOutput(directoryPath);

        output.Dispose();
        var exception = Record.Exception(output.Dispose);

        Assert.Null(exception);
    }
}
