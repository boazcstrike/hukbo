using Hukbo.Client.Settings;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Presentation;

/// <summary>
/// The event kind that produced one dust puff (battlefield-environment-
/// design.md, "Dust and disturbed vegetation"). <see cref="DustPuffKind.Death"/>
/// draws as the larger two-rectangle puff; <see cref="DustPuffKind.AttackKick"/>
/// draws as the smaller single-rectangle kick — see
/// <see cref="Hukbo.Client.Rendering.DustGeometry"/>.
/// </summary>
internal enum DustPuffKind
{
    Death,
    AttackKick,
}

/// <summary>
/// One live dust puff, keyed on the authoritative event that spawned it.
/// Fixed-point <see cref="XRaw"/>/<see cref="YRaw"/>, matching
/// <see cref="TrampleMark"/> and every other presentation mark type —
/// converted to a world-unit <c>Vector2</c> only at draw time.
/// </summary>
internal readonly record struct DustPuff(
    long Sequence,
    int XRaw,
    int YRaw,
    DustPuffKind Kind,
    float AgeSeconds)
{
    /// <summary>
    /// PROVISIONAL: sub-second lifetimes (battlefield-environment-design.md,
    /// "Dust and disturbed vegetation"). A death kicks up more ground than a
    /// single footfall, so its puff lingers slightly longer than an attack
    /// kick's.
    /// </summary>
    public float LifetimeSeconds => Kind == DustPuffKind.Death ? 0.40f : 0.22f;
}

/// <summary>
/// Fixed-pool, event-driven dust puffs, matching <see cref="HitEffectSystem"/>'s
/// lifecycle shape (battlefield-environment-design.md, "Dust and disturbed
/// vegetation", R-W4.8 amended to MAY by OD-9). <c>Death</c> spawns one brief
/// puff at the dying agent's end-of-tick position, alongside the trample mark
/// <see cref="TrampleMarkSystem"/> leaves at the same spot. <c>Attack</c> may
/// spawn a small, deterministically throttled kick at the attacker's feet —
/// throttled so dust does not fire on every swing, matching the design's "a
/// small throttled dust kick" framing. <c>Move</c> never spawns anything, the
/// same reason <see cref="TrampleMarkSystem"/> ignores it: it fires for most
/// living agents most ticks. Once an <c>Outcome</c> event is seen, no further
/// puff is added — including on later ticks, since a battle's terminal event
/// still lets the end screen settle — until <see cref="Clear"/> runs for the
/// next round. <see cref="MotionIntensity"/> at <see cref="MotionIntensity.Off"/>
/// suppresses spawning the same way; <see cref="MotionIntensity.Reduced"/>
/// leaves dust unchanged (OD-9's decided setting interaction), reusing
/// <see cref="Hukbo.Client.Rendering.GrassSway"/>'s existing enum rather than
/// inventing a parallel one. Every buffer is a fixed array allocated once
/// here and never grown, so ingest and advance allocate nothing on the heap.
/// </summary>
internal sealed class DustEffectSystem
{
    /// <summary>
    /// Hard cap on live dust puffs (battlefield-environment-design.md, "Dust
    /// and disturbed vegetation").
    /// </summary>
    public const int Capacity = 32;

    /// <summary>
    /// PROVISIONAL: roughly one attack in this many spawns a throttled kick,
    /// so dust reads as occasional footwork punctuation rather than firing on
    /// every swing.
    /// </summary>
    private const int AttackKickThrottleModulus = 6;

    private readonly Dictionary<ulong, AgentView> _agentsById = [];
    private readonly DustPuff[] _puffs;
    private int _count;
    private bool _outcomeReached;
    private MotionIntensity _motionIntensity = MotionIntensity.Full;

    public DustEffectSystem(int capacity = Capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _puffs = new DustPuff[capacity];
    }

    /// <summary>
    /// The spectator's chosen ambient-motion level. Setting this to
    /// <see cref="MotionIntensity.Off"/> suppresses future spawns only —
    /// unlike <see cref="BloodEffectSystem.Intensity"/>, it does not discard
    /// whatever dust is already on screen; existing puffs keep aging out
    /// through <see cref="Advance"/> exactly as they would otherwise (OD-9).
    /// </summary>
    public MotionIntensity MotionIntensity
    {
        get => _motionIntensity;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "Unknown motion intensity.");
            }

            _motionIntensity = value;
        }
    }

    public ReadOnlySpan<DustPuff> ActivePuffs =>
        _puffs.AsSpan(0, _count);

    public void Ingest(
        IReadOnlyList<BattleEvent> events,
        IReadOnlyList<AgentView> agents)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(agents);

        _agentsById.Clear();
        for (var index = 0; index < agents.Count; index++)
        {
            var agent = agents[index];
            _agentsById[agent.EntityId] = agent;
        }

        var spawnAllowed = !_outcomeReached && _motionIntensity != MotionIntensity.Off;

        for (var index = 0; index < events.Count; index++)
        {
            var battleEvent = events[index];
            if (battleEvent.Kind == BattleEventKind.Outcome)
            {
                // Stops new spawns from here on, including every event later
                // in this same batch and every batch that follows, but never
                // retracts a puff already added earlier in this loop — the
                // tick that ends the battle still gets to show its own dust.
                _outcomeReached = true;
                spawnAllowed = false;
                continue;
            }

            if (!spawnAllowed)
            {
                continue;
            }

            switch (battleEvent.Kind)
            {
                case BattleEventKind.Death:
                    if (_agentsById.TryGetValue(
                        battleEvent.SourceEntityId,
                        out var dyingAgent))
                    {
                        Add(new DustPuff(
                            battleEvent.Sequence,
                            dyingAgent.XRaw,
                            dyingAgent.YRaw,
                            DustPuffKind.Death,
                            AgeSeconds: 0f));
                    }

                    break;

                case BattleEventKind.Attack:
                    if (ShouldSpawnAttackKick(battleEvent) &&
                        _agentsById.TryGetValue(
                            battleEvent.SourceEntityId,
                            out var attacker))
                    {
                        Add(new DustPuff(
                            battleEvent.Sequence,
                            attacker.XRaw,
                            attacker.YRaw,
                            DustPuffKind.AttackKick,
                            AgeSeconds: 0f));
                    }

                    break;
            }
        }
    }

    public void Advance(float elapsedSeconds)
    {
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        }

        var writeIndex = 0;
        for (var readIndex = 0; readIndex < _count; readIndex++)
        {
            var advanced = _puffs[readIndex] with
            {
                AgeSeconds = _puffs[readIndex].AgeSeconds + elapsedSeconds,
            };
            if (advanced.AgeSeconds >= advanced.LifetimeSeconds)
            {
                continue;
            }

            _puffs[writeIndex] = advanced;
            writeIndex++;
        }

        Array.Clear(_puffs, writeIndex, _count - writeIndex);
        _count = writeIndex;
    }

    public void Clear()
    {
        Array.Clear(_puffs, 0, _count);
        _count = 0;
        _agentsById.Clear();
        _outcomeReached = false;
    }

    private void Add(DustPuff puff)
    {
        if (_count < _puffs.Length)
        {
            _puffs[_count] = puff;
            _count++;
            return;
        }

        var replacementIndex = 0;
        for (var index = 1; index < _count; index++)
        {
            var candidate = _puffs[index];
            var current = _puffs[replacementIndex];
            if (candidate.AgeSeconds > current.AgeSeconds ||
                (candidate.AgeSeconds == current.AgeSeconds &&
                 candidate.Sequence < current.Sequence))
            {
                replacementIndex = index;
            }
        }

        _puffs[replacementIndex] = puff;
    }

    /// <summary>
    /// Deterministic throttle for the optional attack-kick puff: mixes the
    /// event's own <see cref="BattleEvent.Sequence"/> (globally unique,
    /// already deterministic) with its <see cref="BattleEvent.SourceEntityId"/>
    /// through the same SplitMix64 finalizer
    /// <see cref="Hukbo.Client.Rendering.HitEffectGeometry"/> uses to seed its
    /// shard angle, so two replays of the same seed throttle identically
    /// without drawing from any shared salted stream — a per-event decision
    /// derived entirely from values the event already carries needs no
    /// <see cref="PresentationSalts"/> entry of its own.
    /// </summary>
    private static bool ShouldSpawnAttackKick(BattleEvent battleEvent)
    {
        var seed = Mix((ulong)battleEvent.Sequence + 0x9E3779B97F4A7C15UL) ^
            Mix(battleEvent.SourceEntityId + 0xD1B54A32D192ED03UL);
        return seed % AttackKickThrottleModulus == 0;
    }

    private static ulong Mix(ulong value)
    {
        value ^= value >> 30;
        value *= 0xBF58476D1CE4E5B9UL;
        value ^= value >> 27;
        value *= 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }
}
