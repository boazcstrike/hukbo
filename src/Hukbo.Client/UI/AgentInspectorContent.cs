using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.UI;

/// <summary>
/// Pure layout and text helpers for <see cref="AgentInspectorPanel"/>.
/// Holds every constant and calculation the panel needs so its geometry
/// can be unit tested without a <c>SpriteBatch</c>, <c>GraphicsDevice</c>,
/// or window. See docs/plans and the hukbo-client-ui skill for the
/// pure-helper / untestable-Draw split this repo enforces.
/// </summary>
internal static class AgentInspectorContent
{
    internal const int Padding = 14;
    internal const int AccentWidth = 5;
    internal const int PortraitSize = 56;
    internal const int PortraitGap = 10;
    internal const int LineHeight = 19;
    internal const int TitleHeight = 31;
    internal const int TopDetailRowCount = 4;
    internal const int LowerRowCount = 7;
    internal const int PortraitBottomGap = 5;
    internal const int TopDetailBottomGap = 2;

    /// <summary>
    /// Wrapped-line budget reserved for the evidence note when sizing the
    /// panel. The longest known evidence string ("PROVISIONAL: comparable
    /// to Spanish-era accounts of the kampilan.", 64 characters) wraps to
    /// 2 lines at the panel's ~277px text width budget (InspectorWidth
    /// 310 minus Padding*2 minus AccentWidth) using the Default.spritefont
    /// Arial-18 face drawn at the panel's 0.64 detail-text scale (roughly
    /// 5-6px average advance per character at that size and scale). This
    /// is a sizing estimate, not a hard limit — <see cref="AgentInspectorPanel"/>
    /// additionally refuses to draw any wrapped line that would fall
    /// past the panel bounds, so an under-estimate here can only drop a
    /// line, never overflow the panel.
    /// </summary>
    internal const int EvidenceReservedLineCount = 2;

    /// <summary>
    /// Available pixel width for detail and evidence text inside a panel
    /// of the given total width.
    /// </summary>
    internal static int ComputeContentWidthBudget(int panelWidth) =>
        Math.Max(0, panelWidth - (Padding * 2) - AccentWidth);

    /// <summary>
    /// Panel height needed so the deepest row — including up to
    /// <paramref name="evidenceLineCount"/> wrapped evidence lines —
    /// still fits above the bottom padding. Mirrors the exact row
    /// arithmetic <see cref="AgentInspectorPanel.Draw"/> uses.
    /// </summary>
    internal static int ComputeRequiredHeight(int evidenceLineCount)
    {
        var textY = Padding + TitleHeight;
        var portraitBottom = textY + PortraitSize;
        var lowerTextY = Math.Max(
            portraitBottom + PortraitBottomGap,
            textY + (TopDetailRowCount * LineHeight) + TopDetailBottomGap);
        var lowerRowCount = LowerRowCount + Math.Max(0, evidenceLineCount);
        var lastRowY = lowerTextY + ((lowerRowCount - 1) * LineHeight);
        var lastRowBottom = lastRowY + LineHeight;
        return lastRowBottom + Padding;
    }

    /// <summary>
    /// The spectator's explanation of what collision did to this agent's
    /// movement this tick. Reads the authoritative
    /// <see cref="MovementResolution"/> the simulation wrote; presentation
    /// never infers it from positions.
    /// </summary>
    internal static string FormatMovementLine(MovementResolution resolution) =>
        $"Movement: {GetMovementLabel(resolution)}";

    internal static string GetMovementLabel(MovementResolution resolution) =>
        resolution switch
        {
            MovementResolution.Moved => "Moving",
            MovementResolution.Truncated => "Crowded",
            MovementResolution.Slid => "Sliding",
            MovementResolution.Blocked => "Blocked",
            MovementResolution.Separated => "Pushed apart",
            _ => "Holding",
        };

    internal static string FormatWeaponLine(string weaponLabel) =>
        $"Weapon: {weaponLabel}";

    internal static string FormatArmorLine(ArmorId armor) =>
        $"Armor: {GetArmorLabel(armor)}";

    internal static string FormatShieldLine(ShieldId shield) =>
        $"Shield: {GetShieldLabel(shield)}";

    internal static string GetArmorLabel(ArmorId armor) =>
        armor switch
        {
            ArmorId.LightOrganic => "Light Organic",
            _ => throw new ArgumentOutOfRangeException(
                nameof(armor),
                armor,
                null),
        };

    internal static string GetShieldLabel(ShieldId shield) =>
        shield switch
        {
            ShieldId.None => "None",
            ShieldId.TallHardwood => "Tall Hardwood",
            _ => throw new ArgumentOutOfRangeException(
                nameof(shield),
                shield,
                null),
        };

    /// <summary>
    /// Greedy word-wraps <paramref name="text"/> so no returned line
    /// measures wider than <paramref name="maxWidthPx"/> according to
    /// <paramref name="measureWidth"/>. Returns an empty list for a null
    /// or empty <paramref name="text"/>. A single word wider than the
    /// budget is hard-split at the character level so the width
    /// invariant always holds.
    /// </summary>
    internal static IReadOnlyList<string> WrapText(
        string? text,
        float maxWidthPx,
        Func<string, float> measureWidth)
    {
        ArgumentNullException.ThrowIfNull(measureWidth);
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var lines = new List<string>();
        var currentLine = string.Empty;

        foreach (var word in text.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries))
        {
            currentLine = AppendWord(
                lines,
                currentLine,
                word,
                maxWidthPx,
                measureWidth);
        }

        if (currentLine.Length > 0)
        {
            lines.Add(currentLine);
        }

        return lines;
    }

    private static string AppendWord(
        List<string> lines,
        string currentLine,
        string word,
        float maxWidthPx,
        Func<string, float> measureWidth)
    {
        var candidate = currentLine.Length == 0
            ? word
            : $"{currentLine} {word}";
        if (measureWidth(candidate) <= maxWidthPx)
        {
            return candidate;
        }

        if (currentLine.Length > 0)
        {
            lines.Add(currentLine);
        }

        if (measureWidth(word) <= maxWidthPx)
        {
            return word;
        }

        var chunks = SplitOversizedWord(word, maxWidthPx, measureWidth);
        lines.AddRange(chunks.Take(chunks.Count - 1));
        return chunks[^1];
    }

    private static IReadOnlyList<string> SplitOversizedWord(
        string word,
        float maxWidthPx,
        Func<string, float> measureWidth)
    {
        var chunks = new List<string>();
        var current = string.Empty;

        foreach (var character in word)
        {
            var candidate = current + character;
            if (current.Length == 0 || measureWidth(candidate) <= maxWidthPx)
            {
                current = candidate;
                continue;
            }

            chunks.Add(current);
            current = character.ToString();
        }

        if (current.Length > 0)
        {
            chunks.Add(current);
        }

        return chunks;
    }
}
