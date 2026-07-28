using System.Collections.Immutable;
using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests;

public sealed class ScenarioTests
{
    [Fact]
    public void CreateDefaultBuildsValidatedHundredVersusHundredScenario()
    {
        var scenario = Scenario.CreateDefault(seed: 42, totalAgents: 200);

        scenario.Validate();

        Assert.Equal(42UL, scenario.Seed);
        Assert.Equal(100, scenario.AgentsPerFaction);
        Assert.Equal(200, scenario.TotalAgents);
        Assert.Equal(20, scenario.TickRate);
        Assert.Equal(10_000, scenario.TickLimit);
        Assert.Equal(
            CombatPresetId.PrecolonialPhilippinesV2,
            scenario.CombatPreset);
    }

    [Fact]
    public void CreateDefaultUsesTheApprovedSolidCollisionConfiguration()
    {
        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: 200);

        scenario.Validate();

        Assert.Equal(CollisionRules.DefaultBodyRadiusRaw, scenario.BodyRadiusRaw);
        Assert.Equal(4 * FixedPoint.Scale, scenario.BodyRadiusRaw);
        Assert.Equal(CollisionPolicy.Solid, scenario.CollisionPolicy);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void ValidateRejectsNonpositiveBodyRadius(int bodyRadiusRaw)
    {
        var scenario = Scenario.CreateDefault() with
        {
            BodyRadiusRaw = bodyRadiusRaw,
        };

        Assert.Throws<ArgumentOutOfRangeException>(scenario.Validate);
    }

    [Fact]
    public void ValidateRejectsBodyRadiusAboveTheRawWorldMaximum()
    {
        var scenario = Scenario.CreateDefault() with
        {
            BodyRadiusRaw = checked(
                (Scenario.MaximumMapDimension * FixedPoint.Scale) + 1),
        };

        Assert.Throws<ArgumentOutOfRangeException>(scenario.Validate);
    }

    [Fact]
    public void ValidateRejectsAnyCollisionPolicyOtherThanSolid()
    {
        var scenario = Scenario.CreateDefault() with
        {
            CollisionPolicy = (CollisionPolicy)1,
        };

        Assert.Throws<ArgumentOutOfRangeException>(scenario.Validate);
    }

    [Fact]
    public void ValidateAcceptsBodiesThatExactlyFillTheAttackRange()
    {
        var scenario = Scenario.CreateDefault() with
        {
            AttackRangeRaw = 8 * FixedPoint.Scale,
            BodyRadiusRaw = 4 * FixedPoint.Scale,
        };

        scenario.Validate();

        Assert.Equal(
            scenario.AttackRangeRaw,
            checked(2 * scenario.BodyRadiusRaw));
    }

    [Fact]
    public void ValidateRejectsBodiesWiderThanTheAttackRange()
    {
        var scenario = Scenario.CreateDefault() with
        {
            AttackRangeRaw = 8 * FixedPoint.Scale,
            BodyRadiusRaw = (4 * FixedPoint.Scale) + 1,
        };

        Assert.Throws<ArgumentOutOfRangeException>(scenario.Validate);
    }

    [Fact]
    public void ValidateAcceptsMovementSpeedEqualToTheBodyRadius()
    {
        var scenario = Scenario.CreateDefault() with
        {
            MovementSpeedRaw = CollisionRules.DefaultBodyRadiusRaw,
        };

        scenario.Validate();

        Assert.Equal(scenario.BodyRadiusRaw, scenario.MovementSpeedRaw);
    }

    [Fact]
    public void ValidateRejectsMovementSpeedAboveTheBodyRadius()
    {
        var scenario = Scenario.CreateDefault() with
        {
            MovementSpeedRaw = CollisionRules.DefaultBodyRadiusRaw + 1,
        };

        Assert.Throws<ArgumentOutOfRangeException>(scenario.Validate);
    }

    [Fact]
    public void ValidateAcceptsAMapExactlyOneBodyWideOrOneBodyTall()
    {
        var exactlyOneBodyWide = CreateSingleBodyMapScenario(
            mapWidth: 2,
            mapHeight: 4);
        var exactlyOneBodyTall = CreateSingleBodyMapScenario(
            mapWidth: 4,
            mapHeight: 2);

        exactlyOneBodyWide.Validate();
        exactlyOneBodyTall.Validate();
    }

    [Fact]
    public void ValidateRejectsAMapNarrowerOrShorterThanOneBody()
    {
        var tooNarrow = CreateSingleBodyMapScenario(mapWidth: 1, mapHeight: 4);
        var tooShort = CreateSingleBodyMapScenario(mapWidth: 4, mapHeight: 1);

        Assert.Throws<ArgumentOutOfRangeException>(tooNarrow.Validate);
        Assert.Throws<ArgumentOutOfRangeException>(tooShort.Validate);
    }

    [Fact]
    public void ValidateAcceptsAPopulationThatExactlySquarePacksTheMap()
    {
        var scenario = CreateSquarePackedScenario(agentsPerFaction: 8);

        scenario.Validate();

        Assert.Equal(16, scenario.TotalAgents);
    }

    [Fact]
    public void ValidateRejectsAPopulationDenserThanSquarePacking()
    {
        var scenario = CreateSquarePackedScenario(agentsPerFaction: 9);

        Assert.Throws<ArgumentOutOfRangeException>(scenario.Validate);
    }

    [Fact]
    public void ValidateAcceptsTheLargestSupportedMapWithTheDefaultBody()
    {
        var scenario = Scenario.CreateDefault(totalAgents: 2) with
        {
            MapWidth = Scenario.MaximumMapDimension,
            MapHeight = Scenario.MaximumMapDimension,
        };

        scenario.Validate();
    }

    [Fact]
    public void ValidateRejectsUnregisteredCombatPreset()
    {
        var scenario = Scenario.CreateDefault() with
        {
            CombatPreset = (CombatPresetId)999,
        };

        Assert.Throws<ArgumentOutOfRangeException>(scenario.Validate);
    }

    [Fact]
    public void ValidateAcceptsIndependentPursuitV1MovementPreset()
    {
        var scenario = Scenario.CreateDefault() with
        {
            MovementPreset = MovementPresetId.IndependentPursuitV1,
        };

        scenario.Validate();
    }

    [Fact]
    public void ValidateRejectsUnregisteredMovementPreset()
    {
        var scenario = Scenario.CreateDefault() with
        {
            MovementPreset = (MovementPresetId)99,
        };

        Assert.Throws<ArgumentOutOfRangeException>(scenario.Validate);
    }

    [Fact]
    public void CreateDefaultSelectsPersistentContingentsV3MovementPreset()
    {
        // T6 of docs/plans/2026-07-28-contingent-close-latch.md flips the
        // shipped default from PersistentContingentsV2 to
        // PersistentContingentsV3. PersistentContingentsV2 stays registered
        // and unmodified for a replay that names it explicitly; only the
        // value a caller gets without naming a preset has moved.
        var scenario = Scenario.CreateDefault();

        Assert.Equal(
            MovementPresetId.PersistentContingentsV3,
            scenario.MovementPreset);
    }

    [Fact]
    public void ScenariosDifferingOnlyInMovementPresetAreNotEqual()
    {
        var first = Scenario.CreateDefault() with
        {
            MovementPreset = MovementPresetId.IndependentPursuitV1,
        };
        var second = first with { MovementPreset = (MovementPresetId)99 };

        Assert.NotEqual(first, second);
        Assert.NotEqual(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void CreateDefaultRejectsInvalidTotalAgentCount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Scenario.CreateDefault(totalAgents: 0));
        Assert.Throws<ArgumentException>(
            () => Scenario.CreateDefault(totalAgents: 201));
    }

    [Theory]
    [InlineData(0, 720, 20, 10_000)]
    [InlineData(1280, 0, 20, 10_000)]
    [InlineData(1280, 720, 0, 10_000)]
    [InlineData(1280, 720, 20, 0)]
    public void ValidateRejectsInvalidMapOrTickBounds(
        int mapWidth,
        int mapHeight,
        int tickRate,
        int tickLimit)
    {
        var scenario = Scenario.CreateDefault() with
        {
            MapWidth = mapWidth,
            MapHeight = mapHeight,
            TickRate = tickRate,
            TickLimit = tickLimit,
        };

        Assert.Throws<ArgumentOutOfRangeException>(scenario.Validate);
    }

    [Fact]
    public void ValidateRejectsNonpositiveFactionCount()
    {
        var scenario = Scenario.CreateDefault() with { AgentsPerFaction = 0 };

        Assert.Throws<ArgumentOutOfRangeException>(scenario.Validate);
    }

    [Theory]
    [InlineData(nameof(Scenario.MaximumHitPoints))]
    [InlineData(nameof(Scenario.DamagePerAttack))]
    [InlineData(nameof(Scenario.AttackRangeRaw))]
    [InlineData(nameof(Scenario.PerceptionRangeRaw))]
    [InlineData(nameof(Scenario.MovementSpeedRaw))]
    [InlineData(nameof(Scenario.AttackCooldownTicks))]
    public void ValidateRejectsNonpositiveCombatValues(string propertyName)
    {
        var original = Scenario.CreateDefault();
        var scenario = propertyName switch
        {
            nameof(Scenario.MaximumHitPoints) => original with { MaximumHitPoints = 0 },
            nameof(Scenario.DamagePerAttack) => original with { DamagePerAttack = 0 },
            nameof(Scenario.AttackRangeRaw) => original with { AttackRangeRaw = 0 },
            nameof(Scenario.PerceptionRangeRaw) => original with { PerceptionRangeRaw = 0 },
            nameof(Scenario.MovementSpeedRaw) => original with { MovementSpeedRaw = 0 },
            nameof(Scenario.AttackCooldownTicks) => original with { AttackCooldownTicks = 0 },
            _ => throw new InvalidOperationException($"Unknown property {propertyName}."),
        };

        Assert.Throws<ArgumentOutOfRangeException>(scenario.Validate);
    }

    [Fact]
    public void ValidateRejectsValuesThatRiskFixedPointArithmeticOverflow()
    {
        var oversizedMap = Scenario.CreateDefault() with
        {
            MapWidth = Scenario.MaximumMapDimension + 1,
        };
        var excessivePopulation = Scenario.CreateDefault() with
        {
            AgentsPerFaction = (int.MaxValue / 2) + 1,
        };
        var excessiveRange = Scenario.CreateDefault() with
        {
            AttackRangeRaw = checked((Scenario.MaximumMapDimension * FixedPoint.Scale) + 1),
        };
        var excessiveAccumulatedDamage = Scenario.CreateDefault() with
        {
            AgentsPerFaction = Scenario.MaximumAgentsPerFaction,
            DamagePerAttack = 1_000_000,
        };

        Assert.Throws<ArgumentOutOfRangeException>(oversizedMap.Validate);
        Assert.Throws<ArgumentOutOfRangeException>(excessivePopulation.Validate);
        Assert.Throws<ArgumentOutOfRangeException>(excessiveRange.Validate);
        Assert.Throws<ArgumentOutOfRangeException>(
            excessiveAccumulatedDamage.Validate);
    }

    [Fact]
    public void MinimumMapCreatesAgentsInsideValidatedBounds()
    {
        var scenario = Scenario.CreateDefault(totalAgents: 2) with
        {
            MapWidth = 1,
            MapHeight = 1,
            AttackRangeRaw = 2,
            PerceptionRangeRaw = FixedPoint.Scale,
            MovementSpeedRaw = 1,
            BodyRadiusRaw = 1,
        };

        var simulation = BattleSimulation.Create(scenario);

        Assert.All(
            simulation.Agents,
            agent =>
            {
                Assert.InRange(agent.XRaw, 0, FixedPoint.Scale);
                Assert.InRange(agent.YRaw, 0, FixedPoint.Scale);
            });
    }

    [Fact]
    public void MaximumMapCreatesAgentsInsideValidatedBounds()
    {
        var scenario = Scenario.CreateDefault(totalAgents: 2) with
        {
            MapWidth = Scenario.MaximumMapDimension,
            MapHeight = Scenario.MaximumMapDimension,
        };
        var maximumRawCoordinate = checked(
            Scenario.MaximumMapDimension * FixedPoint.Scale);

        var simulation = BattleSimulation.Create(scenario);

        Assert.All(
            simulation.Agents,
            agent =>
            {
                Assert.InRange(agent.XRaw, 0, maximumRawCoordinate);
                Assert.InRange(agent.YRaw, 0, maximumRawCoordinate);
            });
    }

    [Fact]
    public void DefaultRosterCountsAreEmptyAndSkipValidation()
    {
        var scenario = Scenario.CreateDefault();

        Assert.True(scenario.RosterCounts.IsDefaultOrEmpty);
        scenario.Validate();
    }

    [Fact]
    public void ValidateRejectsRosterCountsLengthMismatch()
    {
        var scenario = Scenario.CreateDefault(totalAgents: 200) with
        {
            RosterCounts = ImmutableArray.Create(25, 25, 50),
        };

        Assert.Throws<ArgumentException>(scenario.Validate);
    }

    [Fact]
    public void ValidateRejectsRosterCountsElementOutOfRange()
    {
        var scenario = Scenario.CreateDefault(totalAgents: 200) with
        {
            RosterCounts = ImmutableArray.Create(-1, 26, 25, 50, 0, 0),
        };

        Assert.Throws<ArgumentOutOfRangeException>(scenario.Validate);
    }

    [Fact]
    public void ValidateRejectsRosterCountsSumThatIsNotAgentsPerFaction()
    {
        var scenario = Scenario.CreateDefault(totalAgents: 200) with
        {
            RosterCounts = ImmutableArray.Create(25, 25, 25, 24, 0, 0),
        };

        Assert.Throws<ArgumentException>(scenario.Validate);
    }

    [Fact]
    public void ValidateAcceptsAnExplicitlyEmptyRosterCountArray()
    {
        var scenario = Scenario.CreateDefault(totalAgents: 200) with
        {
            RosterCounts = ImmutableArray<int>.Empty,
        };

        scenario.Validate();
    }

    [Fact]
    public void EqualityComparesRosterCountsElementwiseRatherThanByReference()
    {
        var first = Scenario.CreateDefault(totalAgents: 200) with
        {
            RosterCounts = ImmutableArray.Create(25, 25, 25, 25, 0, 0),
        };
        var second = Scenario.CreateDefault(totalAgents: 200) with
        {
            RosterCounts = ImmutableArray.Create(25, 25, 25, 25, 0, 0),
        };

        Assert.Equal(first, second);
        Assert.True(first.Equals(second));
    }

    [Fact]
    public void EqualScenariosProduceEqualHashCodes()
    {
        var first = Scenario.CreateDefault(totalAgents: 200) with
        {
            RosterCounts = ImmutableArray.Create(10, 20, 30, 40, 0, 0),
        };
        var second = Scenario.CreateDefault(totalAgents: 200) with
        {
            RosterCounts = ImmutableArray.Create(10, 20, 30, 40, 0, 0),
        };

        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void ValidateAcceptsAZeroLastStandThresholdAsDisabled()
    {
        var scenario = Scenario.CreateDefault() with
        {
            LastStandThresholdAgents = 0,
        };

        scenario.Validate();
    }

    [Fact]
    public void ValidateRejectsALastStandThresholdAboveTheApprovedMaximum()
    {
        var scenario = Scenario.CreateDefault() with
        {
            LastStandThresholdAgents =
                FormationRules.MaximumLastStandThresholdAgents + 1,
        };

        Assert.Throws<ArgumentOutOfRangeException>(scenario.Validate);
    }

    [Fact]
    public void ValidateRejectsANegativeLastStandThreshold()
    {
        var scenario = Scenario.CreateDefault() with
        {
            LastStandThresholdAgents = -1,
        };

        Assert.Throws<ArgumentOutOfRangeException>(scenario.Validate);
    }

    [Fact]
    public void ValidateRejectsABodyRadiusWhoseJitterSpanOverflowsWhenTheLastStandIsEnabled()
    {
        var oversizedBody = Scenario.CreateDefault(totalAgents: 2) with
        {
            BodyRadiusRaw = 268_435_456,
            AttackRangeRaw = 536_870_912,
            PerceptionRangeRaw = 536_870_912,
            MovementSpeedRaw = FixedPoint.Scale,
            MapWidth = Scenario.MaximumMapDimension,
            MapHeight = Scenario.MaximumMapDimension,
            LastStandThresholdAgents = 0,
        };

        oversizedBody.Validate();

        var withLastStandEnabled = oversizedBody with
        {
            LastStandThresholdAgents = 6,
        };

        Assert.Throws<ArgumentOutOfRangeException>(withLastStandEnabled.Validate);
    }

    [Fact]
    public void ValidateRejectsABodyRadiusWhoseContingentTrailOverflowsAndAcceptsTheDefault()
    {
        // The default scenario's small body radius leaves both the
        // contingent jitter and the contingent trail comfortably inside
        // Int32, exactly as it does for the last-stand jitter guard above.
        Scenario.CreateDefault().Validate();

        // With AgentsPerFaction = 1 (totalAgents: 2), the worst-case living
        // count is 1, so the jitter multiplier
        // (IntegerSquareRoot(4 * 1) + 1) is the smallest possible value, 3.
        // At BodyRadiusRaw = 360,000,000 the jitter (1,080,000,000) still
        // fits an Int32, but the trail
        // (((3 * 1,080,000,000 + 1) / 2) + (3 * 360,000,000) =
        // 2,700,000,000) does not. This isolates the trail guard: the
        // jitter guard alone would not catch this body radius.
        var oversizedBody = Scenario.CreateDefault(totalAgents: 2) with
        {
            BodyRadiusRaw = 360_000_000,
            AttackRangeRaw = 720_000_000,
            PerceptionRangeRaw = 720_000_000,
            MovementSpeedRaw = FixedPoint.Scale,
            MapWidth = Scenario.MaximumMapDimension,
            MapHeight = Scenario.MaximumMapDimension,
            LastStandThresholdAgents = 0,
        };

        Assert.Throws<ArgumentOutOfRangeException>(oversizedBody.Validate);
    }

    [Fact]
    public void CreateDefaultEnablesTheLastStandAtTheApprovedThreshold()
    {
        var scenario = Scenario.CreateDefault();

        Assert.Equal(
            FormationRules.DefaultLastStandThresholdAgents,
            scenario.LastStandThresholdAgents);
    }

    [Fact]
    public void ScenariosDifferingOnlyInLastStandThresholdAreNotEqual()
    {
        var first = Scenario.CreateDefault() with { LastStandThresholdAgents = 0 };
        var second = Scenario.CreateDefault() with { LastStandThresholdAgents = 6 };

        Assert.NotEqual(first, second);
        Assert.NotEqual(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void ScenariosDifferingOnlyInBodyRadiusAreNotEqual()
    {
        var first = Scenario.CreateDefault() with
        {
            BodyRadiusRaw = 2 * FixedPoint.Scale,
        };
        var second = Scenario.CreateDefault() with
        {
            BodyRadiusRaw = 3 * FixedPoint.Scale,
        };

        Assert.NotEqual(first, second);
        Assert.NotEqual(first.GetHashCode(), second.GetHashCode());
    }

    private static Scenario CreateSingleBodyMapScenario(
        int mapWidth,
        int mapHeight) =>
        Scenario.CreateDefault(totalAgents: 2) with
        {
            MapWidth = mapWidth,
            MapHeight = mapHeight,
            BodyRadiusRaw = FixedPoint.Scale,
            MovementSpeedRaw = FixedPoint.Scale,
        };

    private static Scenario CreateSquarePackedScenario(int agentsPerFaction) =>
        Scenario.CreateDefault(totalAgents: 2) with
        {
            AgentsPerFaction = agentsPerFaction,
            MapWidth = 8,
            MapHeight = 8,
            BodyRadiusRaw = FixedPoint.Scale,
            MovementSpeedRaw = FixedPoint.Scale,
        };
}
