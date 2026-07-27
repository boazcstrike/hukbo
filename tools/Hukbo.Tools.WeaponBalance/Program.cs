using System.Collections.Immutable;
using System.Globalization;
using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

// Measures mean ticks-to-kill per weapon loadout and per-faction win rate,
// for T32/T27 of docs/plans/2026-07-27-weapon-identity-and-attributes.md.
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
//
// Task 5 of docs/plans/2026-07-27-combat-preset-v3-combos.md extends this
// tool to also run against CombatPresetId.PrecolonialPhilippinesV3, tallying
// per-weapon chain fraction (the share of landed blows whose
// BattleEvent.ComboPosition was non-null) and mean realized chain length
// (the maximum ComboPosition reached per opened chain, averaged across every
// chain that opened), swept across Scenario.PlaceholderFighterLevel 1
// through 5, per design section 7. V3 fields only the four solo loadouts
// (no shields, no paired rows), so the V3 sweep uses its own four-entry
// label set rather than the six-entry V2 one above it.

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

// V3's roster fields exactly the four solo loadouts (no shields, no paired
// rows — see PhilippineCombatPresetV3.Build's roster array), so labeling by
// weapon alone is unambiguous; there is no (weapon, shield) disambiguation
// to make.
string[] v3LoadoutLabels =
[
    "Kampilan (solo)",
    "Wasay (solo)",
    "Kalis (solo)",
    "Itak (solo)",
];

string LabelOfV3(CombatLoadout loadout) => loadout.Weapon switch
{
    WeaponId.Kampilan => v3LoadoutLabels[0],
    WeaponId.Wasay => v3LoadoutLabels[1],
    WeaponId.Kalis => v3LoadoutLabels[2],
    WeaponId.Itak => v3LoadoutLabels[3],
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

// Runs one V3 scenario suite (mirrored, even roster across the four solo
// loadouts) at a fixed Scenario.PlaceholderFighterLevel, tallying the same
// TTK/win-rate metrics as RunSuite plus, per weapon: the fraction of landed
// blows that were part of a chain (BattleEvent.ComboPosition non-null) and
// the mean realized chain length (the maximum ComboPosition reached per
// opened chain, averaged over every chain that opened).
//
// A chain "opens" exactly when ComboPosition == 1 (see
// BattleSimulation.GatherAndCommitAttacks section 3(c) step 5 — position 1
// is only ever assigned by a successful opening roll on an unchained landed
// blow). Because an attacker can only open a new chain once its previous one
// has already ended (broken, capped, or the target killed — step 5 requires
// wasChaining == false), seeing ComboPosition == 1 again for the same
// attacker means its prior chain, if any, has already ended; that prior
// chain's realized length is exactly the highest ComboPosition this tool
// last recorded for that attacker. Any chain still open when a battle ends
// (the attacker's last blow before the battle concluded) is finalized the
// same way once the tick loop exits.
ComboReport RunComboSuite(string label, int totalAgents, int placeholderFighterLevel)
{
    var ttkTicks = new Dictionary<string, long>();
    var ttkKills = new Dictionary<string, int>();
    var landedBlows = new Dictionary<string, int>();
    var comboBlows = new Dictionary<string, int>();
    var chainsOpened = new Dictionary<string, int>();
    var chainLengthSum = new Dictionary<string, long>();
    var faction0Wins = 0;
    var faction1Wins = 0;
    var draws = 0;

    void FinalizeChain(
        string weaponLabel,
        int realizedLength)
    {
        chainsOpened[weaponLabel] = chainsOpened.GetValueOrDefault(weaponLabel) + 1;
        chainLengthSum[weaponLabel] = chainLengthSum.GetValueOrDefault(weaponLabel) + realizedLength;
    }

    foreach (var seed in seeds)
    {
        var scenario = Scenario.CreateDefault(seed, totalAgents) with
        {
            TickLimit = ticks,
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV3,
            PlaceholderFighterLevel = placeholderFighterLevel,
        };

        scenario.Validate();
        var simulation = BattleSimulation.Create(scenario);
        var loadoutByEntity = new Dictionary<ulong, CombatLoadout>();
        foreach (var agent in simulation.Agents)
        {
            loadoutByEntity[agent.EntityId] = agent.Loadout;
        }

        var firstDamageTick = new Dictionary<ulong, long>();
        var openChainMaxPosition = new Dictionary<ulong, int>();
        var openChainWeapon = new Dictionary<ulong, string>();

        while (simulation.Outcome == BattleOutcome.Ongoing)
        {
            simulation.AdvanceOneTick();

            var landedThisTick = new Dictionary<ulong, List<ulong>>();
            foreach (var battleEvent in simulation.LastEvents)
            {
                if (battleEvent.Kind != BattleEventKind.Attack ||
                    battleEvent.Resolution != AttackResolution.Landed)
                {
                    continue;
                }

                var attackerLabel = LabelOfV3(loadoutByEntity[battleEvent.SourceEntityId]);
                landedBlows[attackerLabel] = landedBlows.GetValueOrDefault(attackerLabel) + 1;

                if (battleEvent.ComboPosition is { } position)
                {
                    comboBlows[attackerLabel] = comboBlows.GetValueOrDefault(attackerLabel) + 1;

                    if (position == 1)
                    {
                        if (openChainMaxPosition.TryGetValue(battleEvent.SourceEntityId, out var previousMax))
                        {
                            FinalizeChain(openChainWeapon[battleEvent.SourceEntityId], previousMax);
                        }

                        openChainMaxPosition[battleEvent.SourceEntityId] = 1;
                        openChainWeapon[battleEvent.SourceEntityId] = attackerLabel;
                    }
                    else
                    {
                        openChainMaxPosition[battleEvent.SourceEntityId] = position;
                    }
                }

                if (battleEvent.TargetEntityId is { } targetId)
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
                    var killerLabel = LabelOfV3(loadoutByEntity[killerId]);
                    ttkTicks[killerLabel] = ttkTicks.GetValueOrDefault(killerLabel) + timeToKill;
                    ttkKills[killerLabel] = ttkKills.GetValueOrDefault(killerLabel) + 1;
                }
            }
        }

        // Any chain still open when the battle ends never produced another
        // ComboPosition == 1 event to trigger FinalizeChain above; count its
        // last known position as its realized length.
        foreach (var (attackerId, maxPosition) in openChainMaxPosition)
        {
            FinalizeChain(openChainWeapon[attackerId], maxPosition);
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

    return new ComboReport(
        label,
        seeds.Length,
        faction0Wins,
        faction1Wins,
        draws,
        ttkTicks,
        ttkKills,
        landedBlows,
        comboBlows,
        chainsOpened,
        chainLengthSum);
}

void PrintComboReport(ComboReport report)
{
    Console.WriteLine($"--- {report.Label} ({report.Seeds} seeds) ---");
    Console.WriteLine(
        $"faction0Wins={report.Faction0Wins} faction1Wins={report.Faction1Wins} draws={report.Draws}");
    Console.WriteLine(
        "weapon loadout            kills   meanTicksToKill   landedBlows   chainFraction   meanChainLength");
    foreach (var label in v3LoadoutLabels)
    {
        var kills = report.TtkKills.GetValueOrDefault(label);
        var meanTicks = kills == 0 ? 0.0 : (double)report.TtkTicks.GetValueOrDefault(label) / kills;
        var landed = report.LandedBlows.GetValueOrDefault(label);
        var combo = report.ComboBlows.GetValueOrDefault(label);
        var chainFraction = landed == 0 ? 0.0 : (double)combo / landed;
        var chains = report.ChainsOpened.GetValueOrDefault(label);
        var meanChainLength = chains == 0 ? 0.0 : (double)report.ChainLengthSum.GetValueOrDefault(label) / chains;
        Console.WriteLine(
            $"{label,-24}  {kills,5}   {meanTicks,8:F2}   {landed,10}   {chainFraction,12:F4}   {meanChainLength,14:F3}");
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

// Task 5 of docs/plans/2026-07-27-combat-preset-v3-combos.md: preset V3,
// mirrored 200-agent even roster (all four solo loadouts), swept across
// Scenario.PlaceholderFighterLevel 1 through 5, per design section 7.
Console.WriteLine("=== Preset V3 combo sweep (PlaceholderFighterLevel 1-5) ===");
Console.WriteLine();

var comboReports = new List<ComboReport>();
for (var level = 1; level <= 5; level++)
{
    var comboReport = RunComboSuite(
        $"V3, 200-agent mirrored even roster, PlaceholderFighterLevel {level}",
        200,
        level);
    comboReports.Add(comboReport);
    PrintComboReport(comboReport);
}

// Design section 7's inversion check: "if the itak's realised throughput
// exceeds the wasay's, the design intent has inverted." Realized throughput
// is read here as mean ticks-to-kill (lower is faster, i.e. higher
// throughput) since that is the direct measured proxy this suite produces;
// chain fraction and mean chain length are printed above for the same
// comparison by a reader who wants the combo-specific view instead.
Console.WriteLine("=== Itak vs. Wasay inversion check (design section 7) ===");
foreach (var comboReport in comboReports)
{
    var wasayKills = comboReport.TtkKills.GetValueOrDefault("Wasay (solo)");
    var itakKills = comboReport.TtkKills.GetValueOrDefault("Itak (solo)");
    var wasayMeanTicks = wasayKills == 0
        ? double.PositiveInfinity
        : (double)comboReport.TtkTicks.GetValueOrDefault("Wasay (solo)") / wasayKills;
    var itakMeanTicks = itakKills == 0
        ? double.PositiveInfinity
        : (double)comboReport.TtkTicks.GetValueOrDefault("Itak (solo)") / itakKills;
    var inverted = itakMeanTicks < wasayMeanTicks;
    Console.WriteLine(
        $"{comboReport.Label}: wasayMeanTTK={wasayMeanTicks,8:F2} itakMeanTTK={itakMeanTicks,8:F2} " +
        $"itakFasterThanWasay={inverted}");
}

Console.WriteLine();

record Report(
    string Label,
    int Seeds,
    int Faction0Wins,
    int Faction1Wins,
    int Draws,
    Dictionary<string, long> TtkTicks,
    Dictionary<string, int> TtkKills);

record ComboReport(
    string Label,
    int Seeds,
    int Faction0Wins,
    int Faction1Wins,
    int Draws,
    Dictionary<string, long> TtkTicks,
    Dictionary<string, int> TtkKills,
    Dictionary<string, int> LandedBlows,
    Dictionary<string, int> ComboBlows,
    Dictionary<string, int> ChainsOpened,
    Dictionary<string, long> ChainLengthSum);
