using Hukbo.Core.Determinism;
using Sandata.Core.Weapons;

namespace Sandata.Client.Audio;

/// <summary>
/// The outcome of resolving one fired shot: the declared catalog row, the
/// numbered variant to play (matching <see cref="SandataSoundCatalog.GetVariantFileName"/>'s
/// one-based numbering, not <see cref="SoundSlot.VariantCount"/>'s zero-based
/// selection index), and that variant's exact file name.
/// </summary>
internal readonly record struct ShotSlotResolution(SoundSlot Slot, int VariantNumber, string FileName);

/// <summary>
/// Resolves a fired shot to the Sandata sound slot, variant, and file name
/// that should sound for it. Design section 10, "how the simulation's mode
/// choice selects the slot":
/// <code>
/// family      = mode is Auto ? GunLoop : GunReport
/// familyKey   = FirearmCatalog[firearm].Caliber
/// environment = f(shooter is indoors, range band, suppressor fitted)
/// slot        = SandataSoundCatalog.Find(family, familyKey, mode, environment)
/// variant     = SoundVariantSelector.Select(tick, shooterEntityId, slot.VariantCount)
/// </code>
/// This type is that pseudocode plus the file-name step, kept entirely in the
/// client: "the simulation never names a file" (design section 10). No task
/// before this one adds a <c>ShotFired</c> event type to <c>Sandata.Core</c> —
/// the firing tick stage is a later wave than this task's two dependencies,
/// 24 and 33 — so this resolver takes the event's would-be fields as separate
/// parameters (caliber, fire mode, range, tick, shooter entity id) rather than
/// a concrete event record, matching exactly what design section 10 names.
/// </summary>
/// <remarks>
/// <b>SoundVariantSelector was not reachable.</b> Design section 10 says to
/// call <c>Hukbo.Client.Audio.SoundVariantSelector.Select</c>, "copied by
/// reference, not by fork". That type is declared <c>internal static</c> in
/// <c>Hukbo.Client</c>, and <c>Hukbo.Client</c>'s <c>Properties/AssemblyInfo.cs</c>
/// grants <c>InternalsVisibleTo</c> only to <c>Hukbo.Client.Tests</c> — not to
/// <c>Sandata.Client</c> — so the type is unreachable from here, and that
/// <c>AssemblyInfo.cs</c> file sits outside this task's five-file allowance
/// and under <c>src/Hukbo.Client/</c>, which this task must not edit either
/// way. <see cref="SelectVariantNumber"/> below therefore ports the formula
/// verbatim — the same <see cref="SplitMix64"/> mix of the tick and the
/// source entity id — as a small private helper, rather than widening
/// <c>Hukbo.Client</c>'s internals or forking a second copy of
/// <c>SoundVariantSelector</c> under a different name.
/// </remarks>
/// <remarks>
/// <b>Environment selection is this task's own reasoned rule, not a pinned
/// design value.</b> Design section 10 names three inputs to
/// <c>environment = f(...)</c> — "shooter is indoors, range band, suppressor
/// fitted" — but gives no thresholds or priority order, and no earlier task
/// models "indoors", "range band", or "suppressor fitted" as a type at all;
/// only <c>RangeWu</c>, the raw world-unit distance the design's own
/// <c>ShotFired</c> field list names, exists anywhere upstream. This resolver
/// applies a documented priority — a suppressor overrides every other signal,
/// then a distant range, then indoors versus outdoors, then a close range —
/// using round world-unit thresholds set from the same scale
/// <c>FirearmCatalog</c>'s own range bands already use (the rifle template's
/// 240 wu automatic band and 800 wu single-fire band), and is provisional in
/// exactly the sense that catalog's own per-weapon timing values are
/// provisional: reasoned placeholders, not a measurement, pending a later
/// calibration task. When the computed environment is not declared for the
/// resolved family and mode — every baked-burst row omits <c>Distant</c> and
/// <c>Suppressed</c>, and every automatic row omits all three of
/// <c>CloseDry</c>, <c>Distant</c>, and <c>Suppressed</c> — this resolver
/// falls back to <see cref="SoundEnvironment.IndoorTail"/> or
/// <see cref="SoundEnvironment.OutdoorTail"/> by the shooter's own indoor
/// reading, since those two environments are the only pair every declared gun
/// family and mode combination in <see cref="SandataSoundCatalog"/> carries.
/// </remarks>
internal static class ShotSlotResolver
{
    /// <summary>
    /// The mix constant <c>Hukbo.Client.Audio.SoundVariantSelector</c> uses,
    /// reproduced exactly so a replay of the same seed selects the same
    /// variant a melee-side cue would with the same tick and entity id.
    /// </summary>
    private const ulong VariantMixConstant = 0x9E3779B97F4A7C15UL;

    /// <summary>
    /// The range, in world units, at or below which an unsuppressed,
    /// non-distant, outdoor shot reads as <see cref="SoundEnvironment.CloseDry"/>
    /// rather than <see cref="SoundEnvironment.OutdoorTail"/>. Provisional —
    /// see this type's remarks.
    /// </summary>
    private const int CloseRangeMaxWu = 200;

    /// <summary>
    /// The range, in world units, at or beyond which a shot reads as
    /// <see cref="SoundEnvironment.Distant"/> regardless of whether the
    /// shooter is indoors. Provisional — see this type's remarks.
    /// </summary>
    private const int DistantRangeMinWu = 800;

    /// <summary>
    /// Resolves one fired shot per design section 10's mapping, including
    /// this type's own documented environment and fallback rules.
    /// </summary>
    /// <param name="caliber">
    /// <c>FirearmCatalog[firearm].Caliber</c> — the caliber family that keys
    /// the report or loop sample, independent of the specific weapon.
    /// </param>
    /// <param name="mode">
    /// The fire mode the simulation selected for this shot. Only
    /// <see cref="FireMode.Single"/>, <see cref="FireMode.Burst2"/>,
    /// <see cref="FireMode.Burst3"/>, and <see cref="FireMode.Auto"/> are
    /// valid; every other member of <see cref="FireMode"/> names a mechanism
    /// or casing axis this resolver does not handle.
    /// </param>
    /// <param name="rangeWu">The engagement range, in world units. Never negative.</param>
    /// <param name="shooterIsIndoors">Whether the shooter's position is inside a room.</param>
    /// <param name="suppressorFitted">Whether the firing weapon carries a suppressor.</param>
    /// <param name="tick">The tick the shot occurred on, folded into variant selection.</param>
    /// <param name="shooterEntityId">The shooter's entity id, folded into variant selection.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="rangeWu"/> is negative, or <paramref name="mode"/> is
    /// not one of the four firing modes this resolver handles.
    /// </exception>
    public static ShotSlotResolution Resolve(
        CaliberFamily caliber,
        FireMode mode,
        int rangeWu,
        bool shooterIsIndoors,
        bool suppressorFitted,
        long tick,
        ulong shooterEntityId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rangeWu);

        var family = ResolveFamily(mode);
        var environment = ResolveEnvironment(shooterIsIndoors, rangeWu, suppressorFitted);
        var slot = FindWithFallback(family, (int)caliber, mode, environment, shooterIsIndoors);
        var variantNumber = SelectVariantNumber(tick, shooterEntityId, slot.VariantCount);
        var fileName = SandataSoundCatalog.GetVariantFileName(slot, variantNumber);

        return new ShotSlotResolution(slot, variantNumber, fileName);
    }

    /// <summary>
    /// Resolves the <see cref="SoundFamily.GunTail"/> row that pairs with an
    /// automatic shot's loop row under the same caliber, environment, and
    /// indoor reading — <see cref="SandataSoundCatalog"/> declares the two
    /// together, one <see cref="SoundFamily.GunLoop"/> and one
    /// <see cref="SoundFamily.GunTail"/> row per caliber and environment. The
    /// sound player uses this to pre-reserve and, once fire stops, play the
    /// tail instance that follows a sustained automatic burst.
    /// </summary>
    public static SoundSlot ResolveGunTailSlot(
        CaliberFamily caliber,
        int rangeWu,
        bool shooterIsIndoors,
        bool suppressorFitted)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rangeWu);

        var environment = ResolveEnvironment(shooterIsIndoors, rangeWu, suppressorFitted);
        return FindWithFallback(
            SoundFamily.GunTail, (int)caliber, FireMode.Auto, environment, shooterIsIndoors);
    }

    /// <summary>
    /// The ported <c>SoundVariantSelector.Select</c> formula, returning a
    /// one-based variant number in <c>[1, variantCount]</c> — one greater
    /// than the zero-based index the original selects — so the result feeds
    /// <see cref="SandataSoundCatalog.GetVariantFileName"/> directly. A count
    /// of zero or one always returns <c>1</c> without touching the generator,
    /// since there is nothing to choose between.
    /// </summary>
    public static int SelectVariantNumber(long tick, ulong shooterEntityId, int variantCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(variantCount);

        if (variantCount <= 1)
        {
            return 1;
        }

        var seed = unchecked((ulong)tick * VariantMixConstant) ^ shooterEntityId;
        var generator = new SplitMix64(seed);
        return generator.NextInt(variantCount) + 1;
    }

    private static SoundFamily ResolveFamily(FireMode mode) =>
        mode switch
        {
            FireMode.Auto => SoundFamily.GunLoop,
            FireMode.Single or FireMode.Burst2 or FireMode.Burst3 => SoundFamily.GunReport,
            _ => throw new ArgumentOutOfRangeException(
                nameof(mode),
                mode,
                "ShotSlotResolver only resolves the four firing fire modes: " +
                "Single, Burst2, Burst3, and Auto."),
        };

    private static SoundEnvironment ResolveEnvironment(bool indoors, int rangeWu, bool suppressed)
    {
        if (suppressed)
        {
            return SoundEnvironment.Suppressed;
        }

        if (rangeWu >= DistantRangeMinWu)
        {
            return SoundEnvironment.Distant;
        }

        if (indoors)
        {
            return SoundEnvironment.IndoorTail;
        }

        return rangeWu <= CloseRangeMaxWu
            ? SoundEnvironment.CloseDry
            : SoundEnvironment.OutdoorTail;
    }

    private static SoundSlot FindWithFallback(
        SoundFamily family,
        int familyKey,
        FireMode mode,
        SoundEnvironment environment,
        bool indoors)
    {
        if (SandataSoundCatalog.TryFind(family, familyKey, mode, environment, out var slot))
        {
            return slot;
        }

        // See this type's remarks: a baked-burst or automatic row does not
        // declare every environment. Falling back to the shooter's own
        // indoor reading rather than throwing keeps every (caliber, mode)
        // combination resolvable regardless of range or suppressor state.
        var fallbackEnvironment = indoors ? SoundEnvironment.IndoorTail : SoundEnvironment.OutdoorTail;
        return SandataSoundCatalog.Find(family, familyKey, mode, fallbackEnvironment);
    }
}
