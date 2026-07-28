using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
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

    // Detail and evidence rows draw at the Body rung (Rajdhani SemiBold,
    // baked 14px), whose real vertical line spacing measures 24px. Was 22,
    // which clipped that spacing; raised to 24.
    internal const int LineHeight = 24;

    // The space reserved for "AGENT INSPECTOR" before the portrait and
    // detail rows begin. Carries the Title rung (Bebas Neue, baked 22px),
    // whose real vertical line spacing measures 35px. Was 31, which clipped
    // the header face; raised to 35.
    internal const int TitleHeight = 35;
    internal const int TopDetailRowCount = 4;

    /// <summary>
    /// The most lower detail rows <see cref="BuildLowerLines"/> can produce:
    /// intent, contingent, target, position, weapon, attributes, level,
    /// combo attributes, evidence tier, grip, armor, shield, and movement.
    /// The contingent row is present exactly when the agent's
    /// <see cref="AgentView.ContingentState"/> is not
    /// <see cref="ContingentState.None"/>. The combo attributes row is
    /// present exactly when the attributes row is (both come from the same
    /// resolved <see cref="WeaponProfile"/>), and the grip row is absent for
    /// a two-handed weapon, so a real panel draws this many or fewer — the
    /// panel is sized for the maximum so the taller case never clips.
    /// </summary>
    internal const int MaximumLowerRowCount = 13;
    internal const int PortraitBottomGap = 5;
    internal const int TopDetailBottomGap = 2;

    /// <summary>
    /// Wrapped-line budget reserved for the evidence note when sizing the
    /// panel. Detail and evidence rows are drawn at the <c>Body</c> rung
    /// (Rajdhani SemiBold, baked at 14px, scale 1.0 — see
    /// <c>Theming/UiFontRamp.cs</c>). The longest known evidence string
    /// ("PROVISIONAL: comparable to Spanish-era accounts of the
    /// kampilan.", 64 characters) is proven, in
    /// <c>AgentInspectorContentTests</c>, to wrap to no more than 3 lines
    /// at the panel's ~277px text width budget (InspectorWidth 310 minus
    /// Padding*2 minus AccentWidth) across a theory spanning 5, 6, 7, and
    /// 8 pixels of average advance per character — a range chosen to
    /// bracket Rajdhani SemiBold's real condensed advance at this size
    /// rather than pinning to one legacy measurement. This is a sizing
    /// estimate, not a hard limit — <see cref="AgentInspectorPanel"/>
    /// additionally refuses to draw any wrapped line that would fall
    /// past the panel bounds, so an under-estimate here can only drop a
    /// line, never overflow the panel.
    /// </summary>
    internal const int EvidenceReservedLineCount = 3;

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
        var lowerRowCount =
            MaximumLowerRowCount + Math.Max(0, evidenceLineCount);
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
    /// <summary>
    /// Every lower detail row for one selected warrior, in draw order. The
    /// grip row is present only for a one-handed weapon, so the caller draws
    /// this list sequentially rather than indexing fixed rows.
    /// </summary>
    /// <remarks>
    /// Pure: takes the authoritative <see cref="AgentView"/> and two
    /// presentation labels, touches no <c>SpriteBatch</c>,
    /// <c>GraphicsDevice</c>, or window, and is therefore unit testable in
    /// full — the split this repository enforces for panel content.
    /// </remarks>
    internal static IReadOnlyList<string> BuildLowerLines(
        AgentView agent,
        string weaponLabel,
        string evidenceTierLabel)
    {
        var loadout = agent.Loadout;
        var lines = new List<string>(MaximumLowerRowCount)
        {
            $"Intent: {agent.Intent}",
        };

        if (FormatContingentLine(agent.ContingentId, agent.ContingentState)
            is { } contingentLine)
        {
            lines.Add(contingentLine);
        }

        lines.Add($"Target: {agent.TargetEntityId?.ToString() ?? "none"}");
        lines.Add(FormatPositionLine(agent));
        lines.Add(FormatWeaponLine(weaponLabel));

        var profile = TryResolveProfile(loadout);
        if (profile is { } resolvedProfile)
        {
            lines.Add(FormatAttributeLine(resolvedProfile));
        }

        lines.Add(FormatLevelLine(agent.Level));

        if (profile is { } comboProfile)
        {
            lines.Add(FormatComboAttributeLine(comboProfile));
        }

        lines.Add(FormatEvidenceTierLine(evidenceTierLabel));

        if (FormatGripLine(loadout.Weapon, loadout.Shield) is { } gripLine)
        {
            lines.Add(gripLine);
        }

        lines.Add(FormatArmorLine(loadout.Armor));
        lines.Add(FormatShieldLine(loadout.Shield));
        lines.Add(FormatMovementLine(agent.MovementResolution));

        return lines;
    }

    /// <summary>
    /// The contingent row: which contingent this warrior was dealt into and
    /// its current behavioural mode, or <c>null</c> when its
    /// <see cref="ContingentState"/> is
    /// <see cref="ContingentState.None"/> — the frozen
    /// <c>IndependentPursuitV1</c> preset, and every agent under
    /// <c>PersistentContingentsV2</c> before its contingent stage first
    /// resolves. The label names no culture or historical arrangement; it
    /// is a spectator-facing description of the current preset's own
    /// mechanic.
    /// </summary>
    internal static string? FormatContingentLine(
        int contingentId,
        ContingentState state) =>
        state == ContingentState.None
            ? null
            : $"Contingent: {contingentId} — {GetContingentStateLabel(state)}";

    internal static string GetContingentStateLabel(ContingentState state) =>
        state switch
        {
            ContingentState.Advance => "Advancing",
            ContingentState.Hold => "Holding",
            ContingentState.Close => "Closing",
            ContingentState.Break => "Broken",
            _ => throw new ArgumentOutOfRangeException(
                nameof(state),
                state,
                null),
        };

    internal static string FormatPositionLine(AgentView agent) =>
        $"Position: {agent.XRaw / (double)FixedPoint.Scale:0.00}, " +
        $"{agent.YRaw / (double)FixedPoint.Scale:0.00}";

    /// <summary>
    /// The active profile for a loadout, or <c>null</c> when no registered
    /// preset declares attributes for its weapon. Presentation reads the
    /// authoritative preset here; it never recomputes an attribute itself.
    /// </summary>
    private static WeaponProfile? TryResolveProfile(CombatLoadout loadout)
    {
        foreach (var id in Enum.GetValues<CombatPresetId>())
        {
            if (!CombatPresetRegistry.IsRegistered(id))
            {
                continue;
            }

            var rules = CombatPresetRegistry.Get(id);
            if (rules.HasWeaponProfiles)
            {
                return rules.ResolveWeaponProfile(
                    loadout.Weapon,
                    loadout.Shield);
            }
        }

        return null;
    }

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

    /// <summary>
    /// The three attributes the weapon and shield resolve to, in the units a
    /// spectator can check against the event feed: damage as it appears in an
    /// attack line, reach in world units, recovery in ticks.
    /// </summary>
    /// <remarks>
    /// This is the line that makes the solo-versus-shielded trade readable
    /// without watching two battles. Reach is converted out of raw
    /// fixed-point here because nobody reasons in raw units.
    /// </remarks>
    internal static string FormatAttributeLine(WeaponProfile profile) =>
        $"        {profile.DamagePerAttack} dmg / " +
        $"{profile.AttackRangeRaw / FixedPoint.Scale} reach / " +
        $"{profile.AttackCooldownTicks} tick recovery";

    /// <summary>
    /// This warrior's level, set once at spawn from
    /// <see cref="Scenario.PlaceholderFighterLevel"/>. Shown unconditionally
    /// — even at the placeholder value, where every fighter shares the same
    /// level and the field is not yet spectator-discoverable from battle
    /// outcomes — so the row is already present when leveling becomes real.
    /// </summary>
    internal static string FormatLevelLine(int level) =>
        $"Level: {level}";

    /// <summary>
    /// The four attack-combination attributes a weapon's profile declares:
    /// the opening-roll chance, the continuation-roll chance, the maximum
    /// chain length, and the faster cooldown a chain uses while active. This
    /// is where a spectator confirms, for example, that the itak chains more
    /// often than the wasay, rather than inferring it from a sample of
    /// battles. Basis points are out of <see cref="ClashProfile.
    /// BasisPointScale"/> (10,000), so dividing by 100 renders a whole or
    /// fractional percentage.
    /// </summary>
    internal static string FormatComboAttributeLine(WeaponProfile profile) =>
        $"        {profile.ComboOpenChanceBasisPoints / 100.0:0.##}% " +
        $"combo open / " +
        $"{profile.ComboContinueChanceBasisPoints / 100.0:0.##}% " +
        $"combo continue / " +
        $"{profile.ComboMaxSteps} max steps / " +
        $"{profile.ComboCooldownTicks} tick combo cooldown";

    /// <summary>
    /// Which of the weapon's profiles is active, and nothing at all for a
    /// two-handed weapon, which has only one.
    /// </summary>
    internal static string? FormatGripLine(WeaponId weapon, ShieldId shield) =>
        CombatPresetRegistry.TryResolveGrip(weapon) is WeaponGrip.OneHanded
            ? $"Grip:   One-handed, {(shield == ShieldId.None ? "solo" : "shielded")}"
            : null;

    /// <summary>
    /// How far the evidence behind this weapon's name actually reaches.
    /// Shown so a spectator can tell a contemporary attestation from a later
    /// reconstruction rather than reading every name as equally certain.
    /// </summary>
    internal static string FormatEvidenceTierLine(string tierLabel) =>
        $"        Evidence: {tierLabel}";

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
