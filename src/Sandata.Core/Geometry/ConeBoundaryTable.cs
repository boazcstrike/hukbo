using Sandata.Core.Mathematics;

namespace Sandata.Core.Geometry;

/// <summary>
/// The pinned integer boundary-vector table that <see cref="VisionCone"/>
/// reads its two edge directions from.
/// </summary>
/// <remarks>
/// <para>
/// This table exists because the direction vectors already sitting in
/// <c>Hukbo.Core</c>'s <c>FacingRules</c> cannot be trusted for a cone test.
/// <c>FacingRules</c>' sixteen sector vectors are scaled to 1024 but are not
/// unit length: for example 946² + 392² is 1,048,580 against 1024²'s
/// 1,048,576 (off by 4), while 724² doubled is 1,048,352 (off by 224). The
/// error is a different size for every sector. A cone test written as a
/// cosine comparison — <c>dot(direction, boundary) &gt;= cos(halfWidth) *
/// |direction| * |boundary|</c> — implicitly assumes <c>|boundary|</c> is the
/// same constant for every sector, and silently produces a different cone
/// shape depending on which way the unit faces once that assumption is
/// false. It would also need to multiply two squared magnitudes together to
/// normalise, which overflows <see cref="long"/> at this game's map scale.
/// </para>
/// <para>
/// This table avoids the whole problem by never normalising anything and
/// never taking a dot product. <see cref="BoundaryVector"/> returns a
/// direction vector whose two components come from <c>Trig.Cos</c> and
/// <c>Trig.Sin</c>. Reconciled 2026-08-07 by task 56: this file originally
/// declared its own independently pinned copy of <c>Trig</c>'s 257-entry
/// quarter-wave sine table, on the theory that <see cref="VisionCone"/>'s
/// boundary vectors should never depend on, or be perturbed by, a change to
/// <c>Trig</c>. Before folding the two together, the two tables were checked
/// element-for-element and found identical — expected, since both were
/// derived independently from the same mathematical definition of sine —
/// so the fold changes no boundary vector this type returns. Nothing
/// downstream of this table ever needs the vector's length, only its
/// direction, so an interpolation rounding error of a few raw units changes
/// nothing about which side of a boundary a point falls on except in a band
/// of a handful of raw units either side of the boundary itself, which is
/// exactly the region <see cref="VisionCone"/>'s own boundary tests are
/// written to probe.
/// </para>
/// </remarks>
public static class ConeBoundaryTable
{
    /// <summary>
    /// The direction vector for <paramref name="angle"/>, as <c>(cosine,
    /// sine)</c> at scale 65,536. Angle zero points along <c>(+1, 0)</c>;
    /// increasing <paramref name="angle"/> rotates toward <c>(0, +1)</c> at
    /// the quarter turn, matching this project's <c>+Y</c>-is-screen-down
    /// convention. Only the direction this vector points is a stable
    /// contract — <see cref="VisionCone"/> never reads its magnitude, so an
    /// interpolation rounding error of a few raw units never changes which
    /// side of a boundary a point falls on outside a band of a handful of
    /// raw units either side of the boundary itself.
    /// </summary>
    public static (long X, long Y) BoundaryVector(Bam16 angle) =>
        (Trig.Cos(angle.Raw), Trig.Sin(angle.Raw));
}
