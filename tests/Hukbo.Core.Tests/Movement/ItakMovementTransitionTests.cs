using Hukbo.Core.Movement;
using Hukbo.Core.Movement.Profiles;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// The Itak disengagement hysteresis boundaries of design section 9.2,
/// called directly on
/// <see cref="WeaponMovementRules.ResolveProvisionalFootwork"/> with the
/// actual solo and shielded Itak profile thresholds rather than the
/// loadout-agnostic tuning of <see cref="FootworkPhaseRulesTests"/>: entry
/// equality enters, release equality leaves, a ratio strictly between the
/// two thresholds preserves the previous state on both sides of the shield
/// split, and zero living enemies never enters or remains under either row.
/// Every threshold consumed here is a provisional reconstruction — gameplay
/// tuning, not a historical measurement
/// (docs/research/movement/itak.md, section 7).
/// </summary>
public sealed class ItakMovementTransitionTests
{
    /// <summary>
    /// The solo Itak (<c>IT</c>) row: disengage entry 12,500 basis points,
    /// release 10,000 — an entry ratio of 1.25 enemies per ally and a
    /// release of 1.0.
    /// </summary>
    private static readonly LoadoutMovementProfile SoloRow =
        ItakMovementProfile.Row;

    /// <summary>
    /// The shielded Itak (<c>IS</c>) row: disengage entry 15,000 basis
    /// points, release 11,000 — an entry ratio of 1.5 enemies per ally and
    /// a release of 1.1.
    /// </summary>
    private static readonly LoadoutMovementProfile ShieldedRow =
        TallHardwoodMovementProfiles.ItakRow;

    private static (FootworkPhase Phase, int TicksRemaining) Resolve(
        LoadoutMovementProfile profile,
        FootworkPhase priorPhase = FootworkPhase.None,
        int supportAllies = 1,
        int supportEnemies = 0,
        bool hasTarget = false,
        bool targetAtOrInsidePreferredDistance = false) =>
        WeaponMovementRules.ResolveProvisionalFootwork(
            isAlive: true,
            priorPhase,
            priorTicksRemaining: 0,
            TacticalPosture.Hold,
            supportAllies,
            supportEnemies,
            profile.DisengageEnemyToAllyBasisPoints,
            profile.ReengageEnemyToAllyBasisPoints,
            profile.RecoveryTicks,
            hasTarget,
            targetAtOrInsidePreferredDistance);

    // ----- Solo Itak hysteresis (entry 12500, release 10000) -----

    /// <summary>
    /// Entry equality enters: five enemies against four allies sits exactly
    /// on the solo entry threshold, 5 &#215; 10000 = 4 &#215; 12500.
    /// </summary>
    [Fact]
    public void TheSoloItakEntryEqualityEntersDisengagement() =>
        Assert.Equal(
            (FootworkPhase.Disengage, 0),
            Resolve(SoloRow, supportAllies: 4, supportEnemies: 5));

    /// <summary>
    /// Release equality leaves: four enemies against four allies sits
    /// exactly on the solo release threshold, 4 &#215; 10000 =
    /// 4 &#215; 10000, so an already-disengaging agent falls through — here
    /// to <c>None</c>, having no target and a <c>Hold</c> posture.
    /// </summary>
    [Fact]
    public void TheSoloItakReleaseEqualityLeavesDisengagement() =>
        Assert.Equal(
            (FootworkPhase.None, 0),
            Resolve(
                SoloRow,
                priorPhase: FootworkPhase.Disengage,
                supportAllies: 4,
                supportEnemies: 4));

    /// <summary>
    /// Six enemies against five allies is ratio 1.2, strictly between the
    /// solo release of 1.0 (60000 &gt; 50000, so a disengaging agent
    /// remains) and the solo entry of 1.25 (60000 &lt; 62500, so an engaged
    /// one does not enter).
    /// </summary>
    [Fact]
    public void StrictlyBetweenTheSoloItakThresholdsADisengagingAgentRemains() =>
        Assert.Equal(
            (FootworkPhase.Disengage, 0),
            Resolve(
                SoloRow,
                priorPhase: FootworkPhase.Disengage,
                supportAllies: 5,
                supportEnemies: 6));

    [Fact]
    public void StrictlyBetweenTheSoloItakThresholdsAnEngagedAgentDoesNotEnter() =>
        Assert.Equal(
            (FootworkPhase.Engage, 0),
            Resolve(
                SoloRow,
                priorPhase: FootworkPhase.Engage,
                supportAllies: 5,
                supportEnemies: 6,
                hasTarget: true,
                targetAtOrInsidePreferredDistance: true));

    // ----- Shielded Itak hysteresis (entry 15000, release 11000) -----

    /// <summary>
    /// Entry equality enters: three enemies against two allies sits exactly
    /// on the shielded entry threshold, 3 &#215; 10000 = 2 &#215; 15000.
    /// </summary>
    [Fact]
    public void TheShieldedItakEntryEqualityEntersDisengagement() =>
        Assert.Equal(
            (FootworkPhase.Disengage, 0),
            Resolve(ShieldedRow, supportAllies: 2, supportEnemies: 3));

    /// <summary>
    /// Release equality leaves: eleven enemies against ten allies sits
    /// exactly on the shielded release threshold, 11 &#215; 10000 =
    /// 10 &#215; 11000. The shielded release of 1.1 sits above the solo
    /// 1.0, so this same count keeps a solo warrior disengaging while the
    /// shielded one falls through (design section 13.3).
    /// </summary>
    [Fact]
    public void TheShieldedItakReleaseEqualityLeavesDisengagement() =>
        Assert.Equal(
            (FootworkPhase.None, 0),
            Resolve(
                ShieldedRow,
                priorPhase: FootworkPhase.Disengage,
                supportAllies: 10,
                supportEnemies: 11));

    /// <summary>
    /// Six enemies against five allies is ratio 1.2, strictly between the
    /// shielded release of 1.1 (60000 &gt; 55000, so a disengaging agent
    /// remains) and the shielded entry of 1.5 (60000 &lt; 75000, so an
    /// engaged one does not enter).
    /// </summary>
    [Fact]
    public void StrictlyBetweenTheShieldedItakThresholdsADisengagingAgentRemains() =>
        Assert.Equal(
            (FootworkPhase.Disengage, 0),
            Resolve(
                ShieldedRow,
                priorPhase: FootworkPhase.Disengage,
                supportAllies: 5,
                supportEnemies: 6));

    [Fact]
    public void StrictlyBetweenTheShieldedItakThresholdsAnEngagedAgentDoesNotEnter() =>
        Assert.Equal(
            (FootworkPhase.Engage, 0),
            Resolve(
                ShieldedRow,
                priorPhase: FootworkPhase.Engage,
                supportAllies: 5,
                supportEnemies: 6,
                hasTarget: true,
                targetAtOrInsidePreferredDistance: true));

    // ----- The zero-enemy rule under both Itak rows -----

    /// <summary>
    /// Design section 9.2's zero-enemy rule holds on the Itak thresholds
    /// with no special case: zero living enemies never enters and never
    /// remains in disengagement under either row, on the ratio arithmetic
    /// alone.
    /// </summary>
    [Theory]
    [InlineData(false, FootworkPhase.None)]
    [InlineData(false, FootworkPhase.Disengage)]
    [InlineData(true, FootworkPhase.None)]
    [InlineData(true, FootworkPhase.Disengage)]
    public void ZeroEnemiesNeverEntersAndNeverRemainsUnderEitherItakRow(
        bool shielded,
        FootworkPhase priorPhase) =>
        Assert.Equal(
            (FootworkPhase.None, 0),
            Resolve(
                shielded ? ShieldedRow : SoloRow,
                priorPhase: priorPhase,
                supportAllies: 1,
                supportEnemies: 0));
}
