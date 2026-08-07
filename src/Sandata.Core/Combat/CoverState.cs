using Sandata.Core.Mathematics;

namespace Sandata.Core.Combat;

/// <summary>
/// An operator's stance with respect to incoming fire: upright and able to
/// return fire, or crouched and unable to.
/// </summary>
/// <remarks>
/// Design section 4 of docs/plans/2026-08-07-sandata-scaffold-design.md lists
/// "posture (standing or crouched)" among the authoritative, hashed
/// per-operator fields, but no earlier task has declared the operator state
/// record that field will eventually live on — <c>Mission</c>,
/// <c>MissionState</c>, and their snapshot exist only as bare shells (task
/// 17), and the operator record itself is not yet written. This enum is
/// declared here, in <c>Sandata.Core.Combat</c>, because <see cref="CoverRules"/>
/// needs a posture value today; whichever future task adds the full operator
/// state record is expected to reference this type rather than declare a
/// second one.
/// </remarks>
public enum CoverPosture
{
    /// <summary>Upright. Standing cover protection applies only inside the
    /// covering object's protected arc, per <see cref="CoverRules"/>.</summary>
    Standing = 0,

    /// <summary>
    /// Tucked down behind the covering object. A crouched operator cannot
    /// produce a fire proposal (see <see cref="CoverRules.CanProduceFireProposal"/>)
    /// and, while affiliated with a cover object, receives near-total
    /// protection regardless of the direction fire arrives from — see the
    /// remarks on <see cref="CoverRules.CrouchedCoverReductionPercent"/> for
    /// why that protection is not arc-gated the way standing cover is.
    /// </summary>
    Crouched = 1,
}

/// <summary>
/// An operator's affiliation with a single cover object for the purpose of
/// resolving one incoming shot: whether the operator is currently behind a
/// cover object at all, that object's protected arc, and the operator's
/// posture.
/// </summary>
/// <remarks>
/// <para>
/// This type deliberately does not carry any per-shot geometry — no shooter
/// position, no incoming direction. Two operators standing behind the same
/// physical cover object can each hold their own <see cref="CoverState"/>
/// with the same <see cref="ArcCentreBam"/> and <see cref="ArcHalfBam"/> (both
/// copied from the same map <c>COVER</c> record), because the object's arc is
/// a property of the object, not of the operator. What differs per operator
/// is each one's own position relative to any given shooter, which
/// <see cref="CoverRules"/> takes as separate parameters at the moment a shot
/// is resolved — see design section 9's directional cover model, which is
/// explicit that "two operators behind the same car do not both get it" the
/// benefit turns on each operator's own position, not on the object.
/// </para>
/// <para>
/// Which cover object (if any) an operator is currently affiliated with, and
/// how that affiliation is established or lost, is out of this task's scope —
/// task 30 covers only the arc math and the percentage rules once an
/// affiliation already exists. A future task assigns <see cref="InCover"/>
/// and the two arc fields from the map's <c>COVER</c> records.
/// </para>
/// </remarks>
/// <param name="InCover">
/// Whether the operator is currently affiliated with a cover object. When
/// <see langword="false"/>, <see cref="ArcCentreBam"/> and
/// <see cref="ArcHalfBam"/> are meaningless and <see cref="CoverRules"/>
/// never reads them.
/// </param>
/// <param name="ArcCentreBam">
/// The direction the cover object's protected arc faces, copied unchanged
/// from the map's <c>COVER</c> record. Meaningless when <see cref="InCover"/>
/// is <see langword="false"/>.
/// </param>
/// <param name="ArcHalfBam">
/// Half the protected arc's total angular width, in <see cref="Bam16"/> raw
/// units, copied unchanged from the map's <c>COVER</c> record. The map loader
/// (design section 12) restricts this field to <c>1..32768</c>, where
/// <c>32768</c> means the object protects from every direction. Meaningless
/// when <see cref="InCover"/> is <see langword="false"/>.
/// </param>
/// <param name="Posture">The operator's current stance.</param>
public readonly record struct CoverState(
    bool InCover,
    Bam16 ArcCentreBam,
    ushort ArcHalfBam,
    CoverPosture Posture)
{
    /// <summary>
    /// The state of an operator who is not affiliated with any cover object
    /// and is standing.
    /// </summary>
    public static readonly CoverState NotInCover = new(
        InCover: false,
        ArcCentreBam: default,
        ArcHalfBam: 0,
        Posture: CoverPosture.Standing);
}
