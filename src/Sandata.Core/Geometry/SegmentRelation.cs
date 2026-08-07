namespace Sandata.Core.Geometry;

/// <summary>
/// The result of <see cref="ExactPredicates.ClassifySegments"/> classifying
/// segment A-B against segment C-D using exact integer orientation tests
/// only. Every named result below is a written rule computed from exact
/// integer arithmetic — there is no tolerance and no "close enough" case
/// anywhere in the classification.
/// </summary>
public enum SegmentRelation
{
    /// <summary>
    /// The two segments share no point. The rule: the segments do not
    /// properly cross (see <see cref="Crossing"/>), and for every one of the
    /// four orientation tests that returns zero — meaning an endpoint of one
    /// segment is collinear with the other segment's line — that endpoint
    /// still falls outside the other segment's closed bounding box. If no
    /// orientation test returns zero at all, the segments are simply on
    /// opposite sides of one another and never meet.
    /// </summary>
    Disjoint = 0,

    /// <summary>
    /// The segments cross at exactly one interior point that is not an
    /// endpoint of either segment. The rule: C and D fall strictly on
    /// opposite sides of the directed line A→B, <em>and</em> A and B fall
    /// strictly on opposite sides of the directed line C→D. All four
    /// orientation tests must be non-zero, with each of the two pairs
    /// carrying opposite signs.
    /// </summary>
    Crossing = 1,

    /// <summary>
    /// The segments meet at exactly one point without a proper crossing —
    /// a shared endpoint, a T-junction where one segment's endpoint lands on
    /// the interior of the other, or two collinear segments meeting end to
    /// end. The rule: at least one of the four orientation tests is zero,
    /// the point it names lies within the other segment's closed bounding
    /// box, and the two segments are not collinear over an entire interval
    /// (see <see cref="CollinearOverlap"/>).
    /// </summary>
    Touching = 2,

    /// <summary>
    /// Both segments lie on the same infinite line and their projections
    /// onto that line's own axis overlap in more than one point. The rule:
    /// all four orientation tests are zero, and the closed intervals the two
    /// segments occupy along the shared line intersect in an interval wider
    /// than a single point. A collinear meeting at exactly one point is
    /// <see cref="Touching"/>, not this.
    /// </summary>
    CollinearOverlap = 3,
}
