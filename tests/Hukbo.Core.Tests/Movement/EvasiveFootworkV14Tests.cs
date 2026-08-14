using Hukbo.Core.Movement;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// Coverage for <see cref="MovementPresetId.EvasiveFootworkV14"/>, plan task 3
/// of the 2026-08-15 in-fight evasion plan. Registry facts mirror
/// <see cref="CohortLateralSpreadV13Tests"/>, which is the convention every
/// preset since V8 has followed: <c>MovementPresetRegistryTests</c> stops at
/// V7, and each later preset carries its own registry facts beside its own
/// behaviour.
/// </summary>
public sealed class EvasiveFootworkV14Tests
{
    /// <summary>
    /// V14's <see cref="MovementRuleset.ContentHash"/>, recorded from the built
    /// code and never calculated by hand — the same rule the seven identity
    /// literals in <c>MovementPresetRegistryTests</c> are recorded under.
    /// </summary>
    /// <remarks>
    /// This literal pins what the preset <i>is</i>, not what it achieves. It
    /// moves only if a field is added to <see cref="MovementRuleset"/> and
    /// folded outside a version gate, in which case every other identity
    /// literal moves with it and all of them are re-recorded together. It does
    /// not move when an <c>EvasionRules</c> tuning constant changes, because
    /// those constants are not ruleset fields.
    /// </remarks>
    private const ulong EvasiveFootworkV14ContentHash = 0x1EF1350E65941D97UL;

    [Fact]
    public void EvasiveFootworkV14HasTheAppendedNumericValue()
    {
        // Append-only: the numeric value is part of the deterministic replay
        // and state-hash contract and may never be renumbered.
        Assert.Equal(14, (int)MovementPresetId.EvasiveFootworkV14);
    }

    [Fact]
    public void EvasiveFootworkV14IsRegistered()
    {
        Assert.True(
            MovementPresetRegistry.IsRegistered(
                MovementPresetId.EvasiveFootworkV14));
    }

    [Fact]
    public void EvasiveFootworkV14ResolvesToItsOwnRuleset()
    {
        var ruleset = MovementPresetRegistry.Get(
            MovementPresetId.EvasiveFootworkV14);

        Assert.Equal(MovementPresetId.EvasiveFootworkV14, ruleset.Id);
    }

    [Fact]
    public void EvasiveFootworkV14ContentHashMatchesThePinnedLiteral()
    {
        var ruleset = MovementPresetRegistry.Get(
            MovementPresetId.EvasiveFootworkV14);

        Assert.Equal(EvasiveFootworkV14ContentHash, ruleset.ContentHash);
    }

    [Fact]
    public void EvasiveFootworkV14ContentHashDiffersFromEveryOtherPreset()
    {
        var ruleset = MovementPresetRegistry.Get(
            MovementPresetId.EvasiveFootworkV14);

        foreach (var other in Enum.GetValues<MovementPresetId>())
        {
            if (other == MovementPresetId.EvasiveFootworkV14 ||
                !MovementPresetRegistry.IsRegistered(other))
            {
                continue;
            }

            Assert.NotEqual(
                MovementPresetRegistry.Get(other).ContentHash,
                ruleset.ContentHash);
        }
    }

    /// <summary>
    /// V14 restates V13's field values verbatim under its own identity. Every
    /// field except <see cref="MovementRuleset.Id"/> and the derived
    /// <see cref="MovementRuleset.ContentHash"/> is equal, which is what makes
    /// the two presets a controlled pair: any behavioural difference between
    /// them is attributable to the identity-gated evasive footwork and to
    /// nothing else.
    /// </summary>
    [Fact]
    public void EvasiveFootworkV14RestatesCohortLateralSpreadV13FieldForField()
    {
        var v13 = MovementPresetRegistry.Get(
            MovementPresetId.CohortLateralSpreadV13);
        var v14 = MovementPresetRegistry.Get(
            MovementPresetId.EvasiveFootworkV14);

        Assert.Equal(v13.Version, v14.Version);
        Assert.Equal(v13.CohesionRadiusMultiplier, v14.CohesionRadiusMultiplier);
        Assert.Equal(v13.CloseRadiusMultiplier, v14.CloseRadiusMultiplier);
        Assert.Equal(v13.CloseFractionNumerator, v14.CloseFractionNumerator);
        Assert.Equal(v13.CloseFractionDenominator, v14.CloseFractionDenominator);
        Assert.Equal(v13.MinimumCohesiveMembers, v14.MinimumCohesiveMembers);
        Assert.Equal(v13.CohesionCycleTicks, v14.CohesionCycleTicks);
        Assert.Equal(v13.CohesionDutyTicks, v14.CohesionDutyTicks);
        Assert.Equal(v13.ArrivalTaperMultiplier, v14.ArrivalTaperMultiplier);
        Assert.Equal(v13.OffsetUnit, v14.OffsetUnit);
        Assert.Equal(
            v13.NarrowsCohesionScanToCohesionCapableContingents,
            v14.NarrowsCohesionScanToCohesionCapableContingents);
        Assert.Equal(v13.SelectsLeaderByRank, v14.SelectsLeaderByRank);
        Assert.Equal(
            v13.UsesEquipmentRelativeFootwork,
            v14.UsesEquipmentRelativeFootwork);
        Assert.Equal(
            v13.ImmediateRadiusBodyDiametersBasisPoints,
            v14.ImmediateRadiusBodyDiametersBasisPoints);
        Assert.Equal(
            v13.SupportRadiusBodyDiametersBasisPoints,
            v14.SupportRadiusBodyDiametersBasisPoints);
        Assert.Equal(
            v13.LoadoutMovementProfiles.Length,
            v14.LoadoutMovementProfiles.Length);
        Assert.Equal(v13.AppliesPressureInterrupt, v14.AppliesPressureInterrupt);
        Assert.Equal(
            v13.SupportPressureWeightBasisPoints,
            v14.SupportPressureWeightBasisPoints);
        Assert.Equal(
            v13.IncomingDamageWeightBasisPoints,
            v14.IncomingDamageWeightBasisPoints);
        Assert.Equal(
            v13.AllyCollapseWeightBasisPoints,
            v14.AllyCollapseWeightBasisPoints);
    }

    /// <summary>
    /// The equipment-relative route pipeline stays off. This is the load-bearing
    /// negative of the whole design: V14 adds movement during an engagement, and
    /// deliberately does not revive the footwork phase machinery that V10
    /// abandoned. A future change that flips this flag is a different feature
    /// and needs its own preset.
    /// </summary>
    [Fact]
    public void EvasiveFootworkV14DoesNotReviveEquipmentRelativeFootwork()
    {
        var ruleset = MovementPresetRegistry.Get(
            MovementPresetId.EvasiveFootworkV14);

        Assert.False(ruleset.UsesEquipmentRelativeFootwork);
        Assert.Empty(ruleset.LoadoutMovementProfiles);
    }
}
