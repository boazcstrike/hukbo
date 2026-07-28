using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Presentation;

/// <summary>
/// Accumulates the per-unit, per-faction, and battle-wide statistics behind
/// <see cref="BattleReport"/> from the raw per-tick event stream. Owned by
/// <c>PresentationCoordinator</c>: fed once per tick from <c>IngestTick</c>
/// with the raw per-tick event list (never through <c>BattleEventFeed</c>,
/// which truncates to its last 200 entries), cleared by <c>ResetFor</c>, and
/// turned into an immutable <see cref="BattleReport"/> by
/// <see cref="Snapshot"/> at <c>ProcessTerminal</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Kill credit is inferred, not authoritative.</b> <c>Hukbo.Core</c> does
/// not record who killed whom; a <see cref="BattleEventKind.Death"/> event
/// carries only the victim. On each death this accumulator credits the
/// attacker of the highest-<see cref="BattleEvent.Value"/> landed
/// <see cref="BattleEventKind.Attack"/> event against that same victim in
/// that same tick, ties broken on the lowest attacker
/// <see cref="BattleEvent.SourceEntityId"/>. If no landed attack against the
/// victim was seen in the death's tick, no kill is credited to anyone, the
/// faction total is not incremented, and nothing throws — this heuristic
/// fails safe rather than assuming the case is unreachable under the current
/// damage-and-death coupling in <c>BattleSimulation</c>. A future reader
/// must not mistake this inference for a Core guarantee, which is why the
/// battle report panel discloses it too.
/// </para>
/// <para>
/// <b>Faction, weapon, and shield identity are read from events, not from
/// the agent roster.</b> <see cref="Ingest"/> takes only the raw event list
/// for one tick — it is never handed the agent roster — so a unit's faction,
/// weapon, and shield are captured directly off the events that mention it:
/// an attacker's own <see cref="BattleEvent.FactionId"/>,
/// <see cref="BattleEvent.Weapon"/>, and <see cref="BattleEvent.Shield"/>
/// whenever it lands a blow, and a victim's own
/// <see cref="BattleEvent.FactionId"/> when it dies. A unit seen only as a
/// target — hit but neither attacking nor dying before the battle ends — has
/// its faction inferred as the complement of its attacker's faction. That
/// inference holds only because the game currently fields exactly two
/// factions (0 and 1) and units never attack their own side; it is
/// overwritten the instant authoritative evidence (the unit's own attack or
/// death) is seen. Such a unit's weapon and shield stay unknown and are
/// reported as <c>null</c> rather than a guessed value.
/// </para>
/// </remarks>
internal sealed class BattleReportAccumulator
{
    private readonly Dictionary<ulong, UnitAccumulator> _units = [];
    private BattleAttackHighlight? _firstBlood;
    private BattleAttackHighlight? _decisiveKill;

    /// <summary>
    /// Folds one tick's events into the running counters. Never retains
    /// <paramref name="events"/> beyond this call: the simulation
    /// double-buffers that list, so holding a reference to it across ticks
    /// would be a correctness bug, not merely a style concern.
    /// </summary>
    public void Ingest(IReadOnlyList<BattleEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        foreach (var battleEvent in events)
        {
            switch (battleEvent.Kind)
            {
                case BattleEventKind.Attack:
                    ProcessAttack(battleEvent);
                    break;
                case BattleEventKind.Death:
                    ProcessDeath(battleEvent, events);
                    break;
            }
        }
    }

    /// <summary>
    /// Clears every accumulated counter and highlight. Called on every round
    /// reset, alongside the presentation coordinator's other clears.
    /// </summary>
    public void Clear()
    {
        _units.Clear();
        _firstBlood = null;
        _decisiveKill = null;
    }

    /// <summary>
    /// Materializes the immutable <see cref="BattleReport"/>. Every ordering
    /// below is total, so the result never depends on dictionary iteration
    /// order: the leaderboard sorts on kills descending, then damage dealt
    /// descending, then entity ID ascending, and is capped at ten rows; each
    /// faction's top killer is its highest kill count with the lowest entity
    /// ID breaking ties; the longest survivor is the largest recorded death
    /// tick with the lowest entity ID breaking ties.
    /// </summary>
    public BattleReport Snapshot(long terminalTick)
    {
        var ordered = new List<UnitAccumulator>(_units.Values);
        ordered.Sort(CompareForLeaderboard);

        var leaderboard = new List<UnitReportRow>(Math.Min(10, ordered.Count));
        for (var index = 0; index < ordered.Count && index < 10; index++)
        {
            var unit = ordered[index];
            leaderboard.Add(new UnitReportRow(
                Rank: index + 1,
                EntityId: unit.EntityId,
                FactionId: unit.FactionId,
                Weapon: unit.Weapon,
                Shield: unit.Shield,
                Kills: unit.Kills,
                DamageDealt: unit.DamageDealt,
                DamageTaken: unit.DamageTaken,
                AttacksMade: unit.AttacksMade,
                AttacksLanded: unit.AttacksLanded,
                Accuracy: ComputeAccuracy(unit.AttacksLanded, unit.AttacksMade),
                DeathTick: unit.DeathTick));
        }

        return new BattleReport(
            terminalTick,
            leaderboard,
            BuildFactionTotals(),
            _firstBlood,
            _decisiveKill,
            FindLongestSurvivor());
    }

    private void ProcessAttack(BattleEvent attackEvent)
    {
        if (attackEvent.FactionId is not { } attackerFactionId ||
            attackEvent.Weapon is not { } weapon ||
            attackEvent.Shield is not { } shield ||
            attackEvent.TargetEntityId is not { } victimId)
        {
            // An Attack event always carries all four per BattleEvent's own
            // constructor invariant; this guard is defensive only.
            return;
        }

        var attacker = GetOrCreateUnit(attackEvent.SourceEntityId, attackerFactionId);
        attacker.FactionId = attackerFactionId;
        attacker.Weapon = weapon;
        attacker.Shield = shield;
        attacker.AttacksMade++;

        if (attackEvent.Resolution != AttackResolution.Landed)
        {
            return;
        }

        attacker.AttacksLanded++;
        attacker.DamageDealt += attackEvent.Value;

        var victim = GetOrCreateUnit(victimId, InferOpponentFaction(attackerFactionId));
        victim.DamageTaken += attackEvent.Value;

        _firstBlood ??= new BattleAttackHighlight(
            attackEvent.Tick,
            attackEvent.SourceEntityId,
            attackerFactionId,
            victimId,
            victim.FactionId,
            attackEvent.Value,
            weapon,
            shield);
    }

    /// <param name="tickEvents">
    /// The full batch <see cref="Ingest"/> was called with. Used only to
    /// look up the landed <see cref="BattleEventKind.Attack"/> events against
    /// this same victim in this same tick, per the kill-credit rule
    /// documented on this type. Never retained past this call.
    /// </param>
    private void ProcessDeath(BattleEvent deathEvent, IReadOnlyList<BattleEvent> tickEvents)
    {
        var victimId = deathEvent.SourceEntityId;
        var victim = GetOrCreateUnit(victimId, deathEvent.FactionId ?? 0);
        if (deathEvent.FactionId is { } victimFactionId)
        {
            victim.FactionId = victimFactionId;
        }

        // A duplicate Death for the same entity is not expected, but the
        // first recorded tick wins rather than the last if it somehow
        // occurs.
        victim.DeathTick ??= deathEvent.Tick;

        BattleEvent? winningAttack = null;
        foreach (var candidate in tickEvents)
        {
            if (candidate.Kind != BattleEventKind.Attack ||
                candidate.Tick != deathEvent.Tick ||
                candidate.TargetEntityId != victimId ||
                candidate.Resolution != AttackResolution.Landed)
            {
                continue;
            }

            if (winningAttack is not { } currentBest ||
                candidate.Value > currentBest.Value ||
                (candidate.Value == currentBest.Value &&
                 candidate.SourceEntityId < currentBest.SourceEntityId))
            {
                winningAttack = candidate;
            }
        }

        if (winningAttack is not { } killingBlow)
        {
            // No landed attack against this victim was seen in its death
            // tick. No kill is credited to anyone; nothing throws.
            return;
        }

        var killerFactionId = killingBlow.FactionId ?? InferOpponentFaction(victim.FactionId);
        var killer = GetOrCreateUnit(killingBlow.SourceEntityId, killerFactionId);
        killer.FactionId = killerFactionId;
        killer.Kills++;

        _decisiveKill = new BattleAttackHighlight(
            killingBlow.Tick,
            killingBlow.SourceEntityId,
            killer.FactionId,
            victimId,
            victim.FactionId,
            killingBlow.Value,
            killingBlow.Weapon!.Value,
            killingBlow.Shield!.Value);
    }

    private UnitAccumulator GetOrCreateUnit(ulong entityId, int factionId)
    {
        if (_units.TryGetValue(entityId, out var existing))
        {
            return existing;
        }

        var created = new UnitAccumulator(entityId, factionId);
        _units[entityId] = created;
        return created;
    }

    private List<FactionReportTotals> BuildFactionTotals()
    {
        var factionIds = new List<int>();
        foreach (var unit in _units.Values)
        {
            if (!factionIds.Contains(unit.FactionId))
            {
                factionIds.Add(unit.FactionId);
            }
        }

        factionIds.Sort();

        var factions = new List<FactionReportTotals>(factionIds.Count);
        foreach (var factionId in factionIds)
        {
            var totalKills = 0;
            var totalDamageDealt = 0;
            var attacksMade = 0;
            var attacksLanded = 0;
            var survivors = 0;
            ulong? topKillerEntityId = null;
            var topKillerKills = -1;

            foreach (var unit in _units.Values)
            {
                if (unit.FactionId != factionId)
                {
                    continue;
                }

                totalKills += unit.Kills;
                totalDamageDealt += unit.DamageDealt;
                attacksMade += unit.AttacksMade;
                attacksLanded += unit.AttacksLanded;
                if (unit.DeathTick is null)
                {
                    survivors++;
                }

                if (unit.Kills > topKillerKills ||
                    (unit.Kills == topKillerKills &&
                     topKillerEntityId is { } currentTopKiller &&
                     unit.EntityId < currentTopKiller))
                {
                    topKillerKills = unit.Kills;
                    topKillerEntityId = unit.EntityId;
                }
            }

            factions.Add(new FactionReportTotals(
                factionId,
                totalKills,
                totalDamageDealt,
                ComputeAccuracy(attacksLanded, attacksMade),
                survivors,
                topKillerEntityId,
                Math.Max(0, topKillerKills)));
        }

        return factions;
    }

    private BattleSurvivorHighlight? FindLongestSurvivor()
    {
        BattleSurvivorHighlight? longest = null;
        foreach (var unit in _units.Values)
        {
            if (unit.DeathTick is not { } deathTick)
            {
                continue;
            }

            if (longest is not { } currentLongest ||
                deathTick > currentLongest.DeathTick ||
                (deathTick == currentLongest.DeathTick &&
                 unit.EntityId < currentLongest.EntityId))
            {
                longest = new BattleSurvivorHighlight(unit.EntityId, unit.FactionId, deathTick);
            }
        }

        return longest;
    }

    private static int CompareForLeaderboard(UnitAccumulator left, UnitAccumulator right)
    {
        var byKills = right.Kills.CompareTo(left.Kills);
        if (byKills != 0)
        {
            return byKills;
        }

        var byDamage = right.DamageDealt.CompareTo(left.DamageDealt);
        return byDamage != 0 ? byDamage : left.EntityId.CompareTo(right.EntityId);
    }

    private static double ComputeAccuracy(int attacksLanded, int attacksMade) =>
        attacksMade > 0 ? (double)attacksLanded / attacksMade : 0.0;

    /// <summary>
    /// The opposing faction, valid only because the game currently fields
    /// exactly two factions numbered 0 and 1. Falls back to the input
    /// unchanged for any other value rather than guessing.
    /// </summary>
    private static int InferOpponentFaction(int factionId) =>
        factionId switch
        {
            0 => 1,
            1 => 0,
            _ => factionId,
        };

    private sealed class UnitAccumulator
    {
        public UnitAccumulator(ulong entityId, int factionId)
        {
            EntityId = entityId;
            FactionId = factionId;
        }

        public ulong EntityId { get; }

        public int FactionId { get; set; }

        public WeaponId? Weapon { get; set; }

        public ShieldId? Shield { get; set; }

        public int Kills { get; set; }

        public int DamageDealt { get; set; }

        public int DamageTaken { get; set; }

        public int AttacksMade { get; set; }

        public int AttacksLanded { get; set; }

        public long? DeathTick { get; set; }
    }
}
