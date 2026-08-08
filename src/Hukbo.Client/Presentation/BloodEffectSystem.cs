using Hukbo.Client.Settings;
using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Presentation;

/// <summary>
/// Keyed on the authoritative <c>Attack</c> event, alongside
/// <see cref="HitEffectSystem"/>, which stays keyed on <c>Damage</c>. Every
/// buffer is a fixed array allocated once here and never grown, so ingest and
/// advance allocate nothing on the heap.
/// </summary>
/// <remarks>
/// <para>
/// A victim struck twice in one tick therefore shows one impact ring and two
/// sprays. That is intended: the simulation names no killing blow, so every
/// blow on a victim who dies this tick renders at the lethal tier rather than
/// awarding the kill to one attacker.
/// </para>
/// <para>
/// Because it keys on the attack rather than on the damage, it has to read the
/// resolution itself: only a landed blow draws blood.
/// </para>
/// </remarks>
internal sealed class BloodEffectSystem
{
    private readonly Dictionary<ulong, AgentView> _agentsById = [];
    private readonly HashSet<ulong> _deathEntityIds = [];
    private readonly BloodBurst[] _bursts;
    private readonly GroundMark[] _groundMarks;
    private readonly LethalSpurt[] _spurts;
    private GoreIntensity _intensity = GoreIntensity.Stylized;
    private int _burstCount;
    private int _groundMarkCount;
    private int _spurtCount;

    public BloodEffectSystem(
        int burstCapacity = 256,
        int groundMarkCapacity = 384,
        int spurtCapacity = 32)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(burstCapacity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(groundMarkCapacity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(spurtCapacity);
        _bursts = new BloodBurst[burstCapacity];
        _groundMarks = new GroundMark[groundMarkCapacity];
        _spurts = new LethalSpurt[spurtCapacity];
    }

    /// <summary>
    /// The spectator's chosen gore level. Setting this to
    /// <see cref="GoreIntensity.Off"/> also discards whatever is already on
    /// screen, so the promise that no slot is occupied at Off holds even when
    /// the level is changed mid-round from the menu.
    /// </summary>
    public GoreIntensity Intensity
    {
        get => _intensity;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "Unknown gore intensity.");
            }

            _intensity = value;
            if (value == GoreIntensity.Off)
            {
                Clear();
            }
        }
    }

    public ReadOnlySpan<BloodBurst> ActiveBursts =>
        _bursts.AsSpan(0, _burstCount);

    public ReadOnlySpan<GroundMark> ActiveGroundMarks =>
        _groundMarks.AsSpan(0, _groundMarkCount);

    public ReadOnlySpan<LethalSpurt> ActiveSpurts =>
        _spurts.AsSpan(0, _spurtCount);

    /// <summary>
    /// Starts blood owned by one atomic landed contact. Lethal classification
    /// comes from the dispatcher-owned bundle rather than from a later Death
    /// scan, so only the highest-sequence lethal contributor gets a spurt.
    /// </summary>
    public void StartContact(
        AttackContactBundle contact,
        AgentView attacker,
        AgentView defender)
    {
        if (_intensity == GoreIntensity.Off ||
            contact.Resolution != AttackResolution.Landed)
        {
            return;
        }

        var direction = ResolveDirection(attacker, defender);
        AddBlow(
            contact.Sequence,
            contact.AttackerEntityId,
            contact.DefenderEntityId,
            contact.Damage,
            contact.Weapon,
            contact.HitLocation,
            contact.IsLethal,
            defender,
            direction);
    }

    public void Ingest(
        IReadOnlyList<BattleEvent> events,
        IReadOnlyList<AgentView> agents)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(agents);

        if (_intensity == GoreIntensity.Off)
        {
            return;
        }

        _agentsById.Clear();
        for (var index = 0; index < agents.Count; index++)
        {
            var agent = agents[index];
            _agentsById[agent.EntityId] = agent;
        }

        _deathEntityIds.Clear();
        for (var index = 0; index < events.Count; index++)
        {
            var battleEvent = events[index];
            if (battleEvent.Kind == BattleEventKind.Death)
            {
                _deathEntityIds.Add(battleEvent.SourceEntityId);
            }
        }

        for (var index = 0; index < events.Count; index++)
        {
            var battleEvent = events[index];

            // This system keys on the Attack event rather than on damage, so
            // the resolution is the only thing separating a blow that reached
            // the defender from one the shield, the weapon, or the footwork
            // turned aside. Without this clause every parried blow sprays.
            if (battleEvent.Kind != BattleEventKind.Attack ||
                battleEvent.Resolution is not AttackResolution.Landed ||
                battleEvent.TargetEntityId is not { } targetEntityId ||
                battleEvent.Weapon is not { } weapon ||
                battleEvent.HitLocation is not { } hitLocation ||
                !_agentsById.TryGetValue(targetEntityId, out var victim))
            {
                continue;
            }

            AddBlow(battleEvent, targetEntityId, weapon, hitLocation, victim);
        }
    }

    public void Advance(float elapsedSeconds)
    {
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        }

        if (_intensity == GoreIntensity.Off)
        {
            return;
        }

        _burstCount = AdvanceBuffer(_bursts, _burstCount, elapsedSeconds);
        _groundMarkCount = AdvanceBuffer(
            _groundMarks,
            _groundMarkCount,
            elapsedSeconds);
        _spurtCount = AdvanceBuffer(_spurts, _spurtCount, elapsedSeconds);
    }

    public void Clear()
    {
        Array.Clear(_bursts, 0, _burstCount);
        Array.Clear(_groundMarks, 0, _groundMarkCount);
        Array.Clear(_spurts, 0, _spurtCount);
        _burstCount = 0;
        _groundMarkCount = 0;
        _spurtCount = 0;
        _agentsById.Clear();
        _deathEntityIds.Clear();
    }

    private void AddBlow(
        BattleEvent battleEvent,
        ulong targetEntityId,
        WeaponId weapon,
        BodyPart hitLocation,
        AgentView victim)
    {
        var direction = ResolveDirection(battleEvent.SourceEntityId, victim);
        var isLethal = _deathEntityIds.Contains(targetEntityId);
        AddBlow(
            battleEvent.Sequence,
            battleEvent.SourceEntityId,
            targetEntityId,
            battleEvent.Value,
            weapon,
            hitLocation,
            isLethal,
            victim,
            direction);
    }

    private void AddBlow(
        long sequence,
        ulong sourceEntityId,
        ulong targetEntityId,
        int damage,
        WeaponId weapon,
        BodyPart hitLocation,
        bool isLethal,
        AgentView victim,
        (float X, float Y) direction)
    {
        var severityRatio = ResolveSeverityRatio(
            damage,
            victim.MaximumHitPoints);
        var isDense = _intensity == GoreIntensity.Full;

        _burstCount = Add(
            _bursts,
            _burstCount,
            new BloodBurst(
                sequence,
                sourceEntityId,
                targetEntityId,
                victim.XRaw,
                victim.YRaw,
                direction.X,
                direction.Y,
                weapon,
                hitLocation,
                severityRatio,
                isLethal,
                AgeSeconds: 0f));
        _groundMarkCount = Add(
            _groundMarks,
            _groundMarkCount,
            new GroundMark(
                sequence,
                victim.XRaw,
                victim.YRaw,
                isLethal,
                isDense,
                AgeSeconds: 0f));

        if (!isLethal || !isDense)
        {
            return;
        }

        _spurtCount = Add(
            _spurts,
            _spurtCount,
            new LethalSpurt(
                sequence,
                sourceEntityId,
                targetEntityId,
                victim.XRaw,
                victim.YRaw,
                direction.X,
                direction.Y,
                severityRatio,
                AgeSeconds: 0f));
    }

    private static (float X, float Y) ResolveDirection(
        AgentView attacker,
        AgentView victim)
    {
        var deltaX = (float)(victim.XRaw - attacker.XRaw);
        var deltaY = (float)(victim.YRaw - attacker.YRaw);
        var length = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        return length > 0f ? (deltaX / length, deltaY / length) : (0f, 0f);
    }

    /// <summary>
    /// The unit vector from the attacker to the victim. Movement commits
    /// strictly before attacks resolve, so both end-of-tick positions are the
    /// positions at the instant the blow landed. Returns the zero vector when
    /// the attacker is unresolvable or the two share a position; the geometry
    /// falls back to a seeded angle in that case.
    /// </summary>
    private (float X, float Y) ResolveDirection(
        ulong sourceEntityId,
        AgentView victim)
    {
        if (!_agentsById.TryGetValue(sourceEntityId, out var attacker))
        {
            return (0f, 0f);
        }

        var deltaX = (float)(victim.XRaw - attacker.XRaw);
        var deltaY = (float)(victim.YRaw - attacker.YRaw);
        var length = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        return length > 0f ? (deltaX / length, deltaY / length) : (0f, 0f);
    }

    /// <summary>
    /// Clamped so that an overkill final blow, which can exceed the victim's
    /// remaining hit points by a wide margin, does not produce an outsized
    /// spray.
    /// </summary>
    private static float ResolveSeverityRatio(
        int damage,
        int maximumHitPoints) =>
        maximumHitPoints <= 0
            ? 0f
            : Math.Clamp(damage / (float)maximumHitPoints, 0f, 1f);

    /// <summary>
    /// Appends to a fixed buffer, or overwrites the oldest entry once it is
    /// full, breaking age ties on the lowest sequence. Shared by all three
    /// buffers; the struct constraint keeps the interface calls unboxed.
    /// </summary>
    private static int Add<T>(T[] buffer, int count, T effect)
        where T : struct, IBloodEffect<T>
    {
        if (count < buffer.Length)
        {
            buffer[count] = effect;
            return count + 1;
        }

        var replacementIndex = 0;
        for (var index = 1; index < count; index++)
        {
            var candidate = buffer[index];
            var current = buffer[replacementIndex];
            if (candidate.AgeSeconds > current.AgeSeconds ||
                (candidate.AgeSeconds == current.AgeSeconds &&
                 candidate.Sequence < current.Sequence))
            {
                replacementIndex = index;
            }
        }

        buffer[replacementIndex] = effect;
        return count;
    }

    /// <summary>
    /// Ages every entry on unscaled presentation time and compacts the buffer
    /// in place with a read index and a write index, clearing the tail so no
    /// expired record is retained.
    /// </summary>
    private static int AdvanceBuffer<T>(
        T[] buffer,
        int count,
        float elapsedSeconds)
        where T : struct, IBloodEffect<T>
    {
        var writeIndex = 0;
        for (var readIndex = 0; readIndex < count; readIndex++)
        {
            var advanced = buffer[readIndex].WithAge(
                buffer[readIndex].AgeSeconds + elapsedSeconds);
            if (advanced.AgeSeconds >= advanced.LifetimeSeconds)
            {
                continue;
            }

            buffer[writeIndex] = advanced;
            writeIndex++;
        }

        Array.Clear(buffer, writeIndex, count - writeIndex);
        return writeIndex;
    }
}
