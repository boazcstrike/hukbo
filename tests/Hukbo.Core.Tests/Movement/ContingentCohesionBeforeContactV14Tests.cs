using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// Coverage for <see cref="MovementPresetId.ContingentCohesionBeforeContactV14"/>
/// and the rules the contingent cohesion before contact plan changes.
/// </summary>
public sealed class ContingentCohesionBeforeContactV14Tests
{
    // ----- Task 6 (R2): the narrowed scan excludes exactly two states -----

    /// <summary>
    /// The complete set of <see cref="ContingentState"/> values that
    /// <see cref="MovementRules.ParticipatesInCrossContingentScan"/> keeps out
    /// of movement gate 6, in ascending numeric order. This array is the
    /// expectation, written out by hand rather than derived from the predicate,
    /// so a change to the predicate has something independent to fail against.
    /// R2 asks that the blanket denial in <c>BattleSimulation</c> be restricted
    /// to exactly these two states; it already is, and this array is the pin
    /// that keeps it that way.
    /// </summary>
    private static readonly ContingentState[] ExpectedExcludedStates =
    [
        ContingentState.Close,
        ContingentState.Break,
    ];

    /// <summary>
    /// The excluded set is enumerated from <see cref="ContingentState"/> itself
    /// rather than from a hand-written list of the states to try, so a value
    /// appended to the enum later cannot slip past this pin: a new state the
    /// predicate excludes lands in <c>excluded</c> and fails the comparison.
    /// </summary>
    [Fact]
    public void CrossContingentScanExcludesExactlyCloseAndBreak()
    {
        var excluded = Enum.GetValues<ContingentState>()
            .Where(state => !MovementRules.ParticipatesInCrossContingentScan(state))
            .OrderBy(state => (int)state)
            .ToArray();

        Assert.Equal(ExpectedExcludedStates, excluded);
    }

    /// <summary>
    /// The other half of the same statement: every value the enum carries that
    /// is not one of the two excluded states takes part. This is what makes
    /// <see cref="ContingentState.None"/> a participant rather than an
    /// oversight, and it too is driven off the enum rather than a fixed list.
    /// </summary>
    [Fact]
    public void EveryOtherContingentStateTakesPartInTheCrossContingentScan()
    {
        foreach (var state in Enum.GetValues<ContingentState>())
        {
            if (ExpectedExcludedStates.Contains(state))
            {
                continue;
            }

            Assert.True(
                MovementRules.ParticipatesInCrossContingentScan(state),
                $"{state} must take part in the cross-contingent scan.");
        }
    }

    /// <summary>
    /// <see cref="ContingentState.None"/> gets its own assertion because it is
    /// the value every contingent carries on the first tick, before its state
    /// has ever been resolved, and because it is the value a preset that
    /// assigns no contingent states leaves in place. Excluding it would silence
    /// gate 6 on the tick the deployment is at its most crowded.
    /// </summary>
    [Fact]
    public void ContingentStateNoneTakesPartInTheCrossContingentScan()
    {
        Assert.True(
            MovementRules.ParticipatesInCrossContingentScan(ContingentState.None));
    }

    /// <summary>
    /// The two excluded states, asserted directly rather than through the
    /// enumeration above, so a failure names which one moved.
    /// </summary>
    [Theory]
    [InlineData(ContingentState.Close)]
    [InlineData(ContingentState.Break)]
    public void CloseAndBreakTakeNoPartInTheCrossContingentScan(
        ContingentState state)
    {
        Assert.False(MovementRules.ParticipatesInCrossContingentScan(state));
    }
}
