using Hukbo.Core.Movement;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// The ten-step provisional footwork lifecycle of design section 9.1 and
/// the lane-clearance finalisation of section 9.4, called directly on
/// <see cref="WeaponMovementRules.ResolveProvisionalFootwork"/> and
/// <see cref="WeaponMovementRules.FinalizeFootwork"/> with hand-built
/// scalar inputs: first-match ordering of the ratio steps, both hysteresis
/// equality sides, the strictly-between preservation, the zero-enemy rule,
/// the actor-only ally count, the <c>Commit</c>/<c>Recover</c> timer
/// lifecycle with its entry-tick semantics, the unconditional posture step,
/// and the full nine-phase finalisation matrix on both lane outcomes.
/// </summary>
public sealed class FootworkPhaseRulesTests
{
    /// <summary>
    /// Section 13.1's rows put the disengage entry at 1.5 enemies per ally
    /// and the release below it; the exact tuning does not matter here,
    /// only that entry and release are distinct with release strictly
    /// lower, as <c>LoadoutMovementProfile</c> validation requires.
    /// </summary>
    private const int DisengageBasisPoints = 15_000;

    private const int ReengageBasisPoints = 10_000;

    private const int RecoveryTicks = 4;

    private static (FootworkPhase Phase, int TicksRemaining) Resolve(
        bool isAlive = true,
        FootworkPhase priorPhase = FootworkPhase.None,
        int priorTicksRemaining = 0,
        TacticalPosture posture = TacticalPosture.Hold,
        int supportAllies = 1,
        int supportEnemies = 0,
        bool hasTarget = false,
        bool targetAtOrInsidePreferredDistance = false) =>
        WeaponMovementRules.ResolveProvisionalFootwork(
            isAlive,
            priorPhase,
            priorTicksRemaining,
            posture,
            supportAllies,
            supportEnemies,
            DisengageBasisPoints,
            ReengageBasisPoints,
            RecoveryTicks,
            hasTarget,
            targetAtOrInsidePreferredDistance);

    // ----- 9.1 step 1: dead -----

    [Fact]
    public void ADeadAgentResolvesNoneWithAZeroTimerBeforeEveryOtherStep() =>
        Assert.Equal(
            (FootworkPhase.None, 0),
            Resolve(
                isAlive: false,
                priorPhase: FootworkPhase.Commit,
                priorTicksRemaining: 5,
                posture: TacticalPosture.Withdraw,
                supportAllies: 1,
                supportEnemies: 9));

    /// <summary>
    /// The dead step short-circuits before the living-agent precondition
    /// that <c>SupportAllies</c> is at least one, because a dead agent
    /// counts nowhere, including on its own side.
    /// </summary>
    [Fact]
    public void ADeadAgentIsResolvedBeforeTheSupportAllyPrecondition() =>
        Assert.Equal(
            (FootworkPhase.None, 0),
            Resolve(isAlive: false, supportAllies: 0));

    // ----- 9.1 steps 2 and 3: the Commit/Recover timer lifecycle -----

    /// <summary>
    /// Design section 9.5: an entry timer counts the current tick, so a
    /// <c>Commit</c> entered with duration 3 is seen by this resolver as
    /// prior timer 3 and decrements through 2 and 1 before expiring.
    /// </summary>
    [Theory]
    [InlineData(3, 2)]
    [InlineData(2, 1)]
    public void AContinuingCommitDecrementsItsTimer(
        int priorTicks,
        int expectedTicks) =>
        Assert.Equal(
            (FootworkPhase.Commit, expectedTicks),
            Resolve(
                priorPhase: FootworkPhase.Commit,
                priorTicksRemaining: priorTicks));

    [Fact]
    public void AnExpiringCommitEntersRecoverWithTheProfileRecoveryDuration() =>
        Assert.Equal(
            (FootworkPhase.Recover, RecoveryTicks),
            Resolve(
                priorPhase: FootworkPhase.Commit,
                priorTicksRemaining: 1));

    /// <summary>
    /// Step 2 precedes the ratio steps: an expiring <c>Commit</c> enters
    /// <c>Recover</c> even under a local disadvantage that step 5 would
    /// otherwise turn into <c>Disengage</c>.
    /// </summary>
    [Fact]
    public void TheCommitLifecycleOutranksTheDisengagementRatioSteps() =>
        Assert.Equal(
            (FootworkPhase.Recover, RecoveryTicks),
            Resolve(
                priorPhase: FootworkPhase.Commit,
                priorTicksRemaining: 1,
                supportAllies: 1,
                supportEnemies: 9));

    [Theory]
    [InlineData(4, 3)]
    [InlineData(2, 1)]
    public void AContinuingRecoverDecrementsItsTimer(
        int priorTicks,
        int expectedTicks) =>
        Assert.Equal(
            (FootworkPhase.Recover, expectedTicks),
            Resolve(
                priorPhase: FootworkPhase.Recover,
                priorTicksRemaining: priorTicks));

    /// <summary>
    /// Step 3's expiry does not return: an expiring <c>Recover</c> falls
    /// through to the rules below and resolves whatever they demand.
    /// </summary>
    [Fact]
    public void AnExpiringRecoverFallsThroughToTheTargetSteps() =>
        Assert.Equal(
            (FootworkPhase.Engage, 0),
            Resolve(
                priorPhase: FootworkPhase.Recover,
                priorTicksRemaining: 1,
                hasTarget: true,
                targetAtOrInsidePreferredDistance: true));

    [Fact]
    public void AnExpiringRecoverFallsThroughToTheDisengagementEntry() =>
        Assert.Equal(
            (FootworkPhase.Disengage, 0),
            Resolve(
                priorPhase: FootworkPhase.Recover,
                priorTicksRemaining: 1,
                supportAllies: 2,
                supportEnemies: 3));

    // ----- 9.1 steps 4 and 5, 9.2: hysteresis -----

    /// <summary>
    /// Entry equality enters: two allies against three enemies sits exactly
    /// at the 1.5 entry ratio.
    /// </summary>
    [Fact]
    public void EntryEqualityEntersDisengagement() =>
        Assert.Equal(
            (FootworkPhase.Disengage, 0),
            Resolve(supportAllies: 2, supportEnemies: 3));

    /// <summary>
    /// Release equality leaves: one enemy per ally sits exactly at the 1.0
    /// release ratio, so an already-disengaging agent falls through — here
    /// to <c>None</c>, having no target and a <c>Hold</c> posture.
    /// </summary>
    [Fact]
    public void ReleaseEqualityLeavesDisengagement() =>
        Assert.Equal(
            (FootworkPhase.None, 0),
            Resolve(
                priorPhase: FootworkPhase.Disengage,
                supportAllies: 3,
                supportEnemies: 3));

    /// <summary>
    /// A ratio strictly between the two thresholds preserves the previous
    /// state, on both sides: an already-disengaging agent remains, and an
    /// engaged one does not enter. Four allies against five enemies is
    /// 1.25, strictly between the 1.0 release and the 1.5 entry.
    /// </summary>
    [Fact]
    public void StrictlyBetweenTheThresholdsAnAgentRemainsDisengaging() =>
        Assert.Equal(
            (FootworkPhase.Disengage, 0),
            Resolve(
                priorPhase: FootworkPhase.Disengage,
                supportAllies: 4,
                supportEnemies: 5));

    [Fact]
    public void StrictlyBetweenTheThresholdsAnEngagedAgentDoesNotEnter() =>
        Assert.Equal(
            (FootworkPhase.Engage, 0),
            Resolve(
                priorPhase: FootworkPhase.Engage,
                supportAllies: 4,
                supportEnemies: 5,
                hasTarget: true,
                targetAtOrInsidePreferredDistance: true));

    /// <summary>
    /// The release, step 4, is consulted before the entry, step 5: an
    /// already-disengaging agent above the entry ratio remains through the
    /// release test, never re-enters, and carries no timer.
    /// </summary>
    [Fact]
    public void AnAlreadyDisengagingAgentAboveTheEntryRatioRemains() =>
        Assert.Equal(
            (FootworkPhase.Disengage, 0),
            Resolve(
                priorPhase: FootworkPhase.Disengage,
                supportAllies: 1,
                supportEnemies: 2));

    /// <summary>
    /// Design section 9.2's zero-enemy rule, with no special case: zero
    /// living enemies never enters and never remains in disengagement on
    /// the ratio arithmetic alone.
    /// </summary>
    [Theory]
    [InlineData(FootworkPhase.None)]
    [InlineData(FootworkPhase.Disengage)]
    public void ZeroEnemiesNeverEntersAndNeverRemainsInDisengagement(
        FootworkPhase priorPhase) =>
        Assert.Equal(
            (FootworkPhase.None, 0),
            Resolve(
                priorPhase: priorPhase,
                supportAllies: 1,
                supportEnemies: 0));

    /// <summary>
    /// The actor counts itself, so the ratios work with no ally beyond
    /// self: one warrior facing two enemies is at ratio 2.0, past entry.
    /// </summary>
    [Fact]
    public void AnActorAloneStillEntersOnItsOwnSelfCount() =>
        Assert.Equal(
            (FootworkPhase.Disengage, 0),
            Resolve(supportAllies: 1, supportEnemies: 2));

    [Fact]
    public void ALivingAgentWithAZeroSupportAllyCountIsRejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Resolve(supportAllies: 0));

    // ----- 9.1 step 6, 9.3: unconditional posture disengagement -----

    /// <summary>
    /// Step 6 carries no ratio: every member of a <c>Withdraw</c> or
    /// <c>Yield</c> contingent disengages regardless of its own local
    /// advantage, even standing on a target inside preferred distance.
    /// </summary>
    [Theory]
    [InlineData(TacticalPosture.Withdraw)]
    [InlineData(TacticalPosture.Yield)]
    public void AWithdrawOrYieldPostureDisengagesUnconditionally(
        TacticalPosture posture) =>
        Assert.Equal(
            (FootworkPhase.Disengage, 0),
            Resolve(
                posture: posture,
                supportAllies: 9,
                supportEnemies: 1,
                hasTarget: true,
                targetAtOrInsidePreferredDistance: true));

    // ----- 9.1 step 7 -----

    [Fact]
    public void ARegroupPostureResolvesRegroupBeforeAnyTargetStep() =>
        Assert.Equal(
            (FootworkPhase.Regroup, 0),
            Resolve(
                posture: TacticalPosture.Regroup,
                hasTarget: true,
                targetAtOrInsidePreferredDistance: true));

    // ----- 9.1 steps 8 through 10 -----

    [Fact]
    public void ATargetAtOrInsidePreferredDistanceResolvesEngage() =>
        Assert.Equal(
            (FootworkPhase.Engage, 0),
            Resolve(
                hasTarget: true,
                targetAtOrInsidePreferredDistance: true));

    [Fact]
    public void ATargetBeyondPreferredDistanceResolvesApproach() =>
        Assert.Equal(
            (FootworkPhase.Approach, 0),
            Resolve(hasTarget: true));

    /// <summary>
    /// Steps 8 and 9 precede step 10: a pursuing contingent's member with a
    /// target in hand approaches it rather than taking the pursuit phase.
    /// </summary>
    [Fact]
    public void APursuePostureWithATargetInHandStillApproaches() =>
        Assert.Equal(
            (FootworkPhase.Approach, 0),
            Resolve(posture: TacticalPosture.Pursue, hasTarget: true));

    [Fact]
    public void APursuePostureWithNoTargetResolvesPursue() =>
        Assert.Equal(
            (FootworkPhase.Pursue, 0),
            Resolve(posture: TacticalPosture.Pursue));

    [Theory]
    [InlineData(TacticalPosture.None)]
    [InlineData(TacticalPosture.Hold)]
    [InlineData(TacticalPosture.Advance)]
    public void NoTargetAndNoDemandingPostureResolvesNone(
        TacticalPosture posture) =>
        Assert.Equal((FootworkPhase.None, 0), Resolve(posture: posture));

    // ----- 9.4: the finalisation matrix -----

    /// <summary>
    /// With a surviving candidate every provisional phase stands, timer
    /// included.
    /// </summary>
    [Theory]
    [InlineData(FootworkPhase.None)]
    [InlineData(FootworkPhase.Approach)]
    [InlineData(FootworkPhase.Engage)]
    [InlineData(FootworkPhase.Commit)]
    [InlineData(FootworkPhase.Recover)]
    [InlineData(FootworkPhase.Refuse)]
    [InlineData(FootworkPhase.Disengage)]
    [InlineData(FootworkPhase.Regroup)]
    [InlineData(FootworkPhase.Pursue)]
    public void ASurvivingCandidateLetsEveryProvisionalPhaseStand(
        FootworkPhase provisional) =>
        Assert.Equal(
            (provisional, 7),
            WeaponMovementRules.FinalizeFootwork(
                provisional,
                provisionalTicksRemaining: 7,
                hasSurvivingCandidate: true));

    /// <summary>
    /// With no surviving candidate the movement-seeking phases finalise
    /// <c>Refuse</c> with a zero timer, while the attack lifecycle and the
    /// safety phases retain phase and timer — a blocked lane must not erase
    /// them — and <c>None</c> stays <c>None</c>.
    /// </summary>
    [Theory]
    [InlineData(FootworkPhase.None, FootworkPhase.None, 7)]
    [InlineData(FootworkPhase.Approach, FootworkPhase.Refuse, 0)]
    [InlineData(FootworkPhase.Engage, FootworkPhase.Refuse, 0)]
    [InlineData(FootworkPhase.Commit, FootworkPhase.Commit, 7)]
    [InlineData(FootworkPhase.Recover, FootworkPhase.Recover, 7)]
    [InlineData(FootworkPhase.Refuse, FootworkPhase.Refuse, 7)]
    [InlineData(FootworkPhase.Disengage, FootworkPhase.Disengage, 7)]
    [InlineData(FootworkPhase.Regroup, FootworkPhase.Regroup, 7)]
    [InlineData(FootworkPhase.Pursue, FootworkPhase.Refuse, 0)]
    public void ABlockedLaneRefusesMovementSeekersAndRetainsTheRest(
        FootworkPhase provisional,
        FootworkPhase expectedPhase,
        int expectedTicks) =>
        Assert.Equal(
            (expectedPhase, expectedTicks),
            WeaponMovementRules.FinalizeFootwork(
                provisional,
                provisionalTicksRemaining: 7,
                hasSurvivingCandidate: false));

    // ----- Append-only numeric pins -----

    /// <summary>
    /// The numeric values are part of the deterministic replay and
    /// state-hash contract; this pin fails loudly if anyone renumbers or
    /// reorders the enum instead of appending.
    /// </summary>
    [Fact]
    public void TheFootworkPhaseNumericValuesArePinned()
    {
        Assert.Equal(0, (byte)FootworkPhase.None);
        Assert.Equal(1, (byte)FootworkPhase.Approach);
        Assert.Equal(2, (byte)FootworkPhase.Engage);
        Assert.Equal(3, (byte)FootworkPhase.Commit);
        Assert.Equal(4, (byte)FootworkPhase.Recover);
        Assert.Equal(5, (byte)FootworkPhase.Refuse);
        Assert.Equal(6, (byte)FootworkPhase.Disengage);
        Assert.Equal(7, (byte)FootworkPhase.Regroup);
        Assert.Equal(8, (byte)FootworkPhase.Pursue);
        Assert.Equal(9, Enum.GetValues<FootworkPhase>().Length);
    }
}
