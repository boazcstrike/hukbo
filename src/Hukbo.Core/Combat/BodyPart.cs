using System.Collections.Immutable;

namespace Hukbo.Core.Combat;

/// <summary>
/// Canonical pre-colonial Philippine warrior body-part taxonomy used to
/// give every accepted attack an explainable, authoritative hit location.
/// Hit location is metadata only: it does not change damage, health
/// capacity, cooldown, future actions, or death.
/// </summary>
/// <remarks>
/// Numeric IDs are part of the deterministic replay and content-hash
/// contract. They must stay stable, and their ascending order drives
/// deterministic weighted selection in <see cref="HitLocationResolver"/>.
/// Changing a value, adding, or reordering a part requires a new combat
/// preset version and new golden expectations.
/// </remarks>
public enum BodyPart
{
    WeaponArm = 1,
    ShieldArm = 2,
    Shoulder = 3,
    Head = 4,
    Neck = 5,
    Face = 6,
    Chest = 7,
    Abdomen = 8,
    Thigh = 9,
    Knee = 10,
    Shin = 11,
    Hands = 12,
    Feet = 13,
}

/// <summary>
/// Fixed, allocation-free enumeration of every canonical <see cref="BodyPart"/>
/// value in ascending numeric order. Deterministic combat code must use
/// this catalog instead of <c>Enum.GetValues&lt;BodyPart&gt;()</c> so that
/// iteration order is pinned to the replay contract and does not allocate
/// on every attack. Backed by <see cref="ImmutableArray{T}"/> instead of a
/// mutable array so nothing can rewrite the catalog after startup; a test
/// asserts this sequence stays in lockstep with every declared
/// <see cref="BodyPart"/> enum member, so an added or reordered member
/// cannot silently fall out of the replay and content-hash contract.
/// </summary>
internal static class BodyPartCatalog
{
    internal static readonly ImmutableArray<BodyPart> Ordered =
    [
        BodyPart.WeaponArm,
        BodyPart.ShieldArm,
        BodyPart.Shoulder,
        BodyPart.Head,
        BodyPart.Neck,
        BodyPart.Face,
        BodyPart.Chest,
        BodyPart.Abdomen,
        BodyPart.Thigh,
        BodyPart.Knee,
        BodyPart.Shin,
        BodyPart.Hands,
        BodyPart.Feet,
    ];
}
