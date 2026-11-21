using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Hukbo.Client.Presentation;
using Hukbo.Client.Settings;
using Hukbo.Client.Theming;
using Hukbo.Client.UI;
using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Hukbo.Client.Tests;

/// <summary>
/// The damage card's pure layout and its hover and refresh behaviour, per the
/// weapon damage tally design's section 5. No <c>SpriteBatch</c> is built:
/// <see cref="DamageTallyPanel.Draw"/> only paints what these helpers decided.
/// </summary>
[Collection(UiScaleContextCollectionDefinition.Name)]
public sealed class DamageTallyLayoutTests
{
    private const int Margin = 20;

    public static TheoryData<int, int, int, int, int, int> SupportedViewports => new()
    {
        { 0, 0, 1280, 720, 11, 11 },
        { 0, 0, 1600, 900, 11, 11 },
        { 0, 0, 1920, 1080, 11, 11 },
        { 0, 0, 1280, 720, 21, 21 },
        { 0, 0, 1280, 720, 21, 3 },
        { 0, 0, 1280, 720, 0, 0 },
        { 300, 40, 1280, 720, 11, 11 },
        { 0, 0, 1280, 160, 11, 11 },
        { 0, 0, 1280, 60, 11, 11 },
    };

    [Theory]
    [MemberData(nameof(SupportedViewports))]
    public void Calculate_AnchorsTopRightAndStaysInsideTheViewport(
        int left,
        int top,
        int width,
        int height,
        int firstRows,
        int secondRows)
    {
        var arena = new Rectangle(left, top, width, height);

        var layout = DamageTallyLayout.Calculate(arena, firstRows, secondRows);

        AssertInside(arena, layout.Bounds);
        Assert.Equal(arena.Right - Margin, layout.Bounds.Right);
        Assert.Equal(arena.Top + Margin, layout.Bounds.Top);
        Assert.Equal(DamageTallyLayout.PreferredWidth, layout.Bounds.Width);
    }

    [Theory]
    [MemberData(nameof(SupportedViewports))]
    public void Calculate_KeepsEveryRegionInsideTheCardAndRowsNeverOverlap(
        int left,
        int top,
        int width,
        int height,
        int firstRows,
        int secondRows)
    {
        var layout = DamageTallyLayout.Calculate(
            new Rectangle(left, top, width, height),
            firstRows,
            secondRows);

        var stacked = new List<Rectangle> { layout.TitleBounds };
        foreach (var faction in new[] { 0, 1 })
        {
            var section = layout.GetSection(faction);
            stacked.Add(section.HeaderBounds);
            for (var index = 0; index < section.VisibleRowCount; index++)
            {
                stacked.Add(DamageTallyLayout.GetRowBounds(section, index));
            }
        }

        stacked.Add(layout.FooterBounds);

        foreach (var region in stacked)
        {
            AssertInside(layout.Bounds, region);
        }

        for (var index = 1; index < stacked.Count; index++)
        {
            Assert.True(
                stacked[index - 1].Bottom <= stacked[index].Top,
                $"Region {index - 1} overlaps region {index}.");
        }
    }

    [Fact]
    public void Calculate_AtSeventyTwentyDropsTheWeakestRowsAndSharesTheBudget()
    {
        var arena = new Rectangle(0, 0, 1280, 720);

        var both = DamageTallyLayout.Calculate(arena, 21, 21);
        var lopsided = DamageTallyLayout.Calculate(arena, 21, 3);
        var fits = DamageTallyLayout.Calculate(arena, 4, 4);

        Assert.True(both.FirstSection.VisibleRowCount < 21);
        Assert.Equal(
            both.FirstSection.VisibleRowCount,
            both.SecondSection.VisibleRowCount);
        Assert.Equal(3, lopsided.SecondSection.VisibleRowCount);
        Assert.True(
            lopsided.FirstSection.VisibleRowCount >
            both.FirstSection.VisibleRowCount);
        Assert.Equal(4, fits.FirstSection.VisibleRowCount);
        Assert.Equal(4, fits.SecondSection.VisibleRowCount);
    }

    [Fact]
    public void Calculate_ShrinksTheCardToItsRows()
    {
        var arena = new Rectangle(0, 0, 1920, 1080);

        var empty = DamageTallyLayout.Calculate(arena, 0, 0);
        var full = DamageTallyLayout.Calculate(arena, 5, 5);

        Assert.Equal(
            10 * DamageTallyLayout.RowHeight,
            full.Bounds.Height - empty.Bounds.Height);
    }

    [Fact]
    public void Calculate_StaysInsideAtTwoHundredPercentScale()
    {
        WithScale(
            UiScale.Percent200,
            () =>
            {
                var arena = new Rectangle(0, 0, 2560, 1440);

                var layout = DamageTallyLayout.Calculate(arena, 11, 11);

                AssertInside(arena, layout.Bounds);
                Assert.Equal(52, DamageTallyLayout.RowHeight);
                Assert.Equal(1200, layout.Bounds.Width);
            });
    }

    [Theory]
    [InlineData(600)]
    [InlineData(300)]
    [InlineData(60)]
    public void GetRowColumns_NeverOverlapAndStayInsideTheRow(int rowWidth)
    {
        var row = new Rectangle(100, 50, rowWidth, DamageTallyLayout.RowHeight);

        var columns = DamageTallyLayout.GetRowColumns(row);

        Rectangle[] ordered =
            [columns.Label, columns.Damage, columns.Share, columns.Blows, columns.Alive];
        foreach (var column in ordered)
        {
            AssertInside(row, column);
        }

        for (var index = 1; index < ordered.Length; index++)
        {
            Assert.True(ordered[index - 1].Right <= ordered[index].Left);
        }
    }

    [Theory]
    [InlineData(-1f, 0)]
    [InlineData(0f, 0)]
    [InlineData(0.5f, 50)]
    [InlineData(1f, 100)]
    [InlineData(7f, 100)]
    [InlineData(float.NaN, 0)]
    public void GetBarBounds_ClampsTheFractionToTheRow(float fraction, int expectedWidth)
    {
        var row = new Rectangle(10, 10, 100, 26);

        var bar = DamageTallyLayout.GetBarBounds(row, fraction);

        Assert.Equal(expectedWidth, bar.Width);
        Assert.Equal(row.Left, bar.Left);
        Assert.Equal(row.Height, bar.Height);
    }

    [Fact]
    public void HitTestRow_FindsEachRowAndNothingElse()
    {
        var layout = DamageTallyLayout.Calculate(new Rectangle(0, 0, 1920, 1080), 3, 2);

        for (var index = 0; index < 3; index++)
        {
            var center = DamageTallyLayout.GetRowBounds(layout.FirstSection, index).Center;
            Assert.Equal(
                new DamageTallyRowHit(0, index),
                DamageTallyLayout.HitTestRow(layout, center));
        }

        var secondCenter = DamageTallyLayout.GetRowBounds(layout.SecondSection, 1).Center;
        Assert.Equal(
            new DamageTallyRowHit(1, 1),
            DamageTallyLayout.HitTestRow(layout, secondCenter));

        Assert.Null(DamageTallyLayout.HitTestRow(layout, layout.TitleBounds.Center));
        Assert.Null(DamageTallyLayout.HitTestRow(layout, layout.FirstSection.HeaderBounds.Center));
        Assert.Null(DamageTallyLayout.HitTestRow(layout, layout.FooterBounds.Center));
        Assert.Null(DamageTallyLayout.HitTestRow(layout, new Point(0, 0)));
    }

    [Fact]
    public void Calculate_InAnArenaShorterThanTheChromeDropsWhatDoesNotFit()
    {
        // 160 - 2 * 20 margin leaves 120 pixels; the full chrome needs 140.
        var layout = DamageTallyLayout.Calculate(new Rectangle(0, 0, 1280, 160), 11, 11);

        Assert.True(layout.TitleBounds.Height > 0);
        Assert.True(layout.FirstSection.HeaderBounds.Height > 0);
        Assert.Equal(0, layout.FirstSection.VisibleRowCount);
        Assert.Equal(0, layout.FooterBounds.Height);
        AssertInside(layout.Bounds, layout.FooterBounds);
    }

    [Fact]
    public void Calculate_InAnArenaTooShortForTheTitleDrawsNothing()
    {
        var layout = DamageTallyLayout.Calculate(new Rectangle(0, 0, 1280, 60), 11, 11);

        Assert.Equal(0, layout.TitleBounds.Height);
        Assert.Equal(0, layout.FirstSection.HeaderBounds.Height);
        Assert.Equal(0, layout.SecondSection.HeaderBounds.Height);
        Assert.Equal(0, layout.FooterBounds.Height);
    }

    [Fact]
    public void Panel_ExposesTheHoveredRowAndNullElsewhere()
    {
        var tally = SeededTally();
        var panel = new DamageTallyPanel();
        var arena = new Rectangle(0, 0, 1920, 1080);
        var layout = DamageTallyLayout.Calculate(arena, 2, 1);
        var firstRow = DamageTallyLayout.GetRowBounds(layout.FirstSection, 0).Center;
        var otherFaction = DamageTallyLayout.GetRowBounds(layout.SecondSection, 0).Center;
        panel.Tick(tally, TimeSpan.Zero);

        var interaction = panel.UpdatePointer(InputAt(firstRow), arena);

        Assert.True(interaction.PointerConsumed);
        Assert.Equal(
            new DamageTallyHover(0, WeaponId.Wasay, ShieldId.None),
            panel.Hovered);

        panel.UpdatePointer(InputAt(otherFaction), arena);
        Assert.Equal(
            new DamageTallyHover(1, WeaponId.Kampilan, ShieldId.None),
            panel.Hovered);

        var outside = panel.UpdatePointer(InputAt(new Point(5, 5)), arena);
        Assert.Null(panel.Hovered);
        Assert.False(outside.PointerConsumed);
    }

    [Fact]
    public void Panel_ClearPointerForgetsTheHoveredRow()
    {
        var panel = new DamageTallyPanel();
        var arena = new Rectangle(0, 0, 1920, 1080);
        var layout = DamageTallyLayout.Calculate(arena, 2, 1);
        panel.Tick(SeededTally(), TimeSpan.Zero);
        panel.UpdatePointer(
            InputAt(DamageTallyLayout.GetRowBounds(layout.FirstSection, 0).Center),
            arena);
        Assert.NotNull(panel.Hovered);

        panel.ClearPointer();

        Assert.Null(panel.Hovered);
    }

    [Fact]
    public void Panel_RefreshesItsNumbersOnlyEveryQuarterSecond()
    {
        var tally = SeededTally();
        var panel = new DamageTallyPanel();
        var arena = new Rectangle(0, 0, 1920, 1080);
        var layout = DamageTallyLayout.Calculate(arena, 2, 1);
        var topRow = InputAt(DamageTallyLayout.GetRowBounds(layout.FirstSection, 0).Center);
        panel.Tick(tally, TimeSpan.Zero);
        panel.UpdatePointer(topRow, arena);
        Assert.Equal(WeaponId.Wasay, panel.Hovered!.Value.Weapon);

        // Kalis overtakes Wasay in the live tally.
        tally.Ingest(
            [Landed(1, source: 3, target: 9, damage: 500, WeaponId.Kalis)],
            Roster());

        panel.Tick(tally, TimeSpan.FromMilliseconds(100));
        panel.UpdatePointer(topRow, arena);
        Assert.Equal(WeaponId.Wasay, panel.Hovered!.Value.Weapon);

        panel.Tick(tally, TimeSpan.FromMilliseconds(100));
        panel.UpdatePointer(topRow, arena);
        Assert.Equal(WeaponId.Wasay, panel.Hovered!.Value.Weapon);

        panel.Tick(tally, TimeSpan.FromMilliseconds(100));
        panel.UpdatePointer(topRow, arena);
        Assert.Equal(WeaponId.Kalis, panel.Hovered!.Value.Weapon);
    }

    [Fact]
    public void Panel_RefreshClockAdvancesWithNoPointerUpdateAtAll()
    {
        // A surface above the card consuming the pointer means UpdatePointer
        // is never called; the numbers must keep refreshing regardless.
        var tally = SeededTally();
        var panel = new DamageTallyPanel();
        panel.Tick(tally, TimeSpan.Zero);
        var before = panel.GetHeaderText(0);
        Assert.Contains("40 dealt", before, StringComparison.Ordinal);

        tally.Ingest(
            [Landed(1, source: 3, target: 9, damage: 500, WeaponId.Kalis)],
            Roster());
        panel.Tick(tally, TimeSpan.FromMilliseconds(200));
        Assert.Equal(before, panel.GetHeaderText(0));

        panel.Tick(tally, TimeSpan.FromMilliseconds(100));
        Assert.Contains("540 dealt", panel.GetHeaderText(0), StringComparison.Ordinal);
    }

    [Fact]
    public void Panel_RefreshRetakesTheSnapshotWithoutWaitingForTheClock()
    {
        var tally = SeededTally();
        var panel = new DamageTallyPanel();
        panel.Tick(tally, TimeSpan.Zero);

        tally.Reset();
        panel.Refresh(tally);

        Assert.Contains("0 dealt", panel.GetHeaderText(0), StringComparison.Ordinal);
    }

    [Fact]
    public void Panel_ShowsTheRosterOfASeededTallyBeforeAnyTick()
    {
        var tally = new WeaponDamageTally(
            CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV8));
        tally.SeedRoster(Roster());
        var panel = new DamageTallyPanel();
        var arena = new Rectangle(0, 0, 1920, 1080);
        var layout = DamageTallyLayout.Calculate(arena, 2, 1);

        panel.Tick(tally, TimeSpan.Zero);
        panel.UpdatePointer(
            InputAt(DamageTallyLayout.GetRowBounds(layout.FirstSection, 0).Center),
            arena);

        Assert.NotNull(panel.Hovered);
    }

    [Theory]
    [InlineData(0, "")]
    [InlineData(-3, "")]
    [InlineData(1, ".")]
    [InlineData(3, "...")]
    [InlineData(8, "Kampi...")]
    [InlineData(40, "Kampilan — Great Blade")]
    public void ClipLabel_FitsTheColumnWithNoFloor(int maximumCharacters, string expected)
    {
        var clipped = DamageTallyPanel.ClipLabel(
            "Kampilan — Great Blade",
            maximumCharacters);

        Assert.Equal(expected, clipped);
    }

    [Fact]
    public void Panel_ClipsEachLabelOncePerSnapshotAndWidth()
    {
        var panel = new DamageTallyPanel();
        panel.Tick(SeededTally(), TimeSpan.Zero);

        var first = panel.GetClippedLabel(0, 0, 6);
        var again = panel.GetClippedLabel(0, 0, 6);
        var wider = panel.GetClippedLabel(0, 0, 40);

        Assert.Same(first, again);
        Assert.True(first.Length <= 6);
        Assert.NotEqual(first, wider);
    }

    [Fact]
    public void Formatting_IgnoresTheCurrentCultureAndStaysInTheFontRange()
    {
        // A culture whose group separator, percent symbol, and percent pattern
        // all fall outside the bundled fonts, as fr-FR and de-DE do on a
        // machine with ICU. Built from the invariant culture so the test does
        // not depend on the host's culture data.
        var hostile = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        hostile.NumberFormat.NumberGroupSeparator = "\u202F";
        hostile.NumberFormat.NumberDecimalSeparator = ",";
        hostile.NumberFormat.PercentSymbol = "\u066A";
        hostile.NumberFormat.PercentPositivePattern = 2;

        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = hostile;

            Assert.Equal("1,234,567", DamageTallyPanel.FormatCount(1_234_567));
            Assert.Equal("50 %", DamageTallyPanel.FormatShare(1, 2));
            Assert.Equal("0 %", DamageTallyPanel.FormatShare(0, 0));
            Assert.All(
                DamageTallyPanel.FormatHeader(0, 1_234_567),
                character => Assert.True(character < 0x2000 || character == '\u2014'));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    private static WeaponDamageTally SeededTally()
    {
        var tally = new WeaponDamageTally(
            CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV8));
        tally.Ingest(
            [
                Landed(1, source: 1, target: 9, damage: 30, WeaponId.Wasay),
                Landed(2, source: 3, target: 9, damage: 10, WeaponId.Kalis),
            ],
            Roster());
        return tally;
    }

    private static AgentView[] Roster() =>
    [
        Agent(1, 0, WeaponId.Wasay),
        Agent(3, 0, WeaponId.Kalis),
        Agent(9, 1, WeaponId.Kampilan),
    ];

    private static AgentView Agent(ulong entityId, int faction, WeaponId weapon) =>
        new(
            entityId,
            faction,
            XRaw: 0,
            YRaw: 0,
            HitPoints: 100,
            MaximumHitPoints: 100,
            TargetEntityId: null,
            Intent: AgentIntent.Idle,
            IsAlive: true,
            Loadout: new CombatLoadout(weapon, ArmorId.None, ShieldId.None));

    private static BattleEvent Landed(
        long sequence,
        ulong source,
        ulong target,
        int damage,
        WeaponId weapon) =>
        BattleEvent.Attack(
            sequence,
            tick: 1,
            source,
            target,
            damage,
            factionId: 0,
            weapon,
            ShieldId.None,
            BodyPart.Chest);

    private static InputEdges InputAt(Point position)
    {
        var mouse = new MouseState(
            position.X,
            position.Y,
            0,
            ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released);
        return new InputEdges(default, default, mouse, mouse);
    }

    private static void AssertInside(Rectangle outer, Rectangle inner)
    {
        Assert.True(inner.Width >= 0);
        Assert.True(inner.Height >= 0);
        Assert.True(inner.Left >= outer.Left);
        Assert.True(inner.Top >= outer.Top);
        Assert.True(inner.Right <= outer.Right);
        Assert.True(inner.Bottom <= outer.Bottom);
    }

    private static void WithScale(UiScale scale, Action assertion)
    {
        try
        {
            UiScaleContext.Set(scale);
            assertion();
        }
        finally
        {
            UiScaleContext.Set(UiScale.Percent100);
        }
    }
}
