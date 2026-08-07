using Sandata.Core.Navigation;

namespace Sandata.Core.Squads;

/// <summary>
/// The single-file collapse design section 8 of
/// <c>docs/plans/2026-08-07-sandata-scaffold-design.md</c> describes under
/// "Doorway collapse falls out of the clearance field": "When the clearance
/// at the leader's cell drops below the formation half-width,
/// <c>slot.LateralOffset</c> goes to zero for every slot in the group and
/// the squad becomes a single file. On the far side, clearance rises and the
/// offsets return." This type is that comparison and nothing more — a pure
/// function of a <see cref="ClearanceField"/> value already looked up by the
/// caller and a formation-half-width constant, with no field, no timer, and
/// no case inside <see cref="Navigation.NavSearch"/> or anywhere else in the
/// pathfinder. A slot's trail offset — its position along the path, which
/// keeps the squad strung out single-file rather than collapsing onto one
/// point — is untouched; only the lateral (sideways) component is ever
/// forced to zero.
/// </summary>
/// <remarks>
/// <para>
/// <b>The unit hazard this type exists to resolve, in the open.</b> A
/// <see cref="ClearanceField"/> value is a chamfer distance in the same
/// weighted units as <see cref="NavHeuristic"/>'s octile step costs: an
/// orthogonal step costs <see cref="NavHeuristic.OrthogonalStepCost"/> (10)
/// and covers exactly one cell, <see cref="NavGrid.CellSizeWu"/> (4) world
/// units. So a clearance of 10 means "one cell", which means "4 world
/// units" — the conversion factor from a clearance value to a world-unit
/// distance is <c>CellSizeWu / OrthogonalStepCost</c>, that is,
/// <c>4 / 10</c>, not 1. A formation half-width, by contrast, is authored
/// directly in world units — the same unit
/// <see cref="Squads.SlotTargets.ComputeTarget"/>'s <c>lateralOffset</c>
/// parameter already uses — because it describes a physical span of bodies
/// standing side by side, not a grid distance. The two values are never
/// compared as raw integers; this type converts before it compares.
/// </para>
/// <para>
/// <b>Division-free by cross-multiplication</b>, matching the house style
/// already used for <see cref="Navigation.GridRay"/>'s parametric comparisons
/// and <see cref="Navigation.RayBox"/>'s slab tests. The comparison
/// "clearance, converted to world units, is less than the formation
/// half-width" —
/// <c>leaderClearance * CellSizeWu / OrthogonalStepCost &lt; formationHalfWidthWu</c>
/// — is algebraically identical, for the strictly positive
/// <see cref="NavHeuristic.OrthogonalStepCost"/> denominator, to
/// <c>leaderClearance * CellSizeWu &lt; formationHalfWidthWu * OrthogonalStepCost</c>,
/// which is exactly what <see cref="IsCollapsed"/> evaluates: no division,
/// no truncation, and no rounding direction to pin.
/// </para>
/// <para>
/// <b>"Drops below" is a strict inequality.</b> A leader clearance exactly
/// equal to the formation half-width, once both sides are expressed in the
/// same unit, has not yet dropped below it, so the formation stays expanded
/// at that exact value and only collapses the moment the clearance falls
/// one further chamfer unit short. <c>FormationCollapseTests</c> pins both
/// sides of that boundary directly.
/// </para>
/// </remarks>
public static class FormationCollapse
{
    /// <summary>
    /// Whether the clearance at the leader's cell has dropped below
    /// <paramref name="formationHalfWidthWu"/>, once both values are
    /// expressed in the same unit — see this type's remarks for the exact
    /// conversion and the division-free comparison it reduces to.
    /// </summary>
    /// <param name="leaderClearance">
    /// The <see cref="ClearanceField"/> value already looked up at the
    /// leader's cell, in chamfer units. Never negative in a field
    /// <see cref="ClearanceField.Build"/> or
    /// <see cref="ClearanceField.RebuildLocal"/> produced —
    /// <see cref="ClearanceField.BlockedClearance"/> (0) is the smallest
    /// value either method ever writes.
    /// </param>
    /// <param name="formationHalfWidthWu">
    /// The squad's formation half-width, in world units — the same unit
    /// <see cref="SlotTargets.ComputeTarget"/>'s own <c>lateralOffset</c>
    /// parameter uses. Never negative: a half-width describes a physical
    /// span and cannot be a negative distance.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the leader's clearance, converted to
    /// world units, is strictly less than <paramref name="formationHalfWidthWu"/>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="leaderClearance"/> or <paramref name="formationHalfWidthWu"/>
    /// is negative.
    /// </exception>
    public static bool IsCollapsed(int leaderClearance, long formationHalfWidthWu)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(leaderClearance);
        ArgumentOutOfRangeException.ThrowIfNegative(formationHalfWidthWu);

        return checked((long)leaderClearance * NavGrid.CellSizeWu) <
            checked(formationHalfWidthWu * NavHeuristic.OrthogonalStepCost);
    }

    /// <summary>
    /// One slot's lateral offset after the collapse rule is applied: zero
    /// when <see cref="IsCollapsed"/> is <see langword="true"/> for
    /// <paramref name="leaderClearance"/> and
    /// <paramref name="formationHalfWidthWu"/>, otherwise
    /// <paramref name="uncollapsedLateralOffset"/> unchanged. This is design
    /// section 8's rule stated as a single pure function: feed the result
    /// straight into <see cref="SlotTargets.ComputeTarget"/>'s own
    /// <c>lateralOffset</c> parameter, for every slot in the group, using the
    /// same <paramref name="leaderClearance"/> looked up once at the
    /// leader's cell — there is nothing per-slot in the collapse decision
    /// itself, only in the offset it is applied to.
    /// </summary>
    /// <param name="leaderClearance">Same contract as <see cref="IsCollapsed"/>'s parameter of the same name.</param>
    /// <param name="formationHalfWidthWu">Same contract as <see cref="IsCollapsed"/>'s parameter of the same name.</param>
    /// <param name="uncollapsedLateralOffset">
    /// This slot's own lateral offset when the formation is expanded — the
    /// value <see cref="SlotTargets.ComputeTarget"/>'s <c>lateralOffset</c>
    /// parameter would otherwise receive directly. May be positive,
    /// negative, or zero; a slot already on the centreline is unaffected by
    /// the collapse either way.
    /// </param>
    /// <returns>
    /// Zero when collapsed; otherwise <paramref name="uncollapsedLateralOffset"/> verbatim.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="leaderClearance"/> or <paramref name="formationHalfWidthWu"/>
    /// is negative.
    /// </exception>
    public static long LateralOffset(int leaderClearance, long formationHalfWidthWu, long uncollapsedLateralOffset) =>
        IsCollapsed(leaderClearance, formationHalfWidthWu) ? 0 : uncollapsedLateralOffset;
}
