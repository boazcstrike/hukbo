using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Hukbo.Core.Combat;
using Hukbo.Core.Determinism;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;
using Hukbo.Headless;

namespace Hukbo.Core.Tests;

public sealed class DeterminismTests
{
    /// <summary>
    /// The ruleset content hash recorded at the commit the pre-clash digest was
    /// captured from. It is passed to the state hasher as a literal rather than
    /// read off the injected ruleset, and that is what makes the comparison
    /// survive folding the clash tables into <c>ComputeContentHash</c>: that fold
    /// moves every content hash, including a neutral one, because
    /// <c>Fnv1a.Add</c> runs eight multiply rounds per word whatever the word
    /// contains. It is <b>not</b> one of the golden constants the
    /// implementation phase re-baselines, and must not be swept up in that edit.
    /// </summary>
    private const ulong PreClashContentHash = 0x59FB4CA563D87A49UL;

    /// <summary>
    /// The state hash the pre-change build reported at the terminal tick of the
    /// seed-1, two-hundred-agent workload. Recaptured with the fixture when the
    /// last-stand formation and the collision priority amendment merged from
    /// <c>main</c>; the superseded value was <c>0xDC7F2E7A107C885A</c> at tick
    /// 1081. It is the value the capture harness recorded, not a golden edited
    /// to match output: the per-tick digest guard proves the same run row by row.
    /// Recaptured again for
    /// the combat preset V3 combinations plan task 4:
    /// <c>StateHasher.Compute</c> now folds three new per-agent words
    /// (<c>Level</c>, <c>ComboStepsRemaining</c>, <c>ComboTargetEntityId</c>) for
    /// every <c>CombatPresetId</c>, including this fixture's
    /// <c>PrecolonialPhilippinesV1</c> control run, so the wider hash no longer
    /// matches the pre-combo build's narrower one even though the run itself is
    /// byte-for-byte the same battle. The superseded value was
    /// <c>0x5BEBA7A68F69BE0D</c>, still at terminal tick 1154.
    /// Recaptured a third time for task T5b of
    /// the formation and movement realism plan: T4
    /// folded <c>Scenario.MovementPreset</c> and the two new per-agent
    /// contingent words into <c>StateHasher.Compute</c> for every
    /// <c>CombatPresetId</c>,
    /// including this fixture's <c>PrecolonialPhilippinesV1</c> control run,
    /// which this fixture's own scenario runs at its default
    /// <c>MovementPreset</c> and default (zero) <c>ContingentId</c> /
    /// <c>ContingentState</c> for every agent -- so the hash moves even though
    /// nothing about this control run's behaviour does. The per-tick event
    /// fold, event count, and final per-agent rows were reconfirmed
    /// unchanged before the fixture's <c>stateHash</c> column was rewritten;
    /// see that task for the confirmation method. The superseded value was
    /// <c>0xFD85207FF329F02D</c>, still at terminal tick 1154.
    /// </summary>
    private const ulong PreClashTerminalStateHash = 0xAE3BEC9EE7BCEDFCUL;

    private const string PreClashDigestFileName =
        "seed-1-200-agents-preclash-digest.json";

    [Fact]
    public void IndependentSameSeedRunsProduceIdenticalEventsAndStateHashes()
    {
        var scenario = Scenario.CreateDefault(seed: 0xDEADBEEF, totalAgents: 200);
        var left = BattleSimulation.Create(scenario);
        var right = BattleSimulation.Create(scenario);
        var sawAttackEvent = false;

        // The loop bound tracks the scenario's own TickLimit rather than a
        // second hardcoded figure. It was 2,000 -- comfortably above the
        // frozen IndependentPursuitV1 preset's own pinned seed-1 tick count
        // of 1,710 -- until T15 of
        // the formation and movement realism plan
        // flipped the shipped default to PersistentContingentsV2, whose
        // contingent cohesion cycle (up to 240 ticks per duty window)
        // legitimately extends how long an arbitrary seed can take to reach
        // a terminal outcome, well inside the 10,000-tick TickLimit the
        // design's own
        // twenty-seed liveness sweep is measured against.
        for (var tick = 0; tick < scenario.TickLimit && left.Outcome == BattleOutcome.Ongoing; tick++)
        {
            left.AdvanceOneTick();
            right.AdvanceOneTick();

            Assert.Equal(left.Tick, right.Tick);
            Assert.Equal(left.Outcome, right.Outcome);
            Assert.Equal(left.LastEvents, right.LastEvents);
            Assert.Equal(left.ComputeStateHash(), right.ComputeStateHash());

            for (var index = 0; index < left.LastEvents.Count; index++)
            {
                var battleEvent = left.LastEvents[index];
                if (battleEvent.Kind != BattleEventKind.Attack)
                {
                    Assert.Null(battleEvent.Weapon);
                    Assert.Null(battleEvent.HitLocation);
                    Assert.Null(battleEvent.Resolution);
                    continue;
                }

                sawAttackEvent = true;
                Assert.NotNull(battleEvent.Weapon);
                Assert.NotNull(battleEvent.HitLocation);

                // The resolution is authoritative, so two runs of one seed must
                // agree on it event for event, and every attack event must carry
                // a defined one.
                Assert.NotNull(battleEvent.Resolution);
                Assert.True(Enum.IsDefined(battleEvent.Resolution!.Value));
                Assert.Equal(
                    battleEvent.Resolution,
                    right.LastEvents[index].Resolution);
            }
        }

        Assert.NotEqual(0UL, left.ComputeStateHash());
        Assert.NotEqual(BattleOutcome.Ongoing, left.Outcome);
        Assert.True(sawAttackEvent, "Expected at least one Attack event across the run.");
    }

    [Fact]
    public void PhilippinePresetContentHash_IsStableAcrossIndependentRegistryLookups()
    {
        var first = CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV1);
        var second = CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV1);

        // D1/D2 regression guard: preset V1 is frozen, declares no weapon
        // attributes and no clash profile, and neither block is folded into
        // its content hash -- not even a zero count -- so this value must
        // never move. It is also PreClashContentHash above, used for a
        // different purpose entirely: the argument the control run passes to
        // the state hasher, not a golden pinned here for its own sake.
        Assert.Equal(0x59FB4CA563D87A49UL, first.ContentHash);
        Assert.Equal(first.ContentHash, second.ContentHash);
    }

    [Fact]
    public void PresetV2ContentHash_IsPinnedAndDistinctFromV1()
    {
        // Pinned so that an accidental edit to a V2 weight, profile, grip,
        // roster entry, or clash table fails here rather than silently
        // invalidating every V2 replay. Changing a V2 value on purpose means a
        // new preset version, not a new literal in this test. Re-baselined for
        // the clash integration: V2 now folds a full clash profile (D1), which
        // moved this value. The superseded value was 0xE653F1802A447662UL,
        // captured before the six-loadout roster carried clash tables. Moved
        // again to 0x10AB1CC226AB3636UL after the T60 retune of the
        // shieldless Kalis/Itak cells in PhilippineCombatPresetV2.BuildClashProfile,
        // per the plan's retune-invalidates-T23/T41/T46 sequencing rule.
        var v1 = CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV1);
        var v2 = CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV2);

        Assert.Equal(0x10AB1CC226AB3636UL, v2.ContentHash);
        Assert.NotEqual(v1.ContentHash, v2.ContentHash);
    }

    /// <summary>
    /// Task 4 of
    /// the combat preset V3 combinations plan.
    /// Pinned so that an accidental edit to a V3 weapon attribute, grip,
    /// roster entry, or combo field fails here rather than silently
    /// invalidating every V3 replay. V3 fields only the four solo loadouts
    /// V2 already carries
    /// (Kampilan, Wasay, solo Kalis, solo Itak) with V2's own damage/reach/
    /// cooldown/target-weight/grip/clash values for those four weapons, plus
    /// the combo table, so its content hash is expected to differ from both
    /// V1 (no weapon attributes at all) and V2 (six loadouts, not four).
    /// </summary>
    [Fact]
    public void PresetV3ContentHash_IsPinnedAndDistinctFromV1AndV2()
    {
        var v1 = CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV1);
        var v2 = CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV2);
        var v3 = CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV3);

        Assert.Equal(0xCD790E489293B304UL, v3.ContentHash);
        Assert.NotEqual(v1.ContentHash, v3.ContentHash);
        Assert.NotEqual(v2.ContentHash, v3.ContentHash);
    }

    /// <summary>
    /// Group R, task R5 of
    /// the warrior rank plan. Preset V4
    /// assigns a <see cref="RankId"/> to each of V3's four solo roster
    /// entries and declares a per-rank fighter level table, which is folded
    /// into <c>ComputeContentHash</c> only because V4 is the first preset to
    /// pass a non-null <c>rankLevels</c> argument. Pinned so an accidental
    /// change to the roster, the rank levels, or the fold order fails here.
    /// </summary>
    [Fact]
    public void PresetV4ContentHash_IsPinnedAndDistinctFromV1V2AndV3()
    {
        var v1 = CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV1);
        var v2 = CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV2);
        var v3 = CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV3);
        var v4 = CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV4);

        Assert.Equal(0x4E3E4F8C0A3822E0UL, v4.ContentHash);
        Assert.NotEqual(v1.ContentHash, v4.ContentHash);
        Assert.NotEqual(v2.ContentHash, v4.ContentHash);
        Assert.NotEqual(v3.ContentHash, v4.ContentHash);
    }

    /// <summary>
    /// Group R, task R5 of
    /// the warrior rank plan. A small, fast
    /// seed-1 workload run through the same headless path
    /// <see cref="PresetV3_SeedOneStateAndEventHashArePinned"/> uses, pinned
    /// against preset V4 so an accidental change to the rank fold in
    /// <c>StateHasher.Compute</c>, the V4 roster, or the V4 rank levels
    /// fails here rather than only in the much slower benchmark.
    /// </summary>
    [Fact]
    public void PresetV4_SeedOneStateAndEventHashArePinned()
    {
        const ulong Seed = 1;
        const int Agents = 20;
        const int Ticks = 200;

        var output = new StringWriter();
        var error = new StringWriter();
        string[] arguments =
        [
            "--agents", Agents.ToString(CultureInfo.InvariantCulture),
            "--ticks", Ticks.ToString(CultureInfo.InvariantCulture),
            "--seed", Seed.ToString(CultureInfo.InvariantCulture),
            "--preset", nameof(CombatPresetId.PrecolonialPhilippinesV4),
            "--movement-preset", nameof(MovementPresetId.IndependentPursuitV1),
        ];
        var exitCode = HeadlessRunner.Run(arguments, output, error);
        Assert.Equal(0, exitCode);

        using var report = JsonDocument.Parse(output.ToString());
        var stateHash = report.RootElement.GetProperty("stateHash").GetString();
        var eventHash = report.RootElement.GetProperty("eventHash").GetString();

        // Captured from a real run of this exact command against this build:
        // `dotnet run --project src/Hukbo.Headless -c Release --no-build --
        // --agents 20 --ticks 200 --seed 1 --preset PrecolonialPhilippinesV4
        // --movement-preset IndependentPursuitV1`.
        Assert.Equal("2BBEDD668CC38FD6", stateHash);
        Assert.Equal("228818712E5AE6C6", eventHash);
    }

    /// <summary>
    /// RU-26. Preset V5 restates V4's four melee loadouts verbatim and adds
    /// three ranged loadouts (Bangkaw, Busog, Arquebus) plus the two RU-45
    /// shielded melee rows (Kalis+TallHardwood, Itak+TallHardwood), for a
    /// nine-entry roster. Pinned so an accidental change to any V5 weapon
    /// attribute, target-weight profile, clash cell, roster entry, or rank
    /// level fails here rather than only in the much slower benchmark.
    /// </summary>
    [Fact]
    public void PresetV5ContentHash_IsPinnedAndDistinctFromV1V2V3AndV4()
    {
        var v1 = CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV1);
        var v2 = CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV2);
        var v3 = CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV3);
        var v4 = CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV4);
        var v5 = CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV5);

        Assert.Equal(0x55F4F5B36EE59CF7UL, v5.ContentHash);
        Assert.NotEqual(v1.ContentHash, v5.ContentHash);
        Assert.NotEqual(v2.ContentHash, v5.ContentHash);
        Assert.NotEqual(v3.ContentHash, v5.ContentHash);
        Assert.NotEqual(v4.ContentHash, v5.ContentHash);
    }

    /// <summary>
    /// RU-26. A small, fast seed-1 workload run through the same headless
    /// path <see cref="PresetV4_SeedOneStateAndEventHashArePinned"/> uses,
    /// pinned against preset V5 so an accidental change to the ranged
    /// combat state machine, the V5 roster, or the shared StateHasher/
    /// event-hash fold fails here rather than only in the much slower
    /// benchmark. Run under
    /// <see cref="MovementPresetId.PersistentContingentsV4"/> rather than
    /// V4's <see cref="MovementPresetId.IndependentPursuitV1"/> because
    /// ranged standoff behaviour is only exercised under a movement preset
    /// that lets a contingent hold formation at range.
    /// </summary>
    [Fact]
    public void PresetV5_SeedOneStateAndEventHashArePinned()
    {
        const ulong Seed = 1;
        const int Agents = 20;
        const int Ticks = 200;

        var output = new StringWriter();
        var error = new StringWriter();
        string[] arguments =
        [
            "--agents", Agents.ToString(CultureInfo.InvariantCulture),
            "--ticks", Ticks.ToString(CultureInfo.InvariantCulture),
            "--seed", Seed.ToString(CultureInfo.InvariantCulture),
            "--preset", nameof(CombatPresetId.PrecolonialPhilippinesV5),
            "--movement-preset", nameof(MovementPresetId.PersistentContingentsV4),
        ];
        var exitCode = HeadlessRunner.Run(arguments, output, error);
        Assert.Equal(0, exitCode);

        using var report = JsonDocument.Parse(output.ToString());
        var stateHash = report.RootElement.GetProperty("stateHash").GetString();
        var eventHash = report.RootElement.GetProperty("eventHash").GetString();

        // Captured from a real run of this exact command against this build:
        // `./src/Hukbo.Headless/bin/Release/net10.0/Hukbo.Headless.exe
        // --agents 20 --ticks 200 --seed 1 --preset PrecolonialPhilippinesV5
        // --movement-preset PersistentContingentsV4`.
        Assert.Equal("DFD7751E249243E3", stateHash);
        Assert.Equal("E8C1D6B300075418", eventHash);
    }

    /// <summary>
    /// RU-26 control. Both sides are built with an explicit
    /// <see cref="Scenario.RosterCounts"/> so both resolve loadouts through
    /// the identical code path, <c>RosterCountExpansion.Expand</c>
    /// (<c>BattleSimulation.cs:563-574</c>), rather than comparing that path
    /// against <c>CombatRuleset.ResolveLoadout</c>'s unrelated interleaved
    /// round-robin -- an earlier version of this control compared V5-with-
    /// <c>RosterCounts</c> against V4's own pinned no-<c>RosterCounts</c> run
    /// and found the two assignment functions disagreed on which warrior
    /// gets which loadout even for byte-identical roster rows, which is a
    /// real fact about <c>ResolveLoadout</c> but proves nothing about
    /// whether a ranged or shield fold leaks into melee combat resolution.
    /// Putting both presets on the one assignment path removes that
    /// confound.
    /// <para>
    /// V4's four-entry roster (Kampilan, Wasay, Kalis, Itak) gets
    /// <c>RosterCounts = [3, 3, 2, 2]</c>, length taken from
    /// <see cref="PhilippineCombatPresetV4"/>'s live roster count. V5's
    /// nine-entry roster (the same four melee weapons in the same order,
    /// plus three ranged rows and the two RU-45 shielded rows) gets
    /// <c>RosterCounts = [3, 3, 2, 2, 0, 0, 0, 0, 0]</c>, length taken from
    /// <see cref="PhilippineCombatPresetV5"/>'s live roster count -- the
    /// same contiguous melee block layout, with the five new rows zeroed.
    /// Same seed, same tick count, same <c>AgentsPerFaction</c>, both hashes
    /// computed live rather than compared against either preset's own
    /// pinned no-<c>RosterCounts</c> fixture, which is a different run.
    /// </para>
    /// <para>
    /// Measured, not assumed: the event hash <b>is</b> identical between the
    /// two runs -- the ordered event stream a melee-only V5 battle produces
    /// is byte-for-byte the one V4's four-loadout roster produces under the
    /// same assignment path, direct proof that RU-26's ranged fold and
    /// RU-45's shield fold touch only the roster rows that declare them. The
    /// state hash differs, exactly as expected and for the one already-
    /// understood reason: <c>BattleSimulation.ComputeStateHash</c> folds
    /// <c>_rules.ContentHash</c> into every per-tick state-hash word, and
    /// V5's <see cref="CombatRuleset.ContentHash"/> necessarily differs from
    /// V4's because V5 declares three ranged weapon profiles and two
    /// shielded roster rows V4 never declares, regardless of which roster
    /// rows a scenario's <c>RosterCounts</c> actually field.
    /// </para>
    /// </summary>
    [Fact]
    public void V4AndV5MeleeOnlyRosterControls_ShareTheSameEventStreamButNotTheSameStateHash()
    {
        var (v4StateHashHex, v4EventHashHex) = RunMeleeOnlyRosterControl(
            CombatPresetId.PrecolonialPhilippinesV4,
            PhilippineCombatPresetV4.Rules.Roster.Count);
        var (v5StateHashHex, v5EventHashHex) = RunMeleeOnlyRosterControl(
            CombatPresetId.PrecolonialPhilippinesV5,
            PhilippineCombatPresetV5.Rules.Roster.Count);

        // Measured live on both sides -- no comparison against either
        // preset's own pinned no-RosterCounts fixture, which is a different
        // run entirely. The real proof: an unchanged ordered event stream
        // across the ranged/shield-carrying preset once both runs share the
        // one assignment path.
        Assert.Equal(v4EventHashHex, v5EventHashHex);
        Assert.NotEqual(v4StateHashHex, v5StateHashHex);
    }

    /// <summary>
    /// Builds a seed-1, twenty-agent, two-hundred-tick melee-only control
    /// run for <paramref name="preset"/>: an explicit
    /// <see cref="Scenario.RosterCounts"/> of <c>[3, 3, 2, 2]</c> for the
    /// preset's first four roster entries (Kampilan, Wasay, Kalis, Itak in
    /// every preset that declares them) and zero for every roster entry
    /// beyond the fourth, sized from <paramref name="rosterCount"/> rather
    /// than a literal. Returns the terminal state hash and the accumulated
    /// event hash, both formatted the same way
    /// <see cref="HeadlessRunner"/> formats them, so a caller can compare
    /// two runs, or a run against a pinned literal, without repeating the
    /// simulation loop.
    /// </summary>
    private static (string StateHashHex, string EventHashHex) RunMeleeOnlyRosterControl(
        CombatPresetId preset,
        int rosterCount)
    {
        var builder = ImmutableArray.CreateBuilder<int>(rosterCount);
        builder.Count = rosterCount;
        for (var index = 0; index < rosterCount; index++)
        {
            builder[index] = 0;
        }

        // The first four roster entries are the shared melee loadouts
        // (Kampilan, Wasay, Kalis, Itak) in every preset this control
        // exercises; the contiguous block layout below is the same one on
        // both sides, so both runs resolve through RosterCountExpansion.Expand
        // rather than one of them falling back to
        // CombatRuleset.ResolveLoadout.
        builder[0] = 3;
        builder[1] = 3;
        builder[2] = 2;
        builder[3] = 2;

        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: 20) with
        {
            TickLimit = 200,
            CombatPreset = preset,
            MovementPreset = MovementPresetId.PersistentContingentsV4,
            RosterCounts = builder.MoveToImmutable(),
        };
        scenario.Validate();

        var simulation = BattleSimulation.Create(scenario);
        var eventHash = Fnv1a.OffsetBasis;
        for (var tick = 0;
             tick < scenario.TickLimit && simulation.Outcome == BattleOutcome.Ongoing;
             tick++)
        {
            simulation.AdvanceOneTick();
            foreach (var battleEvent in simulation.LastEvents)
            {
                HeadlessRunner.AddEventToHash(ref eventHash, battleEvent);
            }
        }

        var stateHash = simulation.ComputeStateHash();
        return (
            stateHash.ToString("X16", CultureInfo.InvariantCulture),
            eventHash.ToString("X16", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Task 4 of
    /// the combat preset V3 combinations plan. A
    /// small, fast seed-1 workload run through the same headless path
    /// <see cref="CombatMetrics_ReachesNeitherHash"/> uses, pinned against
    /// preset V3 so an accidental change anywhere in the V3 attack-
    /// combination state machine, the V3 roster, or the shared StateHasher/
    /// event-hash fold fails here rather than only in the much slower
    /// 200-agent/10,000-tick benchmark. Not a substitute for that benchmark
    /// -- see docs/development/testing.md for the recorded seed-1,
    /// 200-agent, 10,000-tick V3 result -- but this Fact runs on every
    /// <c>dotnet test</c> invocation.
    /// </summary>
    [Fact]
    public void PresetV3_SeedOneStateAndEventHashArePinned()
    {
        const ulong Seed = 1;
        const int Agents = 20;
        const int Ticks = 200;

        var output = new StringWriter();
        var error = new StringWriter();
        string[] arguments =
        [
            "--agents", Agents.ToString(CultureInfo.InvariantCulture),
            "--ticks", Ticks.ToString(CultureInfo.InvariantCulture),
            "--seed", Seed.ToString(CultureInfo.InvariantCulture),
            "--preset", nameof(CombatPresetId.PrecolonialPhilippinesV3),
            // Named explicitly rather than left to whatever
            // Scenario.MovementPreset defaults to. This Fact exists to isolate
            // the V3 attack-combination axis -- see the type-level remarks --
            // and task T15 of
            // the formation and movement realism plan
            // makes the shipped default PersistentContingentsV2, which changes
            // real movement behaviour rather than merely moving a
            // representational hash. Following the default here would let a
            // future movement-axis regression move this Fact's pinned values
            // for a reason that has nothing to do with what it exists to
            // guard.
            "--movement-preset", nameof(MovementPresetId.IndependentPursuitV1),
        ];
        var exitCode = HeadlessRunner.Run(arguments, output, error);
        Assert.Equal(0, exitCode);

        using var report = JsonDocument.Parse(output.ToString());
        var stateHash = report.RootElement.GetProperty("stateHash").GetString();
        var eventHash = report.RootElement.GetProperty("eventHash").GetString();

        // Recaptured on the merge of branch formation-movement-realism into
        // main. Two independent changes moved these values since the previous
        // capture, and neither is a regression:
        //
        //   1. CollisionRules.DefaultBodyRadiusRaw moved from four world units
        //      to 4.25 on main (task C5, design doc
        //      2026-07-28-collision-report-and-shell-design.md section 1.3),
        //      which moves both hashes because it moves real positions.
        //   2. StateHasher.Compute on the branch now folds
        //      Scenario.MovementPreset and two new per-agent words
        //      (ContingentId, ContingentState) for every scenario, including
        //      this preset-V3 control run, which moves the state hash
        //      representationally.
        //
        // Captured from a real run of this exact command on the merge result:
        // `dotnet run --project src/Hukbo.Headless -c Release --no-build --
        // --agents 20 --ticks 200 --seed 1 --preset PrecolonialPhilippinesV3
        // --movement-preset IndependentPursuitV1`.
        //
        // Superseded values, in the order they were superseded. Against the
        // original four-world-unit radius before the movement fold:
        // "C2728456AEB9F760" (state) and "E30AD003EFDDD267" (event). Against
        // the 4.5-world-unit radius, which was abandoned when it reintroduced
        // the last-stand deadlock: "3633AE94D42A49D6" (state) and
        // "DA8A604E5FC575BA" (event). Against the 4.25-world-unit radius on
        // main, before the movement fold: "9F82DB470782B330" (state) and
        // "71E7B6746D00C5D1" (event). On the branch, at the four-world-unit
        // radius with the movement fold: "09851F8966D124D9" (state) and
        // "E30AD003EFDDD267" (event).
        Assert.Equal("BD2E2055DC1E29A9", stateHash);
        Assert.Equal("71E7B6746D00C5D1", eventHash);
    }

    /// <summary>
    /// Task T15 of
    /// the formation and movement realism plan. A
    /// small, fast seed-1 workload run through the same headless path
    /// <see cref="PresetV3_SeedOneStateAndEventHashArePinned"/> uses, pinned
    /// against <see cref="MovementPresetId.PersistentContingentsV2"/> -- the
    /// preset T15 makes the shipped default -- so an accidental change
    /// anywhere in the contingent state machine, the cohesion movement
    /// branch, the arrival taper, or the shared StateHasher/event-hash fold
    /// fails here rather than only in the much slower 200-agent/10,000-tick
    /// benchmark. Not a substitute for that benchmark -- see
    /// docs/development/testing.md for the recorded seed-1, 200-agent,
    /// 10,000-tick result -- but this Fact runs on every
    /// <c>dotnet test</c> invocation.
    /// </summary>
    [Fact]
    public void PersistentContingentsV2_SeedOneStateAndEventHashArePinned()
    {
        const ulong Seed = 1;
        const int Agents = 20;
        const int Ticks = 200;

        var output = new StringWriter();
        var error = new StringWriter();
        string[] arguments =
        [
            "--agents", Agents.ToString(CultureInfo.InvariantCulture),
            "--ticks", Ticks.ToString(CultureInfo.InvariantCulture),
            "--seed", Seed.ToString(CultureInfo.InvariantCulture),
            "--movement-preset", nameof(MovementPresetId.PersistentContingentsV2),
        ];
        var exitCode = HeadlessRunner.Run(arguments, output, error);
        Assert.Equal(0, exitCode);

        using var report = JsonDocument.Parse(output.ToString());
        var stateHash = report.RootElement.GetProperty("stateHash").GetString();
        var eventHash = report.RootElement.GetProperty("eventHash").GetString();

        // Originally captured for task T15 of
        // the formation and movement realism plan,
        // the task that flips Scenario.MovementPreset's shipped default to
        // PersistentContingentsV2.
        //
        // Recaptured on the merge of that branch into main, for one reason and
        // not a regression: the branch was developed while
        // CollisionRules.DefaultBodyRadiusRaw was four world units, and main had
        // already moved it to 4.25. This Fact tracks the shipped default, so it
        // follows the radius rather than pinning it. That is the opposite of
        // MovementPresetFreezeTests, which pins the captured radius because its
        // fixtures are frozen oracles of a past build; the difference is
        // deliberate and each file states its own reason.
        //
        // Recaptured again by task P1 of
        // the rank composition panel plan,
        // which flips Scenario.CombatPreset's own shipped default from
        // PrecolonialPhilippinesV2 to PrecolonialPhilippinesV4. This Fact
        // leaves --preset unnamed on the arguments above precisely so it
        // tracks that default too, matching the "follows rather than pins"
        // rationale already stated for the body radius.
        //
        // Captured from a real run of this exact command against this build:
        // `dotnet run --project src/Hukbo.Headless -c Release --no-build --
        // --agents 20 --ticks 200 --seed 1 --movement-preset
        // PersistentContingentsV2`.
        //
        // Recaptured again on 2026-08-11, when the combat cadence package
        // flipped Scenario.CombatPreset's shipped default from
        // PrecolonialPhilippinesV4 to PrecolonialPhilippinesV6 -- V4's tables
        // restated with every melee attack cooldown, combo cooldown, and
        // damage retuned, so blows land roughly half as often and hurt roughly
        // twice as much. Both hashes had to move: the preset identifier itself
        // folds into the state hash, and halving the attack rate changes the
        // ordered event stream from the first exchange onward. Same rationale
        // as every recapture above -- this Fact follows the shipped default
        // rather than pinning one. See
        // docs/plans/2026-08-11-combat-cadence-v6-design.md.
        //
        // Superseded values, in the order they were superseded. Against the
        // PrecolonialPhilippinesV4 combat default: "41201454CCBADC75" (state)
        // and "514D986A2BD633E8" (event). Against the
        // PrecolonialPhilippinesV2 combat default before that:
        // "62F0E17B85D5D590" (state) and "96A77A6AEEE24BB4" (event). Against
        // the four-world-unit radius before that: "96D59BDBCDD05293" (state)
        // and "12C14F63B4BA1E3B" (event).
        Assert.Equal("DB25EB02805721BC", stateHash);
        Assert.Equal("6F1A64795B7C8E96", eventHash);
    }

    /// <summary>
    /// T45. <c>CombatMetrics</c> is a per-tick counter of resolutions,
    /// accumulated only by <c>HeadlessRunner.Execute</c> for the report; it is
    /// never read by <see cref="BattleSimulation.ComputeStateHash()"/> and
    /// never folded into the event hash alongside the ordinary event fields.
    /// This is the "before" (a bare <see cref="BattleSimulation"/>, which never
    /// builds a <c>CombatMetricsAccumulator</c> at all) against the "after" (the
    /// full headless pipeline, which builds and serializes one every tick) for
    /// the same seed and scenario: if <c>CombatMetrics</c> had leaked into
    /// authoritative state, the two paths would diverge. Captured fresh on this
    /// merged tree rather than trusting the clash branch's own pair, per design
    /// section 6.
    /// </summary>
    [Fact]
    public void CombatMetrics_ReachesNeitherHash()
    {
        const ulong Seed = 1234;
        const int Agents = 20;
        const int Ticks = 200;

        var bareScenario = Scenario.CreateDefault(Seed, Agents) with
        {
            TickLimit = Ticks,
        };
        bareScenario.Validate();
        var bareSimulation = BattleSimulation.Create(bareScenario);
        while (bareSimulation.Outcome == BattleOutcome.Ongoing &&
            bareSimulation.Tick < bareScenario.TickLimit)
        {
            bareSimulation.AdvanceOneTick();
        }

        var output = new StringWriter();
        var error = new StringWriter();
        string[] arguments =
        [
            "--agents", Agents.ToString(CultureInfo.InvariantCulture),
            "--ticks", Ticks.ToString(CultureInfo.InvariantCulture),
            "--seed", Seed.ToString(CultureInfo.InvariantCulture),
        ];
        var exitCode = HeadlessRunner.Run(arguments, output, error);
        Assert.Equal(0, exitCode);

        using var report = JsonDocument.Parse(output.ToString());
        var headlessStateHash = report.RootElement.GetProperty("stateHash").GetString();
        var combatMetrics = report.RootElement.GetProperty("combatMetrics");

        // The "after" path actually built a non-trivial CombatMetrics: this
        // scenario is not clash-neutral, so some accepted attack besides a
        // landed one must have occurred, or this comparison would prove
        // nothing about whether the metrics leaked into the hash.
        Assert.True(
            combatMetrics.GetProperty("acceptedAttacks").GetInt64() > 0,
            "Expected at least one accepted attack in this run.");

        Assert.Equal(
            bareSimulation.ComputeStateHash().ToString("X16", CultureInfo.InvariantCulture),
            headlessStateHash);
    }

    [Fact]
    public void StateHash_ChangesWhenAnyAgentWeaponArmorOrShieldChanges()
    {
        var scenario = Scenario.CreateDefault(seed: 5, totalAgents: 2);

        var baseline = ComputeSingleAgentStateHash(
            scenario,
            new CombatLoadout(WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None));
        var weaponChanged = ComputeSingleAgentStateHash(
            scenario,
            new CombatLoadout(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.None));
        var armorChanged = ComputeSingleAgentStateHash(
            scenario,
            new CombatLoadout(WeaponId.Kampilan, (ArmorId)99, ShieldId.None));
        var shieldChanged = ComputeSingleAgentStateHash(
            scenario,
            new CombatLoadout(WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.TallHardwood));

        Assert.NotEqual(baseline, weaponChanged);
        Assert.NotEqual(baseline, armorChanged);
        Assert.NotEqual(baseline, shieldChanged);
        Assert.NotEqual(weaponChanged, armorChanged);
        Assert.NotEqual(weaponChanged, shieldChanged);
        Assert.NotEqual(armorChanged, shieldChanged);
    }

    [Fact]
    public void StateHash_ChangesWhenTheScenarioBodyRadiusChanges()
    {
        var loadout = new CombatLoadout(
            WeaponId.Kampilan,
            ArmorId.LightOrganic,
            ShieldId.None);
        // The step is lowered once up front so that halving the radius still
        // satisfies the tunneling guard, leaving the radius as the only
        // difference between the two hashed scenarios.
        var scenario = Scenario.CreateDefault(seed: 5, totalAgents: 2) with
        {
            MovementSpeedRaw = FixedPoint.Scale,
        };
        var narrowerBodies = scenario with
        {
            BodyRadiusRaw = scenario.BodyRadiusRaw / 2,
        };

        var baseline = ComputeSingleAgentStateHash(scenario, loadout);
        var changed = ComputeSingleAgentStateHash(narrowerBodies, loadout);

        scenario.Validate();
        narrowerBodies.Validate();
        Assert.NotEqual(scenario.BodyRadiusRaw, narrowerBodies.BodyRadiusRaw);
        Assert.NotEqual(baseline, changed);
    }

    [Fact]
    public void StateHash_ChangesWhenTheScenarioCollisionPolicyChanges()
    {
        // The cast value is deliberately outside the approved contract: Solid is
        // the only policy Validate accepts. StateHasher does not validate, so an
        // unapproved value is the only way to prove the field reaches the hash.
        var loadout = new CombatLoadout(
            WeaponId.Kampilan,
            ArmorId.LightOrganic,
            ShieldId.None);
        var scenario = Scenario.CreateDefault(seed: 5, totalAgents: 2);
        var unapprovedPolicy = scenario with
        {
            CollisionPolicy = (CollisionPolicy)1,
        };

        var baseline = ComputeSingleAgentStateHash(scenario, loadout);
        var changed = ComputeSingleAgentStateHash(unapprovedPolicy, loadout);

        Assert.Equal(CollisionPolicy.Solid, scenario.CollisionPolicy);
        Assert.NotEqual(baseline, changed);
    }

    [Fact]
    public void StateHashChangesWhenTheLastStandThresholdChanges()
    {
        var loadout = new CombatLoadout(
            WeaponId.Kampilan,
            ArmorId.LightOrganic,
            ShieldId.None);
        var scenario = Scenario.CreateDefault(seed: 5, totalAgents: 2) with
        {
            LastStandThresholdAgents = 0,
        };
        var thresholdChanged = scenario with
        {
            LastStandThresholdAgents = 6,
        };

        var baseline = ComputeSingleAgentStateHash(scenario, loadout);
        var changed = ComputeSingleAgentStateHash(thresholdChanged, loadout);

        Assert.NotEqual(baseline, changed);
    }

    private static ulong ComputeSingleAgentStateHash(
        Scenario scenario,
        CombatLoadout loadout)
    {
        var agent = new AgentState(
            entityId: 1,
            factionId: 0,
            xRaw: 0,
            yRaw: 0,
            maximumHitPoints: scenario.MaximumHitPoints,
            movementSpeedRaw: scenario.MovementSpeedRaw,
            perceptionRangeRaw: scenario.PerceptionRangeRaw,
            attackRangeRaw: scenario.AttackRangeRaw,
            damagePerAttack: scenario.DamagePerAttack,
            attackCooldownTicks: scenario.AttackCooldownTicks,
            loadout: loadout);

        var rules = CombatPresetRegistry.Get(scenario.CombatPreset);

        return StateHasher.Compute(
            scenario,
            tick: 1,
            BattleOutcome.Ongoing,
            eventSequence: 0,
            agents: [agent],
            contentHash: rules.ContentHash,
            hasRankLevels: rules.HasRankLevels);
    }

    /// <summary>
    /// Acceptance row <c>Determinism</c>: two independent runs of one seed agree
    /// on the ordered event stream and on the state hash at <em>every</em> tick,
    /// not merely at the end. Comparing only the final state would let a
    /// divergence that cancels itself out pass unnoticed.
    /// </summary>
    [Fact]
    public void TwoIndependentSameSeedRunsAgreeOnOrderedEventsAndStateHashEveryTick()
    {
        var scenario = Scenario.CreateDefault(seed: 11, totalAgents: 60);
        var left = BattleSimulation.Create(scenario);
        var right = BattleSimulation.Create(scenario);

        Assert.Equal(left.ComputeStateHash(), right.ComputeStateHash());

        while (left.Outcome == BattleOutcome.Ongoing)
        {
            left.AdvanceOneTick();
            right.AdvanceOneTick();

            if (!left.LastEvents.SequenceEqual(right.LastEvents))
            {
                Assert.Fail(
                    $"Ordered events first diverged at tick {left.Tick}: the first " +
                    $"run emitted {left.LastEvents.Count} events and the second " +
                    $"emitted {right.LastEvents.Count}.");
            }

            var leftHash = left.ComputeStateHash();
            var rightHash = right.ComputeStateHash();

            if (leftHash != rightHash)
            {
                Assert.Fail(
                    $"State hash first diverged at tick {left.Tick}: " +
                    $"0x{leftHash:X16} against 0x{rightHash:X16}.");
            }
        }

        Assert.Equal(left.Tick, right.Tick);
        Assert.Equal(left.Outcome, right.Outcome);
        Assert.NotEqual(BattleOutcome.Ongoing, left.Outcome);
    }

    /// <summary>
    /// Task 7 coverage: the same lockstep, every-tick comparison as
    /// <see cref="TwoIndependentSameSeedRunsAgreeOnOrderedEventsAndStateHashEveryTick"/>,
    /// but with the last-stand formation explicitly active, so a divergence
    /// introduced by the rally-agent scan, the aim-point movement, or the
    /// new <see cref="AgentIntent.Regrouping"/> hash input would be caught
    /// here even if it cancelled out by the final tick.
    /// </summary>
    [Fact]
    public void TheSameSeedProducesIdenticalHashesAndEventsWithTheLastStandActive()
    {
        var scenario = Scenario.CreateDefault(seed: 3, totalAgents: 40) with
        {
            LastStandThresholdAgents = 6,
        };
        var left = BattleSimulation.Create(scenario);
        var right = BattleSimulation.Create(scenario);

        Assert.Equal(left.ComputeStateHash(), right.ComputeStateHash());

        while (left.Outcome == BattleOutcome.Ongoing)
        {
            left.AdvanceOneTick();
            right.AdvanceOneTick();

            if (!left.LastEvents.SequenceEqual(right.LastEvents))
            {
                Assert.Fail(
                    $"Ordered events first diverged at tick {left.Tick} " +
                    "with the last stand active: the first run emitted " +
                    $"{left.LastEvents.Count} events and the second " +
                    $"emitted {right.LastEvents.Count}.");
            }

            var leftHash = left.ComputeStateHash();
            var rightHash = right.ComputeStateHash();

            if (leftHash != rightHash)
            {
                Assert.Fail(
                    $"State hash first diverged at tick {left.Tick} with " +
                    $"the last stand active: 0x{leftHash:X16} against " +
                    $"0x{rightHash:X16}.");
            }
        }

        Assert.Equal(left.Tick, right.Tick);
        Assert.Equal(left.Outcome, right.Outcome);
        Assert.NotEqual(BattleOutcome.Ongoing, left.Outcome);
    }

    /// <summary>
    /// Acceptance row <c>Permutation</c>: the order the caller happens to store
    /// agents in cannot reach any ordered result. Three storage orders of one
    /// identical roster are advanced in lockstep and compared every tick.
    /// </summary>
    [Fact]
    public void InputArrayOrderCannotChangeOrderedResults()
    {
        var scenario = PermutationScenario();
        var ascending = BattleSimulation.CreateForTesting(
            scenario,
            BuildCrowdedRoster(scenario, AgentOrder.Ascending));
        var descending = BattleSimulation.CreateForTesting(
            scenario,
            BuildCrowdedRoster(scenario, AgentOrder.Descending));
        var interleaved = BattleSimulation.CreateForTesting(
            scenario,
            BuildCrowdedRoster(scenario, AgentOrder.Interleaved));

        for (var tick = 0;
             tick < 60 && ascending.Outcome == BattleOutcome.Ongoing;
             tick++)
        {
            ascending.AdvanceOneTick();
            descending.AdvanceOneTick();
            interleaved.AdvanceOneTick();

            AssertSameOrderedResults(ascending, descending, "descending");
            AssertSameOrderedResults(ascending, interleaved, "interleaved");
        }

        Assert.True(
            ascending.Tick > 0,
            "The permutation comparison never advanced a tick.");
    }

    /// <summary>
    /// Acceptance row <c>ID order</c>: a contested destination goes to the mover
    /// with the lower <see cref="CollisionPriority"/> key for the tick being
    /// resolved, not to the lower <c>EntityId</c>. Identity still decides the
    /// contest — the key is a hash of the seed, the tick and the entity ID — so
    /// renumbering the same two bodies still moves the win. What changed is that
    /// the winner is no longer the same agent on every tick of the battle, which
    /// is what let the faction holding the low IDs win every cross-faction push
    /// of an entire battle.
    /// </summary>
    /// <remarks>
    /// Two allies sit one body diameter apart and converge on one enemy. Their
    /// preferred destinations overlap, so exactly one of them can take its
    /// preferred destination and report <see cref="MovementResolution.Moved"/>.
    /// </remarks>
    [Fact]
    public void ContestedGroundGoesToTheLowerPriorityKeyAndFollowsARenumbering()
    {
        var straight = ResolveContestedGround(lowerRowEntityId: 1, upperRowEntityId: 2);
        var renumbered = ResolveContestedGround(lowerRowEntityId: 2, upperRowEntityId: 1);

        var scenario = ContestScenario();
        var firstKey = CollisionPriority.Resolve(scenario.Seed, tick: 1, entityId: 1);
        var secondKey = CollisionPriority.Resolve(scenario.Seed, tick: 1, entityId: 2);
        var winner = firstKey < secondKey ? 1UL : 2UL;
        var loser = winner == 1UL ? 2UL : 1UL;

        Assert.Equal(MovementResolution.Moved, straight[winner].MovementResolution);
        Assert.Equal(MovementResolution.Moved, renumbered[winner].MovementResolution);
        Assert.NotEqual(MovementResolution.Moved, straight[loser].MovementResolution);
        Assert.NotEqual(MovementResolution.Moved, renumbered[loser].MovementResolution);

        // One body stands on the lower row in both arrangements, under the
        // winning ID in the first and the losing ID in the second. Renumbering
        // therefore has to move it: same ground, different outcome.
        Assert.NotEqual(straight[winner].XRaw, renumbered[loser].XRaw);
    }

    private static void AssertSameOrderedResults(
        BattleSimulation reference,
        BattleSimulation candidate,
        string orderName)
    {
        if (!reference.Agents.SequenceEqual(candidate.Agents))
        {
            Assert.Fail(
                $"The {orderName} storage order first produced different agent " +
                $"state at tick {reference.Tick}.");
        }

        if (!reference.LastEvents.SequenceEqual(candidate.LastEvents))
        {
            Assert.Fail(
                $"The {orderName} storage order first produced a different event " +
                $"stream at tick {reference.Tick}.");
        }

        var referenceHash = reference.ComputeStateHash();
        var candidateHash = candidate.ComputeStateHash();

        if (referenceHash != candidateHash)
        {
            Assert.Fail(
                $"The {orderName} storage order first produced a different state " +
                $"hash at tick {reference.Tick}: 0x{referenceHash:X16} against " +
                $"0x{candidateHash:X16}.");
        }
    }

    private static Dictionary<ulong, AgentView> ResolveContestedGround(
        ulong lowerRowEntityId,
        ulong upperRowEntityId)
    {
        var scenario = ContestScenario();
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            CreateAgent(lowerRowEntityId, 0, 60 * FixedPoint.Scale, 46 * FixedPoint.Scale, scenario),
            // 54.5, not 54: exactly one body diameter (8.5 world units at the
            // enlarged 4.25-world-unit collision radius, task C1,
            // the collision report and window shell plan)
            // above the lower row. CreateForTesting does not validate initial
            // placement, so an unwidened eight-unit gap here would start the
            // two allies illegally overlapped and poison the contest this
            // test exists to check. The gap must be exactly one diameter and
            // no more: the two bodies have to start legally tangent for the
            // contest to happen at all, and any slack lets both allies move
            // freely instead of competing for the same ground.
            CreateAgent(upperRowEntityId, 0, 60 * FixedPoint.Scale, (109 * FixedPoint.Scale) / 2, scenario),
            CreateAgent(3, 1, 100 * FixedPoint.Scale, 50 * FixedPoint.Scale, scenario));

        simulation.AdvanceOneTick();

        return simulation.Agents.ToDictionary(agent => agent.EntityId);
    }

    private enum AgentOrder
    {
        Ascending,
        Descending,
        Interleaved,
    }

    /// <summary>
    /// Two opposing lines close enough to crowd into one another within the
    /// compared window, stored in one of three orders. The rosters are identical
    /// in content; only the array order differs.
    /// </summary>
    private static AgentState[] BuildCrowdedRoster(Scenario scenario, AgentOrder order)
    {
        const int rows = 6;
        var agents = new List<AgentState>(rows * 2);

        for (var row = 0; row < rows; row++)
        {
            var yRaw = checked((20 + (row * 8)) * FixedPoint.Scale);
            agents.Add(
                CreateAgent(
                    checked((ulong)row + 1),
                    factionId: 0,
                    40 * FixedPoint.Scale,
                    yRaw,
                    scenario));
            agents.Add(
                CreateAgent(
                    checked((ulong)(rows + row) + 1),
                    factionId: 1,
                    70 * FixedPoint.Scale,
                    yRaw,
                    scenario));
        }

        return order switch
        {
            AgentOrder.Ascending => [.. agents.OrderBy(agent => agent.EntityId)],
            AgentOrder.Descending => [.. agents.OrderByDescending(agent => agent.EntityId)],
            _ => [.. agents],
        };
    }

    private static Scenario PermutationScenario() =>
        new(
            Seed: 3,
            MapWidth: 200,
            MapHeight: 200,
            AgentsPerFaction: 6,
            TickRate: 20,
            TickLimit: 1_000);

    private static Scenario ContestScenario() =>
        new(
            Seed: 3,
            MapWidth: 200,
            MapHeight: 200,
            AgentsPerFaction: 2,
            TickRate: 20,
            TickLimit: 1_000);

    private static AgentState CreateAgent(
        ulong entityId,
        int factionId,
        int xRaw,
        int yRaw,
        Scenario scenario) =>
        new(
            entityId,
            factionId,
            xRaw,
            yRaw,
            scenario.MaximumHitPoints,
            scenario.MovementSpeedRaw,
            scenario.PerceptionRangeRaw,
            scenario.AttackRangeRaw,
            scenario.DamagePerAttack,
            scenario.AttackCooldownTicks,
            new CombatLoadout(
                WeaponId.Kampilan,
                ArmorId.LightOrganic,
                ShieldId.None));

    /// <summary>
    /// The zero-interception control run. A ruleset that is the registered
    /// preset except for an all-zero clash profile must reproduce the committed
    /// pre-change event stream event for event, and the pre-change state hash
    /// tick for tick, for seed 1 at two hundred agents.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the highest-value case in the change. Four separate mechanisms
    /// move both hashes when the clash lands — the preset version, blows that no
    /// longer land, longer battles, and the new event field — so an ordinary run
    /// gives no way to tell an intended movement from an accidental fifth one.
    /// Holding every clash channel at zero isolates them.
    /// </para>
    /// <para>
    /// The comparand is a fixture captured from the pre-change build, not
    /// another run of this build. Comparing this build against itself would
    /// prove only that zero interception yields <c>Landed</c>, and nothing
    /// whatever about the pre-change behaviour.
    /// </para>
    /// <para>
    /// The per-tick state hash is the half that catches a hashed field which
    /// emits no event, diverges mid-battle, and reconverges by the end.
    /// <c>MovementResolution</c> and <c>Intent</c> are both folded per agent and
    /// neither emits an event.
    /// </para>
    /// </remarks>
    [Fact]
    public void ZeroInterceptionProfile_ReproducesThePreClashDigest()
    {
        var digest = LoadPreClashDigest();
        var simulation = CreateZeroInterceptionControlRun();

        foreach (var row in digest.Ticks)
        {
            simulation.AdvanceOneTick();

            if (simulation.Tick != row.Tick)
            {
                Assert.Fail(
                    $"The control run reached tick {simulation.Tick} where the " +
                    $"pre-change digest recorded tick {row.Tick}.");
            }

            var fold = Fnv1a.OffsetBasis;
            var count = 0;
            foreach (var battleEvent in simulation.LastEvents)
            {
                FoldPreClashEvent(ref fold, battleEvent);
                count++;
            }

            if (count != row.EventCount)
            {
                Assert.Fail(
                    $"Event count first diverged at tick {row.Tick}: the control " +
                    $"run emitted {count} events against {row.EventCount} " +
                    "recorded before the change.");
            }

            if (fold != row.EventFold)
            {
                Assert.Fail(
                    $"The ordered event stream first diverged at tick " +
                    $"{row.Tick}: fold 0x{fold:X16} against the recorded " +
                    $"0x{row.EventFold:X16}.");
            }

            var stateHash = simulation.ComputeStateHash(PreClashContentHash);
            if (stateHash != row.StateHash)
            {
                Assert.Fail(
                    $"The state hash first diverged at tick {row.Tick}: " +
                    $"0x{stateHash:X16} against the recorded " +
                    $"0x{row.StateHash:X16}.");
            }
        }

        Assert.Equal(digest.TerminalTick, simulation.Tick);
        Assert.Equal(digest.Outcome, simulation.Outcome.ToString());
        Assert.Equal(
            digest.Faction0Survivors,
            simulation.Agents.Count(agent => agent.IsAlive && agent.FactionId == 0));
        Assert.Equal(
            digest.Faction1Survivors,
            simulation.Agents.Count(agent => agent.IsAlive && agent.FactionId == 1));

        AssertFinalAgentsMatch(digest, simulation);
    }

    /// <summary>
    /// The one decidable terminal assertion. "The state hash differs only by the
    /// content-hash fold" cannot be evaluated, because FNV-1a is a linear fold
    /// and one cannot inspect an output and conclude that exactly one input word
    /// changed. Passing the recorded content hash to the hasher turns it into a
    /// plain equality against a recorded value instead.
    /// </summary>
    [Fact]
    public void ZeroInterceptionProfile_ReproducesTheRecordedStateHash()
    {
        var digest = LoadPreClashDigest();
        var simulation = CreateZeroInterceptionControlRun();

        while (simulation.Outcome == BattleOutcome.Ongoing &&
            simulation.Tick < digest.TerminalTick)
        {
            simulation.AdvanceOneTick();
        }

        Assert.Equal(digest.TerminalTick, simulation.Tick);
        Assert.Equal(digest.Outcome, simulation.Outcome.ToString());
        Assert.Equal(
            PreClashTerminalStateHash,
            simulation.ComputeStateHash(PreClashContentHash));
        Assert.Equal(PreClashTerminalStateHash, digest.TerminalStateHash);
    }

    private static BattleSimulation CreateZeroInterceptionControlRun()
    {
        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: 200) with
        {
            TickLimit = 10_000,
            // The pre-clash digest fixture was captured against preset V1
            // (PrecolonialPhilippinesV1), the four-loadout roster, before
            // Scenario.CombatPreset defaulted to V2. The control run has to
            // name V1 explicitly, or it silently proves neutrality against a
            // different preset's six-loadout roster and per-weapon attributes
            // than the one the fixture actually recorded.
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV1,
            // The fixture was also captured before CollisionRules
            // .DefaultBodyRadiusRaw moved from four world units to 4.5
            // (design doc 2026-07-28-collision-report-and-shell-design.md
            // section 1.3). Pinning the old radius explicitly here, rather
            // than reading the current default, keeps this control run
            // holding every quantity but the clash profile constant, which
            // is the whole point of the comparison it makes.
            BodyRadiusRaw = 4 * FixedPoint.Scale,
            // Likewise for the movement axis: the fixture was captured under
            // IndependentPursuitV1, before Scenario.MovementPreset defaulted
            // to PersistentContingentsV2 in T15 of
            // the formation and movement realism plan.
            // The control run has to name V1 explicitly, or it silently
            // replays the frozen digest against a preset that moves every
            // agent's trajectory.
            MovementPreset = MovementPresetId.IndependentPursuitV1,
        };
        scenario.Validate();

        // Built with the copy helper so the injected ruleset is provably the
        // preset except for the clash profile, rather than six constructor
        // arguments reassembled by hand from the public surface.
        var rules = CombatPresetRegistry
            .Get(scenario.CombatPreset)
            .WithClashProfile(ClashProfile.Neutral);

        return BattleSimulation.Create(scenario, rules);
    }

    /// <summary>
    /// The pre-clash digest fixture recorded <c>WeaponId.Itak</c>'s name at
    /// capture time under its prior identifier, <c>Bolo</c>, before the V2
    /// weapon-identity rename (design section 2.1) renumbered no value and
    /// changed no behaviour -- only the enum member's name. Translating the
    /// legacy label here, rather than editing the committed fixture or its
    /// pinned hashes, keeps the fixture's own recorded provenance intact while
    /// still comparing the name a fixture row actually identifies. No other
    /// weapon name differs: Kampilan, Wasay, and Kalis already carried their
    /// current names by the commit the digest was captured from.
    /// </summary>
    private static string TranslateLegacyWeaponName(string legacyName) =>
        legacyName == "Bolo" ? nameof(WeaponId.Itak) : legacyName;

    private static void AssertFinalAgentsMatch(
        PreClashDigest digest,
        BattleSimulation simulation)
    {
        var actual = simulation.Agents;
        Assert.Equal(digest.FinalAgents.Count, actual.Count);

        for (var index = 0; index < actual.Count; index++)
        {
            var expected = digest.FinalAgents[index];
            var agent = actual[index];

            Assert.Equal(expected.EntityId, agent.EntityId);
            Assert.Equal(expected.FactionId, agent.FactionId);
            Assert.Equal(expected.XRaw, agent.XRaw);
            Assert.Equal(expected.YRaw, agent.YRaw);
            Assert.Equal(expected.HitPoints, agent.HitPoints);
            Assert.Equal(expected.MaximumHitPoints, agent.MaximumHitPoints);
            Assert.Equal(expected.TargetEntityId, agent.TargetEntityId ?? 0);
            Assert.Equal(expected.Intent, agent.Intent.ToString());
            Assert.Equal(expected.IsAlive, agent.IsAlive);
            Assert.Equal(
                TranslateLegacyWeaponName(expected.Weapon),
                agent.Loadout.Weapon.ToString());
            Assert.Equal(expected.Armor, agent.Loadout.Armor.ToString());
            Assert.Equal(expected.Shield, agent.Loadout.Shield.ToString());
            Assert.Equal(
                expected.MovementResolution,
                agent.MovementResolution.ToString());
        }
    }

    /// <summary>
    /// Folds one event exactly as the capture harness did, over the nine fields
    /// a pre-change event carries. <see cref="BattleEvent.Resolution"/> is
    /// deliberately excluded: a post-change event carries a field a pre-change
    /// event cannot, and folding it would guarantee a mismatch that means
    /// nothing.
    /// </summary>
    private static void FoldPreClashEvent(ref ulong hash, BattleEvent battleEvent)
    {
        Fnv1a.Add(ref hash, unchecked((ulong)battleEvent.Sequence));
        Fnv1a.Add(ref hash, unchecked((ulong)battleEvent.Tick));
        Fnv1a.Add(ref hash, (ulong)battleEvent.Kind);
        Fnv1a.Add(ref hash, battleEvent.SourceEntityId);
        Fnv1a.Add(ref hash, battleEvent.TargetEntityId ?? 0);
        Fnv1a.Add(ref hash, unchecked((ulong)(uint)battleEvent.Value));
        Fnv1a.Add(
            ref hash,
            battleEvent.FactionId is { } factionId
                ? unchecked((ulong)(uint)factionId)
                : ulong.MaxValue);
        Fnv1a.Add(
            ref hash,
            battleEvent.Weapon is { } weapon
                ? unchecked((ulong)(uint)(int)weapon)
                : ulong.MaxValue);
        Fnv1a.Add(
            ref hash,
            battleEvent.HitLocation is { } hitLocation
                ? unchecked((ulong)(uint)(int)hitLocation)
                : ulong.MaxValue);
    }

    private static PreClashDigest LoadPreClashDigest()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            PreClashDigestFileName);

        Assert.True(
            File.Exists(path),
            $"The pre-clash digest fixture is missing at '{path}'. It is " +
            "committed under tests/Hukbo.Core.Tests/Fixtures and copied to the " +
            "output directory by the project's Fixtures item.");

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;

        var ticks = new List<PreClashTickRow>();
        foreach (var row in root.GetProperty("ticks").EnumerateArray())
        {
            ticks.Add(
                new PreClashTickRow(
                    row.GetProperty("tick").GetInt64(),
                    row.GetProperty("eventCount").GetInt32(),
                    ParseHex(row.GetProperty("eventFold").GetString()),
                    ParseHex(row.GetProperty("stateHash").GetString())));
        }

        var agents = new List<PreClashAgentRow>();
        foreach (var agent in root.GetProperty("finalAgents").EnumerateArray())
        {
            agents.Add(
                new PreClashAgentRow(
                    agent.GetProperty("entityId").GetUInt64(),
                    agent.GetProperty("factionId").GetInt32(),
                    agent.GetProperty("xRaw").GetInt32(),
                    agent.GetProperty("yRaw").GetInt32(),
                    agent.GetProperty("hitPoints").GetInt32(),
                    agent.GetProperty("maximumHitPoints").GetInt32(),
                    agent.GetProperty("targetEntityId").GetUInt64(),
                    agent.GetProperty("intent").GetString() ?? string.Empty,
                    agent.GetProperty("isAlive").GetBoolean(),
                    agent.GetProperty("weapon").GetString() ?? string.Empty,
                    agent.GetProperty("armor").GetString() ?? string.Empty,
                    agent.GetProperty("shield").GetString() ?? string.Empty,
                    agent.GetProperty("movementResolution").GetString() ??
                        string.Empty));
        }

        return new PreClashDigest(
            root.GetProperty("terminalTick").GetInt64(),
            root.GetProperty("outcome").GetString() ?? string.Empty,
            root.GetProperty("faction0Survivors").GetInt32(),
            root.GetProperty("faction1Survivors").GetInt32(),
            ParseHex(root.GetProperty("terminalStateHash").GetString()),
            ticks,
            agents);
    }

    private static ulong ParseHex(string? value)
    {
        Assert.False(
            string.IsNullOrWhiteSpace(value),
            "The pre-clash digest fixture carries an empty hash field.");

        return ulong.Parse(
            value!,
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture);
    }

    private sealed record PreClashTickRow(
        long Tick,
        int EventCount,
        ulong EventFold,
        ulong StateHash);

    private sealed record PreClashAgentRow(
        ulong EntityId,
        int FactionId,
        int XRaw,
        int YRaw,
        int HitPoints,
        int MaximumHitPoints,
        ulong TargetEntityId,
        string Intent,
        bool IsAlive,
        string Weapon,
        string Armor,
        string Shield,
        string MovementResolution);

    private sealed record PreClashDigest(
        long TerminalTick,
        string Outcome,
        int Faction0Survivors,
        int Faction1Survivors,
        ulong TerminalStateHash,
        IReadOnlyList<PreClashTickRow> Ticks,
        IReadOnlyList<PreClashAgentRow> FinalAgents);

    [Fact]
    public void SnapshotIsAnImmutableCopyOfTheCompletedTick()
    {
        var simulation = BattleSimulation.Create(
            Scenario.CreateDefault(seed: 7, totalAgents: 20));
        simulation.AdvanceOneTick();

        var snapshot = simulation.CreateSnapshot();
        var firstAgent = snapshot.Agents[0];

        simulation.AdvanceOneTick();

        Assert.Equal(1, snapshot.Tick);
        Assert.Equal(firstAgent, snapshot.Agents[0]);
        Assert.NotEqual(snapshot.StateHash, simulation.ComputeStateHash());
    }

    /// <summary>
    /// Task F0 of the 2026-07-31 movement V7 pressure interrupt task plan.
    /// The same lockstep, every-tick comparison
    /// <see cref="TwoIndependentSameSeedRunsAgreeOnOrderedEventsAndStateHashEveryTick"/>
    /// makes for the shipped default, but under
    /// <see cref="MovementPresetId.EquipmentRelativeFootworkV7"/>, whose
    /// pressure interrupt writes three new per-agent fields and folds four new
    /// ruleset values into the state hash. A divergence introduced by the
    /// interrupt's scratch arrays, its cooldown and combo writes, or the
    /// conditional <c>StateHasher</c> block would surface here at the tick it
    /// first appeared rather than only in the terminal hash.
    /// </summary>
    /// <remarks>
    /// The loop is bounded by an explicit tick count rather than by
    /// <see cref="BattleOutcome.Ongoing"/>, which is what the two tests above
    /// use. V7 does not terminate: every cell of the calibration matrix in
    /// the 2026-07-31 movement V7 pressure interrupt calibration record ended
    /// <see cref="BattleOutcome.Draw"/> at the 10,000-tick limit, so an
    /// outcome-driven loop would run the full limit twice over on every
    /// <c>dotnet test</c> invocation and prove nothing the bounded window does
    /// not.
    /// </remarks>
    /// <remarks>
    /// The agent count and the seed are not free choices. The interrupt fires
    /// rarely -- section 4 of the calibration record measures 129 firings
    /// across all six rows of the whole 10,000-tick seed-1 200-agent cell --
    /// and at 60 agents it does not fire inside this window at all, which the
    /// closing assertion caught when this test was first written against that
    /// size. Seed 2 at 200 agents is the densest cell in the matrix, at 360
    /// firings, and is chosen so the compared window reliably contains some.
    /// </remarks>
    [Fact]
    public void EquipmentRelativeFootworkV7_SameSeedRunsAgreeOnEventsAndStateHashEveryTick()
    {
        const int ComparedTicks = 1_500;

        var scenario = Scenario.CreateDefault(seed: 2, totalAgents: 200) with
        {
            MovementPreset = MovementPresetId.EquipmentRelativeFootworkV7,
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV2,
        };
        var left = BattleSimulation.Create(scenario);
        var right = BattleSimulation.Create(scenario);
        var sawInterruptFire = false;

        Assert.Equal(left.ComputeStateHash(), right.ComputeStateHash());

        for (var tick = 0;
            tick < ComparedTicks && left.Outcome == BattleOutcome.Ongoing;
            tick++)
        {
            left.AdvanceOneTick();
            right.AdvanceOneTick();

            sawInterruptFire |= left.Agents.Any(
                agent => agent.IsAlive && agent.BrokeOffUnderPressure);

            if (!left.LastEvents.SequenceEqual(right.LastEvents))
            {
                Assert.Fail(
                    $"Ordered events first diverged at tick {left.Tick}: the " +
                    $"first run emitted {left.LastEvents.Count} events and the " +
                    $"second emitted {right.LastEvents.Count}.");
            }

            var leftHash = left.ComputeStateHash();
            var rightHash = right.ComputeStateHash();

            if (leftHash != rightHash)
            {
                Assert.Fail(
                    $"State hash first diverged at tick {left.Tick}: " +
                    $"0x{leftHash:X16} against 0x{rightHash:X16}.");
            }
        }

        Assert.Equal(left.Tick, right.Tick);
        Assert.Equal(left.Outcome, right.Outcome);

        // The interrupt must actually have fired somewhere in the compared
        // window, or this test would agree just as happily about a V7 that
        // never took its own branch, and would therefore be asserting
        // determinism over the V6 code path under a V7 name.
        Assert.True(
            sawInterruptFire,
            $"No warrior broke off under pressure in the first {ComparedTicks} " +
            "ticks, so this run never exercised the V7 branch it exists to " +
            "cover.");
    }

    /// <summary>
    /// Task F0 of the 2026-07-31 movement V7 pressure interrupt task plan.
    /// Two independent headless runs of one seed under V7 agree on the state
    /// hash, the event hash, the outcome, and the survivor counts. This is the
    /// <c>CLAUDE.md</c> section 5 contract -- same seed plus same build gives
    /// an identical state hash, event hash, winner, and ordered event stream --
    /// asserted through the same runner the canonical gate's determinism
    /// workload uses, so it covers the whole path rather than
    /// <c>BattleSimulation</c> alone.
    /// </summary>
    [Fact]
    public void EquipmentRelativeFootworkV7_RepeatedHeadlessRunsAgree()
    {
        var first = RunV7Headless();
        var second = RunV7Headless();

        Assert.Equal(
            first.RootElement.GetProperty("stateHash").GetString(),
            second.RootElement.GetProperty("stateHash").GetString());
        Assert.Equal(
            first.RootElement.GetProperty("eventHash").GetString(),
            second.RootElement.GetProperty("eventHash").GetString());
        Assert.Equal(
            first.RootElement.GetProperty("outcome").GetString(),
            second.RootElement.GetProperty("outcome").GetString());
        Assert.Equal(
            first.RootElement.GetProperty("faction0Survivors").GetInt32(),
            second.RootElement.GetProperty("faction0Survivors").GetInt32());
        Assert.Equal(
            first.RootElement.GetProperty("faction1Survivors").GetInt32(),
            second.RootElement.GetProperty("faction1Survivors").GetInt32());

        // The runner's own two-run comparison, which is the check the gate's
        // determinism workload reports on.
        Assert.True(first.RootElement.GetProperty("deterministic").GetBoolean());
        Assert.True(second.RootElement.GetProperty("deterministic").GetBoolean());

        first.Dispose();
        second.Dispose();
    }

    private static JsonDocument RunV7Headless()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        string[] arguments =
        [
            "--agents", "20",
            "--ticks", "400",
            "--seed", "1",
            "--preset", nameof(CombatPresetId.PrecolonialPhilippinesV2),
            "--movement-preset",
            nameof(MovementPresetId.EquipmentRelativeFootworkV7),
        ];

        var exitCode = HeadlessRunner.Run(arguments, output, error);
        Assert.Equal(0, exitCode);

        return JsonDocument.Parse(output.ToString());
    }
}
