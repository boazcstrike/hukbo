namespace Sandata.Core.Sensing;

/// <summary>
/// The three-tier contact model design section 4 of
/// docs/plans/2026-08-07-sandata-scaffold-design.md names directly: "unknown,
/// then a question mark meaning 'something is there but beyond identify range
/// and not shootable', then identified." Backs
/// <c>Sandata.Core.Simulation.ContactMemoryEntry.ContactTier</c>'s raw
/// <see langword="int"/> field — that type's own remarks say this enum's
/// numbering is task 35's to declare, not that file's to enforce, and this is
/// that declaration.
/// </summary>
/// <remarks>
/// The numeric values are ordered so that a higher number always means "more
/// is known", which is what lets <see cref="ContactMemory.ClassifyTier"/>
/// decide a tier from nothing but a squared distance comparison, with no
/// branch that has to reason about tier order separately from tier value.
/// </remarks>
public enum ContactTier
{
    /// <summary>
    /// No contact recorded. An enemy at this tier has no
    /// <c>Sandata.Core.Simulation.ContactMemoryEntry</c> in an operator's
    /// contact memory at all — the entry's absence from the memory array
    /// <em>is</em> the "unknown" representation, so nothing ever constructs a
    /// <c>ContactMemoryEntry</c> whose <c>ContactTier</c> is this value. See
    /// <see cref="ContactMemory.Update"/>'s remarks.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Present — detected within sensing range this tick, or remembered from
    /// an earlier tick as a ghost — but beyond identify range: something is
    /// there, and design section 4 is explicit that it is "not shootable" at
    /// this tier.
    /// </summary>
    QuestionMark = 1,

    /// <summary>
    /// Within identify range: identified and shootable.
    /// </summary>
    Identified = 2,
}
