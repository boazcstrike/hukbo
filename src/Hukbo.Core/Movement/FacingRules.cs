namespace Hukbo.Core.Movement;

/// <summary>
/// Pure, integer-only arithmetic for the 16-sector facing model of the
/// weapon-relative movement design, section 6: the exact vector table, delta
/// resolution, turning, circular separation, and the direction-band pace
/// cap. Everything here is long multiplication and modular sector math — no
/// trigonometry and no fractional numeric type appears in this file, and a
/// source-hygiene test scans the file to keep it that way.
/// </summary>
/// <remarks>
/// Faction 1 is canonicalized by mirroring across the vertical axis, so
/// reflected inputs produce exactly reflected facings and a mirrored-duel
/// test can assert an exact relationship between the two factions. Every tie
/// rule resolves in that canonical space: an exact dot-product tie takes the
/// lower numeric sector, and an eight-step turning tie goes clockwise, which
/// maps back to counter-clockwise in world space for faction 1.
/// </remarks>
public static class FacingRules
{
    private const int SectorCount = 16;
    private const int HalfTurnSectors = 8;

    /// <summary>
    /// X components of the sixteen sector vectors at
    /// <see cref="Mathematics.FixedPoint.Scale"/> = 1024, exact to the digit
    /// per design section 6.1.
    /// </summary>
    private static ReadOnlySpan<int> VectorXBySector =>
    [
        1_024, 946, 724, 392, 0, -392, -724, -946,
        -1_024, -946, -724, -392, 0, 392, 724, 946,
    ];

    /// <summary>
    /// Y components of the sixteen sector vectors. Positive Y is screen-down,
    /// so sector 4, <see cref="Facing16.South"/>, is (0, 1024).
    /// </summary>
    private static ReadOnlySpan<int> VectorYBySector =>
    [
        0, 392, 724, 946, 1_024, 946, 724, 392,
        0, -392, -724, -946, -1_024, -946, -724, -392,
    ];

    /// <summary>
    /// The exact integer vector for one sector, in raw fixed-point units at
    /// scale 1024. Rejects <see cref="Facing16.None"/> and any undeclared
    /// value.
    /// </summary>
    public static (int RawX, int RawY) SectorVector(Facing16 facing)
    {
        var sector = ValidateSector(facing, nameof(facing));
        return (VectorXBySector[sector], VectorYBySector[sector]);
    }

    /// <summary>
    /// Resolves a raw world-space delta to the facing sector whose table
    /// vector has the greatest dot product with it. A (0, 0) delta returns
    /// <see cref="Facing16.None"/> without consulting the table. Faction 1's
    /// delta is canonicalized by negating X, and an exact dot-product tie
    /// takes the lower numeric sector in canonical space, so reflected
    /// deltas — ties included — produce exactly reflected facings.
    /// </summary>
    public static Facing16 FromDelta(
        long deltaXRaw,
        long deltaYRaw,
        int factionId)
    {
        ValidateFaction(factionId);
        if (deltaXRaw == 0 && deltaYRaw == 0)
        {
            return Facing16.None;
        }

        var canonicalDeltaX = factionId == 1
            ? checked(-deltaXRaw)
            : deltaXRaw;

        var bestSector = 0;
        var bestDot = long.MinValue;
        for (var sector = 0; sector < SectorCount; sector++)
        {
            var dot = checked(
                (VectorXBySector[sector] * canonicalDeltaX) +
                (VectorYBySector[sector] * deltaYRaw));

            // Strictly greater, so the first — lowest — sector of an exact
            // tie is the one retained.
            if (dot > bestDot)
            {
                bestDot = dot;
                bestSector = sector;
            }
        }

        return (Facing16)ToWorldSector(bestSector, factionId);
    }

    /// <summary>
    /// Turns <paramref name="current"/> toward <paramref name="desired"/> by
    /// at most <paramref name="maximumFacingSteps"/> sectors, along the
    /// shorter arc in faction-canonical space. An eight-step tie turns
    /// clockwise in canonical space, which maps to counter-clockwise in world
    /// space for faction 1. A request exactly at the step cap reaches the
    /// desired facing; a request one sector beyond it advances only by the
    /// cap. Callers select the cap — the profile's ordinary or committed
    /// facing steps — before calling.
    /// </summary>
    public static Facing16 TurnToward(
        Facing16 current,
        Facing16 desired,
        int maximumFacingSteps,
        int factionId)
    {
        ValidateFaction(factionId);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumFacingSteps);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            maximumFacingSteps, HalfTurnSectors);
        var canonicalCurrent = ToCanonicalSector(
            ValidateSector(current, nameof(current)), factionId);
        var canonicalDesired = ToCanonicalSector(
            ValidateSector(desired, nameof(desired)), factionId);

        var clockwise =
            (canonicalDesired - canonicalCurrent + SectorCount) % SectorCount;
        var counterClockwise =
            (canonicalCurrent - canonicalDesired + SectorCount) % SectorCount;

        // The smaller arc wins; the eight-step tie goes clockwise.
        var next = clockwise <= counterClockwise
            ? (canonicalCurrent + Math.Min(clockwise, maximumFacingSteps))
                % SectorCount
            : (canonicalCurrent - Math.Min(counterClockwise, maximumFacingSteps)
                + SectorCount) % SectorCount;

        return (Facing16)ToWorldSector(next, factionId);
    }

    /// <summary>
    /// The circular separation between two sectors, in sectors, from 0 to 8.
    /// Symmetric, and invariant under the faction mirror, so it needs no
    /// faction argument.
    /// </summary>
    public static int SectorSeparation(Facing16 first, Facing16 second)
    {
        var firstSector = ValidateSector(first, nameof(first));
        var secondSector = ValidateSector(second, nameof(second));
        var clockwise =
            (secondSector - firstSector + SectorCount) % SectorCount;
        return Math.Min(clockwise, SectorCount - clockwise);
    }

    /// <summary>
    /// The design section 6.4 direction band: separation 0 to 1 caps pace at
    /// the profile's forward value, 2 to 5 at its lateral value, and 6 to 8
    /// at its backward value. The committed-phase clamp and the
    /// <c>MovementSpeedRaw</c> cap are applied by the caller, not here.
    /// </summary>
    public static int DirectionBandPaceCapBasisPoints(
        LoadoutMovementProfile profile,
        int separationSectors)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentOutOfRangeException.ThrowIfNegative(separationSectors);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            separationSectors, HalfTurnSectors);

        if (separationSectors <= 1)
        {
            return profile.ForwardPaceBasisPoints;
        }

        return separationSectors <= 5
            ? profile.LateralPaceBasisPoints
            : profile.BackwardPaceBasisPoints;
    }

    /// <summary>
    /// The world-to-canonical sector mirror for faction 1, its own inverse,
    /// so it also maps canonical results back to world space.
    /// </summary>
    private static int ToCanonicalSector(int sector, int factionId) =>
        factionId == 1
            ? (HalfTurnSectors - sector + SectorCount) % SectorCount
            : sector;

    /// <inheritdoc cref="ToCanonicalSector" />
    private static int ToWorldSector(int sector, int factionId) =>
        ToCanonicalSector(sector, factionId);

    private static int ValidateSector(Facing16 facing, string parameterName)
    {
        var sector = (int)facing;
        if (sector >= SectorCount)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                facing,
                "Sector arithmetic requires one of the sixteen declared " +
                "sectors; Facing16.None is not a valid input here.");
        }

        return sector;
    }

    private static void ValidateFaction(int factionId)
    {
        if (factionId is not (0 or 1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(factionId),
                factionId,
                "This battle has exactly two factions, 0 and 1.");
        }
    }
}
