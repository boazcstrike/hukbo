namespace Hukbo.Core.Movement;

/// <summary>
/// One of sixteen 22.5-degree facing sectors, plus <see cref="None"/> for an
/// agent with no resolved facing. Sector values run clockwise on screen from
/// <see cref="East"/>: positive Y is screen-down in this simulation, so
/// sector 4 is <see cref="South"/>. Numeric values are part of the
/// deterministic replay and state-hash contract; the enum is append-only —
/// never renumber, reorder, or remove a member. The exact integer vector for
/// each sector lives in <see cref="FacingRules"/>.
/// </summary>
public enum Facing16 : byte
{
    /// <summary>Sector 0, screen-right. Initial facing for faction 0.</summary>
    East = 0,

    /// <summary>Sector 1, one step clockwise from <see cref="East"/>.</summary>
    EastSouthEast = 1,

    /// <summary>Sector 2, screen-right and screen-down.</summary>
    SouthEast = 2,

    /// <summary>Sector 3, one step counter-clockwise from <see cref="South"/>.</summary>
    SouthSouthEast = 3,

    /// <summary>Sector 4, screen-down.</summary>
    South = 4,

    /// <summary>Sector 5, one step clockwise from <see cref="South"/>.</summary>
    SouthSouthWest = 5,

    /// <summary>Sector 6, screen-left and screen-down.</summary>
    SouthWest = 6,

    /// <summary>Sector 7, one step counter-clockwise from <see cref="West"/>.</summary>
    WestSouthWest = 7,

    /// <summary>Sector 8, screen-left. Initial facing for faction 1.</summary>
    West = 8,

    /// <summary>Sector 9, one step clockwise from <see cref="West"/>.</summary>
    WestNorthWest = 9,

    /// <summary>Sector 10, screen-left and screen-up.</summary>
    NorthWest = 10,

    /// <summary>Sector 11, one step counter-clockwise from <see cref="North"/>.</summary>
    NorthNorthWest = 11,

    /// <summary>Sector 12, screen-up.</summary>
    North = 12,

    /// <summary>Sector 13, one step clockwise from <see cref="North"/>.</summary>
    NorthNorthEast = 13,

    /// <summary>Sector 14, screen-right and screen-up.</summary>
    NorthEast = 14,

    /// <summary>Sector 15, one step counter-clockwise from <see cref="East"/>.</summary>
    EastNorthEast = 15,

    /// <summary>
    /// No resolved facing. Deliberately far from the sector range so a raw
    /// sector arithmetic slip cannot reach it; the value legacy presets carry
    /// implicitly, and never a valid input to sector arithmetic in
    /// <see cref="FacingRules"/>.
    /// </summary>
    None = 255,
}
