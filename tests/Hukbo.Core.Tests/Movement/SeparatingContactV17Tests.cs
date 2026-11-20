using System.Collections.Immutable;
using Hukbo.Core.Movement;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// Registry facts for <see cref="MovementPresetId.SeparatingContactV17"/> and
/// the <see cref="MovementRuleset.AppliesSeparatingContact"/> gate, from the
/// 2026-11-21 soft-separation design section 3 and its decision record. Every
/// tunable asserted here is a provisional gameplay value, not a measurement.
/// </summary>
public sealed class SeparatingContactV17Tests
{
    /// <summary>
    /// V17's <see cref="MovementRuleset.ContentHash"/>, recorded from the built
    /// code and never calculated by hand. It pins what the preset <i>is</i>:
    /// it moves only if a gated tunable or a V14 field changes, which needs a
    /// new preset version rather than an edit here.
    /// </summary>
    private const ulong SeparatingContactV17ContentHash = 0x85E6A25091F7B8FDUL;

    private const int BodyRadiusRaw = 4608;

    private static MovementRuleset V14 =>
        MovementPresetRegistry.Get(MovementPresetId.EvasiveFootworkV14);

    private static MovementRuleset V17 =>
        MovementPresetRegistry.Get(MovementPresetId.SeparatingContactV17);

    private static MovementRuleset Build(
        bool applies,
        int softBand,
        int pushCap,
        int stiffness,
        int committedWeight,
        int sideways) =>
        new(
            id: MovementPresetId.SeparatingContactV17,
            version: 1,
            cohesionRadiusMultiplier: 24,
            closeRadiusMultiplier: 16,
            closeFractionNumerator: 1,
            closeFractionDenominator: 2,
            minimumCohesiveMembers: 3,
            cohesionCycleTicks: 240,
            cohesionDutyTicks: 180,
            arrivalTaperMultiplier: 4,
            offsetUnit: 1024,
            narrowsCohesionScanToCohesionCapableContingents: true,
            selectsLeaderByRank: false,
            usesEquipmentRelativeFootwork: false,
            immediateRadiusBodyDiametersBasisPoints: 0,
            supportRadiusBodyDiametersBasisPoints: 0,
            loadoutMovementProfiles: ImmutableArray<LoadoutMovementProfile>.Empty,
            appliesSeparatingContact: applies,
            separationSoftBandBasisPoints: softBand,
            separationPushCapRadiusBasisPoints: pushCap,
            separationStiffnessBasisPoints: stiffness,
            separationCommittedWeightMultiplier: committedWeight,
            separationSidewaysBasisPoints: sideways);

    private static MovementRuleset BuildSettled() =>
        Build(true, 1_500, 500, 5_000, 3, 2_500);

    [Fact]
    public void SeparatingContactV17HasTheAppendedNumericValue()
    {
        // Append-only: the numeric value is part of the deterministic replay
        // and state-hash contract and may never be renumbered.
        Assert.Equal(17, (int)MovementPresetId.SeparatingContactV17);
    }

    [Fact]
    public void SeparatingContactV17IsRegisteredAndResolvesToItsOwnRuleset()
    {
        Assert.True(
            MovementPresetRegistry.IsRegistered(
                MovementPresetId.SeparatingContactV17));
        Assert.Equal(MovementPresetId.SeparatingContactV17, V17.Id);
    }

    [Fact]
    public void SeparatingContactV17ContentHashMatchesThePinnedLiteral()
    {
        Assert.Equal(SeparatingContactV17ContentHash, V17.ContentHash);
    }

    [Fact]
    public void SeparatingContactV17ContentHashDiffersFromEveryOtherPreset()
    {
        foreach (var other in Enum.GetValues<MovementPresetId>())
        {
            if (other == MovementPresetId.SeparatingContactV17 ||
                !MovementPresetRegistry.IsRegistered(other))
            {
                continue;
            }

            Assert.NotEqual(
                MovementPresetRegistry.Get(other).ContentHash,
                V17.ContentHash);
        }
    }

    /// <summary>
    /// V17 is V14 plus the separating-contact gate and nothing else, which is
    /// what makes the two a controlled pair for the design's A/B.
    /// </summary>
    [Fact]
    public void SeparatingContactV17RestatesEvasiveFootworkV14FieldForField()
    {
        var v14 = V14;
        var v17 = V17;

        Assert.Equal(v14.Version, v17.Version);
        Assert.Equal(v14.CohesionRadiusMultiplier, v17.CohesionRadiusMultiplier);
        Assert.Equal(v14.CloseRadiusMultiplier, v17.CloseRadiusMultiplier);
        Assert.Equal(v14.CloseFractionNumerator, v17.CloseFractionNumerator);
        Assert.Equal(v14.CloseFractionDenominator, v17.CloseFractionDenominator);
        Assert.Equal(v14.MinimumCohesiveMembers, v17.MinimumCohesiveMembers);
        Assert.Equal(v14.CohesionCycleTicks, v17.CohesionCycleTicks);
        Assert.Equal(v14.CohesionDutyTicks, v17.CohesionDutyTicks);
        Assert.Equal(v14.ArrivalTaperMultiplier, v17.ArrivalTaperMultiplier);
        Assert.Equal(v14.OffsetUnit, v17.OffsetUnit);
        Assert.Equal(
            v14.NarrowsCohesionScanToCohesionCapableContingents,
            v17.NarrowsCohesionScanToCohesionCapableContingents);
        Assert.Equal(v14.SelectsLeaderByRank, v17.SelectsLeaderByRank);
        Assert.Equal(
            v14.UsesEquipmentRelativeFootwork,
            v17.UsesEquipmentRelativeFootwork);
        Assert.Equal(
            v14.ImmediateRadiusBodyDiametersBasisPoints,
            v17.ImmediateRadiusBodyDiametersBasisPoints);
        Assert.Equal(
            v14.SupportRadiusBodyDiametersBasisPoints,
            v17.SupportRadiusBodyDiametersBasisPoints);
        Assert.Equal(v14.LoadoutMovementProfiles, v17.LoadoutMovementProfiles);
        Assert.Equal(v14.AppliesPressureInterrupt, v17.AppliesPressureInterrupt);
        Assert.Equal(
            v14.SupportPressureWeightBasisPoints,
            v17.SupportPressureWeightBasisPoints);
        Assert.Equal(
            v14.IncomingDamageWeightBasisPoints,
            v17.IncomingDamageWeightBasisPoints);
        Assert.Equal(
            v14.AllyCollapseWeightBasisPoints,
            v17.AllyCollapseWeightBasisPoints);
        Assert.Equal(
            v14.GathersContingentsBeforeContact,
            v17.GathersContingentsBeforeContact);
        Assert.Equal(v14.AppliesShieldBlockRecovery, v17.AppliesShieldBlockRecovery);
        Assert.Equal(v14.AppliesShieldEncumbrance, v17.AppliesShieldEncumbrance);
        Assert.NotEqual(v14.ContentHash, v17.ContentHash);
    }

    [Fact]
    public void SeparatingContactV17RegistersTheSettledTunables()
    {
        var v17 = V17;

        Assert.True(v17.AppliesSeparatingContact);
        Assert.Equal(1_500, v17.SeparationSoftBandBasisPoints);
        Assert.Equal(500, v17.SeparationPushCapRadiusBasisPoints);
        Assert.Equal(5_000, v17.SeparationStiffnessBasisPoints);
        Assert.Equal(3, v17.SeparationCommittedWeightMultiplier);
        Assert.Equal(2_500, v17.SeparationSidewaysBasisPoints);
    }

    [Fact]
    public void EveryOtherRegisteredPresetLeavesTheGateClosedAndTheTunablesZero()
    {
        foreach (var id in Enum.GetValues<MovementPresetId>())
        {
            if (id == MovementPresetId.SeparatingContactV17 ||
                !MovementPresetRegistry.IsRegistered(id))
            {
                continue;
            }

            var ruleset = MovementPresetRegistry.Get(id);

            Assert.False(ruleset.AppliesSeparatingContact);
            Assert.Equal(0, ruleset.SeparationSoftBandBasisPoints);
            Assert.Equal(0, ruleset.SeparationPushCapRadiusBasisPoints);
            Assert.Equal(0, ruleset.SeparationStiffnessBasisPoints);
            Assert.Equal(0, ruleset.SeparationCommittedWeightMultiplier);
            Assert.Equal(0, ruleset.SeparationSidewaysBasisPoints);
        }
    }

    /// <summary>
    /// Literal expectations: 2 x 4608 x 8500 / 10000 truncates to 7833, and
    /// 4608 x 500 / 10000 truncates to 230. A preset without the gate gets the
    /// full diameter 9216 and a zero cap.
    /// </summary>
    [Fact]
    public void SeparationHelpersResolveTheFloorAndTheCapFromTheBodyRadius()
    {
        Assert.Equal(7_833, V17.SeparationFloorRaw(BodyRadiusRaw));
        Assert.Equal(230, V17.SeparationPushCapRaw(BodyRadiusRaw));
        Assert.Equal(9_216, V14.SeparationFloorRaw(BodyRadiusRaw));
        Assert.Equal(0, V14.SeparationPushCapRaw(BodyRadiusRaw));
    }

    [Fact]
    public void TheGatedTunablesFoldIntoTheContentHash()
    {
        var settled = BuildSettled().ContentHash;

        Assert.Equal(settled, BuildSettled().ContentHash);
        Assert.NotEqual(settled, Build(true, 1_400, 500, 5_000, 3, 2_500).ContentHash);
        Assert.NotEqual(settled, Build(true, 1_500, 400, 5_000, 3, 2_500).ContentHash);
        Assert.NotEqual(settled, Build(true, 1_500, 500, 4_000, 3, 2_500).ContentHash);
        Assert.NotEqual(settled, Build(true, 1_500, 500, 5_000, 2, 2_500).ContentHash);
        Assert.NotEqual(settled, Build(true, 1_500, 500, 5_000, 3, 2_000).ContentHash);
    }

    [Theory]
    [InlineData(1, 500, 5_000, 3, 2_500)]
    [InlineData(0, 1, 1, 0, 0)]
    [InlineData(0, 0, 0, 1, 0)]
    [InlineData(0, 0, 0, 0, 1)]
    public void AClosedGateRejectsAnyNonZeroTunable(
        int softBand,
        int pushCap,
        int stiffness,
        int committedWeight,
        int sideways)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Build(false, softBand, pushCap, stiffness, committedWeight, sideways));
    }

    [Theory]
    [InlineData(0, 500, 5_000, 3, 2_500)]
    [InlineData(5_001, 500, 5_000, 3, 2_500)]
    [InlineData(1_500, 0, 5_000, 3, 2_500)]
    [InlineData(1_500, 5_001, 5_000, 3, 2_500)]
    [InlineData(1_500, 500, 0, 3, 2_500)]
    [InlineData(1_500, 500, 10_001, 3, 2_500)]
    [InlineData(1_500, 500, 5_000, 0, 2_500)]
    [InlineData(1_500, 500, 5_000, 3, -1)]
    [InlineData(1_500, 500, 5_000, 3, 10_001)]
    public void AnOpenGateRejectsATunableOutsideItsRange(
        int softBand,
        int pushCap,
        int stiffness,
        int committedWeight,
        int sideways)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Build(true, softBand, pushCap, stiffness, committedWeight, sideways));
    }

    [Theory]
    [InlineData(5_000, 5_000, 10_000, 1, 0)]
    [InlineData(1, 1, 1, 1, 10_000)]
    public void AnOpenGateAcceptsTheRangeEdges(
        int softBand,
        int pushCap,
        int stiffness,
        int committedWeight,
        int sideways)
    {
        var ruleset = Build(true, softBand, pushCap, stiffness, committedWeight, sideways);

        Assert.True(ruleset.AppliesSeparatingContact);
    }
}
