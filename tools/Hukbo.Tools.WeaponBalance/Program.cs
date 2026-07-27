using System.Collections.Immutable;
using System.Globalization;
using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

// Measures mean ticks-to-kill per weapon loadout and per-faction win rate,
// for T32/T27 of
// docs/archives/2026-07-28/2026-07-27-weapon-identity-and-attributes.md.
// Runs real BattleSimulation instances; read-only against Hukbo.Core and
// writes nothing to a repository file.
//
// Scenario.RosterCounts is applied identically to both factions (see its
// doc comment on Scenario.cs), so there is no built-in way to field two
// different rosters against each other. "Asymmetric" here means a roster
// skewed toward one loadout rather than split evenly across all six,
// still mirrored on both sides. A genuine per-faction asymmetric matchup
// would need Scenario extended to carry a roster per faction, which is a
// separate, non-trivial change with its own design document and is not
// attempted here.

var ticks = args.Length > 0 ? int.Parse(args[0], CultureInfo.InvariantCulture) : 10_000;
var seeds = Enumerable.Range(1, 5).Select(index => (ulong)index).ToArray();

string[] loadoutLabels =
[
    "Kampilan (solo)",
    "Wasay (solo)",
    "Kalis (solo)",
    "Kalis (paired)",
    "Itak (solo)",
    "Itak (paired)",
];

string LabelOf(CombatLoadout loadout) => (loadout.Weapon, loadout.Shield) switch
{
    (WeaponId.Kampilan, _) => loadoutLabels[0],
    (WeaponId.Wasay, _) => loadoutLabels[1],
    (WeaponId.Kalis, ShieldId.None) => loadoutLabels[2],
    (WeaponId.Kalis, _) => loadoutLabels[3],
    (WeaponId.Itak, ShieldId.None) => loadoutLabels[4],
    (WeaponId.Itak, _) => loadoutLabels[5],
    _ => loadout.Weapon.ToString(),
};

Report RunSuite(string label, int totalAgents, int[]? rosterCounts)
{
    var ttkTicks = new Dictionary<string, long>();
    var ttkKills = new Dictionary<string, int>();
    var faction0Wins = 0;
    var faction1Wins = 0;
    var draws = 0;

    foreach (var seed in seeds)
    {
        var scenario = Scenario.CreateDefault(seed, totalAgents) with { TickLimit = ticks };
        if (rosterCounts is not null)
        {
            scenario = scenario with { RosterCounts = ImmutableArray.Create(rosterCounts) };
        }

        scenario.Validate();
        var simulation = BattleSimulation.Create(scenario);
        var loadoutByEntity = new Dictionary<ulong, CombatLoadout>();
        foreach (var agent in simulation.Agents)
        {
            loadoutByEntity[agent.EntityId] = agent.Loadout;
        }

        var firstDamageTick = new Dictionary<ulong, long>();

        while (simulation.Outcome == BattleOutcome.Ongoing)
        {
            simulation.AdvanceOneTick();

            var landedThisTick = new Dictionary<ulong, List<ulong>>();
            foreach (var battleEvent in simulation.LastEvents)
            {
                if (battleEvent.Kind == BattleEventKind.Attack &&
                    battleEvent.Resolution == AttackResolution.Landed &&
                    battleEvent.TargetEntityId is { } targetId)
                {
                    firstDamageTick.TryAdd(targetId, battleEvent.Tick);
                    if (!landedThisTick.TryGetValue(targetId, out var attackers))
                    {
                        attackers = [];
                        landedThisTick[targetId] = attackers;
                    }

                    attackers.Add(battleEvent.SourceEntityId);
                }
            }

            foreach (var battleEvent in simulation.LastEvents)
            {
                if (battleEvent.Kind != BattleEventKind.Death)
                {
                    continue;
                }

                var victim = battleEvent.SourceEntityId;
                if (!landedThisTick.TryGetValue(victim, out var killers))
                {
                    continue;
                }

                var timeToKill = battleEvent.Tick -
                    (firstDamageTick.TryGetValue(victim, out var first) ? first : battleEvent.Tick);
                foreach (var killerId in killers)
                {
                    var killerLabel = LabelOf(loadoutByEntity[killerId]);
                    ttkTicks[killerLabel] = ttkTicks.GetValueOrDefault(killerLabel) + timeToKill;
                    ttkKills[killerLabel] = ttkKills.GetValueOrDefault(killerLabel) + 1;
                }
            }
        }

        switch (simulation.Outcome)
        {
            case BattleOutcome.Faction0Victory:
                faction0Wins++;
                break;
            case BattleOutcome.Faction1Victory:
                faction1Wins++;
                break;
            default:
                draws++;
                break;
        }
    }

    return new Report(label, seeds.Length, faction0Wins, faction1Wins, draws, ttkTicks, ttkKills);
}

void PrintReport(Report report)
{
    Console.WriteLine($"--- {report.Label} ({report.Seeds} seeds) ---");
    Console.WriteLine(
        $"faction0Wins={report.Faction0Wins} faction1Wins={report.Faction1Wins} draws={report.Draws}");
    Console.WriteLine("weapon loadout            kills   meanTicksToKill");
    foreach (var label in loadoutLabels)
    {
        var kills = report.TtkKills.GetValueOrDefault(label);
        var meanTicks = kills == 0 ? 0.0 : (double)report.TtkTicks.GetValueOrDefault(label) / kills;
        Console.WriteLine($"{label,-24}  {kills,5}   {meanTicks,8:F2}");
    }

    Console.WriteLine();
}

int[] Skewed(int heavyIndex, int agentsPerFaction)
{
    var counts = new int[6];
    var heavy = agentsPerFaction / 2;
    counts[heavyIndex] = heavy;

    var remainder = agentsPerFaction - heavy;
    var otherIndexes = Enumerable.Range(0, 6).Where(index => index != heavyIndex).ToArray();
    var share = remainder / otherIndexes.Length;
    var extra = remainder - (share * otherIndexes.Length);
    foreach (var index in otherIndexes)
    {
        counts[index] = share;
    }

    counts[otherIndexes[0]] += extra;
    return counts;
}

PrintReport(RunSuite("200-agent, mirrored, even roster", 200, null));
PrintReport(RunSuite("500-agent, mirrored, even roster", 500, null));

for (var index = 0; index < loadoutLabels.Length; index++)
{
    PrintReport(RunSuite(
        $"500-agent, mirrored, {loadoutLabels[index]}-heavy roster",
        500,
        Skewed(index, 250)));
}

record Report(
    string Label,
    int Seeds,
    int Faction0Wins,
    int Faction1Wins,
    int Draws,
    Dictionary<string, long> TtkTicks,
    Dictionary<string, int> TtkKills);
