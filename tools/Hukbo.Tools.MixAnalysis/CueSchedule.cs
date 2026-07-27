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
/// This mirrors <c>SoundCueMapper</c>, <c>HitClassCatalog</c>, and
/// <c>SoundVariantSelector</c> rather than calling them, because those types are
/// <c>internal</c> to <c>Hukbo.Client</c> and that assembly is a windowed
/// MonoGame application. The variant selection uses the same
/// <see cref="SplitMix64"/> from <c>Hukbo.Core</c> that the client uses, so the
/// chosen file for a given tick and entity is identical, not merely similar.
/// If the client's mapping changes, this file must change with it.
/// </remarks>
internal static class CueSchedule
{
    public const int SlotCount = 9;

    private static readonly string[] SlotBaseNames =
    [
        "attack-kampilan",
        "attack-wasay",
        "attack-kalis",
        "attack-itak",
        "death",
        "victory-blue",
        "victory-red",
        "draw",
        "ui-click",
    ];

    private static readonly string[] ClassTokens =
    [
        "skull", "neck", "ribcage", "gut", "limb", "extremity",
    ];

    // Mirrors HitClassCatalog.GetFallbackChain, by class index.
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

    /// <summary>
    /// Runs the battle and returns every cue in emission order, with no budget
    /// applied. Applying a budget is the caller's job, so the same schedule can
    /// be rendered under several policies.
    /// </summary>
    public static (IReadOnlyList<ScheduledCue> Cues, long TicksRun, string Outcome) Build(
        int agents,
        ulong seed,
        int tickLimit,
        IReadOnlyDictionary<string, IReadOnlyList<string>> variantsByPrefix)
    {
        var scenario = Scenario.CreateDefault(seed, agents) with { TickLimit = tickLimit };
        scenario.Validate();
        var simulation = BattleSimulation.Create(scenario);
        var cues = new List<ScheduledCue>();

        while (simulation.Outcome == BattleOutcome.Ongoing && simulation.Tick < tickLimit)
        {
            simulation.AdvanceOneTick();
            foreach (var battleEvent in simulation.LastEvents)
            {
                var slot = MapSlot(battleEvent);
                if (slot < 0)
                {
                    continue;
                }

                var fileName = ResolveFile(
                    slot,
                    battleEvent.HitLocation,
                    battleEvent.Tick,
                    battleEvent.SourceEntityId,
                    variantsByPrefix);

                if (fileName is not null)
                {
                    cues.Add(new ScheduledCue(battleEvent.Tick, slot, fileName));
                }
            }
        }

        return (cues, simulation.Tick, simulation.Outcome.ToString());
    }

    private static int MapSlot(BattleEvent battleEvent) =>
        battleEvent.Kind switch
        {
            BattleEventKind.Death => 4,
            BattleEventKind.Outcome => battleEvent.FactionId switch
            {
                0 => 5,
                1 => 6,
                _ => 7,
            },
            BattleEventKind.Attack => battleEvent.Weapon switch
            {
                WeaponId.Kampilan => 0,
                WeaponId.Wasay => 1,
                WeaponId.Kalis => 2,
                WeaponId.Itak => 3,
                _ => -1,
            },
            _ => -1,
        };

    private static bool IsHitLocationDriven(int slot) => slot <= 3;

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

    private static string? ResolveFile(
        int slot,
        BodyPart? hitLocation,
        long tick,
        ulong sourceEntityId,
        IReadOnlyDictionary<string, IReadOnlyList<string>> variantsByPrefix)
    {
        var baseName = SlotBaseNames[slot];
        IReadOnlyList<string>? variants = null;

        if (IsHitLocationDriven(slot) && hitLocation is { } bodyPart)
        {
            var hitClass = ClassFromBodyPart(bodyPart);
            variants = Lookup(variantsByPrefix, $"{baseName}-{ClassTokens[hitClass]}-");
            if (variants is null)
            {
                foreach (var fallback in FallbackChains[hitClass])
                {
                    variants = Lookup(variantsByPrefix, $"{baseName}-{ClassTokens[fallback]}-");
                    if (variants is not null)
                    {
                        break;
                    }
                }
            }
        }

        variants ??= Lookup(variantsByPrefix, baseName + "-");
        if (variants is null || variants.Count == 0)
        {
            return null;
        }

        return variants[SelectVariant(tick, sourceEntityId, variants.Count)];
    }

    private static IReadOnlyList<string>? Lookup(
        IReadOnlyDictionary<string, IReadOnlyList<string>> variantsByPrefix,
        string prefix) =>
        variantsByPrefix.TryGetValue(prefix, out var found) && found.Count > 0
            ? found
            : null;

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
