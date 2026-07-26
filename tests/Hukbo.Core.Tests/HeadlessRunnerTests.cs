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
}
