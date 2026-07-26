using Hukbo.Core.Simulation;

namespace Hukbo.Client.Presentation;

/// <summary>
/// Holds at most one in-flight swing per agent, in a fixed-capacity array
/// sized at construction. An agent cannot accumulate swings and the array
/// cannot grow.
/// </summary>
/// <remarks>
/// <b>No-op stub.</b> It stores nothing and expires nothing yet. It exists so
/// that three client workstreams can write into one shared test assembly
/// without a file referencing a type that does not exist and failing the whole
/// assembly to compile.
/// </remarks>
internal sealed class SwingAnimationSystem
{
    private readonly SwingAnimation[] _swings;
    private int _count;

    public SwingAnimationSystem(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _swings = new SwingAnimation[capacity];
    }

    public ReadOnlySpan<SwingAnimation> ActiveSwings => _swings.AsSpan(0, _count);

    /// <summary>
    /// Starts a swing for every attack event whose attacker and target are
    /// both present in the supplied views, replacing any swing already in
    /// flight for that attacker.
    /// </summary>
    public void Ingest(
        IReadOnlyList<BattleEvent> events,
        IReadOnlyList<AgentView> agents)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(agents);
    }

    /// <summary>
    /// Advances every in-flight swing by speed-scaled presentation seconds and
    /// drops the ones that have run their full duration.
    /// </summary>
    public void Advance(float elapsedSeconds)
    {
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        }
    }

    /// <summary>
    /// Fetches the swing in flight for one agent, if any.
    /// </summary>
    public bool TryGetSwing(ulong entityId, out SwingAnimation swing)
    {
        for (var index = 0; index < _count; index++)
        {
            if (_swings[index].AttackerEntityId != entityId)
            {
                continue;
            }

            swing = _swings[index];
            return true;
        }

        swing = default;
        return false;
    }

    public void Clear()
    {
        Array.Clear(_swings, 0, _count);
        _count = 0;
    }
}
