namespace Sandata.Core.Navigation;

/// <summary>
/// The passability state one <see cref="NavGrid"/> cell holds, as baked by
/// <see cref="NavBake.Bake"/>. Design section 7's data-structures table names
/// this array <c>passability</c> and pins its three literal byte values —
/// <c>0</c> blocked, <c>1</c> open, <c>2</c> door — which this enum's members
/// mirror exactly so a value read from that table and a value read from this
/// type always agree.
///
/// <para>
/// Named <c>NavCellFlags</c> per task 18's file assignment even though it is
/// not a <c>[Flags]</c> bit field: a cell is in exactly one of these three
/// states, never a combination. If a later task needs an orthogonal,
/// independently-toggleable property of a cell — for example "this cell was
/// blocked purely by inflation, not by a wall's own footprint" — that is a
/// second, genuinely bit-flagged type, not a fourth member added here.
/// </para>
///
/// <para>
/// <b>Door means closed.</b> An open door's cells are rasterized as
/// <see cref="Open"/>, not <see cref="Door"/> — <see cref="NavBake.Bake"/>
/// only rasterizes a <c>DOOR</c> record whose <c>State</c> field is 0
/// (closed; design section 12). <see cref="Door"/> is deliberately not
/// simply "impassable": it is passable to the planner at high cost (so a
/// squad routes to a door rather than around the whole building) and
/// impassable to the mover until the door actually opens, at which point a
/// later stage rebakes the affected cells to <see cref="Open"/>.
/// </para>
/// </summary>
public enum NavCellFlags : byte
{
    /// <summary>
    /// No body can occupy this cell: a wall's rasterized footprint, or a cell
    /// too close to one for a body of the baked radius to fit without
    /// clipping it (see <see cref="NavBake.Bake"/>'s inflation pass).
    /// </summary>
    Blocked = 0,

    /// <summary>A body of the baked radius fits here without qualification.</summary>
    Open = 1,

    /// <summary>
    /// A closed door's rasterized footprint: high cost but passable to the
    /// planner, impassable to the mover until the door opens.
    /// </summary>
    Door = 2,
}
