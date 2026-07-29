using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests;

/// <summary>
/// The solid resolver's algorithm written out in its unaccelerated form: every
/// obstacle query is a linear scan over the whole candidate set, with no spatial
/// index of any kind.
/// </summary>
/// <remarks>
/// <para>
/// This is the oracle for the collision scaling work. That change replaced two
/// linear scans inside <c>CollisionResolver.IsFree</c> with bounded uniform-grid
/// queries, on the argument that an existential question over a finite set does
/// not depend on the order the set is enumerated in. The argument predicts that
/// the resolver's whole result list is unchanged, and this type is what lets a
/// test check that prediction directly rather than only through a state hash.
/// </para>
/// <para>
/// It is a transcription of the resolver as it stood at <c>a6ca2a8</c>, before
/// the pending index existed, and it is deliberately dumb: the commit order, the
/// candidate ladder, the truncation arithmetic, the boundary clamp, and the
/// co-location repair are all reproduced exactly, and only the obstacle lookup
/// is written the slow way. A divergence therefore localizes to the lookup.
/// </para>
/// <para>
/// It is test-only and is not a second implementation anybody is expected to
/// maintain in step with gameplay changes. If the ladder itself ever changes,
/// this file changes with it or the test that consumes it is deleted.
/// </para>
/// </remarks>
internal static class NaiveCollisionResolution
{
    /// <summary>Mirrors <c>CollisionResolver.MaximumTruncationRungs</c>.</summary>
    private const int MaximumTruncationRungs = 11;

    /// <summary>Mirrors <c>CollisionResolver.SeparationDirections</c>.</summary>
    private static readonly (int X, int Y)[] SeparationDirections =
    [
        (1, 0),
        (-1, 0),
        (0, 1),
        (0, -1),
    ];

    /// <summary>
    /// Resolves one tick and returns one result per request, in request order.
    /// </summary>
    internal static CollisionMoveResult[] Resolve(
        IReadOnlyList<CollisionMoveRequest> requests,
        int bodyRadiusRaw,
        int mapWidthRaw,
        int mapHeightRaw)
    {
        var diameterRaw = 2 * bodyRadiusRaw;
        var results = new CollisionMoveResult[requests.Count];
        var committed = new List<CollisionBody>();
        var moverIndices = new List<int>();

        for (var index = 0; index < requests.Count; index++)
        {
            if (requests[index].HasProposal)
            {
                moverIndices.Add(index);
            }
        }

        // The entity ID is stamped into the low half exactly as the resolver
        // does it, so equal high halves cannot permute.
        var keys = new ulong[moverIndices.Count];
        var order = new int[moverIndices.Count];

        for (var moverIndex = 0; moverIndex < moverIndices.Count; moverIndex++)
        {
            var request = requests[moverIndices[moverIndex]];

            keys[moverIndex] =
                (request.PriorityKey & 0xFFFF_FFFF_0000_0000UL) |
                (request.EntityId & 0xFFFF_FFFFUL);
            order[moverIndex] = moverIndices[moverIndex];
        }

        Array.Sort(keys, order);

        // Pass one: stationary bodies, ascending entity ID, repairing an exact
        // co-location on the way.
        for (var index = 0; index < requests.Count; index++)
        {
            var request = requests[index];

            if (request.HasProposal)
            {
                continue;
            }

            var xRaw = request.StartXRaw;
            var yRaw = request.StartYRaw;
            var resolution = MovementResolution.None;

            if (IsCoincidentWithCommitted(committed, xRaw, yRaw, request.EntityId))
            {
                resolution = TrySeparate(
                    requests,
                    committed,
                    order,
                    request,
                    bodyRadiusRaw,
                    diameterRaw,
                    mapWidthRaw,
                    mapHeightRaw,
                    out var separatedXRaw,
                    out var separatedYRaw)
                    ? MovementResolution.Separated
                    : MovementResolution.Blocked;

                if (resolution == MovementResolution.Separated)
                {
                    xRaw = separatedXRaw;
                    yRaw = separatedYRaw;
                }
            }

            results[index] = new CollisionMoveResult(
                request.EntityId,
                xRaw,
                yRaw,
                resolution);
            committed.Add(new CollisionBody(request.EntityId, xRaw, yRaw, IsAlive: true));
        }

        // Pass two: movers, ascending priority key.
        for (var moverIndex = 0; moverIndex < order.Length; moverIndex++)
        {
            var requestIndex = order[moverIndex];
            var request = requests[requestIndex];
            var pendingFrom = moverIndex + 1;

            if (TryAccept(
                requests,
                committed,
                order,
                request,
                request.PreferredXRaw,
                request.PreferredYRaw,
                pendingFrom,
                rejectStartPosition: false,
                bodyRadiusRaw,
                mapWidthRaw,
                mapHeightRaw,
                out var xRaw,
                out var yRaw))
            {
                Commit(results, committed, requestIndex, request, xRaw, yRaw, MovementResolution.Moved);
                continue;
            }

            if (TryAccept(
                    requests,
                    committed,
                    order,
                    request,
                    request.PreferredXRaw,
                    request.StartYRaw,
                    pendingFrom,
                    rejectStartPosition: true,
                    bodyRadiusRaw,
                    mapWidthRaw,
                    mapHeightRaw,
                    out xRaw,
                    out yRaw) ||
                TryAccept(
                    requests,
                    committed,
                    order,
                    request,
                    request.StartXRaw,
                    request.PreferredYRaw,
                    pendingFrom,
                    rejectStartPosition: true,
                    bodyRadiusRaw,
                    mapWidthRaw,
                    mapHeightRaw,
                    out xRaw,
                    out yRaw))
            {
                Commit(results, committed, requestIndex, request, xRaw, yRaw, MovementResolution.Slid);
                continue;
            }

            if (TryTruncate(
                requests,
                committed,
                order,
                request,
                pendingFrom,
                bodyRadiusRaw,
                mapWidthRaw,
                mapHeightRaw,
                out xRaw,
                out yRaw))
            {
                Commit(results, committed, requestIndex, request, xRaw, yRaw, MovementResolution.Truncated);
                continue;
            }

            Commit(
                results,
                committed,
                requestIndex,
                request,
                request.StartXRaw,
                request.StartYRaw,
                MovementResolution.Blocked);
        }

        return results;
    }

    private static void Commit(
        CollisionMoveResult[] results,
        List<CollisionBody> committed,
        int requestIndex,
        in CollisionMoveRequest request,
        int xRaw,
        int yRaw,
        MovementResolution resolution)
    {
        results[requestIndex] = new CollisionMoveResult(
            request.EntityId,
            xRaw,
            yRaw,
            resolution);
        committed.Add(new CollisionBody(request.EntityId, xRaw, yRaw, IsAlive: true));
    }

    private static bool TryTruncate(
        IReadOnlyList<CollisionMoveRequest> requests,
        List<CollisionBody> committed,
        int[] order,
        in CollisionMoveRequest request,
        int pendingFrom,
        int bodyRadiusRaw,
        int mapWidthRaw,
        int mapHeightRaw,
        out int xRaw,
        out int yRaw)
    {
        var deltaXRaw = (long)request.PreferredXRaw - request.StartXRaw;
        var deltaYRaw = (long)request.PreferredYRaw - request.StartYRaw;
        var distanceRaw = IntegerSquareRoot(
            checked((deltaXRaw * deltaXRaw) + (deltaYRaw * deltaYRaw)));

        if (distanceRaw > 0)
        {
            for (var rung = 1; rung <= MaximumTruncationRungs; rung++)
            {
                var lengthRaw = distanceRaw >> rung;

                if (lengthRaw < 1)
                {
                    break;
                }

                var stepXRaw = deltaXRaw * lengthRaw / distanceRaw;
                var stepYRaw = deltaYRaw * lengthRaw / distanceRaw;

                if (stepXRaw == 0 && stepYRaw == 0)
                {
                    continue;
                }

                if (TryAccept(
                    requests,
                    committed,
                    order,
                    request,
                    request.StartXRaw + stepXRaw,
                    request.StartYRaw + stepYRaw,
                    pendingFrom,
                    rejectStartPosition: true,
                    bodyRadiusRaw,
                    mapWidthRaw,
                    mapHeightRaw,
                    out xRaw,
                    out yRaw))
                {
                    return true;
                }
            }
        }

        xRaw = request.StartXRaw;
        yRaw = request.StartYRaw;
        return false;
    }

    private static bool TryAccept(
        IReadOnlyList<CollisionMoveRequest> requests,
        List<CollisionBody> committed,
        int[] order,
        in CollisionMoveRequest request,
        long candidateXRaw,
        long candidateYRaw,
        int pendingFrom,
        bool rejectStartPosition,
        int bodyRadiusRaw,
        int mapWidthRaw,
        int mapHeightRaw,
        out int xRaw,
        out int yRaw)
    {
        xRaw = CollisionGeometry.ClampCenterToBounds(
            ToCoordinate(candidateXRaw),
            mapWidthRaw,
            bodyRadiusRaw);
        yRaw = CollisionGeometry.ClampCenterToBounds(
            ToCoordinate(candidateYRaw),
            mapHeightRaw,
            bodyRadiusRaw);

        if (rejectStartPosition &&
            xRaw == request.StartXRaw &&
            yRaw == request.StartYRaw)
        {
            return false;
        }

        return IsFree(
            requests,
            committed,
            order,
            xRaw,
            yRaw,
            request.EntityId,
            pendingFrom,
            bodyRadiusRaw);
    }

    private static bool TrySeparate(
        IReadOnlyList<CollisionMoveRequest> requests,
        List<CollisionBody> committed,
        int[] order,
        in CollisionMoveRequest request,
        int bodyRadiusRaw,
        int diameterRaw,
        int mapWidthRaw,
        int mapHeightRaw,
        out int xRaw,
        out int yRaw)
    {
        foreach (var direction in SeparationDirections)
        {
            var candidateXRaw = (long)request.StartXRaw + ((long)direction.X * diameterRaw);
            var candidateYRaw = (long)request.StartYRaw + ((long)direction.Y * diameterRaw);

            if (!IsInsideBounds(
                candidateXRaw,
                candidateYRaw,
                bodyRadiusRaw,
                mapWidthRaw,
                mapHeightRaw))
            {
                continue;
            }

            xRaw = (int)candidateXRaw;
            yRaw = (int)candidateYRaw;

            if (IsFree(
                requests,
                committed,
                order,
                xRaw,
                yRaw,
                request.EntityId,
                pendingFrom: 0,
                bodyRadiusRaw))
            {
                return true;
            }
        }

        xRaw = request.StartXRaw;
        yRaw = request.StartYRaw;
        return false;
    }

    /// <summary>
    /// The linear form of the obstacle test: every committed body, then every
    /// pending mover at its tick-start position. This is the method the scaling
    /// work replaced, and it is the whole reason this file exists.
    /// </summary>
    private static bool IsFree(
        IReadOnlyList<CollisionMoveRequest> requests,
        List<CollisionBody> committed,
        int[] order,
        int xRaw,
        int yRaw,
        ulong entityId,
        int pendingFrom,
        int bodyRadiusRaw)
    {
        for (var index = 0; index < committed.Count; index++)
        {
            var body = committed[index];

            if (body.EntityId != entityId &&
                CollisionGeometry.Overlaps(
                    xRaw,
                    yRaw,
                    body.XRaw,
                    body.YRaw,
                    bodyRadiusRaw))
            {
                return false;
            }
        }

        for (var moverIndex = pendingFrom; moverIndex < order.Length; moverIndex++)
        {
            var pending = requests[order[moverIndex]];

            if (pending.EntityId != entityId &&
                CollisionGeometry.Overlaps(
                    xRaw,
                    yRaw,
                    pending.StartXRaw,
                    pending.StartYRaw,
                    bodyRadiusRaw))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsCoincidentWithCommitted(
        List<CollisionBody> committed,
        int xRaw,
        int yRaw,
        ulong entityId)
    {
        for (var index = 0; index < committed.Count; index++)
        {
            var body = committed[index];

            if (body.EntityId != entityId &&
                CollisionGeometry.IsCoincident(xRaw, yRaw, body.XRaw, body.YRaw))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInsideBounds(
        long xRaw,
        long yRaw,
        int bodyRadiusRaw,
        int mapWidthRaw,
        int mapHeightRaw) =>
        xRaw >= bodyRadiusRaw &&
        xRaw <= (long)mapWidthRaw - bodyRadiusRaw &&
        yRaw >= bodyRadiusRaw &&
        yRaw <= (long)mapHeightRaw - bodyRadiusRaw;

    private static int ToCoordinate(long valueRaw) =>
        (int)Math.Clamp(valueRaw, int.MinValue, int.MaxValue);

    /// <summary>
    /// Mirrors <c>CollisionResolver.IntegerSquareRoot</c>. Floating point is
    /// banned anywhere near the state hash, so the oracle uses the same integer
    /// method rather than <see cref="Math.Sqrt"/>.
    /// </summary>
    private static long IntegerSquareRoot(long value)
    {
        if (value <= 0)
        {
            return 0;
        }

        var remainder = value;
        var result = 0L;
        var bit = 1L << 62;

        while (bit > remainder)
        {
            bit >>= 2;
        }

        while (bit != 0)
        {
            if (remainder >= result + bit)
            {
                remainder -= result + bit;
                result = (result >> 1) + bit;
            }
            else
            {
                result >>= 1;
            }

            bit >>= 2;
        }

        return result;
    }
}
