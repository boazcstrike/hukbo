namespace Sandata.Core.Squads;

/// <summary>
/// Turns one squad slot's fixed formation-shape constants into a raw
/// fixed-point target point, per design section 8's formula:
/// <c>targetPoint(slot) = pointAtArclength(leaderArclength - slot.TrailOffset)
/// + lateralNormal * slot.LateralOffset</c>. Every follower is placed on the
/// leader's own past path — offset only along the arclength the leader
/// already walked and, from there, sideways relative to the path's own
/// local direction at that point — rather than at a position built from the
/// leader's current heading. That is "the whole trick": a corridor bends,
/// but a point that is already sitting on the corridor's own past centreline
/// bends with it, so the same fixed lateral half-width stays inside the
/// corridor through a turn that a rigid, world-space lateral offset (one
/// direction vector applied to every follower regardless of where the
/// corridor has since turned) would carry outside it.
/// </summary>
public static class SlotTargets
{
    /// <summary>
    /// Computes the slot's target point.
    /// </summary>
    /// <param name="path">
    /// The shared group polyline's precomputed arclength table — one
    /// instance serves every slot in the group, since the table itself does
    /// not depend on any slot's offsets.
    /// </param>
    /// <param name="leaderArclength">
    /// The leader's own position along <paramref name="path"/>, expressed as
    /// an arclength value in raw fixed-point units. Not required to be
    /// <c>path.TotalLength</c>: a caller that tracks the leader's progress
    /// along a path already in flight supplies whatever arclength corresponds
    /// to the leader's current position.
    /// </param>
    /// <param name="trailOffset">
    /// How far behind <paramref name="leaderArclength"/>, in raw fixed-point
    /// units measured along the path, this slot marches. Must not be
    /// negative — a slot ahead of the
    /// leader is not a trailing slot. A value that would place the slot
    /// before the path's own start clamps to the start rather than
    /// extrapolating past it or throwing, the same clamping
    /// <see cref="PolylineArclength.SampleAt"/> already applies for any
    /// out-of-range arclength.
    /// </param>
    /// <param name="lateralOffset">
    /// This slot's sideways displacement from the path's local centreline, in
    /// raw fixed-point units, applied perpendicular to the path's direction
    /// of travel at the
    /// sampled point. Positive displaces to the side reached by rotating the
    /// local direction vector <c>(dx, dy)</c> to <c>(dy, -dx)</c>; negative
    /// displaces to the opposite side. Zero leaves the target exactly on the
    /// centreline — the formation-collapse case design section 8 describes
    /// ("Doorway collapse falls out of the clearance field") reduces to
    /// calling this method with zero once a caller forces every slot's
    /// lateral offset to zero.
    /// </param>
    /// <returns>
    /// The slot's target point in raw fixed-point units — the same
    /// representation <c>MovementProposal</c> carries, so the caller passes it
    /// straight through rather than converting. When the sampled point falls
    /// where the path's local direction is undefined (a one-vertex path, or
    /// a duplicate-vertex zero-length segment), the lateral offset cannot be
    /// applied and the point on the centreline is returned unchanged.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="trailOffset"/> is negative.</exception>
    /// <remarks>
    /// The perpendicular is built by dividing the local segment's raw delta
    /// by its own true Euclidean length — see <see cref="PolylineArclength"/>'s
    /// remarks for why that length is Euclidean rather than a squared-free
    /// approximation — one component at a time. Each component division
    /// truncates toward zero the same way every other integer division in
    /// this codebase does, so the two components of the resulting
    /// displacement can round independently by at most one raw unit; that is
    /// a bounded, deterministic rounding behaviour, not an approximation
    /// that could disagree between two evaluations of the same input.
    /// </remarks>
    public static (long X, long Y) ComputeTarget(
        in PolylineArclength path,
        long leaderArclength,
        long trailOffset,
        long lateralOffset)
    {
        if (trailOffset < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(trailOffset), trailOffset, "trailOffset must not be negative.");
        }

        var slotArclength = leaderArclength - trailOffset;

        if (slotArclength < 0)
        {
            slotArclength = 0;
        }

        var sample = path.SampleAt(slotArclength);

        if (lateralOffset == 0 || sample.DirectionLength == 0)
        {
            return (sample.X, sample.Y);
        }

        var lateralX = checked(sample.DirectionY * lateralOffset / sample.DirectionLength);
        var lateralY = checked(-sample.DirectionX * lateralOffset / sample.DirectionLength);

        return (checked(sample.X + lateralX), checked(sample.Y + lateralY));
    }
}
