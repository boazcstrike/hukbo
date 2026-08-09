using Hukbo.Client.Presentation;
using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Tests;

public sealed class BattleReportAccumulatorTests
{
    [Fact]
    public void Ingest_CreditsKillToHighestDamageLandedAttacker()
    {
        var accumulator = new BattleReportAccumulator();

        // The higher-value attack is listed first and the lower-value one
        // second, so a naive "credit whichever landed attack was processed
        // last" implementation would wrongly credit entity 10 here.
        var events = new[]
        {
            Attack(seq: 1, tick: 1, source: 20, target: 500, damage: 9, factionId: 0),
            Attack(seq: 2, tick: 1, source: 10, target: 500, damage: 3, factionId: 0),
            Death(seq: 3, tick: 1, source: 500, factionId: 1),
        };

        accumulator.Ingest(events, default);
        var report = accumulator.Snapshot(terminalTick: 1);

        var killer = Assert.Single(report.Leaderboard, row => row.Kills == 1);
        Assert.Equal(20UL, killer.EntityId);
    }

    [Fact]
    public void Ingest_TiesKillCreditByLowestAttackerEntityId()
    {
        var accumulator = new BattleReportAccumulator();
        var events = new[]
        {
            Attack(seq: 1, tick: 1, source: 5, target: 500, damage: 10, factionId: 0),
            Attack(seq: 2, tick: 1, source: 3, target: 500, damage: 10, factionId: 0),
            Death(seq: 3, tick: 1, source: 500, factionId: 1),
        };

        accumulator.Ingest(events, default);
        var report = accumulator.Snapshot(terminalTick: 1);

        var killer = Assert.Single(report.Leaderboard, row => row.Kills == 1);
        Assert.Equal(3UL, killer.EntityId);
    }

    [Fact]
    public void Ingest_RecordsFirstBloodOnceAndNeverOverwritesIt()
    {
        var accumulator = new BattleReportAccumulator();

        accumulator.Ingest(new[]
        {
            Attack(seq: 1, tick: 1, source: 1, target: 100, damage: 5, factionId: 0),
        }, default);
        accumulator.Ingest(new[]
        {
            Attack(seq: 2, tick: 2, source: 2, target: 101, damage: 20, factionId: 0),
        }, default);

        var report = accumulator.Snapshot(terminalTick: 2);

        Assert.NotNull(report.FirstBlood);
        Assert.Equal(1L, report.FirstBlood!.Value.Tick);
        Assert.Equal(1UL, report.FirstBlood.Value.AttackerEntityId);
        Assert.Equal(5, report.FirstBlood.Value.Value);
    }

    [Fact]
    public void Ingest_DecisiveKillTracksTheMostRecentCreditedKill()
    {
        var accumulator = new BattleReportAccumulator();

        accumulator.Ingest(new[]
        {
            Attack(seq: 1, tick: 1, source: 1, target: 100, damage: 10, factionId: 0),
            Death(seq: 2, tick: 1, source: 100, factionId: 1),
        }, default);
        accumulator.Ingest(new[]
        {
            Attack(seq: 3, tick: 2, source: 2, target: 101, damage: 5, factionId: 0),
            Death(seq: 4, tick: 2, source: 101, factionId: 1),
        }, default);

        var report = accumulator.Snapshot(terminalTick: 2);

        Assert.NotNull(report.DecisiveKill);
        Assert.Equal(2L, report.DecisiveKill!.Value.Tick);
        Assert.Equal(2UL, report.DecisiveKill.Value.AttackerEntityId);
        Assert.Equal(101UL, report.DecisiveKill.Value.VictimEntityId);
    }

    [Fact]
    public void Ingest_DeathWithoutALandedAttackLeavesKillUncreditedAndDoesNotThrow()
    {
        var accumulator = new BattleReportAccumulator();

        var exception = Record.Exception(() =>
            accumulator.Ingest(new[]
            {
                Death(seq: 1, tick: 1, source: 100, factionId: 1),
            }, default));

        Assert.Null(exception);

        var report = accumulator.Snapshot(terminalTick: 1);

        var victimRow = Assert.Single(report.Leaderboard);
        Assert.Equal(100UL, victimRow.EntityId);
        Assert.Equal(0, victimRow.Kills);
        Assert.Equal(1L, victimRow.DeathTick);

        var factionTotals = Assert.Single(report.Factions);
        Assert.Equal(0, factionTotals.TotalKills);
    }

    [Fact]
    public void Ingest_AccumulatesPerFactionTotals()
    {
        var accumulator = new BattleReportAccumulator();

        // Kills, damage, survivors, and the top killer stay derived from the
        // events. The attack counts and accuracy do not: they are the
        // simulation's own per-tick counters, injected here to match what the
        // events describe — faction 0 lands one of two attacks, faction 1
        // lands its only one.
        accumulator.Ingest(
            new[]
            {
                Attack(seq: 1, tick: 1, source: 1, target: 100, damage: 10, factionId: 0),
                Death(seq: 2, tick: 1, source: 100, factionId: 1),
            },
            new FactionCombatMetrics(
                new CombatMetrics(1, 1, 0, 0, 0, 0),
                default));
        accumulator.Ingest(
            new[]
            {
                Attack(
                    seq: 3,
                    tick: 2,
                    source: 1,
                    target: 100,
                    damage: 0,
                    factionId: 0,
                    resolution: AttackResolution.Evaded),
            },
            new FactionCombatMetrics(
                new CombatMetrics(1, 0, 0, 0, 0, 1),
                default));
        accumulator.Ingest(
            new[]
            {
                Attack(seq: 4, tick: 3, source: 2, target: 1, damage: 5, factionId: 1),
            },
            new FactionCombatMetrics(
                default,
                new CombatMetrics(1, 1, 0, 0, 0, 0)));

        var report = accumulator.Snapshot(terminalTick: 3);

        Assert.Equal(2, report.Factions.Count);
        var factionZero = report.Factions.Single(faction => faction.FactionId == 0);
        Assert.Equal(1, factionZero.TotalKills);
        Assert.Equal(10, factionZero.TotalDamageDealt);
        Assert.Equal(0.5, factionZero.Accuracy);
        Assert.Equal(2, factionZero.Combat.AcceptedAttacks);
        Assert.Equal(1, factionZero.Combat.LandedAttacks);
        Assert.Equal(1, factionZero.Combat.EvadedAttacks);
        Assert.Equal(1, factionZero.Survivors);
        Assert.Equal(1UL, factionZero.TopKillerEntityId);
        Assert.Equal(1, factionZero.TopKillerKills);

        var factionOne = report.Factions.Single(faction => faction.FactionId == 1);
        Assert.Equal(0, factionOne.TotalKills);
        Assert.Equal(5, factionOne.TotalDamageDealt);
        Assert.Equal(1.0, factionOne.Accuracy);
        Assert.Equal(1, factionOne.Survivors);

        // Nobody in this faction scored a kill, so "top killer" falls back
        // to the lowest EntityId among its zero-kill members (entity 2, not
        // victim 100) rather than reporting no top killer at all.
        Assert.Equal(2UL, factionOne.TopKillerEntityId);
        Assert.Equal(0, factionOne.TopKillerKills);
    }

    // ===== RU-16: live per-faction Holding count =====

    [Fact]
    public void Ingest_WithAgents_CountsLivingHoldingWarriorsPerFaction()
    {
        var accumulator = new BattleReportAccumulator();
        var agents = new[]
        {
            Agent(entityId: 1, factionId: 0, intent: AgentIntent.Holding),
            Agent(entityId: 2, factionId: 0, intent: AgentIntent.Holding),
            Agent(entityId: 3, factionId: 0, intent: AgentIntent.Moving),
            Agent(entityId: 4, factionId: 1, intent: AgentIntent.Holding),
            // Dead, so it must not count despite carrying Holding.
            Agent(entityId: 5, factionId: 1, intent: AgentIntent.Holding, isAlive: false),
        };

        // Both factions must already be "observed" (a unit row exists) for
        // BuildFactionTotals to emit a row at all — the same requirement
        // every other faction-totals test in this file relies on. These
        // deaths are otherwise inert: no kill is credited by either.
        var events = new[]
        {
            Death(seq: 1, tick: 1, source: 900, factionId: 0),
            Death(seq: 2, tick: 1, source: 901, factionId: 1),
        };

        accumulator.Ingest(events, default, agents);
        var report = accumulator.Snapshot(terminalTick: 1);

        var factionZero = report.Factions.Single(faction => faction.FactionId == 0);
        var factionOne = report.Factions.Single(faction => faction.FactionId == 1);
        Assert.Equal(2, factionZero.HoldingCount);
        Assert.Equal(1, factionOne.HoldingCount);
    }

    /// <summary>
    /// Holding is a momentary per-tick state, not an event to sum: the
    /// second Ingest call's roster must replace the first call's count
    /// rather than add to it.
    /// </summary>
    [Fact]
    public void Ingest_WithAgents_ReplacesTheHoldingCountRatherThanAccumulatingIt()
    {
        var accumulator = new BattleReportAccumulator();
        var seedFactionZero = new[] { Death(seq: 1, tick: 1, source: 900, factionId: 0) };

        accumulator.Ingest(
            seedFactionZero,
            default,
            new[] { Agent(entityId: 1, factionId: 0, intent: AgentIntent.Holding) });
        accumulator.Ingest(
            Array.Empty<BattleEvent>(),
            default,
            new[] { Agent(entityId: 1, factionId: 0, intent: AgentIntent.Moving) });

        var report = accumulator.Snapshot(terminalTick: 2);

        var factionZero = report.Factions.Single(faction => faction.FactionId == 0);
        Assert.Equal(0, factionZero.HoldingCount);
    }

    /// <summary>
    /// A caller that never passes the roster (every call site not yet
    /// updated for RU-16) must still compile and must report 0 rather than
    /// throwing — the default keeps every other call in this file
    /// unaffected.
    /// </summary>
    [Fact]
    public void Ingest_WithoutAgents_LeavesHoldingCountAtZero()
    {
        var accumulator = new BattleReportAccumulator();

        accumulator.Ingest(
            new[] { Attack(seq: 1, tick: 1, source: 1, target: 100, damage: 5, factionId: 0) },
            default);

        var report = accumulator.Snapshot(terminalTick: 1);

        var factionZero = report.Factions.Single(faction => faction.FactionId == 0);
        Assert.Equal(0, factionZero.HoldingCount);
    }

    [Fact]
    public void Clear_ResetsTheHoldingCount()
    {
        var accumulator = new BattleReportAccumulator();
        accumulator.Ingest(
            Array.Empty<BattleEvent>(),
            default,
            new[] { Agent(entityId: 1, factionId: 0, intent: AgentIntent.Holding) });

        accumulator.Clear();
        accumulator.Ingest(
            new[] { Attack(seq: 1, tick: 1, source: 2, target: 100, damage: 5, factionId: 0) },
            default);
        var report = accumulator.Snapshot(terminalTick: 1);

        var factionZero = report.Factions.Single(faction => faction.FactionId == 0);
        Assert.Equal(0, factionZero.HoldingCount);
    }

    private static AgentView Agent(
        ulong entityId,
        int factionId,
        AgentIntent intent,
        bool isAlive = true) =>
        new(
            EntityId: entityId,
            FactionId: factionId,
            XRaw: 0,
            YRaw: 0,
            HitPoints: isAlive ? 10 : 0,
            MaximumHitPoints: 10,
            TargetEntityId: null,
            Intent: intent,
            IsAlive: isAlive,
            Loadout: new CombatLoadout(
                WeaponId.Kampilan,
                ArmorId.LightOrganic,
                ShieldId.None));

    [Fact]
    public void Clear_ResetsCountersAndHighlights()
    {
        var accumulator = new BattleReportAccumulator();
        accumulator.Ingest(new[]
        {
            Attack(seq: 1, tick: 1, source: 1, target: 100, damage: 10, factionId: 0),
            Death(seq: 2, tick: 1, source: 100, factionId: 1),
        }, default);

        accumulator.Clear();
        var report = accumulator.Snapshot(terminalTick: 0);

        Assert.Empty(report.Leaderboard);
        Assert.Empty(report.Factions);
        Assert.Null(report.FirstBlood);
        Assert.Null(report.DecisiveKill);
        Assert.Null(report.LongestSurvivor);
    }

    [Fact]
    public void Snapshot_OrdersLeaderboardByKillsThenDamageThenEntityId()
    {
        var accumulator = new BattleReportAccumulator();

        // Entities 5 and 3 each score one kill for equal damage; the tie
        // must resolve to the lower entity ID (3) ranking first. Entity 1
        // out-damages both but never lands a kill, so it must rank behind
        // both despite the higher raw damage number.
        accumulator.Ingest(new[]
        {
            Attack(seq: 1, tick: 1, source: 5, target: 201, damage: 5, factionId: 0),
            Death(seq: 2, tick: 1, source: 201, factionId: 1),
        }, default);
        accumulator.Ingest(new[]
        {
            Attack(seq: 3, tick: 2, source: 3, target: 202, damage: 5, factionId: 0),
            Death(seq: 4, tick: 2, source: 202, factionId: 1),
        }, default);
        accumulator.Ingest(new[]
        {
            Attack(seq: 5, tick: 3, source: 1, target: 203, damage: 100, factionId: 0),
        }, default);

        var report = accumulator.Snapshot(terminalTick: 3);

        Assert.Equal(3UL, report.Leaderboard[0].EntityId);
        Assert.Equal(5UL, report.Leaderboard[1].EntityId);
        Assert.Equal(1UL, report.Leaderboard[2].EntityId);
    }

    [Fact]
    public void Snapshot_CapsLeaderboardAtTenRows()
    {
        var accumulator = new BattleReportAccumulator();
        var events = new List<BattleEvent>();
        for (var entityId = 1UL; entityId <= 11UL; entityId++)
        {
            events.Add(Attack(
                seq: (long)entityId,
                tick: 1,
                source: entityId,
                target: 1000,
                damage: (int)entityId * 10,
                factionId: 0));
        }

        accumulator.Ingest(events, default);
        var report = accumulator.Snapshot(terminalTick: 1);

        Assert.Equal(10, report.Leaderboard.Count);
        Assert.Equal(11UL, report.Leaderboard[0].EntityId);
        Assert.DoesNotContain(report.Leaderboard, row => row.EntityId == 1UL);
    }

    [Fact]
    public void Snapshot_LongestSurvivorIsHighestDeathTickTieBrokenByEntityId()
    {
        var accumulator = new BattleReportAccumulator();
        accumulator.Ingest(new[]
        {
            Death(seq: 1, tick: 5, source: 100, factionId: 1),
            Death(seq: 2, tick: 9, source: 200, factionId: 0),
            Death(seq: 3, tick: 9, source: 150, factionId: 1),
        }, default);

        var report = accumulator.Snapshot(terminalTick: 9);

        Assert.NotNull(report.LongestSurvivor);
        Assert.Equal(150UL, report.LongestSurvivor!.Value.EntityId);
        Assert.Equal(9L, report.LongestSurvivor.Value.DeathTick);
    }

    private static BattleEvent Attack(
        long seq,
        long tick,
        ulong source,
        ulong target,
        int damage,
        int factionId,
        WeaponId weapon = WeaponId.Kampilan,
        ShieldId shield = ShieldId.None,
        BodyPart hitLocation = BodyPart.Chest,
        AttackResolution resolution = AttackResolution.Landed) =>
        BattleEvent.Attack(
            seq,
            tick,
            source,
            target,
            damage,
            factionId,
            weapon,
            shield,
            hitLocation,
            resolution);

    private static BattleEvent Death(long seq, long tick, ulong source, int? factionId) =>
        BattleEvent.NonAttack(
            seq,
            tick,
            BattleEventKind.Death,
            source,
            targetEntityId: null,
            value: 0,
            factionId);
    /// <summary>
    /// Faction attack totals and accuracy must come from the simulation's own
    /// counters, not from summing the derived per-unit rows.
    /// </summary>
    /// <remarks>
    /// The events below deliberately disagree with the injected metrics: one
    /// landed attack is present in the event stream, while the simulation
    /// reports four accepted and three landed for faction 0. A report that
    /// still re-derived its faction totals from events would show one and one
    /// and an accuracy of 1.0, so this test fails against the previous
    /// implementation and can only pass once Core is the source.
    /// </remarks>
    [Fact]
    public void FactionTotals_ComeFromCoreMetricsRatherThanFromTheEventStream()
    {
        var accumulator = new BattleReportAccumulator();
        var events = new[]
        {
            Attack(seq: 1, tick: 1, source: 20, target: 500, damage: 9, factionId: 0),
        };

        var coreMetrics = new FactionCombatMetrics(
            new CombatMetrics(
                AcceptedAttacks: 4,
                LandedAttacks: 3,
                ShieldBlockedAttacks: 1,
                ParriedAttacks: 0,
                DeflectedAttacks: 0,
                EvadedAttacks: 0),
            new CombatMetrics(
                AcceptedAttacks: 2,
                LandedAttacks: 1,
                ShieldBlockedAttacks: 0,
                ParriedAttacks: 1,
                DeflectedAttacks: 0,
                EvadedAttacks: 0));

        accumulator.Ingest(events, coreMetrics);
        var report = accumulator.Snapshot(terminalTick: 1);

        var faction0 = Assert.Single(report.Factions, f => f.FactionId == 0);
        Assert.Equal(4, faction0.Combat.AcceptedAttacks);
        Assert.Equal(3, faction0.Combat.LandedAttacks);
        Assert.Equal(1, faction0.Combat.ShieldBlockedAttacks);
        Assert.Equal(3d / 4d, faction0.Accuracy);
    }

    /// <summary>
    /// The authoritative counters accumulate across ticks and are cleared by
    /// <c>Clear</c>, so a second battle cannot inherit the first one's totals.
    /// </summary>
    [Fact]
    public void FactionTotals_SumAcrossTicksAndResetOnClear()
    {
        var accumulator = new BattleReportAccumulator();
        var tick = new FactionCombatMetrics(
            new CombatMetrics(2, 1, 1, 0, 0, 0),
            default);
        var events = new[]
        {
            Attack(seq: 1, tick: 1, source: 20, target: 500, damage: 9, factionId: 0),
        };

        accumulator.Ingest(events, tick);
        accumulator.Ingest(events, tick);
        accumulator.Ingest(events, tick);

        var summed = Assert.Single(
            accumulator.Snapshot(terminalTick: 3).Factions,
            f => f.FactionId == 0);
        Assert.Equal(6, summed.Combat.AcceptedAttacks);
        Assert.Equal(3, summed.Combat.LandedAttacks);

        accumulator.Clear();
        accumulator.Ingest(events, default);

        var afterClear = Assert.Single(
            accumulator.Snapshot(terminalTick: 1).Factions,
            f => f.FactionId == 0);
        Assert.Equal(0, afterClear.Combat.AcceptedAttacks);
        Assert.Equal(0, afterClear.Accuracy);
    }

}
