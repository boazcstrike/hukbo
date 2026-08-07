using Sandata.Core.Navigation;
using Sandata.Core.Sensing;
using Sandata.Core.Simulation;

namespace Sandata.Core.Tests;

/// <summary>
/// Proves plan task 44's acceptance criteria for design section 5 stage 8:
/// one fixture per intent that produces that intent and no other; every
/// returned intent carries a non-default reason code distinct from every
/// other fixture's; permuting operator evaluation order produces an
/// identical intent list; one operator's inputs changing never moves
/// another operator's result; and <see cref="OperatorIntent.Dead"/> wins
/// regardless of every other input.
/// </summary>
public sealed class IntentSelectionTests
{
    // A "neutral" input satisfies none of the five conditions ahead of
    // Hold in IntentSelection.Select's cascade, so a fixture only needs to
    // flip the one field its own condition tests.
    private static IntentSelectionInput Neutral(ulong entityId) => new(
        EntityId: entityId,
        Health: 100,
        SuppressionCounter: 0,
        BestContactTier: ContactTier.Unknown,
        IsAtBreachPoint: false,
        PathReasonCode: PathReasonCode.NoDestinationRequested);

    // --- One fixture per intent, full-result equality ----------------------

    [Fact]
    public void NeutralFixture_ProducesHoldAndNoOtherIntent()
    {
        var result = IntentSelection.Select(Neutral(entityId: 1));

        Assert.Equal(
            new IntentSelectionResult(1, OperatorIntent.Hold, IntentReasonCode.HoldingPosition),
            result);
    }

    [Fact]
    public void ValidPathFixture_ProducesAdvanceAndNoOtherIntent()
    {
        var input = Neutral(entityId: 2) with { PathReasonCode = PathReasonCode.PathValid };

        var result = IntentSelection.Select(input);

        Assert.Equal(
            new IntentSelectionResult(2, OperatorIntent.Advance, IntentReasonCode.FollowingPublishedPath),
            result);
    }

    [Fact]
    public void AtBreachPointFixture_ProducesBreachAndNoOtherIntent()
    {
        // A valid path is also present here, proving breach outranks advance
        // in the cascade rather than merely being reachable in isolation.
        var input = Neutral(entityId: 3) with
        {
            IsAtBreachPoint = true,
            PathReasonCode = PathReasonCode.PathValid,
        };

        var result = IntentSelection.Select(input);

        Assert.Equal(
            new IntentSelectionResult(3, OperatorIntent.Breach, IntentReasonCode.AtBreachPoint),
            result);
    }

    [Fact]
    public void IdentifiedContactFixture_ProducesEngageAndNoOtherIntent()
    {
        // A breach point and a valid path are also present, proving engage
        // outranks both rather than merely being reachable in isolation.
        var input = Neutral(entityId: 4) with
        {
            BestContactTier = ContactTier.Identified,
            IsAtBreachPoint = true,
            PathReasonCode = PathReasonCode.PathValid,
        };

        var result = IntentSelection.Select(input);

        Assert.Equal(
            new IntentSelectionResult(4, OperatorIntent.Engage, IntentReasonCode.IdentifiedHostileContact),
            result);
    }

    [Fact]
    public void SuppressionAtThresholdFixture_ProducesRepositionAndNoOtherIntent()
    {
        // An identified contact, a breach point, and a valid path are also
        // present, proving suppression outranks all three rather than merely
        // being reachable in isolation.
        var input = Neutral(entityId: 5) with
        {
            SuppressionCounter = IntentSelection.SuppressionRepositionThreshold,
            BestContactTier = ContactTier.Identified,
            IsAtBreachPoint = true,
            PathReasonCode = PathReasonCode.PathValid,
        };

        var result = IntentSelection.Select(input);

        Assert.Equal(
            new IntentSelectionResult(5, OperatorIntent.Reposition, IntentReasonCode.RepositioningUnderSuppression),
            result);
    }

    [Fact]
    public void ZeroHealthFixture_ProducesDeadAndNoOtherIntent()
    {
        var input = Neutral(entityId: 6) with { Health = 0 };

        var result = IntentSelection.Select(input);

        Assert.Equal(
            new IntentSelectionResult(6, OperatorIntent.Dead, IntentReasonCode.OperatorIsDead),
            result);
    }

    // --- Question-mark contact must not itself trigger Engage ---------------

    [Fact]
    public void QuestionMarkContactAlone_DoesNotProduceEngage()
    {
        // Design section 4: a question-mark contact is "not shootable" —
        // proves the boundary between the two contact tiers is honoured, not
        // just that Identified works.
        var input = Neutral(entityId: 7) with { BestContactTier = ContactTier.QuestionMark };

        var result = IntentSelection.Select(input);

        Assert.Equal(OperatorIntent.Hold, result.Intent);
    }

    // --- Reason codes: non-default and distinct across the six fixtures ----

    [Fact]
    public void EveryFixturesReasonCode_IsNonDefaultAndDistinctFromEveryOther()
    {
        var results = new[]
        {
            IntentSelection.Select(Neutral(1)),
            IntentSelection.Select(Neutral(2) with { PathReasonCode = PathReasonCode.PathValid }),
            IntentSelection.Select(Neutral(3) with { IsAtBreachPoint = true }),
            IntentSelection.Select(Neutral(4) with { BestContactTier = ContactTier.Identified }),
            IntentSelection.Select(Neutral(5) with
            {
                SuppressionCounter = IntentSelection.SuppressionRepositionThreshold,
            }),
            IntentSelection.Select(Neutral(6) with { Health = 0 }),
        };

        Assert.All(results, r => Assert.NotEqual(IntentReasonCode.Unspecified, r.ReasonCode));

        var distinctReasonCodes = results.Select(r => r.ReasonCode).Distinct().Count();
        Assert.Equal(results.Length, distinctReasonCodes);
    }

    // --- Order independence -------------------------------------------------

    [Fact]
    public void PermutingOperatorEvaluationOrder_ProducesIdenticalIntentList()
    {
        IntentSelectionInput[] inputs =
        [
            Neutral(1),
            Neutral(2) with { PathReasonCode = PathReasonCode.PathValid },
            Neutral(3) with { IsAtBreachPoint = true },
            Neutral(4) with { BestContactTier = ContactTier.Identified },
            Neutral(5) with { SuppressionCounter = IntentSelection.SuppressionRepositionThreshold },
            Neutral(6) with { Health = 0 },
        ];

        // A genuine permutation, not a simple reversal and not a rerun of
        // the same order: index 3 moves to the front, index 0 moves to the
        // back, and the remaining four are shuffled around them.
        IntentSelectionInput[] permuted =
        [
            inputs[3], inputs[5], inputs[1], inputs[4], inputs[2], inputs[0],
        ];

        var originalOrderResults = IntentSelection.SelectAll(inputs)
            .OrderBy(r => r.EntityId)
            .ToArray();
        var permutedOrderResults = IntentSelection.SelectAll(permuted)
            .OrderBy(r => r.EntityId)
            .ToArray();

        Assert.Equal(originalOrderResults, permutedOrderResults);
    }

    [Fact]
    public void OneOperatorsResult_IsUnchangedWhenAnotherOperatorsInputsChange()
    {
        var operatorA = Neutral(entityId: 10) with { PathReasonCode = PathReasonCode.PathValid };
        var operatorBAsHold = Neutral(entityId: 20);
        var operatorBAsDeadWithEveryOtherFieldMaxed = Neutral(entityId: 20) with
        {
            Health = 0,
            SuppressionCounter = 99,
            BestContactTier = ContactTier.Identified,
            IsAtBreachPoint = true,
            PathReasonCode = PathReasonCode.PathValid,
        };

        var firstCall = IntentSelection.SelectAll([operatorA, operatorBAsHold]);
        var secondCall = IntentSelection.SelectAll([operatorA, operatorBAsDeadWithEveryOtherFieldMaxed]);

        var operatorAResultFirstCall = firstCall.Single(r => r.EntityId == 10);
        var operatorAResultSecondCall = secondCall.Single(r => r.EntityId == 10);

        Assert.Equal(operatorAResultFirstCall, operatorAResultSecondCall);
        Assert.Equal(OperatorIntent.Advance, operatorAResultFirstCall.Intent);

        // Confirms operator B's own result really did change between calls,
        // so this test is not vacuously true because nothing moved anywhere.
        Assert.Equal(OperatorIntent.Hold, firstCall.Single(r => r.EntityId == 20).Intent);
        Assert.Equal(OperatorIntent.Dead, secondCall.Single(r => r.EntityId == 20).Intent);
    }

    // --- Dead overrides every other input ------------------------------------

    [Theory]
    [InlineData(0, ContactTier.Unknown, false, PathReasonCode.NoDestinationRequested)]
    [InlineData(99, ContactTier.Identified, true, PathReasonCode.PathValid)]
    [InlineData(50, ContactTier.QuestionMark, true, PathReasonCode.AwaitingLatency)]
    [InlineData(3, ContactTier.Identified, false, PathReasonCode.Unreachable)]
    public void ZeroHealth_AlwaysProducesDeadRegardlessOfEveryOtherInput(
        int suppressionCounter,
        ContactTier bestContactTier,
        bool isAtBreachPoint,
        PathReasonCode pathReasonCode)
    {
        var input = new IntentSelectionInput(
            EntityId: 42,
            Health: 0,
            SuppressionCounter: suppressionCounter,
            BestContactTier: bestContactTier,
            IsAtBreachPoint: isAtBreachPoint,
            PathReasonCode: pathReasonCode);

        var result = IntentSelection.Select(input);

        Assert.Equal(
            new IntentSelectionResult(42, OperatorIntent.Dead, IntentReasonCode.OperatorIsDead),
            result);
    }

    [Fact]
    public void NegativeHealth_AlsoProducesDead()
    {
        var input = Neutral(entityId: 8) with { Health = -1 };

        var result = IntentSelection.Select(input);

        Assert.Equal(OperatorIntent.Dead, result.Intent);
    }

    // --- Pinned numeric values ------------------------------------------------

    [Fact]
    public void OperatorIntentNumericValues_MatchDesignSection5Stage8Order()
    {
        Assert.Equal(0, (int)OperatorIntent.Hold);
        Assert.Equal(1, (int)OperatorIntent.Advance);
        Assert.Equal(2, (int)OperatorIntent.Breach);
        Assert.Equal(3, (int)OperatorIntent.Engage);
        Assert.Equal(4, (int)OperatorIntent.Reposition);
        Assert.Equal(5, (int)OperatorIntent.Dead);
    }

    [Fact]
    public void IntentReasonCodeNumericValues_ArePinned()
    {
        Assert.Equal(0, (int)IntentReasonCode.Unspecified);
        Assert.Equal(1, (int)IntentReasonCode.OperatorIsDead);
        Assert.Equal(2, (int)IntentReasonCode.RepositioningUnderSuppression);
        Assert.Equal(3, (int)IntentReasonCode.IdentifiedHostileContact);
        Assert.Equal(4, (int)IntentReasonCode.AtBreachPoint);
        Assert.Equal(5, (int)IntentReasonCode.FollowingPublishedPath);
        Assert.Equal(6, (int)IntentReasonCode.HoldingPosition);
    }
}
