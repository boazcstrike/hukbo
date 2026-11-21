using Hukbo.Core.Determinism;
using Hukbo.Core.Mathematics;

namespace Hukbo.Core.Simulation;

/// <summary>
/// Every tunable of the separation pass. All shares are basis points of
/// <see cref="SeparationParameters.BasisPointScale"/>. Every value is a
/// provisional gameplay constant, never a measurement.
/// </summary>
/// <param name="BodyRadiusRaw">Body radius <c>R</c> in raw units. Two bodies touch at <c>2R</c>.</param>
/// <param name="SoftBandBasisPoints">The soft-band fraction <c>f</c>. The hard floor is <c>2R(1 - f)</c>.</param>
/// <param name="PushCapRadiusBasisPoints">The per-pass push cap <c>c</c>, as a fraction of <c>R</c>.</param>
/// <param name="StiffnessBasisPoints">Scales the averaged push.</param>
/// <param name="CommittedWeightMultiplier">Whole-number multiplier on the weight of a body committed to a fight.</param>
/// <param name="SidewaysBasisPoints">Sideways push inside the soft band, as a fraction of the overlap depth.</param>
/// <param name="ArenaWidthRaw">Arena width in raw units.</param>
/// <param name="ArenaHeightRaw">Arena height in raw units.</param>
internal readonly record struct SeparationParameters(
    int BodyRadiusRaw,
    int SoftBandBasisPoints,
    int PushCapRadiusBasisPoints,
    int StiffnessBasisPoints,
    int CommittedWeightMultiplier,
    int SidewaysBasisPoints,
    int ArenaWidthRaw,
    int ArenaHeightRaw)
{
    internal const int BasisPointScale = 10_000;

    /// <summary>
    /// Largest accepted body radius. A diameter of at most 2^29 keeps every
    /// squared centre distance the pass forms, including both axes of a
    /// neighbour up to two cells away, below 2^61, so no <see cref="long"/>
    /// product overflows.
    /// </summary>
    internal const int MaximumBodyRadiusRaw = 1 << 28;

    /// <summary>Contact distance <c>2R</c>.</summary>
    internal long DiameterRaw => 2L * BodyRadiusRaw;

    /// <summary>The hard floor <c>2R(1 - f)</c>, rounded down.</summary>
    internal long FloorRaw =>
        DiameterRaw * (BasisPointScale - SoftBandBasisPoints) / BasisPointScale;

    /// <summary>The longest push one body may take in one pass, <c>c * R</c>, rounded down.</summary>
    internal long PushCapRaw =>
        (long)BodyRadiusRaw * PushCapRadiusBasisPoints / BasisPointScale;

    /// <summary>The guaranteed pair distance <c>B = floor - 2 * cap</c>.</summary>
    internal long BoundRaw => FloorRaw - (2 * PushCapRaw);

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(BodyRadiusRaw);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(CommittedWeightMultiplier);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ArenaWidthRaw);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ArenaHeightRaw);
        ThrowIfOutsideBasisPoints(SoftBandBasisPoints, nameof(SoftBandBasisPoints));
        ThrowIfOutsideBasisPoints(StiffnessBasisPoints, nameof(StiffnessBasisPoints));
        ThrowIfOutsideBasisPoints(SidewaysBasisPoints, nameof(SidewaysBasisPoints));
        ThrowIfOutsideBasisPoints(
            PushCapRadiusBasisPoints,
            nameof(PushCapRadiusBasisPoints));

        if (BodyRadiusRaw > MaximumBodyRadiusRaw)
        {
            throw new ArgumentOutOfRangeException(
                nameof(BodyRadiusRaw),
                BodyRadiusRaw,
                $"Must be at most {MaximumBodyRadiusRaw}.");
        }

        var bound = BoundRaw;
        if (bound < 1 || bound + SeparationPass.ShrinkMarginRaw > DiameterRaw)
        {
            throw new ArgumentOutOfRangeException(
                nameof(PushCapRadiusBasisPoints),
                PushCapRadiusBasisPoints,
                $"The bound floor - 2 * cap is {bound}; it must be at least 1 and, with the "
                + $"shrink margin, at most the diameter {DiameterRaw}.");
        }
    }

    private static void ThrowIfOutsideBasisPoints(int value, string name)
    {
        if (value < 0 || value > BasisPointScale)
        {
            throw new ArgumentOutOfRangeException(name, value, "Must be within 0..10000.");
        }
    }
}

/// <summary>
/// The Jacobi separation pass of
/// <c>docs/plans/2026-11-21-soft-separation-design.md</c> section 3.1, under
/// <c>docs/decisions/2026-11-21-separating-contact.md</c> items 3 to 6. One pass
/// per tick, no convergence loop.
/// </summary>
/// <remarks>
/// <para>
/// Idea reference only: the push structure follows <c>SeparationJob</c> of
/// We March as One (decompiled source, <c>SeparationJob.cs</c> lines 251-647),
/// which is floating point. Nothing here is copied; every operation is integer.
/// </para>
/// <para>
/// <see cref="Run"/> copies the inputs into an internal snapshot, so every body
/// reads the same start-of-pass positions and writes only its own push. The
/// result therefore does not depend on storage order: each pair's
/// contribution is computed from its own two bodies, and integer addition is
/// commutative.
/// </para>
/// <para>
/// Rounding rules. The centre distance is the floor of the exact integer
/// square root of the squared raw distance (not <see cref="FixedPoint.Sqrt"/>,
/// whose Q22.10 pre-multiplication does not apply to a squared raw distance).
/// Every other division truncates toward zero. Each contact's push components
/// are divided once from an unrounded numerator and the per-contact results
/// are summed; the sum is then multiplied by the stiffness and divided by the
/// contact count and the basis-point scale. A push longer than the cap is
/// scaled by <c>cap / ceil(length)</c>, so the capped length never exceeds the
/// cap.
/// </para>
/// <para>
/// Inward-motion limit. After the push is capped and clamped to the arena, each
/// body's displacement <c>D</c> is scaled down, never up, so that no pair can
/// close below the bound <c>B = floor - 2 * cap</c> (<see
/// cref="SeparationParameters.FloorRaw"/> minus twice <see
/// cref="SeparationParameters.PushCapRaw"/>). For a neighbour at centre vector
/// <c>v</c> and floored distance <c>s</c>, the body may move toward it by a dot
/// product of at most <c>(s - B - m) * s / 2</c>, or not at all when
/// <c>s &lt; B + m</c>, where <c>m</c> is <see cref="ShrinkMarginRaw"/>.
/// </para>
/// <para>
/// Why the bound holds. Let <c>v</c> be the centre vector of a pair, <c>d</c>
/// its exact length, <c>s = floor(d)</c>, and <c>D_i</c>, <c>D_j</c> the final
/// displacements. Since <c>|v + w| * |v| &gt;= v . (v + w)</c>, the new
/// distance is at least <c>d - t_i - t_j</c>, where <c>t_i = D_i . v / d</c>
/// and <c>t_j = -D_j . v / d</c> are the components toward each other. Scaling
/// truncates each component toward zero, so a scaled displacement is
/// <c>f * D - e</c> with <c>0 &lt;= f &lt;= 1</c> and <c>|e| &lt; sqrt(2)</c>.
/// Hence <c>D' . v</c> is at most <c>f * max(D . v, 0) + sqrt(2) * d</c>, and
/// <c>f * D . v</c> never exceeds <c>allow</c>.
/// </para>
/// <list type="bullet">
/// <item>For <c>s &gt;= B + m</c>: <c>t &lt;= (s - B - m) / 2 + sqrt(2)</c> for
/// each body, so the new distance is at least <c>B + m - 2 * sqrt(2)</c>, which
/// is more than <c>B</c> because <c>m = 4</c> exceeds 2.83.</item>
/// <item>For <c>s &lt; B + m</c>: truncation can turn a displacement that was
/// not toward the neighbour, and so was skipped when the scale was chosen, into
/// a tiny displacement toward it. After scaling, a body with any such
/// neighbour is therefore stopped dead (<c>D' = 0</c>). Both bodies of the pair
/// then have <c>t &lt;= 0</c>, so the pair cannot get closer at all.</item>
/// <item>For <c>d &gt;= 2R</c>, which the limit never looks at: each
/// displacement is at most <c>cap</c> long (the arena clamp only shortens it
/// while the centre starts within <c>[R, W - R]</c>, and scaling shortens it
/// further), so the pair stays at least <c>2R - 2 * cap</c>, which is at least
/// <c>B</c>.</item>
/// </list>
/// <para>
/// Every step reads only the snapshot and the body's own displacement and takes
/// a minimum, so it is order-independent. Together with a resolver that never
/// ends a pair closer than the smaller of the floor and its start distance, it
/// makes the bound hold by induction across ticks.
/// </para>
/// <para>
/// Preconditions, not checked per pair: living bodies start with their centres
/// within <c>[R, W - R]</c> on each axis, the range the arena clamp keeps them
/// in. A start position outside it is clamped into an edge grid cell, which is
/// memory-safe but may miss contacts and voids the inward-motion proof. Entity
/// ids are unique; two bodies sharing an id at the same position throw
/// <see cref="InvalidOperationException"/>. Weights are within
/// 1..<see cref="MaximumWeight"/> for living bodies.
/// </para>
/// <para>
/// All storage is grow-only, so a warm pass allocates nothing. The grid
/// is a derived accelerator: never hashed, never snapshotted.
/// </para>
/// </remarks>
internal sealed class SeparationPass
{
    internal const int MaximumWeight = 1 << 20;

    /// <summary>
    /// Slack, in raw units, held back from the inward-motion limit so that the
    /// truncation of a scaled displacement cannot take a pair below the bound.
    /// </summary>
    internal const int ShrinkMarginRaw = 4;

    private const int DirectionCount = 16;
    private const int DirectionIndexShift = 60;
    private const int NoBody = -1;

    // Q22.10 unit vectors at 22.5 degree steps, counter-clockwise from +X:
    // round(cos * 1024), round(sin * 1024). A coincident pair picks one by hash.
    private static readonly int[] DirectionX =
    [
        1024, 946, 724, 392, 0, -392, -724, -946,
        -1024, -946, -724, -392, 0, 392, 724, 946,
    ];

    private static readonly int[] DirectionY =
    [
        0, 392, 724, 946, 1024, 946, 724, 392,
        0, -392, -724, -946, -1024, -946, -724, -392,
    ];

    private int _count;
    private int _cellsX;
    private int _cellsY;
    private long _cellSize;
    private int[] _x = [];
    private int[] _y = [];
    private bool[] _alive = [];
    private ulong[] _id = [];
    private long[] _effectiveWeight = [];
    private int[] _cellOf = [];
    private int[] _next = [];
    private int[] _cellHead = [];
    private int[] _pushX = [];
    private int[] _pushY = [];
    private int[] _outX = [];
    private int[] _outY = [];

    /// <summary>Position after the pass, clamped to the arena, indexed like the input. A dead body keeps its input position.</summary>
    internal ReadOnlySpan<int> OutX => _outX.AsSpan(0, _count);

    internal ReadOnlySpan<int> OutY => _outY.AsSpan(0, _count);

    /// <summary>How many bodies the inward-motion limit scaled or stopped in the last pass.</summary>
    internal int LimitedBodyCount { get; private set; }

    /// <summary>True when no grid cell still points at a body. Holds between passes, including after a failed one.</summary>
    internal bool GridIsClear => _cellHead.AsSpan().IndexOfAnyExcept(NoBody) < 0;

    /// <summary>The capped push before the arena clamp and the inward-motion limit. Zero for a dead body.</summary>
    internal ReadOnlySpan<int> PushX => _pushX.AsSpan(0, _count);

    internal ReadOnlySpan<int> PushY => _pushY.AsSpan(0, _count);

    /// <summary>
    /// Runs one pass over the bodies. All six spans must have the same length.
    /// </summary>
    internal void Run(
        ReadOnlySpan<int> xRaw,
        ReadOnlySpan<int> yRaw,
        ReadOnlySpan<bool> isAlive,
        ReadOnlySpan<int> weight,
        ReadOnlySpan<bool> isCommitted,
        ReadOnlySpan<ulong> entityId,
        in SeparationParameters parameters)
    {
        parameters.Validate();
        var count = xRaw.Length;
        if (yRaw.Length != count
            || isAlive.Length != count
            || weight.Length != count
            || isCommitted.Length != count
            || entityId.Length != count)
        {
            throw new ArgumentException("All body spans must have the same length.");
        }

        _count = count;
        LimitedBodyCount = 0;
        EnsureBodyCapacity(count);
        Snapshot(xRaw, yRaw, isAlive, weight, isCommitted, entityId, parameters);
        SizeGrid(parameters);

        // Writing NoBody into any in-range cell is always safe, so the clear
        // runs even when a later step throws and leaves the grid half linked.
        try
        {
            LinkBodies();
            ComputePushes(parameters);
            ApplyPushes(parameters);
        }
        finally
        {
            ClearGrid();
        }
    }

    private void EnsureBodyCapacity(int count)
    {
        if (_x.Length >= count)
        {
            return;
        }

        _x = new int[count];
        _y = new int[count];
        _alive = new bool[count];
        _id = new ulong[count];
        _effectiveWeight = new long[count];
        _cellOf = new int[count];
        _next = new int[count];
        _pushX = new int[count];
        _pushY = new int[count];
        _outX = new int[count];
        _outY = new int[count];
    }

    private void Snapshot(
        ReadOnlySpan<int> xRaw,
        ReadOnlySpan<int> yRaw,
        ReadOnlySpan<bool> isAlive,
        ReadOnlySpan<int> weight,
        ReadOnlySpan<bool> isCommitted,
        ReadOnlySpan<ulong> entityId,
        in SeparationParameters parameters)
    {
        for (var i = 0; i < _count; i++)
        {
            _x[i] = xRaw[i];
            _y[i] = yRaw[i];
            _alive[i] = isAlive[i];
            _id[i] = entityId[i];
            _pushX[i] = 0;
            _pushY[i] = 0;

            if (!isAlive[i])
            {
                continue;
            }

            if (weight[i] < 1 || weight[i] > MaximumWeight)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(weight),
                    weight[i],
                    "A living body's weight must be within 1..MaximumWeight.");
            }

            _effectiveWeight[i] = isCommitted[i]
                ? (long)weight[i] * parameters.CommittedWeightMultiplier
                : weight[i];
        }
    }

    private void SizeGrid(in SeparationParameters parameters)
    {
        _cellSize = parameters.DiameterRaw;
        _cellsX = checked((int)(parameters.ArenaWidthRaw / _cellSize) + 1);
        _cellsY = checked((int)(parameters.ArenaHeightRaw / _cellSize) + 1);
        var cellCount = checked(_cellsX * _cellsY);
        if (_cellHead.Length < cellCount)
        {
            _cellHead = new int[cellCount];
            Array.Fill(_cellHead, NoBody);
        }
    }

    private void LinkBodies()
    {
        for (var i = 0; i < _count; i++)
        {
            if (!_alive[i])
            {
                continue;
            }

            var cellX = (int)Math.Clamp(_x[i] / _cellSize, 0, _cellsX - 1);
            var cellY = (int)Math.Clamp(_y[i] / _cellSize, 0, _cellsY - 1);
            var cell = (cellY * _cellsX) + cellX;
            _cellOf[i] = cell;
            _next[i] = _cellHead[cell];
            _cellHead[cell] = i;
        }
    }

    // Restores the all-NoBody invariant by touching only the cells this pass used.
    // A body a failed pass never linked holds a stale but in-range cell index.
    private void ClearGrid()
    {
        for (var i = 0; i < _count; i++)
        {
            if (_alive[i])
            {
                _cellHead[_cellOf[i]] = NoBody;
            }
        }
    }

    private void ComputePushes(in SeparationParameters parameters)
    {
        for (var i = 0; i < _count; i++)
        {
            if (!_alive[i])
            {
                continue;
            }

            long sumX = 0;
            long sumY = 0;
            var contacts = 0;
            var cellX = _cellOf[i] % _cellsX;
            var cellY = _cellOf[i] / _cellsX;
            var lastCellY = Math.Min(cellY + 1, _cellsY - 1);
            var lastCellX = Math.Min(cellX + 1, _cellsX - 1);

            for (var y = Math.Max(cellY - 1, 0); y <= lastCellY; y++)
            {
                for (var x = Math.Max(cellX - 1, 0); x <= lastCellX; x++)
                {
                    AccumulateCell(
                        i,
                        (y * _cellsX) + x,
                        parameters,
                        ref sumX,
                        ref sumY,
                        ref contacts);
                }
            }

            StorePush(i, sumX, sumY, contacts, parameters);
        }
    }

    private void AccumulateCell(
        int self,
        int cell,
        in SeparationParameters parameters,
        ref long sumX,
        ref long sumY,
        ref int contacts)
    {
        var diameter = parameters.DiameterRaw;
        for (var other = _cellHead[cell]; other != NoBody; other = _next[other])
        {
            if (other == self)
            {
                continue;
            }

            var deltaX = (long)_x[self] - _x[other];
            var deltaY = (long)_y[self] - _y[other];
            if (Math.Abs(deltaX) >= diameter || Math.Abs(deltaY) >= diameter)
            {
                continue;
            }

            var distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);
            if (distanceSquared >= diameter * diameter)
            {
                continue;
            }

            AddContactPush(
                self,
                other,
                deltaX,
                deltaY,
                distanceSquared,
                parameters,
                ref sumX,
                ref sumY);
            contacts++;
        }
    }

    // Pushes `self` away from `other` along the line between centres by
    // overlap * w_other / (w_self + w_other), plus the soft-band sideways term.
    private void AddContactPush(
        int self,
        int other,
        long deltaX,
        long deltaY,
        long distanceSquared,
        in SeparationParameters parameters,
        ref long sumX,
        ref long sumY)
    {
        var distance = FixedPoint.IntegerSquareRoot(distanceSquared);
        var overlap = parameters.DiameterRaw - distance;
        var normalX = deltaX;
        var normalY = deltaY;
        var normalLength = distance;
        if (distance == 0)
        {
            (normalX, normalY) = CoincidentDirection(_id[self], _id[other]);
            normalLength = FixedPoint.IntegerSquareRoot(
                (normalX * normalX) + (normalY * normalY));
        }

        var shareDenominator = normalLength * (_effectiveWeight[self] + _effectiveWeight[other]);
        sumX += ScaledDivide((Int128)overlap * _effectiveWeight[other] * normalX, shareDenominator);
        sumY += ScaledDivide((Int128)overlap * _effectiveWeight[other] * normalY, shareDenominator);

        if (distance <= parameters.FloorRaw)
        {
            return;
        }

        // Rotating each body's own outward normal by +90 degrees gives the two
        // bodies of a pair opposite sideways pushes, so the pair slides past.
        var sidewaysDenominator = normalLength * SeparationParameters.BasisPointScale;
        var sidewaysScale = (Int128)overlap * parameters.SidewaysBasisPoints;
        sumX += ScaledDivide(-normalY * sidewaysScale, sidewaysDenominator);
        sumY += ScaledDivide(normalX * sidewaysScale, sidewaysDenominator);
    }

    // The lower id gets +direction and the higher id -direction, so the two
    // bodies of a coincident pair always separate. The direction is one of 16
    // fixed vectors picked by the top four bits of FNV-1a over (low id, high id).
    private static (long X, long Y) CoincidentDirection(ulong selfId, ulong otherId)
    {
        if (selfId == otherId)
        {
            throw new InvalidOperationException(
                $"Two living bodies share entity id {selfId} at the same position; ids must be unique.");
        }

        var hash = Fnv1a.OffsetBasis;
        Fnv1a.Add(ref hash, Math.Min(selfId, otherId));
        Fnv1a.Add(ref hash, Math.Max(selfId, otherId));
        var index = (int)(hash >> DirectionIndexShift) % DirectionCount;
        return selfId < otherId
            ? (DirectionX[index], DirectionY[index])
            : (-DirectionX[index], -DirectionY[index]);
    }

    private void StorePush(
        int self,
        long sumX,
        long sumY,
        int contacts,
        in SeparationParameters parameters)
    {
        if (contacts == 0)
        {
            return;
        }

        var denominator = (long)contacts * SeparationParameters.BasisPointScale;
        var pushX = sumX * parameters.StiffnessBasisPoints / denominator;
        var pushY = sumY * parameters.StiffnessBasisPoints / denominator;

        var cap = parameters.PushCapRaw;
        var lengthSquared = (pushX * pushX) + (pushY * pushY);
        if (lengthSquared > cap * cap)
        {
            var length = FixedPoint.IntegerSquareRoot(lengthSquared);
            if (length * length < lengthSquared)
            {
                length++;
            }

            pushX = pushX * cap / length;
            pushY = pushY * cap / length;
        }

        _pushX[self] = checked((int)pushX);
        _pushY[self] = checked((int)pushY);
    }

    private void ApplyPushes(in SeparationParameters parameters)
    {
        var bound = parameters.BoundRaw;
        for (var i = 0; i < _count; i++)
        {
            if (!_alive[i])
            {
                _outX[i] = _x[i];
                _outY[i] = _y[i];
                continue;
            }

            // Clamp first and limit second: the arena is convex, so scaling a
            // clamped displacement toward zero cannot leave it.
            var deltaX = (long)CollisionGeometry.ClampCenterToBounds(
                checked(_x[i] + _pushX[i]),
                parameters.ArenaWidthRaw,
                parameters.BodyRadiusRaw) - _x[i];
            var deltaY = (long)CollisionGeometry.ClampCenterToBounds(
                checked(_y[i] + _pushY[i]),
                parameters.ArenaHeightRaw,
                parameters.BodyRadiusRaw) - _y[i];

            LimitInwardMotion(i, bound, parameters, ref deltaX, ref deltaY);

            _outX[i] = checked((int)(_x[i] + deltaX));
            _outY[i] = checked((int)(_y[i] + deltaY));
        }
    }

    // Scales (deltaX, deltaY) by the tightest fraction any overlapping
    // neighbour allows. The fraction is kept as numerator / denominator and
    // compared by cross-multiplication, so the minimum is exact and the result
    // does not depend on the order neighbours are visited in.
    private void LimitInwardMotion(
        int self,
        long bound,
        in SeparationParameters parameters,
        ref long deltaX,
        ref long deltaY)
    {
        if (deltaX == 0 && deltaY == 0)
        {
            return;
        }

        long numerator = 1;
        long denominator = 1;
        var cellX = _cellOf[self] % _cellsX;
        var cellY = _cellOf[self] / _cellsX;
        var lastCellY = Math.Min(cellY + 1, _cellsY - 1);
        var lastCellX = Math.Min(cellX + 1, _cellsX - 1);

        for (var y = Math.Max(cellY - 1, 0); y <= lastCellY; y++)
        {
            for (var x = Math.Max(cellX - 1, 0); x <= lastCellX; x++)
            {
                TightenScale(
                    self,
                    (y * _cellsX) + x,
                    bound,
                    parameters,
                    deltaX,
                    deltaY,
                    ref numerator,
                    ref denominator);
            }
        }

        if (numerator >= denominator)
        {
            return;
        }

        deltaX = deltaX * numerator / denominator;
        deltaY = deltaY * numerator / denominator;
        LimitedBodyCount++;

        if (MovesTowardTightNeighbour(self, bound, parameters, deltaX, deltaY))
        {
            deltaX = 0;
            deltaY = 0;
        }
    }

    // Truncating the scaled displacement can leave a component toward a
    // neighbour closer than B + m that the original displacement was not moving
    // toward, and so was skipped. Such a neighbour allows no inward motion.
    private bool MovesTowardTightNeighbour(
        int self,
        long bound,
        in SeparationParameters parameters,
        long deltaX,
        long deltaY)
    {
        var diameter = parameters.DiameterRaw;
        var cellX = _cellOf[self] % _cellsX;
        var cellY = _cellOf[self] / _cellsX;
        var lastCellY = Math.Min(cellY + 1, _cellsY - 1);
        var lastCellX = Math.Min(cellX + 1, _cellsX - 1);

        for (var y = Math.Max(cellY - 1, 0); y <= lastCellY; y++)
        {
            for (var x = Math.Max(cellX - 1, 0); x <= lastCellX; x++)
            {
                for (var other = _cellHead[(y * _cellsX) + x]; other != NoBody; other = _next[other])
                {
                    var toOtherX = (long)_x[other] - _x[self];
                    var toOtherY = (long)_y[other] - _y[self];
                    var distanceSquared = (toOtherX * toOtherX) + (toOtherY * toOtherY);
                    if (other == self
                        || distanceSquared >= diameter * diameter
                        || (deltaX * toOtherX) + (deltaY * toOtherY) <= 0)
                    {
                        continue;
                    }

                    if (FixedPoint.IntegerSquareRoot(distanceSquared) < bound + ShrinkMarginRaw)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private void TightenScale(
        int self,
        int cell,
        long bound,
        in SeparationParameters parameters,
        long deltaX,
        long deltaY,
        ref long numerator,
        ref long denominator)
    {
        var diameter = parameters.DiameterRaw;
        for (var other = _cellHead[cell]; other != NoBody; other = _next[other])
        {
            if (other == self)
            {
                continue;
            }

            var toOtherX = (long)_x[other] - _x[self];
            var toOtherY = (long)_y[other] - _y[self];
            var distanceSquared = (toOtherX * toOtherX) + (toOtherY * toOtherY);
            if (distanceSquared >= diameter * diameter)
            {
                continue;
            }

            var toward = (deltaX * toOtherX) + (deltaY * toOtherY);
            if (toward <= 0)
            {
                continue;
            }

            var distance = FixedPoint.IntegerSquareRoot(distanceSquared);
            var allow = distance >= bound + ShrinkMarginRaw
                ? (distance - bound - ShrinkMarginRaw) * distance / 2
                : 0;
            if (toward <= allow)
            {
                continue;
            }

            // allow / toward is below one; keep it if it is the smallest so far.
            if (allow * denominator < numerator * toward)
            {
                numerator = allow;
                denominator = toward;
            }
        }
    }

    private static long ScaledDivide(Int128 numerator, long denominator) =>
        checked((long)(numerator / denominator));
}
