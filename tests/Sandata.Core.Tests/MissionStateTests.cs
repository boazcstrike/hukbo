using System.Collections.Immutable;
using System.Reflection;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Sandata.Core.Determinism;
using Sandata.Core.Mathematics;
using Sandata.Core.Rules;
using Sandata.Core.Simulation;
using Sandata.Core.Weapons;

namespace Sandata.Core.Tests;

/// <summary>
/// Task 17 of Sandata's scaffold plan: <see cref="Mission"/>
/// validation, the <see cref="MissionState"/>/<see cref="MissionSnapshot"/>
/// round trip, <see cref="SandataStateHasher"/> stability, and a reflection
/// check that design section 4's derived list never reaches
/// <see cref="MissionSnapshot"/>.
/// </summary>
public sealed class MissionStateTests
{
    private static Mission BuildSampleMission(ulong seed = 12_345UL) => new(
        formatVersion: Mission.CurrentFormatVersion,
        seed: seed,
        mapContentHash: 999UL,
        tickPolicy: new MissionTickPolicy(TickLimit: 10_000, StateHashCadenceTicks: 50),
        factionSetups: ImmutableArray.Create(
            new MissionFactionSetup(FactionId: 1, OperatorCount: 4),
            new MissionFactionSetup(FactionId: 0, OperatorCount: 4)),
        rulesetId: SandataPresetId.ModernTacticalV1);

    private static OperatorState BuildSampleOperator(int entityId) => new(
        EntityId: (ulong)entityId,
        PositionX: FixedPoint.FromWhole(entityId),
        PositionY: FixedPoint.FromWhole(entityId * 2),
        Facing: Facing16.East,
        AimAngle: Bam16.FromFacing16(Facing16.East),
        Health: 100,
        Faction: entityId % 2,
        Intent: 1,
        IsCrouched: false,
        WeaponLowered: false,
        WeaponChainPhase: 0,
        WeaponChainRemainingTicks: 5,
        MagazineRounds: 30,
        CyclicFireAccumulator: 0,
        SuppressionCounter: 0)
    {
        ContactMemory = ImmutableArray.Create(new ContactMemoryEntry(99UL, 5, 1, 10)),
    };

    private static MissionState BuildSampleState() => new(
        Tick: 42,
        Phase: 1,
        Winner: -1,
        NextEntityId: 8,
        NextEventSequence: 3)
    {
        Operators = ImmutableArray.Create(BuildSampleOperator(1), BuildSampleOperator(2)),
        FactionAlerts = ImmutableArray.Create(
            new FactionAlertState(0, 0),
            new FactionAlertState(1, 1)),
        Doors = ImmutableArray.Create(
            new DoorState(1, true, 10),
            new DoorState(2, false, 20)),
        Groups = ImmutableArray.Create(
            new GroupPathState(1, 100, true, 50, 200, 30)),
        RngStreams = ImmutableArray.Create(
            new RngStreamState(1, 1, 111UL, 222UL)),
    };

    // -- Mission validation: one named exception per invalid field. --------

    [Fact]
    public void Constructor_WrongFormatVersion_ThrowsNamed()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new Mission(
            formatVersion: Mission.CurrentFormatVersion + 1,
            seed: 1,
            mapContentHash: 1,
            tickPolicy: new MissionTickPolicy(100, 10),
            factionSetups: ImmutableArray.Create(
                new MissionFactionSetup(0, 1),
                new MissionFactionSetup(1, 1)),
            rulesetId: SandataPresetId.ModernTacticalV1));

        Assert.Equal("formatVersion", exception.ParamName);
    }

    [Fact]
    public void Constructor_NonPositiveTickLimit_ThrowsNamed()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new Mission(
            formatVersion: Mission.CurrentFormatVersion,
            seed: 1,
            mapContentHash: 1,
            tickPolicy: new MissionTickPolicy(TickLimit: 0, StateHashCadenceTicks: 10),
            factionSetups: ImmutableArray.Create(
                new MissionFactionSetup(0, 1),
                new MissionFactionSetup(1, 1)),
            rulesetId: SandataPresetId.ModernTacticalV1));

        Assert.Equal("TickLimit", exception.ParamName);
    }

    [Fact]
    public void Constructor_NonPositiveStateHashCadence_ThrowsNamed()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new Mission(
            formatVersion: Mission.CurrentFormatVersion,
            seed: 1,
            mapContentHash: 1,
            tickPolicy: new MissionTickPolicy(TickLimit: 100, StateHashCadenceTicks: 0),
            factionSetups: ImmutableArray.Create(
                new MissionFactionSetup(0, 1),
                new MissionFactionSetup(1, 1)),
            rulesetId: SandataPresetId.ModernTacticalV1));

        Assert.Equal("StateHashCadenceTicks", exception.ParamName);
    }

    [Fact]
    public void Constructor_CadenceExceedsTickLimit_ThrowsNamed()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new Mission(
            formatVersion: Mission.CurrentFormatVersion,
            seed: 1,
            mapContentHash: 1,
            tickPolicy: new MissionTickPolicy(TickLimit: 10, StateHashCadenceTicks: 50),
            factionSetups: ImmutableArray.Create(
                new MissionFactionSetup(0, 1),
                new MissionFactionSetup(1, 1)),
            rulesetId: SandataPresetId.ModernTacticalV1));

        Assert.Equal("StateHashCadenceTicks", exception.ParamName);
    }

    [Fact]
    public void Constructor_WrongFactionSetupCount_ThrowsNamed()
    {
        var exception = Assert.Throws<ArgumentException>(() => new Mission(
            formatVersion: Mission.CurrentFormatVersion,
            seed: 1,
            mapContentHash: 1,
            tickPolicy: new MissionTickPolicy(100, 10),
            factionSetups: ImmutableArray.Create(new MissionFactionSetup(0, 1)),
            rulesetId: SandataPresetId.ModernTacticalV1));

        Assert.Equal("factionSetups", exception.ParamName);
    }

    [Fact]
    public void Constructor_InvalidFactionId_ThrowsNamed()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new Mission(
            formatVersion: Mission.CurrentFormatVersion,
            seed: 1,
            mapContentHash: 1,
            tickPolicy: new MissionTickPolicy(100, 10),
            factionSetups: ImmutableArray.Create(
                new MissionFactionSetup(2, 1),
                new MissionFactionSetup(1, 1)),
            rulesetId: SandataPresetId.ModernTacticalV1));

        Assert.Equal("FactionId", exception.ParamName);
    }

    [Fact]
    public void Constructor_NonPositiveOperatorCount_ThrowsNamed()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new Mission(
            formatVersion: Mission.CurrentFormatVersion,
            seed: 1,
            mapContentHash: 1,
            tickPolicy: new MissionTickPolicy(100, 10),
            factionSetups: ImmutableArray.Create(
                new MissionFactionSetup(0, 0),
                new MissionFactionSetup(1, 1)),
            rulesetId: SandataPresetId.ModernTacticalV1));

        Assert.Equal("OperatorCount", exception.ParamName);
    }

    [Fact]
    public void Constructor_FactionSetupsDoNotCoverBothFactions_ThrowsNamed()
    {
        var exception = Assert.Throws<ArgumentException>(() => new Mission(
            formatVersion: Mission.CurrentFormatVersion,
            seed: 1,
            mapContentHash: 1,
            tickPolicy: new MissionTickPolicy(100, 10),
            factionSetups: ImmutableArray.Create(
                new MissionFactionSetup(0, 1),
                new MissionFactionSetup(0, 1)),
            rulesetId: SandataPresetId.ModernTacticalV1));

        Assert.Equal("factionSetups", exception.ParamName);
    }

    [Fact]
    public void Constructor_UndefinedRulesetId_ThrowsNamed()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new Mission(
            formatVersion: Mission.CurrentFormatVersion,
            seed: 1,
            mapContentHash: 1,
            tickPolicy: new MissionTickPolicy(100, 10),
            factionSetups: ImmutableArray.Create(
                new MissionFactionSetup(0, 1),
                new MissionFactionSetup(1, 1)),
            rulesetId: (SandataPresetId)9999));

        Assert.Equal("rulesetId", exception.ParamName);
    }

    [Fact]
    public void Constructor_ReordersFactionSetupsAscendingByFactionId()
    {
        var mission = BuildSampleMission();

        Assert.Equal(0, mission.FactionSetups[0].FactionId);
        Assert.Equal(1, mission.FactionSetups[1].FactionId);
    }

    // -- Snapshot round trip. ------------------------------------------------

    [Fact]
    public void Snapshot_RoundTrips_ToAnEqualState()
    {
        var original = BuildSampleState();

        var restored = original.ToSnapshot().ToState();

        Assert.Equal(original, restored);
    }

    [Fact]
    public void State_Equality_IsStructural_NotReferential()
    {
        // BuildSampleState calls ImmutableArray.Create independently for
        // each side, so every backing array below is a distinct allocation.
        // The equality assertion that follows can only pass because
        // MissionState.Equals compares element by element, never because
        // the two states happen to share storage.
        var left = BuildSampleState();
        var right = BuildSampleState();

        Assert.NotSame(left.Operators[0], right.Operators[0]);
        Assert.Equal(left, right);
    }

    [Fact]
    public void State_Equality_DetectsADifferenceInsideNestedContactMemory()
    {
        var left = BuildSampleState();
        var right = BuildSampleState() with
        {
            Operators = ImmutableArray.Create(
                BuildSampleOperator(1) with
                {
                    ContactMemory = ImmutableArray.Create(
                        new ContactMemoryEntry(99UL, 5, 1, 999)),
                },
                BuildSampleOperator(2)),
        };

        Assert.NotEqual(left, right);
    }

    // -- State hash stability. -----------------------------------------------

    [Fact]
    public void StateHash_IsStable_AcrossTwoIndependentConstructions()
    {
        var mission = BuildSampleMission();
        var ruleset = SandataRuleset.ModernTacticalV1;

        var first = SandataStateHasher.Compute(mission, BuildSampleState(), ruleset);
        var second = SandataStateHasher.Compute(mission, BuildSampleState(), ruleset);

        Assert.Equal(first, second);
    }

    [Fact]
    public void StateHash_Moves_WhenTickChanges()
    {
        var mission = BuildSampleMission();
        var ruleset = SandataRuleset.ModernTacticalV1;

        var baseline = SandataStateHasher.Compute(mission, BuildSampleState(), ruleset);
        var changed = SandataStateHasher.Compute(
            mission,
            BuildSampleState() with { Tick = 43 },
            ruleset);

        Assert.NotEqual(baseline, changed);
    }

    [Fact]
    public void StateHash_Moves_WhenAnOperatorFieldChanges()
    {
        var mission = BuildSampleMission();
        var ruleset = SandataRuleset.ModernTacticalV1;
        var state = BuildSampleState();
        var changedState = state with
        {
            Operators = ImmutableArray.Create(
                BuildSampleOperator(1) with { Health = 50 },
                BuildSampleOperator(2)),
        };

        var baseline = SandataStateHasher.Compute(mission, state, ruleset);
        var changed = SandataStateHasher.Compute(mission, changedState, ruleset);

        Assert.NotEqual(baseline, changed);
    }

    [Fact]
    public void StateHash_Moves_WhenAnOperatorContactMemoryChanges()
    {
        var mission = BuildSampleMission();
        var ruleset = SandataRuleset.ModernTacticalV1;
        var state = BuildSampleState();
        var changedState = state with
        {
            Operators = ImmutableArray.Create(
                BuildSampleOperator(1) with
                {
                    ContactMemory = ImmutableArray<ContactMemoryEntry>.Empty,
                },
                BuildSampleOperator(2)),
        };

        var baseline = SandataStateHasher.Compute(mission, state, ruleset);
        var changed = SandataStateHasher.Compute(mission, changedState, ruleset);

        Assert.NotEqual(baseline, changed);
    }

    [Fact]
    public void StateHash_Moves_WhenTheMissionChanges()
    {
        var state = BuildSampleState();
        var ruleset = SandataRuleset.ModernTacticalV1;

        var baseline = SandataStateHasher.Compute(BuildSampleMission(seed: 1), state, ruleset);
        var changed = SandataStateHasher.Compute(BuildSampleMission(seed: 2), state, ruleset);

        Assert.NotEqual(baseline, changed);
    }

    [Fact]
    public void StateHash_Moves_WhenTheRulesetChanges()
    {
        var mission = BuildSampleMission();
        var state = BuildSampleState();
        var changedRuleset = new SandataRuleset(
            SandataRuleset.ModernTacticalV1.TickRate + 1,
            SandataRuleset.ModernTacticalV1.MsToTickConversionRuleId,
            SandataRuleset.ModernTacticalV1.PathLatencyTicks,
            SandataRuleset.ModernTacticalV1.GroupCohesionRadiusWu,
            SandataRuleset.ModernTacticalV1.LoweredWallDistanceWu,
            SandataRuleset.ModernTacticalV1.AimToleranceBam);

        var baseline = SandataStateHasher.Compute(mission, state, SandataRuleset.ModernTacticalV1);
        var changed = SandataStateHasher.Compute(mission, state, changedRuleset);

        Assert.NotEqual(baseline, changed);
    }

    // -- Design section 4's derived list must never reach the snapshot. -----

    /// <summary>
    /// One entry per fragment drawn from design section 4's "What is derived
    /// and never hashed, never snapshotted" bullets, so this is a data-driven
    /// check over the design's own list rather than a handful of spot
    /// assertions.
    /// </summary>
    public static IEnumerable<object[]> DerivedTermsFromDesignSection4()
    {
        string[] terms =
        [
            "NavGrid", // "The nav grid, including wall rasterisation and body-radius inflation."
            "Rasterisation",
            "BodyRadiusInflation",
            "ClearanceField", // "The clearance field."
            "WallBucket", // "The wall bucket index and the cell-to-wall-segment lists."
            "CellToWallSegment",
            "OpenSet", // "A* scratch: the open set, the closed set, gScore, came-from, and the visited stamps."
            "ClosedSet",
            "GScore",
            "CameFrom",
            "VisitedStamp",
            "Polyline", // "Published path polylines and their cumulative arclengths."
            "Arclength",
            "LineOfSight", // "Line-of-sight results and vision-cone membership for the current tick."
            "VisionCone",
            "UniformGrid", // "The collision uniform grid and its pair list."
            "CollisionGrid",
            "CollisionPair",
            "RenderSnapshot", // "The read-only render snapshot, every render metric, and every audio cue."
            "RenderMetric",
            "AudioCue",
        ];

        foreach (var term in terms)
        {
            yield return new object[] { term };
        }
    }

    [Theory]
    [MemberData(nameof(DerivedTermsFromDesignSection4))]
    public void MissionSnapshot_NeverExposesADerivedTerm(string derivedTerm)
    {
        var propertyNames = typeof(MissionSnapshot)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name);

        Assert.All(
            propertyNames,
            name => Assert.DoesNotContain(
                derivedTerm,
                name,
                StringComparison.OrdinalIgnoreCase));
    }

    // -- Task 64's 2026-08-07 correction: the slot index is derived, never stored. --

    /// <summary>
    /// Task 64 of Sandata's scaffold plan removes
    /// <c>OperatorState.SquadSlotIndex</c>: design section 8 states plainly
    /// that group id, leader, membership, and slot index are all derived
    /// each tick, and design section 4 is corrected in place to agree. This
    /// asserts by reflection, rather than by absence of a compile error
    /// alone, that no member named <c>SquadSlotIndex</c> — public or
    /// non-public, property, field, or constructor parameter — survives
    /// anywhere on <see cref="OperatorState"/>.
    /// </summary>
    [Fact]
    public void OperatorState_HasNoSquadSlotIndexMember()
    {
        var type = typeof(OperatorState);

        var memberNames = type
            .GetMembers(
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static)
            .Select(member => member.Name);

        Assert.All(
            memberNames,
            name => Assert.DoesNotContain(
                "SquadSlotIndex",
                name,
                StringComparison.OrdinalIgnoreCase));
    }

    // -- Task 64's 2026-08-07 correction: every entity and group id is ulong. --

    /// <summary>
    /// Task 64 widens every entity and group identifier on the mission
    /// record from <c>int</c> to <see langword="ulong"/>, matching
    /// <c>Hukbo.Core.Simulation.AgentState.EntityId</c> and Sandata's own
    /// <c>SandataCollisionBody</c>/<c>SandataCollisionPair</c>/
    /// <c>SandataCollisionMoveRequest</c>. This pins the four fields that
    /// name an entity or a group — <see cref="OperatorState.EntityId"/>,
    /// <see cref="ContactMemoryEntry.EnemyEntityId"/>,
    /// <see cref="GroupPathState.GroupId"/>, and
    /// <see cref="MissionState.NextEntityId"/> — at <see langword="ulong"/>
    /// by reflection, so a future edit that narrows one back to
    /// <see langword="int"/> fails a test rather than silently reopening the
    /// defect task 64 fixed.
    /// </summary>
    [Theory]
    [InlineData(typeof(OperatorState), nameof(OperatorState.EntityId))]
    [InlineData(typeof(ContactMemoryEntry), nameof(ContactMemoryEntry.EnemyEntityId))]
    [InlineData(typeof(GroupPathState), nameof(GroupPathState.GroupId))]
    [InlineData(typeof(MissionState), nameof(MissionState.NextEntityId))]
    public void EntityAndGroupIdentifiers_AreUlong(Type declaringType, string propertyName)
    {
        var property = declaringType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(property);
        Assert.Equal(typeof(ulong), property!.PropertyType);
    }

    /// <summary>
    /// The fields task 64 deliberately left <see langword="int"/> because
    /// they are not entity or group identifiers: a two-valued faction
    /// selector, a nav-grid cell index, an implementer-added door ordinal,
    /// and the RNG-stream/algorithm selectors. Widening any of these would
    /// be an unreviewed scope expansion, so this pins the negative case too.
    /// </summary>
    [Theory]
    [InlineData(typeof(FactionAlertState), nameof(FactionAlertState.FactionId))]
    [InlineData(typeof(DoorState), nameof(DoorState.DoorId))]
    [InlineData(typeof(GroupPathState), nameof(GroupPathState.DestinationCellIndex))]
    [InlineData(typeof(ContactMemoryEntry), nameof(ContactMemoryEntry.LastKnownCellIndex))]
    [InlineData(typeof(RngStreamState), nameof(RngStreamState.StreamId))]
    [InlineData(typeof(RngStreamState), nameof(RngStreamState.AlgorithmId))]
    [InlineData(typeof(OperatorState), nameof(OperatorState.Faction))]
    public void NonIdentifierFields_StayInt(Type declaringType, string propertyName)
    {
        var property = declaringType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(property);
        Assert.Equal(typeof(int), property!.PropertyType);
    }

    // -- Task 79c (Sandata's scaffold plan, the wave-12 audit's corrected --
    // -- obligation): OperatorState.Firearm folds last -----------------
    // -- inside FoldOperator, after the contact-memory block. -------------

    /// <summary>
    /// Captured by running <see cref="SandataStateHasher.Compute"/> against
    /// <see cref="BuildSampleMission"/>/<see cref="BuildSampleState"/> as
    /// this file's own fixtures stand today, under the hasher exactly as
    /// task 79c leaves it (operator field fold order plus the new
    /// <c>Firearm</c> fold appended last in <c>FoldOperator</c>). Design
    /// section 4's own audit correction for this task states plainly that
    /// no literal can survive this change unmoved — <c>SandataHash.Fold</c>
    /// is FNV-1a, and folding one additional value changes the digest
    /// unconditionally, including when that value equals the old hardcoded
    /// default. This is therefore a fresh baseline, not the pre-task-79c
    /// value carried forward.
    /// </summary>
    /// <remarks>
    /// Task 85 (Sandata's scaffold plan, wave-12 audit's
    /// "Task 52's golden baseline against task 85's single-pin rule"): this
    /// is now the <b>only</b> absolute state-hash literal in a <c>.cs</c>
    /// file under <c>tests/Sandata.Core.Tests/</c> — the deliberate canary
    /// that fires when any state fold changes.
    /// <see cref="OrderStateHashTests"/>' former <c>PreTask61BaselineHash</c>
    /// and <see cref="MissionEventFeedTests"/>' former
    /// <c>PreTask76BaselineHash</c> pinned this same value for the same
    /// fixture to guard properties that are actually relational, and both
    /// are now comparisons between two hashes computed live instead. Task
    /// 52's golden-replay baselines are a different kind of pin — a seed, a
    /// build, and an ordered order stream reproducing a run's outcome, not a
    /// constructed fixture's digest — and they live in
    /// <c>tests/Sandata.Core.Tests/Fixtures/seed-1-baseline.json</c>, read by
    /// <c>GoldenReplayTests</c>, not declared as a C# constant.
    /// </remarks>
    /// <remarks>
    /// <b>Re-examined on 2026-08-14 and deliberately left where it is.</b> The
    /// lowered-weapon and automatic-fire design's section 6 predicted that
    /// seeding the path-blocked span from the baked map (its decision D1)
    /// would move every Sandata digest, this one included. It does not move
    /// this one, and the reason is worth stating rather than discovering
    /// again: this literal is the digest of a <i>constructed fixture</i> —
    /// <see cref="BuildSampleMission"/> and <see cref="BuildSampleState"/> —
    /// which no simulation ever ticks. D1 changes which cells a path search
    /// may cross. A hash taken over authoritative state that was never
    /// advanced cannot see that, so the value stands unchanged, and it stands
    /// as the same canary it has been since task 85 rather than as a value
    /// carried forward for convenience.
    /// </remarks>
    private const ulong PreTask79cBaselineHash = 3_159_438_799_659_597_482UL;

    [Fact]
    public void StateHash_OfSampleState_MatchesThePreTask79cBaseline()
    {
        var mission = BuildSampleMission();
        var ruleset = SandataRuleset.ModernTacticalV1;
        var state = BuildSampleState();

        var hash = SandataStateHasher.Compute(mission, state, ruleset);

        Assert.Equal(PreTask79cBaselineHash, hash);
    }

    /// <summary>
    /// The assertion that is actually decisive about
    /// <see cref="OperatorState.Firearm"/> being folded at all: two mission
    /// states differing in exactly one operator's loadout, and nothing
    /// else, must hash differently. Unlike the baseline literal above (which
    /// only proves *some* value changed), this proves the specific field is
    /// live in the fold rather than dead code the compiler kept around.
    /// </summary>
    [Fact]
    public void StateHash_Moves_WhenOnlyOneOperatorsFirearmDiffers()
    {
        var mission = BuildSampleMission();
        var ruleset = SandataRuleset.ModernTacticalV1;
        var state = BuildSampleState();
        var changedState = state with
        {
            Operators = ImmutableArray.Create(
                BuildSampleOperator(1) with { Firearm = FirearmId.Beretta92Fs },
                BuildSampleOperator(2)),
        };

        var baseline = SandataStateHasher.Compute(mission, state, ruleset);
        var changed = SandataStateHasher.Compute(mission, changedState, ruleset);

        Assert.NotEqual(baseline, changed);
    }

    /// <summary>
    /// <see cref="OperatorState.Firearm"/> defaults to
    /// <see cref="FirearmId.Ak47"/> — the value the private
    /// <c>SandataSimulation.DefaultFirearmId</c> named before task 79d-2a
    /// deleted it — so a state built without setting the field explicitly is
    /// unaffected by the per-operator loadout addition.
    /// </summary>
    [Fact]
    public void OperatorState_Firearm_DefaultsToAk47()
    {
        var op = BuildSampleOperator(1);

        Assert.Equal(FirearmId.Ak47, op.Firearm);
    }
}
