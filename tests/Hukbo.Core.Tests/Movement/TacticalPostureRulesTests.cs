using Hukbo.Core.Combat;
using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// The nine-branch tactical-posture table of design section 8.1, called
/// directly on <see cref="WeaponMovementRules.ResolveTacticalPosture"/>
/// with hand-built scalar inputs: first-match ordering, every boundary
/// equality, the strictly-greater role-coverage tie-break in both
/// directions, the equal-world fall-through to <c>Hold</c>, and the
/// legacy-inert defaults of the five design 14.1 fields on
/// <see cref="AgentState"/>.
/// </summary>
public sealed class TacticalPostureRulesTests
{
    // ----- 8.1 branch 1: no living member -----

    [Fact]
    public void ANoLivingMemberContingentResolvesNoneBeforeEveryOtherBranch() =>
        Assert.Equal(
            TacticalPosture.None,
            WeaponMovementRules.ResolveTacticalPosture(
                globalAllies: 40,
                globalEnemies: 10,
                contingentState: ContingentState.None,
                alliedRoleCoverage: 3,
                enemyRoleCoverage: 0));

    // ----- 8.1 branch 2: no living enemy -----

    [Fact]
    public void NoLivingEnemyResolvesPursue() =>
        Assert.Equal(
            TacticalPosture.Pursue,
            WeaponMovementRules.ResolveTacticalPosture(
                globalAllies: 3,
                globalEnemies: 0,
                contingentState: ContingentState.Advance,
                alliedRoleCoverage: 1,
                enemyRoleCoverage: 0));

    [Fact]
    public void NoLivingEnemyResolvesPursueEvenForAHoldingContingent() =>
        Assert.Equal(
            TacticalPosture.Pursue,
            WeaponMovementRules.ResolveTacticalPosture(
                globalAllies: 3,
                globalEnemies: 0,
                contingentState: ContingentState.Hold,
                alliedRoleCoverage: 1,
                enemyRoleCoverage: 0));

    // ----- 8.1 branch 3: exact double outnumbering is already Withdraw -----

    [Theory]
    [InlineData(10, 20)]
    [InlineData(10, 21)]
    [InlineData(1, 2)]
    public void OutnumberingAtOrBeyondTwoToOneResolvesWithdraw(
        int allies,
        int enemies) =>
        Assert.Equal(
            TacticalPosture.Withdraw,
            WeaponMovementRules.ResolveTacticalPosture(
                allies,
                enemies,
                ContingentState.Advance,
                alliedRoleCoverage: 3,
                enemyRoleCoverage: 0));

    // ----- 8.1 branch 4: exact four-to-three pressure is already Yield -----

    [Theory]
    [InlineData(30, 40)]
    [InlineData(3, 4)]
    [InlineData(55, 100)]
    public void PressureAtOrBeyondFourToThreeButShortOfDoubleResolvesYield(
        int allies,
        int enemies) =>
        Assert.Equal(
            TacticalPosture.Yield,
            WeaponMovementRules.ResolveTacticalPosture(
                allies,
                enemies,
                ContingentState.Advance,
                alliedRoleCoverage: 3,
                enemyRoleCoverage: 0));

    /// <summary>
    /// The widened cross-products keep boundary equality exact at counts
    /// whose products overflow <see langword="int"/>.
    /// </summary>
    [Fact]
    public void TheCrossProductsSurviveCountsWhoseProductsOverflowInt() =>
        Assert.Equal(
            TacticalPosture.Withdraw,
            WeaponMovementRules.ResolveTacticalPosture(
                globalAllies: 1_000_000_000,
                globalEnemies: 2_000_000_000,
                contingentState: ContingentState.Advance,
                alliedRoleCoverage: 3,
                enemyRoleCoverage: 0));

    // ----- 8.1 branch 5: ContingentState.Hold -----

    [Fact]
    public void AHoldingContingentResolvesRegroup() =>
        Assert.Equal(
            TacticalPosture.Regroup,
            WeaponMovementRules.ResolveTacticalPosture(
                globalAllies: 10,
                globalEnemies: 10,
                contingentState: ContingentState.Hold,
                alliedRoleCoverage: 2,
                enemyRoleCoverage: 2));

    /// <summary>
    /// Branch 5 precedes branch 6: a holding contingent gathers even when
    /// its faction holds a five-to-four advantage that would otherwise
    /// resolve <c>Advance</c>.
    /// </summary>
    [Fact]
    public void AHoldingContingentRegroupsEvenWithAFiveToFourAdvantage() =>
        Assert.Equal(
            TacticalPosture.Regroup,
            WeaponMovementRules.ResolveTacticalPosture(
                globalAllies: 25,
                globalEnemies: 20,
                contingentState: ContingentState.Hold,
                alliedRoleCoverage: 3,
                enemyRoleCoverage: 0));

    /// <summary>
    /// Branch 4 precedes branch 5: exact four-to-three pressure yields even
    /// for a holding contingent.
    /// </summary>
    [Fact]
    public void FourToThreePressureYieldsEvenForAHoldingContingent() =>
        Assert.Equal(
            TacticalPosture.Yield,
            WeaponMovementRules.ResolveTacticalPosture(
                globalAllies: 30,
                globalEnemies: 40,
                contingentState: ContingentState.Hold,
                alliedRoleCoverage: 3,
                enemyRoleCoverage: 0));

    // ----- 8.1 branch 6: exact five-to-four advantage is already Advance --

    [Theory]
    [InlineData(25, 20)]
    [InlineData(5, 4)]
    [InlineData(26, 20)]
    public void AdvantageAtOrBeyondFiveToFourResolvesAdvance(
        int allies,
        int enemies) =>
        Assert.Equal(
            TacticalPosture.Advance,
            WeaponMovementRules.ResolveTacticalPosture(
                allies,
                enemies,
                ContingentState.Advance,
                alliedRoleCoverage: 0,
                enemyRoleCoverage: 3));

    // ----- 8.1 branches 7 and 8: the role-coverage tie-break -----

    [Theory]
    [InlineData(10, 10)]
    [InlineData(11, 10)]
    public void EqualOrBetterNumbersWithStrictlyGreaterCoverageAdvance(
        int allies,
        int enemies) =>
        Assert.Equal(
            TacticalPosture.Advance,
            WeaponMovementRules.ResolveTacticalPosture(
                allies,
                enemies,
                ContingentState.Close,
                alliedRoleCoverage: 3,
                enemyRoleCoverage: 2));

    [Theory]
    [InlineData(10, 10)]
    [InlineData(10, 11)]
    public void EqualOrWorseNumbersWithStrictlyLessCoverageYield(
        int allies,
        int enemies) =>
        Assert.Equal(
            TacticalPosture.Yield,
            WeaponMovementRules.ResolveTacticalPosture(
                allies,
                enemies,
                ContingentState.Close,
                alliedRoleCoverage: 1,
                enemyRoleCoverage: 2));

    // ----- 8.1 branch 9: the contested fall-through -----

    /// <summary>
    /// Equal headcounts and equal coverage must fall through branches 7 and
    /// 8 to <c>Hold</c> (design section 8.1's own closing requirement).
    /// </summary>
    [Fact]
    public void EqualHeadcountsAndEqualCoverageFallThroughToHold() =>
        Assert.Equal(
            TacticalPosture.Hold,
            WeaponMovementRules.ResolveTacticalPosture(
                globalAllies: 10,
                globalEnemies: 10,
                contingentState: ContingentState.Close,
                alliedRoleCoverage: 2,
                enemyRoleCoverage: 2));

    /// <summary>
    /// Coverage only breaks a tie in the direction the headcount admits: a
    /// side slightly ahead on numbers but behind on coverage matches
    /// neither branch 7 nor branch 8 and holds.
    /// </summary>
    [Theory]
    [InlineData(11, 10, 1, 2)]
    [InlineData(10, 11, 2, 1)]
    [InlineData(11, 10, 2, 2)]
    [InlineData(10, 11, 2, 2)]
    public void ACoverageEdgeTheHeadcountDoesNotAdmitHolds(
        int allies,
        int enemies,
        int alliedCoverage,
        int enemyCoverage) =>
        Assert.Equal(
            TacticalPosture.Hold,
            WeaponMovementRules.ResolveTacticalPosture(
                allies,
                enemies,
                ContingentState.Close,
                alliedCoverage,
                enemyCoverage));

    // ----- Guards -----

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(10, -1)]
    public void NegativeLivingTotalsAreRejected(int allies, int enemies) =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => WeaponMovementRules.ResolveTacticalPosture(
                allies,
                enemies,
                ContingentState.Advance,
                alliedRoleCoverage: 1,
                enemyRoleCoverage: 1));

    // ----- 14.1: the five AgentState fields default legacy-inert -----

    /// <summary>
    /// A freshly constructed agent carries the exact legacy values of
    /// design section 14.1 — <c>Facing16.None</c> despite that enum's
    /// numeric default being <c>East</c>, and <c>None</c>/<c>0</c> for the
    /// other four — so every V1-through-V5 battle leaves all five fields
    /// byte-identical to before this task.
    /// </summary>
    [Fact]
    public void AFreshAgentCarriesTheLegacyInertMovementDefaults()
    {
        var agent = new AgentState(
            entityId: 1,
            factionId: 0,
            xRaw: 0,
            yRaw: 0,
            maximumHitPoints: 100,
            movementSpeedRaw: 1,
            perceptionRangeRaw: 1,
            attackRangeRaw: 1,
            damagePerAttack: 1,
            attackCooldownTicks: 1,
            loadout: new CombatLoadout(
                WeaponId.Kampilan,
                ArmorId.LightOrganic,
                ShieldId.None));

        Assert.Equal(Facing16.None, agent.Facing);
        Assert.Equal(0, agent.MovementPaceRaw);
        Assert.Equal(TacticalPosture.None, agent.TacticalPosture);
        Assert.Equal(FootworkPhase.None, agent.FootworkPhase);
        Assert.Equal(0, agent.FootworkTicksRemaining);
    }

    // ----- Append-only numeric pins -----

    /// <summary>
    /// The numeric values are part of the deterministic replay and
    /// state-hash contract; this pin fails loudly if anyone renumbers or
    /// reorders the enum instead of appending.
    /// </summary>
    [Fact]
    public void TheTacticalPostureNumericValuesArePinned()
    {
        Assert.Equal(0, (byte)TacticalPosture.None);
        Assert.Equal(1, (byte)TacticalPosture.Advance);
        Assert.Equal(2, (byte)TacticalPosture.Hold);
        Assert.Equal(3, (byte)TacticalPosture.Yield);
        Assert.Equal(4, (byte)TacticalPosture.Regroup);
        Assert.Equal(5, (byte)TacticalPosture.Pursue);
        Assert.Equal(6, (byte)TacticalPosture.Withdraw);
        Assert.Equal(7, Enum.GetValues<TacticalPosture>().Length);
    }
}
