using Hukbo.Core.Movement;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// Coverage for <see cref="MovementPresetId.ContingentShapeV12"/>, registered
/// as a verbatim restatement of
/// <see cref="MovementPresetId.LastStandEngagementV11"/>'s field values under
/// its own <c>id</c>. This preset carries no behaviour of its own yet — it
/// exists only so the identity is registered and can be selected — so the
/// coverage here is limited to the numeric value, registration, and field
/// equality against V11, the same shape
/// <c>LastStandEngagementV11Tests.LastStandEngagementV11IsRegistered</c> and
/// <c>LastStandEngagementV11Tests.LastStandEngagementV11CarriesItsOwnIdentity</c>
/// use for V11 against V10.
/// </summary>
public sealed class ContingentShapeV12Tests
{
    [Fact]
    public void ContingentShapeV12HasTheExpectedNumericValue()
    {
        Assert.Equal(12, (int)MovementPresetId.ContingentShapeV12);
    }

    [Fact]
    public void ContingentShapeV12IsRegistered()
    {
        Assert.True(
            MovementPresetRegistry.IsRegistered(
                MovementPresetId.ContingentShapeV12));
    }

    [Fact]
    public void ContingentShapeV12CarriesItsOwnIdentity()
    {
        var v11 = MovementPresetRegistry.Get(MovementPresetId.LastStandEngagementV11);
        var v12 = MovementPresetRegistry.Get(MovementPresetId.ContingentShapeV12);

        Assert.Equal(MovementPresetId.ContingentShapeV12, v12.Id);

        // The ruleset is a verbatim restatement of V11's field values, so
        // every tunable matches; only the folded Id separates the two
        // content hashes.
        Assert.Equal(v11.CohesionRadiusMultiplier, v12.CohesionRadiusMultiplier);
        Assert.Equal(v11.CloseRadiusMultiplier, v12.CloseRadiusMultiplier);
        Assert.Equal(v11.CohesionCycleTicks, v12.CohesionCycleTicks);
        Assert.Equal(v11.CohesionDutyTicks, v12.CohesionDutyTicks);
        Assert.Equal(v11.ArrivalTaperMultiplier, v12.ArrivalTaperMultiplier);
        Assert.Equal(v11.OffsetUnit, v12.OffsetUnit);
        Assert.Equal(
            v11.NarrowsCohesionScanToCohesionCapableContingents,
            v12.NarrowsCohesionScanToCohesionCapableContingents);
        Assert.Equal(v11.SelectsLeaderByRank, v12.SelectsLeaderByRank);
        Assert.Equal(
            v11.UsesEquipmentRelativeFootwork,
            v12.UsesEquipmentRelativeFootwork);
        Assert.Equal(v11.AppliesPressureInterrupt, v12.AppliesPressureInterrupt);
        Assert.NotEqual(v11.ContentHash, v12.ContentHash);
    }
}
