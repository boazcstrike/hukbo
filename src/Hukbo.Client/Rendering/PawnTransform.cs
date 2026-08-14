using Microsoft.Xna.Framework;

namespace Hukbo.Client.Rendering;

/// <summary>
/// A rigid transform of the screen plane — one rotation and one translation,
/// applied as <c>p → rot(θ)·p + t</c> — that <c>PawnRenderer</c> applies to
/// every quad it submits for one pawn (the 2026-08-14 death-collapse design,
/// section 4).
/// </summary>
/// <remarks>
/// <para>
/// Stored as an angle plus a translation rather than as an angle plus a pivot,
/// which is the shape <c>PawnRenderer.DrawRotatedBlock</c> already took for the
/// shield's fixed posture rotation. The reason is composition: two rotations
/// about two <em>different</em> pivots compose into exactly one value of this
/// shape, and nothing else on a pawn needs that except the one case that does —
/// a shield on a collapsing body, rotated by its posture angle about its own
/// centre and then by the collapse angle about the foot anchor. An
/// angle-and-pivot pair cannot hold that result, so the pair would have to be
/// carried and re-applied twice at every shield sub-element.
/// </para>
/// <para>
/// <see cref="Identity"/> is the value every living pawn draws under, and
/// <see cref="IsIdentity"/> is what lets <c>PawnRenderer</c> keep taking the
/// plain axis-aligned <c>SpriteBatch.Draw(Texture2D, Rectangle, Color)</c>
/// overload for it. That is not an optimisation: it is what makes a living
/// pawn's drawn pixels identical to what they were before this type existed,
/// rather than identical up to the sub-pixel rounding the rotation overload's
/// floating-point origin introduces on an odd width or height.
/// </para>
/// </remarks>
/// <param name="Radians">
/// The rotation, applied about the plane's origin before
/// <paramref name="Translation"/>. This is also the angle passed straight to
/// <c>SpriteBatch.Draw</c>'s own rotation parameter, because a quad's own
/// orientation is unaffected by where the rotation was centred.
/// </param>
/// <param name="Translation">
/// The translation applied after <paramref name="Radians"/>. For a rotation
/// about a pivot this is <c>pivot - rot(θ)·pivot</c>, which
/// <see cref="AboutPivot"/> derives.
/// </param>
internal readonly record struct PawnTransform(float Radians, Vector2 Translation)
{
    /// <summary>
    /// The neutral transform: no rotation, no translation. Every living pawn
    /// draws under this value, and so does every pawn drawn by a caller that
    /// knows nothing about the collapse.
    /// </summary>
    public static PawnTransform Identity => default;

    /// <summary>
    /// Whether this transform moves nothing. Checked against exact zeroes
    /// deliberately: the only values that reach here are
    /// <see cref="Identity"/> itself and results built from a non-zero angle,
    /// so an approximate comparison would buy nothing and would make the
    /// "identical pixels for a living pawn" guarantee depend on a tolerance.
    /// </summary>
    public bool IsIdentity => Radians == 0f && Translation == Vector2.Zero;

    /// <summary>
    /// The transform that rotates the plane by <paramref name="radians"/> about
    /// <paramref name="pivot"/>, leaving that one point where it is.
    /// </summary>
    /// <remarks>
    /// Returns <see cref="Identity"/> exactly for a zero angle, so a caller
    /// that resolves "no collapse" as an angle of zero gets the neutral value
    /// without having to special-case it, and the renderer's axis-aligned fast
    /// path stays reachable.
    /// </remarks>
    public static PawnTransform AboutPivot(Vector2 pivot, float radians)
    {
        if (radians == 0f)
        {
            return Identity;
        }

        var cosine = MathF.Cos(radians);
        var sine = MathF.Sin(radians);
        var rotatedPivot = new Vector2(
            (pivot.X * cosine) - (pivot.Y * sine),
            (pivot.X * sine) + (pivot.Y * cosine));
        return new PawnTransform(radians, pivot - rotatedPivot);
    }

    /// <summary>
    /// The single transform equivalent to applying this one and then
    /// <paramref name="outer"/>.
    /// </summary>
    /// <remarks>
    /// Order matters and reads left to right: <c>inner.Then(outer)</c> applies
    /// <c>inner</c> first. The shield's call site is
    /// <c>AboutPivot(shieldCentre, posture).Then(collapse)</c>, because the
    /// posture rotation is part of how the shield sits on the body and the
    /// collapse then carries the whole body over.
    /// </remarks>
    public PawnTransform Then(PawnTransform outer)
    {
        if (outer.IsIdentity)
        {
            return this;
        }

        if (IsIdentity)
        {
            return outer;
        }

        var cosine = MathF.Cos(outer.Radians);
        var sine = MathF.Sin(outer.Radians);
        var rotatedTranslation = new Vector2(
            (Translation.X * cosine) - (Translation.Y * sine),
            (Translation.X * sine) + (Translation.Y * cosine));
        return new PawnTransform(
            Radians + outer.Radians,
            rotatedTranslation + outer.Translation);
    }

    /// <summary>
    /// This transform applied to one point.
    /// </summary>
    public Vector2 Apply(Vector2 point)
    {
        if (IsIdentity)
        {
            return point;
        }

        var cosine = MathF.Cos(Radians);
        var sine = MathF.Sin(Radians);
        return new Vector2(
            (point.X * cosine) - (point.Y * sine),
            (point.X * sine) + (point.Y * cosine)) + Translation;
    }
}
