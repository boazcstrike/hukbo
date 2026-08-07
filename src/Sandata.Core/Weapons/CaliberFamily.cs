namespace Sandata.Core.Weapons;

/// <summary>
/// The eight caliber families the 38-row roster reduces to. Design section
/// 10 and docs/research/2026-08-07-sandata-research-consolidated.md section 3
/// record why this is the axis that keys the gunshot report sample rather
/// than the individual weapon: six families cover all 24 rifles and two more
/// cover the 14 pistols, which is what makes a 484-slot audio library
/// tractable instead of needing one report per weapon.
/// </summary>
/// <remarks>
/// Exactly eight members, no more and no fewer — a ninth family or a missing
/// one both break the design's audio-tractability argument. Every numeric
/// value is part of the same replay contract <see cref="FirearmId"/>'s
/// remarks describe: append-only, never reordered, never renumbered without
/// a new preset version, since <c>FirearmCatalog</c>'s content hash folds
/// this value for every row.
/// </remarks>
public enum CaliberFamily
{
    /// <summary>7.62×39mm.</summary>
    Cal762X39 = 0,

    /// <summary>5.45×39mm.</summary>
    Cal545X39 = 1,

    /// <summary>5.56×45mm.</summary>
    Cal556X45 = 2,

    /// <summary>7.62×51mm.</summary>
    Cal762X51 = 3,

    /// <summary>6.8×51mm.</summary>
    Cal68X51 = 4,

    /// <summary>5.8×42mm.</summary>
    Cal58X42 = 5,

    /// <summary>9×19mm.</summary>
    Cal9X19 = 6,

    /// <summary>5.8×21mm.</summary>
    Cal58X21 = 7,
}
