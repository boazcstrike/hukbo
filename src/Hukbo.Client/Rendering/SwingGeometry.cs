using Hukbo.Client.Presentation;
using Hukbo.Core.Combat;

namespace Hukbo.Client.Rendering;

/// <summary>
/// The four phases of one swing, in the order they are visited.
/// </summary>
internal enum SwingPhase
{
    /// <summary>The weapon pulls back outside the body silhouette.</summary>
    Anticipation = 0,

    /// <summary>The arc sweeps through.</summary>
    Strike = 1,

    /// <summary>Held at full extension; where the clash reads.</summary>
    ImpactHold = 2,

    /// <summary>Return to neutral.</summary>
    Recovery = 3,
}

/// <summary>
/// The pose one swing puts a pawn in at one moment. A neutral pose, which is
/// <c>default</c>, is a pawn standing as it does today.
/// </summary>
/// <param name="Phase">Which phase the swing is in.</param>
/// <param name="PhaseProgress">Progress through that phase, zero to one.</param>
/// <param name="WeaponAngleRadians">
/// Rotation of the weapon line about the grip, with the swing direction
/// already applied, so a warrior striking left rotates opposite one striking
/// right.
/// </param>
/// <param name="TorsoLeanX">
/// Torso offset in pawn units, x component. The lean runs along the swing
/// direction and is already multiplied by it here, because this record carries
/// no direction of its own for a caller to apply.
/// </param>
/// <param name="TorsoLeanY">Torso offset in pawn units, y component.</param>
/// <param name="ExtensionRatio">
/// How far along the reach the weapon tip has travelled. A landed blow stops
/// on the target, an evaded blow follows through past it, and the three
/// contact outcomes recoil.
/// </param>
/// <param name="TrailStrength">
/// Strength of the arc trail, zero when no trail is drawn.
/// </param>
internal readonly record struct SwingPose(
    SwingPhase Phase,
    float PhaseProgress,
    float WeaponAngleRadians,
    float TorsoLeanX,
    float TorsoLeanY,
    float ExtensionRatio,
    float TrailStrength);

/// <summary>
/// Pure mapping from one in-flight swing to a phase and a pose.
/// </summary>
/// <remarks>
/// <para>
/// Every value is a keyframe interpolated linearly between phase boundaries,
/// so the pose is continuous by construction rather than by assertion.
/// </para>
/// <para>
/// The impact hold carries three pose branches, one per pose class: the three
/// contact outcomes recoil, a landed blow stops on the target, and a void
/// follows through past it. Without the third branch a landed blow and a void
/// are the same motion, and the animation cannot separate them at all.
/// </para>
/// <para>
/// The swing direction is folded into the returned pose, because
/// <see cref="SwingPose"/> carries no direction of its own and a draw loop
/// could not otherwise recover which way the warrior struck.
/// </para>
/// </remarks>
internal static class SwingGeometry
{
    /// <summary>
    /// PROVISIONAL. Share of the total duration spent pulling the weapon back
    /// outside the body silhouette. Taken from the design timing table, which
    /// budgets the swing against the attack cooldown rather than against the
    /// animation research figure.
    /// </summary>
    public const float AnticipationShare = 0.36f;

    /// <summary>PROVISIONAL. Share spent sweeping the arc through.</summary>
    public const float StrikeShare = 0.20f;

    /// <summary>
    /// PROVISIONAL. Share held at full extension, where the clash reads.
    /// </summary>
    public const float ImpactHoldShare = 0.20f;

    /// <summary>PROVISIONAL. Share spent returning to neutral.</summary>
    public const float RecoveryShare = 0.24f;

    private const float AnticipationEnd = AnticipationShare;
    private const float StrikeEnd = AnticipationEnd + StrikeShare;
    private const float ImpactHoldEnd = StrikeEnd + ImpactHoldShare;

    // PROVISIONAL. Weapon rotation about the grip, in radians, before the
    // swing direction is applied. Negative is pulled back behind the warrior
    // and positive is swung through toward the target.
    private const float PullBackAngleRadians = -0.85f;
    private const float ContactAngleRadians = 0.80f;
    private const float RecoilAngleRadians = 0.30f;
    private const float FollowThroughAngleRadians = 1.15f;

    // PROVISIONAL. Travel of the weapon tip along the reach, as a share of the
    // neutral reach added on top of it. A landed blow stops at the contact
    // value, a contact outcome falls back from it, and a void passes it.
    private const float PullBackExtension = -0.22f;
    private const float ContactExtension = 1f;
    private const float RecoilExtension = 0.55f;
    private const float FollowThroughExtension = 1.3f;

    // PROVISIONAL. Torso offset along the swing direction, in pawn units
    // before the apparent scale is applied.
    private const float PullBackLean = -0.9f;
    private const float ContactLean = 1.6f;
    private const float RecoilLean = 0.5f;
    private const float FollowThroughLean = 2.3f;

    /// <summary>
    /// Resolves which phase a swing is in from its progress through the total
    /// duration.
    /// </summary>
    /// <param name="progress">Progress, zero to one.</param>
    public static SwingPhase ResolvePhase(float progress)
    {
        if (!float.IsFinite(progress) || progress < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(progress));
        }

        return progress switch
        {
            < AnticipationEnd => SwingPhase.Anticipation,
            < StrikeEnd => SwingPhase.Strike,
            < ImpactHoldEnd => SwingPhase.ImpactHold,
            _ => SwingPhase.Recovery,
        };
    }

    /// <summary>
    /// Resolves the pose one swing puts a pawn in.
    /// </summary>
    public static SwingPose ResolvePose(SwingAnimation swing)
    {
        var progress = swing.Progress;
        var phase = ResolvePhase(progress);
        var phaseProgress = ResolvePhaseProgress(progress, phase);
        var hold = ResolveHoldKeyframe(swing.Resolution);

        var (angle, extension, lean, trail) = phase switch
        {
            SwingPhase.Anticipation => (
                Lerp(0f, PullBackAngleRadians, phaseProgress),
                Lerp(0f, PullBackExtension, phaseProgress),
                Lerp(0f, PullBackLean, phaseProgress),
                0f),
            SwingPhase.Strike => (
                Lerp(PullBackAngleRadians, ContactAngleRadians, phaseProgress),
                Lerp(PullBackExtension, ContactExtension, phaseProgress),
                Lerp(PullBackLean, ContactLean, phaseProgress),
                phaseProgress),
            SwingPhase.ImpactHold => (
                Lerp(ContactAngleRadians, hold.AngleRadians, phaseProgress),
                Lerp(ContactExtension, hold.Extension, phaseProgress),
                Lerp(ContactLean, hold.Lean, phaseProgress),
                1f),
            SwingPhase.Recovery => (
                Lerp(hold.AngleRadians, 0f, phaseProgress),
                Lerp(hold.Extension, 0f, phaseProgress),
                Lerp(hold.Lean, 0f, phaseProgress),
                1f - phaseProgress),
            _ => throw new ArgumentOutOfRangeException(
                nameof(swing),
                phase,
                null),
        };

        // The weapon line is drawn pointing forward and up, so a warrior
        // striking to the left mirrors the rotation rather than acquiring a
        // mirrored silhouette. A swing with no facing, which is two agents
        // sharing a position, keeps the forward sign and leans nowhere.
        var facing = swing.DirectionX >= 0f ? 1f : -1f;

        return new SwingPose(
            phase,
            phaseProgress,
            angle * facing,
            lean * swing.DirectionX,
            lean * swing.DirectionY,
            extension,
            trail);
    }

    private static float ResolvePhaseProgress(float progress, SwingPhase phase)
    {
        var (start, share) = phase switch
        {
            SwingPhase.Anticipation => (0f, AnticipationShare),
            SwingPhase.Strike => (AnticipationEnd, StrikeShare),
            SwingPhase.ImpactHold => (StrikeEnd, ImpactHoldShare),
            SwingPhase.Recovery => (ImpactHoldEnd, RecoveryShare),
            _ => throw new ArgumentOutOfRangeException(
                nameof(phase),
                phase,
                null),
        };

        return Math.Clamp((progress - start) / share, 0f, 1f);
    }

    /// <summary>
    /// The three pose branches. A landed blow holds the contact keyframe
    /// unchanged, which is what "stops on the target" means here.
    /// </summary>
    private static HoldKeyframe ResolveHoldKeyframe(
        AttackResolution resolution) =>
        resolution switch
        {
            AttackResolution.Landed => new HoldKeyframe(
                ContactAngleRadians,
                ContactExtension,
                ContactLean),
            AttackResolution.ShieldBlocked or
            AttackResolution.Parried or
            AttackResolution.Deflected => new HoldKeyframe(
                RecoilAngleRadians,
                RecoilExtension,
                RecoilLean),
            AttackResolution.Evaded => new HoldKeyframe(
                FollowThroughAngleRadians,
                FollowThroughExtension,
                FollowThroughLean),
            _ => throw new ArgumentOutOfRangeException(
                nameof(resolution),
                resolution,
                null),
        };

    private static float Lerp(float from, float to, float amount) =>
        from + ((to - from) * amount);

    private readonly record struct HoldKeyframe(
        float AngleRadians,
        float Extension,
        float Lean);
}
