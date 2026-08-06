using System.Text.Json;
using Hukbo.Core.Combat;
using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;
using Hukbo.Headless;

namespace Hukbo.Core.Tests;

public sealed class HeadlessRunnerTests
{
    [Fact]
    public void EmptyArgumentsUseDocumentedDefaults()
    {
        var success = HeadlessRunner.TryParseArguments(
            [],
            out var options,
            out var error);

        Assert.True(success, error);
        Assert.Equal(200, options.AgentCount);
        Assert.Equal(10_000, options.TickCount);
        Assert.Equal(1UL, options.Seed);
        Assert.Null(options.OutputPath);
    }

    [Fact]
    public void SupportedArgumentsAreParsed()
    {
        var success = HeadlessRunner.TryParseArguments(
            [
                "--agents",
                "500",
                "--ticks",
                "2500",
                "--seed",
                "18446744073709551615",
                "--output",
                "report.json",
            ],
            out var options,
            out var error);

        Assert.True(success, error);
        Assert.Equal(500, options.AgentCount);
        Assert.Equal(2_500, options.TickCount);
        Assert.Equal(ulong.MaxValue, options.Seed);
        Assert.Equal("report.json", options.OutputPath);
    }

    [Theory]
    [InlineData("--agents", "0")]
    [InlineData("--agents", "201")]
    [InlineData("--agents", "20001")]
    [InlineData("--ticks", "0")]
    [InlineData("--ticks", "100000001")]
    [InlineData("--seed", "-1")]
    [InlineData("--unknown", "1")]
    [InlineData("--agents", "not-a-number")]
    public void InvalidArgumentsAreRejectedWithActionableMessage(
        string argument,
        string value)
    {
        var success = HeadlessRunner.TryParseArguments(
            [argument, value],
            out _,
            out var error);

        Assert.False(success);
        Assert.False(string.IsNullOrWhiteSpace(error));
        Assert.Contains(argument, error, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingArgumentValueIsRejected()
    {
        var success = HeadlessRunner.TryParseArguments(
            ["--ticks"],
            out _,
            out var error);

        Assert.False(success);
        Assert.Contains("--ticks", error, StringComparison.Ordinal);
    }

    [Fact]
    public void EventHashMixer_IsSensitiveToWeaponAndHitLocation()
    {
        var baseline = BattleEvent.Attack(
            sequence: 5,
            tick: 3,
            sourceEntityId: 1,
            targetEntityId: 2,
            damage: 10,
            factionId: 0,
            WeaponId.Kampilan,
            ShieldId.None,
            BodyPart.Head);
        var weaponChanged = BattleEvent.Attack(
            sequence: 5,
            tick: 3,
            sourceEntityId: 1,
            targetEntityId: 2,
            damage: 10,
            factionId: 0,
            WeaponId.Itak,
            ShieldId.None,
            BodyPart.Head);
        var locationChanged = BattleEvent.Attack(
            sequence: 5,
            tick: 3,
            sourceEntityId: 1,
            targetEntityId: 2,
            damage: 10,
            factionId: 0,
            WeaponId.Kampilan,
            ShieldId.None,
            BodyPart.Feet);

        var baselineHash = 0UL;
        HeadlessRunner.AddEventToHash(ref baselineHash, baseline);
        var weaponHash = 0UL;
        HeadlessRunner.AddEventToHash(ref weaponHash, weaponChanged);
        var locationHash = 0UL;
        HeadlessRunner.AddEventToHash(ref locationHash, locationChanged);

        Assert.NotEqual(baselineHash, weaponHash);
        Assert.NotEqual(baselineHash, locationHash);
        Assert.NotEqual(weaponHash, locationHash);
    }

    [Fact]
    public void EventHashMixer_NullCombatContextIsStableAndDistinctFromDefinedValues()
    {
        var move = BattleEvent.NonAttack(
            sequence: 5,
            tick: 3,
            BattleEventKind.Move,
            sourceEntityId: 1,
            targetEntityId: 2,
            value: 10,
            factionId: 0);
        var attack = BattleEvent.Attack(
            sequence: 5,
            tick: 3,
            sourceEntityId: 1,
            targetEntityId: 2,
            damage: 10,
            factionId: 0,
            WeaponId.Kampilan,
            ShieldId.None,
            BodyPart.Head);

        var firstMoveHash = 0UL;
        HeadlessRunner.AddEventToHash(ref firstMoveHash, move);
        var secondMoveHash = 0UL;
        HeadlessRunner.AddEventToHash(ref secondMoveHash, move);
        var attackHash = 0UL;
        HeadlessRunner.AddEventToHash(ref attackHash, attack);

        Assert.Equal(firstMoveHash, secondMoveHash);
        Assert.NotEqual(firstMoveHash, attackHash);

        // The resolution follows the same nullable convention as the weapon and
        // the hit location: null on every kind other than Attack, defined on an
        // attack event. A null resolution is unreachable on an attack event, so
        // it cannot be isolated from the kind word; the theory below covers the
        // five defined values, and this covers the sentinel.
        Assert.Null(move.Resolution);
        Assert.NotNull(attack.Resolution);
    }

    /// <summary>
    /// Two attack events identical in every other field but carrying different
    /// resolutions must fold to different event hashes. The resolution is
    /// authoritative and rides on every attack event, so a fold that ignored it
    /// would let a parry and a landed blow share a replay signature.
    /// </summary>
    [Theory]
    [InlineData(AttackResolution.Landed, AttackResolution.ShieldBlocked)]
    [InlineData(AttackResolution.Landed, AttackResolution.Parried)]
    [InlineData(AttackResolution.Landed, AttackResolution.Deflected)]
    [InlineData(AttackResolution.Landed, AttackResolution.Evaded)]
    [InlineData(AttackResolution.ShieldBlocked, AttackResolution.Parried)]
    [InlineData(AttackResolution.ShieldBlocked, AttackResolution.Deflected)]
    [InlineData(AttackResolution.ShieldBlocked, AttackResolution.Evaded)]
    [InlineData(AttackResolution.Parried, AttackResolution.Deflected)]
    [InlineData(AttackResolution.Parried, AttackResolution.Evaded)]
    [InlineData(AttackResolution.Deflected, AttackResolution.Evaded)]
    public void EventHash_DiffersForEveryDistinctResolutionPair(
        AttackResolution first,
        AttackResolution second)
    {
        var firstHash = 0UL;
        HeadlessRunner.AddEventToHash(ref firstHash, AttackWith(first));
        var secondHash = 0UL;
        HeadlessRunner.AddEventToHash(ref secondHash, AttackWith(second));

        Assert.True(
            firstHash != secondHash,
            $"The event hash treated {first} and {second} as the same event: " +
            $"both folded to 0x{firstHash:X16}.");
    }

    [Fact]
    public void Run_CombatMetricsSurviveAJsonRoundTrip()
    {
        var output = new StringWriter();
        var errorOutput = new StringWriter();

        var exitCode = HeadlessRunner.Run(
            ["--agents", "20", "--ticks", "200", "--seed", "1234"],
            output,
            errorOutput);

        Assert.Equal(0, exitCode);
        var deserialized = JsonSerializer.Deserialize<RunReport>(
            output.ToString(),
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });

        Assert.NotNull(deserialized);
        using var report = JsonDocument.Parse(output.ToString());
        var metrics = report.RootElement.GetProperty("combatMetrics");

        Assert.Equal(
            metrics.GetProperty("acceptedAttacks").GetInt64(),
            deserialized.CombatMetrics.AcceptedAttacks);
        Assert.Equal(
            metrics.GetProperty("landedAttacks").GetInt64(),
            deserialized.CombatMetrics.LandedAttacks);
        Assert.Equal(
            metrics.GetProperty("shieldBlockedAttacks").GetInt64(),
            deserialized.CombatMetrics.ShieldBlockedAttacks);
        Assert.Equal(
            metrics.GetProperty("parriedAttacks").GetInt64(),
            deserialized.CombatMetrics.ParriedAttacks);
        Assert.Equal(
            metrics.GetProperty("deflectedAttacks").GetInt64(),
            deserialized.CombatMetrics.DeflectedAttacks);
        Assert.Equal(
            metrics.GetProperty("evadedAttacks").GetInt64(),
            deserialized.CombatMetrics.EvadedAttacks);
    }

    /// <summary>
    /// The case that catches a metric leaking non-determinism. Two same-seed
    /// runs must serialize a byte-identical combat-metrics block.
    /// </summary>
    [Fact]
    public void Run_SerializesByteIdenticalCombatMetricsForTwoSameSeedRuns()
    {
        var firstOutput = new StringWriter();
        var secondOutput = new StringWriter();
        var errorOutput = new StringWriter();
        string[] arguments =
        [
            "--agents", "20", "--ticks", "200", "--seed", "1234",
        ];

        var firstExitCode = HeadlessRunner.Run(arguments, firstOutput, errorOutput);
        var secondExitCode = HeadlessRunner.Run(arguments, secondOutput, errorOutput);

        Assert.Equal(0, firstExitCode);
        Assert.Equal(0, secondExitCode);
        using var firstReport = JsonDocument.Parse(firstOutput.ToString());
        using var secondReport = JsonDocument.Parse(secondOutput.ToString());

        Assert.Equal(
            firstReport.RootElement.GetProperty("combatMetrics").GetRawText(),
            secondReport.RootElement.GetProperty("combatMetrics").GetRawText(),
            StringComparer.Ordinal);
    }

    private static BattleEvent AttackWith(AttackResolution resolution) =>
        BattleEvent.Attack(
            sequence: 5,
            tick: 3,
            sourceEntityId: 1,
            targetEntityId: 2,
            damage: 10,
            factionId: 0,
            WeaponId.Kampilan,
            ShieldId.None,
            BodyPart.Head,
            resolution);

    [Fact]
    public void Run_ProducesIdenticalHashesForTwoIndependentDeterministicRuns()
    {
        var firstOutput = new StringWriter();
        var secondOutput = new StringWriter();
        var errorOutput = new StringWriter();
        string[] arguments =
        [
            "--agents", "20", "--ticks", "200", "--seed", "1234",
        ];

        var firstExitCode = HeadlessRunner.Run(arguments, firstOutput, errorOutput);
        var secondExitCode = HeadlessRunner.Run(arguments, secondOutput, errorOutput);

        Assert.Equal(0, firstExitCode);
        Assert.Equal(0, secondExitCode);
        using var firstReport = JsonDocument.Parse(firstOutput.ToString());
        using var secondReport = JsonDocument.Parse(secondOutput.ToString());

        Assert.Equal(
            firstReport.RootElement.GetProperty("eventHash").GetString(),
            secondReport.RootElement.GetProperty("eventHash").GetString());
        Assert.Equal(
            firstReport.RootElement.GetProperty("stateHash").GetString(),
            secondReport.RootElement.GetProperty("stateHash").GetString());
        Assert.Equal(
            firstReport.RootElement.GetProperty("deterministic").GetBoolean(),
            secondReport.RootElement.GetProperty("deterministic").GetBoolean());
        Assert.True(
            firstReport.RootElement.GetProperty("deterministic").GetBoolean());
    }

    [Fact]
    public void Run_ReportsEveryCollisionMetricAggregatedOverTheRun()
    {
        var output = new StringWriter();
        var errorOutput = new StringWriter();

        var exitCode = HeadlessRunner.Run(
            ["--agents", "20", "--ticks", "200", "--seed", "1234"],
            output,
            errorOutput);

        Assert.Equal(0, exitCode);
        using var report = JsonDocument.Parse(output.ToString());
        var metrics = report.RootElement.GetProperty("collisionMetrics");

        Assert.True(metrics.TryGetProperty("candidatePairs", out _));
        Assert.True(metrics.TryGetProperty("contactPairs", out _));
        Assert.True(metrics.TryGetProperty("acceptedMoves", out _));
        Assert.True(metrics.TryGetProperty("blockedAgentTicks", out _));
        Assert.True(metrics.TryGetProperty("attackCapableAgentTicks", out _));
        Assert.True(metrics.TryGetProperty("longestBlockedStreakTicks", out _));
        Assert.True(metrics.TryGetProperty("maximumFrontWidthRaw", out _));
        Assert.True(metrics.TryGetProperty("maximumFrontDepthRaw", out _));
        Assert.True(metrics.TryGetProperty("maximumPenetrationRaw", out _));
    }

    [Fact]
    public void Run_ReportsCollisionMetricsAsWholeCountsAndRawFixedPointUnits()
    {
        var output = new StringWriter();
        var errorOutput = new StringWriter();

        var exitCode = HeadlessRunner.Run(
            ["--agents", "20", "--ticks", "200", "--seed", "1234"],
            output,
            errorOutput);

        Assert.Equal(0, exitCode);
        using var report = JsonDocument.Parse(output.ToString());
        var metrics = report.RootElement.GetProperty("collisionMetrics");

        Assert.True(metrics.GetProperty("candidatePairs").TryGetInt64(out var candidatePairs));
        Assert.True(candidatePairs >= 0);
        Assert.True(metrics.GetProperty("contactPairs").TryGetInt64(out var contactPairs));
        Assert.True(contactPairs >= 0);
        Assert.True(metrics.GetProperty("acceptedMoves").TryGetInt64(out var acceptedMoves));
        Assert.True(acceptedMoves >= 0);
        Assert.True(
            metrics.GetProperty("blockedAgentTicks").TryGetInt64(out var blockedAgentTicks));
        Assert.True(blockedAgentTicks >= 0);
        Assert.True(
            metrics.GetProperty("attackCapableAgentTicks")
                .TryGetInt64(out var attackCapableAgentTicks));
        Assert.True(attackCapableAgentTicks >= 0);
        Assert.True(
            metrics.GetProperty("longestBlockedStreakTicks").TryGetInt32(out var longestStreak));
        Assert.True(longestStreak >= 0);
        Assert.True(metrics.GetProperty("maximumFrontWidthRaw").TryGetInt32(out var frontWidthRaw));
        Assert.True(frontWidthRaw >= 0);
        Assert.True(metrics.GetProperty("maximumFrontDepthRaw").TryGetInt32(out var frontDepthRaw));
        Assert.True(frontDepthRaw >= 0);

        // The approved policy is Solid, so any nonzero penetration is a
        // contract violation rather than a tuning signal.
        Assert.Equal(0, metrics.GetProperty("maximumPenetrationRaw").GetInt32());
    }

    [Fact]
    public void Run_CollisionMetricsSurviveAJsonRoundTrip()
    {
        var output = new StringWriter();
        var errorOutput = new StringWriter();

        var exitCode = HeadlessRunner.Run(
            ["--agents", "20", "--ticks", "200", "--seed", "1234"],
            output,
            errorOutput);

        Assert.Equal(0, exitCode);
        var deserialized = JsonSerializer.Deserialize<RunReport>(
            output.ToString(),
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });

        Assert.NotNull(deserialized);
        using var report = JsonDocument.Parse(output.ToString());
        var metrics = report.RootElement.GetProperty("collisionMetrics");

        Assert.Equal(
            metrics.GetProperty("candidatePairs").GetInt64(),
            deserialized.CollisionMetrics.CandidatePairs);
        Assert.Equal(
            metrics.GetProperty("contactPairs").GetInt64(),
            deserialized.CollisionMetrics.ContactPairs);
        Assert.Equal(
            metrics.GetProperty("acceptedMoves").GetInt64(),
            deserialized.CollisionMetrics.AcceptedMoves);
        Assert.Equal(
            metrics.GetProperty("blockedAgentTicks").GetInt64(),
            deserialized.CollisionMetrics.BlockedAgentTicks);
        Assert.Equal(
            metrics.GetProperty("attackCapableAgentTicks").GetInt64(),
            deserialized.CollisionMetrics.AttackCapableAgentTicks);
        Assert.Equal(
            metrics.GetProperty("longestBlockedStreakTicks").GetInt32(),
            deserialized.CollisionMetrics.LongestBlockedStreakTicks);
        Assert.Equal(
            metrics.GetProperty("maximumFrontWidthRaw").GetInt32(),
            deserialized.CollisionMetrics.MaximumFrontWidthRaw);
        Assert.Equal(
            metrics.GetProperty("maximumFrontDepthRaw").GetInt32(),
            deserialized.CollisionMetrics.MaximumFrontDepthRaw);
        Assert.Equal(
            metrics.GetProperty("maximumPenetrationRaw").GetInt32(),
            deserialized.CollisionMetrics.MaximumPenetrationRaw);
    }

    [Fact]
    public void Run_SerializesByteIdenticalCollisionMetricsForTwoSameSeedRuns()
    {
        var firstOutput = new StringWriter();
        var secondOutput = new StringWriter();
        var errorOutput = new StringWriter();
        string[] arguments =
        [
            "--agents", "20", "--ticks", "200", "--seed", "1234",
        ];

        var firstExitCode = HeadlessRunner.Run(arguments, firstOutput, errorOutput);
        var secondExitCode = HeadlessRunner.Run(arguments, secondOutput, errorOutput);

        Assert.Equal(0, firstExitCode);
        Assert.Equal(0, secondExitCode);
        using var firstReport = JsonDocument.Parse(firstOutput.ToString());
        using var secondReport = JsonDocument.Parse(secondOutput.ToString());

        Assert.Equal(
            firstReport.RootElement.GetProperty("collisionMetrics").GetRawText(),
            secondReport.RootElement.GetProperty("collisionMetrics").GetRawText(),
            StringComparer.Ordinal);
    }

    /// <summary>
    /// coreAllocatedBytes measures only left.AdvanceOneTick() across the run,
    /// while allocatedBytes measures the whole harness loop -- both
    /// simulations, both hash computations, and the determinism comparison.
    /// The Core figure must be strictly positive (advancing a live 20-agent
    /// battle allocates something) and can never exceed the harness total it
    /// is a strict subset of.
    /// </summary>
    [Fact]
    public void Run_ReportsACoreAllocationFigureThatIsPositiveAndNeverExceedsTheHarnessTotal()
    {
        var output = new StringWriter();
        var errorOutput = new StringWriter();

        var exitCode = HeadlessRunner.Run(
            ["--agents", "20", "--ticks", "200", "--seed", "1234"],
            output,
            errorOutput);

        Assert.Equal(0, exitCode);
        var deserialized = JsonSerializer.Deserialize<RunReport>(
            output.ToString(),
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });

        Assert.NotNull(deserialized);
        using var report = JsonDocument.Parse(output.ToString());

        Assert.Equal(
            report.RootElement.GetProperty("coreAllocatedBytes").GetInt64(),
            deserialized.CoreAllocatedBytes);
        Assert.True(
            deserialized.CoreAllocatedBytes > 0,
            $"Expected a strictly positive core allocation figure, got " +
            $"{deserialized.CoreAllocatedBytes}.");
        Assert.True(
            deserialized.CoreAllocatedBytes <= deserialized.AllocatedBytes,
            $"Core allocation {deserialized.CoreAllocatedBytes} exceeded the " +
            $"harness total {deserialized.AllocatedBytes}.");
    }

    [Fact]
    public void MovementPresetArgument_NameAndNumericValueParseToTheSameValue()
    {
        var byNameSuccess = HeadlessRunner.TryParseArguments(
            ["--movement-preset", "IndependentPursuitV1"],
            out var byNameOptions,
            out var byNameError);
        var byNumberSuccess = HeadlessRunner.TryParseArguments(
            ["--movement-preset", "1"],
            out var byNumberOptions,
            out var byNumberError);

        Assert.True(byNameSuccess, byNameError);
        Assert.True(byNumberSuccess, byNumberError);
        Assert.Equal(MovementPresetId.IndependentPursuitV1, byNameOptions.MovementPreset);
        Assert.Equal(byNameOptions.MovementPreset, byNumberOptions.MovementPreset);
    }

    /// <summary>
    /// An unregistered enum value and a value that is not an enum member at
    /// all both fail parsing, and both surface through <see cref="HeadlessRunner.Run"/>
    /// as the same exit code as every other malformed argument.
    /// </summary>
    [Theory]
    [InlineData("99")]
    [InlineData("nonsense")]
    public void Run_RejectsAnUnregisteredMovementPresetWithExitCode2(string value)
    {
        var output = new StringWriter();
        var errorOutput = new StringWriter();

        var exitCode = HeadlessRunner.Run(
            ["--movement-preset", value],
            output,
            errorOutput);

        Assert.Equal(2, exitCode);
        Assert.Contains("--movement-preset", errorOutput.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Omitting the switch leaves <see cref="HeadlessOptions.MovementPreset"/>
    /// null, and a run without it must be byte-identical -- both hashes -- to
    /// the same run with the switch supplied explicitly as
    /// <see cref="MovementPresetId.PersistentContingentsV4"/>, which is
    /// <see cref="Scenario"/>'s own default. That default was
    /// <see cref="MovementPresetId.PersistentContingentsV2"/> until task T6 of
    /// docs/archives/2026-07-28/2026-07-28-contingent-close-latch.md flipped it (itself a
    /// flip from <see cref="MovementPresetId.IndependentPursuitV1"/> by task
    /// T15 of
    /// docs/archives/2026-07-28/2026-07-28-formation-movement-realism.md),
    /// and <see cref="MovementPresetId.PersistentContingentsV3"/> until the
    /// cross-contingent scan narrowing flipped it again; the
    /// comparison itself, and the fact that implicit and explicit runs must
    /// agree, is unchanged -- only which preset name the "explicit" run
    /// names has moved.
    /// </summary>
    /// <summary>
    /// The derived movement counters of design section 16 ride the report as
    /// one camel-case block. A V6 workload must produce nonzero behaviour
    /// counts — every warrior opens with a posture transition out of
    /// <c>None</c> and an <c>Approach</c> toward the enemy line — and every
    /// field must survive the reflection-based JSON round trip.
    /// </summary>
    [Fact]
    public void Run_MovementMetricsSurviveAJsonRoundTripWithNonzeroV6Counters()
    {
        var output = new StringWriter();
        var errorOutput = new StringWriter();

        var exitCode = HeadlessRunner.Run(
            [
                "--agents", "20", "--ticks", "200", "--seed", "1234",
                "--preset", "PrecolonialPhilippinesV2",
                "--movement-preset", "EquipmentRelativeFootworkV6",
            ],
            output,
            errorOutput);

        Assert.Equal(0, exitCode);
        var deserialized = JsonSerializer.Deserialize<RunReport>(
            output.ToString(),
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });

        Assert.NotNull(deserialized);
        using var report = JsonDocument.Parse(output.ToString());
        var metrics = report.RootElement.GetProperty("movementMetrics");

        Assert.Equal(
            metrics.GetProperty("approachAgentTicks").GetInt64(),
            deserialized.MovementMetrics.ApproachAgentTicks);
        Assert.Equal(
            metrics.GetProperty("engageAgentTicks").GetInt64(),
            deserialized.MovementMetrics.EngageAgentTicks);
        Assert.Equal(
            metrics.GetProperty("commitAgentTicks").GetInt64(),
            deserialized.MovementMetrics.CommitAgentTicks);
        Assert.Equal(
            metrics.GetProperty("recoverAgentTicks").GetInt64(),
            deserialized.MovementMetrics.RecoverAgentTicks);
        Assert.Equal(
            metrics.GetProperty("refuseAgentTicks").GetInt64(),
            deserialized.MovementMetrics.RefuseAgentTicks);
        Assert.Equal(
            metrics.GetProperty("disengageAgentTicks").GetInt64(),
            deserialized.MovementMetrics.DisengageAgentTicks);
        Assert.Equal(
            metrics.GetProperty("regroupAgentTicks").GetInt64(),
            deserialized.MovementMetrics.RegroupAgentTicks);
        Assert.Equal(
            metrics.GetProperty("pursueAgentTicks").GetInt64(),
            deserialized.MovementMetrics.PursueAgentTicks);
        Assert.Equal(
            metrics.GetProperty("postureTransitions").GetInt64(),
            deserialized.MovementMetrics.PostureTransitions);
        Assert.Equal(
            metrics.GetProperty("facingStepsTurned").GetInt64(),
            deserialized.MovementMetrics.FacingStepsTurned);
        Assert.Equal(
            metrics.GetProperty("disengagementEntries").GetInt64(),
            deserialized.MovementMetrics.DisengagementEntries);
        Assert.Equal(
            metrics.GetProperty("conflictDenials").GetInt64(),
            deserialized.MovementMetrics.ConflictDenials);

        Assert.True(
            deserialized.MovementMetrics.ApproachAgentTicks > 0,
            "A V6 run must spend agent-ticks approaching the enemy line.");
        Assert.True(
            deserialized.MovementMetrics.PostureTransitions > 0,
            "Every living V6 warrior transitions out of posture None on the first tick.");
    }

    /// <summary>
    /// A legacy preset never runs any equipment-relative footwork stage, so
    /// its report must carry an all-zero movement block, and a report built
    /// or parsed without the member — every report written before T9 — must
    /// default to the same all-zero value.
    /// </summary>
    [Fact]
    public void Run_ReportsZeroMovementMetricsUnderALegacyPreset()
    {
        var output = new StringWriter();
        var errorOutput = new StringWriter();

        var exitCode = HeadlessRunner.Run(
            ["--agents", "20", "--ticks", "200", "--seed", "1234"],
            output,
            errorOutput);

        Assert.Equal(0, exitCode);
        var deserialized = JsonSerializer.Deserialize<RunReport>(
            output.ToString(),
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });

        Assert.NotNull(deserialized);
        Assert.Equal(default(MovementBehaviorMetrics), deserialized.MovementMetrics);
    }

    [Fact]
    public void ALegacyReportWithoutTheMovementBlockDeserializesToDefaultMetrics()
    {
        var output = new StringWriter();
        var errorOutput = new StringWriter();
        var exitCode = HeadlessRunner.Run(
            ["--agents", "20", "--ticks", "50", "--seed", "1234"],
            output,
            errorOutput);
        Assert.Equal(0, exitCode);

        // Strip the block to reconstruct the shape every report written
        // before this member existed actually has on disk.
        var node = System.Text.Json.Nodes.JsonNode.Parse(output.ToString());
        Assert.NotNull(node);
        var stripped = node.AsObject();
        Assert.True(stripped.Remove("movementMetrics"));

        var deserialized = JsonSerializer.Deserialize<RunReport>(
            stripped.ToJsonString(),
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });

        Assert.NotNull(deserialized);
        Assert.Equal(default(MovementBehaviorMetrics), deserialized.MovementMetrics);
    }

    /// <summary>
    /// The case that catches the movement observation leaking either
    /// non-determinism or simulation influence at the harness level: two
    /// same-seed V6 runs, both accumulating metrics, must agree on both
    /// hashes and serialize a byte-identical movement block.
    /// </summary>
    [Fact]
    public void Run_TwoSameSeedV6RunsAgreeOnBothHashesAndTheMovementBlock()
    {
        var firstOutput = new StringWriter();
        var secondOutput = new StringWriter();
        var errorOutput = new StringWriter();
        string[] arguments =
        [
            "--agents", "20", "--ticks", "200", "--seed", "1234",
            "--preset", "PrecolonialPhilippinesV2",
            "--movement-preset", "EquipmentRelativeFootworkV6",
        ];

        var firstExitCode = HeadlessRunner.Run(arguments, firstOutput, errorOutput);
        var secondExitCode = HeadlessRunner.Run(arguments, secondOutput, errorOutput);

        Assert.Equal(0, firstExitCode);
        Assert.Equal(0, secondExitCode);
        using var firstReport = JsonDocument.Parse(firstOutput.ToString());
        using var secondReport = JsonDocument.Parse(secondOutput.ToString());

        Assert.True(firstReport.RootElement.GetProperty("deterministic").GetBoolean());
        Assert.Equal(
            firstReport.RootElement.GetProperty("stateHash").GetString(),
            secondReport.RootElement.GetProperty("stateHash").GetString());
        Assert.Equal(
            firstReport.RootElement.GetProperty("eventHash").GetString(),
            secondReport.RootElement.GetProperty("eventHash").GetString());
        Assert.Equal(
            firstReport.RootElement.GetProperty("movementMetrics").GetRawText(),
            secondReport.RootElement.GetProperty("movementMetrics").GetRawText(),
            StringComparer.Ordinal);
    }

    /// <summary>
    /// The boundary proof of design section 16.3: the same seeded V6
    /// workload advanced twice, once with the whole derived observation
    /// exercised every tick — view comparison, per-tick combat and collision
    /// reads, the denial counter — and once reading nothing, must produce an
    /// identical state hash, ordered event stream, and outcome on every
    /// tick. The observability reaches neither hash.
    /// </summary>
    [Fact]
    public void ObservingDerivedMovementBehaviorChangesNeitherHashNorEventStream()
    {
        var scenario = Scenario.CreateDefault(seed: 1234, totalAgents: 20) with
        {
            TickLimit = 200,
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV2,
            MovementPreset = MovementPresetId.EquipmentRelativeFootworkV6,
        };
        scenario.Validate();
        var observed = BattleSimulation.Create(scenario);
        var ignored = BattleSimulation.Create(scenario);

        var accumulator = default(MovementBehaviorMetricsAccumulator);
        var previousViews = new AgentView[observed.Agents.Count];
        for (var index = 0; index < previousViews.Length; index++)
        {
            previousViews[index] = observed.Agents[index];
        }

        for (var tick = 0;
             tick < 200 && observed.Outcome == BattleOutcome.Ongoing;
             tick++)
        {
            observed.AdvanceOneTick();
            var observation = HeadlessRunner.ObserveMovementTick(
                observed.Agents, previousViews);
            accumulator.AddTick(
                observation.ApproachAgents,
                observation.EngageAgents,
                observation.CommitAgents,
                observation.RecoverAgents,
                observation.RefuseAgents,
                observation.DisengageAgents,
                observation.RegroupAgents,
                observation.PursueAgents,
                observation.PostureTransitions,
                observation.FacingStepsTurned,
                observation.DisengagementEntries);
            _ = observed.LastTickCombat;
            _ = observed.MovementConflictDenials;

            ignored.AdvanceOneTick();

            Assert.Equal(ignored.ComputeStateHash(), observed.ComputeStateHash());
            Assert.True(
                observed.LastEvents.SequenceEqual(ignored.LastEvents),
                $"The event streams diverged at tick {observed.Tick}.");
        }

        accumulator.RecordConflictDenialTotal(observed.MovementConflictDenials);
        Assert.Equal(ignored.Outcome, observed.Outcome);
        Assert.Equal(ignored.ComputeStateHash(), observed.ComputeStateHash());

        var metrics = accumulator.ToMetrics();
        Assert.True(
            metrics.ApproachAgentTicks > 0,
            "The observation must actually have been exercised.");
        Assert.True(metrics.PostureTransitions > 0);
    }

    [Fact]
    public void OmittingMovementPresetSelectsTheScenarioDefault()
    {
        var success = HeadlessRunner.TryParseArguments(
            [],
            out var options,
            out var error);

        Assert.True(success, error);
        Assert.Null(options.MovementPreset);

        var implicitOutput = new StringWriter();
        var explicitOutput = new StringWriter();
        var errorOutput = new StringWriter();
        string[] baseArguments = ["--agents", "20", "--ticks", "200", "--seed", "1234"];

        var implicitExitCode = HeadlessRunner.Run(baseArguments, implicitOutput, errorOutput);
        var explicitExitCode = HeadlessRunner.Run(
            [.. baseArguments, "--movement-preset", "PersistentContingentsV4"],
            explicitOutput,
            errorOutput);

        Assert.Equal(0, implicitExitCode);
        Assert.Equal(0, explicitExitCode);
        using var implicitReport = JsonDocument.Parse(implicitOutput.ToString());
        using var explicitReport = JsonDocument.Parse(explicitOutput.ToString());

        Assert.Equal(
            implicitReport.RootElement.GetProperty("eventHash").GetString(),
            explicitReport.RootElement.GetProperty("eventHash").GetString());
        Assert.Equal(
            implicitReport.RootElement.GetProperty("stateHash").GetString(),
            explicitReport.RootElement.GetProperty("stateHash").GetString());
    }
}
