using System.Collections.Immutable;
using Hukbo.Core.Combat;

namespace Hukbo.Core.Movement;

/// <summary>
/// One immutable row of equipment-relative footwork configuration, validated
/// once at construction and never mutated. Declaration order of the
/// properties is load-bearing: it is the content-hash fold order of the
/// weapon-relative movement design, section 5, and may never be rearranged
/// once a preset ships. The bounds enforced here are engineering limits of
/// the basis-point model, not gameplay tuning and not historical claims; the
/// tuned values themselves live on the profile rows registered by a preset.
/// </summary>
/// <remarks>
/// The profile key is <c>(WeaponId, ArmorId, ShieldId)</c> and is
/// rank-independent (design section 4.1): rank is social standing,
/// documented as carrying no combat-strength value of its own, and it
/// carries no movement meaning either. The constructor therefore stores
/// <see cref="Loadout"/> with the default rank regardless of what the caller
/// passed, so two loadouts differing only in rank yield identical keys.
/// This type holds no mutable state, no derived cache, and no method that
/// takes a <c>Scenario</c>.
/// </remarks>
public sealed class LoadoutMovementProfile
{
    /// <summary>
    /// The number of cells <see cref="OpponentDistanceOffsetBasisPoints"/>
    /// must carry: one per rostered loadout, in canonical opponent order.
    /// </summary>
    public const int OpponentDistanceOffsetCount = 6;

    public LoadoutMovementProfile(
        CombatLoadout loadout,
        int forwardPaceBasisPoints,
        int lateralPaceBasisPoints,
        int backwardPaceBasisPoints,
        int committedPaceBasisPoints,
        int preferredDistanceBasisPoints,
        ImmutableArray<int> opponentDistanceOffsetBasisPoints,
        int maximumFacingStepsPerTick,
        int committedFacingStepsPerTick,
        int accelerationBasisPointsPerTick,
        int decelerationBasisPointsPerTick,
        int commitmentTicks,
        int recoveryTicks,
        int allyClearanceBodyDiametersBasisPoints,
        int disengageEnemyToAllyBasisPoints,
        int reengageEnemyToAllyBasisPoints,
        int pursuitSupportBodyDiametersBasisPoints)
    {
        // Design 4.3: every pace lies in the inclusive range [1, 10_000].
        // Inclusive at the top or the Itak row, whose forward pace is exactly
        // 10_000, fails construction.
        ValidateBasisPointRange(
            forwardPaceBasisPoints, nameof(forwardPaceBasisPoints));
        ValidateBasisPointRange(
            lateralPaceBasisPoints, nameof(lateralPaceBasisPoints));
        ValidateBasisPointRange(
            backwardPaceBasisPoints, nameof(backwardPaceBasisPoints));
        ValidateBasisPointRange(
            committedPaceBasisPoints, nameof(committedPaceBasisPoints));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            committedPaceBasisPoints,
            forwardPaceBasisPoints,
            nameof(committedPaceBasisPoints));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            preferredDistanceBasisPoints,
            nameof(preferredDistanceBasisPoints));

        if (opponentDistanceOffsetBasisPoints.IsDefault ||
            opponentDistanceOffsetBasisPoints.Length !=
                OpponentDistanceOffsetCount)
        {
            throw new ArgumentException(
                "Exactly " + OpponentDistanceOffsetCount + " signed offset " +
                "cells are required, indexed in canonical opponent order.",
                nameof(opponentDistanceOffsetBasisPoints));
        }

        foreach (var cell in opponentDistanceOffsetBasisPoints)
        {
            if (cell is < -2_000 or > 2_000)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(opponentDistanceOffsetBasisPoints),
                    cell,
                    "Every offset cell lies in the inclusive range " +
                    "[-2000, 2000].");
            }

            if (preferredDistanceBasisPoints + cell <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(opponentDistanceOffsetBasisPoints),
                    cell,
                    "The preferred distance plus every offset cell must " +
                    "stay strictly positive.");
            }
        }

        ValidateFacingStepRange(
            maximumFacingStepsPerTick, nameof(maximumFacingStepsPerTick));
        ValidateFacingStepRange(
            committedFacingStepsPerTick, nameof(committedFacingStepsPerTick));
        ValidateBasisPointRange(
            accelerationBasisPointsPerTick,
            nameof(accelerationBasisPointsPerTick));
        ValidateBasisPointRange(
            decelerationBasisPointsPerTick,
            nameof(decelerationBasisPointsPerTick));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            commitmentTicks, nameof(commitmentTicks));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            recoveryTicks, nameof(recoveryTicks));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            allyClearanceBodyDiametersBasisPoints,
            nameof(allyClearanceBodyDiametersBasisPoints));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            pursuitSupportBodyDiametersBasisPoints,
            nameof(pursuitSupportBodyDiametersBasisPoints));

        // Design 4.3: strictly less, so hysteresis always exists and a
        // warrior can never enter and leave disengagement on the same counts.
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(
            reengageEnemyToAllyBasisPoints,
            disengageEnemyToAllyBasisPoints,
            nameof(reengageEnemyToAllyBasisPoints));

        Loadout = new CombatLoadout(
            loadout.Weapon, loadout.Armor, loadout.Shield);
        ForwardPaceBasisPoints = forwardPaceBasisPoints;
        LateralPaceBasisPoints = lateralPaceBasisPoints;
        BackwardPaceBasisPoints = backwardPaceBasisPoints;
        CommittedPaceBasisPoints = committedPaceBasisPoints;
        PreferredDistanceBasisPoints = preferredDistanceBasisPoints;
        OpponentDistanceOffsetBasisPoints = opponentDistanceOffsetBasisPoints;
        MaximumFacingStepsPerTick = maximumFacingStepsPerTick;
        CommittedFacingStepsPerTick = committedFacingStepsPerTick;
        AccelerationBasisPointsPerTick = accelerationBasisPointsPerTick;
        DecelerationBasisPointsPerTick = decelerationBasisPointsPerTick;
        CommitmentTicks = commitmentTicks;
        RecoveryTicks = recoveryTicks;
        AllyClearanceBodyDiametersBasisPoints =
            allyClearanceBodyDiametersBasisPoints;
        DisengageEnemyToAllyBasisPoints = disengageEnemyToAllyBasisPoints;
        ReengageEnemyToAllyBasisPoints = reengageEnemyToAllyBasisPoints;
        PursuitSupportBodyDiametersBasisPoints =
            pursuitSupportBodyDiametersBasisPoints;
    }

    /// <summary>
    /// The row's key. Its <see cref="CombatLoadout.Rank"/> is always the
    /// default and is never read; the constructor drops whatever rank the
    /// caller supplied (design section 4.1).
    /// </summary>
    public CombatLoadout Loadout { get; }

    /// <summary>
    /// Pace cap, in basis points of <c>MovementSpeedRaw</c>, when the
    /// facing-to-travel separation is 0 or 1 sectors.
    /// </summary>
    public int ForwardPaceBasisPoints { get; }

    /// <summary>Pace cap when the separation is 2 through 5 sectors.</summary>
    public int LateralPaceBasisPoints { get; }

    /// <summary>Pace cap when the separation is 6 through 8 sectors.</summary>
    public int BackwardPaceBasisPoints { get; }

    /// <summary>
    /// Additional pace cap applied while the footwork phase is <c>Commit</c>.
    /// Never above <see cref="ForwardPaceBasisPoints"/>.
    /// </summary>
    public int CommittedPaceBasisPoints { get; }

    /// <summary>
    /// Basis points of the warrior's configured combat reach at which
    /// <c>Engage</c> is entered.
    /// </summary>
    public int PreferredDistanceBasisPoints { get; }

    /// <summary>
    /// Exactly six signed cells in canonical opponent order, added to
    /// <see cref="PreferredDistanceBasisPoints"/> for the selected
    /// opponent's loadout. Each cell lies in the inclusive range
    /// [-2000, 2000] and never cancels the preferred distance.
    /// </summary>
    public ImmutableArray<int> OpponentDistanceOffsetBasisPoints { get; }

    /// <summary>
    /// Sectors the warrior may turn in one ordinary tick, 0 through 8; eight
    /// is a half turn and the maximum meaningful value in a 16-sector model.
    /// </summary>
    public int MaximumFacingStepsPerTick { get; }

    /// <summary>
    /// Sectors the warrior may turn while the footwork phase is
    /// <c>Commit</c>, 0 through 8.
    /// </summary>
    public int CommittedFacingStepsPerTick { get; }

    /// <summary>
    /// Basis points of <c>MovementSpeedRaw</c> the retained pace may rise
    /// per tick.
    /// </summary>
    public int AccelerationBasisPointsPerTick { get; }

    /// <summary>
    /// Basis points of <c>MovementSpeedRaw</c> the retained pace may fall
    /// per tick.
    /// </summary>
    public int DecelerationBasisPointsPerTick { get; }

    /// <summary>
    /// <c>Commit</c> duration in ticks, counted inclusive of the entry tick.
    /// </summary>
    public int CommitmentTicks { get; }

    /// <summary>
    /// <c>Recover</c> duration in ticks, counted inclusive of the entry
    /// tick.
    /// </summary>
    public int RecoveryTicks { get; }

    /// <summary>
    /// Basis points of body diameter this warrior wants clear of allies.
    /// </summary>
    public int AllyClearanceBodyDiametersBasisPoints { get; }

    /// <summary>
    /// Enemy-to-ally ratio, in basis points, at or above which
    /// <c>Disengage</c> is entered. Always strictly above
    /// <see cref="ReengageEnemyToAllyBasisPoints"/>.
    /// </summary>
    public int DisengageEnemyToAllyBasisPoints { get; }

    /// <summary>
    /// Ratio at or below which <c>Disengage</c> is left. Always strictly
    /// below <see cref="DisengageEnemyToAllyBasisPoints"/>, so hysteresis
    /// always exists.
    /// </summary>
    public int ReengageEnemyToAllyBasisPoints { get; }

    /// <summary>
    /// Basis points of body diameter inside which an ally must remain for
    /// <c>Pursue</c> to keep proposing a direct route.
    /// </summary>
    public int PursuitSupportBodyDiametersBasisPoints { get; }

    private static void ValidateBasisPointRange(int value, string parameterName)
    {
        if (value is < 1 or > 10_000)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Basis-point values lie in the inclusive range [1, 10000].");
        }
    }

    private static void ValidateFacingStepRange(int value, string parameterName)
    {
        if (value is < 0 or > 8)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Facing steps lie in the inclusive range [0, 8]; eight is " +
                "a half turn, the maximum meaningful value in a 16-sector " +
                "model.");
        }
    }
}
