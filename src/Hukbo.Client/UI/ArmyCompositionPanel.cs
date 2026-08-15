using System.Collections.Immutable;
using System.Globalization;
using Hukbo.Client.Presentation.Catalogs;
using Hukbo.Client.Theming;
using Hukbo.Core.Movement;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Hukbo.Client.UI;

/// <summary>
/// One battle's worth of category counts plus the total they must sum to.
/// A record struct holding an <see cref="ImmutableArray{T}"/> needs a
/// hand-written <see cref="Equals(ArmyComposition)"/>/
/// <see cref="GetHashCode"/> pair, because
/// <see cref="ImmutableArray{T}"/>.Equals compares the underlying array by
/// reference. Two compositions built independently with identical counts
/// would otherwise compare unequal — the same trap documented for
/// <c>Scenario.RosterCounts</c> in Hukbo.Core.
/// </summary>
internal readonly record struct ArmyComposition(
    ImmutableArray<int> CategoryCounts,
    int UnitsPerTeam)
{
    public int CategorySum =>
        CategoryCounts.IsDefaultOrEmpty ? 0 : CategoryCounts.Sum();

    public int Unassigned => UnitsPerTeam - CategorySum;

    public bool Equals(ArmyComposition other) =>
        UnitsPerTeam == other.UnitsPerTeam &&
        AreCategoryCountsEqual(other.CategoryCounts);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(UnitsPerTeam);
        if (!CategoryCounts.IsDefaultOrEmpty)
        {
            foreach (var count in CategoryCounts)
            {
                hash.Add(count);
            }
        }

        return hash.ToHashCode();
    }

    private bool AreCategoryCountsEqual(ImmutableArray<int> other)
    {
        if (CategoryCounts.IsDefaultOrEmpty || other.IsDefaultOrEmpty)
        {
            return CategoryCounts.IsDefaultOrEmpty == other.IsDefaultOrEmpty;
        }

        return CategoryCounts.AsSpan().SequenceEqual(other.AsSpan());
    }
}

internal enum ArmyCompositionPanelAction
{
    None,
    DistributeEvenly,
    ResetToDefault,
    Cancel,
    Apply,
}

internal enum ArmyCompositionPanelResult
{
    None,
    Cancelled,
    Applied,
}

internal readonly record struct ArmyCompositionInteraction(
    ArmyCompositionPanelResult Result,
    bool PointerConsumed)
{
    public static ArmyCompositionInteraction None =>
        new(ArmyCompositionPanelResult.None, false);
}

internal sealed partial class ArmyCompositionPanel
{
    /// <summary>
    /// One control per category, plus the units-per-team stepper and the four
    /// buttons. Derived rather than written as a literal so growing the
    /// roster cannot leave the last category unreachable by keyboard focus.
    /// </summary>
    internal const int ControlCount =
        ArmyCompositionStepper.CategoryCount + 6;
    internal const int UnitsPerTeamControlIndex =
        ArmyCompositionStepper.CategoryCount;
    internal const int DistributeEvenlyControlIndex =
        UnitsPerTeamControlIndex + 1;
    internal const int ResetToDefaultControlIndex =
        DistributeEvenlyControlIndex + 1;
    internal const int MovementPresetControlIndex =
        ResetToDefaultControlIndex + 1;
    internal const int CancelControlIndex = MovementPresetControlIndex + 1;
    internal const int ApplyControlIndex = CancelControlIndex + 1;

    /// <summary>
    /// Every registered <see cref="MovementPresetId"/>, in enum order. The
    /// player-facing selector on the Army Composition panel; a staged choice,
    /// consumed by <c>ArenaGame.BuildScenario</c> only on the next Full Reset
    /// — see the pressure-interrupt observability design section 2.
    /// <para>
    /// "Every registered" is enforced rather than intended:
    /// <c>ArmyCompositionPanelTests.EveryRegisteredMovementPresetHasAMatchingDisplayName</c>
    /// enumerates <see cref="MovementPresetRegistry"/> and fails if a
    /// registered preset is missing here. It did not always do that, and
    /// <see cref="MovementPresetId.ContingentShapeV12"/> was registered but
    /// absent from this list — and so unreachable by any spectator — until
    /// 2026-08-14. The same omission happened a second time on the same day,
    /// when <see cref="MovementPresetId.CohortLateralSpreadV13"/> was appended
    /// here while V12 was still missing, which is why the test now asks the
    /// registry rather than only checking these two lists against each other.
    /// </para>
    /// </summary>
    internal static readonly IReadOnlyList<MovementPresetId>
        MovementPresetOptions =
        [
            MovementPresetId.IndependentPursuitV1,
            MovementPresetId.PersistentContingentsV2,
            MovementPresetId.PersistentContingentsV3,
            MovementPresetId.PersistentContingentsV4,
            MovementPresetId.PersistentContingentsV5,
            MovementPresetId.EquipmentRelativeFootworkV6,
            MovementPresetId.EquipmentRelativeFootworkV7,
            MovementPresetId.RangedStandoffV8,
            MovementPresetId.MonotoneAllyClearanceV9,
            MovementPresetId.BattlefieldRealismV10,
            MovementPresetId.LastStandEngagementV11,
            MovementPresetId.ContingentShapeV12,
            MovementPresetId.CohortLateralSpreadV13,
            MovementPresetId.EvasiveFootworkV14,
            MovementPresetId.ContingentCohesionBeforeContactV15,
            MovementPresetId.ShieldEncumbranceV16,
        ];

    /// <summary>
    /// Human-readable names for <see cref="MovementPresetOptions"/>, in the
    /// same order — never the bare enum spelling, per the selector's own
    /// display convention.
    /// </summary>
    internal static readonly IReadOnlyList<string> MovementPresetNames =
        [
            "V1 Independent Pursuit",
            "V2 Persistent Contingents",
            "V3 Contingent Close-Latch",
            "V4 Narrowed Cohesion Scan",
            "V5 Rank-Aware Leader Scan",
            "V6 Equipment-Relative Footwork",
            "V7 Pressure Interrupt",
            "V8 Ranged Standoff",
            "V9 Monotone Ally Clearance",
            "V10 Battlefield Realism",
            "V11 Last-Stand Engagement",
            "V12 Contingent Shape",
            "V13 Cohort Lateral Spread",
            "V14 Evasive Footwork",
            "V15 Contingent Cohesion Before Contact",
            "V16 Shield Encumbrance",
        ];

    /// <summary>
    /// One label per roster entry, in declared roster-index order — Datu,
    /// Maharlika, Timawa, Aliping Namamahay, matching combat preset V4's
    /// roster. Pair form only — a cultural identification never appears
    /// without its plain English descriptor (CLAUDE.md section 7). Reused
    /// directly from <see cref="RankLabelCatalog"/> so this panel and the
    /// agent inspector never drift onto different wording for the same rank.
    /// </summary>
    /// <remarks>
    /// Four entries, one per rank, not one per weapon: V4 assigns each rank
    /// exactly one loadout, so a category and a rank coincide (unlike V2,
    /// whose six categories were solo/shielded grip variants of a weapon).
    /// <para>
    /// The preferred label also names the rank's weapon in a second pair
    /// (for example "Datu — Chief · Kampilan — Great Blade"), but that
    /// combined form was measured against the shipped 640px panel and only
    /// the Datu row fits it: at the Label font's conservative 12px/char
    /// advance estimate and the row's 460px label box, the combined form
    /// needs 444px for Datu but 516-612px for the other three rows. Rather
    /// than show three rows in one format and one row in another, every row
    /// falls back uniformly to the rank pair alone, which fits every row with
    /// margin to spare (144-372px of 460px). The weapon identification is not
    /// dropped from the game — it is still shown, pair-form, in the agent
    /// inspector — only from this panel's row label.
    /// </para>
    /// </remarks>
    internal static readonly IReadOnlyList<string> CategoryLabels =
    [
        RankLabelCatalog.Datu.Label,
        RankLabelCatalog.Maharlika.Label,
        RankLabelCatalog.Timawa.Label,
        RankLabelCatalog.AlipingNamamahay.Label,
    ];

    private readonly UiArmyCompositionLayout _metrics;

    /// <summary>
    /// One <see cref="UiSelectorMotion"/> per stepper row's minus/plus arrow
    /// pair — <see cref="ArmyCompositionStepper.CategoryCount"/> category
    /// pairs plus the separate units-per-team pair, so that hovering one
    /// row's arrows never bleeds motion into another row's static ones.
    /// Reused unmodified from the shared selector motion helper (UI-52 T4);
    /// only the arrow-hover channels are read here, never the marker pulse.
    /// </summary>
    private readonly UiSelectorMotion[] _categoryArrowMotion =
        CreateArrowMotionArray();

    private readonly UiSelectorMotion _unitsPerTeamArrowMotion = new();

    /// <summary>
    /// Reused verbatim from <see cref="SettingsChoiceSelector{T}"/> — the same
    /// control the menu's UI-scale and startup-display rows use — rather than
    /// a hand-rolled cycling widget. Only its pure helpers
    /// (<c>GetNext</c>/<c>GetPrevious</c>/<c>GetDisplayName</c>/<c>Draw</c>/
    /// <c>AdvanceMotion</c>/<c>PreviousBounds</c>/<c>NextBounds</c>) are used;
    /// its own self-contained <c>Update</c> is not, because this panel's
    /// focus-then-arrow-key convention differs from the menu's per-selector
    /// dispatch and mixing both would double-handle one input.
    /// </summary>
    private readonly SettingsChoiceSelector<MovementPresetId>
        _movementPresetSelector;

    private readonly UiThemeSelectorLayout _selectorLayout;

    private ArmyComposition _draft;
    private ArmyComposition _saved;
    private MovementPresetId _draftMovementPreset;
    private MovementPresetId _savedMovementPreset;
    private int _focusedControlIndex;

    public ArmyCompositionPanel(
        ArmyComposition saved,
        MovementPresetId savedMovementPreset,
        UiArmyCompositionLayout metrics,
        UiThemeStandards standards)
    {
        ArgumentNullException.ThrowIfNull(standards);
        _saved = saved;
        _draft = saved;
        _savedMovementPreset = savedMovementPreset;
        _draftMovementPreset = savedMovementPreset;
        _metrics = metrics;
        _selectorLayout = standards.Shared.Selector;
        _movementPresetSelector = new SettingsChoiceSelector<MovementPresetId>(
            "MOVEMENT PRESET",
            MovementPresetOptions,
            MovementPresetNames,
            "NEXT FULL RESET",
            standards);
    }

    private static UiSelectorMotion[] CreateArrowMotionArray()
    {
        var motion = new UiSelectorMotion[ArmyCompositionStepper.CategoryCount];
        for (var index = 0; index < motion.Length; index++)
        {
            motion[index] = new UiSelectorMotion();
        }

        return motion;
    }

    public ArmyComposition Draft => _draft;

    public ArmyComposition Saved => _saved;

    public MovementPresetId DraftMovementPreset => _draftMovementPreset;

    public MovementPresetId SavedMovementPreset => _savedMovementPreset;

    /// <summary>
    /// Test seam onto the per-row arrow-hover motion — pure reads of state
    /// the four-argument <c>Update</c> overload already advanced, so a test
    /// can assert a resolved colour without constructing a
    /// <c>SpriteBatch</c>. Not called from <see cref="Draw"/>, which reads
    /// the same <see cref="UiSelectorMotion"/> instances directly; these
    /// accessors exist for tests only.
    /// </summary>
    internal Color GetCategoryMinusArrowColor(
        int categoryIndex,
        UiThemeColors colors) =>
        _categoryArrowMotion[categoryIndex].PreviousArrowColor(colors);

    internal Color GetCategoryPlusArrowColor(
        int categoryIndex,
        UiThemeColors colors) =>
        _categoryArrowMotion[categoryIndex].NextArrowColor(colors);

    internal Color GetUnitsPerTeamMinusArrowColor(UiThemeColors colors) =>
        _unitsPerTeamArrowMotion.PreviousArrowColor(colors);

    internal Color GetUnitsPerTeamPlusArrowColor(UiThemeColors colors) =>
        _unitsPerTeamArrowMotion.NextArrowColor(colors);

    public int Unassigned => _draft.Unassigned;

    /// <summary>
    /// Enabled by either an unassigned-free composition change or a movement
    /// preset change, since the two are independent staged choices sharing
    /// one Apply button. The composition-only static
    /// <see cref="IsApplyEnabled"/> stays untouched underneath — existing
    /// tests call it directly — this only adds the second, independent
    /// reason Apply may light up.
    /// </summary>
    public bool CanApply =>
        _draft.Unassigned == 0 &&
        (!_draft.Equals(_saved) || _draftMovementPreset != _savedMovementPreset);

    public int FocusedControlIndex => _focusedControlIndex;

    public void Open(ArmyComposition saved, MovementPresetId savedMovementPreset)
    {
        _saved = saved;
        _draft = saved;
        _savedMovementPreset = savedMovementPreset;
        _draftMovementPreset = savedMovementPreset;
        _focusedControlIndex = 0;
    }

    public void MoveFocus(int keyboardDirection, int hoveredControlIndex = -1)
    {
        _focusedControlIndex = MenuOverlay.ResolveFocusedControlIndex(
            _focusedControlIndex,
            keyboardDirection,
            hoveredControlIndex,
            ControlCount);
    }

    public void AdjustFocusedValue(int direction, bool isShiftHeld)
    {
        if (_focusedControlIndex == MovementPresetControlIndex)
        {
            _draftMovementPreset = direction switch
            {
                > 0 => _movementPresetSelector.GetNext(_draftMovementPreset),
                < 0 => _movementPresetSelector.GetPrevious(_draftMovementPreset),
                _ => _draftMovementPreset,
            };
            return;
        }

        _draft = AdjustValue(
            _draft,
            _focusedControlIndex,
            direction,
            isShiftHeld);
    }

    public ArmyCompositionInteraction Activate() =>
        PerformAction(ResolveActivatedAction(_focusedControlIndex));

    public ArmyCompositionInteraction PerformAction(
        ArmyCompositionPanelAction action)
    {
        switch (action)
        {
            case ArmyCompositionPanelAction.DistributeEvenly:
            case ArmyCompositionPanelAction.ResetToDefault:
                _draft = _draft with
                {
                    CategoryCounts = ArmyCompositionStepper.DistributeEvenly(
                        _draft.UnitsPerTeam),
                };
                return new ArmyCompositionInteraction(
                    ArmyCompositionPanelResult.None,
                    true);

            case ArmyCompositionPanelAction.Cancel:
                _draft = _saved;
                _draftMovementPreset = _savedMovementPreset;
                return new ArmyCompositionInteraction(
                    ArmyCompositionPanelResult.Cancelled,
                    true);

            case ArmyCompositionPanelAction.Apply:
                if (!CanApply)
                {
                    return new ArmyCompositionInteraction(
                        ArmyCompositionPanelResult.None,
                        true);
                }

                _saved = _draft;
                _savedMovementPreset = _draftMovementPreset;
                return new ArmyCompositionInteraction(
                    ArmyCompositionPanelResult.Applied,
                    true);

            default:
                return ArmyCompositionInteraction.None;
        }
    }

    internal static bool IsApplyEnabled(
        ArmyComposition draft,
        ArmyComposition saved) =>
        draft.Unassigned == 0 && !draft.Equals(saved);

    internal static ArmyCompositionPanelAction ResolveActivatedAction(
        int focusedControlIndex) =>
        focusedControlIndex switch
        {
            DistributeEvenlyControlIndex =>
                ArmyCompositionPanelAction.DistributeEvenly,
            ResetToDefaultControlIndex =>
                ArmyCompositionPanelAction.ResetToDefault,
            CancelControlIndex => ArmyCompositionPanelAction.Cancel,
            ApplyControlIndex => ArmyCompositionPanelAction.Apply,
            _ => ArmyCompositionPanelAction.None,
        };

    internal static ArmyComposition AdjustValue(
        ArmyComposition composition,
        int focusedControlIndex,
        int direction,
        bool isShiftHeld)
    {
        if (focusedControlIndex >= 0 &&
            focusedControlIndex < ArmyCompositionStepper.CategoryCount)
        {
            var updatedCounts = composition.CategoryCounts.SetItem(
                focusedControlIndex,
                ArmyCompositionStepper.AdjustCategory(
                    composition.CategoryCounts[focusedControlIndex],
                    composition.UnitsPerTeam,
                    direction,
                    isShiftHeld));
            return composition with { CategoryCounts = updatedCounts };
        }

        if (focusedControlIndex == UnitsPerTeamControlIndex)
        {
            return composition with
            {
                UnitsPerTeam = ArmyCompositionStepper.AdjustUnitsPerTeam(
                    composition.UnitsPerTeam,
                    direction,
                    isShiftHeld),
            };
        }

        return composition;
    }
}
