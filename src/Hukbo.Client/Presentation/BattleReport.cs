using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Presentation;

/// <summary>
/// One row of the battle report leaderboard, already ranked and capped at
/// ten by <see cref="BattleReportAccumulator.Snapshot"/>. <see cref="Weapon"/>
/// and <see cref="Shield"/> are <c>null</c> for a unit that was never seen
/// landing an attack — most often one that died before ever swinging — and
/// the panel omits the weapon label for that row rather than guessing one.
/// </summary>
internal readonly record struct UnitReportRow(
    int Rank,
    ulong EntityId,
    int FactionId,
    WeaponId? Weapon,
    ShieldId? Shield,
    int Kills,
    int DamageDealt,
    int DamageTaken,
    int AttacksMade,
    int AttacksLanded,
    double Accuracy,
    long? DeathTick);

/// <summary>
/// One faction's aggregate totals, plus that faction's top killer. Present
/// once per faction observed in the battle, ordered by ascending
/// <see cref="FactionId"/> in <see cref="BattleReport.Factions"/>.
/// </summary>
/// <remarks>
/// <b>Two classes of number live in this record, and the distinction is
/// load-bearing.</b> <see cref="Combat"/>, and the <see cref="Accuracy"/>
/// derived from it, are the simulation's own authoritative per-tick counters
/// summed across the battle — they cannot disagree with what happened.
/// <see cref="TotalKills"/>, <see cref="TotalDamageDealt"/>,
/// <see cref="Survivors"/>, <see cref="TopKillerEntityId"/>, and
/// <see cref="TopKillerKills"/> are derived presentation-side from the event
/// stream and rest on a kill-attribution heuristic, because
/// <c>Hukbo.Core</c> tracks no per-entity counters. A renderer must not present
/// the second group as carrying the authority of the first.
/// </remarks>
internal readonly record struct FactionReportTotals(
    int FactionId,
    int TotalKills,
    int TotalDamageDealt,
    double Accuracy,
    int Survivors,
    ulong? TopKillerEntityId,
    int TopKillerKills,
    CombatMetrics Combat);

/// <summary>
/// One landed attack singled out as a battle highlight — either the first
/// landed blow of the battle (<see cref="BattleReport.FirstBlood"/>) or the
/// most recent credited kill (<see cref="BattleReport.DecisiveKill"/>).
/// </summary>
internal readonly record struct BattleAttackHighlight(
    long Tick,
    ulong AttackerEntityId,
    int AttackerFactionId,
    ulong VictimEntityId,
    int VictimFactionId,
    int Value,
    WeaponId Weapon,
    ShieldId Shield);

/// <summary>
/// The unit that held out the longest before dying, by recorded death tick.
/// A unit that survives the whole battle is not eligible — it is already
/// represented by the match summary's winner and survivor count, so this
/// highlight is about the losing side's toughest fight.
/// </summary>
internal readonly record struct BattleSurvivorHighlight(
    ulong EntityId,
    int FactionId,
    long DeathTick);

/// <summary>
/// The immutable snapshot produced by <see cref="BattleReportAccumulator.Snapshot"/>
/// once a battle reaches a terminal outcome. Every field is presentation-only
/// derived data: nothing here is fed back into the simulation and nothing
/// here reaches the state hash or the event hash.
/// </summary>
internal sealed record BattleReport(
    long TerminalTick,
    IReadOnlyList<UnitReportRow> Leaderboard,
    IReadOnlyList<FactionReportTotals> Factions,
    BattleAttackHighlight? FirstBlood,
    BattleAttackHighlight? DecisiveKill,
    BattleSurvivorHighlight? LongestSurvivor);
