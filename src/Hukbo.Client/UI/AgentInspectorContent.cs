using Hukbo.Client.Presentation;
using Hukbo.Client.Presentation.Catalogs;
using Hukbo.Client.Theming;
using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
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
    /// intent, the intent's gameplay-model evidence tier and note, contingent,
    /// the contingent's gameplay-model evidence tier and note, target,
    /// position, rank, rank reconstruction note, weapon, attributes, level,
    /// combo attributes, evidence tier, grip, armor, shield, movement,
    /// facing, posture, footwork, pace, and pressure. The contingent row is
    /// present exactly when the agent's
    /// <see cref="AgentView.ContingentState"/> is not
    /// <see cref="ContingentState.None"/>; the standalone leadership row
    /// (leader rank plan L6, see <see cref="FormatLeadershipLine"/>) takes
    /// its place, never adding to the count, exactly when that state is
    /// <see cref="ContingentState.None"/> and the agent is a leader. The
    /// contingent's own gameplay-model evidence tier and note
    /// (<see cref="FormatContingentGameplayModelTierLine"/>,
    /// <see cref="FormatContingentGameplayModelNoteLine"/>) follow the
    /// contingent row whenever it is present, regardless of preset — a
    /// contingent's internal ordering is never a positional assignment under
    /// any preset (<see cref="ContingentState"/>'s own remarks), so the
    /// badge is not gated on
    /// <see cref="MovementPresetId.BattlefieldRealismV10"/>, which
    /// <see cref="AgentView"/> has no field to name (plan section
    /// "Rules that bind every task", item 7: no new field). The intent row's
    /// own gameplay-model evidence tier and note
    /// (<see cref="FormatIntentGameplayModelTierLine"/>,
    /// <see cref="FormatIntentGameplayModelNoteLine"/>) follow the intent row
    /// exactly when <see cref="AgentView.Intent"/> is
    /// <see cref="AgentIntent.BackingAway"/> — the one intent value V10
    /// introduces (battlefield-realism design section 10, "And the label").
    /// The rank reconstruction note row
    /// is present exactly when the agent's <see cref="AgentView.Rank"/>
    /// carries a <see cref="RankLabelEntry.ReconstructionNote"/> — today,
    /// only <see cref="RankId.AlipingNamamahay"/>. The combo attributes row
    /// is present exactly when the attributes row is (both come from the
    /// same resolved <see cref="WeaponProfile"/>), and the grip row is
    /// absent for a two-handed weapon. The four movement rows — facing,
    /// posture, footwork, pace — are present only under a movement preset
    /// that uses equipment-relative footwork; under every legacy preset the
    /// projected fields hold their defaults and all four formatters return
    /// <see langword="null"/> (weapon-relative movement design, section 15).
    /// The pressure row is present only under a preset whose
    /// <c>MovementRuleset.AppliesPressureInterrupt</c> is
    /// <see langword="true"/>, because every other preset leaves this
    /// warrior's threshold at zero and
    /// <see cref="FormatPressureLine"/> then returns
    /// <see langword="null"/> (pressure-interrupt design, section 3,
    /// question 8, channel 3).
    /// A real panel therefore draws this many or fewer — the panel is sized
    /// for the maximum so the taller case never clips.
    /// </summary>
    internal const int MaximumLowerRowCount = 24;
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
        Math.Max(
            0,
            panelWidth
                - (UiScaleContext.Pixels(Padding) * 2)
                - UiScaleContext.Pixels(AccentWidth));

    /// <summary>
    /// Panel height needed so the deepest row — including up to
    /// <paramref name="evidenceLineCount"/> wrapped evidence lines —
    /// still fits above the bottom padding. Mirrors the exact row
    /// arithmetic <see cref="AgentInspectorPanel.Draw"/> uses.
    /// </summary>
    /// <summary>
    /// Row budget reserved for a selected warrior's name provenance when
    /// sizing the panel: the six short rows
    /// <see cref="BuildWarriorNameLines"/> can produce before its two
    /// standalone research notes — evidence tier, recorded spelling where it
    /// differs, name corpus, source document, recorded gender, and the reuse
    /// note's first wrapped line. The two long research notes that follow are
    /// not budgeted for: like the weapon, shield, and appearance variant lines
    /// already appended after them, they are drawn only while rows still fit,
    /// and <see cref="AgentInspectorPanel.Draw"/> refuses to draw any row that
    /// would fall past the panel bounds. This is a sizing estimate, so an
    /// under-estimate can only drop a line, never overflow the panel.
    /// </summary>
    internal const int WarriorNameReservedLineCount = 6;

    internal static int ComputeRequiredHeight(int evidenceLineCount)
    {
        var padding = UiScaleContext.Pixels(Padding);
        var lineHeight = UiScaleContext.Pixels(LineHeight);
        var textY = padding + UiScaleContext.Pixels(TitleHeight);
        var portraitBottom = textY + UiScaleContext.Pixels(PortraitSize);
        var lowerTextY = Math.Max(
            portraitBottom + UiScaleContext.Pixels(PortraitBottomGap),
            textY
                + (TopDetailRowCount * lineHeight)
                + UiScaleContext.Pixels(TopDetailBottomGap));
        var lowerRowCount =
            MaximumLowerRowCount
            + WarriorNameReservedLineCount
            + Math.Max(0, evidenceLineCount);
        var lastRowY = lowerTextY + ((lowerRowCount - 1) * lineHeight);
        var lastRowBottom = lastRowY + lineHeight;
        return lastRowBottom + padding;
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
    /// <param name="agent">The selected warrior's authoritative view.</param>
    /// <param name="weaponLabel">The pair-form weapon label.</param>
    /// <param name="evidenceTierLabel">The weapon's evidence tier label.</param>
    /// <param name="movementSpeedRaw">
    /// The scenario's full movement speed in raw distance per tick — the
    /// denominator of the pace row's percentage. Defaulted to <c>0</c>,
    /// which omits the pace row, so callers written before the
    /// weapon-relative movement design existed (and every caller under a
    /// legacy preset, whose <see cref="AgentView.MovementPaceRaw"/> is
    /// <c>0</c> anyway) still compile and render byte-identically.
    /// </param>
    internal static IReadOnlyList<string> BuildLowerLines(
        AgentView agent,
        string weaponLabel,
        string evidenceTierLabel,
        int movementSpeedRaw = 0)
    {
        var loadout = agent.Loadout;
        var lines = new List<string>(MaximumLowerRowCount)
        {
            FormatIntentLine(agent.Intent),
        };

        // Battlefield-realism design section 10 ("And the label"): the one
        // intent value V10 introduces gets its own gameplay-model badge
        // immediately below the intent row, so a spectator reading
        // "Backing away from close fighters" cannot mistake it for an
        // attested historical retreat drill.
        if (agent.Intent == AgentIntent.BackingAway)
        {
            lines.Add(FormatIntentGameplayModelTierLine());
            lines.Add(FormatIntentGameplayModelNoteLine());
        }

        if (FormatContingentLine(agent.ContingentId, agent.ContingentState, agent.IsLeader)
            is { } contingentLine)
        {
            lines.Add(contingentLine);

            // The same gameplay-model badge, on the contingent row. Shown
            // whenever the row itself is shown, under every preset — a
            // contingent's internal ordering is never a positional
            // assignment (ContingentState.cs's own remarks), and AgentView
            // carries no field naming the active preset for this to gate on
            // (plan section "Rules that bind every task", item 7).
            lines.Add(FormatContingentGameplayModelTierLine());
            lines.Add(FormatContingentGameplayModelNoteLine());
        }
        else if (agent.IsLeader)
        {
            lines.Add(FormatLeadershipLine());
        }

        lines.Add($"Target: {agent.TargetEntityId?.ToString() ?? "none"}");
        lines.Add(FormatPositionLine(agent));
        lines.Add(FormatRankLine(agent.Rank));

        if (RankLabelCatalog.Get(agent.Rank).ReconstructionNote is { } rankNote)
        {
            lines.Add(FormatRankReconstructionNoteLine(rankNote));
        }

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

        // The four weapon-relative movement rows (design section 15.2), in
        // the design's order: Facing, Posture, Footwork, Pace. Every
        // formatter returns null while its projected field holds the
        // default a legacy preset never moves, so legacy inspector output
        // is byte-identical.
        if (FormatFacingLine(agent.Facing) is { } facingLine)
        {
            lines.Add(facingLine);
        }

        if (FormatPostureLine(agent.TacticalPosture) is { } postureLine)
        {
            lines.Add(postureLine);
        }

        if (FormatFootworkLine(
                agent.FootworkPhase,
                agent.FootworkTicksRemaining,
                agent.BrokeOffUnderPressure)
            is { } footworkLine)
        {
            lines.Add(footworkLine);
        }

        if (FormatPaceLine(agent.MovementPaceRaw, movementSpeedRaw)
            is { } paceLine)
        {
            lines.Add(paceLine);
        }

        // The pressure row (pressure-interrupt design section 3, question 8,
        // channel 3), last because it is the newest and because a preset
        // without the interrupt leaves the threshold at zero and drops the
        // row entirely, keeping every earlier preset's output byte-identical.
        if (FormatPressureLine(
                agent.PressureBasisPoints,
                agent.PressureThresholdBasisPoints)
            is { } pressureLine)
        {
            lines.Add(pressureLine);
        }

        return lines;
    }

    /// <summary>
    /// The contingent row: which contingent this warrior was dealt into, its
    /// current behavioural mode, and — appended to the same line rather than
    /// as a separate row, so no unconditional fourteenth row is added and
    /// <see cref="MaximumLowerRowCount"/> stays untouched (leader rank plan
    /// L5) — whether this warrior currently leads it. <c>null</c> when its
    /// <see cref="ContingentState"/> is <see cref="ContingentState.None"/> —
    /// the frozen <c>IndependentPursuitV1</c> preset, and every agent under
    /// <c>PersistentContingentsV2</c> before its contingent stage first
    /// resolves — regardless of <paramref name="isLeader"/>, which is always
    /// <see langword="false"/> under those same conditions in practice
    /// (<c>BattleSimulation</c> never marks a leader while the contingent
    /// scan has not run) but is not itself what decides whether the line is
    /// shown. The label names no culture or historical arrangement; it is a
    /// spectator-facing description of the current preset's own mechanic.
    /// </summary>
    /// <remarks>
    /// The leadership suffix reads "leading", never "chief" or "commander"
    /// (<c>CLAUDE.md</c> section 7): the succession rule it reflects is a
    /// <b>Provisional reconstruction</b>, not a documented historical fact,
    /// and either of those words would present an unearned rank claim the
    /// historical accuracy policy does not license here.
    /// </remarks>
    internal static string? FormatContingentLine(
        int contingentId,
        ContingentState state,
        bool isLeader)
    {
        if (state == ContingentState.None)
        {
            return null;
        }

        var line = $"Contingent: {contingentId} — {GetContingentStateLabel(state)}";
        return isLeader ? $"{line} (leading)" : line;
    }

    /// <summary>
    /// The standalone leadership row (leader rank plan L6). Emitted by
    /// <see cref="BuildLowerLines"/> exactly when
    /// <see cref="FormatContingentLine"/> returned <see langword="null"/> —
    /// today that is only <see cref="ContingentState.None"/> — and
    /// <see cref="AgentView.IsLeader"/> is <see langword="true"/>: the frozen
    /// <c>IndependentPursuitV1</c> preset, and every agent under
    /// <c>PersistentContingentsV2</c> before its contingent stage first
    /// resolves. Without this row a leader elected under either condition had
    /// no leadership indication anywhere in the inspector — the contingent
    /// row that normally carries the "(leading)" suffix is itself absent
    /// there. <see cref="BuildLowerLines"/>'s <c>if</c>/<c>else if</c>
    /// structure makes this row and that suffix mutually exclusive by
    /// construction: the two conditions partition on whether
    /// <see cref="FormatContingentLine"/> returned a line, so a warrior never
    /// produces both, and the deepest row count — and therefore
    /// <see cref="MaximumLowerRowCount"/> — is unchanged.
    /// </summary>
    /// <remarks>
    /// Reads "leading", never "chief" or "commander" (<c>CLAUDE.md</c>
    /// section 7), for the same reason <see cref="FormatContingentLine"/>'s
    /// own suffix does: the succession rule that elects a leader is a
    /// <b>Provisional reconstruction</b>, not a documented historical fact,
    /// and either of those words would present an unearned rank claim.
    /// </remarks>
    internal static string FormatLeadershipLine() => "Leading";

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

    /// <summary>
    /// RU-16: the reason-code row, first-class for all seven
    /// <see cref="AgentIntent"/> values including <see cref="AgentIntent.Holding"/>
    /// and <see cref="AgentIntent.BackingAway"/>. This is one of the two
    /// defences against risk 8 — a ranged warrior deliberately holding its
    /// preferred engagement distance must read as visibly distinct from
    /// <see cref="FormatMovementLine"/>'s "Blocked", the V6/V7 standoff bug
    /// this feature would otherwise look like
    /// (docs/plans/2026-08-07-ranged-units.md, RU-16). Movement and intent
    /// are independent fields on <see cref="AgentView"/>, so this row and
    /// the movement row below it always render side by side rather than one
    /// suppressing the other.
    /// </summary>
    internal static string FormatIntentLine(AgentIntent intent) =>
        $"Intent: {GetIntentLabel(intent)}";

    /// <summary>
    /// The five original <see cref="AgentIntent"/> values read as their bare
    /// enum name, unchanged. <see cref="AgentIntent.Holding"/> reads "Holding
    /// at range" instead of the bare enum name "Holding" so it cannot be
    /// misread beside <see cref="GetMovementLabel"/>'s "Blocked" — see
    /// <see cref="FormatIntentLine"/>. <see cref="AgentIntent.BackingAway"/>
    /// — battlefield-realism design section 10 — reads "Backing away from
    /// close fighters", deliberately distinct from "Holding at range": a
    /// warrior that chose to hold its distance and a warrior driven off it
    /// by a close melee threat must never read the same way to a spectator.
    /// Every arm is explicit and the default throws rather than falling
    /// through to a borrowed label, so a future value can never silently
    /// collapse into an existing one here the way an unmapped
    /// <see cref="MovementResolution"/> collapses into
    /// <see cref="GetMovementLabel"/>'s "Holding" catch-all.
    /// </summary>
    internal static string GetIntentLabel(AgentIntent intent) =>
        intent switch
        {
            AgentIntent.Idle => "Idle",
            AgentIntent.Moving => "Moving",
            AgentIntent.Attacking => "Attacking",
            AgentIntent.Dead => "Dead",
            AgentIntent.Regrouping => "Regrouping",
            AgentIntent.Holding => "Holding at range",
            AgentIntent.BackingAway => "Backing away from close fighters",
            _ => throw new ArgumentOutOfRangeException(
                nameof(intent),
                intent,
                null),
        };

    /// <summary>
    /// The plain-language note shared by every battlefield-realism V10
    /// gameplay-model badge (design section 10, "And the label";
    /// docs/plans/2026-08-11-battlefield-realism-design.md sections 2.1,
    /// 2.2). <c>CLAUDE.md</c> section 7 draws a hard line between the three
    /// historical evidence tiers — Documented, Documented form uncertain,
    /// Provisional reconstruction — and a gameplay rule, which is none of
    /// them and must say so plainly rather than being left to read as a
    /// bare "Provisional reconstruction" historical claim. This note is
    /// that plain statement, shared by <see cref="FormatContingentGameplayModelNoteLine"/>
    /// and <see cref="FormatIntentGameplayModelNoteLine"/>.
    /// </summary>
    internal const string GameplayModelNote =
        "Gameplay model — not a historical formation or behavior. See " +
        "docs/plans/2026-08-11-battlefield-realism-design.md.";

    /// <summary>
    /// The evidence-tier badge for the intent row (design section 10),
    /// shown only when <see cref="AgentView.Intent"/> is
    /// <see cref="AgentIntent.BackingAway"/> — the one intent value
    /// battlefield-realism V10 introduces. Reuses the exact badge mechanism
    /// <see cref="FormatVariantTierLine"/> and
    /// <see cref="FormatShieldVariantTierLine"/> already draw for weapon and
    /// shield variants: <see cref="FormatVisualEvidenceTierLabel"/> feeding a
    /// prefixed tier line, suffixed with "gameplay model" so the tier
    /// reading can never be mistaken for a bare historical attestation.
    /// </summary>
    internal static string FormatIntentGameplayModelTierLine() =>
        $"        Intent evidence: " +
        $"{FormatVisualEvidenceTierLabel(VisualEvidenceTier.ProvisionalReconstruction)} " +
        "— gameplay model";

    internal static string FormatIntentGameplayModelNoteLine() =>
        $"        {GameplayModelNote}";

    /// <summary>
    /// The evidence-tier badge for the contingent row (design section 10),
    /// shown whenever <see cref="FormatContingentLine"/> renders a row —
    /// under every preset, since a contingent's internal ordering is never a
    /// positional assignment regardless of which preset assembled it, and
    /// <see cref="AgentView"/> carries no field naming the active preset for
    /// this to gate on more narrowly. Reuses the same badge mechanism as
    /// <see cref="FormatIntentGameplayModelTierLine"/>.
    /// </summary>
    internal static string FormatContingentGameplayModelTierLine() =>
        $"        Contingent evidence: " +
        $"{FormatVisualEvidenceTierLabel(VisualEvidenceTier.ProvisionalReconstruction)} " +
        "— gameplay model";

    internal static string FormatContingentGameplayModelNoteLine() =>
        $"        {GameplayModelNote}";

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

    /// <summary>
    /// The facing row (weapon-relative movement design, section 15.2): a
    /// compass label for the warrior's authoritative 16-sector facing,
    /// never a sector number. <see langword="null"/> for
    /// <see cref="Facing16.None"/>, the value every legacy preset leaves
    /// forever, so legacy inspector output is byte-identical. A dead V6
    /// warrior's retained facing still renders — that is the point of
    /// retaining it.
    /// </summary>
    internal static string? FormatFacingLine(Facing16 facing) =>
        facing == Facing16.None ? null : $"Facing: {GetFacingLabel(facing)}";

    /// <summary>
    /// The compass label for one resolved sector. Sector 0 is screen-right
    /// and positive Y is screen-down, so sector 4 reads South — the same
    /// convention <see cref="Facing16"/> itself documents.
    /// </summary>
    internal static string GetFacingLabel(Facing16 facing) =>
        facing switch
        {
            Facing16.East => "East",
            Facing16.EastSouthEast => "East-southeast",
            Facing16.SouthEast => "Southeast",
            Facing16.SouthSouthEast => "South-southeast",
            Facing16.South => "South",
            Facing16.SouthSouthWest => "South-southwest",
            Facing16.SouthWest => "Southwest",
            Facing16.WestSouthWest => "West-southwest",
            Facing16.West => "West",
            Facing16.WestNorthWest => "West-northwest",
            Facing16.NorthWest => "Northwest",
            Facing16.NorthNorthWest => "North-northwest",
            Facing16.North => "North",
            Facing16.NorthNorthEast => "North-northeast",
            Facing16.NorthEast => "Northeast",
            Facing16.EastNorthEast => "East-northeast",
            _ => throw new ArgumentOutOfRangeException(
                nameof(facing),
                facing,
                null),
        };

    /// <summary>
    /// The posture row (design section 15.2): the contingent-level stance
    /// in plain English, mirroring <see cref="GetContingentStateLabel"/>'s
    /// style. <see langword="null"/> for
    /// <see cref="TacticalPosture.None"/>, the value every legacy preset —
    /// and death cleanup — leaves, so legacy inspector output is
    /// byte-identical.
    /// </summary>
    internal static string? FormatPostureLine(TacticalPosture posture) =>
        posture == TacticalPosture.None
            ? null
            : $"Posture: {GetPostureLabel(posture)}";

    internal static string GetPostureLabel(TacticalPosture posture) =>
        posture switch
        {
            TacticalPosture.Advance => "Advancing",
            TacticalPosture.Hold => "Holding",
            TacticalPosture.Yield => "Yielding",
            TacticalPosture.Regroup => "Regrouping",
            TacticalPosture.Pursue => "Pursuing",
            TacticalPosture.Withdraw => "Withdrawing",
            _ => throw new ArgumentOutOfRangeException(
                nameof(posture),
                posture,
                null),
        };

    /// <summary>
    /// The footwork row (design section 15.2): the lifecycle phase in plain
    /// English, appending the remaining ticks only while the phase is
    /// <see cref="FootworkPhase.Commit"/> or
    /// <see cref="FootworkPhase.Recover"/> — the two phases whose timer is
    /// authoritative. <see langword="null"/> for
    /// <see cref="FootworkPhase.None"/>, the value every legacy preset —
    /// and death cleanup — leaves, so legacy inspector output is
    /// byte-identical.
    /// </summary>
    /// <param name="phase">The warrior's finalised footwork phase.</param>
    /// <param name="ticksRemaining">
    /// The <see cref="FootworkPhase.Commit"/> / <see cref="FootworkPhase.Recover"/>
    /// timer, appended only in those two phases.
    /// </param>
    /// <param name="brokeOffUnderPressure">
    /// The warrior's authoritative
    /// <see cref="AgentView.BrokeOffUnderPressure"/> — channel 2 of
    /// pressure-interrupt design section 3, question 8. When it is set and the
    /// phase really is <see cref="FootworkPhase.Disengage"/>, the row gains a
    /// suffix so a warrior broken off mid-commitment reads differently from one
    /// that disengaged on the ordinary ratio rule. Defaulted to
    /// <see langword="false"/>, which is what every preset without the
    /// interrupt projects, so those presets' rows stay byte-identical and
    /// callers written before the interrupt existed still compile. The suffix
    /// is gated on the phase for the same reason the timer is: a stale flag
    /// must not leak onto a phase that does not carry it.
    /// </param>
    internal static string? FormatFootworkLine(
        FootworkPhase phase,
        int ticksRemaining,
        bool brokeOffUnderPressure = false)
    {
        if (phase == FootworkPhase.None)
        {
            return null;
        }

        var label = GetFootworkLabel(phase);
        if (phase is FootworkPhase.Commit or FootworkPhase.Recover)
        {
            return $"Footwork: {label} " +
                $"({ticksRemaining} {(ticksRemaining == 1 ? "tick" : "ticks")})";
        }

        return phase == FootworkPhase.Disengage && brokeOffUnderPressure
            ? $"Footwork: {label} (broke off under pressure)"
            : $"Footwork: {label}";
    }

    internal static string GetFootworkLabel(FootworkPhase phase) =>
        phase switch
        {
            FootworkPhase.Approach => "Approaching",
            FootworkPhase.Engage => "Engaging",
            FootworkPhase.Commit => "Committed",
            FootworkPhase.Recover => "Recovering",
            FootworkPhase.Refuse => "Refused",
            FootworkPhase.Disengage => "Disengaging",
            FootworkPhase.Regroup => "Regrouping",
            FootworkPhase.Pursue => "Pursuing",
            _ => throw new ArgumentOutOfRangeException(
                nameof(phase),
                phase,
                null),
        };

    /// <summary>
    /// The pace row (design section 15.2): the retained pace as an integer
    /// percentage of the warrior's full movement speed, computed with pure
    /// integer arithmetic — truncating division, never floating point.
    /// <see langword="null"/> when no pace is retained — the legacy
    /// default, a V6 warrior standing still, and death cleanup all share
    /// the same zero — and <see langword="null"/> when the caller supplies
    /// no <paramref name="movementSpeedRaw"/>, because a percentage of an
    /// unknown speed cannot be rendered honestly.
    /// </summary>
    internal static string? FormatPaceLine(
        int movementPaceRaw,
        int movementSpeedRaw)
    {
        if (movementPaceRaw <= 0 || movementSpeedRaw <= 0)
        {
            return null;
        }

        var percent = (int)((long)movementPaceRaw * 100 / movementSpeedRaw);
        return $"Pace: {percent}% of full speed";
    }

    /// <summary>
    /// The pressure row (pressure-interrupt design section 3, question 8,
    /// channel 3): this warrior's running weighted pressure read against its
    /// own break-off threshold, both in the basis points the simulation
    /// already works in, so the two numbers compare with no further
    /// arithmetic. It is drawn on every tick for every living warrior under a
    /// preset that applies the interrupt, not only on the tick an interrupt
    /// fires — a running value is what lets a spectator predict a break-off
    /// and understand why one warrior broke and the neighbour beside it did
    /// not, and the interrupt fires far too rarely for a firing-only row to
    /// show anything.
    /// </summary>
    /// <remarks>
    /// Presentation renders the two authoritative values and nothing else: it
    /// never recomputes the pressure, never compares it to decide a
    /// break-off, and never infers one from the numbers.
    /// </remarks>
    /// <param name="pressureBasisPoints">
    /// <see cref="AgentView.PressureBasisPoints"/> as of this tick.
    /// </param>
    /// <param name="thresholdBasisPoints">
    /// <see cref="AgentView.PressureThresholdBasisPoints"/> — this warrior's
    /// own bar, from its resolved loadout profile.
    /// </param>
    /// <returns>
    /// <see langword="null"/> when the threshold is zero or negative. Every
    /// preset that does not apply the interrupt projects a zero threshold, as
    /// does death cleanup, so the row is absent under all of them and their
    /// inspector output stays byte-identical. A zero threshold is also the one
    /// value that cannot be rendered honestly: it would read as a warrior
    /// already past its bar.
    /// </returns>
    internal static string? FormatPressureLine(
        int pressureBasisPoints,
        int thresholdBasisPoints)
    {
        if (thresholdBasisPoints <= 0)
        {
            return null;
        }

        return $"Pressure: {pressureBasisPoints} of {thresholdBasisPoints} " +
            "basis points to break off";
    }

    internal static string FormatWeaponLine(string weaponLabel) =>
        $"Weapon: {weaponLabel}";

    /// <summary>
    /// This warrior's social and legal standing, in pair form
    /// (CLAUDE.md section 7) — read straight from
    /// <see cref="RankLabelCatalog"/>, never re-derived or hand-duplicated.
    /// <see cref="RankId"/> is never a delegated military office; this row
    /// shows standing only, the same distinction
    /// <c>docs/research/ARMY-COMPOSITION.md</c> §11.4 draws.
    /// </summary>
    internal static string FormatRankLine(RankId rank) =>
        $"Rank: {RankLabelCatalog.Get(rank).Label}";

    /// <summary>
    /// The reconstruction caveat <see cref="RankLabelEntry.ReconstructionNote"/>
    /// carries for <see cref="RankId.AlipingNamamahay"/> — shown immediately
    /// below the rank row whenever the catalog entry declares one, per
    /// <c>docs/research/HISTORICAL_1500s_RANKS.md</c>'s requirement that any
    /// roster fielding this class say so wherever the class is shown.
    /// </summary>
    internal static string FormatRankReconstructionNoteLine(string note) =>
        $"        {note}";

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

    /// <summary>
    /// VIS-012: the inspector's weapon-variant lines (weapon-visuals-
    /// design.md; R-W1.6, R-X.6, R-X.7, R-X.10) — the spectator-
    /// discoverability channel for the presentation-only catalog work
    /// VIS-010/VIS-011 shipped. Two parts, both read verbatim off
    /// <see cref="WeaponVisualCatalog"/> entries rather than re-derived or
    /// hand-duplicated (weapon-visuals-design.md implementation step 2):
    /// <list type="bullet">
    /// <item>
    /// the resolved tint <paramref name="weaponTintId"/> names — its own
    /// evidence tier (always <c>presentation-only, no historical claim</c>
    /// today, since every shipped tint is tone-only) and its note — distinct
    /// from, and shown in addition to, the weapon's silhouette-level
    /// evidence tier and note <see cref="FormatEvidenceTierLine"/> already
    /// surfaces.
    /// </item>
    /// <item>
    /// every catalog silhouette entry for <paramref name="weapon"/> that the
    /// pawn-scale selection stream can never return (Kampilan <c>k2</c>;
    /// Kalis <c>l2</c>, <c>l3</c>) — R-W1.4's later or provisional forms,
    /// each explicitly labelled as such and carrying its own tier and note.
    /// Wasay and Itak contribute none: neither weapon catalogs a later or
    /// provisional form. Neither do Bangkaw, Busog, or Arquebus (RU-16,
    /// plan section 3's "second correction") — an evidence claim, not an
    /// unexamined default.
    /// </item>
    /// </list>
    /// Every returned line's weapon name is
    /// <see cref="VisualCatalogEntry.DisplayLabel"/> read straight from the
    /// catalog — the same unchanged pair-form label
    /// <see cref="FormatWeaponLine"/> already shows, never a bare Filipino
    /// term (R-X.6). Where an entry's note names a place and time (for
    /// example "Mactan, 1521"), that inspiration tag is already part of the
    /// catalog note text this method returns verbatim (R-X.10).
    /// </summary>
    /// <remarks>
    /// Pure: reads only the static <see cref="WeaponVisualCatalog"/> tables
    /// keyed by <paramref name="weapon"/> and <paramref name="weaponTintId"/>,
    /// touches no <c>SpriteBatch</c>, <c>GraphicsDevice</c>, or window.
    /// Returns an empty list, never throws, when <paramref name="weaponTintId"/>
    /// does not resolve to a known tint for <paramref name="weapon"/> — a
    /// stale or unrecognized identifier drops the tint lines rather than
    /// crashing the inspector; the later-or-provisional-form lines are
    /// unaffected because they never depend on the tint lookup.
    /// </remarks>
    internal static IReadOnlyList<string> BuildWeaponVariantLines(
        PawnWeaponRole weapon,
        string weaponTintId)
    {
        ArgumentNullException.ThrowIfNull(weaponTintId);

        var lines = new List<string>();

        var tint = WeaponVisualCatalog.GetTints(weapon)
            .FirstOrDefault(entry => string.Equals(
                entry.Catalog.Id,
                weaponTintId,
                StringComparison.Ordinal));
        if (tint is not null)
        {
            lines.Add(FormatVariantTierLine(tint.Catalog.EvidenceTier));
            lines.Add(FormatVariantNoteLine(tint.Catalog.Notes));
        }

        foreach (var laterForm in GetLaterOrProvisionalForms(weapon))
        {
            lines.Add(FormatLaterFormLine(laterForm.Catalog));
        }

        return lines;
    }

    internal static string FormatVariantTierLine(VisualEvidenceTier tier) =>
        $"        Variant evidence: {FormatVisualEvidenceTierLabel(tier)}";

    internal static string FormatVariantNoteLine(string note) =>
        $"        {note}";

    /// <summary>
    /// One inspector-only later or provisional form (Kampilan <c>k2</c>;
    /// Kalis <c>l2</c>, <c>l3</c>), explicitly labelled as such (R-W1.4) —
    /// the pawn-scale selection stream can never return
    /// <paramref name="laterForm"/>, but the inspector may honestly say the
    /// form exists.
    /// </summary>
    internal static string FormatLaterFormLine(VisualCatalogEntry laterForm) =>
        $"{laterForm.DisplayLabel} — later or provisional form " +
        $"({FormatVisualEvidenceTierLabel(laterForm.EvidenceTier)}): " +
        laterForm.Notes;

    internal static string FormatVisualEvidenceTierLabel(VisualEvidenceTier tier) =>
        tier switch
        {
            VisualEvidenceTier.Documented => "Documented",
            VisualEvidenceTier.DocumentedFormUncertain =>
                "Documented, form uncertain",
            VisualEvidenceTier.ProvisionalReconstruction =>
                "Provisional reconstruction",
            VisualEvidenceTier.PresentationOnly =>
                "presentation-only, no historical claim",
            _ => throw new ArgumentOutOfRangeException(
                nameof(tier),
                tier,
                null),
        };

    /// <summary>
    /// Every catalog silhouette entry for <paramref name="weapon"/> the
    /// pawn-scale selection stream can never return
    /// (<see cref="WeaponSilhouetteEntry.PawnSelectable"/> is
    /// <see langword="false"/>) — R-W1.4's later or provisional forms.
    /// Reads the same static per-weapon lists <see cref="WeaponVisualCatalog"/>
    /// already exposes for iteration; adds no new catalog data.
    /// </summary>
    private static IReadOnlyList<WeaponSilhouetteEntry> GetLaterOrProvisionalForms(
        PawnWeaponRole weapon) =>
        weapon switch
        {
            PawnWeaponRole.Kampilan => WeaponVisualCatalog.KampilanSilhouettes
                .Where(entry => !entry.PawnSelectable)
                .ToArray(),
            PawnWeaponRole.Kalis => WeaponVisualCatalog.KalisSilhouettes
                .Where(entry => !entry.PawnSelectable)
                .ToArray(),
            PawnWeaponRole.Wasay => WeaponVisualCatalog.WasaySilhouettes
                .Where(entry => !entry.PawnSelectable)
                .ToArray(),
            PawnWeaponRole.Itak => WeaponVisualCatalog.ItakSilhouettes
                .Where(entry => !entry.PawnSelectable)
                .ToArray(),
            // RU-16 (plan section 3, "second correction"): none of the three
            // ranged weapons catalogs a later or provisional form today.
            // This is an evidence claim under CLAUDE.md section 7 — each
            // arm was checked against WeaponVisualCatalog and found empty —
            // not a default the switch falls through to; that is why each
            // gets its own arm here instead of joining the throwing default
            // below.
            PawnWeaponRole.Bangkaw => Array.Empty<WeaponSilhouetteEntry>(),
            PawnWeaponRole.Busog => Array.Empty<WeaponSilhouetteEntry>(),
            PawnWeaponRole.Arquebus => Array.Empty<WeaponSilhouetteEntry>(),
            _ => throw new ArgumentOutOfRangeException(
                nameof(weapon),
                weapon,
                null),
        };

    /// <summary>
    /// VIS-016: the standalone shield-family research note the inspector
    /// always appends for a selected shielded pawn, independent of which
    /// skin resolved (shield-visuals-design.md, OD-2, resolved 2026-07-28).
    /// The <i>palisay</i> — a small Tagalog round buckler (Scott 1994; the
    /// shield design's status catalog, entry S6) — is not implemented in
    /// Hukbo as a shield skin or a mechanical shield: buckler coverage is a
    /// different defensive object from the tall body shield above, and S6 is
    /// recorded as future-mechanical-only, excluded as a skin (R-X.12,
    /// R-W2.4). The name itself is a provisional attachment, pending
    /// page-level verification of an early-1600s vocabulary entry. This text
    /// is inspector metadata only, explicitly flagged attestation-pending,
    /// and is never tied to any resolved shield skin or shown in a
    /// player-facing label (OD-2, R-X.6).
    /// </summary>
    internal const string PalisayResearchNote =
        "Research note: the palisay, a small Tagalog round buckler (Scott " +
        "1994), is not implemented in Hukbo as a shield skin or a " +
        "mechanical shield — it is a different defensive object from the " +
        "tall body shield above. The palisay name is a provisional " +
        "attachment, attestation-pending page-level verification of an " +
        "early-1600s vocabulary entry.";

    /// <summary>
    /// VIS-016: the inspector's shield-variant lines (shield-visuals-
    /// design.md; R-W2.6, R-W2.7, R-X.6, R-X.10) — the spectator-
    /// discoverability channel for the presentation-only skin work
    /// VIS-013/VIS-014 shipped. Reads <see cref="ShieldVisualCatalog"/>
    /// directly rather than hand-duplicating catalog text, mirroring
    /// <see cref="BuildWeaponVariantLines"/>'s own pattern. Returns an empty
    /// list for <see cref="PawnShieldRole.None"/>: an unshielded pawn has no
    /// shield skin, and no shield research note, to show (R-X.1, the shield
    /// design's manual row "for a selected shielded pawn").
    /// </summary>
    /// <remarks>
    /// Pure: reads only the static <see cref="ShieldVisualCatalog"/> tables
    /// keyed by <paramref name="shield"/> and <paramref name="shieldSkinId"/>,
    /// touches no <c>SpriteBatch</c>, <c>GraphicsDevice</c>, or window. Never
    /// throws for a stale or unrecognized <paramref name="shieldSkinId"/> — it
    /// omits the skin's own label/tier/note lines and still appends the
    /// standalone <see cref="PalisayResearchNote"/> line, because that line
    /// never depends on the skin lookup.
    /// </remarks>
    internal static IReadOnlyList<string> BuildShieldVariantLines(
        PawnShieldRole shield,
        string shieldSkinId)
    {
        ArgumentNullException.ThrowIfNull(shieldSkinId);

        if (shield == PawnShieldRole.None)
        {
            return [];
        }

        var lines = new List<string>();

        var skin = ShieldVisualCatalog.GetSkins(shield)
            .FirstOrDefault(entry => string.Equals(
                entry.Catalog.Id,
                shieldSkinId,
                StringComparison.Ordinal));
        if (skin is not null)
        {
            lines.Add(FormatShieldSkinLabelLine(skin.Catalog.DisplayLabel));
            lines.Add(FormatShieldVariantTierLine(skin.Catalog.EvidenceTier));
            lines.Add(FormatShieldVariantNoteLine(skin.Catalog.Notes));
        }

        lines.Add(FormatShieldResearchNoteLine(PalisayResearchNote));

        return lines;
    }

    internal static string FormatShieldSkinLabelLine(string label) =>
        $"        Shield skin: {label}";

    internal static string FormatShieldVariantTierLine(VisualEvidenceTier tier) =>
        $"        Shield evidence: {FormatVisualEvidenceTierLabel(tier)}";

    internal static string FormatShieldVariantNoteLine(string note) =>
        $"        {note}";

    internal static string FormatShieldResearchNoteLine(string note) =>
        $"        {note}";

    // ===== VIS-024: appearance-preset inspector lines =====

    /// <summary>
    /// VIS-024: the appearance domain's pending-term flags (R-X.6). The
    /// research names an indigenous term for each of these five
    /// recipe-representable components, but <see cref="AppearanceComponentCatalog"/>'s
    /// own class remarks ("R-X.6 pair-form labels") deliberately keep every
    /// one of them out of every player- and inspector-facing string in that
    /// file, pending attestation review. This task is the one place those
    /// terms are allowed back in — explicitly flagged pending, and only in
    /// inspector metadata (R-X.6: "MAY appear in inspector metadata
    /// explicitly flagged as pending"), never in a player-facing label and
    /// never unflagged. Keyed by the <see cref="VisualCatalogEntry.Id"/> of
    /// the single <see cref="AppearanceComponentEntry"/> each term names, so
    /// <see cref="BuildAppearancePresetLines"/> only ever shows a
    /// pending-term line for a preset whose recipe actually carries that
    /// component. I1 and I2 (full and partial tattoos) share the one entry —
    /// both render through the same tattoo tone-shift channel
    /// (<see cref="AppearanceComponentCatalog.TattooToneShift"/>) and the
    /// research names <i>batuk</i> for tattooing generally, not for one
    /// coverage stage over the other.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> AppearancePendingTermsByComponentCatalogId =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [AppearanceComponentCatalog.ArmorF2CordedFiberArmor.Catalog.Id] =
                "Pending term: barote (Scott) — the Visayan corded-armor " +
                "term; this research could not independently confirm its " +
                "attestation date inside the research window. Pending " +
                "verification; kept out of the label.",
            [AppearanceComponentCatalog.SashBeltG1RedWaistSash.Catalog.Id] =
                "Pending term: kandit (Scott) — pending one review of the " +
                "attestation chain. Pending verification; kept out of the " +
                "label.",
            [AppearanceComponentCatalog.AdornmentI4GoldEarrings.Catalog.Id] =
                "Pending term: panika (Scott) — the Visayan gold-earring " +
                "vocabulary. Pending attestation review; kept out of the " +
                "label.",
            [AppearanceComponentCatalog.AdornmentI5GoldNecklace.Catalog.Id] =
                "Pending term: kamagi — the massive Visayan gold-necklace " +
                "type, attested in later Visayan sources and the object " +
                "record. Pending attestation review; kept out of the " +
                "label.",
            [AppearanceComponentCatalog.AdornmentI1FullBodyTattoos.Catalog.Id] =
                "Pending term: batuk (Scott, early lexical record; batok " +
                "is the modern revival term). Pending attestation review " +
                "of the transmission chain; kept out of the label.",
            [AppearanceComponentCatalog.AdornmentI2PartialTattoos.Catalog.Id] =
                "Pending term: batuk (Scott, early lexical record; batok " +
                "is the modern revival term). Pending attestation review " +
                "of the transmission chain; kept out of the label.",
        };

    /// <summary>
    /// VIS-024: the appearance domain's standalone non-renderable-adornment
    /// research note (R-W3.15, MAY; warrior-appearance-design.md, "Component
    /// catalog": "Non-renderable adornments (I3, I6, I7, I8, H3) are
    /// inspector flavor text only"). None of I3, I6, I7, or I8 has an
    /// <see cref="AppearanceComponentEntry"/> at all — see
    /// <see cref="AppearanceComponentCatalog"/>'s own class remarks — so none
    /// of them can ever be part of a preset's <see cref="AppearancePresetRecipe"/>;
    /// this constant is therefore the only channel through which the
    /// research's own "make the inspector humane" instruction for these five
    /// items (including H3, the betel pouch) is honored. Appended
    /// unconditionally after every preset's own component lines, mirroring
    /// <see cref="PalisayResearchNote"/>'s own "standalone note, independent
    /// of what actually resolved" shape for the sibling shield domain.
    /// Carries the one pending term this note governs directly — kolombiga
    /// (I6, R-X.6: "attestation clears the bar per the research, Morga 1609;
    /// held pending spelling review only") — in its own pair form, flagged
    /// pending, never shown as a player-facing label.
    /// </summary>
    internal const string AppearanceNonRenderableFlavorNote =
        "Research note: several documented practices are not renderable " +
        "at pawn scale and are recorded here as inspector text only " +
        "(R-W3.15) rather than changing the sprite — a small betel-" +
        "chewing kit pouch (Documented as practice; Provisional " +
        "reconstruction as a warrior's own belt item); Gold Armlets " +
        "(Kolombiga — Gold Armlet, Morga 1609; pending spelling review " +
        "only); Gold-Worked Teeth (Documented, Pigafetta 1521 and Chirino " +
        "1604); filed and blackened teeth as a documented beauty " +
        "practice; and facial tattooing, on which the sources disagree — " +
        "Chirino says the face went untouched, other Visayan material " +
        "describes it on the most seasoned warriors. None of these is " +
        "claimed for the pawn shown; they describe documented period " +
        "practice, not this figure.";

    /// <summary>
    /// VIS-024: the inspector's appearance-preset lines (warrior-appearance-
    /// design.md, "Inspector surface"; R-W3.3, R-W3.12, R-X.6, R-X.7,
    /// R-X.10) — the spectator-discoverability channel for the preset roster
    /// VIS-018/020/021/022 shipped. Reads <see cref="AppearancePresets"/> and
    /// each recipe component's own <see cref="AppearanceComponentEntry"/>
    /// directly rather than hand-duplicating catalog text, mirroring
    /// <see cref="BuildWeaponVariantLines"/> and
    /// <see cref="BuildShieldVariantLines"/>'s own pattern. In order: the
    /// preset's own plain-English name, its scope tag (always shown — every
    /// shipped preset carries one of the four named
    /// <see cref="VisualScopeTag"/> members, never
    /// <see cref="VisualScopeTag.NotApplicable"/>, R-W3.3), its own
    /// weakest-link evidence tier, then one label/tier line plus one note
    /// line per recipe component — hair, head covering, torso garment, lower
    /// garment, armor (if any), sash/belt, accessory (if any), every
    /// adornment, and finally condition — with a pending-term flag line
    /// appended immediately after any component
    /// <see cref="AppearancePendingTermsByComponentCatalogId"/> names. The
    /// standalone <see cref="AppearanceNonRenderableFlavorNote"/> is always
    /// appended last, independent of whether <paramref name="appearancePresetId"/>
    /// resolved.
    /// </summary>
    /// <remarks>
    /// Pure: reads only the static <see cref="AppearancePresets"/> table
    /// keyed by <paramref name="appearancePresetId"/>, touches no
    /// <c>SpriteBatch</c>, <c>GraphicsDevice</c>, or window. Never throws for
    /// a stale or unrecognized <paramref name="appearancePresetId"/> — it
    /// omits the preset's own name/scope/tier/component lines and still
    /// appends the standalone flavor note, because that note never depends
    /// on the preset lookup (mirroring
    /// <see cref="BuildShieldVariantLines"/>'s own unresolved-identifier
    /// behavior).
    /// </remarks>
    internal static IReadOnlyList<string> BuildAppearancePresetLines(
        string appearancePresetId)
    {
        ArgumentNullException.ThrowIfNull(appearancePresetId);

        var lines = new List<string>();

        var preset = AppearancePresets.All.FirstOrDefault(entry => string.Equals(
            entry.Catalog.Id,
            appearancePresetId,
            StringComparison.Ordinal));
        if (preset is not null)
        {
            lines.Add(FormatAppearancePresetNameLine(preset.Catalog.DisplayLabel));
            lines.Add(FormatAppearanceScopeLine(preset.Block));
            lines.Add(FormatAppearancePresetTierLine(preset.Catalog.EvidenceTier));

            foreach (var (category, component) in GetAppearanceRecipeComponentsInDisplayOrder(preset.Recipe))
            {
                lines.Add(FormatAppearanceComponentLine(category, component.Catalog));
                lines.Add(FormatAppearanceComponentNoteLine(component.Catalog.Notes));

                if (AppearancePendingTermsByComponentCatalogId.TryGetValue(
                    component.Catalog.Id,
                    out var pendingLine))
                {
                    lines.Add(FormatAppearancePendingTermLine(pendingLine));
                }
            }
        }

        lines.Add(FormatAppearanceFlavorNoteLine(AppearanceNonRenderableFlavorNote));

        return lines;
    }

    /// <summary>
    /// Every recipe component of <paramref name="recipe"/>, category-tagged,
    /// in the order <see cref="BuildAppearancePresetLines"/> displays them:
    /// hair, head covering, torso garment, lower garment, armor (if
    /// present), sash/belt, accessory (if present), every adornment, then
    /// condition last (R-W3.11: presentation-only, "no historical claim" —
    /// grouped after every historically-evidenced component rather than
    /// among them).
    /// </summary>
    private static IEnumerable<(AppearanceComponentCategory Category, AppearanceComponentEntry Entry)>
        GetAppearanceRecipeComponentsInDisplayOrder(AppearancePresetRecipe recipe)
    {
        yield return (AppearanceComponentCategory.Hair, recipe.Hair);
        yield return (AppearanceComponentCategory.HeadCovering, recipe.HeadCovering);
        yield return (AppearanceComponentCategory.TorsoGarment, recipe.TorsoGarment);
        yield return (AppearanceComponentCategory.LowerGarment, recipe.LowerGarment);

        if (recipe.Armor is { } armor)
        {
            yield return (AppearanceComponentCategory.Armor, armor);
        }

        yield return (AppearanceComponentCategory.SashBelt, recipe.SashBelt);

        if (recipe.Accessory is { } accessory)
        {
            yield return (AppearanceComponentCategory.Accessory, accessory);
        }

        foreach (var adornment in recipe.Adornments)
        {
            yield return (AppearanceComponentCategory.Adornment, adornment);
        }

        yield return (AppearanceComponentCategory.Condition, recipe.Condition);
    }

    internal static string FormatAppearancePresetNameLine(string label) =>
        $"Appearance preset: {label}";

    internal static string FormatAppearanceScopeLine(VisualScopeTag scope) =>
        $"        Scope: {FormatVisualScopeTagLabel(scope)}";

    internal static string FormatAppearancePresetTierLine(VisualEvidenceTier tier) =>
        $"        Preset evidence: {FormatVisualEvidenceTierLabel(tier)}";

    internal static string FormatAppearanceComponentLine(
        AppearanceComponentCategory category,
        VisualCatalogEntry catalogEntry) =>
        $"        {FormatAppearanceComponentCategoryLabel(category)}: " +
        $"{catalogEntry.DisplayLabel} — " +
        $"{FormatVisualEvidenceTierLabel(catalogEntry.EvidenceTier)}";

    internal static string FormatAppearanceComponentNoteLine(string note) =>
        $"        {note}";

    internal static string FormatAppearancePendingTermLine(string pendingNote) =>
        $"        {pendingNote}";

    internal static string FormatAppearanceFlavorNoteLine(string note) =>
        $"        {note}";

    internal static string FormatVisualScopeTagLabel(VisualScopeTag scope) =>
        scope switch
        {
            VisualScopeTag.NotApplicable => "Not applicable",
            VisualScopeTag.Visayan => "Visayan",
            VisualScopeTag.Tagalog => "Tagalog",
            VisualScopeTag.Cagayan => "Cagayan",
            VisualScopeTag.UnscopedGeneric => "Unscoped-generic",
            _ => throw new ArgumentOutOfRangeException(
                nameof(scope),
                scope,
                null),
        };

    internal static string FormatAppearanceComponentCategoryLabel(
        AppearanceComponentCategory category) =>
        category switch
        {
            AppearanceComponentCategory.Hair => "Hair",
            AppearanceComponentCategory.HeadCovering => "Head Covering",
            AppearanceComponentCategory.TorsoGarment => "Torso Garment",
            AppearanceComponentCategory.LowerGarment => "Lower Garment",
            AppearanceComponentCategory.SashBelt => "Sash / Belt",
            AppearanceComponentCategory.Accessory => "Accessory",
            AppearanceComponentCategory.Condition => "Condition",
            AppearanceComponentCategory.Armor => "Armor",
            AppearanceComponentCategory.Adornment => "Adornment",
            _ => throw new ArgumentOutOfRangeException(
                nameof(category),
                category,
                null),
        };

    // ===== Warrior personal-name inspector lines =====

    /// <summary>
    /// The inspector's top identity row: this warrior's personal name and its
    /// entity identifier together. The identifier stays because the name pools
    /// are smaller than a roster and two warriors may share a name; it is also
    /// what the event-log filter matches on.
    /// </summary>
    internal static string FormatWarriorNameLine(
        WarriorNameEntry name,
        ulong entityId)
    {
        ArgumentNullException.ThrowIfNull(name);
        return $"Name: {name.DisplayForm} #{entityId}";
    }

    /// <summary>
    /// The provenance behind the name in the identity row: the form's own
    /// evidence tier, the recorded spelling where it differs from the
    /// displayed one, the regional corpus the faction was assigned, the source
    /// document, what the source records about the bearer's or example's
    /// gender, and what is and is not being claimed by reusing the form. The
    /// two standalone research notes — the structures this catalog does not
    /// generate, and the documentary gap in women's names — are appended last,
    /// mirroring <see cref="PalisayResearchNote"/>'s own "standalone note,
    /// independent of what resolved" shape.
    /// </summary>
    /// <remarks>
    /// Pure: reads only the passed <see cref="WarriorNameEntry"/> and the
    /// static <see cref="WarriorNameCatalog"/> label tables, touches no
    /// <c>SpriteBatch</c>, <c>GraphicsDevice</c>, or window.
    /// </remarks>
    internal static IReadOnlyList<string> BuildWarriorNameLines(WarriorNameEntry name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var lines = new List<string>
        {
            FormatWarriorNameTierLine(name.EvidenceTier),
        };

        if (!string.Equals(name.RecordedForm, name.DisplayForm, StringComparison.Ordinal))
        {
            lines.Add(FormatWarriorNameRecordedFormLine(name.RecordedForm));
        }

        lines.Add(FormatWarriorNameRegionLine(name.Region));
        lines.Add(FormatWarriorNameSourceLine(name.SourceCitation));
        lines.Add(FormatWarriorNameGenderLine(name.RecordedGender));
        lines.Add(FormatWarriorNameNoteLine(name.ReuseNote));
        lines.Add(FormatWarriorNameNoteLine(
            WarriorNameCatalog.ParenthoodResearchNote));
        lines.Add(FormatWarriorNameNoteLine(
            WarriorNameCatalog.WomensNamesResearchNote));

        return lines;
    }

    internal static string FormatWarriorNameTierLine(VisualEvidenceTier tier) =>
        $"        Name evidence: {FormatVisualEvidenceTierLabel(tier)}";

    internal static string FormatWarriorNameRecordedFormLine(string recordedForm) =>
        $"        Recorded as: {recordedForm}";

    internal static string FormatWarriorNameRegionLine(WarriorNameRegion region) =>
        $"        Name corpus: {WarriorNameCatalog.GetRegionLabel(region)}";

    internal static string FormatWarriorNameSourceLine(string sourceCitation) =>
        $"        Source: {sourceCitation}";

    /// <summary>
    /// What the source says about the gender of the bearer or example, and
    /// nothing more. The catalog records evidence, never a restriction, so
    /// this line says "recorded" rather than naming the warrior's own gender.
    /// </summary>
    internal static string FormatWarriorNameGenderLine(
        WarriorNameGenderEvidence gender) =>
        $"        Recorded gender in the source: " +
        $"{GetWarriorNameGenderLabel(gender)}";

    internal static string GetWarriorNameGenderLabel(
        WarriorNameGenderEvidence gender) =>
        gender switch
        {
            WarriorNameGenderEvidence.RecordedMan => "a man",
            WarriorNameGenderEvidence.RecordedWoman => "a woman",
            WarriorNameGenderEvidence.Unspecified => "not specified",
            _ => throw new ArgumentOutOfRangeException(
                nameof(gender),
                gender,
                null),
        };

    internal static string FormatWarriorNameNoteLine(string note) =>
        $"        {note}";

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
