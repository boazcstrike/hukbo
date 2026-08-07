using Hukbo.Core.Movement;

namespace Hukbo.Core.Tests;

/// <summary>
/// Exercises <see cref="MovementPresetRegistry"/>'s exhaustive-switch shape:
/// the one registered value resolves and the pinned ruleset it resolves to
/// hashes to a literal this task records, while every unregistered value
/// fails loudly through both <see cref="MovementPresetRegistry.IsRegistered"/>
/// and <see cref="MovementPresetRegistry.Get"/> rather than falling back to a
/// default.
/// </summary>
public sealed class MovementPresetRegistryTests
{
    /// <summary>
    /// Pins <c>IndependentPursuitV1</c>'s <see cref="MovementRuleset.ContentHash"/>
    /// to its current field values. This is an identity assertion over the
    /// ruleset's own fields, not a behavioural golden — it does not reach
    /// the state hash, so it moves whenever a task adds a field to
    /// <see cref="MovementRuleset"/>, most recently the four
    /// equipment-relative footwork members (<c>UsesEquipmentRelativeFootwork</c>,
    /// the two context radii, and <c>LoadoutMovementProfiles</c>), and before
    /// that <c>SelectsLeaderByRank</c>, and before that
    /// <c>NarrowsCohesionScanToCohesionCapableContingents</c>, and before that
    /// T2's <c>CloseFractionNumerator</c> and <c>CloseFractionDenominator</c>.
    /// All five V1-through-V5 literals below were recomputed from the built
    /// code when the equipment-relative footwork members landed, never
    /// calculated by hand.
    /// What stays frozen across such a change is the preset's simulated
    /// behaviour, proved instead by
    /// <c>IndependentPursuitV1_ReproducesTheFrozenTrajectoryDigest</c>.
    /// </summary>
    private const ulong IndependentPursuitV1ContentHash = 0x36BAA7847258E4E3UL;

    /// <summary>
    /// Pinned by T9 against <c>PersistentContingentsV2</c>'s constant set,
    /// which is identical to <c>IndependentPursuitV1</c>'s field-for-field —
    /// only <see cref="Movement.MovementRuleset.Id"/> differs — so this
    /// literal differs from <see cref="IndependentPursuitV1ContentHash"/>
    /// only through the folded <c>Id</c> field.
    /// </summary>
    private const ulong PersistentContingentsV2ContentHash = 0x0E37E2FD11051440UL;

    /// <summary>
    /// Pinned by T5 against <c>PersistentContingentsV3</c>'s constant set,
    /// which differs from <c>PersistentContingentsV2</c>'s only in
    /// <c>CloseFractionNumerator</c> and <c>CloseFractionDenominator</c>
    /// (<c>1, 2</c> instead of <c>0, 1</c>), so this literal differs from
    /// both existing literals.
    /// </summary>
    private const ulong PersistentContingentsV3ContentHash = 0x47E9B3788ACE6783UL;

    /// <summary>
    /// Pinned against <c>PersistentContingentsV4</c>'s constant set, which
    /// differs from <c>PersistentContingentsV3</c>'s only in
    /// <c>NarrowsCohesionScanToCohesionCapableContingents</c>
    /// (<see langword="true"/> instead of <see langword="false"/>), so this
    /// literal differs from all three existing literals.
    /// </summary>
    private const ulong PersistentContingentsV4ContentHash = 0x26302C0CF6885235UL;

    /// <summary>
    /// Pinned against <c>PersistentContingentsV5</c>'s constant set, which
    /// differs from <c>PersistentContingentsV4</c>'s only in
    /// <c>SelectsLeaderByRank</c> (<see langword="true"/> instead of
    /// <see langword="false"/>), so this literal differs from all four
    /// existing literals.
    /// </summary>
    private const ulong PersistentContingentsV5ContentHash = 0x84FA62B037FAC275UL;

    /// <summary>
    /// Pinned against <c>EquipmentRelativeFootworkV6</c>'s constant set,
    /// which differs from <c>PersistentContingentsV5</c>'s in
    /// <c>UsesEquipmentRelativeFootwork</c> (<see langword="true"/>), both
    /// context radii, and the six loadout movement profile rows folded per
    /// design section 5.1, so this literal differs from all five existing
    /// literals.
    /// </summary>
    private const ulong EquipmentRelativeFootworkV6ContentHash = 0x0FFE5D202B324D25UL;

    /// <summary>
    /// Pinned by the pressure-interrupt plan's task E2 against
    /// <c>EquipmentRelativeFootworkV7</c>'s constant set, once task E1 had
    /// settled the four tuning values for good. V7 carries every one of
    /// <c>EquipmentRelativeFootworkV6</c>'s field values unchanged and differs
    /// from it in <see cref="MovementRuleset.Id"/>, in
    /// <c>AppliesPressureInterrupt</c> (<see langword="true"/>), and in the
    /// four values that flag gates: the three signal weights folded once for
    /// the ruleset and the sixteenth per-row scalar,
    /// <c>PressureInterruptThresholdBasisPoints</c>, folded once per profile
    /// row. Those four fold inside <c>if (AppliesPressureInterrupt)</c>
    /// precisely so that this seventh literal can exist without moving the six
    /// above it, and
    /// <see cref="EquipmentRelativeFootworkV7ContentHashMatchesThePinnedLiteral"/>
    /// states that separation as six <c>NotEqual</c> assertions.
    /// Recorded from the built code, never calculated by hand.
    /// </summary>
    /// <remarks>
    /// Task E1 measured that V7's tuning does not meet the design section 2.1
    /// termination bar at any setting it tried, and left the values where they
    /// stand for the reasons recorded in
    /// docs/archives/2026-08-06/movement/2026-07-31-movement-v7-calibration-record.md. That is a
    /// finding about what the preset achieves; this literal records what the
    /// preset is, which is frozen either way.
    /// </remarks>
    private const ulong EquipmentRelativeFootworkV7ContentHash = 0x66F4FDF91F56AF1BUL;

    [Fact]
    public void IndependentPursuitV1IsRegistered()
    {
        Assert.True(MovementPresetRegistry.IsRegistered(MovementPresetId.IndependentPursuitV1));
    }

    [Fact]
    public void PersistentContingentsV2IsRegistered()
    {
        Assert.True(MovementPresetRegistry.IsRegistered(MovementPresetId.PersistentContingentsV2));
    }

    [Fact]
    public void PersistentContingentsV3IsRegistered()
    {
        Assert.True(MovementPresetRegistry.IsRegistered(MovementPresetId.PersistentContingentsV3));
    }

    [Fact]
    public void PersistentContingentsV4IsRegistered()
    {
        Assert.True(MovementPresetRegistry.IsRegistered(MovementPresetId.PersistentContingentsV4));
    }

    [Fact]
    public void PersistentContingentsV5IsRegistered()
    {
        Assert.True(MovementPresetRegistry.IsRegistered(MovementPresetId.PersistentContingentsV5));
    }

    [Fact]
    public void EquipmentRelativeFootworkV6IsRegistered()
    {
        Assert.True(MovementPresetRegistry.IsRegistered(MovementPresetId.EquipmentRelativeFootworkV6));
    }

    [Fact]
    public void EquipmentRelativeFootworkV7IsRegistered()
    {
        Assert.True(MovementPresetRegistry.IsRegistered(MovementPresetId.EquipmentRelativeFootworkV7));
    }

    [Fact]
    public void TheZeroValueIsNotRegistered()
    {
        Assert.False(MovementPresetRegistry.IsRegistered((MovementPresetId)0));
    }

    [Fact]
    public void AnUnassignedHighValueIsNotRegistered()
    {
        Assert.False(MovementPresetRegistry.IsRegistered((MovementPresetId)99));
    }

    [Fact]
    public void GetThrowsForTheZeroValue()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MovementPresetRegistry.Get((MovementPresetId)0));
    }

    [Fact]
    public void GetThrowsForAnUnassignedHighValue()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MovementPresetRegistry.Get((MovementPresetId)99));
    }

    [Fact]
    public void IndependentPursuitV1ContentHashMatchesThePinnedLiteral()
    {
        var ruleset = MovementPresetRegistry.Get(MovementPresetId.IndependentPursuitV1);

        Assert.Equal(IndependentPursuitV1ContentHash, ruleset.ContentHash);
    }

    /// <summary>
    /// Pins <c>PersistentContingentsV2</c>'s content hash to a literal
    /// distinct from <c>IndependentPursuitV1</c>'s, satisfying T9's own
    /// verification criterion: introducing the second preset must move
    /// nothing about the first.
    /// </summary>
    [Fact]
    public void PersistentContingentsV2ContentHashMatchesThePinnedLiteral()
    {
        var ruleset = MovementPresetRegistry.Get(MovementPresetId.PersistentContingentsV2);

        Assert.Equal(PersistentContingentsV2ContentHash, ruleset.ContentHash);
        Assert.NotEqual(IndependentPursuitV1ContentHash, ruleset.ContentHash);
    }

    /// <summary>
    /// Pins <c>PersistentContingentsV3</c>'s content hash to a literal
    /// distinct from both existing literals, satisfying T5's own
    /// verification criterion: introducing the third preset must move
    /// nothing about the first two.
    /// </summary>
    [Fact]
    public void PersistentContingentsV3ContentHashMatchesThePinnedLiteral()
    {
        var ruleset = MovementPresetRegistry.Get(MovementPresetId.PersistentContingentsV3);

        Assert.Equal(PersistentContingentsV3ContentHash, ruleset.ContentHash);
        Assert.NotEqual(IndependentPursuitV1ContentHash, ruleset.ContentHash);
        Assert.NotEqual(PersistentContingentsV2ContentHash, ruleset.ContentHash);
    }

    /// <summary>
    /// Pins <c>PersistentContingentsV4</c>'s content hash to a literal
    /// distinct from all three existing literals: introducing the fourth
    /// preset must move nothing about the first three.
    /// </summary>
    [Fact]
    public void PersistentContingentsV4ContentHashMatchesThePinnedLiteral()
    {
        var ruleset = MovementPresetRegistry.Get(MovementPresetId.PersistentContingentsV4);

        Assert.Equal(PersistentContingentsV4ContentHash, ruleset.ContentHash);
        Assert.NotEqual(IndependentPursuitV1ContentHash, ruleset.ContentHash);
        Assert.NotEqual(PersistentContingentsV2ContentHash, ruleset.ContentHash);
        Assert.NotEqual(PersistentContingentsV3ContentHash, ruleset.ContentHash);
    }

    /// <summary>
    /// Pins <c>PersistentContingentsV5</c>'s content hash to a literal
    /// distinct from all four existing literals: introducing the fifth
    /// preset must move nothing about the first four.
    /// </summary>
    [Fact]
    public void PersistentContingentsV5ContentHashMatchesThePinnedLiteral()
    {
        var ruleset = MovementPresetRegistry.Get(MovementPresetId.PersistentContingentsV5);

        Assert.Equal(PersistentContingentsV5ContentHash, ruleset.ContentHash);
        Assert.NotEqual(IndependentPursuitV1ContentHash, ruleset.ContentHash);
        Assert.NotEqual(PersistentContingentsV2ContentHash, ruleset.ContentHash);
        Assert.NotEqual(PersistentContingentsV3ContentHash, ruleset.ContentHash);
        Assert.NotEqual(PersistentContingentsV4ContentHash, ruleset.ContentHash);
    }

    /// <summary>
    /// Pins <c>EquipmentRelativeFootworkV6</c>'s content hash to a literal
    /// distinct from all five existing literals: introducing the sixth
    /// preset must move nothing about the first five.
    /// </summary>
    [Fact]
    public void EquipmentRelativeFootworkV6ContentHashMatchesThePinnedLiteral()
    {
        var ruleset = MovementPresetRegistry.Get(MovementPresetId.EquipmentRelativeFootworkV6);

        Assert.Equal(EquipmentRelativeFootworkV6ContentHash, ruleset.ContentHash);
        Assert.NotEqual(IndependentPursuitV1ContentHash, ruleset.ContentHash);
        Assert.NotEqual(PersistentContingentsV2ContentHash, ruleset.ContentHash);
        Assert.NotEqual(PersistentContingentsV3ContentHash, ruleset.ContentHash);
        Assert.NotEqual(PersistentContingentsV4ContentHash, ruleset.ContentHash);
        Assert.NotEqual(PersistentContingentsV5ContentHash, ruleset.ContentHash);
    }

    /// <summary>
    /// Pins <c>EquipmentRelativeFootworkV7</c>'s content hash to a literal
    /// distinct from all six existing literals: introducing the seventh preset
    /// must move nothing about the first six. The comparison against
    /// <see cref="EquipmentRelativeFootworkV6ContentHash"/> is the load-bearing
    /// one, because V7 is registered from V6's own field values plus the four
    /// pressure-interrupt values alone. Were those four folded
    /// unconditionally rather than behind the version gate, V6's literal would
    /// have moved when V7 shipped, and the state hash and frozen trajectory
    /// digest that literal reaches would have moved with it.
    /// </summary>
    [Fact]
    public void EquipmentRelativeFootworkV7ContentHashMatchesThePinnedLiteral()
    {
        var ruleset = MovementPresetRegistry.Get(MovementPresetId.EquipmentRelativeFootworkV7);

        Assert.Equal(EquipmentRelativeFootworkV7ContentHash, ruleset.ContentHash);
        Assert.NotEqual(IndependentPursuitV1ContentHash, ruleset.ContentHash);
        Assert.NotEqual(PersistentContingentsV2ContentHash, ruleset.ContentHash);
        Assert.NotEqual(PersistentContingentsV3ContentHash, ruleset.ContentHash);
        Assert.NotEqual(PersistentContingentsV4ContentHash, ruleset.ContentHash);
        Assert.NotEqual(PersistentContingentsV5ContentHash, ruleset.ContentHash);
        Assert.NotEqual(EquipmentRelativeFootworkV6ContentHash, ruleset.ContentHash);
    }

    /// <summary>
    /// The narrowing flag is what separates <c>PersistentContingentsV4</c>
    /// from <c>PersistentContingentsV3</c>, and no earlier preset carries it,
    /// so a preset added later cannot quietly turn it on for a frozen
    /// trajectory without this Fact failing. <c>PersistentContingentsV5</c>
    /// and <c>EquipmentRelativeFootworkV6</c> both carry the flag forward
    /// deliberately — V5 layers rank-aware leader selection on V4's
    /// narrowed scan, and V6 carries V5's cohesion tunables unchanged — and
    /// this Fact states both so neither value can drift silently, as does
    /// <c>EquipmentRelativeFootworkV7</c>, which carries V6's cohesion
    /// tunables forward unchanged in turn.
    /// </summary>
    /// <remarks>
    /// The name has been inaccurate since V5 shipped — the body already
    /// asserts <see langword="true"/> for V5 and V6 — and it is left alone
    /// deliberately: renaming it here would be churn that hides the real diff.
    /// Pressure-interrupt design section 8 records that decision.
    /// </remarks>
    [Fact]
    public void OnlyPersistentContingentsV4NarrowsTheCrossContingentScan()
    {
        Assert.False(MovementPresetRegistry
            .Get(MovementPresetId.IndependentPursuitV1)
            .NarrowsCohesionScanToCohesionCapableContingents);
        Assert.False(MovementPresetRegistry
            .Get(MovementPresetId.PersistentContingentsV2)
            .NarrowsCohesionScanToCohesionCapableContingents);
        Assert.False(MovementPresetRegistry
            .Get(MovementPresetId.PersistentContingentsV3)
            .NarrowsCohesionScanToCohesionCapableContingents);
        Assert.True(MovementPresetRegistry
            .Get(MovementPresetId.PersistentContingentsV4)
            .NarrowsCohesionScanToCohesionCapableContingents);
        Assert.True(MovementPresetRegistry
            .Get(MovementPresetId.PersistentContingentsV5)
            .NarrowsCohesionScanToCohesionCapableContingents);
        Assert.True(MovementPresetRegistry
            .Get(MovementPresetId.EquipmentRelativeFootworkV6)
            .NarrowsCohesionScanToCohesionCapableContingents);
        Assert.True(MovementPresetRegistry
            .Get(MovementPresetId.EquipmentRelativeFootworkV7)
            .NarrowsCohesionScanToCohesionCapableContingents);
    }

    /// <summary>
    /// The rank-selection flag is what separates
    /// <c>PersistentContingentsV5</c> from every earlier preset; no earlier
    /// preset carries it, so a preset added later cannot quietly turn it on
    /// for a frozen trajectory without this Fact failing.
    /// <c>EquipmentRelativeFootworkV6</c> carries the flag forward
    /// deliberately — it carries V5's cohesion tunables unchanged — and so
    /// does <c>EquipmentRelativeFootworkV7</c> in turn; this Fact states both
    /// values so neither can drift silently.
    /// </summary>
    [Fact]
    public void OnlyPersistentContingentsV5SelectsLeaderByRank()
    {
        Assert.False(MovementPresetRegistry
            .Get(MovementPresetId.IndependentPursuitV1)
            .SelectsLeaderByRank);
        Assert.False(MovementPresetRegistry
            .Get(MovementPresetId.PersistentContingentsV2)
            .SelectsLeaderByRank);
        Assert.False(MovementPresetRegistry
            .Get(MovementPresetId.PersistentContingentsV3)
            .SelectsLeaderByRank);
        Assert.False(MovementPresetRegistry
            .Get(MovementPresetId.PersistentContingentsV4)
            .SelectsLeaderByRank);
        Assert.True(MovementPresetRegistry
            .Get(MovementPresetId.PersistentContingentsV5)
            .SelectsLeaderByRank);
        Assert.True(MovementPresetRegistry
            .Get(MovementPresetId.EquipmentRelativeFootworkV6)
            .SelectsLeaderByRank);
        Assert.True(MovementPresetRegistry
            .Get(MovementPresetId.EquipmentRelativeFootworkV7)
            .SelectsLeaderByRank);
    }

    /// <summary>
    /// The equipment-relative footwork flag is what separates
    /// <c>EquipmentRelativeFootworkV6</c> from every earlier preset; no
    /// earlier preset carries it, so a preset added later cannot quietly
    /// turn it on for a frozen trajectory without this Fact failing.
    /// <c>EquipmentRelativeFootworkV7</c> carries the flag forward
    /// deliberately — the pressure interrupt is evaluated inside a stage only
    /// this flag runs, which is why
    /// <see cref="MovementRuleset.AppliesPressureInterrupt"/> may be
    /// <see langword="true"/> only alongside it — and this Fact states that
    /// value so it cannot drift silently.
    /// </summary>
    /// <remarks>
    /// The name has been inaccurate since V7 shipped, for the same reason and
    /// with the same deliberate decision recorded in
    /// <c>OnlyPersistentContingentsV4NarrowsTheCrossContingentScan</c>.
    /// </remarks>
    [Fact]
    public void OnlyEquipmentRelativeFootworkV6UsesEquipmentRelativeFootwork()
    {
        Assert.False(MovementPresetRegistry
            .Get(MovementPresetId.IndependentPursuitV1)
            .UsesEquipmentRelativeFootwork);
        Assert.False(MovementPresetRegistry
            .Get(MovementPresetId.PersistentContingentsV2)
            .UsesEquipmentRelativeFootwork);
        Assert.False(MovementPresetRegistry
            .Get(MovementPresetId.PersistentContingentsV3)
            .UsesEquipmentRelativeFootwork);
        Assert.False(MovementPresetRegistry
            .Get(MovementPresetId.PersistentContingentsV4)
            .UsesEquipmentRelativeFootwork);
        Assert.False(MovementPresetRegistry
            .Get(MovementPresetId.PersistentContingentsV5)
            .UsesEquipmentRelativeFootwork);
        Assert.True(MovementPresetRegistry
            .Get(MovementPresetId.EquipmentRelativeFootworkV6)
            .UsesEquipmentRelativeFootwork);
        Assert.True(MovementPresetRegistry
            .Get(MovementPresetId.EquipmentRelativeFootworkV7)
            .UsesEquipmentRelativeFootwork);
    }

    /// <summary>
    /// The pressure-interrupt flag is what separates
    /// <c>EquipmentRelativeFootworkV7</c> from every earlier preset; no
    /// earlier preset carries it, so a preset added later cannot quietly turn
    /// it on for a frozen trajectory without this Fact failing. That matters
    /// more here than for the three flags above, because this flag is the
    /// version gate the four pressure-interrupt values fold behind: were it
    /// ever registered <see langword="true"/> on V6, V6's
    /// <see cref="MovementRuleset.ContentHash"/> would move, and with it the
    /// state hash and the frozen trajectory digest that hash records.
    /// </summary>
    /// <remarks>
    /// It also states the value the V7 entry itself registers. A V7 entry that
    /// left the flag to its trailing constructor default would compile
    /// cleanly, register <see langword="false"/>, and silently never fire the
    /// feature the preset exists for; this Fact is what makes that failure
    /// loud.
    /// </remarks>
    [Fact]
    public void OnlyEquipmentRelativeFootworkV7AppliesThePressureInterrupt()
    {
        Assert.False(MovementPresetRegistry
            .Get(MovementPresetId.IndependentPursuitV1)
            .AppliesPressureInterrupt);
        Assert.False(MovementPresetRegistry
            .Get(MovementPresetId.PersistentContingentsV2)
            .AppliesPressureInterrupt);
        Assert.False(MovementPresetRegistry
            .Get(MovementPresetId.PersistentContingentsV3)
            .AppliesPressureInterrupt);
        Assert.False(MovementPresetRegistry
            .Get(MovementPresetId.PersistentContingentsV4)
            .AppliesPressureInterrupt);
        Assert.False(MovementPresetRegistry
            .Get(MovementPresetId.PersistentContingentsV5)
            .AppliesPressureInterrupt);
        Assert.False(MovementPresetRegistry
            .Get(MovementPresetId.EquipmentRelativeFootworkV6)
            .AppliesPressureInterrupt);
        Assert.True(MovementPresetRegistry
            .Get(MovementPresetId.EquipmentRelativeFootworkV7)
            .AppliesPressureInterrupt);
    }
}
