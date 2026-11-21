using Hukbo.Core.Simulation;

namespace Hukbo.Client.Presentation;

/// <summary>
/// Positions to draw pawns at between simulation ticks: each pawn sits between
/// where it stood one tick ago and where it stands now (play-rate governor
/// design, sections 4.1 to 4.3).
/// </summary>
/// <remarks>
/// <para>
/// Pure and clockless. It owns flat previous and current position arrays indexed
/// like the agent view list, and reads nothing but <see cref="AgentView"/>.
/// </para>
/// <para>
/// <b>Usage.</b> Call <see cref="Reset"/> when a battle is created, call
/// <see cref="Capture"/> after every <c>AdvanceOneTick</c>, and call
/// <see cref="Resolve"/> per agent each frame with the accumulator fraction left
/// after the tick loop. A frame that runs several ticks captures several times,
/// so only the last tick's pair survives; a frame that runs none captures
/// nothing, so paused pawns hold still.
/// </para>
/// <para>
/// <b>Drawing only.</b> Gait animation, event effects, and projectiles read the
/// authoritative tick positions, never these (design section 4.2).
/// </para>
/// <para>
/// The arrays are reused across frames and only ever grow, so steady-state
/// <see cref="Capture"/> and <see cref="Resolve"/> allocate nothing.
/// </para>
/// </remarks>
internal sealed class RenderPositionInterpolator
{
    private int[] _previousX = [];
    private int[] _previousY = [];
    private int[] _currentX = [];
    private int[] _currentY = [];

    /// <summary>The number of agents captured by the last <see cref="Reset"/> or <see cref="Capture"/>.</summary>
    public int Count { get; private set; }

    /// <summary>
    /// Loads the agents' positions as both the previous and the current
    /// position, so nothing slides in from a stale or empty history. Call on
    /// battle creation and on restart.
    /// </summary>
    public void Reset(IReadOnlyList<AgentView> agents)
    {
        ArgumentNullException.ThrowIfNull(agents);

        PrepareCurrent(agents.Count);
        for (var index = 0; index < agents.Count; index++)
        {
            _currentX[index] = agents[index].XRaw;
            _currentY[index] = agents[index].YRaw;
        }

        CopyCurrentToPrevious();
    }

    /// <summary>
    /// Moves the current positions to previous and loads the agents' positions
    /// as current. Call once after each simulation tick. A corpse is held where
    /// it lies, so previous equals current for it. If the agent count differs
    /// from the last call, indices no longer line up, so history is dropped as
    /// in <see cref="Reset"/>.
    /// </summary>
    public void Capture(IReadOnlyList<AgentView> agents)
    {
        ArgumentNullException.ThrowIfNull(agents);

        if (agents.Count != Count)
        {
            Reset(agents);
            return;
        }

        (_previousX, _currentX) = (_currentX, _previousX);
        (_previousY, _currentY) = (_currentY, _previousY);
        for (var index = 0; index < agents.Count; index++)
        {
            var agent = agents[index];
            _currentX[index] = agent.XRaw;
            _currentY[index] = agent.YRaw;
            if (!agent.IsAlive)
            {
                _previousX[index] = agent.XRaw;
                _previousY[index] = agent.YRaw;
            }
        }
    }

    /// <summary>
    /// The raw position to draw agent <paramref name="index"/> at.
    /// </summary>
    /// <param name="index">Index into the agent view list. Below <see cref="Count"/>.</param>
    /// <param name="alpha">
    /// The fraction of a tick elapsed since the last one, clamped to [0, 1]: 0
    /// returns the previous position exactly and 1 the current position exactly.
    /// </param>
    public (int XRaw, int YRaw) Resolve(int index, double alpha)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);

        var fraction = Math.Clamp(alpha, 0d, 1d);
        return (
            Lerp(_previousX[index], _currentX[index], fraction),
            Lerp(_previousY[index], _currentY[index], fraction));
    }

    private static int Lerp(int previous, int current, double fraction) =>
        (int)(previous + (long)Math.Round(((long)current - previous) * fraction));

    private void PrepareCurrent(int count)
    {
        if (count > _currentX.Length)
        {
            _previousX = new int[count];
            _previousY = new int[count];
            _currentX = new int[count];
            _currentY = new int[count];
        }

        Count = count;
    }

    private void CopyCurrentToPrevious()
    {
        Array.Copy(_currentX, _previousX, Count);
        Array.Copy(_currentY, _previousY, Count);
    }
}
