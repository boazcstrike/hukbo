namespace Hukbo.Client.Rendering;

/// <summary>
/// One live ranged shot as held by
/// <see cref="Hukbo.Client.Presentation.ProjectileFlightSystem"/>: its
/// origin, its target, when it launched, how long its flight lasts, and its
/// current screen-space endpoint, interpolated between origin and
/// destination for whatever tick the system was last ingested at.
/// Presentation only — it mirrors, but never drives, the flight the
/// simulation itself resolves, and it decides no targeting, no damage, and
/// no outcome.
/// </summary>
/// <param name="Sequence">
/// The originating <c>Release</c> event's sequence. Used only to break a tie
/// when the store is full and two live entries expire on the same tick.
/// </param>
/// <param name="TargetEntityId">
/// The warrior this shot is bound for, or <c>null</c> for a shot released
/// with no recorded target. Carried for identity only — the destination
/// coordinates below are captured once, at launch, and never re-resolved
/// against the target's later movement, the same way
/// <c>HitEffect</c> and <c>ClashEffect</c> capture a position once rather
/// than tracking it.
/// </param>
/// <param name="LaunchTick">
/// The tick <see cref="Hukbo.Client.Presentation.ProjectileFlightSystem.Ingest"/>
/// was called with when this entry was created.
/// </param>
/// <param name="FlightTicks">
/// How many ticks the shot stays live, taken from the originating
/// <c>Release</c> event's <c>Value</c>. The entry is live for ticks
/// <c>LaunchTick</c> through <c>LaunchTick + FlightTicks - 1</c> inclusive,
/// and gone from tick <c>LaunchTick + FlightTicks</c> onward.
/// </param>
/// <param name="OriginXRaw">
/// Raw fixed-point x of the launch point — the source warrior's position at
/// the moment of release. Fixed for the entry's whole life.
/// </param>
/// <param name="OriginYRaw">The <c>y</c> counterpart of <see cref="OriginXRaw"/>.</param>
/// <param name="DestinationXRaw">
/// Raw fixed-point x of the aim point, captured once at launch from the
/// target's position that tick, or equal to <see cref="OriginXRaw"/> when no
/// target was resolved. Fixed for the entry's whole life.
/// </param>
/// <param name="DestinationYRaw">The <c>y</c> counterpart of <see cref="DestinationXRaw"/>.</param>
/// <param name="CurrentXRaw">
/// Raw fixed-point x of the shot's interpolated position as of the last
/// ingested tick — <see cref="OriginXRaw"/> at launch, approaching
/// <see cref="DestinationXRaw"/> as the flight nears its last live tick, and
/// linear between the two in between. This, paired with
/// <see cref="CurrentYRaw"/>, is the screen-space endpoint a renderer reads.
/// </param>
/// <param name="CurrentYRaw">The <c>y</c> counterpart of <see cref="CurrentXRaw"/>.</param>
internal readonly record struct ProjectileFlight(
    long Sequence,
    ulong? TargetEntityId,
    long LaunchTick,
    int FlightTicks,
    int OriginXRaw,
    int OriginYRaw,
    int DestinationXRaw,
    int DestinationYRaw,
    int CurrentXRaw,
    int CurrentYRaw);
