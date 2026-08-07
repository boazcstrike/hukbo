using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Movement;

/// <summary>
/// Pure, integer-only route arithmetic for the equipment-relative footwork
/// preset: the one-tick <see cref="StepEndpoint"/> helper, the 22.5-degree
/// oblique rotation, side parity, the retained-pace model, ally-clearance
/// radii, and the friendly-clearance conflict pass of the weapon-relative
/// movement design, sections 6 and 10. Every method reads only its own
/// arguments — no agent array, no simulation, no tick pipeline — matching
/// the testability shape of <see cref="FacingRules"/> and
/// <see cref="WeaponMovementRules"/>. Division truncates toward zero
/// everywhere, and nothing here touches floating point.
/// </summary>
internal static class MovementRouteRules
{
    /// <summary>
    /// The basis-point denominator of the pace and clearance model (design
    /// section 4.4).
    /// </summary>
    private const long BasisPointDenominator = 10_000;

    /// <summary>
    /// The fixed-point scale of the oblique rotation table, matching
    /// <see cref="FixedPoint.Scale"/> and the <see cref="FacingRules"/>
    /// sector-vector table.
    /// </summary>
    private const long ObliqueScale = 1_024;

    /// <summary>Cosine of 22.5 degrees at scale 1024, per design 10.2.</summary>
    private const long ObliqueCosine = 946;

    /// <summary>Sine of 22.5 degrees at scale 1024, per design 10.2.</summary>
    private const long ObliqueSine = 392;

    /// <summary>
    /// The highest value <see cref="ConflictPhaseSafetyRank"/> returns, the
    /// defensive bucket for a phase outside the seven named by design 10.6.
    /// </summary>
    internal const int MaximumConflictSafetyRank = 7;

    /// <summary>
    /// Builds one candidate endpoint exactly per design section 10.1: reject
    /// a zero delta, scale the delta by <c>paceRaw / distance</c> with
    /// truncation toward zero, fall back to one raw unit on the greater
    /// absolute axis when the scaled move truncates to zero — X wins an
    /// exact tie — and clamp the result to the map through the existing
    /// centre clamp. Returns <see langword="null"/> only for the rejected
    /// zero delta.
    /// </summary>
    internal static (int XRaw, int YRaw)? StepEndpoint(
        int actorXRaw,
        int actorYRaw,
        long deltaXRaw,
        long deltaYRaw,
        int paceRaw,
        int mapWidthRaw,
        int mapHeightRaw,
        int bodyRadiusRaw)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(paceRaw);
        if (deltaXRaw == 0 && deltaYRaw == 0)
        {
            return null;
        }

        var distance = FixedPoint.IntegerSquareRoot(
            checked((deltaXRaw * deltaXRaw) + (deltaYRaw * deltaYRaw)));
        var moveX = checked(deltaXRaw * paceRaw) / Math.Max(1L, distance);
        var moveY = checked(deltaYRaw * paceRaw) / Math.Max(1L, distance);

        if (moveX == 0 && moveY == 0)
        {
            if (Math.Abs(deltaXRaw) >= Math.Abs(deltaYRaw))
            {
                moveX = Math.Sign(deltaXRaw);
            }
            else
            {
                moveY = Math.Sign(deltaYRaw);
            }
        }

        var endpointX = CollisionGeometry.ClampCenterToBounds(
            SaturateToInt32(checked(actorXRaw + moveX)),
            mapWidthRaw,
            bodyRadiusRaw);
        var endpointY = CollisionGeometry.ClampCenterToBounds(
            SaturateToInt32(checked(actorYRaw + moveY)),
            mapHeightRaw,
            bodyRadiusRaw);
        return (endpointX, endpointY);
    }

    /// <summary>
    /// Rotates a target delta by 22.5 degrees in the requested world-space
    /// direction, verbatim from design section 10.2, truncating toward
    /// zero. If a nonzero source delta truncates to <c>(0, 0)</c>, the
    /// delta's world sector is rotated by one sector in the requested
    /// direction and the exact table vector is substituted, so a degenerate
    /// oblique is never silently dropped.
    /// </summary>
    internal static (long DeltaXRaw, long DeltaYRaw) RotateOblique(
        long deltaXRaw,
        long deltaYRaw,
        bool clockwise)
    {
        if (deltaXRaw == 0 && deltaYRaw == 0)
        {
            throw new ArgumentException(
                "An oblique of a zero delta has no direction.",
                nameof(deltaXRaw));
        }

        long obliqueX;
        long obliqueY;
        if (clockwise)
        {
            obliqueX = checked(
                (ObliqueCosine * deltaXRaw) - (ObliqueSine * deltaYRaw)) /
                ObliqueScale;
            obliqueY = checked(
                (ObliqueSine * deltaXRaw) + (ObliqueCosine * deltaYRaw)) /
                ObliqueScale;
        }
        else
        {
            obliqueX = checked(
                (ObliqueCosine * deltaXRaw) + (ObliqueSine * deltaYRaw)) /
                ObliqueScale;
            obliqueY = checked(
                (-ObliqueSine * deltaXRaw) + (ObliqueCosine * deltaYRaw)) /
                ObliqueScale;
        }

        if (obliqueX != 0 || obliqueY != 0)
        {
            return (obliqueX, obliqueY);
        }

        // The degenerate substitution: resolve the delta's world sector —
        // faction 0 is the identity canonicalization — rotate it one sector
        // in the requested direction, and take the exact table vector.
        var sector = (int)FacingRules.FromDelta(deltaXRaw, deltaYRaw, 0);
        var rotated = (sector + (clockwise ? 1 : 15)) % 16;
        var (vectorX, vectorY) = FacingRules.SectorVector((Facing16)rotated);
        return (vectorX, vectorY);
    }

    /// <summary>
    /// The 90-degree perpendicular of a delta in the requested world-space
    /// rotation direction, used by the disengage escape fallback of design
    /// section 10.4. Positive Y is screen-down, so the clockwise
    /// perpendicular of east is south.
    /// </summary>
    internal static (long DeltaXRaw, long DeltaYRaw) PerpendicularVector(
        long deltaXRaw,
        long deltaYRaw,
        bool clockwise) =>
        clockwise
            ? (checked(-deltaYRaw), deltaXRaw)
            : (deltaYRaw, checked(-deltaXRaw));

    /// <summary>
    /// Side parity per design section 10.3: <c>sideA</c> is
    /// canonical-clockwise for an even faction-local index and
    /// canonical-counter-clockwise for an odd one, and mapping back to world
    /// space swaps the two rotations for faction 1. Returns whether
    /// <c>sideA</c> is the world-space clockwise oblique.
    /// </summary>
    internal static bool SideAIsWorldClockwise(
        int factionLocalIndex,
        int factionId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(factionLocalIndex);
        if (factionId is not (0 or 1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(factionId),
                factionId,
                "This battle has exactly two factions, 0 and 1.");
        }

        var canonicalClockwise = (factionLocalIndex & 1) == 0;
        return factionId == 1 ? !canonicalClockwise : canonicalClockwise;
    }

    /// <summary>
    /// Converts a direction-band pace cap in basis points to a raw desired
    /// pace, capped at the warrior's own <c>MovementSpeedRaw</c> (design
    /// section 6.4), truncating toward zero.
    /// </summary>
    internal static int DesiredPaceRaw(
        int movementSpeedRaw,
        int paceCapBasisPoints)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(movementSpeedRaw);
        ArgumentOutOfRangeException.ThrowIfNegative(paceCapBasisPoints);
        var scaled = checked((long)movementSpeedRaw * paceCapBasisPoints) /
            BasisPointDenominator;
        return (int)Math.Min(movementSpeedRaw, scaled);
    }

    /// <summary>
    /// One acceleration or deceleration step in raw units per tick —
    /// <c>max(1, MovementSpeedRaw * basisPoints / 10000)</c> per design
    /// section 4.4, so the retained pace can always make progress toward its
    /// target.
    /// </summary>
    internal static int PaceStepRaw(
        int movementSpeedRaw,
        int basisPointsPerTick)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(movementSpeedRaw);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(basisPointsPerTick);
        var step = checked((long)movementSpeedRaw * basisPointsPerTick) /
            BasisPointDenominator;
        return (int)Math.Max(1L, step);
    }

    /// <summary>
    /// Moves the retained pace toward the desired pace by one bounded step,
    /// never overshooting the target in either direction (design section
    /// 6.5).
    /// </summary>
    internal static int AdvanceRetainedPaceRaw(
        int currentPaceRaw,
        int desiredPaceRaw,
        int accelerationStepRaw,
        int decelerationStepRaw)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(currentPaceRaw);
        ArgumentOutOfRangeException.ThrowIfNegative(desiredPaceRaw);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(accelerationStepRaw);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(decelerationStepRaw);

        if (currentPaceRaw < desiredPaceRaw)
        {
            return Math.Min(
                desiredPaceRaw, checked(currentPaceRaw + accelerationStepRaw));
        }

        if (currentPaceRaw > desiredPaceRaw)
        {
            return Math.Max(
                desiredPaceRaw, currentPaceRaw - decelerationStepRaw);
        }

        return currentPaceRaw;
    }

    /// <summary>
    /// Materializes a body-diameter-relative clearance radius as a raw
    /// <see langword="long"/> (design section 4.4), truncating toward zero
    /// after the widened multiply. Shared by the lane scan, the conflict
    /// pass, and the pursuit-support test, which all express their distances
    /// in basis points of body diameter.
    /// </summary>
    internal static long ClearanceRadiusRaw(
        int bodyRadiusRaw,
        int bodyDiametersBasisPoints)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bodyRadiusRaw);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            bodyDiametersBasisPoints);
        var bodyDiameterRaw = checked(2L * bodyRadiusRaw);
        return checked(bodyDiameterRaw * bodyDiametersBasisPoints) /
            BasisPointDenominator;
    }

    /// <summary>
    /// The offset-adjusted preferred distance of design section 4.4:
    /// <c>attackRangeRaw * (PreferredDistanceBasisPoints + offset cell) /
    /// 10000</c>, truncating toward zero. Construction validation keeps the
    /// adjusted basis points strictly positive.
    /// </summary>
    internal static long EffectivePreferredDistanceRaw(
        int attackRangeRaw,
        LoadoutMovementProfile actorProfile,
        int opponentCanonicalIndex)
    {
        ArgumentNullException.ThrowIfNull(actorProfile);
        var adjustedBasisPoints = checked(
            actorProfile.PreferredDistanceBasisPoints +
            actorProfile.OpponentDistanceOffsetBasisPoints[
                opponentCanonicalIndex]);
        return checked((long)attackRangeRaw * adjustedBasisPoints) /
            BasisPointDenominator;
    }

    /// <summary>
    /// Maps an equipment triple to its canonical loadout index — <c>KP</c>
    /// 0, <c>WA</c> 1, <c>KA</c> 2, <c>IT</c> 3, <c>KS</c> 4, <c>IS</c> 5 —
    /// the index the opponent-distance offset cells are declared in. The
    /// key is rank-independent, and an unmapped triple throws rather than
    /// resolving to a neighbouring row's spacing.
    /// </summary>
    internal static int CanonicalOpponentIndex(CombatLoadout loadout) =>
        (loadout.Weapon, loadout.Armor, loadout.Shield) switch
        {
            (WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None) => 0,
            (WeaponId.Wasay, ArmorId.LightOrganic, ShieldId.None) => 1,
            (WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.None) => 2,
            (WeaponId.Itak, ArmorId.LightOrganic, ShieldId.None) => 3,
            (WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.TallHardwood) => 4,
            (WeaponId.Itak, ArmorId.LightOrganic, ShieldId.TallHardwood) => 5,
            _ => throw new ArgumentOutOfRangeException(
                nameof(loadout),
                loadout,
                "No canonical opponent index exists for this equipment " +
                "triple."),
        };

    /// <summary>
    /// The number of occupied loadout buckets in one composition — the
    /// homogeneity test of design section 10.4's engage ordering, where two
    /// or more occupied enemy buckets put the oblique candidates ahead of
    /// the direct one.
    /// </summary>
    internal static int OccupiedLoadoutBuckets(
        in LoadoutCompositionCounts composition) =>
        (composition.Kampilan > 0 ? 1 : 0) +
        (composition.Wasay > 0 ? 1 : 0) +
        (composition.Kalis > 0 ? 1 : 0) +
        (composition.Itak > 0 ? 1 : 0) +
        (composition.KalisShield > 0 ? 1 : 0) +
        (composition.ItakShield > 0 ? 1 : 0);

    /// <summary>
    /// The conflict-pass ordering of design section 10.6, safest phase
    /// first: <c>Disengage, Recover, Commit, Regroup, Engage, Approach,
    /// Pursue</c>. Any other phase — reachable only defensively, since a
    /// refused or phaseless agent emits no proposal — sorts last.
    /// </summary>
    internal static int ConflictPhaseSafetyRank(FootworkPhase phase) =>
        phase switch
        {
            FootworkPhase.Disengage => 0,
            FootworkPhase.Recover => 1,
            FootworkPhase.Commit => 2,
            FootworkPhase.Regroup => 3,
            FootworkPhase.Engage => 4,
            FootworkPhase.Approach => 5,
            FootworkPhase.Pursue => 6,
            _ => MaximumConflictSafetyRank,
        };

    /// <summary>
    /// Runs the friendly-clearance conflict pass of design section 10.6
    /// over one faction's proposals: in phase-safety order, then ascending
    /// <c>EntityId</c>, accept a proposal only when its endpoint lies at or
    /// beyond the larger clearance radius from every already accepted
    /// endpoint — equality accepts, the same side as the lane test. The
    /// pass never reroutes and never changes a phase; the caller turns a
    /// rejection into a no-move.
    /// </summary>
    /// <param name="proposals">
    /// One faction's proposals in strictly ascending <c>EntityId</c> order,
    /// which combined with the per-rank passes below realises the design's
    /// total order without sorting.
    /// </param>
    /// <param name="accepted">
    /// Receives one flag per proposal. Must be at least as long as
    /// <paramref name="proposals"/>.
    /// </param>
    internal static void AcceptFriendlyClearanceConflicts(
        ReadOnlySpan<FriendlyClearanceProposal> proposals,
        Span<bool> accepted)
    {
        if (accepted.Length < proposals.Length)
        {
            throw new ArgumentException(
                "The accepted span must cover every proposal.",
                nameof(accepted));
        }

        for (var index = 1; index < proposals.Length; index++)
        {
            if (proposals[index].EntityId <= proposals[index - 1].EntityId)
            {
                throw new ArgumentException(
                    "Proposals must arrive in strictly ascending EntityId " +
                    "order; the phase-then-EntityId total order depends on " +
                    "it.",
                    nameof(proposals));
            }
        }

        accepted[..proposals.Length].Clear();

        for (var rank = 0; rank <= MaximumConflictSafetyRank; rank++)
        {
            for (var index = 0; index < proposals.Length; index++)
            {
                var candidate = proposals[index];
                if (ConflictPhaseSafetyRank(candidate.Phase) != rank)
                {
                    continue;
                }

                var isAccepted = true;
                for (var other = 0; other < proposals.Length; other++)
                {
                    if (!accepted[other])
                    {
                        continue;
                    }

                    var extant = proposals[other];
                    var required = Int128.Max(
                        candidate.ClearanceRadiusSquared,
                        extant.ClearanceRadiusSquared);
                    var separation = (Int128)CollisionGeometry.SquaredDistance(
                        candidate.EndpointXRaw,
                        candidate.EndpointYRaw,
                        extant.EndpointXRaw,
                        extant.EndpointYRaw);
                    if (separation < required)
                    {
                        isAccepted = false;
                        break;
                    }
                }

                accepted[index] = isAccepted;
            }
        }
    }

    /// <summary>
    /// Saturates a long coordinate into <see cref="int"/>, mirroring the
    /// simulation's own saturation before a boundary clamp. Safe because
    /// <see cref="StepEndpoint"/> always clamps the result to the map
    /// immediately afterward.
    /// </summary>
    private static int SaturateToInt32(long valueRaw) =>
        (int)Math.Clamp(valueRaw, int.MinValue, int.MaxValue);
}

/// <summary>
/// One agent's movement proposal as the friendly-clearance conflict pass of
/// design section 10.6 sees it: its stable identity, the committed footwork
/// phase that orders the pass, the proposed endpoint, and the squared
/// clearance radius its profile wants around allies. Derived scratch —
/// never hashed, never snapshotted, rebuilt every tick.
/// </summary>
internal readonly record struct FriendlyClearanceProposal(
    ulong EntityId,
    FootworkPhase Phase,
    int EndpointXRaw,
    int EndpointYRaw,
    Int128 ClearanceRadiusSquared);
