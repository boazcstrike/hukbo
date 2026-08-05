using Hukbo.Client.Presentation;
using Hukbo.Client.Settings;
using Hukbo.Client.Theming;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hukbo.Client.UI;

/// <summary>
/// Post-battle per-unit statistics panel. Reached from
/// <see cref="MatchSummaryPanel"/>'s "BATTLE REPORT" button and closed by
/// its own close button, both of which send
/// <see cref="ClientCommand.ToggleBattleReport"/> — the same command flips
/// the panel open and shut, mirroring the toggle already used for the sound
/// log. It only ever draws once a <see cref="BattleReport"/> snapshot
/// exists, matching <see cref="MatchSummaryPanel"/>'s own strictly
/// post-battle lifecycle. All geometry comes from
/// <see cref="BattleReportLayout.Calculate"/>; this class adds only scroll
/// state and pointer-hover tracking on top of it.
/// </summary>
internal sealed class BattleReportPanel
{
    private const int RowsPerWheelDetent = 3;
    private const int MouseWheelDeltaPerDetent = 120;

    // Two classes of number appear in this panel and the difference matters.
    // Attack counts and accuracy are the simulation's own per-faction
    // counters, summed tick by tick, and cannot disagree with what happened.
    // Kills, damage, survivors, and every per-warrior row rest on a
    // kill-credit heuristic, because Hukbo.Core tracks no per-entity counters
    // — see BattleReportAccumulator's own remarks. This line always draws,
    // regardless of which optional highlight lines are present, so a
    // spectator never mistakes the second group for the first.
    private const string KillCreditDisclosure =
        "Attacks and accuracy are simulation-reported. Kills, damage and " +
        "warrior rows are estimated from the last landed blow.";

    private int _scrollStart;
    private Point _pointerPosition;
    private readonly UiEntranceMotion _entrance = new();
    private bool _wasVisible;

    public Rectangle Bounds { get; private set; }

    internal float EntranceOpacity => _entrance.PanelOpacity;

    public UiInteraction Update(
        InputEdges input,
        BattleReport? report,
        Rectangle arenaContentBounds) =>
        Update(
            input,
            report,
            arenaContentBounds,
            TimeSpan.Zero,
            MotionIntensity.Off);

    public UiInteraction Update(
        InputEdges input,
        BattleReport? report,
        Rectangle arenaContentBounds,
        TimeSpan elapsed,
        MotionIntensity motionIntensity)
    {
        _pointerPosition = input.MousePosition;

        if (report is null)
        {
            Bounds = Rectangle.Empty;
            _scrollStart = 0;
            _wasVisible = false;
            _entrance.Reset();
            return UiInteraction.None;
        }

        if (!_wasVisible)
        {
            _wasVisible = true;
            _entrance.Begin();
        }

        _entrance.Advance(
            elapsed,
            motionIntensity,
            UiEntranceMotion.ReportPanelDuration,
            hasScrim: false);
        var layout = BattleReportLayout.Calculate(arenaContentBounds);
        Bounds = layout.Bounds;
        var pointerInside = Bounds.Contains(input.MousePosition);
        var visibleRowCount = BattleReportLayout.GetVisibleRowCount(layout);
        ClampScroll(report.Leaderboard.Count, visibleRowCount);

        if (input.WasLeftMousePressed() && pointerInside)
        {
            if (layout.CloseButtonBounds.Contains(input.MousePosition))
            {
                return new UiInteraction(
                    ClientCommand.ToggleBattleReport,
                    true);
            }

            if (layout.ScrollbarBounds.Contains(input.MousePosition))
            {
                PageFromScrollbar(
                    layout,
                    report.Leaderboard.Count,
                    visibleRowCount,
                    input.MousePosition);
            }
        }

        if (pointerInside && input.ScrollWheelDelta != 0)
        {
            var detents = Math.Max(
                1,
                Math.Abs(input.ScrollWheelDelta) / MouseWheelDeltaPerDetent);
            var direction = input.ScrollWheelDelta > 0 ? -1 : 1;
            Scroll(
                direction * detents * RowsPerWheelDetent,
                report.Leaderboard.Count,
                visibleRowCount);
        }

        return new UiInteraction(ClientCommand.None, pointerInside);
    }

    /// <param name="scenarioSeed">
    /// The match's <c>Scenario.Seed</c>, one of the three inputs
    /// <see cref="WarriorNames.Resolve"/> derives each named warrior's
    /// personal name from. Presentation-only: names are derived from the
    /// identity the simulation published, and no name reaches the report's
    /// own numbers.
    /// </param>
    public void Draw(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        UiFontSet fonts,
        BattleReport? report,
        Rectangle arenaContentBounds,
        ulong scenarioSeed,
        UiTheme theme)
    {
        if (report is null)
        {
            Bounds = Rectangle.Empty;
            return;
        }

        var layout = BattleReportLayout.Calculate(arenaContentBounds);
        Bounds = layout.Bounds;
        theme = UiMotionTheme.WithOpacity(theme, EntranceOpacity);
        var visibleRowCount = BattleReportLayout.GetVisibleRowCount(layout);
        ClampScroll(report.Leaderboard.Count, visibleRowCount);

        spriteBatch.Draw(pixel, Bounds, theme.Colors.PanelSurface);
        UiPrimitives.DrawBorder(
            spriteBatch,
            pixel,
            Bounds,
            theme.Colors.PanelBorder,
            Math.Max(
                UiScaleContext.Pixels(3),
                UiScaleContext.Pixels(theme.Metrics.BorderThickness)));

        DrawHeader(spriteBatch, pixel, fonts, layout, theme);
        DrawFactionTotals(spriteBatch, fonts, report, layout, scenarioSeed, theme);
        DrawHighlights(spriteBatch, fonts, report, layout, scenarioSeed, theme);
        DrawLeaderboardHeader(spriteBatch, fonts, layout, theme);
        DrawLeaderboardRows(
            spriteBatch,
            pixel,
            fonts,
            report,
            layout,
            visibleRowCount,
            scenarioSeed,
            theme);
        DrawScrollbar(
            spriteBatch,
            pixel,
            report.Leaderboard.Count,
            layout,
            visibleRowCount,
            theme);
    }

    private void DrawScrollbar(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        int totalRowCount,
        BattleReportPanelLayout layout,
        int visibleRowCount,
        UiTheme theme)
    {
        spriteBatch.Draw(
            pixel,
            layout.ScrollbarBounds,
            theme.Colors.CanvasBackground);
        var thumb = BattleEventLogPanel.GetScrollbarThumb(
            layout.ScrollbarBounds,
            totalRowCount,
            visibleRowCount,
            _scrollStart);
        spriteBatch.Draw(pixel, thumb, theme.Colors.ActionDefault);
    }

    private void DrawHeader(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        UiFontSet fonts,
        BattleReportPanelLayout layout,
        UiTheme theme)
    {
        UiPrimitives.DrawText(
            spriteBatch,
            fonts.Get(UiFontRole.Title),
            "BATTLE REPORT",
            new Vector2(layout.HeaderBounds.Left, layout.HeaderBounds.Top),
            theme.Colors.TextPrimary);

        var isHovered = layout.CloseButtonBounds.Contains(_pointerPosition);
        spriteBatch.Draw(
            pixel,
            layout.CloseButtonBounds,
            isHovered ? theme.Colors.ActionHover : theme.Colors.ActionDefault);
        UiPrimitives.DrawBorder(
            spriteBatch,
            pixel,
            layout.CloseButtonBounds,
            theme.Colors.PanelBorder,
            UiScaleContext.Pixels(1));
        UiPrimitives.DrawCenteredText(
            spriteBatch,
            fonts.Get(UiFontRole.Label),
            "X",
            layout.CloseButtonBounds.Center.ToVector2(),
            theme.Colors.TextInverse);
    }

    private static void DrawFactionTotals(
        SpriteBatch spriteBatch,
        UiFontSet fonts,
        BattleReport report,
        BattleReportPanelLayout layout,
        ulong scenarioSeed,
        UiTheme theme)
    {
        var bodyFont = fonts.Get(UiFontRole.Body);
        for (var index = 0;
            index < report.Factions.Count && index < 2;
            index++)
        {
            var faction = report.Factions[index];
            var factionColor = FactionColorPalette.GetThemeColor(
                faction.FactionId,
                theme,
                theme.Colors.TextPrimary);
            var topKiller = faction.TopKillerEntityId is { } topKillerId
                ? " ~top " +
                    WarriorNames.FormatWarrior(
                        topKillerId,
                        faction.FactionId,
                        scenarioSeed) +
                    $" ({faction.TopKillerKills})"
                : string.Empty;
            // Authoritative figures first, then the estimated ones. The
            // ordering is deliberate: it puts the numbers the simulation
            // vouches for ahead of the numbers this panel inferred, and the
            // disclosure line below names which is which.
            var line =
                $"{BattleEventFormatter.GetFactionLabel(faction.FactionId)} " +
                $"— Atk {faction.Combat.AcceptedAttacks} " +
                $"Hit {faction.Combat.LandedAttacks} " +
                $"Acc {faction.Accuracy:P0}  ~Kills {faction.TotalKills} " +
                $"~Dmg {faction.TotalDamageDealt} " +
                $"Alive {faction.Survivors}" +
                topKiller;
            UiPrimitives.DrawText(
                spriteBatch,
                bodyFont,
                line,
                new Vector2(
                    layout.FactionTotalsBounds.Left,
                    layout.FactionTotalsBounds.Top +
                    (index * UiScaleContext.Pixels(24))),
                factionColor);
        }
    }

    private static void DrawHighlights(
        SpriteBatch spriteBatch,
        UiFontSet fonts,
        BattleReport report,
        BattleReportPanelLayout layout,
        ulong scenarioSeed,
        UiTheme theme)
    {
        var captionFont = fonts.Get(UiFontRole.Caption);
        var lines = new List<string>(BattleReportLayout.HighlightsLineCount);

        if (report.FirstBlood is { } firstBlood)
        {
            lines.Add(
                $"First blood — {FormatHighlight(firstBlood, scenarioSeed)}");
        }

        if (report.DecisiveKill is { } decisiveKill)
        {
            lines.Add(
                $"Decisive kill — {FormatHighlight(decisiveKill, scenarioSeed)}");
        }

        if (report.LongestSurvivor is { } longestSurvivor)
        {
            lines.Add(
                "Longest survivor — " +
                $"{BattleEventFormatter.GetFactionLabel(longestSurvivor.FactionId)} " +
                WarriorNames.FormatWarrior(
                    longestSurvivor.EntityId,
                    longestSurvivor.FactionId,
                    scenarioSeed) +
                $" held out until tick {longestSurvivor.DeathTick:N0}.");
        }

        lines.Add(KillCreditDisclosure);

        for (var index = 0;
            index < lines.Count && index < BattleReportLayout.HighlightsLineCount;
            index++)
        {
            UiPrimitives.DrawText(
                spriteBatch,
                captionFont,
                lines[index],
                new Vector2(
                    layout.HighlightsBounds.Left,
                    layout.HighlightsBounds.Top +
                    (index * UiScaleContext.Pixels(20))),
                theme.Colors.TextSecondary);
        }
    }

    private static string FormatHighlight(
        BattleAttackHighlight highlight,
        ulong scenarioSeed) =>
        $"{BattleEventFormatter.GetFactionLabel(highlight.AttackerFactionId)} " +
        WarriorNames.FormatWarrior(
            highlight.AttackerEntityId,
            highlight.AttackerFactionId,
            scenarioSeed) +
        " struck " +
        $"{BattleEventFormatter.GetFactionLabel(highlight.VictimFactionId)} " +
        WarriorNames.FormatWarrior(
            highlight.VictimEntityId,
            highlight.VictimFactionId,
            scenarioSeed) +
        $" for {highlight.Value} with " +
        $"{BattleEventFormatter.GetWeaponLabel(highlight.Weapon, highlight.Shield)} " +
        $"(tick {highlight.Tick:N0}).";

    private static void DrawLeaderboardHeader(
        SpriteBatch spriteBatch,
        UiFontSet fonts,
        BattleReportPanelLayout layout,
        UiTheme theme)
    {
        UiPrimitives.DrawText(
            spriteBatch,
            fonts.Get(UiFontRole.Caption),
            "WARRIOR LEADERBOARD",
            new Vector2(
                layout.LeaderboardHeaderBounds.Left,
                layout.LeaderboardHeaderBounds.Top),
            theme.Colors.TextDisabled);
    }

    private void DrawLeaderboardRows(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        UiFontSet fonts,
        BattleReport report,
        BattleReportPanelLayout layout,
        int visibleRowCount,
        ulong scenarioSeed,
        UiTheme theme)
    {
        var captionFont = fonts.Get(UiFontRole.Caption);
        var rowWidth = layout.LeaderboardListBounds.Width;
        var maximumCharacters = Math.Max(
            8,
            rowWidth / fonts.GetApproximateAdvancePx(UiFontRole.Caption));

        for (var offset = 0; offset < visibleRowCount; offset++)
        {
            var index = _scrollStart + offset;
            if (index >= report.Leaderboard.Count)
            {
                break;
            }

            var row = report.Leaderboard[index];
            var rowBounds = new Rectangle(
                layout.LeaderboardListBounds.Left,
                layout.LeaderboardListBounds.Top + (offset * BattleReportLayout.RowHeight),
                layout.LeaderboardListBounds.Width,
                BattleReportLayout.RowHeight);

            if (rowBounds.Contains(_pointerPosition))
            {
                spriteBatch.Draw(pixel, rowBounds, theme.Colors.ActionHover);
            }

            var factionColor = FactionColorPalette.GetThemeColor(
                row.FactionId,
                theme,
                theme.Colors.TextPrimary);
            spriteBatch.Draw(
                pixel,
                new Rectangle(
                    rowBounds.Left,
                    rowBounds.Top,
                    Math.Min(UiScaleContext.Pixels(4), rowBounds.Width),
                    rowBounds.Height),
                factionColor);

            UiPrimitives.DrawText(
                spriteBatch,
                captionFont,
                ClipRow(FormatRow(row, scenarioSeed), maximumCharacters),
                new Vector2(
                    rowBounds.Left + UiScaleContext.Pixels(10),
                    rowBounds.Top + UiScaleContext.Pixels(6)),
                theme.Colors.TextPrimary);
        }
    }

    private static string FormatRow(UnitReportRow row, ulong scenarioSeed)
    {
        var weaponLabel = row.Weapon is { } weapon && row.Shield is { } shield
            ? $"  {BattleEventFormatter.GetWeaponLabel(weapon, shield)}"
            : string.Empty;

        return $"{row.Rank}. " +
            $"{BattleEventFormatter.GetFactionLabel(row.FactionId)} " +
            WarriorNames.FormatWarrior(
                row.EntityId,
                row.FactionId,
                scenarioSeed) +
            $"  K{row.Kills} Dmg {row.DamageDealt}/{row.DamageTaken}  " +
            $"Acc {row.Accuracy:P0}" +
            weaponLabel;
    }

    private static string ClipRow(string text, int maximumCharacters)
    {
        const string Ellipsis = "...";

        if (maximumCharacters <= 0)
        {
            return string.Empty;
        }

        if (text.Length <= maximumCharacters)
        {
            return text;
        }

        if (maximumCharacters <= Ellipsis.Length)
        {
            return Ellipsis[..maximumCharacters];
        }

        return string.Concat(
            text.AsSpan(0, maximumCharacters - Ellipsis.Length),
            Ellipsis);
    }

    private void ClampScroll(int totalRowCount, int visibleRowCount)
    {
        var maximumStart = Math.Max(0, totalRowCount - visibleRowCount);
        _scrollStart = Math.Clamp(_scrollStart, 0, maximumStart);
    }

    private void Scroll(int delta, int totalRowCount, int visibleRowCount)
    {
        var maximumStart = Math.Max(0, totalRowCount - visibleRowCount);
        _scrollStart = Math.Clamp(_scrollStart + delta, 0, maximumStart);
    }

    private void PageFromScrollbar(
        BattleReportPanelLayout layout,
        int totalRowCount,
        int visibleRowCount,
        Point pointerPosition)
    {
        var thumb = BattleEventLogPanel.GetScrollbarThumb(
            layout.ScrollbarBounds,
            totalRowCount,
            visibleRowCount,
            _scrollStart);
        if (pointerPosition.Y < thumb.Top)
        {
            Scroll(-Math.Max(1, visibleRowCount), totalRowCount, visibleRowCount);
        }
        else if (pointerPosition.Y >= thumb.Bottom)
        {
            Scroll(Math.Max(1, visibleRowCount), totalRowCount, visibleRowCount);
        }
    }
}
