using Hukbo.Core.Combat;

namespace Hukbo.Client.Audio;

/// <summary>
/// The six acoustic classes a hit-location driven sound variant can belong to.
/// Several of the thirteen <see cref="BodyPart"/> values are acoustically
/// identical to an ear, so they collapse into one class rather than needing
/// thirteen separate sounds. See
/// the sound variant matrix design for
/// the acoustic rationale behind each grouping.
/// </summary>
internal enum HitClass
{
    Skull = 0,
    Neck = 1,
    Ribcage = 2,
    Gut = 3,
    Limb = 4,
    Extremity = 5,
}

/// <summary>
/// The fixed <see cref="HitClass"/> ordering, the <see cref="BodyPart"/> map,
/// the lowercase file-name token, and the fallback chain used when a class has
/// no generated file of its own. Every rule here is a fixed table, never
/// derived from enum reflection order, so the file-name contract cannot drift
/// silently if a member is ever added.
/// </summary>
internal static class HitClassCatalog
{
    /// <summary>
    /// Every class, in the fixed display and take-allocation order used by the
    /// design document. Never derived from <c>Enum.GetValues</c>.
    /// </summary>
    public static IReadOnlyList<HitClass> All { get; } =
    [
        HitClass.Skull,
        HitClass.Neck,
        HitClass.Ribcage,
        HitClass.Gut,
        HitClass.Limb,
        HitClass.Extremity,
    ];

    /// <summary>
    /// Maps a resolved hit location to its acoustic class. Every one of the
    /// thirteen <see cref="BodyPart"/> values is covered; an undefined value
    /// throws rather than silently picking a default.
    /// </summary>
    public static HitClass FromBodyPart(BodyPart bodyPart) =>
        bodyPart switch
        {
            BodyPart.Head or BodyPart.Face => HitClass.Skull,
            BodyPart.Neck => HitClass.Neck,
            BodyPart.Chest => HitClass.Ribcage,
            BodyPart.Abdomen => HitClass.Gut,
            BodyPart.Shoulder or BodyPart.Thigh or BodyPart.Knee => HitClass.Limb,
            BodyPart.WeaponArm or
                BodyPart.ShieldArm or
                BodyPart.Shin or
                BodyPart.Hands or
                BodyPart.Feet => HitClass.Extremity,
            _ => throw new ArgumentOutOfRangeException(
                nameof(bodyPart),
                bodyPart,
                "Every body part must map to an acoustic hit class."),
        };

    /// <summary>
    /// The lowercase token used inside a variant file name, e.g. the
    /// <c>skull</c> in <c>attack-kampilan-skull-01.wav</c>. Matches the
    /// <c>-Class</c> parameter's validated set in <c>scripts/sfx.ps1</c>
    /// exactly.
    /// </summary>
    public static string GetToken(HitClass hitClass) =>
        hitClass switch
        {
            HitClass.Skull => "skull",
            HitClass.Neck => "neck",
            HitClass.Ribcage => "ribcage",
            HitClass.Gut => "gut",
            HitClass.Limb => "limb",
            HitClass.Extremity => "extremity",
            _ => throw new ArgumentOutOfRangeException(
                nameof(hitClass),
                hitClass,
                "Every hit class must declare a file-name token."),
        };

    /// <summary>
    /// The fixed, ordered fallback chain tried when <paramref name="hitClass"/>
    /// has no file of its own: extremity falls back to limb then ribcage,
    /// skull to neck then ribcage, neck and gut fall back to ribcage directly,
    /// and limb falls back to ribcage. Ribcage has no class to fall back to —
    /// its last resort is the bare <c>&lt;slot&gt;.wav</c> single, which is a
    /// property of the slot rather than of a class and is therefore applied
    /// by the caller, not returned here.
    /// </summary>
    public static IReadOnlyList<HitClass> GetFallbackChain(HitClass hitClass) =>
        hitClass switch
        {
            HitClass.Extremity => [HitClass.Limb, HitClass.Ribcage],
            HitClass.Skull => [HitClass.Neck, HitClass.Ribcage],
            HitClass.Neck => [HitClass.Ribcage],
            HitClass.Gut => [HitClass.Ribcage],
            HitClass.Limb => [HitClass.Ribcage],
            HitClass.Ribcage => [],
            _ => throw new ArgumentOutOfRangeException(
                nameof(hitClass),
                hitClass,
                "Every hit class must declare a fallback chain."),
        };
}
