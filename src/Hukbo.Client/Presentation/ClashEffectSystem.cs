using Hukbo.Core.Simulation;

namespace Hukbo.Client.Presentation;

/// <summary>
/// Fixed-capacity pool of clash effects, copying the shape of
/// <see cref="HitEffectSystem"/>.
/// </summary>
/// <remarks>
/// <b>No-op stub.</b> It places nothing and expires nothing yet. It exists so
/// that three client workstreams can write into one shared test assembly
/// without a file referencing a type that does not exist and failing the whole
/// assembly to compile.
/// </remarks>
internal sealed class ClashEffectSystem
{
    private readonly ClashEffect[] _effects;
    private int _count;

    public ClashEffectSystem(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _effects = new ClashEffect[capacity];
    }

    public ReadOnlySpan<ClashEffect> ActiveEffects => _effects.AsSpan(0, _count);

    /// <summary>
    /// Places one effect at the contact midpoint for every attack event that
    /// resolved to a contact outcome and whose attacker and target are both
    /// present in the supplied views.
    /// </summary>
    public void Ingest(
        IReadOnlyList<BattleEvent> events,
        IReadOnlyList<AgentView> agents)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(agents);
    }

    public void Advance(float elapsedSeconds)
    {
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        }
    }

    public void Clear()
    {
        Array.Clear(_effects, 0, _count);
        _count = 0;
    }
}
