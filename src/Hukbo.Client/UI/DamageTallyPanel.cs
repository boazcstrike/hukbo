using Hukbo.Client.Presentation;
using Hukbo.Client.Settings;
using Hukbo.Client.Theming;
using Hukbo.Core.Combat;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hukbo.Client.UI;

/// <summary>
/// The weapon, shield, and faction of the row the pointer is over. The
/// renderer rings every living agent that matches it.
/// </summary>
internal readonly record struct DamageTallyHover(
    int FactionId,
    WeaponId Weapon,
    ShieldId Shield);

/// <summary>
/// The damage card: per faction, one row per weapon and shield with a bar, the
/// damage dealt after armour, its share of the faction's total, blows landed,
/// and living count. All geometry comes from <see cref="DamageTallyLayout"/>;
/// this class adds the 0.25 s refresh and hover tracking on top of it.
/// </summary>
/// <remarks>
/// The numbers it draws are a snapshot of a <see cref="WeaponDamageTally"/>
/// taken at most every <see cref="RefreshInterval"/> of wall time, so they
/// stay readable at 4x playback and <see cref="Draw"/> formats nothing. The
/// card does not decide visibility: its owner calls <see cref="Update"/> and
/// <see cref="Draw"/> only while the card is shown.
/// </remarks>
internal sealed class DamageTallyPanel
{
    internal static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(250);

    private const string Title = "DAMAGE TALLY";
    private const string Footer = "Damage after armour, includes overkill.";
    private const float BarOpacity = 0.35f;
    private const int MinimumLabelCharacters = 4;

    private readonly FactionSnapshot[] _factions =
    [
        new FactionSnapshot(0),
        new FactionSnapshot(1),
    ];

    private TimeSpan _sinceRefresh;
    private bool _hasSnapshot;
    private Point _pointerPosition;

    public Rectangle Bounds { get; private set; }

    /// <summary>
    /// The row under the pointer as of the last <see cref="Update"/>, or
    /// <c>null</c> when the pointer is over no row.
    /// </summary>
    public DamageTallyHover? Hovered { get; private set; }

    public UiInteraction Update(
        InputEdges input,
        WeaponDamageTally tally,
        Rectangle arenaContentBounds,
        TimeSpan elapsed)
    {
        _pointerPosition = input.MousePosition;
        _sinceRefresh += elapsed;
        if (!_hasSnapshot || _sinceRefresh >= RefreshInterval)
        {
            Refresh(tally);
        }

        var layout = CalculateLayout(arenaContentBounds);
        Bounds = layout.Bounds;
        Hovered = DamageTallyLayout.HitTestRow(layout, _pointerPosition) is { } hit
            ? _factions[hit.FactionId].GetHover(hit.RowIndex)
            : null;

        return new UiInteraction(ClientCommand.None, Bounds.Contains(_pointerPosition));
    }

    public void Draw(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        UiFontSet fonts,
        Rectangle arenaContentBounds,
        UiTheme theme)
    {
        var layout = CalculateLayout(arenaContentBounds);
        Bounds = layout.Bounds;

        spriteBatch.Draw(pixel, Bounds, theme.Colors.PanelSurface);
        UiPrimitives.DrawBorder(
            spriteBatch,
            pixel,
            Bounds,
            theme.Colors.PanelBorder,
            Math.Max(
                UiScaleContext.Pixels(1),
                UiScaleContext.Pixels(theme.Metrics.BorderThickness)));

        UiPrimitives.DrawText(
            spriteBatch,
            fonts.Get(UiFontRole.Body),
            Title,
            new Vector2(layout.TitleBounds.Left, layout.TitleBounds.Top),
            theme.Colors.TextPrimary);

        for (var factionId = 0; factionId < _factions.Length; factionId++)
        {
            DrawSection(
                spriteBatch,
                pixel,
                fonts,
                _factions[factionId],
                layout.GetSection(factionId),
                theme);
        }

        UiPrimitives.DrawText(
            spriteBatch,
            fonts.Get(UiFontRole.Caption),
            Footer,
            new Vector2(layout.FooterBounds.Left, layout.FooterBounds.Top),
            theme.Colors.TextSecondary);
    }

    private DamageTallyPanelLayout CalculateLayout(Rectangle arenaContentBounds) =>
        DamageTallyLayout.Calculate(
            arenaContentBounds,
            _factions[0].Count,
            _factions[1].Count);

    private void Refresh(WeaponDamageTally tally)
    {
        foreach (var faction in _factions)
        {
            faction.Refresh(tally);
        }

        _sinceRefresh = TimeSpan.Zero;
        _hasSnapshot = true;
    }

    private void DrawSection(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        UiFontSet fonts,
        FactionSnapshot faction,
        DamageTallySectionLayout section,
        UiTheme theme)
    {
        var captionFont = fonts.Get(UiFontRole.Caption);
        var factionColor = FactionColorPalette.GetThemeColor(
            faction.FactionId,
            theme,
            theme.Colors.TextPrimary);

        var headerColumns = DamageTallyLayout.GetRowColumns(section.HeaderBounds);
        DrawLeft(spriteBatch, captionFont, faction.HeaderText, headerColumns.Label, factionColor);
        DrawRight(spriteBatch, captionFont, "DAMAGE", headerColumns.Damage, theme.Colors.TextDisabled);
        DrawRight(spriteBatch, captionFont, "SHARE", headerColumns.Share, theme.Colors.TextDisabled);
        DrawRight(spriteBatch, captionFont, "BLOWS", headerColumns.Blows, theme.Colors.TextDisabled);
        DrawRight(spriteBatch, captionFont, "ALIVE", headerColumns.Alive, theme.Colors.TextDisabled);

        for (var index = 0; index < section.VisibleRowCount; index++)
        {
            DrawRow(
                spriteBatch,
                pixel,
                fonts,
                faction,
                index,
                DamageTallyLayout.GetRowBounds(section, index),
                factionColor,
                theme);
        }
    }

    private void DrawRow(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        UiFontSet fonts,
        FactionSnapshot faction,
        int index,
        Rectangle rowBounds,
        Color factionColor,
        UiTheme theme)
    {
        var captionFont = fonts.Get(UiFontRole.Caption);
        var row = faction.Rows[index];
        var text = faction.Text[index];

        if (rowBounds.Contains(_pointerPosition))
        {
            spriteBatch.Draw(pixel, rowBounds, theme.Colors.ActionHover);
        }

        spriteBatch.Draw(
            pixel,
            DamageTallyLayout.GetBarBounds(rowBounds, text.BarFraction),
            factionColor * BarOpacity);

        // A faction's last warrior of a type has fallen: the row stays, in the
        // disabled text role, and its "0 / n" says why.
        var textColor = row.Alive == 0
            ? theme.Colors.TextDisabled
            : theme.Colors.TextPrimary;
        var columns = DamageTallyLayout.GetRowColumns(rowBounds);
        var maximumCharacters = Math.Max(
            MinimumLabelCharacters,
            columns.Label.Width / fonts.GetApproximateAdvancePx(UiFontRole.Caption));
        DrawLeft(
            spriteBatch,
            captionFont,
            ClipLabel(row.Label, maximumCharacters),
            columns.Label,
            textColor);
        DrawRight(spriteBatch, captionFont, text.Damage, columns.Damage, textColor);
        DrawRight(spriteBatch, captionFont, text.Share, columns.Share, textColor);
        DrawRight(spriteBatch, captionFont, text.Blows, columns.Blows, textColor);
        DrawRight(spriteBatch, captionFont, text.Alive, columns.Alive, textColor);
    }

    private static void DrawLeft(
        SpriteBatch spriteBatch,
        SpriteFont font,
        string text,
        Rectangle column,
        Color color) =>
        UiPrimitives.DrawText(
            spriteBatch,
            font,
            text,
            new Vector2(column.Left, column.Top + VerticalInset(font, column)),
            color);

    private static void DrawRight(
        SpriteBatch spriteBatch,
        SpriteFont font,
        string text,
        Rectangle column,
        Color color) =>
        UiPrimitives.DrawText(
            spriteBatch,
            font,
            text,
            new Vector2(
                column.Right - font.MeasureString(text).X,
                column.Top + VerticalInset(font, column)),
            color);

    private static int VerticalInset(SpriteFont font, Rectangle column) =>
        Math.Max(0, (column.Height - font.LineSpacing) / 2);

    private static string ClipLabel(string label, int maximumCharacters)
    {
        const string Ellipsis = "...";

        if (label.Length <= maximumCharacters)
        {
            return label;
        }

        if (maximumCharacters <= Ellipsis.Length)
        {
            return Ellipsis[..maximumCharacters];
        }

        return string.Concat(
            label.AsSpan(0, maximumCharacters - Ellipsis.Length),
            Ellipsis);
    }

    private readonly record struct RowText(
        string Damage,
        string Share,
        string Blows,
        string Alive,
        float BarFraction);

    /// <summary>
    /// One faction's rows as of the last refresh, with their numbers already
    /// formatted. The buffers are allocated once; only the strings are new.
    /// </summary>
    private sealed class FactionSnapshot(int factionId)
    {
        public int FactionId { get; } = factionId;

        public int Count { get; private set; }

        public string HeaderText { get; private set; } = string.Empty;

        public WeaponDamageTallyRow[] Rows { get; } =
            new WeaponDamageTallyRow[WeaponDamageTally.MaximumRowsPerFaction];

        public RowText[] Text { get; } =
            new RowText[WeaponDamageTally.MaximumRowsPerFaction];

        public DamageTallyHover GetHover(int rowIndex)
        {
            var row = Rows[rowIndex];
            return new DamageTallyHover(row.FactionId, row.Weapon, row.Shield);
        }

        public void Refresh(WeaponDamageTally tally)
        {
            Count = tally.GetRows(FactionId, Rows);
            var total = tally.GetFactionDamage(FactionId);
            var top = Count > 0 ? Rows[0].Damage : 0;
            HeaderText =
                $"{BattleEventFormatter.GetFactionLabel(FactionId)} — {total:N0} dealt";

            for (var index = 0; index < Count; index++)
            {
                var row = Rows[index];
                Text[index] = new RowText(
                    row.Damage.ToString("N0"),
                    (total == 0 ? 0d : (double)row.Damage / total).ToString("P0"),
                    row.BlowsLanded.ToString("N0"),
                    $"{row.Alive} / {row.Fielded}",
                    top == 0 ? 0f : (float)row.Damage / top);
            }
        }
    }
}
