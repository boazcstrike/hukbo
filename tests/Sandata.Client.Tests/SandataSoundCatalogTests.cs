using Hukbo.Diagnostics;
using Sandata.Client.Audio;
using Sandata.Core.Weapons;

namespace Sandata.Client.Tests;

/// <summary>
/// Covers plan task 24's test bar: every declared tuple resolves to exactly
/// one row, no tuple resolves to two, every <see cref="SoundSlot.VariantCount"/>
/// is between 1 and 99, the base names for the nine worked examples in design
/// section 10 are produced exactly, the bullpup selector slot is distinct
/// from the AR and AK selector slots, and the lookup uses no dictionary.
/// </summary>
public sealed class SandataSoundCatalogTests
{
    // xunit test methods must be public, and a public method cannot expose an
    // internal enum in its signature (CS0051), so every enum axis crosses the
    // [Theory]/[MemberData] boundary as int and is cast back to its internal
    // enum type inside the test body. Mirrors the (int)GameSoundId convention
    // Hukbo.Client.Tests.SoundCatalogTests already uses for the same reason.
    public static IEnumerable<object[]> WorkedExamples()
    {
        yield return
        [
            (int)SoundFamily.GunReport,
            (int)CaliberFamily.Cal556X45,
            (int)FireMode.Single,
            (int)SoundEnvironment.IndoorTail,
            3,
            "gun-556x45-single-indoor-03.wav",
        ];
        yield return
        [
            (int)SoundFamily.GunReport,
            (int)CaliberFamily.Cal545X39,
            (int)FireMode.Burst2,
            (int)SoundEnvironment.CloseDry,
            1,
            "gun-545x39-burst2-close-01.wav",
        ];
        yield return
        [
            (int)SoundFamily.GunLoop,
            (int)CaliberFamily.Cal762X39,
            (int)FireMode.Auto,
            (int)SoundEnvironment.OutdoorTail,
            2,
            "gunloop-762x39-auto-outdoor-02.wav",
        ];
        yield return
        [
            (int)SoundFamily.GunTail,
            (int)CaliberFamily.Cal762X39,
            (int)FireMode.Auto,
            (int)SoundEnvironment.OutdoorTail,
            2,
            "guntail-762x39-auto-outdoor-02.wav",
        ];
        yield return
        [
            (int)SoundFamily.Mechanism,
            (int)MechanismGroup.Ar,
            (int)FireMode.Selector,
            (int)SoundEnvironment.None,
            1,
            "mech-ar-selector-none-01.wav",
        ];
        yield return
        [
            (int)SoundFamily.Mechanism,
            (int)MechanismGroup.Bullpup,
            (int)FireMode.Selector,
            (int)SoundEnvironment.None,
            1,
            "mech-bullpup-selector-none-01.wav",
        ];
        yield return
        [
            (int)SoundFamily.Dry,
            (int)CaliberFamily.Cal9X19,
            (int)FireMode.None,
            (int)SoundEnvironment.None,
            2,
            "dry-9x19-none-none-02.wav",
        ];
        yield return
        [
            (int)SoundFamily.Impact,
            (int)ImpactSurface.Concrete,
            (int)FireMode.None,
            (int)SoundEnvironment.None,
            4,
            "impact-concrete-none-none-04.wav",
        ];
        yield return
        [
            (int)SoundFamily.Casing,
            (int)WeaponClass.Rifle,
            (int)FireMode.GroundConcrete,
            (int)SoundEnvironment.None,
            3,
            "casing-rifle-concrete-none-03.wav",
        ];
    }

    /// <summary>
    /// Design section 10's nine worked examples, reproduced exactly: the
    /// declared row for the tuple, formatted with the given variant index,
    /// must equal the design document's literal file name.
    /// </summary>
    [Theory]
    [MemberData(nameof(WorkedExamples))]
    public void WorkedExampleBaseNamesAreProducedExactly(
        int family,
        int familyKey,
        int mode,
        int environment,
        int variantIndex,
        string expectedFileName)
    {
        var slot = SandataSoundCatalog.Find(
            (SoundFamily)family,
            familyKey,
            (FireMode)mode,
            (SoundEnvironment)environment);

        var fileName = SandataSoundCatalog.GetVariantFileName(slot, variantIndex);

        Assert.Equal(expectedFileName, fileName);
    }

    /// <summary>
    /// Every row the catalog declares must be reachable through
    /// <see cref="SandataSoundCatalog.TryFind"/> by its own tuple, and the row
    /// returned must be the same row that was declared.
    /// </summary>
    [Fact]
    public void EveryDeclaredTupleResolvesToExactlyOneRow()
    {
        foreach (var row in SandataSoundCatalog.Rows)
        {
            var found = SandataSoundCatalog.TryFind(
                row.Family,
                row.FamilyKey,
                row.Mode,
                row.Environment,
                out var resolved);

            Assert.True(
                found,
                $"No row resolved for {row.Family}/{row.FamilyKey}/" +
                $"{row.Mode}/{row.Environment}.");
            Assert.Equal(row, resolved);
        }
    }

    /// <summary>
    /// No two declared rows may share a (Family, FamilyKey, Mode,
    /// Environment) tuple. <see cref="SandataSoundCatalog"/>'s static
    /// initializer already throws on a duplicate, so simply loading the type
    /// without an exception is itself part of this fact; this test also
    /// checks it independently by building the tuple set directly from
    /// <see cref="SandataSoundCatalog.Rows"/>.
    /// </summary>
    [Fact]
    public void NoTupleResolvesToTwoRows()
    {
        var seen = new HashSet<(SoundFamily Family, int FamilyKey, FireMode Mode, SoundEnvironment Environment)>();

        foreach (var row in SandataSoundCatalog.Rows)
        {
            var tuple = (row.Family, row.FamilyKey, row.Mode, row.Environment);
            Assert.True(
                seen.Add(tuple),
                $"Tuple {row.Family}/{row.FamilyKey}/{row.Mode}/" +
                $"{row.Environment} is declared by more than one row.");
        }
    }

    [Fact]
    public void EveryVariantCountIsWithinTheFilenameCap()
    {
        Assert.All(
            SandataSoundCatalog.Rows,
            row => Assert.InRange(
                row.VariantCount,
                (byte)SandataSoundCatalog.MinVariantCount,
                (byte)SandataSoundCatalog.MaxVariantCount));
    }

    /// <summary>
    /// The Steyr AUG has a cross-bolt push-button safety rather than a
    /// rotary selector detent, so its mechanism sample must not be the AR's
    /// or the AK's. <c>MechanismGroup.Bullpup</c> being its own ordinal
    /// already guarantees a distinct tuple; this test also confirms the
    /// three resolved rows and their base names are pairwise distinct.
    /// </summary>
    [Fact]
    public void BullpupSelectorSlotDiffersFromArAndAkSelectorSlots()
    {
        var ar = SandataSoundCatalog.Find(
            SoundFamily.Mechanism, (int)MechanismGroup.Ar, FireMode.Selector, SoundEnvironment.None);
        var ak = SandataSoundCatalog.Find(
            SoundFamily.Mechanism, (int)MechanismGroup.Ak, FireMode.Selector, SoundEnvironment.None);
        var bullpup = SandataSoundCatalog.Find(
            SoundFamily.Mechanism, (int)MechanismGroup.Bullpup, FireMode.Selector, SoundEnvironment.None);

        Assert.NotEqual(ar, bullpup);
        Assert.NotEqual(ak, bullpup);
        Assert.NotEqual(
            SandataSoundCatalog.GetBaseName(ar),
            SandataSoundCatalog.GetBaseName(bullpup));
        Assert.NotEqual(
            SandataSoundCatalog.GetBaseName(ak),
            SandataSoundCatalog.GetBaseName(bullpup));
    }

    /// <summary>
    /// Design section 10 and the task's own test bar: the slot lookup is a
    /// flat array, never a <c>Dictionary</c>. Scoped to the Audio folder this
    /// task owns, and duplicated locally from
    /// <c>Sandata.Core.Tests.SandataSourceHygieneTests</c>'s scanning
    /// technique rather than sharing code across projects that share none.
    /// </summary>
    [Fact]
    public void SandataAudioCatalogSourceDeclaresNoDictionary()
    {
        var root = GetRepositoryRoot();
        var audioDirectory = Path.Combine(root, "src", "Sandata.Client", "Audio");

        var offenders = FindOffendingCodeLines(
            root,
            EnumerateSourceFiles(audioDirectory),
            line => line.Contains("Dictionary<", StringComparison.Ordinal));

        Assert.Empty(offenders);
    }

    private static string[] FindOffendingCodeLines(
        string root,
        IEnumerable<string> files,
        Func<string, bool> isBannedLine)
    {
        var offenders = new List<string>();

        foreach (var path in files)
        {
            var lines = File.ReadAllLines(path);
            for (var index = 0; index < lines.Length; index++)
            {
                var trimmed = lines[index].TrimStart();
                if (trimmed.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                if (isBannedLine(lines[index]))
                {
                    offenders.Add(
                        Path.GetRelativePath(root, path) + ":" + (index + 1));
                }
            }
        }

        return [.. offenders.Order(StringComparer.Ordinal)];
    }

    private static IEnumerable<string> EnumerateSourceFiles(string directory) =>
        !Directory.Exists(directory)
            ? []
            : Directory
                .EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                .Where(path =>
                    !path.Contains(
                        Path.DirectorySeparatorChar + "obj" +
                        Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase) &&
                    !path.Contains(
                        Path.DirectorySeparatorChar + "bin" +
                        Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase));

    private static string GetRepositoryRoot()
    {
        var root = LogPaths.FindRepositoryRoot(AppContext.BaseDirectory);
        Assert.True(
            root is not null,
            "No ancestor of " + AppContext.BaseDirectory + " contains " +
            LogPaths.RepositoryMarkerFileName +
            ", so the source tree cannot be scanned.");
        return root!;
    }
}
