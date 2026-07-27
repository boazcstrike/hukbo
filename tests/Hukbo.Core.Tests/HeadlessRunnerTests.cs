using System.Text.Json;
using Hukbo.Core.Combat;
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
}
