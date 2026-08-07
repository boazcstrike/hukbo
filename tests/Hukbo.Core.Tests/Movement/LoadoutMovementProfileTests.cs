using System.Collections.Immutable;
using Hukbo.Core.Combat;
using Hukbo.Core.Movement;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// Constructor contract of <see cref="LoadoutMovementProfile"/>: every design
/// 4.3 bound gets its own rejection test, every accepted boundary gets an
/// acceptance test, and the rank-independence of the profile key (design 4.1)
/// is proven on the stored <see cref="LoadoutMovementProfile.Loadout"/>.
/// Values used here are arbitrary in-range fixtures, not tuning claims.
/// </summary>
public sealed class LoadoutMovementProfileTests
{
    private static readonly CombatLoadout DefaultLoadout = new(
        WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None);

    /// <summary>
    /// One valid profile with every argument overridable, so each rejection
    /// test names exactly the argument it violates.
    /// </summary>
    private static LoadoutMovementProfile Create(
        CombatLoadout? loadout = null,
        int forwardPaceBasisPoints = 9_800,
        int lateralPaceBasisPoints = 8_200,
        int backwardPaceBasisPoints = 7_000,
        int committedPaceBasisPoints = 3_000,
        int preferredDistanceBasisPoints = 11_500,
        ImmutableArray<int>? opponentDistanceOffsetBasisPoints = null,
        int maximumFacingStepsPerTick = 2,
        int committedFacingStepsPerTick = 1,
        int accelerationBasisPointsPerTick = 5_000,
        int decelerationBasisPointsPerTick = 6_000,
        int commitmentTicks = 3,
        int recoveryTicks = 3,
        int allyClearanceBodyDiametersBasisPoints = 15_000,
        int disengageEnemyToAllyBasisPoints = 20_000,
        int reengageEnemyToAllyBasisPoints = 12_500,
        int pursuitSupportBodyDiametersBasisPoints = 12_500,
        int pressureInterruptThresholdBasisPoints = 0) =>
        new(
            loadout ?? DefaultLoadout,
            forwardPaceBasisPoints,
            lateralPaceBasisPoints,
            backwardPaceBasisPoints,
            committedPaceBasisPoints,
            preferredDistanceBasisPoints,
            opponentDistanceOffsetBasisPoints ?? [0, 0, 250, 500, 250, 500],
            maximumFacingStepsPerTick,
            committedFacingStepsPerTick,
            accelerationBasisPointsPerTick,
            decelerationBasisPointsPerTick,
            commitmentTicks,
            recoveryTicks,
            allyClearanceBodyDiametersBasisPoints,
            disengageEnemyToAllyBasisPoints,
            reengageEnemyToAllyBasisPoints,
            pursuitSupportBodyDiametersBasisPoints,
            pressureInterruptThresholdBasisPoints);

    [Fact]
    public void ConstructionStoresEveryPropertyUnchanged()
    {
        var profile = Create();

        Assert.Equal(DefaultLoadout, profile.Loadout);
        Assert.Equal(9_800, profile.ForwardPaceBasisPoints);
        Assert.Equal(8_200, profile.LateralPaceBasisPoints);
        Assert.Equal(7_000, profile.BackwardPaceBasisPoints);
        Assert.Equal(3_000, profile.CommittedPaceBasisPoints);
        Assert.Equal(11_500, profile.PreferredDistanceBasisPoints);
        Assert.Equal(
            new[] { 0, 0, 250, 500, 250, 500 },
            profile.OpponentDistanceOffsetBasisPoints);
        Assert.Equal(2, profile.MaximumFacingStepsPerTick);
        Assert.Equal(1, profile.CommittedFacingStepsPerTick);
        Assert.Equal(5_000, profile.AccelerationBasisPointsPerTick);
        Assert.Equal(6_000, profile.DecelerationBasisPointsPerTick);
        Assert.Equal(3, profile.CommitmentTicks);
        Assert.Equal(3, profile.RecoveryTicks);
        Assert.Equal(15_000, profile.AllyClearanceBodyDiametersBasisPoints);
        Assert.Equal(20_000, profile.DisengageEnemyToAllyBasisPoints);
        Assert.Equal(12_500, profile.ReengageEnemyToAllyBasisPoints);
        Assert.Equal(12_500, profile.PursuitSupportBodyDiametersBasisPoints);
        Assert.Equal(0, profile.PressureInterruptThresholdBasisPoints);
    }

    /// <summary>
    /// Design 4.1: the profile key is equipment only. A loadout arriving with
    /// any rank is stored with the default rank, so two loadouts differing
    /// only in rank yield byte-identical profile keys.
    /// </summary>
    [Fact]
    public void TheStoredLoadoutKeyDropsTheIncomingRank()
    {
        var ranked = new CombatLoadout(
            WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None, RankId.Datu);

        var profile = Create(loadout: ranked);

        Assert.Equal(RankId.Timawa, profile.Loadout.Rank);
        Assert.Equal(DefaultLoadout, profile.Loadout);
        Assert.Equal(Create(loadout: DefaultLoadout).Loadout, profile.Loadout);
    }

    /// <summary>
    /// Design 4.3: the pace bound is inclusive at 10,000 or the Itak row,
    /// whose forward pace is exactly 10,000, fails construction.
    /// </summary>
    [Fact]
    public void ForwardPaceAtTheInclusiveUpperBoundIsAccepted()
    {
        var profile = Create(forwardPaceBasisPoints: 10_000);

        Assert.Equal(10_000, profile.ForwardPaceBasisPoints);
    }

    [Fact]
    public void PaceAtTheInclusiveLowerBoundIsAccepted()
    {
        var profile = Create(
            forwardPaceBasisPoints: 1,
            lateralPaceBasisPoints: 1,
            backwardPaceBasisPoints: 1,
            committedPaceBasisPoints: 1);

        Assert.Equal(1, profile.ForwardPaceBasisPoints);
        Assert.Equal(1, profile.LateralPaceBasisPoints);
        Assert.Equal(1, profile.BackwardPaceBasisPoints);
        Assert.Equal(1, profile.CommittedPaceBasisPoints);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10_001)]
    public void ForwardPaceOutsideTheInclusiveBoundIsRejected(int pace) =>
        Assert.Equal(
            "forwardPaceBasisPoints",
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Create(forwardPaceBasisPoints: pace)).ParamName);

    [Theory]
    [InlineData(0)]
    [InlineData(10_001)]
    public void LateralPaceOutsideTheInclusiveBoundIsRejected(int pace) =>
        Assert.Equal(
            "lateralPaceBasisPoints",
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Create(lateralPaceBasisPoints: pace)).ParamName);

    [Theory]
    [InlineData(0)]
    [InlineData(10_001)]
    public void BackwardPaceOutsideTheInclusiveBoundIsRejected(int pace) =>
        Assert.Equal(
            "backwardPaceBasisPoints",
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Create(backwardPaceBasisPoints: pace)).ParamName);

    [Theory]
    [InlineData(0)]
    [InlineData(10_001)]
    public void CommittedPaceOutsideTheInclusiveBoundIsRejected(int pace) =>
        Assert.Equal(
            "committedPaceBasisPoints",
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Create(committedPaceBasisPoints: pace)).ParamName);

    [Fact]
    public void CommittedPaceAboveForwardPaceIsRejected() =>
        Assert.Equal(
            "committedPaceBasisPoints",
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Create(
                    forwardPaceBasisPoints: 5_000,
                    committedPaceBasisPoints: 5_001)).ParamName);

    [Fact]
    public void CommittedPaceEqualToForwardPaceIsAccepted()
    {
        var profile = Create(
            forwardPaceBasisPoints: 5_000,
            committedPaceBasisPoints: 5_000);

        Assert.Equal(5_000, profile.CommittedPaceBasisPoints);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositivePreferredDistanceIsRejected(int preferred) =>
        Assert.Equal(
            "preferredDistanceBasisPoints",
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Create(preferredDistanceBasisPoints: preferred)).ParamName);

    [Fact]
    public void ADefaultOffsetArrayIsRejectedAsMalformed() =>
        Assert.Equal(
            "opponentDistanceOffsetBasisPoints",
            Assert.Throws<ArgumentException>(
                () => Create(
                    opponentDistanceOffsetBasisPoints:
                        default(ImmutableArray<int>))).ParamName);

    [Fact]
    public void FiveOffsetCellsAreRejectedAsMalformed() =>
        Assert.Equal(
            "opponentDistanceOffsetBasisPoints",
            Assert.Throws<ArgumentException>(
                () => Create(
                    opponentDistanceOffsetBasisPoints:
                        [0, 0, 0, 0, 0])).ParamName);

    [Fact]
    public void SevenOffsetCellsAreRejectedAsMalformed() =>
        Assert.Equal(
            "opponentDistanceOffsetBasisPoints",
            Assert.Throws<ArgumentException>(
                () => Create(
                    opponentDistanceOffsetBasisPoints:
                        [0, 0, 0, 0, 0, 0, 0])).ParamName);

    [Theory]
    [InlineData(-2_001)]
    [InlineData(2_001)]
    public void AnOffsetCellOutsideTheInclusiveBoundIsRejected(int cell) =>
        Assert.Equal(
            "opponentDistanceOffsetBasisPoints",
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Create(
                    opponentDistanceOffsetBasisPoints:
                        [0, 0, cell, 0, 0, 0])).ParamName);

    [Fact]
    public void OffsetCellsAtBothInclusiveBoundsAreAccepted()
    {
        var profile = Create(
            preferredDistanceBasisPoints: 2_001,
            opponentDistanceOffsetBasisPoints: [-2_000, 2_000, 0, 0, 0, 0]);

        Assert.Equal(
            new[] { -2_000, 2_000, 0, 0, 0, 0 },
            profile.OpponentDistanceOffsetBasisPoints);
    }

    /// <summary>
    /// Design 4.3: for every cell, preferred distance plus the cell stays
    /// strictly positive. Exactly zero is rejected; one above it is not.
    /// </summary>
    [Fact]
    public void AnOffsetCellThatCancelsThePreferredDistanceIsRejected() =>
        Assert.Equal(
            "opponentDistanceOffsetBasisPoints",
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Create(
                    preferredDistanceBasisPoints: 2_000,
                    opponentDistanceOffsetBasisPoints:
                        [0, 0, 0, 0, 0, -2_000])).ParamName);

    [Fact]
    public void AnOffsetCellLeavingOneBasisPointOfPreferredDistanceIsAccepted()
    {
        var profile = Create(
            preferredDistanceBasisPoints: 2_001,
            opponentDistanceOffsetBasisPoints: [0, 0, 0, 0, 0, -2_000]);

        Assert.Equal(2_001, profile.PreferredDistanceBasisPoints);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(9)]
    public void MaximumFacingStepsOutsideTheInclusiveBoundAreRejected(
        int steps) =>
        Assert.Equal(
            "maximumFacingStepsPerTick",
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Create(maximumFacingStepsPerTick: steps)).ParamName);

    [Theory]
    [InlineData(-1)]
    [InlineData(9)]
    public void CommittedFacingStepsOutsideTheInclusiveBoundAreRejected(
        int steps) =>
        Assert.Equal(
            "committedFacingStepsPerTick",
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Create(committedFacingStepsPerTick: steps)).ParamName);

    [Fact]
    public void FacingStepsAtBothInclusiveBoundsAreAccepted()
    {
        var profile = Create(
            maximumFacingStepsPerTick: 8,
            committedFacingStepsPerTick: 0);

        Assert.Equal(8, profile.MaximumFacingStepsPerTick);
        Assert.Equal(0, profile.CommittedFacingStepsPerTick);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10_001)]
    public void AccelerationOutsideTheInclusiveBoundIsRejected(int step) =>
        Assert.Equal(
            "accelerationBasisPointsPerTick",
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Create(accelerationBasisPointsPerTick: step)).ParamName);

    [Theory]
    [InlineData(0)]
    [InlineData(10_001)]
    public void DecelerationOutsideTheInclusiveBoundIsRejected(int step) =>
        Assert.Equal(
            "decelerationBasisPointsPerTick",
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Create(decelerationBasisPointsPerTick: step)).ParamName);

    [Fact]
    public void AccelerationAndDecelerationAtBothInclusiveBoundsAreAccepted()
    {
        var profile = Create(
            accelerationBasisPointsPerTick: 1,
            decelerationBasisPointsPerTick: 10_000);

        Assert.Equal(1, profile.AccelerationBasisPointsPerTick);
        Assert.Equal(10_000, profile.DecelerationBasisPointsPerTick);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveCommitmentTicksAreRejected(int ticks) =>
        Assert.Equal(
            "commitmentTicks",
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Create(commitmentTicks: ticks)).ParamName);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveRecoveryTicksAreRejected(int ticks) =>
        Assert.Equal(
            "recoveryTicks",
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Create(recoveryTicks: ticks)).ParamName);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveAllyClearanceIsRejected(int clearance) =>
        Assert.Equal(
            "allyClearanceBodyDiametersBasisPoints",
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Create(
                    allyClearanceBodyDiametersBasisPoints: clearance))
                .ParamName);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositivePursuitSupportIsRejected(int support) =>
        Assert.Equal(
            "pursuitSupportBodyDiametersBasisPoints",
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Create(
                    pursuitSupportBodyDiametersBasisPoints: support))
                .ParamName);

    /// <summary>
    /// Design 4.3: reengage strictly below disengage, so hysteresis always
    /// exists and a warrior can never enter and leave disengagement on the
    /// same counts. Equality is rejected, not merely inversion.
    /// </summary>
    [Theory]
    [InlineData(20_000)]
    [InlineData(20_001)]
    public void ReengageAtOrAboveDisengageIsRejected(int reengage) =>
        Assert.Equal(
            "reengageEnemyToAllyBasisPoints",
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Create(
                    disengageEnemyToAllyBasisPoints: 20_000,
                    reengageEnemyToAllyBasisPoints: reengage)).ParamName);

    [Fact]
    public void ReengageOneBasisPointBelowDisengageIsAccepted()
    {
        var profile = Create(
            disengageEnemyToAllyBasisPoints: 20_000,
            reengageEnemyToAllyBasisPoints: 19_999);

        Assert.Equal(19_999, profile.ReengageEnemyToAllyBasisPoints);
    }

    /// <summary>
    /// Pressure-interrupt design 6.3: the threshold is a trailing optional
    /// parameter defaulting to zero. This test calls the constructor
    /// positionally with sixteen arguments rather than through
    /// <see cref="Create"/>, because the property under test is the
    /// constructor's own default and not the helper's — a construction site
    /// written before this member existed still compiles and still carries no
    /// threshold.
    /// </summary>
    [Fact]
    public void AnOmittedPressureInterruptThresholdDefaultsToZero()
    {
        var profile = new LoadoutMovementProfile(
            DefaultLoadout,
            9_800,
            8_200,
            7_000,
            3_000,
            11_500,
            [0, 0, 250, 500, 250, 500],
            2,
            1,
            5_000,
            6_000,
            3,
            3,
            15_000,
            20_000,
            12_500,
            12_500);

        Assert.Equal(0, profile.PressureInterruptThresholdBasisPoints);
    }

    [Fact]
    public void AnExplicitPressureInterruptThresholdIsStoredUnchanged()
    {
        var profile = Create(pressureInterruptThresholdBasisPoints: 7_500);

        Assert.Equal(7_500, profile.PressureInterruptThresholdBasisPoints);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-10_000)]
    public void ANegativePressureInterruptThresholdIsRejected(int threshold) =>
        Assert.Equal(
            "pressureInterruptThresholdBasisPoints",
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Create(
                    pressureInterruptThresholdBasisPoints: threshold))
                .ParamName);

    /// <summary>
    /// Pressure-interrupt design 6.3: a row validates non-negativity and
    /// nothing else. The coupled bounds — zero everywhere under a preset that
    /// does not apply the interrupt, and
    /// <c>[1, SignalCeilingBasisPoints]</c> under one that does — need the
    /// ruleset's version gate, which no single row can see, so a row on its
    /// own accepts both a zero and a value above the signal ceiling.
    /// </summary>
    [Fact]
    public void ARowAloneEnforcesNeitherTheZeroFloorNorTheSignalCeiling()
    {
        var unregistered = Create(pressureInterruptThresholdBasisPoints: 0);
        var aboveCeiling = Create(
            pressureInterruptThresholdBasisPoints:
                (int)WeaponMovementRules.SignalCeilingBasisPoints + 1);

        Assert.Equal(0, unregistered.PressureInterruptThresholdBasisPoints);
        Assert.Equal(
            (int)WeaponMovementRules.SignalCeilingBasisPoints + 1,
            aboveCeiling.PressureInterruptThresholdBasisPoints);
    }

    /// <summary>
    /// Pressure-interrupt design 6.3: the derivation is immutable. The source
    /// row is a different instance, still carries its own threshold, and every
    /// other value is copied across unchanged, so a preset built from an
    /// earlier preset's rows cannot drift from that preset's tuning.
    /// </summary>
    [Fact]
    public void WithPressureInterruptThresholdReturnsANewRowAndLeavesTheSourceIntact()
    {
        var source = Create(pressureInterruptThresholdBasisPoints: 4_000);

        var derived = source.WithPressureInterruptThreshold(6_250);

        Assert.NotSame(source, derived);
        Assert.Equal(4_000, source.PressureInterruptThresholdBasisPoints);
        Assert.Equal(6_250, derived.PressureInterruptThresholdBasisPoints);
    }

    [Fact]
    public void WithPressureInterruptThresholdCopiesEveryOtherValueUnchanged()
    {
        var source = Create();

        var derived = source.WithPressureInterruptThreshold(9_000);

        Assert.Equal(source.Loadout, derived.Loadout);
        Assert.Equal(
            source.ForwardPaceBasisPoints, derived.ForwardPaceBasisPoints);
        Assert.Equal(
            source.LateralPaceBasisPoints, derived.LateralPaceBasisPoints);
        Assert.Equal(
            source.BackwardPaceBasisPoints, derived.BackwardPaceBasisPoints);
        Assert.Equal(
            source.CommittedPaceBasisPoints, derived.CommittedPaceBasisPoints);
        Assert.Equal(
            source.PreferredDistanceBasisPoints,
            derived.PreferredDistanceBasisPoints);
        Assert.Equal(
            source.OpponentDistanceOffsetBasisPoints,
            derived.OpponentDistanceOffsetBasisPoints);
        Assert.Equal(
            source.MaximumFacingStepsPerTick,
            derived.MaximumFacingStepsPerTick);
        Assert.Equal(
            source.CommittedFacingStepsPerTick,
            derived.CommittedFacingStepsPerTick);
        Assert.Equal(
            source.AccelerationBasisPointsPerTick,
            derived.AccelerationBasisPointsPerTick);
        Assert.Equal(
            source.DecelerationBasisPointsPerTick,
            derived.DecelerationBasisPointsPerTick);
        Assert.Equal(source.CommitmentTicks, derived.CommitmentTicks);
        Assert.Equal(source.RecoveryTicks, derived.RecoveryTicks);
        Assert.Equal(
            source.AllyClearanceBodyDiametersBasisPoints,
            derived.AllyClearanceBodyDiametersBasisPoints);
        Assert.Equal(
            source.DisengageEnemyToAllyBasisPoints,
            derived.DisengageEnemyToAllyBasisPoints);
        Assert.Equal(
            source.ReengageEnemyToAllyBasisPoints,
            derived.ReengageEnemyToAllyBasisPoints);
        Assert.Equal(
            source.PursuitSupportBodyDiametersBasisPoints,
            derived.PursuitSupportBodyDiametersBasisPoints);
    }

    /// <summary>
    /// The derivation runs the full constructor, so the non-negativity bound
    /// is enforced on the new row exactly as it is at construction rather than
    /// bypassed by the copy.
    /// </summary>
    [Fact]
    public void WithPressureInterruptThresholdRejectsANegativeThreshold() =>
        Assert.Equal(
            "pressureInterruptThresholdBasisPoints",
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Create().WithPressureInterruptThreshold(-1)).ParamName);

    /// <summary>
    /// Design 4.1 survives the derivation: the new row runs the same
    /// rank-dropping constructor, so a derived row's key is still equipment
    /// only.
    /// </summary>
    [Fact]
    public void WithPressureInterruptThresholdKeepsTheRankIndependentKey()
    {
        var ranked = new CombatLoadout(
            WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None, RankId.Datu);

        var derived = Create(loadout: ranked).WithPressureInterruptThreshold(1);

        Assert.Equal(RankId.Timawa, derived.Loadout.Rank);
        Assert.Equal(DefaultLoadout, derived.Loadout);
    }
}
