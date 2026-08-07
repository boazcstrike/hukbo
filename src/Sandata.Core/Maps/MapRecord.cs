namespace Sandata.Core.Maps;

/// <summary>
/// Base type for one parsed, per-record-validated line of an `.hkmap`
/// file (design section 12). <see cref="LineNumber"/> is the 1-based line
/// number of the original source text, preserved through the blank-line
/// and comment removal <c>MapTokenizer</c> performs before parsing, so a
/// later load error — or a caller that just wants to say where a wall
/// came from — can still name the real line.
///
/// Every field on every derived record has already passed the per-record
/// field validation in design section 12 by the time
/// <c>MapTokenizer.Tokenize</c> returns it. Cross-record validation —
/// spawn presence and separation, full enclosure, faction-0 reachability
/// to every objective, and duplicate detection of any kind — is
/// <c>MapValidator</c>'s job (task 25), not this type's, and not
/// <c>MapTokenizer</c>'s.
/// </summary>
public abstract record MapRecord(MapRecordKind Kind, int LineNumber);

/// <summary>The <c>HKMAP</c> header. Must be line 1. <see cref="Version"/> must equal 1.</summary>
public sealed record HkmapRecord(int LineNumber, int Version)
    : MapRecord(MapRecordKind.Hkmap, LineNumber);

/// <summary>The <c>NAME</c> header. <see cref="Id"/> matches <c>[a-z0-9-]{1,32}</c>.</summary>
public sealed record NameRecord(int LineNumber, string Id)
    : MapRecord(MapRecordKind.Name, LineNumber);

/// <summary>
/// The <c>GRID</c> header. <see cref="CellWu"/> must be a power of two;
/// <see cref="WidthWu"/> and <see cref="HeightWu"/> must each be a
/// positive multiple of it, and neither may span more than 512 cells.
/// </summary>
public sealed record GridRecord(int LineNumber, int WidthWu, int HeightWu, int CellWu)
    : MapRecord(MapRecordKind.Grid, LineNumber);

/// <summary>
/// A wall segment. Endpoints must differ and lie inside the map bounds.
/// <see cref="Material"/> is 0 glass, 1 solid, 2 partition, 3 breachable.
/// Canonical (lexicographic-ascending) endpoint ordering is
/// <c>MapCanonicalizer</c>'s job, not this record's — it carries the
/// endpoints exactly as written.
/// </summary>
public sealed record WallRecord(int LineNumber, int X1, int Y1, int X2, int Y2, int Material)
    : MapRecord(MapRecordKind.Wall, LineNumber);

/// <summary>
/// A door segment. Endpoints must differ, be axis-aligned, and lie inside
/// the map bounds. <see cref="Hinge"/> is 0 or 1. <see cref="State"/> is 0
/// closed, 1 open. Collinear-overlap with a <see cref="WallRecord"/> is
/// cross-record validation, deferred to <c>MapValidator</c>.
/// </summary>
public sealed record DoorRecord(int LineNumber, int X1, int Y1, int X2, int Y2, int Hinge, int State)
    : MapRecord(MapRecordKind.Door, LineNumber);

/// <summary>
/// A directional cover object. <see cref="MinX"/>/<see cref="MinY"/> must
/// be strictly less than <see cref="MaxX"/>/<see cref="MaxY"/>, both
/// corners inside the map bounds. <see cref="ArcCentreBam"/> is 0..65535.
/// <see cref="ArcHalfBam"/> is 1..32768, where 32768 means the object
/// covers from every direction. <see cref="Height"/> is 0..2.
/// </summary>
public sealed record CoverRecord(
    int LineNumber,
    int MinX,
    int MinY,
    int MaxX,
    int MaxY,
    int ArcCentreBam,
    int ArcHalfBam,
    int Height) : MapRecord(MapRecordKind.Cover, LineNumber);

/// <summary>
/// A faction spawn point. <see cref="Faction"/> is 0 or 1, the point must
/// lie inside the map bounds, and <see cref="FacingBam"/> is 0..65535.
/// Whether the spawn cell is passable after body-radius inflation is
/// cross-record validation (it needs the baked nav grid), deferred to
/// <c>MapValidator</c>.
/// </summary>
public sealed record SpawnRecord(int LineNumber, int Faction, int X, int Y, int FacingBam)
    : MapRecord(MapRecordKind.Spawn, LineNumber);

/// <summary>
/// A mission objective. The point must lie inside the map bounds and
/// <see cref="RadiusWu"/> must be positive. Whether every
/// <see cref="Index"/> across the file is dense from 0 is cross-record
/// validation, deferred to <c>MapValidator</c>.
/// </summary>
public sealed record ObjectiveRecord(int LineNumber, int Index, int X, int Y, int RadiusWu)
    : MapRecord(MapRecordKind.Objective, LineNumber);

/// <summary>The terminator record. Carries no fields.</summary>
public sealed record EndRecord(int LineNumber) : MapRecord(MapRecordKind.End, LineNumber);
