namespace Sandata.Core.Determinism;

/// <summary>
/// Identifies one of Sandata's independent random-number streams. Design
/// section 4's "ordering and randomness" rule derives a stream from
/// <c>(missionSeed, systemTag, entityId or eventId)</c>, so that a draw added
/// to one system can never collide with, or shift, a draw made by another —
/// the tag is what keeps the streams apart. The four v0.1 tags are named in
/// that same design paragraph: <see cref="Accuracy"/>, <see cref="Reaction"/>,
/// <see cref="Sidestep"/>, and <see cref="SpawnJitter"/>.
/// </summary>
/// <remarks>
/// <b>Dense from zero, append-only.</b> A member's numeric value is part of
/// the same replay contract <c>CLAUDE.md</c> section 5 states for Hukbo and
/// design section 4 states for Sandata: every stream key folded through this
/// tag, in a saved replay or a recorded seed-1 baseline, names the system by
/// this numeric value, not by its member name. Changing an existing member's
/// value re-keys every draw that system makes — the same draw, taken with the
/// same mission seed and the same entity or event id, would silently produce
/// a different number — so a member's value never changes once it has
/// shipped, a member is never reordered, and a retired value is never reused
/// for a different system. Adding a fifth tag is always safe: append it after
/// <see cref="SpawnJitter"/> with the next integer. <c>SandataSystemTagTests</c>
/// pins every member's numeric value as a literal so an accidental
/// renumbering fails loudly.
/// </remarks>
public enum SandataSystemTag
{
    /// <summary>
    /// The firearm accuracy draw: design section 9's angular error sampled
    /// from a shot's computed dispersion. Folded by
    /// <see cref="Combat.AccuracyRules.DrawAngularErrorBam"/>.
    /// </summary>
    Accuracy = 0,

    /// <summary>
    /// An operator's reaction-time draw when a contact first becomes visible
    /// or the alert state changes. Not yet consumed by any task in this
    /// worktree; declared here so its stream key is stable before the system
    /// that draws from it is built.
    /// </summary>
    Reaction = 1,

    /// <summary>
    /// The lateral sidestep offset an operator draws while approaching or
    /// holding a position, mirroring the "readable tag constant" stream
    /// <c>Hukbo.Core.Simulation.ApproachSidestep</c> keys by name alone. Not
    /// yet consumed by any task in this worktree.
    /// </summary>
    Sidestep = 2,

    /// <summary>
    /// The small positional jitter applied to a spawned operator so a squad
    /// does not spawn in a perfectly regular grid. Not yet consumed by any
    /// task in this worktree.
    /// </summary>
    SpawnJitter = 3,
}
