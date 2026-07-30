using System.Collections.Immutable;

using Hukbo.Core.Combat;
using Hukbo.Core.Determinism;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Core.Movement.Profiles;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// The tall-hardwood-shield slice of the movement scenario matrix, run as
/// whole battles. The matrix itself (<see cref="MovementScenarioMatrix"/>)
/// only enumerates cells; this class consumes the shielded ones: the eleven
/// shield-containing unordered 1v1 pairs run as mirrored duels across the
/// approved seed set under a twin-rerun determinism check, the 176
/// shield-containing team matchups run as mirrored 2v2 battles at seed one,
/// and the focused geometries, group cases, asymmetric counts, and roster
/// preservation cases pin the movement shape of the shielded Kalis
/// (<c>KS</c>) and shielded Itak (<c>IS</c>) rows.
/// </summary>
/// <remarks>
/// <para>
/// Every threshold, pace, distance, and count boundary consumed here is a
/// <strong>provisional reconstruction: gameplay tuning; no historical
/// measurement</strong>. The evidence ledger is
/// docs/research/movement/tall-hardwood-shield.md, and the values are owned
/// by <see cref="TallHardwoodMovementProfiles"/>, which this class only
/// reads.
/// </para>
/// <para>
/// Every simulation names <see cref="CombatPresetId.PrecolonialPhilippinesV2"/>
/// and <see cref="MovementPresetId.EquipmentRelativeFootworkV6"/> explicitly:
/// V2 is the only combat preset fielding both shielded loadouts, and V6 is
/// the only movement preset that reads the shield rows. Neither shipped
/// default is changed anywhere in this file.
/// </para>
/// <para>
/// No assertion in this class reads a winner. Matchup cells assert the
/// movement contract — determinism, step legality, declared phases, spacing,
/// clearance, and lifecycle bounds — never the outcome. Win rates, equal win
/// rates, universal shield dominance, and the absence of a viable shieldless
/// entry are outcome statistics; they are calibration evidence for the
/// orchestrator's benchmark runs, not pass criteria, and they are deliberately
/// absent here.
/// </para>
/// <para>
/// The weapon plan's 100v100 and 250v250 cases are mass workloads. They belong
/// to <c>scripts/benchmark.ps1</c> and the orchestrator's calibration
/// evidence, not to a unit test inside the canonical gate. The Kampilan
/// session recorded exactly this decision in
/// <c>KampilanMovementTests.AKampilanGroupScenarioStaysDeterministicAndBounded</c>;
/// this class follows it.
/// </para>
/// </remarks>
public sealed class TallHardwoodMovementScenarioTests
{
    // ----- Index spaces: the movement canonical order, not the combat roster -----

    /// <summary>
    /// The shielded Kalis position in the <em>movement</em> canonical order
    /// <c>KP, WA, KA, IT, KS, IS</c>. This is the index
    /// <see cref="MovementRuleset.LoadoutMovementProfiles"/> and the six
    /// opponent-distance offset cells use. It is <strong>not</strong> the
    /// combat roster slot.
    /// </summary>
    private const int ShieldedKalisMovementIndex = 4;

    /// <summary>
    /// The shielded Itak position in the movement canonical order.
    /// </summary>
    private const int ShieldedItakMovementIndex = 5;

    private const int SoloKampilanMovementIndex = 0;

    private const int SoloWasayMovementIndex = 1;

    private const int SoloKalisMovementIndex = 2;

    private const int SoloItakMovementIndex = 3;

    /// <summary>
    /// The shielded Kalis slot in <see cref="CombatPresetId.PrecolonialPhilippinesV2"/>'s
    /// combat roster, which is ordered weapon-first and solo before paired
    /// within a weapon: <c>KP, WA, KA, KS, IT, IS</c>. Shielded Kalis is slot
    /// three here while it is movement index four, and
    /// <see cref="Scenario.RosterCounts"/> is indexed by this roster. A
    /// movement index is never reused as a roster slot.
    /// </summary>
    private const int ShieldedKalisRosterSlot = 3;

    /// <summary>
    /// The shielded Itak slot in the V2 combat roster. Slot five in both
    /// index spaces, which is a coincidence of the two orderings rather than
    /// a shared rule.
    /// </summary>
    private const int ShieldedItakRosterSlot = 5;

    /// <summary>The number of entries in the V2 combat roster.</summary>
    private const int VTwoRosterLength = 6;

    // ----- Shared probe constants, matching the foundation derivation -----

    private const int AttackRangeRaw = 5 * FixedPoint.Scale; // 5120

    private const int BodyRadiusRaw = FixedPoint.Scale / 2; // 512

    private const int MovementSpeedRaw = FixedPoint.Scale / 2; // 512

    /// <summary>
    /// The cooldown value written onto an observed agent before each tick so
    /// no accepted attack can open a commitment and mask the footwork
    /// transition under observation. The same device the Itak session used.
    /// </summary>
    private const int CooldownPin = 100;

    /// <summary>Half of the 200-cell-wide map, in raw units.</summary>
    private const int MapCenterXRaw = 102_400;

    /// <summary>The mirrored 1v1 and 2v2 x offset from the map centre.</summary>
    private const int MirrorOffsetXRaw = 10_240;

    /// <summary>The shared 1v1 row, the vertical map centre.</summary>
    private const int MirrorYRaw = 51_200;

    /// <summary>The first team member's row in the 2v2 geometry.</summary>
    private const int TeamFirstMemberYRaw = 49_152;

    /// <summary>The second team member's row in the 2v2 geometry.</summary>
    private const int TeamSecondMemberYRaw = 53_248;

    private const int OneVersusOneTicks = 600;

    private const int TeamMatchupTicks = 200;

    /// <summary>
    /// The eleven shield-containing unordered 1v1 cells, and the 176
    /// shield-containing team matchups, as
    /// <c>MovementScenarioMatrixTests.OneVersusOnePairs_FlagExactlyTheShieldedCellsAsRequiringV2</c>
    /// and its team counterpart already count them from the other side.
    /// </summary>
    private const int ShieldOneVersusOneCellCount = 11;

    private const int ShieldTeamMatchupCellCount = 176;

    private static readonly ulong[] ApprovedSeeds = [1, 2, 3, 5, 8];

    private static readonly CombatLoadout ShieldedKalis =
        new(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.TallHardwood);

    private static readonly CombatLoadout ShieldedItak =
        new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.TallHardwood);

    private static readonly CombatLoadout SoloKalis =
        new(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.None);

    private static readonly CombatLoadout SoloItak =
        new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.None);

    private static readonly CombatLoadout SoloKampilan =
        new(WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None);

    private static readonly CombatLoadout SoloWasay =
        new(WeaponId.Wasay, ArmorId.LightOrganic, ShieldId.None);

    private static MovementRuleset V6 =>
        MovementPresetRegistry.Get(MovementPresetId.EquipmentRelativeFootworkV6);

    private static CombatRuleset CombatRules =>
        CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV2);

    // ----- The shielded 1v1 slice: eleven of the twenty-one cells -----

    /// <summary>
    /// Of the 21 unordered 1v1 cells, exactly eleven contain a shielded
    /// loadout. Four unshielded loadouts give <c>C(4,2) + 4 = 10</c>
    /// unshielded cells, so <c>21 - 10 = 11</c>. <c>KS-IS</c> is one unordered
    /// cell, not two, which is why the count is eleven rather than twelve.
    /// The slice is asserted for its count, its uniqueness, its two shielded
    /// mirrors, and its canonical enumeration order before any simulation
    /// runs, and it is selected through the matrix's own
    /// <c>RequiresPrecolonialPhilippinesV2</c> predicate rather than a
    /// hand-rolled index check.
    /// </summary>
    [Fact]
    public void TheShieldOneVersusOneSliceCountsElevenUniqueCellsInCanonicalOrder()
    {
        var all = MovementScenarioMatrix.EnumerateOneVersusOnePairs();
        var cells = all
            .Where(pair => pair.RequiresPrecolonialPhilippinesV2)
            .ToList();

        Assert.Equal(ShieldOneVersusOneCellCount, cells.Count);
        Assert.Equal(ShieldOneVersusOneCellCount, cells.Distinct().Count());

        // Both shielded mirrors are present, and exactly one KS-IS cell.
        Assert.Contains(cells, pair =>
            pair.IsMirror &&
            pair.FirstLoadoutIndex == ShieldedKalisMovementIndex);
        Assert.Contains(cells, pair =>
            pair.IsMirror &&
            pair.FirstLoadoutIndex == ShieldedItakMovementIndex);
        Assert.Single(
            cells,
            pair =>
                pair.FirstLoadoutIndex == ShieldedKalisMovementIndex &&
                pair.SecondLoadoutIndex == ShieldedItakMovementIndex);

        // Canonical ordering: every cell is normalised i <= j, and the slice
        // is a subsequence of the full canonical enumeration in order.
        Assert.All(cells, pair => Assert.True(
            pair.FirstLoadoutIndex <= pair.SecondLoadoutIndex,
            $"Cell ({pair.FirstLoadoutIndex},{pair.SecondLoadoutIndex}) is " +
            "not normalised to canonical i <= j order."));
        Assert.Equal(
            all.Where(pair => pair.RequiresPrecolonialPhilippinesV2).ToList(),
            cells);
    }

    public static TheoryData<int, int> ShieldOneVersusOneCells()
    {
        var data = new TheoryData<int, int>();
        foreach (var pair in MovementScenarioMatrix.EnumerateOneVersusOnePairs())
        {
            if (pair.RequiresPrecolonialPhilippinesV2)
            {
                data.Add(pair.FirstLoadoutIndex, pair.SecondLoadoutIndex);
            }
        }

        return data;
    }

    /// <summary>
    /// Every shield-containing 1v1 cell runs a mirrored duel on each of the
    /// approved seeds <c>1, 2, 3, 5, 8</c>, twice per seed from an identical
    /// construction, and the two runs agree on the state hash, the ordered
    /// event stream, and the outcome. The per-tick movement contract holds
    /// throughout: no agent exceeds the per-axis speed baseline, no agent's
    /// Euclidean step exceeds the baseline by more than the documented
    /// one-raw-unit integer-lattice truncation headroom, every footwork phase
    /// and posture is a declared member, and no lifecycle timer goes negative.
    /// The isolation invariant is the twin run itself: the repeat construction
    /// is built and advanced after the subject run has finished, so a cell
    /// that leaked state across runs would diverge. The progress invariant is
    /// the no-progress streak — a cell that neither terminates nor moves nor
    /// emits an event for the whole budget is a defect, not a slow test.
    /// </summary>
    [Theory]
    [MemberData(nameof(ShieldOneVersusOneCells))]
    public void EveryShieldOneVersusOneCellReplaysIdenticallyOnEveryApprovedSeed(
        int firstIndex,
        int secondIndex)
    {
        foreach (var seed in ApprovedSeeds)
        {
            var scenario = CreateScenario(seed);
            var cellName = $"{CellName(firstIndex, secondIndex)} seed {seed}";

            AgentState[] Build() =>
            [
                CreateAgent(
                    1,
                    factionId: 0,
                    MapCenterXRaw - MirrorOffsetXRaw,
                    MirrorYRaw,
                    scenario,
                    MovementScenarioMatrix.CanonicalLoadouts[firstIndex]),
                CreateAgent(
                    2,
                    factionId: 1,
                    MapCenterXRaw + MirrorOffsetXRaw,
                    MirrorYRaw,
                    scenario,
                    MovementScenarioMatrix.CanonicalLoadouts[secondIndex]),
            ];

            var run = RunToCompletion(scenario, Build(), OneVersusOneTicks);
            var repeat = RunToCompletion(scenario, Build(), OneVersusOneTicks);

            AssertRunContract(run, repeat, cellName, OneVersusOneTicks);
        }
    }

    // ----- The shielded team-matchup slice: 176 of the 231 cells -----

    /// <summary>
    /// Of the 231 team matchups, exactly 176 contain a shielded loadout. The
    /// ten shield-free two-member compositions over <c>KP, WA, KA, IT</c>
    /// give <c>C(10,2) + 10 = 55</c> shield-free matchups, so
    /// <c>231 - 55 = 176</c>. Counted, deduplicated, checked for the twenty-one
    /// team mirrors that contain a shield, and checked for canonical
    /// enumeration order before any simulation runs.
    /// </summary>
    [Fact]
    public void TheShieldTeamMatchupSliceCountsOneHundredSeventySixUniqueCellsInCanonicalOrder()
    {
        var all = MovementScenarioMatrix.EnumerateTeamMatchups();
        var cells = all
            .Where(matchup => matchup.RequiresPrecolonialPhilippinesV2)
            .ToList();

        Assert.Equal(ShieldTeamMatchupCellCount, cells.Count);
        Assert.Equal(ShieldTeamMatchupCellCount, cells.Distinct().Count());

        // Eleven of the twenty-one team compositions contain a shield, and a
        // matchup of one against itself is a mirror, so eleven of the
        // twenty-one team mirrors fall inside the slice.
        var shieldedCompositions = MovementScenarioMatrix
            .EnumerateTeamCompositions()
            .Where(team => team.ContainsShieldedLoadout)
            .ToList();
        Assert.Equal(ShieldOneVersusOneCellCount, shieldedCompositions.Count);
        var mirrors = cells.Where(matchup => matchup.IsMirror).ToList();
        Assert.Equal(ShieldOneVersusOneCellCount, mirrors.Count);
        Assert.Equal(
            shieldedCompositions,
            mirrors.Select(matchup => matchup.FirstTeam).ToList());

        // Canonical ordering: each team is normalised i <= j, and the slice
        // is a subsequence of the full canonical enumeration in order.
        Assert.All(cells, matchup =>
        {
            Assert.True(
                matchup.FirstTeam.FirstMemberIndex <=
                matchup.FirstTeam.SecondMemberIndex,
                "The first team is not normalised to canonical order.");
            Assert.True(
                matchup.SecondTeam.FirstMemberIndex <=
                matchup.SecondTeam.SecondMemberIndex,
                "The second team is not normalised to canonical order.");
        });
        Assert.Equal(
            all.Where(m => m.RequiresPrecolonialPhilippinesV2).ToList(),
            cells);
    }

    public static TheoryData<int, int, int, int> ShieldTeamMatchupCells()
    {
        var data = new TheoryData<int, int, int, int>();
        foreach (var matchup in MovementScenarioMatrix.EnumerateTeamMatchups())
        {
            if (matchup.RequiresPrecolonialPhilippinesV2)
            {
                data.Add(
                    matchup.FirstTeam.FirstMemberIndex,
                    matchup.FirstTeam.SecondMemberIndex,
                    matchup.SecondTeam.FirstMemberIndex,
                    matchup.SecondTeam.SecondMemberIndex);
            }
        }

        return data;
    }

    /// <summary>
    /// Every shield-containing team matchup runs a mirrored 2v2 twice from an
    /// identical construction and holds the same per-tick movement contract,
    /// determinism, and progress invariants as the 1v1 slice.
    /// </summary>
    /// <remarks>
    /// This slice runs at seed one only, deliberately. The full approved seed
    /// set across 176 cells at two runs per cell would put 1,760 simulations
    /// inside one file inside the canonical gate, which the gate does not need
    /// and should not pay for: the seed sweep is exercised end to end by the
    /// eleven 1v1 cells above, which do run all five seeds, and the property
    /// this slice adds is combinatorial coverage of team composition rather
    /// than seed coverage.
    /// </remarks>
    [Theory]
    [MemberData(nameof(ShieldTeamMatchupCells))]
    public void EveryShieldTeamMatchupCellReplaysIdenticallyAtSeedOne(
        int firstTeamFirstIndex,
        int firstTeamSecondIndex,
        int secondTeamFirstIndex,
        int secondTeamSecondIndex)
    {
        var scenario = CreateScenario();
        var loadouts = MovementScenarioMatrix.CanonicalLoadouts;
        var cellName =
            $"{CellName(firstTeamFirstIndex, firstTeamSecondIndex)} vs " +
            CellName(secondTeamFirstIndex, secondTeamSecondIndex);

        AgentState[] Build() =>
        [
            CreateAgent(
                1,
                factionId: 0,
                MapCenterXRaw - MirrorOffsetXRaw,
                TeamFirstMemberYRaw,
                scenario,
                loadouts[firstTeamFirstIndex]),
            CreateAgent(
                2,
                factionId: 0,
                MapCenterXRaw - MirrorOffsetXRaw,
                TeamSecondMemberYRaw,
                scenario,
                loadouts[firstTeamSecondIndex]),
            CreateAgent(
                3,
                factionId: 1,
                MapCenterXRaw + MirrorOffsetXRaw,
                TeamFirstMemberYRaw,
                scenario,
                loadouts[secondTeamFirstIndex]),
            CreateAgent(
                4,
                factionId: 1,
                MapCenterXRaw + MirrorOffsetXRaw,
                TeamSecondMemberYRaw,
                scenario,
                loadouts[secondTeamSecondIndex]),
        ];

        var run = RunToCompletion(scenario, Build(), TeamMatchupTicks);
        var repeat = RunToCompletion(scenario, Build(), TeamMatchupTicks);

        AssertRunContract(run, repeat, cellName, TeamMatchupTicks);
    }

    // ----- Focused matchup geometries -----

    public static TheoryData<int, int> FocusedShieldMatchups()
    {
        var data = new TheoryData<int, int>();
        foreach (var actorIndex in
            new[] { ShieldedKalisMovementIndex, ShieldedItakMovementIndex })
        {
            for (var opponentIndex = 0;
                opponentIndex < MovementScenarioMatrix.CanonicalLoadoutCount;
                opponentIndex++)
            {
                data.Add(actorIndex, opponentIndex);
            }
        }

        return data;
    }

    /// <summary>
    /// The twelve focused shield matchups — each shield row against each of
    /// the six canonical loadouts — observed as spacing rather than as an
    /// outcome. From a mirrored start well outside every band, the shield
    /// bearer's first <see cref="FootworkPhase.Engage"/> tick lands exactly on
    /// its own offset-adjusted preferred distance for that opponent: the
    /// tick-start separation the decision read is at or inside the effective
    /// preferred distance, and the previous tick's separation was strictly
    /// outside it. Entry is inclusive, so equality enters. Each expectation is
    /// derived through
    /// <see cref="MovementRouteRules.EffectivePreferredDistanceRaw"/> from the
    /// row and the opponent's canonical index rather than hand-multiplied, and
    /// each agent carries its weapon's real reach from the named combat preset,
    /// so the band under observation is the one a full-roster run uses.
    /// </summary>
    /// <remarks>
    /// This is the shield-owned counterpart of
    /// <c>MovementPipelineIntegrationTests</c>' general band coverage and of
    /// <c>ItakMovementProfileTests.TheShieldedItakEffectivePreferredDistanceCoversEveryOpponentColumn</c>,
    /// which pins the shielded Itak column values as literals from the
    /// profile side. Here the same quantity is observed end to end through a
    /// whole tick pipeline. Every value is a provisional reconstruction:
    /// gameplay tuning; no historical measurement
    /// (docs/research/movement/tall-hardwood-shield.md).
    /// </remarks>
    [Theory]
    [MemberData(nameof(FocusedShieldMatchups))]
    public void EveryFocusedShieldMatchupEntersTheEngageBandAtItsOffsetAdjustedPreferredDistance(
        int actorIndex,
        int opponentIndex)
    {
        var loadouts = MovementScenarioMatrix.CanonicalLoadouts;
        var actorLoadout = loadouts[actorIndex];
        var opponentLoadout = loadouts[opponentIndex];
        var scenario = CreateScenario();
        var cellName = CellName(actorIndex, opponentIndex);

        var entry = FirstEngageEntry(
            scenario, actorLoadout, opponentLoadout, tickBound: 200);

        Assert.NotNull(entry);
        var preferredRaw = EffectivePreferredRaw(actorLoadout, opponentLoadout);
        var preferredSquared = checked(preferredRaw * preferredRaw);
        Assert.True(
            entry.EntrySquared <= preferredSquared,
            $"Cell {cellName}: the tick-start separation squared " +
            $"{entry.EntrySquared} at the first Engage tick sits outside the " +
            $"effective preferred distance squared {preferredSquared}.");
        Assert.True(
            entry.PriorSquared > preferredSquared,
            $"Cell {cellName}: the separation squared {entry.PriorSquared} " +
            "one tick before entry was already at or inside the effective " +
            $"preferred distance squared {preferredSquared}, so the entry " +
            "tick was not the boundary crossing.");
    }

    /// <summary>
    /// The shielded Kalis holds the longer band. Against each solo opponent it
    /// enters its engage band strictly farther out than a solo Kalis does from
    /// the identical start, which is the observable form of the product
    /// statement that <c>KS</c> is the longer-spacing lane-control pairing.
    /// The comparison is between two runs of identical geometry differing only
    /// in the actor's shield, so nothing but the row can account for the gap.
    /// Provisional reconstruction: gameplay tuning; no historical measurement
    /// (docs/research/movement/tall-hardwood-shield.md).
    /// </summary>
    [Theory]
    [InlineData(SoloKampilanMovementIndex)]
    [InlineData(SoloWasayMovementIndex)]
    [InlineData(SoloKalisMovementIndex)]
    [InlineData(SoloItakMovementIndex)]
    public void AShieldedKalisEntersItsEngageBandFartherOutThanASoloKalis(
        int opponentIndex)
    {
        var opponentLoadout =
            MovementScenarioMatrix.CanonicalLoadouts[opponentIndex];
        var scenario = CreateScenario();

        var shielded = FirstEngageEntry(
            scenario, ShieldedKalis, opponentLoadout, tickBound: 200);
        var solo = FirstEngageEntry(
            scenario, SoloKalis, opponentLoadout, tickBound: 200);

        Assert.NotNull(shielded);
        Assert.NotNull(solo);
        Assert.True(
            shielded.EntrySquared > solo.EntrySquared,
            "The shielded Kalis entered its band at separation squared " +
            $"{shielded.EntrySquared}, not farther out than the solo Kalis " +
            $"at {solo.EntrySquared}.");
        Assert.True(
            EffectivePreferredRaw(ShieldedKalis, opponentLoadout) >
            EffectivePreferredRaw(SoloKalis, opponentLoadout),
            "The shielded Kalis effective preferred distance is not longer " +
            "than the solo Kalis one against this opponent.");
    }

    /// <summary>
    /// The mixed shield cell, in both directions. Inside a single
    /// <c>KS</c>-versus-<c>IS</c> duel the shielded Kalis enters its band
    /// strictly farther out than the shielded Itak does, so the shielded Itak
    /// respects the longer Kalis band rather than dictating the spacing; and
    /// the shielded Itak carries the higher lateral pace cap of the two rows,
    /// 8,700 basis points against the shielded Kalis's 8,400, which is what
    /// makes it the closer repositioning pairing. The two literals are pinned
    /// field-for-field by
    /// <c>TallHardwoodMovementProfileTests</c> and by
    /// <c>MovementProfileRegistrationTests</c>; asserted here as an ordering so
    /// the behavioural claim and the row cannot drift apart. Provisional
    /// reconstruction: gameplay tuning; no historical measurement
    /// (docs/research/movement/tall-hardwood-shield.md).
    /// </summary>
    [Fact]
    public void AShieldedKalisHoldsALongerBandThanAShieldedItakInTheMixedShieldCell()
    {
        var scenario = CreateScenario();

        var kalisEntry = FirstEngageEntry(
            scenario, ShieldedKalis, ShieldedItak, tickBound: 200);
        var itakEntry = FirstEngageEntry(
            scenario, ShieldedItak, ShieldedKalis, tickBound: 200);

        Assert.NotNull(kalisEntry);
        Assert.NotNull(itakEntry);
        Assert.True(
            kalisEntry.EntrySquared > itakEntry.EntrySquared,
            "The shielded Kalis did not enter its band farther out than the " +
            $"shielded Itak: {kalisEntry.EntrySquared} against " +
            $"{itakEntry.EntrySquared}, squared.");
        Assert.True(
            TallHardwoodMovementProfiles.ItakRow.LateralPaceBasisPoints >
            TallHardwoodMovementProfiles.KalisRow.LateralPaceBasisPoints,
            "The shielded Itak no longer carries the higher lateral pace cap.");
    }

    public static TheoryData<int, int, int> ShieldAgainstSoloCounterpartCells()
    {
        var data = new TheoryData<int, int, int>();
        foreach (var (shieldedIndex, soloIndex) in new[]
        {
            (ShieldedKalisMovementIndex, SoloKalisMovementIndex),
            (ShieldedItakMovementIndex, SoloItakMovementIndex),
        })
        {
            foreach (var opponentIndex in new[]
            {
                SoloKampilanMovementIndex,
                SoloWasayMovementIndex,
                SoloKalisMovementIndex,
                SoloItakMovementIndex,
            })
            {
                data.Add(shieldedIndex, soloIndex, opponentIndex);
            }
        }

        return data;
    }

    /// <summary>
    /// A shield never grants a speed bonus, observed end to end. In two runs
    /// of identical geometry against the same opponent, differing only in
    /// whether the actor carries a tall hardwood shield, the shielded run's
    /// largest single-tick displacement never exceeds the solo run's. Every
    /// pace field of both shield rows sits at or below its solo counterpart's,
    /// so no route, no direction band, and no lifecycle branch can produce a
    /// faster step for the shielded row. Provisional reconstruction: gameplay
    /// tuning; no historical measurement
    /// (docs/research/movement/tall-hardwood-shield.md).
    /// </summary>
    [Theory]
    [MemberData(nameof(ShieldAgainstSoloCounterpartCells))]
    public void NeitherShieldRowOutrunsItsSoloCounterpartInAnyFocusedMatchup(
        int shieldedIndex,
        int soloIndex,
        int opponentIndex)
    {
        var loadouts = MovementScenarioMatrix.CanonicalLoadouts;
        var scenario = CreateScenario();
        var opponent = loadouts[opponentIndex];

        AgentState[] Build(CombatLoadout actor) =>
        [
            CreateAgent(
                1,
                factionId: 0,
                MapCenterXRaw - MirrorOffsetXRaw,
                MirrorYRaw,
                scenario,
                actor),
            CreateAgent(
                2,
                factionId: 1,
                MapCenterXRaw + MirrorOffsetXRaw,
                MirrorYRaw,
                scenario,
                opponent),
        ];

        var shielded = RunToCompletion(
            scenario, Build(loadouts[shieldedIndex]), ticks: 300);
        var solo = RunToCompletion(
            scenario, Build(loadouts[soloIndex]), ticks: 300);

        Assert.True(
            shielded.MaximumDisplacementSquared <=
            solo.MaximumDisplacementSquared,
            "The shielded row's largest single-tick displacement squared " +
            $"{shielded.MaximumDisplacementSquared} exceeded the solo row's " +
            $"{solo.MaximumDisplacementSquared}, which would be a speed " +
            "bonus a shield must never grant.");
    }

    /// <summary>
    /// A shielded mirror cell resolves its ties deterministically and never
    /// settles into a wall. Two shield bearers a side, same row on both sides,
    /// mirrored about the map centre: the twin rerun is bit-identical, both
    /// factions' bearers reach <see cref="FootworkPhase.Engage"/>, and no
    /// bearer is refused for more than a bounded stretch — the probes
    /// terminate on the shared ordered conflict pass and the stable
    /// <c>EntityId</c> tie-break, with no bespoke deadlock rule, no
    /// shield-pair state, and no front-lock field anywhere in
    /// <see cref="AgentState"/>. Provisional reconstruction: gameplay tuning;
    /// no historical measurement
    /// (docs/research/movement/tall-hardwood-shield.md).
    /// </summary>
    [Theory]
    [InlineData(ShieldedKalisMovementIndex)]
    [InlineData(ShieldedItakMovementIndex)]
    public void EachShieldedMirrorCellBreaksItsTiesDeterministicallyWithoutAWall(
        int shieldIndex)
    {
        var loadout = MovementScenarioMatrix.CanonicalLoadouts[shieldIndex];
        var scenario = CreateScenario();

        AgentState[] Build() =>
        [
            CreateAgent(
                1,
                factionId: 0,
                MapCenterXRaw - MirrorOffsetXRaw,
                TeamFirstMemberYRaw,
                scenario,
                loadout),
            CreateAgent(
                2,
                factionId: 0,
                MapCenterXRaw - MirrorOffsetXRaw,
                TeamSecondMemberYRaw,
                scenario,
                loadout),
            CreateAgent(
                3,
                factionId: 1,
                MapCenterXRaw + MirrorOffsetXRaw,
                TeamFirstMemberYRaw,
                scenario,
                loadout),
            CreateAgent(
                4,
                factionId: 1,
                MapCenterXRaw + MirrorOffsetXRaw,
                TeamSecondMemberYRaw,
                scenario,
                loadout),
        ];

        var run = RunToCompletion(scenario, Build(), ticks: 400);
        var repeat = RunToCompletion(scenario, Build(), ticks: 400);
        var cellName = CellName(shieldIndex, shieldIndex);

        AssertRunContract(run, repeat, cellName, tickBudget: 400);
        Assert.True(
            run.MaximumRefuseStreak <= MaximumTolerableRefuseStreak,
            $"Cell {cellName}: a shield bearer was refused for " +
            $"{run.MaximumRefuseStreak} consecutive ticks, past the " +
            $"{MaximumTolerableRefuseStreak}-tick bound this mirror is " +
            "allowed, which reads as a wall rather than a tie-break.");
    }

    /// <summary>
    /// Every candidate lane blocked finalises <see cref="FootworkPhase.Refuse"/>
    /// on both shield rows. An ally stands 1,100 raw ahead on the direct line
    /// to the enemy. On the shielded Kalis the first-tick pace is the 286-raw
    /// acceleration step (512 &#215; 5600 / 10000), so the direct endpoint
    /// sits 814 raw from the ally and both 22.5-degree obliques nearer than
    /// 900 raw, all strictly inside the 1,433-raw shielded Kalis clearance
    /// radius. On the shielded Itak the first-tick pace is 332 raw
    /// (512 &#215; 6500 / 10000), the direct endpoint sits 768 raw out, and
    /// every oblique stays strictly inside the 1,382-raw shielded Itak radius.
    /// With no surviving candidate the approacher refuses, holds position, and
    /// retains zero pace. The Itak counterpart is
    /// <c>ItakMovementScenarioTests.AnItakApproachWithEveryLaneAllyBlockedFinalisesRefuse</c>.
    /// Provisional reconstruction: gameplay tuning; no historical measurement
    /// (docs/research/movement/tall-hardwood-shield.md).
    /// </summary>
    [Theory]
    [InlineData(ShieldedKalisMovementIndex)]
    [InlineData(ShieldedItakMovementIndex)]
    public void AShieldedApproachWithEveryLaneAllyBlockedFinalisesRefuse(
        int shieldIndex)
    {
        var loadout = MovementScenarioMatrix.CanonicalLoadouts[shieldIndex];
        var scenario = CreateScenario();
        var actor = CreateAgent(
            1, factionId: 0, 51_200, 51_200, scenario, loadout);
        var allyAhead = CreateAgent(
            2, factionId: 0, 52_300, 51_200, scenario, loadout);
        var enemy = CreateAgent(
            3, factionId: 1, 92_160, 51_200, scenario, loadout);
        var simulation = BattleSimulation.CreateForTesting(
            scenario, actor, allyAhead, enemy);

        simulation.AdvanceOneTick();

        Assert.Equal(FootworkPhase.Refuse, actor.FootworkPhase);
        Assert.Equal(0, actor.FootworkTicksRemaining);
        Assert.Equal(51_200, actor.XRaw);
        Assert.Equal(51_200, actor.YRaw);
        Assert.Equal(0, actor.MovementPaceRaw);
    }

    public static TheoryData<int, int, bool> ShieldLanePairings()
    {
        var data = new TheoryData<int, int, bool>();
        foreach (var (first, second) in new[]
        {
            (ShieldedKalisMovementIndex, ShieldedKalisMovementIndex),
            (ShieldedItakMovementIndex, ShieldedItakMovementIndex),
            (ShieldedKalisMovementIndex, ShieldedItakMovementIndex),
        })
        {
            data.Add(first, second, true);
            data.Add(first, second, false);
        }

        return data;
    }

    /// <summary>
    /// Two shielded allies on parallel lanes 2,048 raw apart — beyond both
    /// rows' clearance radii, 1,433 raw for the shielded Kalis and 1,382 raw
    /// for the shielded Itak — hold distinct lanes rather than stacking. On
    /// every tick where both allies' proposals were accepted unchanged their
    /// committed separation sits at or beyond the larger of the two radii, and
    /// they never share a position. Both faced with a mirrored enemy pair and
    /// faced with a single shared target: the single-target case is the one
    /// that would stack the pair behind one enemy if the conflict pass had a
    /// blind spot. There is no shield-pair state, no synchronised turn-taking,
    /// and no wall: the stagger comes from actual positions and the shared
    /// clearance scan. The Itak counterpart is
    /// <c>ItakMovementScenarioTests.TwoItakAlliesCooperateOnSeparateLanesTowardTheEnemy</c>.
    /// Provisional reconstruction: gameplay tuning; no historical measurement
    /// (docs/research/movement/tall-hardwood-shield.md).
    /// </summary>
    [Theory]
    [MemberData(nameof(ShieldLanePairings))]
    public void TwoShieldedAlliesHoldDistinctLanesRatherThanStacking(
        int firstIndex,
        int secondIndex,
        bool mirroredEnemyPair)
    {
        var loadouts = MovementScenarioMatrix.CanonicalLoadouts;
        var scenario = CreateScenario();
        var firstAlly = CreateAgent(
            1,
            factionId: 0,
            MapCenterXRaw - MirrorOffsetXRaw,
            MirrorYRaw - (LaneSeparationRaw / 2),
            scenario,
            loadouts[firstIndex]);
        var secondAlly = CreateAgent(
            2,
            factionId: 0,
            MapCenterXRaw - MirrorOffsetXRaw,
            MirrorYRaw + (LaneSeparationRaw / 2),
            scenario,
            loadouts[secondIndex]);
        var enemies = mirroredEnemyPair
            ? new[]
            {
                CreateAgent(
                    3,
                    factionId: 1,
                    MapCenterXRaw + MirrorOffsetXRaw,
                    MirrorYRaw - (LaneSeparationRaw / 2),
                    scenario,
                    loadouts[firstIndex]),
                CreateAgent(
                    4,
                    factionId: 1,
                    MapCenterXRaw + MirrorOffsetXRaw,
                    MirrorYRaw + (LaneSeparationRaw / 2),
                    scenario,
                    loadouts[secondIndex]),
            }
            : [
                CreateAgent(
                    3,
                    factionId: 1,
                    MapCenterXRaw + MirrorOffsetXRaw,
                    MirrorYRaw,
                    scenario,
                    loadouts[firstIndex]),
            ];
        var all = new[] { firstAlly, secondAlly }.Concat(enemies).ToArray();
        var simulation = BattleSimulation.CreateForTesting(scenario, all);

        var controllingRadius = Math.Max(
            ClearanceRadiusRawOf(loadouts[firstIndex]),
            ClearanceRadiusRawOf(loadouts[secondIndex]));
        var controllingSquared = checked(controllingRadius * controllingRadius);

        for (var tick = 0; tick < 120; tick++)
        {
            foreach (var agent in all)
            {
                agent.AttackCooldownRemaining = CooldownPin;
            }

            simulation.AdvanceOneTick();

            Assert.True(
                firstAlly.XRaw != secondAlly.XRaw ||
                firstAlly.YRaw != secondAlly.YRaw,
                $"Tick {simulation.Tick}: the two shielded allies shared a " +
                "position, so one stacked onto the other.");
            if (firstAlly.MovementResolution == MovementResolution.Moved &&
                secondAlly.MovementResolution == MovementResolution.Moved)
            {
                var separationSquared =
                    SquaredDistance(firstAlly, secondAlly);
                Assert.True(
                    separationSquared >= controllingSquared,
                    $"Tick {simulation.Tick}: both allies moved but their " +
                    $"separation squared {separationSquared} fell inside the " +
                    $"controlling clearance squared {controllingSquared}.");
            }
        }
    }

    public static TheoryData<int, int> ShieldAndAllyPairings()
    {
        var data = new TheoryData<int, int>();
        foreach (var shieldIndex in
            new[] { ShieldedKalisMovementIndex, ShieldedItakMovementIndex })
        {
            foreach (var allyIndex in new[]
            {
                SoloKampilanMovementIndex,
                SoloWasayMovementIndex,
                SoloKalisMovementIndex,
                SoloItakMovementIndex,
            })
            {
                data.Add(shieldIndex, allyIndex);
            }
        }

        return data;
    }

    /// <summary>
    /// Each shield bearer paired with a Kampilan, a Wasay, a solo Kalis, and a
    /// solo Itak ally: the larger of the two profiles' clearance radii
    /// controls the pair, in every one of the eight pairings. The six radii
    /// materialise to 1,433 shielded Kalis, 1,382 shielded Itak, 1,536
    /// Kampilan, 1,792 Wasay, 1,228 solo Kalis, and 1,177 solo Itak raw units,
    /// asserted here as literals derived through
    /// <see cref="MovementRouteRules.ClearanceRadiusRaw"/> so a silent formula
    /// change is caught, and every pair among them has a strictly larger
    /// member. On every tick where both allies' proposals were accepted
    /// unchanged the committed separation sits at or beyond that larger
    /// radius. Provisional reconstruction: gameplay tuning; no historical
    /// measurement (docs/research/movement/tall-hardwood-shield.md).
    /// </summary>
    [Theory]
    [MemberData(nameof(ShieldAndAllyPairings))]
    public void EveryShieldedAllyPairingRespectsTheLargerClearanceRadius(
        int shieldIndex,
        int allyIndex)
    {
        var loadouts = MovementScenarioMatrix.CanonicalLoadouts;
        var scenario = CreateScenario();
        var shieldRadius = ClearanceRadiusRawOf(loadouts[shieldIndex]);
        var allyRadius = ClearanceRadiusRawOf(loadouts[allyIndex]);
        Assert.NotEqual(shieldRadius, allyRadius);
        var controllingRadius = Math.Max(shieldRadius, allyRadius);
        var controllingSquared = checked(controllingRadius * controllingRadius);

        var shieldBearer = CreateAgent(
            1,
            factionId: 0,
            MapCenterXRaw - MirrorOffsetXRaw,
            MirrorYRaw - (LaneSeparationRaw / 2),
            scenario,
            loadouts[shieldIndex]);
        var ally = CreateAgent(
            2,
            factionId: 0,
            MapCenterXRaw - MirrorOffsetXRaw,
            MirrorYRaw + (LaneSeparationRaw / 2),
            scenario,
            loadouts[allyIndex]);
        var firstEnemy = CreateAgent(
            3,
            factionId: 1,
            MapCenterXRaw + MirrorOffsetXRaw,
            MirrorYRaw - (LaneSeparationRaw / 2),
            scenario,
            loadouts[shieldIndex]);
        var secondEnemy = CreateAgent(
            4,
            factionId: 1,
            MapCenterXRaw + MirrorOffsetXRaw,
            MirrorYRaw + (LaneSeparationRaw / 2),
            scenario,
            loadouts[allyIndex]);
        var all = new[] { shieldBearer, ally, firstEnemy, secondEnemy };
        var simulation = BattleSimulation.CreateForTesting(scenario, all);

        for (var tick = 0; tick < 120; tick++)
        {
            foreach (var agent in all)
            {
                agent.AttackCooldownRemaining = CooldownPin;
            }

            simulation.AdvanceOneTick();

            if (shieldBearer.MovementResolution == MovementResolution.Moved &&
                ally.MovementResolution == MovementResolution.Moved)
            {
                var separationSquared = SquaredDistance(shieldBearer, ally);
                Assert.True(
                    separationSquared >= controllingSquared,
                    $"Tick {simulation.Tick}: both allies moved but their " +
                    $"separation squared {separationSquared} fell inside the " +
                    $"controlling clearance squared {controllingSquared}.");
            }
        }
    }

    /// <summary>
    /// The six clearance radii this file reasons about, materialised through
    /// <see cref="MovementRouteRules.ClearanceRadiusRaw"/> at the shared
    /// 512-raw body radius and asserted as literals. A shield row's radius sits
    /// exactly 2,000 basis points of body diameter above its solo
    /// counterpart's, which is 204 raw units of extra lane after truncation for
    /// the shielded Kalis and 205 for the shielded Itak. Provisional
    /// reconstruction: gameplay tuning; no historical measurement
    /// (docs/research/movement/tall-hardwood-shield.md).
    /// </summary>
    [Fact]
    public void TheSixClearanceRadiiMaterialiseToTheirPinnedRawValues()
    {
        Assert.Equal(1_433L, ClearanceRadiusRawOf(ShieldedKalis));
        Assert.Equal(1_382L, ClearanceRadiusRawOf(ShieldedItak));
        Assert.Equal(1_536L, ClearanceRadiusRawOf(SoloKampilan));
        Assert.Equal(1_792L, ClearanceRadiusRawOf(SoloWasay));
        Assert.Equal(1_228L, ClearanceRadiusRawOf(SoloKalis));
        Assert.Equal(1_177L, ClearanceRadiusRawOf(SoloItak));
    }

    /// <summary>
    /// A shielded recovery spell never outlasts its row's declared
    /// <see cref="LoadoutMovementProfile.RecoveryTicks"/>. Against a Wasay,
    /// whose four-tick commitment and 1,792-raw clearance radius put the most
    /// pressure on a shield bearer's repositioning window, both rows recover
    /// for at most their three declared ticks per spell — a bearer
    /// repositioning during recovery leaves recovery rather than circling
    /// indefinitely. Only the bearer is observed: the Wasay opposite it
    /// declares four recovery ticks of its own, so a bound taken across both
    /// agents would be measuring the wrong row. Provisional reconstruction:
    /// gameplay tuning; no historical measurement
    /// (docs/research/movement/tall-hardwood-shield.md).
    /// </summary>
    [Theory]
    [InlineData(ShieldedKalisMovementIndex)]
    [InlineData(ShieldedItakMovementIndex)]
    public void AShieldedRecoverySpellNeverOutlastsItsProfileRecoveryTicks(
        int shieldIndex)
    {
        var loadout = MovementScenarioMatrix.CanonicalLoadouts[shieldIndex];
        var scenario = CreateScenario();
        var profile = V6.ResolveLoadoutProfile(loadout);
        var bearer = CreateAgent(
            1,
            factionId: 0,
            MapCenterXRaw - MirrorOffsetXRaw,
            MirrorYRaw,
            scenario,
            loadout);
        var wasay = CreateAgent(
            2,
            factionId: 1,
            MapCenterXRaw + MirrorOffsetXRaw,
            MirrorYRaw,
            scenario,
            SoloWasay);
        var simulation = BattleSimulation.CreateForTesting(
            scenario, bearer, wasay);

        var streak = 0;
        var longestStreak = 0;
        for (var tick = 0; tick < 400; tick++)
        {
            simulation.AdvanceOneTick();
            if (bearer.FootworkPhase == FootworkPhase.Recover)
            {
                streak++;
                longestStreak = Math.Max(longestStreak, streak);
            }
            else
            {
                streak = 0;
            }
        }

        Assert.True(
            longestStreak > 0,
            "The probe never recovered, so the recovery bound could not be " +
            "observed.");
        Assert.True(
            longestStreak <= profile.RecoveryTicks,
            $"A recovery spell ran {longestStreak} consecutive ticks against " +
            $"the row's declared {profile.RecoveryTicks}.");
    }

    /// <summary>
    /// An outnumbered shield bearer keeps its reverse clearance. One bearer
    /// with a single ally faces two Wasay warriors inside the support radius —
    /// past both rows' entry ratios, <c>2 &#215; 4 &#8805; 1 &#215; 7</c> for
    /// the shielded Kalis and <c>2 &#215; 2 &#8805; 1 &#215; 3</c> for the
    /// shielded Itak — so it disengages, and the exit actually opens: its
    /// distance to the nearest enemy does not shrink on every post-entry tick.
    /// A live but distant ally keeps the global headcount level so the observed
    /// entry can only be the support-ratio step, never the unconditional
    /// posture step, which is asserted every tick. Provisional reconstruction:
    /// gameplay tuning; no historical measurement
    /// (docs/research/movement/tall-hardwood-shield.md).
    /// </summary>
    [Theory]
    [InlineData(ShieldedKalisMovementIndex)]
    [InlineData(ShieldedItakMovementIndex)]
    public void AnOutnumberedShieldBearerOpensItsExitAgainstTwoWasayWarriors(
        int shieldIndex)
    {
        var loadout = MovementScenarioMatrix.CanonicalLoadouts[shieldIndex];
        var scenario = CreateScenario();
        var actor = CreateAgent(
            1, factionId: 0, 51_200, 51_200, scenario, loadout);
        var farAlly = CreateAgent(
            2, factionId: 0, 10_240, 10_240, scenario, loadout);
        var firstEnemy = CreateAgent(
            3, factionId: 1, 55_296, 50_176, scenario, SoloWasay);
        var secondEnemy = CreateAgent(
            4, factionId: 1, 55_296, 52_224, scenario, SoloWasay);
        var simulation = BattleSimulation.CreateForTesting(
            scenario, actor, farAlly, firstEnemy, secondEnemy);

        var entrySeen = false;
        var postEntryDistances = new List<long>();
        for (var tick = 0; tick < 80; tick++)
        {
            actor.AttackCooldownRemaining = CooldownPin;
            simulation.AdvanceOneTick();

            Assert.NotEqual(TacticalPosture.Withdraw, actor.TacticalPosture);
            Assert.NotEqual(TacticalPosture.Yield, actor.TacticalPosture);
            entrySeen |= actor.FootworkPhase == FootworkPhase.Disengage;
            if (entrySeen)
            {
                postEntryDistances.Add(Math.Min(
                    SquaredDistance(actor, firstEnemy),
                    SquaredDistance(actor, secondEnemy)));
            }
        }

        Assert.True(entrySeen, "The outnumbered shield bearer never disengaged.");
        Assert.True(
            postEntryDistances.Count >= 2,
            "The entry left no post-entry ticks to observe.");
        var sawNonShrinkingStep = false;
        for (var index = 1; index < postEntryDistances.Count; index++)
        {
            sawNonShrinkingStep |=
                postEntryDistances[index] >= postEntryDistances[index - 1];
        }

        Assert.True(
            sawNonShrinkingStep,
            "The bearer's distance to the nearest enemy shrank on every " +
            "post-entry tick, so the reverse exit never opened.");
    }

    public static TheoryData<int, int> ShieldClosingCells()
    {
        var data = new TheoryData<int, int>();
        data.Add(ShieldedKalisMovementIndex, SoloKalisMovementIndex);
        data.Add(ShieldedItakMovementIndex, SoloKalisMovementIndex);
        data.Add(ShieldedItakMovementIndex, SoloItakMovementIndex);
        data.Add(ShieldedKalisMovementIndex, SoloItakMovementIndex);
        return data;
    }

    /// <summary>
    /// A shield bearer closes through a free line and then restores an exit.
    /// Facing a single solo opponent with no ally in the way, the bearer's
    /// separation from its target falls well inside its opening value, and at
    /// some later tick grows again rather than pinning permanently at contact:
    /// the observable form of a maintained band, and the reason a solo
    /// opponent gets no free close entry against a shielded row. Provisional
    /// reconstruction: gameplay tuning; no historical measurement
    /// (docs/research/movement/tall-hardwood-shield.md).
    /// </summary>
    [Theory]
    [MemberData(nameof(ShieldClosingCells))]
    public void AShieldedBearerClosesThroughAFreeLineAndThenRestoresAnExit(
        int shieldIndex,
        int opponentIndex)
    {
        var loadouts = MovementScenarioMatrix.CanonicalLoadouts;
        var scenario = CreateScenario();
        var actor = CreateAgent(
            1,
            factionId: 0,
            MapCenterXRaw - MirrorOffsetXRaw,
            MirrorYRaw,
            scenario,
            loadouts[shieldIndex]);
        var opponent = CreateAgent(
            2,
            factionId: 1,
            MapCenterXRaw + MirrorOffsetXRaw,
            MirrorYRaw,
            scenario,
            loadouts[opponentIndex]);
        var simulation = BattleSimulation.CreateForTesting(
            scenario, actor, opponent);

        var initialSquared = SquaredDistance(actor, opponent);
        var minimumSquared = initialSquared;
        var sawGrowth = false;
        var previousSquared = initialSquared;

        for (var tick = 0; tick < 400; tick++)
        {
            simulation.AdvanceOneTick();

            var currentSquared = SquaredDistance(actor, opponent);
            minimumSquared = Math.Min(minimumSquared, currentSquared);
            sawGrowth |= currentSquared > previousSquared;
            previousSquared = currentSquared;
        }

        Assert.True(
            minimumSquared < initialSquared,
            "The shield bearer never closed on its opponent.");
        Assert.True(
            sawGrowth,
            "The separation never grew on any tick, so the bearer pinned at " +
            "contact instead of restoring an exit.");
    }

    // ----- Group cases -----

    public static TheoryData<int, int, int> ShieldGroupSizes()
    {
        var data = new TheoryData<int, int, int>();
        foreach (var (factionZeroCount, factionOneCount) in new[]
        {
            (1, 2), (2, 3), (3, 5), (4, 4), (5, 5), (8, 8),
        })
        {
            // Composition selector: the two homogeneous shield rows, then a
            // mixed group that alternates the two rows on both sides.
            data.Add(factionZeroCount, factionOneCount, ShieldedKalisMovementIndex);
            data.Add(factionZeroCount, factionOneCount, ShieldedItakMovementIndex);
            data.Add(factionZeroCount, factionOneCount, MixedShieldComposition);
        }

        return data;
    }

    /// <summary>
    /// Shield-present groups at every small and mid size — homogeneous
    /// shielded Kalis, homogeneous shielded Itak, and a mixed shield group
    /// alternating the two rows — stay deterministic and inside the shared
    /// legality bounds, including the three asymmetric sizes 1v2, 2v3, and
    /// 3v5. The weapon plan's 100v100 and 250v250 cases are mass workloads and
    /// belong to <c>scripts/benchmark.ps1</c> and the orchestrator's
    /// calibration evidence, not to a unit test inside the canonical gate; the
    /// Kampilan session recorded exactly this decision and this file follows
    /// it. Provisional reconstruction: gameplay tuning; no historical
    /// measurement (docs/research/movement/tall-hardwood-shield.md).
    /// </summary>
    [Theory]
    [MemberData(nameof(ShieldGroupSizes))]
    public void AShieldGroupScenarioStaysDeterministicAndBounded(
        int factionZeroCount,
        int factionOneCount,
        int composition)
    {
        var scenario = CreateScenario();
        var cellName =
            $"{factionZeroCount}v{factionOneCount} composition {composition}";

        CombatLoadout Pick(int index) => composition switch
        {
            MixedShieldComposition => index % 2 == 0
                ? ShieldedKalis
                : ShieldedItak,
            _ => MovementScenarioMatrix.CanonicalLoadouts[composition],
        };

        AgentState[] Build()
        {
            var agents = new List<AgentState>();
            ulong entityId = 1;
            for (var index = 0; index < factionZeroCount; index++)
            {
                agents.Add(CreateAgent(
                    entityId++,
                    0,
                    96_000,
                    40_000 + (index * 2_048),
                    scenario,
                    Pick(index)));
            }

            for (var index = 0; index < factionOneCount; index++)
            {
                agents.Add(CreateAgent(
                    entityId++,
                    1,
                    112_000,
                    40_000 + (index * 2_048),
                    scenario,
                    Pick(index)));
            }

            return [.. agents];
        }

        var run = RunToCompletion(scenario, Build(), ticks: 400);
        var repeat = RunToCompletion(scenario, Build(), ticks: 400);

        AssertRunContract(run, repeat, cellName, tickBudget: 400);
    }

    /// <summary>
    /// A shield pair against the two long-clearance two-handed weapons — a
    /// Wasay at 1,792 raw and a Kampilan at 1,536 raw, both wider lanes than
    /// either shield row's — stays deterministic and inside the shared
    /// legality bounds, and the shield pair's own committed separation still
    /// respects the larger of its two radii. The long enemy lanes do not push
    /// the pair into each other. Provisional reconstruction: gameplay tuning;
    /// no historical measurement
    /// (docs/research/movement/tall-hardwood-shield.md).
    /// </summary>
    [Fact]
    public void AShieldPairAgainstTwoLongClearanceWeaponsStaysDeterministicAndBounded()
    {
        var scenario = CreateScenario();

        AgentState[] Build() =>
        [
            CreateAgent(
                1,
                factionId: 0,
                MapCenterXRaw - MirrorOffsetXRaw,
                MirrorYRaw - (LaneSeparationRaw / 2),
                scenario,
                ShieldedKalis),
            CreateAgent(
                2,
                factionId: 0,
                MapCenterXRaw - MirrorOffsetXRaw,
                MirrorYRaw + (LaneSeparationRaw / 2),
                scenario,
                ShieldedItak),
            CreateAgent(
                3,
                factionId: 1,
                MapCenterXRaw + MirrorOffsetXRaw,
                MirrorYRaw - (LaneSeparationRaw / 2),
                scenario,
                SoloWasay),
            CreateAgent(
                4,
                factionId: 1,
                MapCenterXRaw + MirrorOffsetXRaw,
                MirrorYRaw + (LaneSeparationRaw / 2),
                scenario,
                SoloKampilan),
        ];

        var run = RunToCompletion(scenario, Build(), ticks: 400);
        var repeat = RunToCompletion(scenario, Build(), ticks: 400);

        AssertRunContract(run, repeat, "KS-IS vs WA-KP", tickBudget: 400);
    }

    /// <summary>
    /// An ally dying mid-lifecycle does not cancel a shield bearer's
    /// commitment or its recovery, and the scripted death replays identically.
    /// The ladder's commitment and recovery continuations sit at steps two and
    /// three, ahead of the support-ratio checks at steps four and five, so the
    /// loss of local support cannot interrupt a spell already running: the
    /// tick after the death the bearer holds the same phase with its timer
    /// exactly one lower. Two runs of the identical script — advance to the
    /// discovered tick, kill the same ally, advance one more — reach the same
    /// state hash. Provisional reconstruction: gameplay tuning; no historical
    /// measurement (docs/research/movement/tall-hardwood-shield.md).
    /// </summary>
    [Theory]
    [InlineData(ShieldedKalisMovementIndex, FootworkPhase.Commit)]
    [InlineData(ShieldedKalisMovementIndex, FootworkPhase.Recover)]
    [InlineData(ShieldedItakMovementIndex, FootworkPhase.Commit)]
    [InlineData(ShieldedItakMovementIndex, FootworkPhase.Recover)]
    public void AShieldBearerKeepsItsLifecycleWhenAnAllyDiesPartWayThrough(
        int shieldIndex,
        FootworkPhase phase)
    {
        var loadout = MovementScenarioMatrix.CanonicalLoadouts[shieldIndex];
        var scenario = CreateScenario();

        AgentState[] Build() =>
        [
            CreateAgent(1, 0, 51_200, 51_200, scenario, loadout),
            CreateAgent(2, 0, 51_200, 48_128, scenario, loadout),
            CreateAgent(3, 1, 57_344, 51_200, scenario, loadout),
            CreateAgent(4, 1, 57_344, 48_128, scenario, loadout),
        ];

        // Discovery pass: the first tick at which the observed bearer holds
        // the phase under test with at least two ticks left to run, so the
        // spell is genuinely mid-flight when the ally dies.
        var probeAgents = Build();
        var probe = BattleSimulation.CreateForTesting(scenario, probeAgents);
        var deathTick = -1;
        for (var tick = 0; tick < 200; tick++)
        {
            probe.AdvanceOneTick();
            if (probeAgents[0].FootworkPhase == phase &&
                probeAgents[0].FootworkTicksRemaining >= 2)
            {
                deathTick = tick;
                break;
            }
        }

        Assert.True(
            deathTick >= 0,
            $"The probe never reached {phase} with two ticks left, so the " +
            "mid-lifecycle ally death could not be scripted.");

        (ulong StateHash, FootworkPhase Phase, int TicksRemaining) Script()
        {
            var agents = Build();
            var simulation = BattleSimulation.CreateForTesting(
                scenario, agents);
            for (var tick = 0; tick <= deathTick; tick++)
            {
                simulation.AdvanceOneTick();
            }

            var timerBefore = agents[0].FootworkTicksRemaining;
            agents[1].HitPoints = 0;

            // The bearer's own cooldown is pinned for the tick after the
            // death so no newly accepted attack can restart a commitment out
            // of recovery and mask the continuation under observation.
            agents[0].AttackCooldownRemaining = CooldownPin;
            simulation.AdvanceOneTick();

            Assert.Equal(phase, agents[0].FootworkPhase);
            Assert.Equal(timerBefore - 1, agents[0].FootworkTicksRemaining);
            return (
                simulation.ComputeStateHash(),
                agents[0].FootworkPhase,
                agents[0].FootworkTicksRemaining);
        }

        var first = Script();
        var second = Script();

        Assert.Equal(first, second);
    }

    /// <summary>
    /// A shield bearer isolated in a locally outnumbered pocket disengages even
    /// while its faction holds a commanding headcount advantage: three hostiles
    /// against it alone inside the support radius is past both rows' entry
    /// ratios, and seven distant allies hold the faction total at eight against
    /// three so the posture branch never withdraws. Local geometry decides, not
    /// the roster total.
    /// </summary>
    /// <remarks>
    /// The bearer's attack cooldown is pinned every tick. Without the pin the
    /// ratio step is never reached at all: both shield rows spend three
    /// commitment ticks plus three recovery ticks per accepted attack, six in
    /// total, while the shielded Kalis reloads its attack in five ticks and the
    /// shielded Itak in four, so a bearer in continuous contact restarts its
    /// commitment before the lifecycle can expire. Steps two and three of
    /// <see cref="WeaponMovementRules.ResolveProvisionalFootwork"/> sit ahead of
    /// the ratio checks at steps four and five, which is the documented
    /// ordering, so the pin is what isolates the branch under observation
    /// rather than a workaround. Provisional reconstruction: gameplay tuning;
    /// no historical measurement
    /// (docs/research/movement/tall-hardwood-shield.md).
    /// </remarks>
    [Theory]
    [InlineData(ShieldedKalisMovementIndex)]
    [InlineData(ShieldedItakMovementIndex)]
    public void ALocallyOutnumberedShieldBearerDisengagesUnderAFavourableFactionTotal(
        int shieldIndex)
    {
        var loadout = MovementScenarioMatrix.CanonicalLoadouts[shieldIndex];
        var scenario = CreateScenario();
        var agents = new List<AgentState>();
        ulong entityId = 1;

        var isolated = CreateAgent(
            entityId++, 0, 40_000, 20_000, scenario, loadout);
        agents.Add(isolated);
        for (var index = 0; index < 3; index++)
        {
            agents.Add(CreateAgent(
                entityId++, 1, 43_000, 20_000 + (index * 1_200), scenario,
                loadout));
        }

        for (var index = 0; index < 7; index++)
        {
            agents.Add(CreateAgent(
                entityId++, 0, 180_000, 80_000 + (index * 1_500), scenario,
                loadout));
        }

        var simulation = BattleSimulation.CreateForTesting(
            scenario, [.. agents]);

        var sawDisengage = false;
        for (var tick = 0; tick < 200 && isolated.IsAlive; tick++)
        {
            isolated.AttackCooldownRemaining = CooldownPin;
            simulation.AdvanceOneTick();
            Assert.NotEqual(TacticalPosture.Withdraw, isolated.TacticalPosture);
            Assert.NotEqual(TacticalPosture.Yield, isolated.TacticalPosture);
            if (isolated.FootworkPhase == FootworkPhase.Disengage)
            {
                sawDisengage = true;
                break;
            }
        }

        Assert.True(
            sawDisengage,
            "A shield bearer facing three local hostiles with a faction total " +
            "of eight against three never disengaged.");
    }

    /// <summary>
    /// The mirror case: a globally disadvantageous roster disengages every
    /// member of the contingent even where the local pocket is well supported.
    /// Two warriors against eight puts the contingent past the exact
    /// double-outnumbering boundary, so the posture is
    /// <see cref="TacticalPosture.Withdraw"/>, and step six of the footwork
    /// ladder is unconditional: every member of a withdrawing contingent takes
    /// <see cref="FootworkPhase.Disengage"/> regardless of its own local
    /// advantage, with only its route differing. Local support cannot override
    /// the global posture, which is the direction the previous case does not
    /// prove. Provisional reconstruction: gameplay tuning; no historical
    /// measurement (docs/research/movement/tall-hardwood-shield.md).
    /// </summary>
    [Theory]
    [InlineData(ShieldedKalisMovementIndex)]
    [InlineData(ShieldedItakMovementIndex)]
    public void AGloballyWithdrawingShieldContingentDisengagesInALocallySupportedPocket(
        int shieldIndex)
    {
        var loadout = MovementScenarioMatrix.CanonicalLoadouts[shieldIndex];
        var scenario = CreateScenario();
        var agents = new List<AgentState>();
        ulong entityId = 1;

        // The observed bearer and its one ally, locally supported: two allies
        // against a single nearby enemy inside the support radius.
        var observed = CreateAgent(
            entityId++, 0, 40_000, 20_000, scenario, loadout);
        agents.Add(observed);
        agents.Add(CreateAgent(
            entityId++, 0, 40_000, 22_048, scenario, loadout));
        agents.Add(CreateAgent(
            entityId++, 1, 43_000, 21_000, scenario, loadout));

        // Seven more hostiles far away, so faction one holds eight against
        // two and the acting contingent withdraws globally.
        for (var index = 0; index < 7; index++)
        {
            agents.Add(CreateAgent(
                entityId++, 1, 180_000, 80_000 + (index * 1_500), scenario,
                loadout));
        }

        var simulation = BattleSimulation.CreateForTesting(
            scenario, [.. agents]);

        var sawWithdraw = false;
        var sawDisengage = false;
        for (var tick = 0; tick < 60; tick++)
        {
            observed.AttackCooldownRemaining = CooldownPin;
            simulation.AdvanceOneTick();
            sawWithdraw |=
                observed.TacticalPosture == TacticalPosture.Withdraw;
            sawDisengage |= observed.FootworkPhase == FootworkPhase.Disengage;
        }

        Assert.True(
            sawWithdraw,
            "Two against eight never resolved a Withdraw posture, so the " +
            "unconditional step could not be observed.");
        Assert.True(
            sawDisengage,
            "A locally supported bearer in a withdrawing contingent never " +
            "disengaged, so the unconditional posture step did not fire.");
    }

    // ----- Asymmetric counts -----

    /// <summary>
    /// One against two enters disengagement on both shield rows:
    /// <c>2 &#215; 4 &#8805; 1 &#215; 7</c> for the shielded Kalis at 17,500
    /// basis points and <c>2 &#215; 2 &#8805; 1 &#215; 3</c> for the shielded
    /// Itak at 15,000. A live but distant ally holds the global headcount at
    /// two against two, so the posture is never
    /// <see cref="TacticalPosture.Withdraw"/> or
    /// <see cref="TacticalPosture.Yield"/> — asserted every tick — and the
    /// observed entry can only be the support-ratio step. Provisional
    /// reconstruction: gameplay tuning; no historical measurement
    /// (docs/research/movement/tall-hardwood-shield.md).
    /// </summary>
    [Theory]
    [InlineData(ShieldedKalisMovementIndex)]
    [InlineData(ShieldedItakMovementIndex)]
    public void BothShieldRowsEnterDisengageOneAgainstTwo(int shieldIndex)
    {
        var loadout = MovementScenarioMatrix.CanonicalLoadouts[shieldIndex];
        var scenario = CreateScenario();
        var actor = CreateAgent(
            1, factionId: 0, 51_200, 51_200, scenario, loadout);
        var farAlly = CreateAgent(
            2, factionId: 0, 10_240, 10_240, scenario, loadout);
        var enemies = new[]
        {
            CreateAgent(3, factionId: 1, 55_296, 50_176, scenario, loadout),
            CreateAgent(4, factionId: 1, 55_296, 52_224, scenario, loadout),
        };
        var simulation = BattleSimulation.CreateForTesting(
            scenario, [actor, farAlly, .. enemies]);

        var entered = false;
        for (var tick = 0; tick < 40; tick++)
        {
            actor.AttackCooldownRemaining = CooldownPin;
            simulation.AdvanceOneTick();

            Assert.NotEqual(TacticalPosture.Withdraw, actor.TacticalPosture);
            Assert.NotEqual(TacticalPosture.Yield, actor.TacticalPosture);
            entered |= actor.FootworkPhase == FootworkPhase.Disengage;
        }

        Assert.True(entered, "The bearer never disengaged one against two.");
    }

    /// <summary>
    /// Two against three separates the two rows exactly. The shielded Itak
    /// enters at precise equality — <c>3 &#215; 10,000 = 2 &#215; 15,000</c>,
    /// reduced to <c>3 &#215; 2 &#8805; 2 &#215; 3</c> — while the shielded
    /// Kalis does not enter on counts alone, because
    /// <c>3 &#215; 4 = 12</c> falls short of <c>2 &#215; 7 = 14</c>. Two live
    /// but distant allies hold the global headcount at four against three, so
    /// the posture is never Withdraw or Yield and the observed decision can
    /// only be the support-ratio step. Geometry, divided bearings, and global
    /// posture may still cause refusal or withdrawal in a different
    /// arrangement of the same counts; this arrangement isolates the ratio.
    /// The Itak counterpart at the same equality is
    /// <c>ItakMovementScenarioTests.AShieldedItakPairEntersDisengageAtTheExactTwoAgainstThreeEquality</c>.
    /// Provisional reconstruction: gameplay tuning; no historical measurement
    /// (docs/research/movement/tall-hardwood-shield.md).
    /// </summary>
    [Fact]
    public void AtTwoAgainstThreeOnlyTheShieldedItakEntersDisengageOnCountsAlone()
    {
        Assert.False(
            EntersDisengageInPocket(ShieldedKalis, PocketSize.TwoAgainstThree),
            "The shielded Kalis entered disengagement two against three, " +
            "where 3 x 4 = 12 falls short of 2 x 7 = 14.");
        Assert.True(
            EntersDisengageInPocket(ShieldedItak, PocketSize.TwoAgainstThree),
            "The shielded Itak did not enter disengagement at the exact two " +
            "against three equality, where 3 x 2 = 6 meets 2 x 3 = 6.");
    }

    /// <summary>
    /// Three against five separates the two rows the same way. The shielded
    /// Itak enters — <c>5 &#215; 2 = 10</c> meets or passes
    /// <c>3 &#215; 3 = 9</c> — while the shielded Kalis still does not on
    /// counts alone, because <c>5 &#215; 4 = 20</c> falls short of
    /// <c>3 &#215; 7 = 21</c>. Two live but distant allies hold the global
    /// headcount at five against five, so the posture is never Withdraw or
    /// Yield. Provisional reconstruction: gameplay tuning; no historical
    /// measurement (docs/research/movement/tall-hardwood-shield.md).
    /// </summary>
    [Fact]
    public void AtThreeAgainstFiveOnlyTheShieldedItakEntersDisengageOnCountsAlone()
    {
        Assert.False(
            EntersDisengageInPocket(ShieldedKalis, PocketSize.ThreeAgainstFive),
            "The shielded Kalis entered disengagement three against five, " +
            "where 5 x 4 = 20 falls short of 3 x 7 = 21.");
        Assert.True(
            EntersDisengageInPocket(ShieldedItak, PocketSize.ThreeAgainstFive),
            "The shielded Itak did not enter disengagement three against " +
            "five, where 5 x 2 = 10 passes 3 x 3 = 9.");
    }

    // ----- Explicit combat V2 roster preservation -----

    /// <summary>
    /// The shipped default combat preset is the solo-only four-entry
    /// <see cref="CombatPresetId.PrecolonialPhilippinesV4"/> roster, so a
    /// six-entry roster carrying both shielded loadouts survives only by naming
    /// <see cref="CombatPresetId.PrecolonialPhilippinesV2"/> explicitly. The
    /// same counts validate under the explicit V2 preset and are a
    /// roster-length mismatch under the default. This is the whole reason every
    /// scenario in this file names its combat preset.
    /// </summary>
    [Fact]
    public void TheSixEntryShieldRosterValidatesOnlyUnderTheExplicitCombatVTwoPreset()
    {
        var preserved = Scenario.CreateDefault(seed: 1, totalAgents: 12) with
        {
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV2,
            MovementPreset = MovementPresetId.EquipmentRelativeFootworkV6,
            RosterCounts = ImmutableArray.Create(1, 1, 1, 1, 1, 1),
        };

        preserved.Validate();

        var defaulted = Scenario.CreateDefault(seed: 1, totalAgents: 12) with
        {
            MovementPreset = MovementPresetId.EquipmentRelativeFootworkV6,
            RosterCounts = ImmutableArray.Create(1, 1, 1, 1, 1, 1),
        };

        Assert.Equal(
            CombatPresetId.PrecolonialPhilippinesV4, defaulted.CombatPreset);
        Assert.Throws<ArgumentException>(defaulted.Validate);
    }

    /// <summary>
    /// The V2 roster is ordered weapon-first, solo before paired within a
    /// weapon: <c>KP, WA, KA, KS, IT, IS</c>. The shielded Kalis therefore sits
    /// at roster slot three while it is movement canonical index four, and the
    /// shielded Itak sits at slot five in both spaces. A scenario naming the
    /// explicit V2 preset with all of one faction's warriors on a single
    /// shielded roster slot spawns that exact loadout on every warrior, and
    /// each spawned loadout resolves to the pinned profile row instance under
    /// <see cref="MovementPresetId.EquipmentRelativeFootworkV6"/> — reference
    /// identity, so nothing multiplies a solo row into a shielded one at
    /// spawn.
    /// </summary>
    [Theory]
    [InlineData(ShieldedKalisRosterSlot)]
    [InlineData(ShieldedItakRosterSlot)]
    public void AnExplicitVTwoShieldRosterSpawnsTheNamedLoadoutOnEveryWarrior(
        int rosterSlot)
    {
        var counts = new int[VTwoRosterLength];
        counts[rosterSlot] = 4;
        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: 8) with
        {
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV2,
            MovementPreset = MovementPresetId.EquipmentRelativeFootworkV6,
            RosterCounts = [.. counts],
        };
        var expectedLoadout = CombatRules.Roster[rosterSlot];
        var expectedRow = V6.ResolveLoadoutProfile(expectedLoadout);

        var simulation = BattleSimulation.Create(scenario);

        Assert.Equal(ShieldId.TallHardwood, expectedLoadout.Shield);
        Assert.Equal(8, simulation.Agents.Count);
        Assert.All(simulation.Agents, agent =>
        {
            Assert.Equal(expectedLoadout.Weapon, agent.Loadout.Weapon);
            Assert.Equal(ArmorId.LightOrganic, agent.Loadout.Armor);
            Assert.Equal(ShieldId.TallHardwood, agent.Loadout.Shield);
            Assert.Same(expectedRow, V6.ResolveLoadoutProfile(agent.Loadout));
        });
    }

    /// <summary>
    /// A six-entry roster carrying both shielded loadouts, run end to end under
    /// the explicitly named V2 combat preset and the V6 movement preset,
    /// reproduces itself exactly on a repeat run: the same ordered event
    /// stream, the same event hash folded from it, the same state hash, and the
    /// same outcome. Every warrior's spawned loadout resolves to the profile row
    /// its roster slot names, and both shielded rows are present in the spawned
    /// battle rather than merely declared in the roster.
    /// </summary>
    [Fact]
    public void AnExplicitVTwoShieldRosterReproducesItsOrderedEventsAndHashes()
    {
        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: 12) with
        {
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV2,
            MovementPreset = MovementPresetId.EquipmentRelativeFootworkV6,
            RosterCounts = ImmutableArray.Create(1, 1, 1, 1, 1, 1),
        };

        (ulong StateHash, ulong EventHash, BattleOutcome Outcome,
            List<string> EventStream) Run()
        {
            var simulation = BattleSimulation.Create(scenario);
            var eventStream = new List<string>();
            var eventHash = Fnv1a.OffsetBasis;

            // Both shielded rows really are in the field, and each spawned
            // loadout resolves to the row its roster slot names.
            Assert.Contains(
                simulation.Agents,
                agent => agent.Loadout == CombatRules.Roster[
                    ShieldedKalisRosterSlot]);
            Assert.Contains(
                simulation.Agents,
                agent => agent.Loadout == CombatRules.Roster[
                    ShieldedItakRosterSlot]);
            Assert.All(simulation.Agents, agent =>
            {
                var row = V6.ResolveLoadoutProfile(agent.Loadout);
                Assert.Equal(agent.Loadout.Weapon, row.Loadout.Weapon);
                Assert.Equal(agent.Loadout.Armor, row.Loadout.Armor);
                Assert.Equal(agent.Loadout.Shield, row.Loadout.Shield);
            });

            for (var tick = 0;
                tick < 2_000 && simulation.Outcome == BattleOutcome.Ongoing;
                tick++)
            {
                simulation.AdvanceOneTick();
                foreach (var battleEvent in simulation.LastEvents)
                {
                    eventStream.Add(
                        $"{battleEvent.Sequence}:{battleEvent.Tick}:" +
                        $"{battleEvent.Kind}:{battleEvent.SourceEntityId}:" +
                        $"{battleEvent.TargetEntityId ?? 0}:" +
                        $"{battleEvent.Value}");
                    Fnv1a.Add(ref eventHash, (ulong)battleEvent.Sequence);
                    Fnv1a.Add(ref eventHash, (ulong)battleEvent.Tick);
                    Fnv1a.Add(ref eventHash, (ulong)battleEvent.Kind);
                    Fnv1a.Add(ref eventHash, battleEvent.SourceEntityId);
                    Fnv1a.Add(
                        ref eventHash, battleEvent.TargetEntityId ?? 0UL);
                    Fnv1a.Add(ref eventHash, (ulong)(long)battleEvent.Value);
                }
            }

            return (
                simulation.ComputeStateHash(),
                eventHash,
                simulation.Outcome,
                eventStream);
        }

        var first = Run();
        var second = Run();

        Assert.NotEmpty(first.EventStream);
        Assert.Equal(first.EventStream, second.EventStream);
        Assert.Equal(first.EventHash, second.EventHash);
        Assert.Equal(first.StateHash, second.StateHash);
        Assert.Equal(first.Outcome, second.Outcome);
    }

    /// <summary>
    /// The shipped combat default is solo-only, read off the registry rather
    /// than restated in prose: a default scenario names
    /// <see cref="CombatPresetId.PrecolonialPhilippinesV4"/>, and neither the
    /// V3 nor the V4 roster contains a single entry carrying a shield. Only
    /// <see cref="CombatPresetId.PrecolonialPhilippinesV2"/> fields both
    /// shielded loadouts, which is why every shielded cell must name it. The
    /// weapon plan's instruction to change a default assertion ahead of a V2
    /// to V3 switch is obsolete: the default is already V4.
    /// </summary>
    [Fact]
    public void TheShippedCombatDefaultAndItsPredecessorFieldNoShieldedLoadout()
    {
        Assert.Equal(
            CombatPresetId.PrecolonialPhilippinesV4,
            Scenario.CreateDefault().CombatPreset);

        foreach (var presetId in new[]
        {
            CombatPresetId.PrecolonialPhilippinesV3,
            CombatPresetId.PrecolonialPhilippinesV4,
        })
        {
            var roster = CombatPresetRegistry.Get(presetId).Roster;
            Assert.All(roster, entry => Assert.Equal(
                ShieldId.None, entry.Shield));
        }

        var vTwoRoster = CombatRules.Roster;
        Assert.Equal(VTwoRosterLength, vTwoRoster.Count);
        Assert.Equal(2, vTwoRoster.Count(
            entry => entry.Shield == ShieldId.TallHardwood));
    }

    // ----- Legacy presets leave the shield rows inert -----

    /// <summary>
    /// The shield counterpart of
    /// <c>MovementPipelineIntegrationTests.ALegacyPresetRunsNoEquipmentStageAtAll</c>:
    /// under every registered movement preset other than
    /// <see cref="MovementPresetId.EquipmentRelativeFootworkV6"/>, a battle
    /// whose warriors carry both shield rows never writes any of the five
    /// equipment-relative fields. The rows exist in the profile tables, but only
    /// V6 may read them, so adding them changed nothing for any earlier replay.
    /// </summary>
    [Theory]
    [InlineData(MovementPresetId.IndependentPursuitV1)]
    [InlineData(MovementPresetId.PersistentContingentsV2)]
    [InlineData(MovementPresetId.PersistentContingentsV3)]
    [InlineData(MovementPresetId.PersistentContingentsV4)]
    [InlineData(MovementPresetId.PersistentContingentsV5)]
    public void EveryLegacyPresetLeavesShieldFootworkStateUntouched(
        MovementPresetId legacyPreset)
    {
        var scenario = CreateScenario() with { MovementPreset = legacyPreset };
        var agents = new[]
        {
            CreateAgent(1, factionId: 0, 92_160, 51_200, scenario, ShieldedKalis),
            CreateAgent(2, factionId: 0, 92_160, 55_296, scenario, ShieldedItak),
            CreateAgent(
                3, factionId: 1, 112_640, 51_200, scenario, ShieldedKalis),
            CreateAgent(
                4, factionId: 1, 112_640, 55_296, scenario, ShieldedItak),
        };
        var simulation = BattleSimulation.CreateForTesting(scenario, agents);

        for (var tick = 0; tick < 40; tick++)
        {
            simulation.AdvanceOneTick();
        }

        Assert.False(
            MovementPresetRegistry
                .Get(legacyPreset)
                .UsesEquipmentRelativeFootwork);
        Assert.All(agents, agent =>
        {
            Assert.Equal(Facing16.None, agent.Facing);
            Assert.Equal(0, agent.MovementPaceRaw);
            Assert.Equal(TacticalPosture.None, agent.TacticalPosture);
            Assert.Equal(FootworkPhase.None, agent.FootworkPhase);
            Assert.Equal(0, agent.FootworkTicksRemaining);
        });
    }

    // ----- Rejected failure modes -----

    /// <summary>
    /// Two rejected failure modes shown absent in a shield-heavy
    /// eight-against-eight group over four hundred ticks. No rigid line: the
    /// sum of pairwise squared separations inside one faction changes across
    /// the run rather than freezing into a held formation. No wall: no bearer
    /// stays in <see cref="FootworkPhase.Refuse"/> for longer than the three
    /// consecutive ticks a shield row's own commitment lasts, so every lane
    /// contest is resolved by the ordered conflict pass rather than deadlocked
    /// by it. The group is also asserted never to resolve a
    /// <see cref="TacticalPosture.Withdraw"/> or
    /// <see cref="TacticalPosture.Yield"/> posture, so what is observed is
    /// bearers holding ground rather than a whole faction retreating.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The remaining two observable modes — no permanent reverse kiting and no
    /// threshold oscillation — are asserted separately, in
    /// <see cref="AShieldBearerLeavesDisengagementOnceThePressureLiftsAndNeverReenters"/>.
    /// They cannot be read off this group, because an even eight against eight
    /// never disengages at all: a bound on a disengagement that never happens
    /// would pass without ever being exercised. The separate probe scripts the
    /// pressure on and then off, so both bounds bite.
    /// </para>
    /// <para>
    /// Two of the plan's rejected modes are deliberately not asserted here.
    /// "No universal shield dominance" and "no viable shieldless entry" are
    /// outcome statistics: they can only be read off win rates, which section
    /// 0.8 of the session contract forbids as a pass criterion. They are
    /// calibration evidence for the orchestrator's benchmark runs.
    /// </para>
    /// <para>
    /// "No decision that depends on visual shield orientation" is also not a
    /// runtime assertion, because there is no such field to depend on:
    /// <see cref="AgentState"/> carries a single orientation,
    /// <see cref="Facing16"/>, derived from the selected target's bearing, and
    /// no shield bearing of any kind. Both shield rows turn at the solo value
    /// of two sectors per tick, so the shield does not even change the facing
    /// budget. The mirror-symmetry proof over that facing lives in
    /// <c>TallHardwoodMovementTests</c>.
    /// </para>
    /// <para>
    /// Provisional reconstruction: gameplay tuning; no historical measurement
    /// (docs/research/movement/tall-hardwood-shield.md).
    /// </para>
    /// </remarks>
    [Fact]
    public void NeitherARigidLineNorAWallFormsInAShieldHeavyGroup()
    {
        const int PerSide = 8;
        const int Ticks = 400;
        var scenario = CreateScenario();
        var agents = new List<AgentState>();
        ulong entityId = 1;
        for (var index = 0; index < PerSide; index++)
        {
            agents.Add(CreateAgent(
                entityId++,
                0,
                96_000,
                40_000 + (index * 2_048),
                scenario,
                index % 2 == 0 ? ShieldedKalis : ShieldedItak));
        }

        for (var index = 0; index < PerSide; index++)
        {
            agents.Add(CreateAgent(
                entityId++,
                1,
                112_000,
                40_000 + (index * 2_048),
                scenario,
                index % 2 == 0 ? ShieldedItak : ShieldedKalis));
        }

        var built = agents.ToArray();
        var simulation = BattleSimulation.CreateForTesting(scenario, built);
        var factionZero = built.Where(agent => agent.FactionId == 0).ToArray();
        var spreads = new List<Int128>();

        for (var tick = 0; tick < Ticks; tick++)
        {
            simulation.AdvanceOneTick();
            spreads.Add(PairwiseSeparationSpread(factionZero));
        }

        Assert.True(
            spreads.Distinct().Count() > 1,
            "The faction's pairwise separation spread never changed across " +
            "the run, which is a rigid held line rather than footwork.");

        var even = RunToCompletion(scenario, agents.ToArray(), Ticks);
        Assert.True(
            even.MaximumRefuseStreak <= MaximumTolerableRefuseStreak,
            $"A bearer was refused for {even.MaximumRefuseStreak} consecutive " +
            $"ticks, past the {MaximumTolerableRefuseStreak}-tick bound, " +
            "which reads as a wall.");

        Assert.False(
            even.SawWithdrawOrYieldPosture,
            "The even eight-against-eight group resolved a Withdraw or Yield " +
            "posture, so its bearers were retreating under the unconditional " +
            "step rather than holding.");
    }

    /// <summary>
    /// Reverse kiting is not permanent, and the hysteresis band does not
    /// oscillate. A shield bearer with one distant ally faces two enemies
    /// inside its support radius, past both rows' entry ratios, and
    /// disengages. When one of the two enemies falls the ratio becomes one
    /// enemy against one ally — <c>1 &#215; 10,000</c> is not above either
    /// row's 11,000 release threshold, and release equality or less leaves —
    /// so the bearer leaves disengagement on the very next tick and never
    /// re-enters for the rest of the run. One entry, one release, no flipping:
    /// the release threshold sitting strictly below the entry threshold is what
    /// forbids a count from entering and leaving on the same tick, and this is
    /// that guarantee observed through whole ticks rather than through the
    /// pure rule.
    /// </summary>
    /// <remarks>
    /// Provisional reconstruction: gameplay tuning; no historical measurement
    /// (docs/research/movement/tall-hardwood-shield.md).
    /// </remarks>
    [Theory]
    [InlineData(ShieldedKalisMovementIndex)]
    [InlineData(ShieldedItakMovementIndex)]
    public void AShieldBearerLeavesDisengagementOnceThePressureLiftsAndNeverReenters(
        int shieldIndex)
    {
        var loadout = MovementScenarioMatrix.CanonicalLoadouts[shieldIndex];
        var scenario = CreateScenario();
        var actor = CreateAgent(
            1, factionId: 0, 51_200, 51_200, scenario, loadout);
        var farAlly = CreateAgent(
            2, factionId: 0, 10_240, 10_240, scenario, loadout);
        var firstEnemy = CreateAgent(
            3, factionId: 1, 55_296, 50_176, scenario, loadout);
        var secondEnemy = CreateAgent(
            4, factionId: 1, 55_296, 52_224, scenario, loadout);
        var simulation = BattleSimulation.CreateForTesting(
            scenario, actor, farAlly, firstEnemy, secondEnemy);

        var entryTick = -1;
        for (var tick = 0; tick < 40 && entryTick < 0; tick++)
        {
            actor.AttackCooldownRemaining = CooldownPin;
            simulation.AdvanceOneTick();
            if (actor.FootworkPhase == FootworkPhase.Disengage)
            {
                entryTick = tick;
            }
        }

        Assert.True(entryTick >= 0, "The bearer never entered disengagement.");

        // The pressure lifts: one enemy of the two falls, leaving one enemy
        // against the actor's single support ally, which is at or below both
        // rows' release threshold.
        secondEnemy.HitPoints = 0;
        actor.AttackCooldownRemaining = CooldownPin;
        simulation.AdvanceOneTick();

        Assert.NotEqual(FootworkPhase.Disengage, actor.FootworkPhase);

        var reentered = false;
        for (var tick = 0; tick < 100; tick++)
        {
            actor.AttackCooldownRemaining = CooldownPin;
            simulation.AdvanceOneTick();
            reentered |= actor.FootworkPhase == FootworkPhase.Disengage;
        }

        Assert.False(
            reentered,
            "The bearer re-entered disengagement after the pressure lifted, " +
            "which is the threshold oscillation the hysteresis band exists " +
            "to forbid.");
    }

    // ----- Helpers -----

    /// <summary>
    /// The composition selector value that means "alternate the two shield
    /// rows" rather than naming a single canonical loadout index. Six is one
    /// past the last canonical index, so it can never collide with one.
    /// </summary>
    private const int MixedShieldComposition =
        MovementScenarioMatrix.CanonicalLoadoutCount;

    /// <summary>
    /// The parallel-lane separation used by the ally-cooperation cases:
    /// 2,048 raw, two body diameters, strictly beyond all six clearance radii
    /// so no pairing starts inside its own lane guarantee.
    /// </summary>
    private const int LaneSeparationRaw = 2_048;

    /// <summary>
    /// The longest consecutive stretch of <see cref="FootworkPhase.Refuse"/>
    /// this file treats as a tie-break rather than a wall. Measured, not
    /// guessed: the observed maximum is zero ticks across both shielded 2v2
    /// mirrors and exactly three ticks across the four-hundred-tick
    /// eight-against-eight shield-heavy group, so the bound is set at the
    /// observed maximum with no slack. A refusal that ran longer than the
    /// three ticks a shield row's own commitment lasts would be a wall rather
    /// than a lane contest resolved by the ordered conflict pass.
    /// </summary>
    private const int MaximumTolerableRefuseStreak = 3;

    private enum PocketSize
    {
        TwoAgainstThree,
        ThreeAgainstFive,
    }

    /// <summary>
    /// Whether the observed shield bearer enters disengagement inside a
    /// balanced-posture pocket of the given asymmetric size. Distant allies
    /// hold the global headcount level in both sizes so the posture is never
    /// Withdraw or Yield, which the probe asserts on every tick, leaving the
    /// support-ratio step as the only branch that can decide. Each pocket
    /// member's attack cooldown is pinned so the commitment lifecycle at steps
    /// two and three cannot mask the ratio checks at steps four and five.
    /// </summary>
    /// <remarks>
    /// Only ticks whose derived <see cref="LocalMovementContext"/> actually
    /// carries the intended support counts are read. The pocket members and
    /// their enemies move, so a later tick can hold a different ratio entirely
    /// — a pocket member that drifts past the six-body-diameter support radius
    /// from its own ally is one ally against three enemies, which enters on
    /// both rows and would make a non-entry claim about the intended counts
    /// meaningless. The context is derived from tick-start positions, the same
    /// positions the tick's own decision reads.
    /// </remarks>
    private static bool EntersDisengageInPocket(
        CombatLoadout loadout, PocketSize size)
    {
        var scenario = CreateScenario();
        var pocket = new List<AgentState>();
        var enemies = new List<AgentState>();
        ulong entityId = 1;

        if (size == PocketSize.TwoAgainstThree)
        {
            pocket.Add(CreateAgent(
                entityId++, 0, 51_200, 50_176, scenario, loadout));
            pocket.Add(CreateAgent(
                entityId++, 0, 51_200, 52_224, scenario, loadout));
        }
        else
        {
            pocket.Add(CreateAgent(
                entityId++, 0, 51_200, 49_664, scenario, loadout));
            pocket.Add(CreateAgent(
                entityId++, 0, 51_200, 51_200, scenario, loadout));
            pocket.Add(CreateAgent(
                entityId++, 0, 51_200, 52_736, scenario, loadout));
        }

        // Two distant allies, well outside the six-body-diameter support
        // radius, level the global headcount without joining the pocket.
        var farAllies = new[]
        {
            CreateAgent(entityId++, 0, 10_240, 10_240, scenario, loadout),
            CreateAgent(entityId++, 0, 12_288, 10_240, scenario, loadout),
        };

        if (size == PocketSize.TwoAgainstThree)
        {
            enemies.Add(CreateAgent(
                entityId++, 1, 55_296, 49_152, scenario, loadout));
            enemies.Add(CreateAgent(
                entityId++, 1, 55_296, 51_200, scenario, loadout));
            enemies.Add(CreateAgent(
                entityId++, 1, 55_296, 53_248, scenario, loadout));
        }
        else
        {
            for (var index = 0; index < 5; index++)
            {
                enemies.Add(CreateAgent(
                    entityId++,
                    1,
                    54_272,
                    48_128 + (index * 1_536),
                    scenario,
                    loadout));
            }
        }

        var all = new List<AgentState>(pocket);
        all.AddRange(farAllies);
        all.AddRange(enemies);
        var built = all.ToArray();
        var simulation = BattleSimulation.CreateForTesting(scenario, built);
        var expectedAllies = pocket.Count;
        var expectedEnemies = enemies.Count;

        var entered = false;
        var sawIntendedCounts = false;
        for (var tick = 0; tick < 40; tick++)
        {
            var countsAsIntended = new bool[pocket.Count];
            for (var index = 0; index < pocket.Count; index++)
            {
                var context = Derive(scenario, pocket[index], built);
                countsAsIntended[index] =
                    context.SupportAllies == expectedAllies &&
                    context.SupportEnemies == expectedEnemies;
                pocket[index].AttackCooldownRemaining = CooldownPin;
            }

            simulation.AdvanceOneTick();

            for (var index = 0; index < pocket.Count; index++)
            {
                if (!countsAsIntended[index])
                {
                    continue;
                }

                sawIntendedCounts = true;
                var member = pocket[index];
                Assert.NotEqual(
                    TacticalPosture.Withdraw, member.TacticalPosture);
                Assert.NotEqual(TacticalPosture.Yield, member.TacticalPosture);
                entered |= member.FootworkPhase == FootworkPhase.Disengage;
            }
        }

        Assert.True(
            sawIntendedCounts,
            "No tick in the probe ever held the intended support counts of " +
            $"{expectedAllies} allies against {expectedEnemies} enemies, so " +
            "nothing about the ratio was observed.");
        return entered;
    }

    /// <summary>
    /// Derives the observed agent's <see cref="LocalMovementContext"/> from the
    /// tick-start positions, at the two radii the V6 ruleset declares. Copied
    /// from <c>KampilanMovementTests.Derive</c>.
    /// </summary>
    private static LocalMovementContext Derive(
        Scenario scenario, AgentState actor, AgentState[] agents)
    {
        var immediateRaw = MovementContextQuery.ContextRadiusRaw(
            scenario.BodyRadiusRaw,
            V6.ImmediateRadiusBodyDiametersBasisPoints);
        var supportRaw = MovementContextQuery.ContextRadiusRaw(
            scenario.BodyRadiusRaw,
            V6.SupportRadiusBodyDiametersBasisPoints);

        return MovementContextQuery.Derive(
            agents,
            actor,
            selectedTargetEntityId: null,
            MovementContextQuery.SquaredContextRadius(immediateRaw),
            MovementContextQuery.SquaredContextRadius(supportRaw));
    }

    private sealed record EngageEntry(
        long Tick, long EntrySquared, long PriorSquared);

    /// <summary>
    /// Runs a mirrored duel and reports the actor's first
    /// <see cref="FootworkPhase.Engage"/> tick together with the tick-start
    /// separation the decision read and the separation one tick earlier. Both
    /// agents' cooldowns are pinned every tick so no accepted attack can open
    /// a commitment and mask the band entry.
    /// </summary>
    private static EngageEntry? FirstEngageEntry(
        Scenario scenario,
        CombatLoadout actorLoadout,
        CombatLoadout opponentLoadout,
        int tickBound)
    {
        var actor = CreateAgent(
            1,
            factionId: 0,
            MapCenterXRaw - MirrorOffsetXRaw,
            MirrorYRaw,
            scenario,
            actorLoadout);
        var opponent = CreateAgent(
            2,
            factionId: 1,
            MapCenterXRaw + MirrorOffsetXRaw,
            MirrorYRaw,
            scenario,
            opponentLoadout);
        var simulation = BattleSimulation.CreateForTesting(
            scenario, actor, opponent);
        var priorSquared = SquaredDistance(actor, opponent);

        for (var tick = 0; tick < tickBound; tick++)
        {
            var tickStartSquared = SquaredDistance(actor, opponent);
            actor.AttackCooldownRemaining = CooldownPin;
            opponent.AttackCooldownRemaining = CooldownPin;

            simulation.AdvanceOneTick();

            if (actor.FootworkPhase == FootworkPhase.Engage)
            {
                return new EngageEntry(
                    simulation.Tick, tickStartSquared, priorSquared);
            }

            priorSquared = tickStartSquared;
        }

        return null;
    }

    /// <summary>
    /// The reach the named combat preset gives this equipment triple, which is
    /// what the movement band scales, rather than the scenario's placeholder
    /// attack range.
    /// </summary>
    private static int WeaponReachRaw(CombatLoadout loadout) =>
        CombatRules
            .ResolveWeaponProfile(loadout.Weapon, loadout.Shield)
            .AttackRangeRaw;

    private static long EffectivePreferredRaw(
        CombatLoadout actorLoadout, CombatLoadout opponentLoadout) =>
        MovementRouteRules.EffectivePreferredDistanceRaw(
            WeaponReachRaw(actorLoadout),
            V6.ResolveLoadoutProfile(actorLoadout),
            MovementRouteRules.CanonicalOpponentIndex(opponentLoadout));

    private static long ClearanceRadiusRawOf(CombatLoadout loadout) =>
        MovementRouteRules.ClearanceRadiusRaw(
            BodyRadiusRaw,
            V6.ResolveLoadoutProfile(loadout)
                .AllyClearanceBodyDiametersBasisPoints);

    /// <summary>
    /// The sum of every pairwise squared separation inside one group, widened
    /// to <see cref="Int128"/> so no square can overflow. A value that never
    /// changes across a run is a formation held rigid rather than footwork.
    /// </summary>
    private static Int128 PairwiseSeparationSpread(AgentState[] group)
    {
        var total = (Int128)0;
        for (var first = 0; first < group.Length; first++)
        {
            for (var second = first + 1; second < group.Length; second++)
            {
                total += SquaredDistance(group[first], group[second]);
            }
        }

        return total;
    }

    private static string CellName(int firstIndex, int secondIndex) =>
        MovementScenarioMatrix.CanonicalLoadoutCodes[firstIndex] + "-" +
        MovementScenarioMatrix.CanonicalLoadoutCodes[secondIndex];

    private static long SquaredDistance(AgentState first, AgentState second)
    {
        var deltaX = (long)first.XRaw - second.XRaw;
        var deltaY = (long)first.YRaw - second.YRaw;
        return checked((deltaX * deltaX) + (deltaY * deltaY));
    }

    /// <summary>
    /// Every scenario names its combat preset and its movement preset
    /// explicitly. <c>PrecolonialPhilippinesV2</c> is the only combat preset
    /// that fields both shielded loadouts, so it spans the whole shield
    /// slice, and <c>EquipmentRelativeFootworkV6</c> is the only movement
    /// preset that reads the shield rows. Neither shipped default is touched.
    /// </summary>
    private static Scenario CreateScenario(ulong seed = 1) =>
        new(
            Seed: seed,
            MapWidth: 200,
            MapHeight: 100,
            AgentsPerFaction: 1,
            TickRate: 20,
            TickLimit: 5_000)
        {
            MaximumHitPoints = 1_000_000,
            DamagePerAttack = 1,
            AttackRangeRaw = AttackRangeRaw,
            PerceptionRangeRaw = 200 * FixedPoint.Scale,
            BodyRadiusRaw = BodyRadiusRaw,
            MovementSpeedRaw = MovementSpeedRaw,
            AttackCooldownTicks = 5,
            LastStandThresholdAgents = 0,
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV2,
            MovementPreset = MovementPresetId.EquipmentRelativeFootworkV6,
        };

    /// <summary>
    /// Builds one agent carrying its weapon's real reach, damage, and cooldown
    /// from the named combat preset, exactly as a full-roster run does, so a
    /// shielded matchup is not artificially reach-equal.
    /// </summary>
    private static AgentState CreateAgent(
        ulong entityId,
        int factionId,
        int xRaw,
        int yRaw,
        Scenario scenario,
        CombatLoadout loadout,
        int? attackCooldownTicksOverride = null)
    {
        var rules = CombatRules;
        var weapon = rules.HasWeaponProfiles
            ? rules.ResolveWeaponProfile(loadout.Weapon, loadout.Shield)
            : new WeaponProfile(
                scenario.DamagePerAttack,
                scenario.AttackRangeRaw,
                scenario.AttackCooldownTicks);

        return new AgentState(
            entityId,
            factionId,
            xRaw,
            yRaw,
            scenario.MaximumHitPoints,
            scenario.MovementSpeedRaw,
            scenario.PerceptionRangeRaw,
            weapon.AttackRangeRaw,
            weapon.DamagePerAttack,
            attackCooldownTicksOverride ?? weapon.AttackCooldownTicks,
            loadout);
    }

    /// <summary>
    /// The shared per-cell verdict: the twin runs agree bit for bit, every
    /// step was legal, every phase, posture, and timer was declared and
    /// non-negative, and the cell either terminated or made observable
    /// progress rather than stalling for its whole budget.
    /// </summary>
    private static void AssertRunContract(
        RunEvidence run, RunEvidence repeat, string cellName, int tickBudget)
    {
        Assert.True(
            run.StateHash == repeat.StateHash,
            $"Cell {cellName}: the twin rerun diverged on the state hash.");
        Assert.Equal(run.EventStream, repeat.EventStream);
        Assert.True(
            run.EventHash == repeat.EventHash,
            $"Cell {cellName}: the twin rerun diverged on the event hash.");
        Assert.Equal(run.Outcome, repeat.Outcome);
        Assert.True(run.LegalSteps, run.StepFailure ?? $"Cell {cellName}: step.");
        Assert.True(
            run.LegalPhases, run.PhaseFailure ?? $"Cell {cellName}: phase.");
        Assert.True(
            run.Outcome != BattleOutcome.Ongoing ||
            run.MaximumNoProgressStreak < tickBudget,
            $"Cell {cellName}: the battle neither reached an outcome nor " +
            $"progressed — {run.MaximumNoProgressStreak} consecutive ticks " +
            $"of the {tickBudget}-tick budget moved no living agent and " +
            "emitted no event.");
    }

    /// <summary>
    /// Advances a battle and records the non-authoritative evidence this file
    /// asserts on, without altering a single authoritative field. Copied from
    /// <c>KampilanMovementTests.RunToCompletion</c> — the two merged weapon
    /// sessions duplicated their helpers per file rather than adding a shared
    /// fixture, and a shared fixture would touch a file no weapon session
    /// owns — and extended with the event hash, the no-progress streak, the
    /// largest single-tick displacement, the per-agent refusal streak, and the
    /// unconditional-posture flag the shield cases need.
    /// </summary>
    private static RunEvidence RunToCompletion(
        Scenario scenario, AgentState[] agents, int ticks)
    {
        var simulation = BattleSimulation.CreateForTesting(scenario, agents);
        var toleratedStepSquared =
            (Int128)(scenario.MovementSpeedRaw + 1) *
            (scenario.MovementSpeedRaw + 1);
        var previous = agents.ToDictionary(
            agent => agent.EntityId, agent => (agent.XRaw, agent.YRaw));
        var eventStream = new List<string>();
        var eventHash = Fnv1a.OffsetBasis;
        var legalSteps = true;
        var legalPhases = true;
        string? stepFailure = null;
        string? phaseFailure = null;
        var maximumNoProgressStreak = 0;
        var noProgressStreak = 0;
        var maximumDisplacementSquared = (Int128)0;

        var refuseStreak = new Dictionary<ulong, int>();
        var maximumRefuseStreak = 0;
        var sawWithdrawOrYieldPosture = false;
        foreach (var agent in agents)
        {
            refuseStreak[agent.EntityId] = 0;
        }

        for (var tick = 0; tick < ticks; tick++)
        {
            simulation.AdvanceOneTick();
            var anyAgentMoved = false;

            foreach (var agent in agents)
            {
                if (agent.IsAlive)
                {
                    var (priorX, priorY) = previous[agent.EntityId];
                    var deltaX = (long)agent.XRaw - priorX;
                    var deltaY = (long)agent.YRaw - priorY;
                    var movedSquared =
                        ((Int128)deltaX * deltaX) + ((Int128)deltaY * deltaY);
                    if (movedSquared > maximumDisplacementSquared)
                    {
                        maximumDisplacementSquared = movedSquared;
                    }

                    anyAgentMoved |= deltaX != 0 || deltaY != 0;

                    // The shipped step model scales the target delta by
                    // paceRaw divided by a truncated integer square root of
                    // the distance, so the per-axis cap is exact while the
                    // Euclidean magnitude may exceed the cap by less than one
                    // raw unit. Both bounds are asserted, at their real
                    // strengths, exactly as the Kampilan session recorded.
                    if (Math.Abs(deltaX) > scenario.MovementSpeedRaw ||
                        Math.Abs(deltaY) > scenario.MovementSpeedRaw)
                    {
                        legalSteps = false;
                        stepFailure ??=
                            $"Agent {agent.EntityId} moved ({deltaX},{deltaY}) " +
                            $"on tick {tick}, exceeding the per-axis baseline " +
                            $"{scenario.MovementSpeedRaw}.";
                    }

                    if (movedSquared > toleratedStepSquared)
                    {
                        legalSteps = false;
                        stepFailure ??=
                            $"Agent {agent.EntityId} moved ({deltaX},{deltaY}) " +
                            $"on tick {tick}, squared {movedSquared}, beyond " +
                            $"the one-raw-unit truncation tolerance " +
                            $"{toleratedStepSquared}.";
                    }
                }

                previous[agent.EntityId] = (agent.XRaw, agent.YRaw);

                if (!Enum.IsDefined(agent.FootworkPhase) ||
                    !Enum.IsDefined(agent.TacticalPosture) ||
                    agent.FootworkTicksRemaining < 0)
                {
                    legalPhases = false;
                    phaseFailure ??=
                        $"Agent {agent.EntityId} on tick {tick} carried " +
                        $"phase {agent.FootworkPhase}, posture " +
                        $"{agent.TacticalPosture}, timer " +
                        $"{agent.FootworkTicksRemaining}.";
                }

                sawWithdrawOrYieldPosture |=
                    agent.TacticalPosture is TacticalPosture.Withdraw
                        or TacticalPosture.Yield;

                maximumRefuseStreak = TrackRefuseStreak(
                    refuseStreak, agent, maximumRefuseStreak);
            }

            var anyEvent = false;
            foreach (var battleEvent in simulation.LastEvents)
            {
                anyEvent = true;
                eventStream.Add(
                    $"{battleEvent.Sequence}:{battleEvent.Tick}:" +
                    $"{battleEvent.Kind}:{battleEvent.SourceEntityId}:" +
                    $"{battleEvent.TargetEntityId ?? 0}:{battleEvent.Value}");
                Fnv1a.Add(ref eventHash, (ulong)battleEvent.Sequence);
                Fnv1a.Add(ref eventHash, (ulong)battleEvent.Tick);
                Fnv1a.Add(ref eventHash, (ulong)battleEvent.Kind);
                Fnv1a.Add(ref eventHash, battleEvent.SourceEntityId);
                Fnv1a.Add(ref eventHash, battleEvent.TargetEntityId ?? 0UL);
                Fnv1a.Add(ref eventHash, (ulong)(long)battleEvent.Value);
            }

            if (anyAgentMoved || anyEvent)
            {
                noProgressStreak = 0;
            }
            else
            {
                noProgressStreak++;
                if (noProgressStreak > maximumNoProgressStreak)
                {
                    maximumNoProgressStreak = noProgressStreak;
                }
            }

            if (simulation.Outcome != BattleOutcome.Ongoing)
            {
                break;
            }
        }

        return new RunEvidence(
            simulation.ComputeStateHash(),
            eventStream,
            eventHash,
            simulation.Outcome,
            legalSteps,
            legalPhases,
            stepFailure,
            phaseFailure,
            maximumNoProgressStreak,
            maximumDisplacementSquared,
            maximumRefuseStreak,
            sawWithdrawOrYieldPosture);
    }

    /// <summary>
    /// The running per-agent <see cref="FootworkPhase.Refuse"/> streak, and the
    /// longest one seen so far across every agent.
    /// </summary>
    private static int TrackRefuseStreak(
        Dictionary<ulong, int> streaks,
        AgentState agent,
        int maximum)
    {
        if (agent.FootworkPhase == FootworkPhase.Refuse)
        {
            var streak = streaks[agent.EntityId] + 1;
            streaks[agent.EntityId] = streak;
            return streak > maximum ? streak : maximum;
        }

        streaks[agent.EntityId] = 0;
        return maximum;
    }

    private sealed record RunEvidence(
        ulong StateHash,
        List<string> EventStream,
        ulong EventHash,
        BattleOutcome Outcome,
        bool LegalSteps,
        bool LegalPhases,
        string? StepFailure,
        string? PhaseFailure,
        int MaximumNoProgressStreak,
        Int128 MaximumDisplacementSquared,
        int MaximumRefuseStreak,
        bool SawWithdrawOrYieldPosture);
}
