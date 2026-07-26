using System.Collections.Immutable;
using System.Globalization;
using Hukbo.Client.Theming;
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
    internal const int ControlCount = 9;
    internal const int UnitsPerTeamControlIndex =
        ArmyCompositionStepper.CategoryCount;
    internal const int DistributeEvenlyControlIndex =
        UnitsPerTeamControlIndex + 1;
    internal const int ResetToDefaultControlIndex =
        DistributeEvenlyControlIndex + 1;
    internal const int CancelControlIndex = ResetToDefaultControlIndex + 1;
    internal const int ApplyControlIndex = CancelControlIndex + 1;

    /// <summary>
    /// Plain descriptors only, in declared roster-index order. Never a
    /// cultural weapon identification (CLAUDE.md §7).
    /// </summary>
    internal static readonly IReadOnlyList<string> CategoryLabels =
    [
        "Great Blade",
        "Heavy Chopper",
        "Thrusting Blade",
        "Work Blade",
    ];

    private readonly UiArmyCompositionLayout _metrics;
    private ArmyComposition _draft;
    private ArmyComposition _saved;
    private int _focusedControlIndex;

    public ArmyCompositionPanel(
        ArmyComposition saved,
        UiArmyCompositionLayout metrics)
    {
        _saved = saved;
        _draft = saved;
        _metrics = metrics;
    }

    public ArmyComposition Draft => _draft;

    public ArmyComposition Saved => _saved;

    public int Unassigned => _draft.Unassigned;

    public bool CanApply => IsApplyEnabled(_draft, _saved);

    public int FocusedControlIndex => _focusedControlIndex;

    public void Open(ArmyComposition saved)
    {
        _saved = saved;
        _draft = saved;
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
