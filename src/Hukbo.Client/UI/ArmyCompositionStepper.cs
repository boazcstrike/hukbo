using System.Collections.Immutable;

namespace Hukbo.Client.UI;

/// <summary>
/// Pure clamp-and-step arithmetic for the Army Composition panel's steppers.
/// Category steppers CLAMP at their bounds and never wrap. This is
/// deliberately the opposite of <see cref="UiThemeSelector"/>'s modulo
/// carousel: wrapping a soldier count from its maximum back to zero would
/// silently destroy a deliberate allocation.
/// </summary>
internal static class ArmyCompositionStepper
{
    internal const int CategoryCount = 6;

    internal const int MinimumUnitsPerTeam = 4;

    /// <summary>
    /// Evidence-backed ceiling: <c>benchmark.ps1 -Agents 500</c> routes
    /// through <c>Scenario.CreateDefault</c>, which halves to
    /// <c>AgentsPerFaction = 250</c>. 500 per team was never measured.
    /// </summary>
    internal const int MaximumUnitsPerTeam = 250;

    internal const int CategoryStep = 1;

    internal const int CategoryShiftStep = 10;

    internal const int UnitsPerTeamStep = 10;

    internal const int UnitsPerTeamShiftStep = 50;

    internal static int ClampCategory(int value, int unitsPerTeam) =>
        Math.Clamp(value, 0, Math.Max(0, unitsPerTeam));

    internal static int ClampUnitsPerTeam(int value) =>
        Math.Clamp(value, MinimumUnitsPerTeam, MaximumUnitsPerTeam);

    internal static int AdjustCategory(
        int value,
        int unitsPerTeam,
        int direction,
        bool isShiftHeld)
    {
        var step = isShiftHeld ? CategoryShiftStep : CategoryStep;
        return ClampCategory(
            value + (Math.Sign(direction) * step),
            unitsPerTeam);
    }

    internal static int AdjustUnitsPerTeam(
        int value,
        int direction,
        bool isShiftHeld)
    {
        var step = isShiftHeld ? UnitsPerTeamShiftStep : UnitsPerTeamStep;
        return ClampUnitsPerTeam(value + (Math.Sign(direction) * step));
    }

    internal static bool IsCategoryDecrementDisabled(int value) =>
        value <= 0;

    internal static bool IsCategoryIncrementDisabled(
        int value,
        int unitsPerTeam) =>
        value >= unitsPerTeam;

    internal static bool IsUnitsPerTeamDecrementDisabled(int value) =>
        value <= MinimumUnitsPerTeam;

    internal static bool IsUnitsPerTeamIncrementDisabled(int value) =>
        value >= MaximumUnitsPerTeam;

    /// <summary>
    /// Splits <paramref name="total"/> across <see cref="CategoryCount"/>
    /// categories, giving the remainder to the earliest indices. This is
    /// exactly the distribution the round-robin loadout assignment
    /// <c>(entityId - 1) % CategoryCount</c> already produces for the same
    /// total, which is why Reset to Default and Distribute Evenly are one
    /// operation rather than two rules that could drift apart.
    /// </summary>
    internal static ImmutableArray<int> DistributeEvenly(int total)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(total);

        var baseCount = total / CategoryCount;
        var remainder = total % CategoryCount;
        var builder = ImmutableArray.CreateBuilder<int>(CategoryCount);
        for (var index = 0; index < CategoryCount; index++)
        {
            builder.Add(baseCount + (index < remainder ? 1 : 0));
        }

        return builder.MoveToImmutable();
    }
}
