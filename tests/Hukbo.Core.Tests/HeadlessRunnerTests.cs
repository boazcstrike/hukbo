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
            WeaponId.GreatBlade,
            BodyPart.Head);
        var weaponChanged = BattleEvent.Attack(
            sequence: 5,
            tick: 3,
            sourceEntityId: 1,
            targetEntityId: 2,
            damage: 10,
            factionId: 0,
            WeaponId.Bolo,
            BodyPart.Head);
        var locationChanged = BattleEvent.Attack(
            sequence: 5,
            tick: 3,
            sourceEntityId: 1,
            targetEntityId: 2,
            damage: 10,
            factionId: 0,
            WeaponId.GreatBlade,
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
            WeaponId.GreatBlade,
            BodyPart.Head);

        var firstMoveHash = 0UL;
        HeadlessRunner.AddEventToHash(ref firstMoveHash, move);
        var secondMoveHash = 0UL;
        HeadlessRunner.AddEventToHash(ref secondMoveHash, move);
        var attackHash = 0UL;
        HeadlessRunner.AddEventToHash(ref attackHash, attack);

        Assert.Equal(firstMoveHash, secondMoveHash);
        Assert.NotEqual(firstMoveHash, attackHash);
    }

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
}
