namespace Sandata.Core.Navigation;

/// <summary>
/// The integer octile heuristic grid A* (task 21) uses to order its open set
/// by the total key <c>(f, h, nodeIndex)</c>. Design section 7: "<c>f</c>
/// uses the integer octile heuristic, <c>h</c>, with the orthogonal step
/// cost 10 and the diagonal 14." There is no float heuristic anywhere in
/// this project — design section 4: "Heuristics are the integer octile form
/// <c>10 * (max - min) + 14 * min</c>. No float heuristic, ever."
/// </summary>
public static class NavHeuristic
{
    /// <summary>The cost of one orthogonal (east/south/west/north) step.</summary>
    public const int OrthogonalStepCost = 10;

    /// <summary>The cost of one diagonal step.</summary>
    public const int DiagonalStepCost = 14;

    /// <summary>
    /// The octile distance between two cells separated by
    /// <paramref name="deltaX"/> cells on the X axis and
    /// <paramref name="deltaY"/> cells on the Y axis: sort the two
    /// magnitudes into <c>min</c> and <c>max</c>, then charge one diagonal
    /// step per unit of <c>min</c> (covering both axes at once) and one
    /// orthogonal step per remaining unit of <c>max</c>.
    ///
    /// <c>10 * (max - min) + 14 * min</c>. This is an admissible and
    /// consistent estimate of the true grid-A* path cost on an unobstructed
    /// grid — on such a grid it is exact, because the diagonal-first
    /// strategy it describes is the one the search itself takes when nothing
    /// blocks it.
    /// </summary>
    public static int Octile(int deltaX, int deltaY)
    {
        var absDeltaX = Math.Abs(deltaX);
        var absDeltaY = Math.Abs(deltaY);
        var min = Math.Min(absDeltaX, absDeltaY);
        var max = Math.Max(absDeltaX, absDeltaY);

        checked
        {
            return (OrthogonalStepCost * (max - min)) + (DiagonalStepCost * min);
        }
    }

    /// <summary>
    /// The octile distance between cell (<paramref name="fromX"/>,
    /// <paramref name="fromY"/>) and cell (<paramref name="toX"/>,
    /// <paramref name="toY"/>). Equivalent to
    /// <c>Octile(toX - fromX, toY - fromY)</c>.
    /// </summary>
    public static int Octile(int fromX, int fromY, int toX, int toY) =>
        Octile(toX - fromX, toY - fromY);
}
