using System.Globalization;
using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

// Measures audible-cue demand for a battle and replays the client's real
// per-frame budget over it, so the share of cues the player never hears is a
// measurement rather than an estimate. Read-only against Hukbo.Core.

var agents = args.Length > 0 ? int.Parse(args[0], CultureInfo.InvariantCulture) : 200;
var seed = args.Length > 1 ? ulong.Parse(args[1], CultureInfo.InvariantCulture) : 1UL;
var ticks = args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 10_000;

var scenario = Scenario.CreateDefault(seed, agents) with { TickLimit = ticks };
scenario.Validate();
var simulation = BattleSimulation.Create(scenario);

// Slot identity only, not the client enum: this project cannot reference
// Hukbo.Client. Nine slots, in the client's catalog order.
const int SlotCount = 9;
static int SlotOf(BattleEvent battleEvent) =>
    battleEvent.Kind switch
    {
        BattleEventKind.Death => 4,
        BattleEventKind.Attack => battleEvent.Weapon switch
        {
            WeaponId.Kampilan => 0,
            WeaponId.Wasay => 1,
            WeaponId.Kalis => 2,
            WeaponId.Itak => 3,
            _ => -1,
        },
        _ => -1,
    };

var perTickCues = new List<int[]>(1024);

while (simulation.Outcome == BattleOutcome.Ongoing && simulation.Tick < ticks)
{
    simulation.AdvanceOneTick();
    var slots = new int[SlotCount];
    foreach (var battleEvent in simulation.LastEvents)
    {
        var slot = SlotOf(battleEvent);
        if (slot >= 0)
        {
            slots[slot]++;
        }
    }

    perTickCues.Add(slots);
}

var totals = perTickCues.Select(slots => slots.Sum()).ToArray();
var sorted = totals.Order().ToArray();
static int Pct(int[] sortedValues, double percentile)
{
    if (sortedValues.Length == 0)
    {
        return 0;
    }

    var rank = (int)Math.Ceiling(percentile * sortedValues.Length) - 1;
    return sortedValues[Math.Clamp(rank, 0, sortedValues.Length - 1)];
}

Console.WriteLine($"agents={agents} seed={seed} ticksRun={perTickCues.Count} outcome={simulation.Outcome}");
Console.WriteLine($"total audible cues demanded = {totals.Sum()}");
Console.WriteLine($"cues per tick: mean={totals.Average():F2} p50={Pct(sorted, 0.50)} p95={Pct(sorted, 0.95)} " +
    $"p99={Pct(sorted, 0.99)} max={sorted[^1]}");
Console.WriteLine();

// Replays the client's frame loop: the budget is cleared once per rendered
// frame, and however many simulation ticks that frame advanced share it.
static (int Played, int Suppressed, int MaxVoices) ReplayBudget(
    IReadOnlyList<int[]> perTick,
    int tickRate,
    double speedMultiplier,
    int maximumPerSound,
    int maximumTotal,
    int clipTicks)
{
    const double FrameSeconds = 1.0 / 60.0;
    var secondsPerTick = 1.0 / tickRate;
    var accumulator = 0.0;
    var tickIndex = 0;
    var played = 0;
    var suppressed = 0;
    var playedPerTick = new List<int>(perTick.Count);

    while (tickIndex < perTick.Count)
    {
        var perSound = new int[SlotCount];
        var total = 0;
        accumulator += FrameSeconds * speedMultiplier;

        var playedThisFrame = 0;
        while (accumulator >= secondsPerTick && tickIndex < perTick.Count)
        {
            var slots = perTick[tickIndex];
            for (var slot = 0; slot < SlotCount; slot++)
            {
                for (var cue = 0; cue < slots[slot]; cue++)
                {
                    if (total >= maximumTotal || perSound[slot] >= maximumPerSound)
                    {
                        suppressed++;
                        continue;
                    }

                    perSound[slot]++;
                    total++;
                    played++;
                    playedThisFrame++;
                }
            }

            accumulator -= secondsPerTick;
            tickIndex++;
        }

        playedPerTick.Add(playedThisFrame);
    }

    // Concurrency: a clip occupies a voice for clipTicks worth of frames.
    var framesPerClip = Math.Max(1, (int)Math.Round(clipTicks / speedMultiplier));
    var running = 0;
    var maxVoices = 0;
    for (var index = 0; index < playedPerTick.Count; index++)
    {
        running += playedPerTick[index];
        if (index >= framesPerClip)
        {
            running -= playedPerTick[index - framesPerClip];
        }

        maxVoices = Math.Max(maxVoices, running);
    }

    return (played, suppressed, maxVoices);
}

// Mean clip is 222 ms, measured from the shipped WAVs. At tick rate 20 that is
// roughly 4.4 ticks of overlap; 10 ticks covers the 480 ms worst case.
const int MeanClipTicks = 5;

Console.WriteLine("current budget (perSound=3, total=8), by playback speed:");
Console.WriteLine("speed  played  suppressed  suppressed%  peakVoices");
foreach (var speed in new[] { 1.0, 2.0, 4.0 })
{
    var result = ReplayBudget(perTickCues, scenario.TickRate, speed, 3, 8, MeanClipTicks);
    var demanded = result.Played + result.Suppressed;
    Console.WriteLine($"{speed,4:F0}x  {result.Played,6}  {result.Suppressed,10}  " +
        $"{100.0 * result.Suppressed / demanded,10:F1}%  {result.MaxVoices,10}");
}

Console.WriteLine();
Console.WriteLine("candidate budgets at 4x (the worst case), and the voice peak each would ask for:");
Console.WriteLine("perSound  total  played  suppressed  suppressed%  peakVoices");
foreach (var (perSound, total) in new[]
         {
             (3, 8), (4, 16), (6, 24), (8, 32), (12, 48), (16, 64),
             (32, 128), (64, 256), (1000, 1000),
         })
{
    var result = ReplayBudget(perTickCues, scenario.TickRate, 4.0, perSound, total, MeanClipTicks);
    var demanded = result.Played + result.Suppressed;
    Console.WriteLine($"{perSound,8}  {total,5}  {result.Played,6}  {result.Suppressed,10}  " +
        $"{100.0 * result.Suppressed / demanded,10:F1}%  {result.MaxVoices,10}");
}
