using System.Globalization;
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
/// card does not decide visibility: its owner calls <see cref="Tick"/> and
/// <see cref="Draw"/> only while the card is shown. The refresh clock and the
/// pointer are separate inputs: <see cref="Tick"/> runs every frame, whoever
/// owns the pointer, while <see cref="UpdatePointer"/> runs only when the card
/// is the topmost surface under it and <see cref="ClearPointer"/> says it is
/// not.
/// </remarks>
internal sealed class DamageTallyPanel
{
    internal static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(250);

    private const string Title = "DAMAGE TALLY";
    private const string Footer = "Damage after armour, includes overkill.";
    private const float BarOpacity = 0.35f;

    private readonly FactionSnapshot[] _factions =
    [
        new FactionSnapshot(0),
        new FactionSnapshot(1),
    ];

    private TimeSpan _sinceRefresh;
    private bool _hasSnapshot;
    private DamageTallyRowHit? _hoverRow;

    public Rectangle Bounds { get; private set; }

    /// <summary>
    /// The row under the pointer as of the last <see cref="UpdatePointer"/>,
    /// or <c>null</c> when the pointer is over no row or was cleared by
    /// <see cref="ClearPointer"/>.
    /// </summary>
    public DamageTallyHover? Hovered { get; private set; }

    /// <summary>
    /// Advances the refresh clock by <paramref name="elapsed"/> and re-takes
    /// the snapshot when it is due. Call it every frame the card is visible,
    /// independent of the pointer: a surface above the card consuming the
    /// pointer must not freeze the numbers.
    /// </summary>
    public void Tick(WeaponDamageTally tally, TimeSpan elapsed)
    {
        _sinceRefresh += elapsed;
        if (!_hasSnapshot || _sinceRefresh >= RefreshInterval)
        {
            Refresh(tally);
        }
    }

    /// <summary>
    /// Re-takes the snapshot now and restarts the refresh clock. For the
    /// moments the numbers must not be stale for even one interval: the card
    /// being switched on, and the tally being reset or replaced.
    /// </summary>
    public void Refresh(WeaponDamageTally tally)
    {
        foreach (var faction in _factions)
        {
            faction.Refresh(tally);
        }

        _sinceRefresh = TimeSpan.Zero;
        _hasSnapshot = true;
    }

    /// <summary>
    /// Hit-tests the pointer against the card. Call it only when no surface
    /// above the card consumed the pointer; otherwise call
    /// <see cref="ClearPointer"/>.
    /// </summary>
    public UiInteraction UpdatePointer(InputEdges input, Rectangle arenaContentBounds)
    {
        var layout = CalculateLayout(arenaContentBounds);
        Bounds = layout.Bounds;
        _hoverRow = DamageTallyLayout.HitTestRow(layout, input.MousePosition);
        Hovered = _hoverRow is { } hit
            ? _factions[hit.FactionId].GetHover(hit.RowIndex)
            : null;

        return new UiInteraction(ClientCommand.None, Bounds.Contains(input.MousePosition));
    }

    /// <summary>
    /// Forgets the pointer: no row keeps its hover fill and
    /// <see cref="Hovered"/> is <c>null</c>.
    /// </summary>
    public void ClearPointer()
    {
        _hoverRow = null;
        Hovered = null;
    }

    internal string GetHeaderText(int factionId) => _factions[factionId].HeaderText;

    /// <summary>
    /// The label of a row clipped to <paramref name="maximumCharacters"/>. The
    /// clipped string is built once per snapshot and width, then reused, so a
    /// frame that draws the same rows at the same width allocates nothing.
    /// </summary>
    internal string GetClippedLabel(int factionId, int rowIndex, int maximumCharacters) =>
        _factions[factionId].GetClippedLabel(rowIndex, maximumCharacters);

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

        if (layout.TitleBounds.Height > 0)
        {
            UiPrimitives.DrawText(
                spriteBatch,
                fonts.Get(UiFontRole.Body),
                Title,
                new Vector2(layout.TitleBounds.Left, layout.TitleBounds.Top),
                theme.Colors.TextPrimary);
        }

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

        if (layout.FooterBounds.Height > 0)
        {
            UiPrimitives.DrawText(
                spriteBatch,
                fonts.Get(UiFontRole.Caption),
                Footer,
                new Vector2(layout.FooterBounds.Left, layout.FooterBounds.Top),
                theme.Colors.TextSecondary);
        }
    }

    private DamageTallyPanelLayout CalculateLayout(Rectangle arenaContentBounds) =>
        DamageTallyLayout.Calculate(
            arenaContentBounds,
            _factions[0].Count,
            _factions[1].Count);

    private void DrawSection(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        UiFontSet fonts,
        FactionSnapshot faction,
        DamageTallySectionLayout section,
        UiTheme theme)
    {
        if (section.HeaderBounds.Height == 0)
        {
            return;
        }

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

        if (_hoverRow is { } hover &&
            hover.FactionId == faction.FactionId &&
            hover.RowIndex == index)
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
        var maximumCharacters =
            columns.Label.Width / fonts.GetApproximateAdvancePx(UiFontRole.Caption);
        DrawLeft(
            spriteBatch,
            captionFont,
            faction.GetClippedLabel(index, maximumCharacters),
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

    internal static string FormatCount(long value) =>
        value.ToString("N0", CultureInfo.InvariantCulture);

    internal static string FormatShare(long damage, long total) =>
        (total == 0 ? 0d : (double)damage / total)
            .ToString("P0", CultureInfo.InvariantCulture);

    internal static string FormatHeader(int factionId, long total) =>
        $"{BattleEventFormatter.GetFactionLabel(factionId)} — {FormatCount(total)} dealt";

    internal static string ClipLabel(string label, int maximumCharacters)
    {
        const string Ellipsis = "...";

        if (maximumCharacters <= 0)
        {
            return string.Empty;
        }

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

        private readonly string?[] _clippedLabels =
            new string?[WeaponDamageTally.MaximumRowsPerFaction];

        // The width each cached label was clipped for; -1 is "not clipped
        // since the last refresh".
        private readonly int[] _clippedFor = CreateUnclipped();

        public DamageTallyHover GetHover(int rowIndex)
        {
            var row = Rows[rowIndex];
            return new DamageTallyHover(row.FactionId, row.Weapon, row.Shield);
        }

        public string GetClippedLabel(int index, int maximumCharacters)
        {
            if (_clippedFor[index] != maximumCharacters)
            {
                _clippedLabels[index] = ClipLabel(Rows[index].Label, maximumCharacters);
                _clippedFor[index] = maximumCharacters;
            }

            return _clippedLabels[index]!;
        }

        public void Refresh(WeaponDamageTally tally)
        {
            Count = tally.GetRows(FactionId, Rows);
            var total = tally.GetFactionDamage(FactionId);
            var top = Count > 0 ? Rows[0].Damage : 0;
            HeaderText = FormatHeader(FactionId, total);
            Array.Fill(_clippedFor, -1);

            for (var index = 0; index < Count; index++)
            {
                var row = Rows[index];
                Text[index] = new RowText(
                    FormatCount(row.Damage),
                    FormatShare(row.Damage, total),
                    FormatCount(row.BlowsLanded),
                    $"{row.Alive} / {row.Fielded}",
                    top == 0 ? 0f : (float)row.Damage / top);
            }
        }

        private static int[] CreateUnclipped()
        {
            var unclipped = new int[WeaponDamageTally.MaximumRowsPerFaction];
            Array.Fill(unclipped, -1);
            return unclipped;
        }
    }
}
