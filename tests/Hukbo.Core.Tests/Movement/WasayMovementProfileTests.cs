using Hukbo.Core.Movement;
using Hukbo.Core.Movement.Profiles;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// The approved calibration envelope of the solo Wasay (<c>WA</c>)
/// equipment-relative movement row: the range every one of its fields is
/// allowed to move inside, so a later retuning pass that leaves an approved
/// band fails here by the name of the field it moved. Every value asserted
/// here is a provisional reconstruction: gameplay tuning; no historical
/// measurement. The evidence behind the row, and the reasoning for each
/// figure, lives in docs/research/movement/wasay.md and in the calibration
/// table of the wasay movement plan section 5. Nothing in this file is a claim
/// about how a sixteenth-century warrior actually moved.
/// </summary>
/// <remarks>
/// The row's exact shipped values are pinned by
/// <c>MovementProfileRegistrationTests</c>, and its equipment-only loadout key,
/// its canonical position at index 1 of
/// <see cref="MovementPresetId.EquipmentRelativeFootworkV6"/>, its rank
/// independence, and its unreachability under a legacy preset are pinned for
/// every canonical row at once by
/// <see cref="MovementProfileRowContractTests"/>. This file asserted all of
/// that a second time for the Wasay row alone; what is left is the calibration
/// table, which is the one thing here that is genuinely about the Wasay.
/// </remarks>
public sealed class WasayMovementProfileTests
{
    /// <summary>
    /// Every scalar of the row sits inside the calibration range approved in
    /// the table of the wasay movement plan section 5.
    /// A later retuning pass that moves a value outside its approved range
    /// fails here by name,
    /// rather than quietly shipping a row nobody agreed to. Both bounds are
    /// inclusive, and every range is a gameplay-tuning decision rather than a
    /// historical measurement.
    /// </summary>
    [Theory]
    [InlineData(nameof(LoadoutMovementProfile.ForwardPaceBasisPoints), 9_000, 9_800)]
    [InlineData(nameof(LoadoutMovementProfile.LateralPaceBasisPoints), 6_500, 8_200)]
    [InlineData(nameof(LoadoutMovementProfile.BackwardPaceBasisPoints), 5_500, 7_200)]
    [InlineData(nameof(LoadoutMovementProfile.CommittedPaceBasisPoints), 2_000, 3_500)]
    [InlineData(
        nameof(LoadoutMovementProfile.PreferredDistanceBasisPoints), 10_000, 12_000)]
    [InlineData(nameof(LoadoutMovementProfile.MaximumFacingStepsPerTick), 1, 2)]
    [InlineData(nameof(LoadoutMovementProfile.CommittedFacingStepsPerTick), 1, 1)]
    [InlineData(
        nameof(LoadoutMovementProfile.AccelerationBasisPointsPerTick), 3_000, 6_000)]
    [InlineData(
        nameof(LoadoutMovementProfile.DecelerationBasisPointsPerTick), 3_500, 7_000)]
    [InlineData(nameof(LoadoutMovementProfile.CommitmentTicks), 3, 5)]
    [InlineData(nameof(LoadoutMovementProfile.RecoveryTicks), 3, 5)]
    [InlineData(
        nameof(LoadoutMovementProfile.AllyClearanceBodyDiametersBasisPoints),
        15_000,
        20_000)]
    [InlineData(
        nameof(LoadoutMovementProfile.DisengageEnemyToAllyBasisPoints),
        15_000,
        20_000)]
    [InlineData(
        nameof(LoadoutMovementProfile.ReengageEnemyToAllyBasisPoints),
        10_000,
        15_000)]
    [InlineData(
        nameof(LoadoutMovementProfile.PursuitSupportBodyDiametersBasisPoints),
        8_000,
        12_500)]
    public void EveryWasayScalarSitsInsideItsApprovedCalibrationRange(
        string field, int lowestApproved, int highestApproved) =>
        Assert.InRange(ScalarByName(field), lowestApproved, highestApproved);

    /// <summary>
    /// Every signed opponent-distance offset cell sits inside the approved
    /// range of the same table, one case per canonical opponent.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void EveryWasayOffsetCellSitsInsideItsApprovedCalibrationRange(
        int cell) =>
        Assert.InRange(
            WasayMovementProfile.Row.OpponentDistanceOffsetBasisPoints[cell],
            -2_000,
            2_000);

    /// <summary>
    /// Reads one scalar of the shipped row by property name, so the range
    /// table above names the field it constrains and a failing case reports
    /// that name.
    /// </summary>
    private static int ScalarByName(string field)
    {
        var row = WasayMovementProfile.Row;
        return field switch
        {
            nameof(LoadoutMovementProfile.ForwardPaceBasisPoints) =>
                row.ForwardPaceBasisPoints,
            nameof(LoadoutMovementProfile.LateralPaceBasisPoints) =>
                row.LateralPaceBasisPoints,
            nameof(LoadoutMovementProfile.BackwardPaceBasisPoints) =>
                row.BackwardPaceBasisPoints,
            nameof(LoadoutMovementProfile.CommittedPaceBasisPoints) =>
                row.CommittedPaceBasisPoints,
            nameof(LoadoutMovementProfile.PreferredDistanceBasisPoints) =>
                row.PreferredDistanceBasisPoints,
            nameof(LoadoutMovementProfile.MaximumFacingStepsPerTick) =>
                row.MaximumFacingStepsPerTick,
            nameof(LoadoutMovementProfile.CommittedFacingStepsPerTick) =>
                row.CommittedFacingStepsPerTick,
            nameof(LoadoutMovementProfile.AccelerationBasisPointsPerTick) =>
                row.AccelerationBasisPointsPerTick,
            nameof(LoadoutMovementProfile.DecelerationBasisPointsPerTick) =>
                row.DecelerationBasisPointsPerTick,
            nameof(LoadoutMovementProfile.CommitmentTicks) => row.CommitmentTicks,
            nameof(LoadoutMovementProfile.RecoveryTicks) => row.RecoveryTicks,
            nameof(LoadoutMovementProfile.AllyClearanceBodyDiametersBasisPoints) =>
                row.AllyClearanceBodyDiametersBasisPoints,
            nameof(LoadoutMovementProfile.DisengageEnemyToAllyBasisPoints) =>
                row.DisengageEnemyToAllyBasisPoints,
            nameof(LoadoutMovementProfile.ReengageEnemyToAllyBasisPoints) =>
                row.ReengageEnemyToAllyBasisPoints,
            nameof(LoadoutMovementProfile.PursuitSupportBodyDiametersBasisPoints) =>
                row.PursuitSupportBodyDiametersBasisPoints,
            _ => throw new ArgumentOutOfRangeException(
                nameof(field),
                field,
                "The calibration range table names a property the Wasay row " +
                "does not carry."),
        };
    }
}
