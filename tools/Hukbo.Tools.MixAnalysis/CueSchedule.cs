using System.Globalization;
using Hukbo.Core.Combat;
using Hukbo.Core.Determinism;
using Hukbo.Core.Simulation;

namespace Hukbo.Tools.MixAnalysis;

/// <summary>
/// One cue the client would request: which file, at which tick.
/// </summary>
internal readonly record struct ScheduledCue(long Tick, int Slot, string FileName);

/// <summary>
/// Replays a real battle and produces the ordered list of sound cues the client
/// would request from it.
/// </summary>
/// <remarks>
/// This mirrors <c>SoundCueMapper</c>, <c>SoundCatalog</c>,
/// <c>HitClassCatalog</c>, <c>SoundLibrary</c>, and <c>SoundVariantSelector</c>
/// rather than calling them, because those types are <c>internal</c> to
/// <c>Hukbo.Client</c> and that assembly is a windowed MonoGame application.
/// The variant selection uses the same <see cref="SplitMix64"/> from
/// <c>Hukbo.Core</c> that the client uses, so the chosen file for a given tick
/// and entity is identical, not merely similar. <b>If the client's mapping,
/// catalog, or fallback chain changes, this file must change with it</b> — see
/// <c>docs/research/SOUND-CAPACITY-MEASUREMENTS.md</c> lines 468-473.
/// </remarks>
internal static class CueSchedule
{
    /// <summary>
    /// Twenty-six slots, in the exact order and at the exact index
    /// <c>GameSoundId</c> and <c>SoundCatalog.AllSounds</c> declare them, so a
    /// slot number here means the same thing it means in
    /// <c>SoundCueBudget</c>'s per-slot cap.
    /// </summary>
    public const int SlotCount = 26;

    // Mirrors SoundCatalog.AllSounds and SoundCatalog.GetBaseName, index for
    // index. Slot 25 (misfire-arquebus) is listed for completeness but
    // MapSlot below never returns it, because SoundCueMapper.Map has no case
    // that reaches it — the arquebus misfire mechanic has no Phase 1 emission
    // site (docs/archives/2026-08-10/2026-08-08-ranged-units-handoff.md).
    private static readonly string[] SlotBaseNames =
    [
        "attack-kampilan",       // 0  AttackKampilan
        "attack-wasay",          // 1  AttackWasay
        "attack-kalis",          // 2  AttackKalis
        "attack-itak",           // 3  AttackItak
        "death",                 // 4  Death
        "victory-blue",          // 5  VictoryBlue
        "victory-red",           // 6  VictoryRed
        "draw",                  // 7  Draw
        "ui-click",              // 8  UiClick
        "clash-shield-kampilan", // 9  ClashShieldKampilan
        "clash-shield-wasay",    // 10 ClashShieldWasay
        "clash-shield-kalis",    // 11 ClashShieldKalis
        "clash-shield-itak",     // 12 ClashShieldItak
        "release-bangkaw",       // 13 ReleaseBangkaw
        "release-busog",         // 14 ReleaseBusog
        "release-arquebus",      // 15 ReleaseArquebus
        "attack-bangkaw",        // 16 AttackBangkaw
        "attack-busog",          // 17 AttackBusog
        "attack-arquebus",       // 18 AttackArquebus
        "clash-shield-bangkaw",  // 19 ClashShieldBangkaw
        "clash-shield-busog",    // 20 ClashShieldBusog
        "clash-shield-arquebus", // 21 ClashShieldArquebus
        "miss-bangkaw",          // 22 MissBangkaw
        "miss-busog",            // 23 MissBusog
        "miss-arquebus",         // 24 MissArquebus
        "misfire-arquebus",      // 25 MisfireArquebus
    ];

    // Mirrors SoundCatalog.IsHitLocationDriven: only the seven weapon attack
    // slots. Not the clash slots, not release, not miss.
    private static readonly bool[] HitLocationDriven =
    [
        true, true, true, true,            // 0-3   melee attacks
        false, false, false, false, false, // 4-8   death / outcome / click
        false, false, false, false,        // 9-12  melee clash
        false, false, false,               // 13-15 release
        true, true, true,                  // 16-18 ranged attacks
        false, false, false,               // 19-21 ranged clash
        false, false, false,               // 22-24 miss
        false,                             // 25    misfire
    ];

    private static readonly string[] ClassTokens =
        ["skull", "neck", "ribcage", "gut", "limb", "extremity"];

    // Mirrors HitClassCatalog.GetFallbackChain, by class index (Skull=0,
    // Neck=1, Ribcage=2, Gut=3, Limb=4, Extremity=5).
    private static readonly int[][] FallbackChains =
    [
        [1, 2], // skull -> neck, ribcage
        [2],    // neck -> ribcage
        [],     // ribcage
        [2],    // gut -> ribcage
        [2],    // limb -> ribcage
        [4, 2], // extremity -> limb, ribcage
    ];

    public static string SlotName(int slot) => SlotBaseNames[slot];

    public static bool IsHitLocationDriven(int slot) => HitLocationDriven[slot];

    /// <summary>
    /// Runs the battle and returns every cue in emission order, with no budget
    /// applied. Applying a budget is the caller's job, so the same schedule can
    /// be rendered under several policies. <paramref name="clips"/> is the raw
    /// set of files discovered on disk, keyed by file name — the same input
    /// <c>SoundLibrary.Resolve</c> and <c>SoundLibrary.ResolveVariants</c>
    /// take — so the numbered-match, fallback-chain, and bare-single rules
    /// below mirror them exactly rather than only the numbered-variant case
    /// the previous replica implemented. <paramref name="preset"/> selects the
    /// combat ruleset via <c>Scenario.CombatPreset</c>, exactly the way
    /// <c>HeadlessRunner</c>'s <c>--preset</c> flag does
    /// (<c>scenario = scenario with { CombatPreset = preset }</c>); passing
    /// <see cref="CombatPresetId.PrecolonialPhilippinesV5"/> is what fields a
    /// ranged roster instead of the melee-only default.
    /// <see cref="MappedCountsBySlot"/> on the result counts every event that
    /// mapped to a slot, whether or not a file existed to resolve — the raw
    /// demand, distinct from <c>Cues.Count</c>, which only counts cues that
    /// also resolved to a playable file. The two agree once every slot has
    /// shipped audio; today they do not for the thirteen ranged slots, and
    /// that gap is itself the finding this task's re-run must report.
    /// <see cref="MappedEvents"/> carries every mapped (tick, slot) pair in
    /// emission order, independent of file resolution, so a caller can run
    /// the same per-frame budget accounting <see cref="Mixer"/> applies to
    /// playable cues against the full demand instead — the only way to ask
    /// whether the per-slot cap would bind on a slot that currently has no
    /// audio to play at all.
    /// </summary>
    public static (
        IReadOnlyList<ScheduledCue> Cues,
        long TicksRun,
        string Outcome,
        int[] MappedCountsBySlot,
        IReadOnlyList<(long Tick, int Slot)> MappedEvents) Build(
        int agents,
        ulong seed,
        int tickLimit,
        IReadOnlyDictionary<string, WavClip> clips,
        CombatPresetId preset)
    {
        var (rawMatches, bareMatches) = BuildRawMatches(clips);

        var scenario = Scenario.CreateDefault(seed, agents) with
        {
            TickLimit = tickLimit,
            CombatPreset = preset,
        };
        scenario.Validate();
        var simulation = BattleSimulation.Create(scenario);

        // A classless Release/Miss event cannot name its own weapon --
        // BattleEvent.NonAttack forces Weapon null by construction for both
        // kinds (BattleEvent.cs). SoundDirector.Ingest (RU-19) resolves the
        // Release case from the source agent's AgentView.Loadout instead of
        // the event; mirror that here or every Release event maps to slot -1
        // forever, which is exactly the defect this re-run exists to catch.
        // A loadout is fixed at spawn and never changes, even after death
        // (BattleSimulation.cs's A0-pass comment on Pass A0), so one snapshot
        // taken immediately after creation -- before any tick runs -- covers
        // the whole battle.
        var launcherWeapons = new Dictionary<ulong, WeaponId>();
        foreach (var view in simulation.Agents)
        {
            launcherWeapons[view.EntityId] = view.Loadout.Weapon;
        }

        var cues = new List<ScheduledCue>();
        var mappedCountsBySlot = new int[SlotCount];
        var mappedEvents = new List<(long Tick, int Slot)>();

        while (simulation.Outcome == BattleOutcome.Ongoing && simulation.Tick < tickLimit)
        {
            simulation.AdvanceOneTick();
            foreach (var battleEvent in simulation.LastEvents)
            {
                var slot = MapSlot(battleEvent, launcherWeapons);
                if (slot < 0)
                {
                    continue;
                }

                mappedCountsBySlot[slot]++;
                mappedEvents.Add((battleEvent.Tick, slot));

                var fileName = ResolveFile(
                    slot,
                    battleEvent.HitLocation,
                    battleEvent.Tick,
                    battleEvent.SourceEntityId,
                    rawMatches,
                    bareMatches);

                if (fileName is not null)
                {
                    cues.Add(new ScheduledCue(battleEvent.Tick, slot, fileName));
                }
            }
        }

        return (cues, simulation.Tick, simulation.Outcome.ToString(), mappedCountsBySlot, mappedEvents);
    }

    /// <summary>Mirrors <c>SoundCueMapper.Map</c>.</summary>
    private static int MapSlot(
        BattleEvent battleEvent,
        IReadOnlyDictionary<ulong, WeaponId> launcherWeapons) =>
        battleEvent.Kind switch
        {
            BattleEventKind.Attack => MapAttackSlot(battleEvent.Weapon, battleEvent.Resolution),
            BattleEventKind.Death => 4,
            BattleEventKind.Outcome => battleEvent.FactionId switch
            {
                0 => 5,
                1 => 6,
                _ => 7,
            },
            BattleEventKind.Release => MapReleaseSlot(
                ResolveLauncherWeapon(battleEvent.SourceEntityId, launcherWeapons)),
            BattleEventKind.Miss => MapMissSlot(battleEvent.Weapon),
            _ => -1,
        };

    /// <summary>
    /// Looks up the launching agent's weapon from the battle-start loadout
    /// snapshot, exactly what <c>SoundDirector.ResolveReleaseSound</c> does
    /// against the live <c>AgentView</c> list. Returns <c>null</c> — no cue,
    /// no throw — if the source entity is somehow absent, mirroring
    /// <c>SoundDirector</c>'s own miss case.
    /// </summary>
    private static WeaponId? ResolveLauncherWeapon(
        ulong sourceEntityId,
        IReadOnlyDictionary<ulong, WeaponId> launcherWeapons) =>
        launcherWeapons.TryGetValue(sourceEntityId, out var weapon) ? weapon : null;

    /// <summary>Mirrors <c>SoundCueMapper.MapAttack</c>.</summary>
    private static int MapAttackSlot(WeaponId? weapon, AttackResolution? resolution)
    {
        if (resolution == AttackResolution.ShieldBlocked)
        {
            return MapShieldClashSlot(weapon);
        }

        if (resolution == AttackResolution.Evaded && IsRanged(weapon))
        {
            return MapMissSlot(weapon);
        }

        return MapWeaponSlot(weapon);
    }

    /// <summary>Mirrors <c>SoundCueMapper.IsRanged</c>.</summary>
    private static bool IsRanged(WeaponId? weapon) =>
        weapon is WeaponId.Bangkaw or WeaponId.Busog or WeaponId.Arquebus;

    /// <summary>Mirrors <c>SoundCueMapper.MapWeapon</c>.</summary>
    private static int MapWeaponSlot(WeaponId? weapon) =>
        weapon switch
        {
            WeaponId.Kampilan => 0,
            WeaponId.Wasay => 1,
            WeaponId.Kalis => 2,
            WeaponId.Itak => 3,
            WeaponId.Bangkaw => 16,
            WeaponId.Busog => 17,
            WeaponId.Arquebus => 18,
            _ => -1,
        };

    /// <summary>Mirrors <c>SoundCueMapper.MapShieldClash</c>.</summary>
    private static int MapShieldClashSlot(WeaponId? weapon) =>
        weapon switch
        {
            WeaponId.Kampilan => 9,
            WeaponId.Wasay => 10,
            WeaponId.Kalis => 11,
            WeaponId.Itak => 12,
            WeaponId.Bangkaw => 19,
            WeaponId.Busog => 20,
            WeaponId.Arquebus => 21,
            _ => -1,
        };

    /// <summary>
    /// Mirrors <c>SoundCueMapper.MapRelease</c>, the hook RU-14 exposed for
    /// <c>SoundDirector.ResolveReleaseSound</c> (RU-19, merged) to call
    /// directly with the launching agent's weapon. <c>BattleEvent.Weapon</c>
    /// is always <c>null</c> for a <see cref="BattleEventKind.Release"/>
    /// event by construction (the enum's own doc comment says so), so the
    /// weapon reaching this method must come from
    /// <see cref="MapSlot"/>'s <c>ResolveLauncherWeapon</c> lookup against the
    /// source agent's loadout, never from <c>battleEvent.Weapon</c> directly
    /// — that is exactly the RU-19 fix this replica now mirrors.
    /// </summary>
    private static int MapReleaseSlot(WeaponId? weapon) =>
        weapon switch
        {
            WeaponId.Bangkaw => 13,
            WeaponId.Busog => 14,
            WeaponId.Arquebus => 15,
            _ => -1,
        };

    /// <summary>
    /// Mirrors <c>SoundCueMapper.MapMiss</c>. Deliberately reads
    /// <c>battleEvent.Weapon</c> directly, unlike <see cref="MapReleaseSlot"/>
    /// — the real client's <c>SoundDirector.Ingest</c> routes only
    /// <see cref="BattleEventKind.Release"/> through the loadout-lookup fix;
    /// a genuine <see cref="BattleEventKind.Miss"/> event (the target died in
    /// flight) still goes through the unmodified
    /// <c>SoundCueMapper.Map(battleEvent)</c> path and so still resolves -1
    /// in production today. Mirroring that here, warts included, is required
    /// parity, not an oversight — an <see cref="AttackResolution.Evaded"/>
    /// ranged attack is a different event kind (<c>Attack</c>, not
    /// <c>Miss</c>) that still carries its weapon and reaches this correctly
    /// through <see cref="MapAttackSlot"/>.
    /// </summary>
    private static int MapMissSlot(WeaponId? weapon) =>
        weapon switch
        {
            WeaponId.Bangkaw => 22,
            WeaponId.Busog => 23,
            WeaponId.Arquebus => 24,
            _ => -1,
        };

    /// <summary>Mirrors <c>HitClassCatalog.FromBodyPart</c>, by class index.</summary>
    private static int ClassFromBodyPart(BodyPart bodyPart) =>
        bodyPart switch
        {
            BodyPart.Head or BodyPart.Face => 0,
            BodyPart.Neck => 1,
            BodyPart.Chest => 2,
            BodyPart.Abdomen => 3,
            BodyPart.Shoulder or BodyPart.Thigh or BodyPart.Knee => 4,
            BodyPart.WeaponArm or
                BodyPart.ShieldArm or
                BodyPart.Shin or
                BodyPart.Hands or
                BodyPart.Feet => 5,
            _ => throw new ArgumentOutOfRangeException(
                nameof(bodyPart),
                bodyPart,
                "Every body part must map to an acoustic hit class."),
        };

    /// <summary>
    /// Resolves the fallback-substituted file list for a (slot, class) pair
    /// exactly the way <c>SoundLibrary.ResolveClassVariant</c> and its
    /// classless counterpart in <c>ResolveVariants</c> do — a direct numbered
    /// match, then the class's fallback chain in order, then the bare
    /// <c>&lt;slot&gt;.wav</c> single — and then draws a variant from that
    /// resolved list with <c>SoundVariantSelector.Select</c>. Returns
    /// <c>null</c> when nothing resolves, exactly as
    /// <c>SoundBindingStatus.Missing</c> does on the client.
    /// </summary>
    private static string? ResolveFile(
        int slot,
        BodyPart? hitLocation,
        long tick,
        ulong sourceEntityId,
        Dictionary<(int Slot, int ClassIndex), List<string>> rawMatches,
        Dictionary<int, string?> bareMatches)
    {
        List<string>? resolved;

        if (HitLocationDriven[slot])
        {
            if (hitLocation is not { } bodyPart)
            {
                return null;
            }

            var hitClass = ClassFromBodyPart(bodyPart);
            resolved = LookupOrNull(rawMatches, slot, hitClass);
            if (resolved is null)
            {
                foreach (var fallbackClass in FallbackChains[hitClass])
                {
                    resolved = LookupOrNull(rawMatches, slot, fallbackClass);
                    if (resolved is not null)
                    {
                        break;
                    }
                }
            }
        }
        else
        {
            resolved = LookupOrNull(rawMatches, slot, classIndex: -1);
        }

        if (resolved is null)
        {
            return bareMatches[slot];
        }

        return resolved[SelectVariant(tick, sourceEntityId, resolved.Count)];
    }

    private static List<string>? LookupOrNull(
        Dictionary<(int Slot, int ClassIndex), List<string>> rawMatches,
        int slot,
        int classIndex) =>
        rawMatches.TryGetValue((slot, classIndex), out var found) && found.Count > 0
            ? found
            : null;

    /// <summary>
    /// Builds the same two lookups <c>SoundLibrary.BuildRawMatches</c> builds:
    /// every numbered match per (slot, class-or-classless) key, sorted
    /// ascending by variant index, and the bare <c>&lt;slot&gt;.wav</c> single
    /// per slot. <paramref name="classIndex"/> <c>-1</c> stands in for
    /// <c>HitClass? null</c> — the classless key a non-hit-location-driven
    /// slot uses.
    /// </summary>
    private static (
        Dictionary<(int Slot, int ClassIndex), List<string>> RawMatches,
        Dictionary<int, string?> BareMatches) BuildRawMatches(
        IReadOnlyDictionary<string, WavClip> clips)
    {
        var fileNames = clips.Keys.Order(StringComparer.Ordinal).ToArray();

        var rawMatches = new Dictionary<(int, int), List<string>>();
        var bareMatches = new Dictionary<int, string?>();

        for (var slot = 0; slot < SlotCount; slot++)
        {
            var baseName = SlotBaseNames[slot];
            bareMatches[slot] = FindExactMatch(fileNames, baseName + ".wav");

            if (HitLocationDriven[slot])
            {
                for (var classIndex = 0; classIndex < ClassTokens.Length; classIndex++)
                {
                    var prefix = baseName + "-" + ClassTokens[classIndex] + "-";
                    rawMatches[(slot, classIndex)] = FindNumberedMatches(fileNames, prefix);
                }
            }
            else
            {
                rawMatches[(slot, -1)] = FindNumberedMatches(fileNames, baseName + "-");
            }
        }

        return (rawMatches, bareMatches);
    }

    private static string? FindExactMatch(string[] fileNames, string expected)
    {
        foreach (var fileName in fileNames)
        {
            if (string.Equals(fileName, expected, StringComparison.OrdinalIgnoreCase))
            {
                return fileName;
            }
        }

        return null;
    }

    /// <summary>
    /// Mirrors <c>SoundLibrary.FindNumberedMatches</c> and its
    /// <c>TryParseVariantIndex</c> helper exactly: a case-insensitive
    /// <c>&lt;prefix&gt;NN.wav</c> match where <c>NN</c> is exactly two digits
    /// and one-based, ordered ascending by index.
    /// </summary>
    private static List<string> FindNumberedMatches(string[] fileNames, string prefix)
    {
        var matches = new List<(int Index, string FileName)>();
        foreach (var fileName in fileNames)
        {
            if (TryParseVariantIndex(fileName, prefix, out var index))
            {
                matches.Add((index, fileName));
            }
        }

        return matches
            .OrderBy(match => match.Index)
            .Select(match => match.FileName)
            .ToList();
    }

    private static bool TryParseVariantIndex(string fileName, string prefix, out int index)
    {
        index = 0;

        const string extension = ".wav";
        if (!fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var withoutExtension = fileName[..^extension.Length];
        if (!withoutExtension.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var suffix = withoutExtension[prefix.Length..];
        return suffix.Length == 2 &&
            int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out index) &&
            index > 0;
    }

    /// <summary>Mirrors <c>SoundVariantSelector.Select</c> exactly.</summary>
    private static int SelectVariant(long tick, ulong sourceEntityId, int variantCount)
    {
        if (variantCount <= 1)
        {
            return 0;
        }

        const ulong MixConstant = 0x9E3779B97F4A7C15UL;
        var seed = unchecked((ulong)tick * MixConstant) ^ sourceEntityId;
        var generator = new SplitMix64(seed);
        return generator.NextInt(variantCount);
    }
}
