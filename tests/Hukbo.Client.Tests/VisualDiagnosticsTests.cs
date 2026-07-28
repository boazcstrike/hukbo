using System.Text.Json;
using Hukbo.Client.Presentation.Catalogs;
using Hukbo.Diagnostics;

namespace Hukbo.Client.Tests;

/// <summary>
/// Pins the three missing-visual diagnostics (visual-system-integration-
/// design.md section 4, R-W6.5): the wire shape of each emitted line, the
/// once-per-distinct-identifier dedup, the fixed seen-set capacity, and that
/// the disabled and already-seen paths allocate nothing.
/// </summary>
public sealed class VisualDiagnosticsTests
{
    // --- Wire shape ---

    [Fact]
    public void ReportVariantMissing_WritesTheThreeDocumentedFields()
    {
        var writer = new StringWriter();
        using var log = DiagnosticLog.CreateForWriter(
            new LogOptions(LogLevel.Warning, LogChannel.All, null),
            writer);
        var diagnostics = new VisualDiagnostics();

        diagnostics.ReportVariantMissing(
            log,
            "weapon.kalis",
            "weapon.kalis.l7",
            VisualFallbackStep.FamilyDefault);

        var line = Assert.Single(ReadLines(writer));
        using var document = JsonDocument.Parse(line);
        var root = document.RootElement;
        Assert.Equal("warn", root.GetProperty("lvl").GetString());
        Assert.Equal("assets", root.GetProperty("ch").GetString());
        Assert.Equal(LogEvents.AssetsVisualVariantMissing, root.GetProperty("ev").GetString());
        Assert.Equal("weapon.kalis", root.GetProperty("catalogId").GetString());
        Assert.Equal("weapon.kalis.l7", root.GetProperty("requestedId").GetString());
        Assert.Equal(
            (int)VisualFallbackStep.FamilyDefault,
            root.GetProperty("resolvedStep").GetInt32());
    }

    [Fact]
    public void ReportFallback_WritesTheTwoDocumentedFields()
    {
        var writer = new StringWriter();
        using var log = DiagnosticLog.CreateForWriter(
            new LogOptions(LogLevel.Warning, LogChannel.All, null),
            writer);
        var diagnostics = new VisualDiagnostics();

        diagnostics.ReportFallback(log, "shield.tallHardwood", "shield.tallHardwood.mactanThin");

        var line = Assert.Single(ReadLines(writer));
        using var document = JsonDocument.Parse(line);
        var root = document.RootElement;
        Assert.Equal(LogEvents.AssetsVisualFallback, root.GetProperty("ev").GetString());
        Assert.Equal("shield.tallHardwood", root.GetProperty("catalogId").GetString());
        Assert.Equal(
            "shield.tallHardwood.mactanThin",
            root.GetProperty("requestedId").GetString());
    }

    [Fact]
    public void ReportCatalogInvalid_WithoutMessage_WritesCatalogIdAndReasonOnly()
    {
        var writer = new StringWriter();
        using var log = DiagnosticLog.CreateForWriter(
            new LogOptions(LogLevel.Warning, LogChannel.All, null),
            writer);
        var diagnostics = new VisualDiagnostics();

        diagnostics.ReportCatalogInvalid(log, "appearance.headcovering", "duplicateId");

        var line = Assert.Single(ReadLines(writer));
        var fields = ReadFieldNames(line);
        Assert.Equal(
            ["seq", "t", "ms", "lvl", "ch", "ev", "catalogId", "reason"],
            fields);

        using var document = JsonDocument.Parse(line);
        var root = document.RootElement;
        Assert.Equal(LogEvents.AssetsVisualCatalogInvalid, root.GetProperty("ev").GetString());
        Assert.Equal("appearance.headcovering", root.GetProperty("catalogId").GetString());
        Assert.Equal("duplicateId", root.GetProperty("reason").GetString());
    }

    [Fact]
    public void ReportCatalogInvalid_WithMessage_AddsAnOptionalMsgFieldLast()
    {
        var writer = new StringWriter();
        using var log = DiagnosticLog.CreateForWriter(
            new LogOptions(LogLevel.Warning, LogChannel.All, null),
            writer);
        var diagnostics = new VisualDiagnostics();

        diagnostics.ReportCatalogInvalid(
            log,
            "appearance.headcovering",
            "duplicateId",
            "'appearance.headcovering.putong' appears twice.");

        var line = Assert.Single(ReadLines(writer));
        var fields = ReadFieldNames(line);
        Assert.Equal(
            ["seq", "t", "ms", "lvl", "ch", "ev", "catalogId", "reason", "msg"],
            fields);

        using var document = JsonDocument.Parse(line);
        Assert.Equal(
            "'appearance.headcovering.putong' appears twice.",
            document.RootElement.GetProperty("msg").GetString());
    }

    // --- Dedup: once per distinct identifier ---

    [Fact]
    public void ReportVariantMissing_EmitsOnlyOnceForTheSameCatalogAndRequestedId()
    {
        var writer = new StringWriter();
        using var log = DiagnosticLog.CreateForWriter(
            new LogOptions(LogLevel.Warning, LogChannel.All, null),
            writer);
        var diagnostics = new VisualDiagnostics();

        for (var i = 0; i < 5; i++)
        {
            diagnostics.ReportVariantMissing(
                log,
                "weapon.kalis",
                "weapon.kalis.l7",
                VisualFallbackStep.FamilyDefault);
        }

        Assert.Single(ReadLines(writer));
    }

    [Fact]
    public void ReportFallback_EmitsAgainForADifferentRequestedIdOnTheSameCatalog()
    {
        var writer = new StringWriter();
        using var log = DiagnosticLog.CreateForWriter(
            new LogOptions(LogLevel.Warning, LogChannel.All, null),
            writer);
        var diagnostics = new VisualDiagnostics();

        diagnostics.ReportFallback(log, "weapon.kalis", "weapon.kalis.l7");
        diagnostics.ReportFallback(log, "weapon.kalis", "weapon.kalis.l9");

        Assert.Equal(2, ReadLines(writer).Length);
    }

    [Fact]
    public void DifferentEventsForTheSameIdentifierEachEmitOnce()
    {
        var writer = new StringWriter();
        using var log = DiagnosticLog.CreateForWriter(
            new LogOptions(LogLevel.Warning, LogChannel.All, null),
            writer);
        var diagnostics = new VisualDiagnostics();

        diagnostics.ReportVariantMissing(
            log,
            "weapon.kalis",
            "weapon.kalis.l7",
            VisualFallbackStep.FamilyDefault);
        diagnostics.ReportFallback(log, "weapon.kalis", "weapon.kalis.l7");

        Assert.Equal(2, ReadLines(writer).Length);
    }

    // --- Bounded seen-set (R-X.16) ---

    [Fact]
    public void TheSeenSetStopsLoggingOnceCapacityIsReached()
    {
        var writer = new StringWriter();
        using var log = DiagnosticLog.CreateForWriter(
            new LogOptions(LogLevel.Warning, LogChannel.All, null),
            writer);
        var diagnostics = new VisualDiagnostics();

        for (var i = 0; i < VisualDiagnostics.SeenSetCapacity + 10; i++)
        {
            diagnostics.ReportFallback(log, "weapon.kalis", $"weapon.kalis.v{i}");
        }

        Assert.Equal(VisualDiagnostics.SeenSetCapacity, ReadLines(writer).Length);
    }

    [Fact]
    public void CapacityIsOnTheOrderOfSixtyFourPerTheDesign()
    {
        Assert.Equal(64, VisualDiagnostics.SeenSetCapacity);
    }

    // --- Allocation: disabled and already-seen paths cost nothing ---

    [Fact]
    public void ADisabledLogAllocatesNothingAcrossRepeatedReports()
    {
        var diagnostics = new VisualDiagnostics();
        var log = DiagnosticLog.Disabled;

        for (var i = 0; i < 100; i++)
        {
            diagnostics.ReportFallback(log, "weapon.kalis", "weapon.kalis.l7");
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1_000; i++)
        {
            diagnostics.ReportFallback(log, "weapon.kalis", "weapon.kalis.l7");
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void AnAlreadySeenIdentifierAllocatesNothingOnRepeatedReports()
    {
        var writer = new StringWriter();
        using var log = DiagnosticLog.CreateForWriter(
            new LogOptions(LogLevel.Warning, LogChannel.All, null),
            writer);
        var diagnostics = new VisualDiagnostics();

        // First call records the identifier and writes the line; only the
        // repeats after this are measured.
        diagnostics.ReportFallback(log, "weapon.kalis", "weapon.kalis.l7");

        for (var i = 0; i < 100; i++)
        {
            diagnostics.ReportFallback(log, "weapon.kalis", "weapon.kalis.l7");
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1_000; i++)
        {
            diagnostics.ReportFallback(log, "weapon.kalis", "weapon.kalis.l7");
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    // --- Argument validation ---

    [Fact]
    public void ReportVariantMissing_ThrowsOnNullLog()
    {
        var diagnostics = new VisualDiagnostics();

        Assert.Throws<ArgumentNullException>(() =>
            diagnostics.ReportVariantMissing(
                null!,
                "weapon.kalis",
                "weapon.kalis.l7",
                VisualFallbackStep.FamilyDefault));
    }

    [Fact]
    public void ReportFallback_ThrowsOnNullCatalogId()
    {
        var diagnostics = new VisualDiagnostics();
        using var log = DiagnosticLog.CreateForWriter(
            new LogOptions(LogLevel.Warning, LogChannel.All, null),
            new StringWriter());

        Assert.Throws<ArgumentNullException>(() =>
            diagnostics.ReportFallback(log, null!, "weapon.kalis.l7"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ReportFallback_ThrowsOnBlankCatalogId(string catalogId)
    {
        var diagnostics = new VisualDiagnostics();
        using var log = DiagnosticLog.CreateForWriter(
            new LogOptions(LogLevel.Warning, LogChannel.All, null),
            new StringWriter());

        Assert.Throws<ArgumentException>(() =>
            diagnostics.ReportFallback(log, catalogId, "weapon.kalis.l7"));
    }

    [Fact]
    public void ReportFallback_ThrowsOnNullRequestedId()
    {
        var diagnostics = new VisualDiagnostics();
        using var log = DiagnosticLog.CreateForWriter(
            new LogOptions(LogLevel.Warning, LogChannel.All, null),
            new StringWriter());

        Assert.Throws<ArgumentNullException>(() =>
            diagnostics.ReportFallback(log, "weapon.kalis", null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ReportFallback_ThrowsOnBlankRequestedId(string requestedId)
    {
        var diagnostics = new VisualDiagnostics();
        using var log = DiagnosticLog.CreateForWriter(
            new LogOptions(LogLevel.Warning, LogChannel.All, null),
            new StringWriter());

        Assert.Throws<ArgumentException>(() =>
            diagnostics.ReportFallback(log, "weapon.kalis", requestedId));
    }

    private static string[] ReadFieldNames(string line)
    {
        using var document = JsonDocument.Parse(line);
        return document.RootElement
            .EnumerateObject()
            .Select(property => property.Name)
            .ToArray();
    }

    private static string[] ReadLines(StringWriter writer) =>
        writer.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
}
