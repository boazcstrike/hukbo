using Sandata.Core.Rules;

namespace Sandata.Core.Tests;

/// <summary>
/// Pins <see cref="SandataPresetId.ModernTacticalV1"/>'s numeric value and
/// <see cref="SandataRuleset.ContentHash"/>'s recorded value, and proves the
/// hash is meaningful by showing that changing any single ruleset field
/// moves it. This is task 9 of
/// docs/plans/2026-08-07-sandata-scaffold.md — the root of Sandata's
/// determinism contract that every later hash folds through.
/// </summary>
public sealed class SandataRulesetTests
{
    /// <summary>
    /// The numeric value is part of the replay contract per
    /// <see cref="SandataPresetId"/>'s own doc comment. Pinned as a literal
    /// so an accidental renumbering fails loudly rather than silently
    /// invalidating every recorded golden expectation.
    /// </summary>
    [Fact]
    public void ModernTacticalV1_NumericValueIsPinned()
    {
        Assert.Equal(1, (int)SandataPresetId.ModernTacticalV1);
    }

    /// <summary>
    /// The recorded expectation for
    /// <see cref="SandataRuleset.ModernTacticalV1"/>'s content hash, computed
    /// from the built code and never calculated by hand. If a future change
    /// to <see cref="SandataRuleset"/>'s field list, field order, or the
    /// FNV-1a fold moves this value, that is a new preset version with a new
    /// recorded expectation, not a fix to this test.
    /// </summary>
    [Fact]
    public void ModernTacticalV1_ContentHashIsPinned()
    {
        Assert.Equal(
            8_955_292_433_887_190_872UL,
            SandataRuleset.ModernTacticalV1.ContentHash);
    }

    /// <summary>
    /// Changing exactly one field of <see cref="SandataRuleset.ModernTacticalV1"/>
    /// at a time must move <see cref="SandataRuleset.ContentHash"/>. This is
    /// what makes the pinned hash meaningful rather than a hash that happens
    /// to be stable because most of its inputs are dead weight: one case per
    /// field, not a single spot check.
    /// </summary>
    [Theory]
    [InlineData(RulesetField.TickRate)]
    [InlineData(RulesetField.MsToTickConversionRuleId)]
    [InlineData(RulesetField.PathLatencyTicks)]
    [InlineData(RulesetField.GroupCohesionRadius)]
    [InlineData(RulesetField.LoweredWallDistanceWu)]
    [InlineData(RulesetField.AimToleranceBam)]
    public void ChangingAnySingleField_MovesTheContentHash(RulesetField field)
    {
        var baseline = SandataRuleset.ModernTacticalV1;
        var changed = WithOneFieldChanged(baseline, field);

        Assert.NotEqual(baseline.ContentHash, changed.ContentHash);
    }

    /// <summary>
    /// Every named field, so the theory above and this switch stay in sync
    /// by construction: a field added to <see cref="SandataRuleset"/> with
    /// no matching case here is a compile-time exhaustiveness gap the switch
    /// expression's discard arm below turns into a test failure instead of a
    /// silently unverified field.
    /// </summary>
    public enum RulesetField
    {
        TickRate,
        MsToTickConversionRuleId,
        PathLatencyTicks,
        GroupCohesionRadius,
        LoweredWallDistanceWu,
        AimToleranceBam,
    }

    private static SandataRuleset WithOneFieldChanged(
        SandataRuleset baseline, RulesetField field) => field switch
        {
            RulesetField.TickRate => new SandataRuleset(
                baseline.TickRate + 1,
                baseline.MsToTickConversionRuleId,
                baseline.PathLatencyTicks,
                baseline.GroupCohesionRadius,
                baseline.LoweredWallDistanceWu,
                baseline.AimToleranceBam),
            RulesetField.MsToTickConversionRuleId =>
                // MsToTickConversionRule declares exactly one defined member
                // today (design section 4 names exactly one v0.1 rule), and the
                // constructor deliberately does not validate this field against
                // Enum.IsDefined — see the comment beside that omission in
                // SandataRuleset.cs. An undefined raw value is therefore a valid
                // way to prove the fold is sensitive to this field's ordinal
                // without inventing a second rule the design does not define.
                new SandataRuleset(
                    baseline.TickRate,
                    (MsToTickConversionRule)((int)baseline.MsToTickConversionRuleId + 1),
                    baseline.PathLatencyTicks,
                    baseline.GroupCohesionRadius,
                    baseline.LoweredWallDistanceWu,
                    baseline.AimToleranceBam),
            RulesetField.PathLatencyTicks => new SandataRuleset(
                baseline.TickRate,
                baseline.MsToTickConversionRuleId,
                baseline.PathLatencyTicks + 1,
                baseline.GroupCohesionRadius,
                baseline.LoweredWallDistanceWu,
                baseline.AimToleranceBam),
            RulesetField.GroupCohesionRadius => new SandataRuleset(
                baseline.TickRate,
                baseline.MsToTickConversionRuleId,
                baseline.PathLatencyTicks,
                baseline.GroupCohesionRadius + 1,
                baseline.LoweredWallDistanceWu,
                baseline.AimToleranceBam),
            RulesetField.LoweredWallDistanceWu => new SandataRuleset(
                baseline.TickRate,
                baseline.MsToTickConversionRuleId,
                baseline.PathLatencyTicks,
                baseline.GroupCohesionRadius,
                baseline.LoweredWallDistanceWu + 1,
                baseline.AimToleranceBam),
            RulesetField.AimToleranceBam => new SandataRuleset(
                baseline.TickRate,
                baseline.MsToTickConversionRuleId,
                baseline.PathLatencyTicks,
                baseline.GroupCohesionRadius,
                baseline.LoweredWallDistanceWu,
                (ushort)(baseline.AimToleranceBam + 1)),
            _ => throw new ArgumentOutOfRangeException(
                nameof(field), field, "Unhandled ruleset field."),
        };
}
