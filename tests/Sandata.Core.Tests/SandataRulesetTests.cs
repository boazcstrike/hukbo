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
    /// <remarks>
    /// Task 63 revisited this pin. The plan's wave-2 note left
    /// <see cref="SandataRuleset.PathLatencyTicks"/>,
    /// <see cref="SandataRuleset.GroupCohesionRadiusWu"/>,
    /// <see cref="SandataRuleset.LoweredWallDistanceWu"/>, and
    /// <see cref="SandataRuleset.AimToleranceBam"/> as task 9's placeholders,
    /// naming tasks 27, 28, 32, and 23 to confirm or replace each one and
    /// move this hash if they did. Those four verdicts were never recorded
    /// anywhere in the plan, so task 63 re-derived each value directly from
    /// its consuming code and tests — see the remarks on
    /// <see cref="SandataRuleset"/> and on each of the four properties — and
    /// found no evidence in any of them for a different number. This pin is
    /// therefore unchanged: the hash moves if and only if a value moves, and
    /// none did. Revising <see cref="SandataRuleset.ModernTacticalV1"/> in
    /// place, rather than adding a new <see cref="SandataPresetId"/> member,
    /// remains the right call for a future change that does move a value,
    /// because no golden replay, no save file, and no recorded Sandata
    /// baseline references this preset yet — design section 16 states
    /// explicitly that no golden mission hash exists — so there is no
    /// earlier artifact for a version bump to protect. That stops being true
    /// the moment one is recorded.
    /// </remarks>
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
    [InlineData(RulesetField.GroupCohesionRadiusWu)]
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
        GroupCohesionRadiusWu,
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
                baseline.GroupCohesionRadiusWu,
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
                    baseline.GroupCohesionRadiusWu,
                    baseline.LoweredWallDistanceWu,
                    baseline.AimToleranceBam),
            RulesetField.PathLatencyTicks => new SandataRuleset(
                baseline.TickRate,
                baseline.MsToTickConversionRuleId,
                baseline.PathLatencyTicks + 1,
                baseline.GroupCohesionRadiusWu,
                baseline.LoweredWallDistanceWu,
                baseline.AimToleranceBam),
            RulesetField.GroupCohesionRadiusWu => new SandataRuleset(
                baseline.TickRate,
                baseline.MsToTickConversionRuleId,
                baseline.PathLatencyTicks,
                baseline.GroupCohesionRadiusWu + 1,
                baseline.LoweredWallDistanceWu,
                baseline.AimToleranceBam),
            RulesetField.LoweredWallDistanceWu => new SandataRuleset(
                baseline.TickRate,
                baseline.MsToTickConversionRuleId,
                baseline.PathLatencyTicks,
                baseline.GroupCohesionRadiusWu,
                baseline.LoweredWallDistanceWu + 1,
                baseline.AimToleranceBam),
            RulesetField.AimToleranceBam => new SandataRuleset(
                baseline.TickRate,
                baseline.MsToTickConversionRuleId,
                baseline.PathLatencyTicks,
                baseline.GroupCohesionRadiusWu,
                baseline.LoweredWallDistanceWu,
                (ushort)(baseline.AimToleranceBam + 1)),
            _ => throw new ArgumentOutOfRangeException(
                nameof(field), field, "Unhandled ruleset field."),
        };
}
