using Hukbo.Core.Combat;

namespace Hukbo.Client.Presentation;

/// <summary>
/// The shared shape of every aged blood record, so the fixed-capacity buffers
/// in <see cref="BloodEffectSystem"/> can share one overflow policy and one
/// compaction loop instead of repeating them three times.
/// </summary>
/// <remarks>
/// The self-referencing type parameter keeps <see cref="WithAge"/>
/// allocation-free: the constraint resolves to a concrete struct, so the call
/// is made without boxing on the ingest and advance paths.
/// </remarks>
/// <typeparam name="TSelf">The implementing record struct.</typeparam>
internal interface IBloodEffect<TSelf>
    where TSelf : struct, IBloodEffect<TSelf>
{
    /// <summary>
    /// The authoritative event sequence that produced this record. Unique
    /// across a whole match, so it is a total order for tie-breaking.
    /// </summary>
    long Sequence { get; }

    /// <summary>Unscaled presentation seconds elapsed since creation.</summary>
    float AgeSeconds { get; }

    /// <summary>The age at which this record is dropped from its buffer.</summary>
    float LifetimeSeconds { get; }

    /// <summary>Returns a copy aged to <paramref name="ageSeconds"/>.</summary>
    TSelf WithAge(float ageSeconds);
}

/// <summary>
/// One directional spray produced by a single accepted attack. Every field is
/// either copied straight from the authoritative <c>Attack</c> event or derived
/// from the end-of-tick agent snapshot, so a burst is a pure function of the
/// simulation and never of the wall clock.
/// </summary>
internal readonly record struct BloodBurst(
    long Sequence,
    ulong SourceEntityId,
    ulong TargetEntityId,
    int XRaw,
    int YRaw,
    float DirectionX,
    float DirectionY,
    WeaponId Weapon,
    BodyPart HitLocation,
    float SeverityRatio,
    bool IsLethal,
    float AgeSeconds) : IBloodEffect<BloodBurst>
{
    private const float OrdinaryLifetimeSeconds = 0.26f;

    // PROVISIONAL legibility tuning per CLAUDE.md section 7, raised
    // 2026-08-13 (docs/plans/2026-08-13-lethal-blow-legibility-design.md) so
    // the burst still has a body to draw over for the widened lethal hold.
    private const float LethalLifetimeSeconds = 0.62f;

    public float LifetimeSeconds =>
        IsLethal ? LethalLifetimeSeconds : OrdinaryLifetimeSeconds;

    public BloodBurst WithAge(float ageSeconds) =>
        this with { AgeSeconds = ageSeconds };
}

/// <summary>
/// A stain left on the arena surface where a blow landed. Ground marks outlive
/// the burst that produced them so a spectator can still read where the
/// fighting was heaviest after the warriors have moved on.
/// </summary>
/// <remarks>
/// <paramref name="IsDense"/> is set when the gore intensity is
/// <c>GoreIntensity.Full</c>. It both lengthens the lifetime and adds blots at
/// draw time, which is the whole visible difference between the Full and
/// Stylized ground-mark presentation.
/// </remarks>
internal readonly record struct GroundMark(
    long Sequence,
    int XRaw,
    int YRaw,
    bool IsLethal,
    bool IsDense,
    float AgeSeconds) : IBloodEffect<GroundMark>
{
    private const float OrdinaryLifetimeSeconds = 2.6f;

    // PROVISIONAL legibility tuning per CLAUDE.md section 7, raised
    // 2026-08-13 (docs/plans/2026-08-13-lethal-blow-legibility-design.md) so
    // a killing blow's stain reads as heavier and outlives the fight longer.
    private const float LethalLifetimeSeconds = 8f;
    private const float DenseLifetimeMultiplier = 1.75f;

    public float LifetimeSeconds =>
        (IsLethal ? LethalLifetimeSeconds : OrdinaryLifetimeSeconds) *
        (IsDense ? DenseLifetimeMultiplier : 1f);

    public GroundMark WithAge(float ageSeconds) =>
        this with { AgeSeconds = ageSeconds };
}

/// <summary>
/// A sustained jet drawn on a lethal blow. Created only at
/// <c>GoreIntensity.Full</c>. Until 2026-08-13 the Stylized default never
/// produced one, because a sustained spurt carries an anatomical reading the
/// evidence does not support. That restraint was overridden on 2026-08-13
/// (docs/plans/2026-08-13-lethal-blow-legibility-design.md) on the explicit
/// request of the person the presentation is for, after they reported that a
/// killing blow did not read as clearly heavier than an ordinary one. The
/// override is provisional legibility tuning per <c>CLAUDE.md</c> section 7,
/// not an evidentiary claim, and it is reversible: it only moved
/// <c>ClientSettingsStore.DefaultGoreIntensity</c> to <c>Full</c>, so a
/// spectator who prefers the old presentation still gets it by selecting
/// <c>Stylized</c> in the menu, and reverting the default is a one-line
/// change.
/// </summary>
internal readonly record struct LethalSpurt(
    long Sequence,
    ulong SourceEntityId,
    ulong TargetEntityId,
    int XRaw,
    int YRaw,
    float DirectionX,
    float DirectionY,
    float SeverityRatio,
    float AgeSeconds) : IBloodEffect<LethalSpurt>
{
    // PROVISIONAL legibility tuning per CLAUDE.md section 7, raised
    // 2026-08-13 (docs/plans/2026-08-13-lethal-blow-legibility-design.md) to
    // match the widened lethal hold and pulse.
    private const float SpurtLifetimeSeconds = 1.1f;

    public float LifetimeSeconds => SpurtLifetimeSeconds;

    public LethalSpurt WithAge(float ageSeconds) =>
        this with { AgeSeconds = ageSeconds };
}
