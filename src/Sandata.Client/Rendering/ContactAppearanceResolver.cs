using System.Collections.Immutable;
using Sandata.Core.Sensing;
using Sandata.Core.Simulation;

namespace Sandata.Client.Rendering;

/// <summary>
/// How a living hostile operator should read on screen, task 10's answer to
/// design section 6's D5: "an <c>Identified</c> hostile draws as it does
/// today, a <c>Detected</c> one draws as an unknown-contact marker with no
/// facing and no weapon, and a hostile nobody has any memory of is not drawn
/// at all." <see cref="ContactAppearanceResolver.ResolveHostileAppearance"/>
/// is this enum's only producer.
/// </summary>
internal enum ContactAppearance
{
    /// <summary>
    /// No assaulting operator has ever observed this hostile
    /// (<see cref="ContactTier.Unknown"/>): not drawn at all. This is not
    /// fog of war — the simulation still moves, still fires, and still
    /// hashes it; only the draw call is skipped.
    /// </summary>
    Hidden,

    /// <summary>
    /// Detected but not identified (<see cref="ContactTier.QuestionMark"/>):
    /// draws as the facingless, weaponless marker
    /// <see cref="Rendering.OperatorGeometry.Create"/>'s <c>isUnknownContact</c>
    /// parameter produces.
    /// </summary>
    Unknown,

    /// <summary>
    /// Identified (<see cref="ContactTier.Identified"/>), or the caller does
    /// not gate this operator on contact tier at all (a friendly, or a
    /// casualty): draws exactly as it always has.
    /// </summary>
    Identified,
}

/// <summary>
/// Pure resolver for task 10's unknown-contact rendering — no
/// <c>GraphicsDevice</c>, no <c>SpriteBatch</c>, no window, only plain
/// <see cref="OperatorState"/> values, so
/// <c>tests/Sandata.Client.Tests</c> can pin it directly. Presentation only:
/// nothing here writes back to <see cref="OperatorState"/> or to any
/// <c>Sandata.Core</c> type, and <see cref="MissionState.Operators"/> is
/// unread by anything else this file touches.
/// </summary>
internal static class ContactAppearanceResolver
{
    /// <summary>
    /// The assaulting faction's own best (highest) <see cref="ContactTier"/>
    /// for <paramref name="hostileEntityId"/>, taken across every operator on
    /// <paramref name="observingFaction"/>'s own <see cref="OperatorState.ContactMemory"/> —
    /// "the assaulting faction's own best contact tier for it", design
    /// section 6. An operator with no <see cref="ContactMemoryEntry"/> for
    /// <paramref name="hostileEntityId"/> at all contributes nothing; an
    /// operator on a different faction is skipped entirely. Returns
    /// <see cref="ContactTier.Unknown"/> when no observer has any memory of
    /// the hostile, matching <see cref="ContactMemory.Update"/>'s own rule
    /// that <see cref="ContactTier.Unknown"/> is never written into a stored
    /// entry — an absent entry <em>is</em> "unknown".
    /// </summary>
    internal static ContactTier GetBestContactTier(
        ImmutableArray<OperatorState> operators,
        int observingFaction,
        ulong hostileEntityId)
    {
        var best = ContactTier.Unknown;
        if (operators.IsDefaultOrEmpty)
        {
            return best;
        }

        foreach (var observer in operators)
        {
            if (observer.Faction != observingFaction || observer.ContactMemory.IsDefaultOrEmpty)
            {
                continue;
            }

            foreach (var entry in observer.ContactMemory)
            {
                if (entry.EnemyEntityId != hostileEntityId)
                {
                    continue;
                }

                var tier = (ContactTier)entry.ContactTier;
                if (tier > best)
                {
                    best = tier;
                }
            }
        }

        return best;
    }

    /// <summary>
    /// Maps a raw <see cref="ContactMemoryEntry.ContactTier"/> value straight
    /// to <see cref="ContactAppearance"/>. Takes the raw <see langword="int"/>
    /// rather than the enum so a caller already holding
    /// <see cref="GetBestContactTier(ImmutableArray{OperatorState}, int, ulong)"/>'s
    /// result, or a raw field read directly off a <see cref="ContactMemoryEntry"/>,
    /// need not round-trip through the enum first.
    /// </summary>
    internal static ContactAppearance ResolveHostileAppearance(int bestContactTierRaw) =>
        (ContactTier)bestContactTierRaw switch
        {
            ContactTier.Identified => ContactAppearance.Identified,
            ContactTier.QuestionMark => ContactAppearance.Unknown,
            _ => ContactAppearance.Hidden,
        };

    /// <summary>
    /// <see cref="ResolveHostileAppearance(int)"/> composed with
    /// <see cref="GetBestContactTier"/> — the one call site
    /// <c>SandataGame.DrawOperatorsAndFireCones</c> needs for a living hostile
    /// operator.
    /// </summary>
    internal static ContactAppearance ResolveHostileAppearance(
        ImmutableArray<OperatorState> operators,
        int observingFaction,
        ulong hostileEntityId) =>
        ResolveHostileAppearance(
            (int)GetBestContactTier(operators, observingFaction, hostileEntityId));
}
