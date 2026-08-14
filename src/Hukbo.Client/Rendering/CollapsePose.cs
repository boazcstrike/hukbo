namespace Hukbo.Client.Rendering;

/// <summary>
/// The death collapse's whole curve, as a pure function of one age in seconds
/// and one final angle (the 2026-08-14 death-collapse design, section 3). No
/// MonoGame dependency, no state, no clock: the clock lives in
/// <c>DeathCollapseSystem</c> and hands its age here.
/// </summary>
/// <remarks>
/// <para>
/// The collapse is the second of a death's three phases. The first is the
/// lethal hold, which <c>DefenderReactionSystem</c> already owns and which this
/// type knows nothing about; the third is the prone body, which is this curve
/// evaluated past its own end and which therefore needs no code of its own.
/// </para>
/// <para>
/// Every constant here is PROVISIONAL presentation choreography (CLAUDE.md
/// section 7). None of it is a measured historical quantity, none of it reaches
/// the simulation, and none of it is stored, hashed, or snapshotted.
/// </para>
/// </remarks>
internal static class CollapsePose
{
    /// <summary>
    /// How long a body takes to go from upright to still, in seconds.
    /// PROVISIONAL: long enough that the fall is a motion a spectator watches
    /// rather than a cut, short enough that it finishes well inside the time a
    /// nearby fight takes to resolve its next blow.
    /// </summary>
    public const float CollapseSeconds = 0.45f;

    /// <summary>
    /// The share of <see cref="CollapseSeconds"/> at which the body reaches the
    /// ground. Before it the body is falling; after it the body is settling
    /// back from the overshoot. PROVISIONAL.
    /// </summary>
    public const float ImpactShare = 0.82f;

    /// <summary>
    /// The final angle's magnitude before per-warrior jitter: a quarter turn,
    /// which is what puts the body flat on the ground rather than leaning.
    /// This one is not a tuning knob — design section 1 is explicit that a
    /// partial turn is the defect being fixed, not an acceptable result.
    /// </summary>
    public const float ProneRotationRadians = MathF.PI / 2f;

    /// <summary>
    /// How far past the final angle the body swings at the moment of impact,
    /// before settling back onto it. PROVISIONAL, and deliberately small:
    /// about 5.7 degrees reads as mass arriving on the ground, while an
    /// overshoot large enough to read as a bounce would also be large enough to
    /// read, at the peak, as a body lying at an angle.
    /// </summary>
    public const float SettleOvershootRadians = 0.10f;

    /// <summary>
    /// The largest per-warrior deviation from <see cref="ProneRotationRadians"/>
    /// in the final resting angle. PROVISIONAL: about 8 degrees, which is
    /// inside what a body on uneven ground looks like and outside what reads as
    /// a lean. Without it a field of dead reads as one body stamped repeatedly.
    /// </summary>
    public const float FallJitterRadians = 0.14f;

    /// <summary>
    /// The rotation a collapsing or fallen body is drawn at.
    /// </summary>
    /// <param name="ageSeconds">
    /// Seconds since the collapse began — that is, since the first frame the
    /// agent resolved to <see cref="PawnVisualState.Dead"/>, not since it
    /// stopped being alive. The two differ by the lethal hold.
    /// </param>
    /// <param name="finalRotationRadians">
    /// The angle the body comes to rest at, from
    /// <see cref="ResolveFinalRotation"/>. Carried in rather than recomputed so
    /// that a body cannot change which way it is lying between frames.
    /// </param>
    /// <returns>
    /// Zero at age zero, exactly <paramref name="finalRotationRadians"/> at and
    /// after <see cref="CollapseSeconds"/>, and never more than
    /// <see cref="SettleOvershootRadians"/> beyond it in between.
    /// </returns>
    public static float Resolve(float ageSeconds, float finalRotationRadians)
    {
        if (!float.IsFinite(ageSeconds) || ageSeconds <= 0f)
        {
            return 0f;
        }

        var progress = ageSeconds / CollapseSeconds;
        if (progress >= 1f)
        {
            return finalRotationRadians;
        }

        // The sign travels with the final angle rather than being applied to a
        // magnitude at the end, so the two segments below only ever have to
        // think about "how far along the fall is".
        var overshootRotation = finalRotationRadians +
            (MathF.Sign(finalRotationRadians) * SettleOvershootRadians);

        if (progress < ImpactShare)
        {
            // Falling. Eased in as a square, because a body accelerates under
            // its own weight; an ease-out here would read as a controlled
            // lie-down rather than a collapse.
            var fall = progress / ImpactShare;
            return overshootRotation * fall * fall;
        }

        // Settling. Eased out from the overshoot back onto the final angle, so
        // the last few degrees decelerate into stillness instead of stopping
        // dead.
        var settle = (progress - ImpactShare) / (1f - ImpactShare);
        var eased = 1f - ((1f - settle) * (1f - settle));
        return overshootRotation +
            ((finalRotationRadians - overshootRotation) * eased);
    }

    /// <summary>
    /// The angle one warrior's body comes to rest at: a quarter turn in the
    /// direction the killing blow pushed it, plus a bounded per-warrior jitter.
    /// </summary>
    /// <param name="fallsRight">
    /// Whether the body topples toward increasing screen X. The caller derives
    /// this from the killing blow's screen direction, so a warrior struck from
    /// its left falls to its right.
    /// </param>
    /// <param name="entityId">
    /// The warrior, as the identity input to the jitter stream. Same warrior,
    /// same jitter, for the whole battle and across runs.
    /// </param>
    public static float ResolveFinalRotation(bool fallsRight, ulong entityId)
    {
        // Presentation.PresentationSalts.DeathFallJitterSalt. Inlined as a
        // literal for the same reason PawnAppearanceFactory inlines its three:
        // the registry exists so the distinctness test can see every salt
        // beside every other, not so consumers take a dependency on it.
        var mixed = Mix(entityId ^ 0x7F2B95E0C4A16D38UL);

        // A signed fraction of the jitter budget, from a byte of the mixed
        // value: 0..255 mapped onto -1..+1 and scaled. Modulo-free so the
        // distribution does not lean, and derived from one byte so the rest of
        // the stream stays available if a later channel wants it.
        var offset = (((int)(mixed & 0xFFUL) - 128) / 128f) * FallJitterRadians;
        var magnitude = ProneRotationRadians + offset;
        return fallsRight ? magnitude : -magnitude;
    }

    /// <summary>
    /// SplitMix64's finaliser, the same one
    /// <c>PawnAppearanceFactory.Mix</c> uses, duplicated here for the same
    /// reason it is private there: a presentation variant stream is not a
    /// simulation stream and must never share a code path with one.
    /// </summary>
    private static ulong Mix(ulong value)
    {
        unchecked
        {
            value += 0x9E3779B97F4A7C15UL;
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }
    }
}
