using System.Collections.Immutable;
using Sandata.Core.Determinism;

namespace Sandata.Core.Events;

/// <summary>
/// Sandata's authoritative, ordered mission event feed and its running event
/// hash. Task 76 of Sandata's scaffold plan, corrected by
/// this plan's own 2026-08-08 wave-11 audit: "The event hash is FNV-1a over
/// the ordered event stream, accumulated as events are emitted, so that the
/// 200-event retention cap cannot truncate it," and "A bounded feed and a
/// complete hash are different things and the cap belongs only to the
/// feed." <see cref="Events"/> is that bounded feed; <see cref="Hash"/> is
/// the complete, uncapped accumulator.
/// </summary>
/// <remarks>
/// CLAUDE.md section 5: "The battle event feed retains at most
/// 200 ordered events" — Hukbo's own rule, applied here to Sandata's mission
/// event feed via <see cref="MaxRetainedEvents"/>. This type holds an
/// <see cref="ImmutableArray{T}"/> member, so — exactly like
/// <c>Sandata.Core.Orders.OrderQueue</c> and
/// <c>Sandata.Core.Simulation.MissionState</c> — it overrides
/// <c>Equals</c>/<c>GetHashCode</c> rather than relying on the record
/// default, which would compare the backing array by reference.
/// </remarks>
public sealed record MissionEventFeed
{
    /// <summary>
    /// The retained window's cap. Does not bound <see cref="Hash"/>, which
    /// accumulates every event ever appended regardless of how many have
    /// since been dropped from <see cref="Events"/>.
    /// </summary>
    public const int MaxRetainedEvents = 200;

    /// <summary>The value a new mission starts from: no events, hash at the FNV-1a offset basis.</summary>
    public static readonly MissionEventFeed Empty = new();

    private MissionEventFeed()
    {
    }

    /// <summary>
    /// The most recent <see cref="MaxRetainedEvents"/> events, oldest first,
    /// newest last. Not itself hashed or snapshotted as a distinct field —
    /// it is fully determined by <see cref="Hash"/>'s inputs plus this cap,
    /// but is carried verbatim (not recomputed) so resume never has to
    /// replay a truncated history to repopulate it.
    /// </summary>
    public ImmutableArray<MissionEvent> Events { get; private init; } = ImmutableArray<MissionEvent>.Empty;

    /// <summary>
    /// FNV-1a over every event ever appended, in emission order, folded
    /// through <see cref="SandataHash"/> exactly as
    /// <c>Sandata.Core.Determinism.SandataStateHasher</c> folds the state
    /// hash's own fields — never truncated by <see cref="MaxRetainedEvents"/>.
    /// This is Sandata's event hash: design section 4's "Event hash. FNV-1a
    /// over the ordered authoritative event stream," independent of the
    /// state hash on purpose (that same section: "a bug that moves state
    /// without emitting an event moves one and not the other").
    /// </summary>
    public ulong Hash { get; private init; } = SandataHash.Begin();

    /// <summary>
    /// Appends one event: folds it into <see cref="Hash"/> first, then
    /// appends it to <see cref="Events"/> and drops the single oldest entry
    /// if the retained window would exceed <see cref="MaxRetainedEvents"/>.
    /// Folding before dropping is what keeps the cap from ever truncating
    /// the returned <see cref="Hash"/>.
    /// </summary>
    public MissionEventFeed Append(MissionEvent mostRecentEvent)
    {
        var hash = Hash;
        FoldEvent(ref hash, mostRecentEvent);

        var events = Events.Add(mostRecentEvent);
        if (events.Length > MaxRetainedEvents)
        {
            events = events.RemoveAt(0);
        }

        return new MissionEventFeed { Events = events, Hash = hash };
    }

    private static void FoldEvent(ref ulong hash, MissionEvent missionEvent)
    {
        SandataHash.Fold(ref hash, missionEvent.Sequence);
        SandataHash.Fold(ref hash, missionEvent.Tick);
        SandataHash.Fold(ref hash, (long)missionEvent.Kind);
        SandataHash.Fold(ref hash, missionEvent.SubjectId);
        SandataHash.Fold(ref hash, missionEvent.ReasonCode);
    }

    public bool Equals(MissionEventFeed? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Hash == other.Hash && EventsSpan.SequenceEqual(other.EventsSpan);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Hash);
        foreach (var missionEvent in EventsSpan)
        {
            hash.Add(missionEvent);
        }

        return hash.ToHashCode();
    }

    private ReadOnlySpan<MissionEvent> EventsSpan =>
        Events.IsDefault ? ReadOnlySpan<MissionEvent>.Empty : Events.AsSpan();
}
