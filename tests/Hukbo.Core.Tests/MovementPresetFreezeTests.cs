using System.Globalization;
using System.Text.Json;
using Hukbo.Core.Combat;
using Hukbo.Core.Determinism;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;
using Hukbo.Headless;

namespace Hukbo.Core.Tests;

/// <summary>
/// Guards two frozen-behaviour trajectories, each captured from the
/// completely unmodified <c>src/</c> tree at the commit named in its own
/// fixture's <c>provenance.capturedFromCommit</c> field, before a single line
/// of the workstream that would go on to change it existed:
/// <list type="bullet">
/// <item>
/// <description>
/// <c>IndependentPursuitV1</c>, captured by
/// docs/plans/2026-07-28-formation-movement-realism.md task T1, before a
/// single line of the movement-preset workstream existed. Every later task
/// in that plan that could plausibly disturb its trajectory -- the formation
/// planner change, the state-hash move, the shared helper extraction --
/// reproduces this fixture byte-identically as part of its own
/// verification.
/// </description>
/// </item>
/// <item>
/// <description>
/// <c>PersistentContingentsV2</c>, captured by
/// docs/archives/2026-07-28/2026-07-28-contingent-close-latch.md task T1, before a single
/// line of the contingent-close-latch workstream existed. That plan rewrites
/// the <c>Close</c>-state transition rule that V2 uses, so every later task
/// in it that could plausibly disturb V2's trajectory reproduces this
/// fixture byte-identically as part of its own verification.
/// </description>
/// </item>
/// </list>
/// This file is the oracle those tasks replay against.
/// </summary>
/// <remarks>
/// Each comparand is a fixture captured once from its own pre-change build,
/// not another run of the current build. Comparing the current build against
/// itself would prove only that the simulation is internally consistent with
/// itself, which <see cref="DeterminismTests"/> already covers, and nothing
/// whatever about whether a later change moved the trajectory.
/// </remarks>
public sealed class MovementPresetFreezeTests
{
    private const string IndependentPursuitV1DigestFileName =
        "seed-1-200-agents-movement-v1-digest.json";

    private const string PersistentContingentsV2DigestFileName =
        "seed-1-200-agents-movement-v2-digest.json";

    /// <summary>
    /// Replays the frozen seed-1, two-hundred-agent trajectory tick by tick
    /// under the default scenario -- <c>IndependentPursuitV1</c> is the only
    /// movement preset that exists at this commit, so there is nothing to
    /// select -- and asserts every tick row and the final per-agent rows
    /// match the fixture exactly.
    /// </summary>
    [Fact]
    public void IndependentPursuitV1_ReproducesTheFrozenTrajectoryDigest()
    {
        var digest = LoadDigest(IndependentPursuitV1DigestFileName);
        var simulation = CreateControlRun(MovementPresetId.IndependentPursuitV1);

        ReplayAndAssertDigest(digest, simulation);

        // Neither column has a real value on this fixture: it was captured
        // before AgentState.ContingentId and AgentState.ContingentState
        // existed, so both are reserved as 0 placeholders rather than
        // compared against the control run's actual values.
        foreach (var expected in digest.FinalAgents)
        {
            Assert.Equal(0, expected.ContingentId);
            Assert.Equal(0, expected.ContingentState);
        }
    }

    /// <summary>
    /// Replays the frozen seed-1, two-hundred-agent trajectory tick by tick
    /// under <c>PersistentContingentsV2</c> and asserts every tick row and
    /// the final per-agent rows -- including the real
    /// <see cref="AgentView.ContingentId"/> and
    /// <see cref="AgentView.ContingentState"/> values this preset populates
    /// -- match the fixture exactly. See
    /// docs/archives/2026-07-28/2026-07-28-contingent-close-latch.md task T1: this fixture
    /// is the oracle every later task in that plan replays against before it
    /// is allowed to touch the <c>Close</c>-state transition rule V2 uses.
    /// </summary>
    [Fact]
    public void PersistentContingentsV2_ReproducesTheFrozenTrajectoryDigest()
    {
        var digest = LoadDigest(PersistentContingentsV2DigestFileName);
        var simulation = CreateControlRun(MovementPresetId.PersistentContingentsV2);

        ReplayAndAssertDigest(digest, simulation);
        AssertFinalContingentFieldsMatch(digest, simulation);
    }

    private static void ReplayAndAssertDigest(
        MovementDigest digest,
        BattleSimulation simulation)
    {
        foreach (var row in digest.Ticks)
        {
            simulation.AdvanceOneTick();

            if (simulation.Tick != row.Tick)
            {
                Assert.Fail(
                    $"The control run reached tick {simulation.Tick} where the " +
                    $"frozen digest recorded tick {row.Tick}.");
            }

            var fold = Fnv1a.OffsetBasis;
            var count = 0;
            foreach (var battleEvent in simulation.LastEvents)
            {
                HeadlessRunner.AddEventToHash(ref fold, battleEvent);
                count++;
            }

            if (count != row.EventCount)
            {
                Assert.Fail(
                    $"Event count first diverged at tick {row.Tick}: the control " +
                    $"run emitted {count} events against {row.EventCount} " +
                    "recorded in the frozen digest.");
            }

            if (fold != row.EventFold)
            {
                Assert.Fail(
                    $"The ordered event stream first diverged at tick " +
                    $"{row.Tick}: fold 0x{fold:X16} against the recorded " +
                    $"0x{row.EventFold:X16}.");
            }

            var stateHash = simulation.ComputeStateHash();
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
        Assert.Equal(digest.TerminalStateHash, simulation.ComputeStateHash());

        AssertFinalAgentsMatch(digest, simulation);
    }

    private static BattleSimulation CreateControlRun(MovementPresetId movementPreset)
    {
        // Named by T15's inventory step
        // (docs/plans/2026-07-28-formation-movement-realism.md) after
        // flipping Scenario.MovementPreset's shipped default to
        // PersistentContingentsV2: this fixture is the frozen-behaviour
        // oracle for IndependentPursuitV1 specifically, and its own pinned
        // trajectory must not move when the default moves out from under
        // it. The preset is named explicitly rather than left to whatever
        // Scenario.CreateDefault happens to select, mirroring
        // DeterminismTests.CreateZeroInterceptionControlRun's identical
        // rationale for CombatPreset. The same reasoning applies to the
        // PersistentContingentsV2 control run once
        // docs/archives/2026-07-28/2026-07-28-contingent-close-latch.md flips the default
        // again, to PersistentContingentsV3.
        // The body radius is pinned here for exactly the reason the preset
        // above is, and it was missed when that reasoning was first written.
        // Both fixtures were captured while CollisionRules.DefaultBodyRadiusRaw
        // was four world units; main had already moved it to 4.25 by the time
        // this workstream merged. Nothing in either fixture records the radius
        // -- provenance pins only "Scenario.CreateDefault(1, 200)" -- so the
        // captured trajectories silently inherited the default of their own
        // capture commit, and inheriting the current default instead replays a
        // different battle and diverges at tick 1.
        //
        // Four world units is therefore not a value to keep in step with the
        // shipped default; it is part of the fixture, and it moves only if the
        // fixture is recaptured. Recapturing is what these files exist to make
        // unnecessary: the type's remarks are explicit that a fixture is
        // captured once from its own pre-change build, because comparing the
        // current build against itself would prove nothing about whether a
        // later change moved the trajectory.
        const int CapturedBodyRadiusRaw = 4 * FixedPoint.Scale;

        // Both fixtures' provenance.construction.combatPreset records
        // "PrecolonialPhilippinesV2" -- the shipped default at capture time.
        // The rank-in-composition-panel plan
        // (docs/plans/2026-07-29-rank-composition-panel.md, task P1) moves
        // Scenario.CombatPreset's own default to
        // CombatPresetId.PrecolonialPhilippinesV4, which changes every
        // agent's roster loadout and therefore the state hash from tick 1
        // onward. Pinning V2 explicitly here, exactly as MovementPreset and
        // BodyRadiusRaw already are, keeps this control run reproducing the
        // frozen fixture regardless of where the shipped default moves next.
        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: 200) with
        {
            MovementPreset = movementPreset,
            BodyRadiusRaw = CapturedBodyRadiusRaw,
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV2,
        };
        scenario.Validate();
        return BattleSimulation.Create(scenario);
    }

    private static void AssertFinalAgentsMatch(
        MovementDigest digest,
        BattleSimulation simulation)
    {
        var actual = simulation.Agents;
        Assert.Equal(digest.FinalAgents.Count, actual.Count);

        for (var index = 0; index < actual.Count; index++)
        {
            var expected = digest.FinalAgents[index];
            var agent = actual[index];

            Assert.Equal(expected.EntityId, agent.EntityId);
            Assert.Equal(expected.XRaw, agent.XRaw);
            Assert.Equal(expected.YRaw, agent.YRaw);
            Assert.Equal(expected.HitPoints, agent.HitPoints);
            Assert.Equal(expected.Intent, agent.Intent.ToString());
            Assert.Equal(
                expected.MovementResolution,
                agent.MovementResolution.ToString());
            Assert.Equal(expected.Loadout, agent.Loadout.ToString());
        }
    }

    private static void AssertFinalContingentFieldsMatch(
        MovementDigest digest,
        BattleSimulation simulation)
    {
        var actual = simulation.Agents;
        Assert.Equal(digest.FinalAgents.Count, actual.Count);

        for (var index = 0; index < actual.Count; index++)
        {
            var expected = digest.FinalAgents[index];
            var agent = actual[index];

            Assert.Equal(expected.ContingentId, agent.ContingentId);
            Assert.Equal(expected.ContingentState, (int)agent.ContingentState);
        }
    }

    private static MovementDigest LoadDigest(string digestFileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", digestFileName);

        Assert.True(
            File.Exists(path),
            $"The movement digest fixture is missing at '{path}'. It is " +
            "committed under tests/Hukbo.Core.Tests/Fixtures and copied to the " +
            "output directory by the project's Fixtures item.");

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;

        var ticks = new List<MovementTickRow>();
        foreach (var row in root.GetProperty("ticks").EnumerateArray())
        {
            ticks.Add(
                new MovementTickRow(
                    row.GetProperty("tick").GetInt64(),
                    row.GetProperty("eventCount").GetInt32(),
                    ParseHex(row.GetProperty("eventFold").GetString()),
                    ParseHex(row.GetProperty("stateHash").GetString())));
        }

        var agents = new List<MovementAgentRow>();
        foreach (var agent in root.GetProperty("finalAgents").EnumerateArray())
        {
            agents.Add(
                new MovementAgentRow(
                    agent.GetProperty("entityId").GetUInt64(),
                    agent.GetProperty("xRaw").GetInt32(),
                    agent.GetProperty("yRaw").GetInt32(),
                    agent.GetProperty("hitPoints").GetInt32(),
                    agent.GetProperty("intent").GetString() ?? string.Empty,
                    agent.GetProperty("movementResolution").GetString() ??
                        string.Empty,
                    agent.GetProperty("loadout").GetString() ?? string.Empty,
                    agent.GetProperty("contingentId").GetInt32(),
                    agent.GetProperty("contingentState").GetInt32()));
        }

        return new MovementDigest(
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
            "The movement digest fixture carries an empty hash field.");

        return ulong.Parse(
            value!,
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture);
    }

    private sealed record MovementTickRow(
        long Tick,
        int EventCount,
        ulong EventFold,
        ulong StateHash);

    private sealed record MovementAgentRow(
        ulong EntityId,
        int XRaw,
        int YRaw,
        int HitPoints,
        string Intent,
        string MovementResolution,
        string Loadout,
        int ContingentId,
        int ContingentState);

    private sealed record MovementDigest(
        long TerminalTick,
        string Outcome,
        int Faction0Survivors,
        int Faction1Survivors,
        ulong TerminalStateHash,
        IReadOnlyList<MovementTickRow> Ticks,
        IReadOnlyList<MovementAgentRow> FinalAgents);
}
