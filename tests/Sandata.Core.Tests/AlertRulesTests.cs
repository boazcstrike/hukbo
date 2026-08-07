using System.Reflection;
using Sandata.Core.Sensing;

namespace Sandata.Core.Tests;

/// <summary>
/// Proves design section 5's 2026-08-07 amendment and plan task 68's done-when
/// clause against <see cref="AlertRules"/>: each trigger raises exactly one
/// level and no more, a trigger for a level already reached changes nothing,
/// the level never decreases across a long synthetic tick sequence including
/// a quiet stretch, permuting the order of the operators evaluated changes
/// nothing, and a level committed this tick is not visible to a reader of the
/// frozen view. Mirrors <c>HearingRulesTests</c> and <c>ContactMemoryTests</c>'
/// exact-threshold and ordering-proof style.
/// </summary>
public sealed class AlertRulesTests
{
    // --- "Each trigger raises exactly one level and no more" ---------------

    [Fact]
    public void IdentifiedContactAlone_FromCalm_RaisesToRaised_NotBreach()
    {
        var result = AlertRules.Evaluate(
            AlertLevel.Calm,
            hasIdentifiedContact: true,
            hasTriggeringSound: false,
            hasFriendlyDeath: false,
            hasWallBreach: false);

        Assert.Equal(AlertLevel.Raised, result);
    }

    [Fact]
    public void TriggeringSoundAlone_FromCalm_RaisesToRaised_NotBreach()
    {
        var result = AlertRules.Evaluate(
            AlertLevel.Calm,
            hasIdentifiedContact: false,
            hasTriggeringSound: true,
            hasFriendlyDeath: false,
            hasWallBreach: false);

        Assert.Equal(AlertLevel.Raised, result);
    }

    [Fact]
    public void IdentifiedContactAndTriggeringSoundTogether_FromCalm_StillOnlyReachesRaised()
    {
        // Two low-severity triggers firing in the same tick must not stack
        // past their shared ceiling: "no more" than Raised.
        var result = AlertRules.Evaluate(
            AlertLevel.Calm,
            hasIdentifiedContact: true,
            hasTriggeringSound: true,
            hasFriendlyDeath: false,
            hasWallBreach: false);

        Assert.Equal(AlertLevel.Raised, result);
    }

    [Fact]
    public void FriendlyDeathAlone_FromRaised_RaisesToBreach()
    {
        var result = AlertRules.Evaluate(
            AlertLevel.Raised,
            hasIdentifiedContact: false,
            hasTriggeringSound: false,
            hasFriendlyDeath: true,
            hasWallBreach: false);

        Assert.Equal(AlertLevel.Breach, result);
    }

    [Fact]
    public void WallBreachAlone_FromRaised_RaisesToBreach()
    {
        var result = AlertRules.Evaluate(
            AlertLevel.Raised,
            hasIdentifiedContact: false,
            hasTriggeringSound: false,
            hasFriendlyDeath: false,
            hasWallBreach: true);

        Assert.Equal(AlertLevel.Breach, result);
    }

    [Fact]
    public void FriendlyDeathAlone_FromCalm_ReachesBreachDirectly_NotStuckAtRaised()
    {
        // A friendly death is a Breach-tier event on its own terms (design
        // section 4: "open engagement... an unambiguous sign of combat"),
        // even when no low-severity trigger fired first this mission. See
        // AlertRules' own remarks for why the ceiling model, not a strict
        // "only from Raised" reading, is the decision this task recorded.
        var result = AlertRules.Evaluate(
            AlertLevel.Calm,
            hasIdentifiedContact: false,
            hasTriggeringSound: false,
            hasFriendlyDeath: true,
            hasWallBreach: false);

        Assert.Equal(AlertLevel.Breach, result);
    }

    [Fact]
    public void EveryTriggerAtOnce_FromCalm_ReachesBreach_AndNoHigherValueExists()
    {
        var result = AlertRules.Evaluate(
            AlertLevel.Calm,
            hasIdentifiedContact: true,
            hasTriggeringSound: true,
            hasFriendlyDeath: true,
            hasWallBreach: true);

        Assert.Equal(AlertLevel.Breach, result);
    }

    [Fact]
    public void NoTrigger_LevelStaysExactlyWhereItWas()
    {
        Assert.Equal(
            AlertLevel.Calm,
            AlertRules.Evaluate(AlertLevel.Calm, false, false, false, false));
        Assert.Equal(
            AlertLevel.Raised,
            AlertRules.Evaluate(AlertLevel.Raised, false, false, false, false));
        Assert.Equal(
            AlertLevel.Breach,
            AlertRules.Evaluate(AlertLevel.Breach, false, false, false, false));
    }

    // --- "A trigger for a level already reached changes nothing" ----------

    [Theory]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    public void LowSeverityTrigger_WhenAlreadyRaised_ChangesNothing(
        bool hasIdentifiedContact, bool hasTriggeringSound, bool hasFriendlyDeath, bool hasWallBreach)
    {
        var result = AlertRules.Evaluate(
            AlertLevel.Raised, hasIdentifiedContact, hasTriggeringSound, hasFriendlyDeath, hasWallBreach);

        Assert.Equal(AlertLevel.Raised, result);
    }

    [Theory]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, false, true)]
    public void AnyTrigger_WhenAlreadyAtBreach_ChangesNothing(
        bool hasIdentifiedContact, bool hasTriggeringSound, bool hasFriendlyDeath, bool hasWallBreach)
    {
        var result = AlertRules.Evaluate(
            AlertLevel.Breach, hasIdentifiedContact, hasTriggeringSound, hasFriendlyDeath, hasWallBreach);

        Assert.Equal(AlertLevel.Breach, result);
    }

    // --- "The level never decreases... including a quiet stretch" ---------

    [Fact]
    public void LongSyntheticSequence_NeverDecreases_AtEveryStep()
    {
        // Deterministic PRNG is fine here: this is test-only code, not
        // Sandata.Core, and SandataSourceHygieneTests scans only src/Sandata.Core.
        var random = new Random(Seed: 20260807);
        var level = AlertLevel.Calm;

        for (var tick = 0; tick < 5000; tick++)
        {
            // A long quiet stretch in the middle of the sequence: every
            // trigger forced false for a run of ticks, proving "never
            // decreases" is not merely true because the level kept climbing.
            var inQuietStretch = tick is >= 2000 and < 3000;

            // Tick 500 deterministically forces a friendly death, so the
            // sequence is guaranteed — not merely overwhelmingly likely — to
            // reach AlertLevel.Breach well before the quiet stretch begins.
            // Every other tick's triggers stay randomised for variety; the
            // property under test (monotonicity) is checked at every single
            // step regardless of which triggers fired.
            var forcedFriendlyDeath = tick == 500;

            var hasIdentifiedContact = !inQuietStretch && random.Next(20) == 0;
            var hasTriggeringSound = !inQuietStretch && random.Next(20) == 0;
            var hasFriendlyDeath = forcedFriendlyDeath || (!inQuietStretch && random.Next(200) == 0);
            var hasWallBreach = !inQuietStretch && random.Next(200) == 0;

            var previous = level;
            level = AlertRules.Evaluate(
                previous, hasIdentifiedContact, hasTriggeringSound, hasFriendlyDeath, hasWallBreach);

            Assert.True(
                level >= previous,
                $"tick {tick}: level regressed from {previous} to {level}.");
        }

        Assert.Equal(AlertLevel.Breach, level);
    }

    [Fact]
    public void QuietStretchAfterBreach_HoldsExactlyAtBreach_NeverDrifts()
    {
        var level = AlertLevel.Breach;

        for (var tick = 0; tick < 1000; tick++)
        {
            level = AlertRules.Evaluate(level, false, false, false, false);
            Assert.Equal(AlertLevel.Breach, level);
        }
    }

    // --- "Permuting the order of the operators evaluated changes nothing" -

    [Theory]
    [MemberData(nameof(OperatorObservationPermutations))]
    public void EvaluateFaction_PermutingOperatorOrder_ProducesTheSameResult(
        AlertTriggerObservation[] observations)
    {
        var result = AlertRules.EvaluateFaction(AlertLevel.Calm, observations);

        Assert.Equal(AlertLevel.Breach, result);
    }

    public static TheoryData<AlertTriggerObservation[]> OperatorObservationPermutations()
    {
        // Three operators: one contributes an identified contact, one
        // contributes a triggering sound, one contributes a friendly death —
        // together forcing Breach regardless of which operator the fold
        // visits first.
        var contactOperator = new AlertTriggerObservation(true, false, false, false);
        var soundOperator = new AlertTriggerObservation(false, true, false, false);
        var deathOperator = new AlertTriggerObservation(false, false, true, false);

        var data = new TheoryData<AlertTriggerObservation[]>();
        data.Add(new[] { contactOperator, soundOperator, deathOperator });
        data.Add(new[] { contactOperator, deathOperator, soundOperator });
        data.Add(new[] { soundOperator, contactOperator, deathOperator });
        data.Add(new[] { soundOperator, deathOperator, contactOperator });
        data.Add(new[] { deathOperator, contactOperator, soundOperator });
        data.Add(new[] { deathOperator, soundOperator, contactOperator });

        return data;
    }

    [Fact]
    public void EvaluateFaction_EmptyRoster_LeavesLevelUnchanged()
    {
        var result = AlertRules.EvaluateFaction(
            AlertLevel.Raised, ReadOnlySpan<AlertTriggerObservation>.Empty);

        Assert.Equal(AlertLevel.Raised, result);
    }

    [Fact]
    public void EvaluateFaction_MatchesEvaluate_WhenFoldedByHand()
    {
        var observations = new AlertTriggerObservation[]
        {
            new(HasIdentifiedContact: false, HasTriggeringSound: false, HasFriendlyDeath: false, HasWallBreach: false),
            new(HasIdentifiedContact: true, HasTriggeringSound: false, HasFriendlyDeath: false, HasWallBreach: false),
        };

        var viaFold = AlertRules.EvaluateFaction(AlertLevel.Calm, observations);
        var viaDirectCall = AlertRules.Evaluate(
            AlertLevel.Calm,
            hasIdentifiedContact: true,
            hasTriggeringSound: false,
            hasFriendlyDeath: false,
            hasWallBreach: false);

        Assert.Equal(viaDirectCall, viaFold);
    }

    // --- "A level committed this tick is not visible to a reader of the
    //      frozen view" -----------------------------------------------------

    [Fact]
    public void AlertRules_HoldsNoMutableStaticState()
    {
        // The frozen-view seam (design section 5: evaluated against the
        // tick-start view during sensing, committed only after that view is
        // released) is modelled by AlertRules taking "previous level" as an
        // explicit parameter rather than tracking a "current" level itself.
        // TickStartView does not exist in this worktree (it is task 49's,
        // a later wave) and must not be invented here, so the seam property
        // this asserts structurally is: nothing in AlertRules could let a
        // level computed by one call leak into another call as an implicit
        // input, because the type carries no static field a computation
        // could read from or write to in the first place. Every static
        // field found must be either a compile-time constant or an
        // immutable, side-effect-free reference (an enum or method group is
        // not possible for a static class to expose without a field, so in
        // practice this type has none at all).
        var offendingFields = typeof(AlertRules)
            .GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(field => !field.IsLiteral && !field.IsInitOnly)
            .Select(field => field.Name)
            .ToArray();

        Assert.Empty(offendingFields);
    }

    [Fact]
    public void SamePreviousLevelInput_UsedByTwoIndependentCalls_NeitherCallSeesTheOthersResult()
    {
        // Simulates two operators of the same faction being evaluated within
        // the same tick's sensing stage, both reading the one frozen-view
        // "previous level" value stage 8 will also read — not the level
        // either evaluation is in the middle of computing. If a computed
        // result could leak back in as an implicit previous-level input, the
        // second call below would see AlertLevel.Breach (the first call's
        // result) rather than the frozen AlertLevel.Calm every caller in this
        // tick actually holds, and would report a spurious "no more" result
        // (Breach is already Breach's own ceiling, indistinguishable from a
        // correct call) — so the assertion is written against the low-ceiling
        // second call, whose correct result (Raised) is only reachable if the
        // frozen Calm value, not the first call's Breach, was actually used.
        var frozenPreviousLevel = AlertLevel.Calm;

        var firstOperatorResult = AlertRules.Evaluate(
            frozenPreviousLevel,
            hasIdentifiedContact: false,
            hasTriggeringSound: false,
            hasFriendlyDeath: true,
            hasWallBreach: false);
        Assert.Equal(AlertLevel.Breach, firstOperatorResult);

        var secondOperatorResult = AlertRules.Evaluate(
            frozenPreviousLevel,
            hasIdentifiedContact: true,
            hasTriggeringSound: false,
            hasFriendlyDeath: false,
            hasWallBreach: false);

        Assert.Equal(AlertLevel.Raised, secondOperatorResult);
    }

    // --- Max --------------------------------------------------------------

    [Theory]
    [InlineData(AlertLevel.Calm, AlertLevel.Calm, AlertLevel.Calm)]
    [InlineData(AlertLevel.Calm, AlertLevel.Raised, AlertLevel.Raised)]
    [InlineData(AlertLevel.Raised, AlertLevel.Calm, AlertLevel.Raised)]
    [InlineData(AlertLevel.Raised, AlertLevel.Breach, AlertLevel.Breach)]
    [InlineData(AlertLevel.Breach, AlertLevel.Raised, AlertLevel.Breach)]
    [InlineData(AlertLevel.Breach, AlertLevel.Breach, AlertLevel.Breach)]
    public void Max_ReturnsTheHigherLevel(AlertLevel left, AlertLevel right, AlertLevel expected)
    {
        Assert.Equal(expected, AlertRules.Max(left, right));
    }

    // --- CarriesIntent / IsTriggeringSound ----------------------------------

    [Theory]
    [InlineData(SoundKind.Gunfire, true)]
    [InlineData(SoundKind.BreakingGlass, true)]
    [InlineData(SoundKind.DeathScream, true)]
    [InlineData(SoundKind.BoltCutter, false)]
    [InlineData(SoundKind.Smoke, false)]
    [InlineData(SoundKind.HammerOrCrowbar, false)]
    [InlineData(SoundKind.BreacherShotgun, false)]
    public void CarriesIntent_MatchesTheAmendmentsNamedThreeSoundKinds(SoundKind kind, bool expected)
    {
        Assert.Equal(expected, AlertRules.CarriesIntent(kind));
    }

    [Fact]
    public void IsTriggeringSound_WithinRadiusAndCarriesIntent_IsTrue()
    {
        Assert.True(AlertRules.IsTriggeringSound(SoundKind.Gunfire, dx: HearingRules.GunfireRadiusWu, dy: 0));
    }

    [Fact]
    public void IsTriggeringSound_BeyondRadius_IsFalseEvenThoughItCarriesIntent()
    {
        Assert.False(AlertRules.IsTriggeringSound(SoundKind.Gunfire, dx: HearingRules.GunfireRadiusWu + 1, dy: 0));
    }

    [Fact]
    public void IsTriggeringSound_WithinRadiusButDoesNotCarryIntent_IsFalse()
    {
        // A bolt cutter heard at point-blank range is still not one of the
        // amendment's three named sound kinds, so it never raises the level
        // through this path.
        Assert.False(AlertRules.IsTriggeringSound(SoundKind.BoltCutter, dx: 0, dy: 0));
    }
}
