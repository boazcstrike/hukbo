using Sandata.Client.Audio;
using Sandata.Core.Weapons;

namespace Sandata.Client.Tests;

/// <summary>
/// Covers plan task 39's test bar for <see cref="ShotSlotResolver"/>: the
/// slot chosen for every fire mode at every environment, and that the
/// resolver is reachable only from the client.
/// </summary>
public sealed class ShotSlotResolverTests
{
    // xunit test methods must be public, and a public method cannot expose an
    // internal enum in its signature (CS0051), so every enum axis crosses the
    // [Theory]/[MemberData] boundary as int and is cast back to its internal
    // enum type inside the test body, matching SandataSoundCatalogTests'
    // convention for the same reason.

    /// <summary>
    /// <see cref="FireMode.Single"/> is declared for every one of the eight
    /// caliber families across all five real environments
    /// (<c>AddSingleShotReports</c>), so this covers both a rifle caliber and
    /// a pistol caliber against every declared environment, driven by the
    /// resolver's own environment inputs rather than the environment enum
    /// directly.
    /// </summary>
    public static IEnumerable<object[]> SingleModeCases()
    {
        // (caliber, rangeWu, indoors, suppressed, expectedEnvironment)
        yield return [(int)CaliberFamily.Cal556X45, 0, false, true, (int)SoundEnvironment.Suppressed];
        yield return [(int)CaliberFamily.Cal556X45, 800, false, false, (int)SoundEnvironment.Distant];
        yield return [(int)CaliberFamily.Cal556X45, 799, true, false, (int)SoundEnvironment.IndoorTail];
        yield return [(int)CaliberFamily.Cal556X45, 200, false, false, (int)SoundEnvironment.CloseDry];
        yield return [(int)CaliberFamily.Cal556X45, 500, false, false, (int)SoundEnvironment.OutdoorTail];

        // A pistol caliber resolves the same way: Single is declared for all
        // eight caliber families, pistols included.
        yield return [(int)CaliberFamily.Cal9X19, 0, false, true, (int)SoundEnvironment.Suppressed];
        yield return [(int)CaliberFamily.Cal9X19, 800, false, false, (int)SoundEnvironment.Distant];
        yield return [(int)CaliberFamily.Cal9X19, 799, true, false, (int)SoundEnvironment.IndoorTail];
        yield return [(int)CaliberFamily.Cal9X19, 200, false, false, (int)SoundEnvironment.CloseDry];
        yield return [(int)CaliberFamily.Cal9X19, 500, false, false, (int)SoundEnvironment.OutdoorTail];
    }

    [Theory]
    [MemberData(nameof(SingleModeCases))]
    public void SingleModeResolvesTheDeclaredEnvironmentDirectly(
        int caliber, int rangeWu, bool indoors, bool suppressed, int expectedEnvironment)
    {
        var resolution = ShotSlotResolver.Resolve(
            (CaliberFamily)caliber, FireMode.Single, rangeWu, indoors, suppressed, tick: 1, shooterEntityId: 7);

        Assert.Equal(SoundFamily.GunReport, resolution.Slot.Family);
        Assert.Equal((SoundEnvironment)expectedEnvironment, resolution.Slot.Environment);
        Assert.Equal(
            SandataSoundCatalog.Find(
                SoundFamily.GunReport, caliber, FireMode.Single, (SoundEnvironment)expectedEnvironment),
            resolution.Slot);
    }

    /// <summary>
    /// <see cref="FireMode.Burst3"/> is declared only for
    /// <see cref="CaliberFamily.Cal556X45"/> and only across the three "near"
    /// environments. An input that computes to <see cref="SoundEnvironment.Distant"/>
    /// or <see cref="SoundEnvironment.Suppressed"/> is not declared for this
    /// mode, so the resolver must fall back to the indoor/outdoor tail
    /// environment rather than throw.
    /// </summary>
    public static IEnumerable<object[]> Burst3Cases()
    {
        // (rangeWu, indoors, suppressed, expectedEnvironment)
        yield return [100, false, false, (int)SoundEnvironment.CloseDry];
        yield return [100, true, false, (int)SoundEnvironment.IndoorTail];
        yield return [500, false, false, (int)SoundEnvironment.OutdoorTail];

        // Not declared for Burst3 at all: falls back by the indoor flag.
        yield return [0, true, true, (int)SoundEnvironment.IndoorTail]; // Suppressed -> fallback
        yield return [0, false, true, (int)SoundEnvironment.OutdoorTail]; // Suppressed -> fallback
        yield return [800, true, false, (int)SoundEnvironment.IndoorTail]; // Distant -> fallback
        yield return [800, false, false, (int)SoundEnvironment.OutdoorTail]; // Distant -> fallback
    }

    [Theory]
    [MemberData(nameof(Burst3Cases))]
    public void Burst3ModeFallsBackWhenTheComputedEnvironmentIsUndeclared(
        int rangeWu, bool indoors, bool suppressed, int expectedEnvironment)
    {
        var resolution = ShotSlotResolver.Resolve(
            CaliberFamily.Cal556X45, FireMode.Burst3, rangeWu, indoors, suppressed, tick: 2, shooterEntityId: 9);

        Assert.Equal(SoundFamily.GunReport, resolution.Slot.Family);
        Assert.Equal((SoundEnvironment)expectedEnvironment, resolution.Slot.Environment);
    }

    /// <summary>
    /// <see cref="FireMode.Burst2"/> is declared for
    /// <see cref="CaliberFamily.Cal545X39"/> and <see cref="CaliberFamily.Cal556X45"/>,
    /// again only across the three "near" environments.
    /// </summary>
    public static IEnumerable<object[]> Burst2Cases()
    {
        yield return [(int)CaliberFamily.Cal545X39, 100, false, false, (int)SoundEnvironment.CloseDry];
        yield return [(int)CaliberFamily.Cal545X39, 100, true, false, (int)SoundEnvironment.IndoorTail];
        yield return [(int)CaliberFamily.Cal545X39, 500, false, false, (int)SoundEnvironment.OutdoorTail];
        yield return [(int)CaliberFamily.Cal556X45, 100, false, false, (int)SoundEnvironment.CloseDry];
    }

    [Theory]
    [MemberData(nameof(Burst2Cases))]
    public void Burst2ModeResolvesTheDeclaredNearEnvironments(
        int caliber, int rangeWu, bool indoors, bool suppressed, int expectedEnvironment)
    {
        var resolution = ShotSlotResolver.Resolve(
            (CaliberFamily)caliber, FireMode.Burst2, rangeWu, indoors, suppressed, tick: 3, shooterEntityId: 11);

        Assert.Equal(SoundFamily.GunReport, resolution.Slot.Family);
        Assert.Equal((SoundEnvironment)expectedEnvironment, resolution.Slot.Environment);
    }

    /// <summary>
    /// <see cref="FireMode.Auto"/> resolves to <see cref="SoundFamily.GunLoop"/>
    /// and is declared only for the six rifle calibers, only across
    /// <see cref="SoundEnvironment.IndoorTail"/> and
    /// <see cref="SoundEnvironment.OutdoorTail"/>. A range or suppressor input
    /// that computes to any other environment must fall back the same way
    /// baked-burst rows do.
    /// </summary>
    public static IEnumerable<object[]> AutoModeCases()
    {
        // (rangeWu, indoors, suppressed, expectedEnvironment)
        yield return [100, true, false, (int)SoundEnvironment.IndoorTail];
        yield return [500, false, false, (int)SoundEnvironment.OutdoorTail];
        yield return [0, true, true, (int)SoundEnvironment.IndoorTail]; // Suppressed -> fallback
        yield return [800, false, false, (int)SoundEnvironment.OutdoorTail]; // Distant -> fallback
        yield return [100, false, false, (int)SoundEnvironment.OutdoorTail]; // CloseDry -> fallback
    }

    [Theory]
    [MemberData(nameof(AutoModeCases))]
    public void AutoModeResolvesToGunLoopWithTheDeclaredEnvironment(
        int rangeWu, bool indoors, bool suppressed, int expectedEnvironment)
    {
        var resolution = ShotSlotResolver.Resolve(
            CaliberFamily.Cal556X45, FireMode.Auto, rangeWu, indoors, suppressed, tick: 4, shooterEntityId: 13);

        Assert.Equal(SoundFamily.GunLoop, resolution.Slot.Family);
        Assert.Equal((SoundEnvironment)expectedEnvironment, resolution.Slot.Environment);
    }

    /// <summary>
    /// <see cref="ShotSlotResolver.ResolveGunTailSlot"/> must resolve the
    /// <see cref="SoundFamily.GunTail"/> row for the same caliber and
    /// environment an equivalent <see cref="FireMode.Auto"/> loop resolves
    /// to, since <c>SandataSoundCatalog</c> declares the two together.
    /// </summary>
    [Fact]
    public void ResolveGunTailSlotMatchesTheEquivalentLoopEnvironment()
    {
        var loop = ShotSlotResolver.Resolve(
            CaliberFamily.Cal762X39, FireMode.Auto, rangeWu: 100, shooterIsIndoors: true,
            suppressorFitted: false, tick: 5, shooterEntityId: 17);
        var tail = ShotSlotResolver.ResolveGunTailSlot(
            CaliberFamily.Cal762X39, rangeWu: 100, shooterIsIndoors: true, suppressorFitted: false);

        Assert.Equal(SoundFamily.GunTail, tail.Family);
        Assert.Equal(loop.Slot.FamilyKey, tail.FamilyKey);
        Assert.Equal(loop.Slot.Environment, tail.Environment);
    }

    /// <summary>
    /// No task before this one declares an <see cref="FireMode.Auto"/> row
    /// for a pistol caliber — <c>AddAutomaticLoopAndTail</c> iterates only
    /// the six rifle calibers. Resolving automatic fire for a pistol caliber
    /// has no declared row and no valid fallback, so it must throw rather
    /// than silently resolve to the wrong weapon's sound.
    /// </summary>
    [Fact]
    public void AutoModeForAPistolCaliberHasNoDeclaredRowAndThrows()
    {
        Assert.Throws<KeyNotFoundException>(() =>
            ShotSlotResolver.Resolve(
                CaliberFamily.Cal9X19, FireMode.Auto, rangeWu: 100, shooterIsIndoors: true,
                suppressorFitted: false, tick: 6, shooterEntityId: 19));
    }

    /// <summary>
    /// The variant selector is deterministic: the same tick and shooter
    /// entity id always selects the same one-based variant number.
    /// </summary>
    [Fact]
    public void SameTickAndEntityIdAlwaysSelectTheSameVariant()
    {
        var first = ShotSlotResolver.SelectVariantNumber(tick: 42, shooterEntityId: 99, variantCount: 6);
        var second = ShotSlotResolver.SelectVariantNumber(tick: 42, shooterEntityId: 99, variantCount: 6);

        Assert.Equal(first, second);
        Assert.InRange(first, 1, 6);
    }

    /// <summary>
    /// A variant count of zero or one has nothing to choose between, so the
    /// selector always returns variant one without touching the generator.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ASingleOrEmptyVariantCountAlwaysSelectsVariantOne(int variantCount)
    {
        Assert.Equal(1, ShotSlotResolver.SelectVariantNumber(tick: 1, shooterEntityId: 1, variantCount));
    }

    /// <summary>
    /// The resolver is reachable only from the client: it is declared
    /// <c>internal</c> in <c>Sandata.Client</c>, so no assembly other than
    /// <c>Sandata.Client</c> itself and its declared
    /// <c>InternalsVisibleTo</c> friend, <c>Sandata.Client.Tests</c>, can
    /// reference it. A public resolver would let a future assembly resolve
    /// shot sounds without going through the client's own audio pipeline.
    /// </summary>
    [Fact]
    public void ResolverTypeIsInternalToTheClientAssembly()
    {
        var type = typeof(ShotSlotResolver);

        Assert.False(type.IsPublic);
        Assert.True(type.IsNotPublic);
        Assert.Equal(typeof(SandataSoundCatalog).Assembly, type.Assembly);
    }
}
