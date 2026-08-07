using Sandata.Core.Geometry;
using Sandata.Core.Mathematics;

namespace Sandata.Core.Combat;

/// <summary>
/// The directional cover model from design section 9 of
/// docs/plans/2026-08-07-sandata-scaffold-design.md: a flat percentage
/// reduction to damage and to hit chance, gated by whether the incoming
/// shot's direction of travel falls inside the covering object's protected
/// arc, unless the object protects from every direction.
/// </summary>
/// <remarks>
/// <para>
/// <b>Arc containment reuses <see cref="VisionCone.Contains"/> unchanged.</b>
/// A cover object's protected arc and a unit's vision cone are the same
/// shape — a centre direction plus a half-width, tested against a candidate
/// offset by two half-plane cross products rather than a cosine comparison,
/// for exactly the reason <see cref="VisionCone"/>'s own remarks give: the
/// facing sector vectors this game already has are not unit length, so a
/// cosine comparison against them would silently change shape with facing.
/// This type declares no second containment test; it calls
/// <see cref="VisionCone.Contains"/> with a range large enough that the range
/// term never excludes anything, because cover has no maximum range of its
/// own — only a direction.
/// </para>
/// <para>
/// <b>Why a half-width of 32,768 needs no special case.</b> Design section 12
/// records that <c>arcHalfBam</c> of exactly 32,768 means the covering object
/// protects from every direction. <see cref="VisionCone.Contains"/> already
/// produces that result on its own: at half-width 32,768, the left boundary
/// angle <c>centre - 32768</c> and the right boundary angle
/// <c>centre + 32768</c> are the same <see cref="Bam16"/> value, because
/// <c>Bam16</c> is a ring of exactly 65,536 raw units and subtracting or
/// adding half that ring from the same starting point lands on the same spot.
/// Both boundary vectors are therefore identical, so the two cross products
/// <c>crossLeft</c> and <c>crossRight</c> are always equal, and the
/// half-width-32,768 branch of <see cref="VisionCone.Contains"/> — a
/// half-width this wide is never narrower than a quarter turn, so the method
/// takes its "union of two half-planes" path — reduces to
/// <c>cross &gt;= 0 || cross &lt;= 0</c>, which every integer satisfies. No
/// separate all-directions branch is written here; the arithmetic already
/// guarantees it.
/// </para>
/// <para>
/// <b>The direction tested.</b> The offset passed to
/// <see cref="VisionCone.Contains"/> is the vector from the defending
/// operator's own position to the shooter's position — "where the shot is
/// coming from, as seen by the operator standing behind the cover object" —
/// not from the cover object's centre. That is deliberate: design section 9
/// is explicit that two operators standing behind the same physical object
/// can receive different results, and the object's own centroid is identical
/// for both of them, so only each operator's individual position can produce
/// a different verdict. Using the defender's own position also means the
/// flank-and-rear bypass and the arc-boundary cases fall out of the ordinary
/// <see cref="VisionCone.Contains"/> boundary rule with no extra code: a
/// shooter positioned so that this vector lands outside the arc is, by
/// definition, attacking from the flank or the rear of the object's facing,
/// and receives no reduction.
/// </para>
/// <para>
/// <b>The rounding rule, pinned once here for both percentages.</b> Every
/// percentage reduction in this type is computed as
/// <c>value * (100 - percent) / 100</c> using C#'s ordinary integer division,
/// which truncates toward zero. For a non-negative <paramref name="rawValue"/>
/// (damage and hit-chance points are never negative), truncation toward zero
/// is the same as truncating downward, so the survivor is always the floor of
/// the exact rational quotient — for example 25 raw damage at the 50 percent
/// standing reduction survives as <c>25 * 50 / 100 = 1250 / 100 = 12</c>
/// (12.5 truncates to 12), not 13. This is the single rounding rule for the
/// whole type; there is no second rule anywhere that rounds the removed
/// amount instead of the surviving amount, which would give a different
/// answer on every odd raw value.
/// </para>
/// </remarks>
public static class CoverRules
{
    /// <summary>
    /// The percentage of damage and of hit chance removed by standing cover
    /// when the incoming shot's direction falls inside the covering object's
    /// protected arc. Design section 9 names this value directly: "flat 50
    /// percent damage and hit reduction".
    /// </summary>
    public const int StandingCoverReductionPercent = 50;

    /// <summary>
    /// The percentage of damage and of hit chance removed for a crouched
    /// operator affiliated with a cover object, applied regardless of the
    /// direction the shot arrives from.
    /// </summary>
    /// <remarks>
    /// Design section 9 calls crouched cover "near-total protection" and "a
    /// survive-the-magazine button rather than a fighting stance" but does
    /// not pin an exact percentage, so this value is a provisional gameplay
    /// tuning choice, not a measured or historical figure — a later task may
    /// retune it once the weapon-damage numbers it interacts with exist.
    /// Ninety-five was chosen so a crouched operator is not literally
    /// invulnerable (a magazine's worth of hits can still, rarely, kill one)
    /// while remaining far safer than the standing 50 percent case.
    /// </remarks>
    public const int CrouchedCoverReductionPercent = 95;

    /// <summary>
    /// A range large enough that <see cref="VisionCone.Contains"/>'s range
    /// term never excludes a candidate at this game's map scale (the largest
    /// map coordinate square, <c>512 * 512</c> cells at 4 world units per
    /// cell, is nowhere close to this value), used because cover has no
    /// maximum range of its own — only a direction.
    /// </summary>
    private const long UnboundedRangeSquared = long.MaxValue / 4;

    /// <summary>
    /// Whether a shot fired from <paramref name="shooterX"/>,
    /// <paramref name="shooterY"/> at a defender standing at
    /// <paramref name="defenderX"/>, <paramref name="defenderY"/> arrives from
    /// inside the protected arc described by <paramref name="arcCentreBam"/>
    /// and <paramref name="arcHalfBam"/>. This is the standing-cover test:
    /// callers checking a crouched operator do not need it, because crouched
    /// protection is not arc-gated — see
    /// <see cref="CrouchedCoverReductionPercent"/>.
    /// </summary>
    /// <param name="arcCentreBam">
    /// The direction the covering object's protected arc faces.
    /// </param>
    /// <param name="arcHalfBam">
    /// Half the protected arc's total angular width. <c>32768</c> protects
    /// from every direction; see this type's remarks for why that needs no
    /// special case here.
    /// </param>
    /// <param name="shooterX">The shooter's position, X axis.</param>
    /// <param name="shooterY">The shooter's position, Y axis.</param>
    /// <param name="defenderX">The defending operator's position, X axis.</param>
    /// <param name="defenderY">The defending operator's position, Y axis.</param>
    public static bool IsWithinProtectedArc(
        Bam16 arcCentreBam,
        ushort arcHalfBam,
        long shooterX,
        long shooterY,
        long defenderX,
        long defenderY)
    {
        checked
        {
            var dx = shooterX - defenderX;
            var dy = shooterY - defenderY;
            return VisionCone.Contains(arcCentreBam, arcHalfBam, UnboundedRangeSquared, dx, dy);
        }
    }

    /// <summary>
    /// The percentage of damage and of hit chance a shot loses to
    /// <paramref name="cover"/>, for a shot travelling from
    /// <paramref name="shooterX"/>, <paramref name="shooterY"/> to
    /// <paramref name="defenderX"/>, <paramref name="defenderY"/>. Zero when
    /// the defender is not affiliated with any cover object, when the
    /// defender is standing and the shot arrives from outside the object's
    /// protected arc (the flank-and-rear bypass), or, symmetrically, whatever
    /// non-zero percentage applies when the defender is protected.
    /// </summary>
    public static int ReductionPercent(
        CoverState cover,
        long shooterX,
        long shooterY,
        long defenderX,
        long defenderY)
    {
        if (!cover.InCover)
        {
            return 0;
        }

        if (cover.Posture == CoverPosture.Crouched)
        {
            // Crouched protection is not arc-gated: an operator fully tucked
            // behind the object is treated as sheltered from every incoming
            // direction, not only the object's facing. See the class remarks
            // and CrouchedCoverReductionPercent's remarks for why this is a
            // deliberate reading of "near-total protection" rather than the
            // standing rule applied with a bigger number.
            return CrouchedCoverReductionPercent;
        }

        var withinArc = IsWithinProtectedArc(
            cover.ArcCentreBam, cover.ArcHalfBam, shooterX, shooterY, defenderX, defenderY);

        return withinArc ? StandingCoverReductionPercent : 0;
    }

    /// <summary>
    /// Applies a flat percentage reduction to <paramref name="rawValue"/>,
    /// truncating toward zero. See this type's remarks for the rounding rule
    /// and a worked example.
    /// </summary>
    /// <param name="rawValue">
    /// The value before cover is applied. Never negative for the two callers
    /// in this type — damage points and hit-chance percentage points — but
    /// the arithmetic itself has no non-negativity requirement.
    /// </param>
    /// <param name="percent">
    /// The percentage removed, in <c>0..100</c>. Values outside that range
    /// are not rejected by this method; a caller passing one is asking for a
    /// value increase (negative) or a negative result (greater than 100),
    /// neither of which this cover model ever produces on its own.
    /// </param>
    public static int ApplyPercentageReduction(int rawValue, int percent)
    {
        checked
        {
            return (int)((long)rawValue * (100 - percent) / 100);
        }
    }

    /// <summary>
    /// Applies <paramref name="cover"/>'s effect to a raw damage amount for a
    /// shot travelling from the shooter's position to the defender's
    /// position. A convenience composition of <see cref="ReductionPercent"/>
    /// and <see cref="ApplyPercentageReduction"/>.
    /// </summary>
    public static int ApplyToDamage(
        int rawDamage,
        CoverState cover,
        long shooterX,
        long shooterY,
        long defenderX,
        long defenderY) =>
        ApplyPercentageReduction(
            rawDamage, ReductionPercent(cover, shooterX, shooterY, defenderX, defenderY));

    /// <summary>
    /// Applies <paramref name="cover"/>'s effect to a raw hit-chance
    /// percentage for a shot travelling from the shooter's position to the
    /// defender's position. A convenience composition of
    /// <see cref="ReductionPercent"/> and <see cref="ApplyPercentageReduction"/>.
    /// </summary>
    public static int ApplyToHitChancePercent(
        int rawHitChancePercent,
        CoverState cover,
        long shooterX,
        long shooterY,
        long defenderX,
        long defenderY) =>
        ApplyPercentageReduction(
            rawHitChancePercent, ReductionPercent(cover, shooterX, shooterY, defenderX, defenderY));

    /// <summary>
    /// Whether an operator in <paramref name="cover"/>'s current posture can
    /// produce a fire proposal. A crouched operator cannot, regardless of
    /// whether they are affiliated with a cover object — crouching itself
    /// forbids firing, which is what makes it "a survive-the-magazine button
    /// rather than a fighting stance" per design section 9.
    /// </summary>
    public static bool CanProduceFireProposal(CoverState cover) =>
        cover.Posture != CoverPosture.Crouched;
}
