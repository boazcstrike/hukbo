using Hukbo.Core.Movement;

namespace Hukbo.Core.Tests;

/// <summary>
/// T10 unit-level sweep of <see cref="MovementRules.ComputeArrivalStepRaw"/>,
/// the arrival-slowdown taper of design section 3.6
/// (the formation and movement realism design).
/// This is the test the T10 plan entry names as owing its own assertions:
/// the frozen-preset reproduction the canonical gate also runs proves
/// nothing about this arithmetic, because the taper never executes under
/// <see cref="MovementPresetId.IndependentPursuitV1"/>. Every fact here calls
/// the pure static directly with hand-built scalars; none of them constructs
/// a <see cref="Simulation.BattleSimulation"/>.
/// </summary>
public sealed class ArrivalTaperTests
{
    /// <summary>
    /// Design section 3.6's fixed multiplier, mirrored here rather than read
    /// from <see cref="MovementRuleset"/> so this test exercises
    /// <see cref="MovementRules.ComputeArrivalStepRaw"/> as the pure function
    /// it is, independent of the ruleset's own wiring.
    /// </summary>
    private const int ArrivalTaperMultiplier = 4;

    /// <summary>
    /// One <c>(bodyRadiusRaw, movementSpeedRaw)</c> pair per case, each
    /// satisfying the <c>MovementSpeedRaw &lt;= BodyRadiusRaw</c> scenario
    /// invariant (<c>src/Hukbo.Core/Simulation/Scenario.cs:326-343</c>) the
    /// taper is designed never to violate: a minimum pairing at the
    /// one-raw-unit floor, a small body, a mid-size body matching the
    /// frozen-fixture scale, and a large body.
    /// </summary>
    public static TheoryData<int, int> BodyRadiiAndMovementSpeeds => new()
    {
        { 1, 1 },
        { 10, 1 },
        { 40, 4 },
        { 100, 10 },
        { 250, 25 },
    };

    /// <summary>
    /// Sweeps every raw remaining distance from one unit through three full
    /// taper bands, asserting on every sampled point that
    /// <see cref="MovementRules.ComputeArrivalStepRaw"/> never returns zero,
    /// never exceeds <c>movementSpeedRaw</c>, and never exceeds the untapered
    /// step <c>Min(movementSpeedRaw, remainingRaw)</c> would have produced —
    /// the taper can only ever shrink a step, never grow or invert one.
    /// </summary>
    [Theory]
    [MemberData(nameof(BodyRadiiAndMovementSpeeds))]
    public void EverySampledStepIsAtLeastOneAtMostSpeedAndNeverExceedsTheUntaperedStep(
        int bodyRadiusRaw,
        int movementSpeedRaw)
    {
        var taperRaw = (long)ArrivalTaperMultiplier * bodyRadiusRaw;
        var sweepCeilingRaw = taperRaw * 3;

        for (var remainingRaw = 1L; remainingRaw <= sweepCeilingRaw; remainingRaw++)
        {
            var stepRaw = MovementRules.ComputeArrivalStepRaw(
                remainingRaw, movementSpeedRaw, taperRaw);
            var untaperedStepRaw = Math.Min((long)movementSpeedRaw, remainingRaw);

            Assert.True(
                stepRaw >= 1,
                $"bodyRadiusRaw={bodyRadiusRaw} movementSpeedRaw={movementSpeedRaw} " +
                $"remainingRaw={remainingRaw}: step {stepRaw} fell below the movement floor.");
            Assert.True(
                stepRaw <= movementSpeedRaw,
                $"bodyRadiusRaw={bodyRadiusRaw} movementSpeedRaw={movementSpeedRaw} " +
                $"remainingRaw={remainingRaw}: step {stepRaw} exceeded movementSpeedRaw.");
            Assert.True(
                stepRaw <= untaperedStepRaw,
                $"bodyRadiusRaw={bodyRadiusRaw} movementSpeedRaw={movementSpeedRaw} " +
                $"remainingRaw={remainingRaw}: step {stepRaw} exceeded the untapered step " +
                $"{untaperedStepRaw}.");
        }
    }

    /// <summary>
    /// Proves the taper is confined to the final approach: for every
    /// <c>remainingRaw &gt;= taperRaw</c> across the same three-band sweep,
    /// the result is exactly the untapered step, with no shrinkage at all.
    /// </summary>
    [Theory]
    [MemberData(nameof(BodyRadiiAndMovementSpeeds))]
    public void TheStepIsExactlyUntaperedAtAndBeyondTheTaperBand(
        int bodyRadiusRaw,
        int movementSpeedRaw)
    {
        var taperRaw = (long)ArrivalTaperMultiplier * bodyRadiusRaw;
        var sweepCeilingRaw = taperRaw * 3;

        for (var remainingRaw = taperRaw; remainingRaw <= sweepCeilingRaw; remainingRaw++)
        {
            var stepRaw = MovementRules.ComputeArrivalStepRaw(
                remainingRaw, movementSpeedRaw, taperRaw);
            var untaperedStepRaw = Math.Min((long)movementSpeedRaw, remainingRaw);

            Assert.Equal(untaperedStepRaw, stepRaw);
        }
    }
}
