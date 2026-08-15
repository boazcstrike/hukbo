using System.Reflection;
using Hukbo.Core.Combat;
using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// Coverage for <see cref="MovementPresetId.ContingentCohesionBeforeContactV14"/>
/// and the rules the contingent cohesion before contact plan changes.
/// </summary>
public sealed class ContingentCohesionBeforeContactV14Tests
{
    // ----- Task 6 (R2): the narrowed scan excludes exactly two states -----

    /// <summary>
    /// The complete set of <see cref="ContingentState"/> values that
    /// <see cref="MovementRules.ParticipatesInCrossContingentScan"/> keeps out
    /// of movement gate 6, in ascending numeric order. This array is the
    /// expectation, written out by hand rather than derived from the predicate,
    /// so a change to the predicate has something independent to fail against.
    /// R2 asks that the blanket denial in <c>BattleSimulation</c> be restricted
    /// to exactly these two states; it already is, and this array is the pin
    /// that keeps it that way.
    /// </summary>
    private static readonly ContingentState[] ExpectedExcludedStates =
    [
        ContingentState.Close,
        ContingentState.Break,
    ];

    /// <summary>
    /// The excluded set is enumerated from <see cref="ContingentState"/> itself
    /// rather than from a hand-written list of the states to try, so a value
    /// appended to the enum later cannot slip past this pin: a new state the
    /// predicate excludes lands in <c>excluded</c> and fails the comparison.
    /// </summary>
    [Fact]
    public void CrossContingentScanExcludesExactlyCloseAndBreak()
    {
        var excluded = Enum.GetValues<ContingentState>()
            .Where(state => !MovementRules.ParticipatesInCrossContingentScan(state))
            .OrderBy(state => (int)state)
            .ToArray();

        Assert.Equal(ExpectedExcludedStates, excluded);
    }

    /// <summary>
    /// The other half of the same statement: every value the enum carries that
    /// is not one of the two excluded states takes part. This is what makes
    /// <see cref="ContingentState.None"/> a participant rather than an
    /// oversight, and it too is driven off the enum rather than a fixed list.
    /// </summary>
    [Fact]
    public void EveryOtherContingentStateTakesPartInTheCrossContingentScan()
    {
        foreach (var state in Enum.GetValues<ContingentState>())
        {
            if (ExpectedExcludedStates.Contains(state))
            {
                continue;
            }

            Assert.True(
                MovementRules.ParticipatesInCrossContingentScan(state),
                $"{state} must take part in the cross-contingent scan.");
        }
    }

    /// <summary>
    /// <see cref="ContingentState.None"/> gets its own assertion because it is
    /// the value every contingent carries on the first tick, before its state
    /// has ever been resolved, and because it is the value a preset that
    /// assigns no contingent states leaves in place. Excluding it would silence
    /// gate 6 on the tick the deployment is at its most crowded.
    /// </summary>
    [Fact]
    public void ContingentStateNoneTakesPartInTheCrossContingentScan()
    {
        Assert.True(
            MovementRules.ParticipatesInCrossContingentScan(ContingentState.None));
    }

    /// <summary>
    /// The two excluded states, asserted directly rather than through the
    /// enumeration above, so a failure names which one moved.
    /// </summary>
    [Theory]
    [InlineData(ContingentState.Close)]
    [InlineData(ContingentState.Break)]
    public void CloseAndBreakTakeNoPartInTheCrossContingentScan(
        ContingentState state)
    {
        Assert.False(MovementRules.ParticipatesInCrossContingentScan(state));
    }

    // ----- Task 10: registry facts, mirroring CohortLateralSpreadV13Tests.cs:40-80 -----

    /// <summary>
    /// The shipped default, and the control every property test below reads
    /// V14 against. It is the immediate predecessor rather than an arbitrary
    /// one: V14 is admitted to the same three preset-identity gates V13
    /// passes, so an A/B between the two isolates the contingent-cohesion
    /// change rather than also re-testing the lateral riffle.
    /// </summary>
    private static MovementRuleset V13 =>
        MovementPresetRegistry.Get(MovementPresetId.CohortLateralSpreadV13);

    private static MovementRuleset V14 =>
        MovementPresetRegistry.Get(
            MovementPresetId.ContingentCohesionBeforeContactV14);

    [Fact]
    public void ContingentCohesionBeforeContactV14HasTheExpectedNumericValue()
    {
        Assert.Equal(
            14, (int)MovementPresetId.ContingentCohesionBeforeContactV14);
    }

    [Fact]
    public void ContingentCohesionBeforeContactV14IsRegistered()
    {
        Assert.True(
            MovementPresetRegistry.IsRegistered(
                MovementPresetId.ContingentCohesionBeforeContactV14));
    }

    /// <summary>
    /// Every field V11 registers is carried forward verbatim, so the only
    /// tunables that separate V14 from the whole V11 line are the gate and
    /// the three values the calibration sweep settled. The content hashes
    /// still differ, because the folded <c>Id</c> alone would separate them
    /// and the three gated numerics separate them again.
    /// </summary>
    [Fact]
    public void ContingentCohesionBeforeContactV14CarriesItsOwnIdentity()
    {
        var v11 = MovementPresetRegistry.Get(
            MovementPresetId.LastStandEngagementV11);
        var v14 = V14;

        Assert.Equal(MovementPresetId.ContingentCohesionBeforeContactV14, v14.Id);

        Assert.Equal(v11.Version, v14.Version);
        Assert.Equal(v11.CohesionRadiusMultiplier, v14.CohesionRadiusMultiplier);
        Assert.Equal(v11.CloseRadiusMultiplier, v14.CloseRadiusMultiplier);
        Assert.Equal(v11.CloseFractionNumerator, v14.CloseFractionNumerator);
        Assert.Equal(v11.CloseFractionDenominator, v14.CloseFractionDenominator);
        Assert.Equal(v11.MinimumCohesiveMembers, v14.MinimumCohesiveMembers);
        Assert.Equal(v11.CohesionCycleTicks, v14.CohesionCycleTicks);
        Assert.Equal(v11.CohesionDutyTicks, v14.CohesionDutyTicks);
        Assert.Equal(v11.ArrivalTaperMultiplier, v14.ArrivalTaperMultiplier);
        Assert.Equal(v11.OffsetUnit, v14.OffsetUnit);
        Assert.Equal(
            v11.NarrowsCohesionScanToCohesionCapableContingents,
            v14.NarrowsCohesionScanToCohesionCapableContingents);
        Assert.Equal(v11.SelectsLeaderByRank, v14.SelectsLeaderByRank);
        Assert.Equal(
            v11.UsesEquipmentRelativeFootwork,
            v14.UsesEquipmentRelativeFootwork);
        Assert.Equal(
            v11.ImmediateRadiusBodyDiametersBasisPoints,
            v14.ImmediateRadiusBodyDiametersBasisPoints);
        Assert.Equal(
            v11.SupportRadiusBodyDiametersBasisPoints,
            v14.SupportRadiusBodyDiametersBasisPoints);
        Assert.Equal(v11.LoadoutMovementProfiles, v14.LoadoutMovementProfiles);
        Assert.Equal(v11.AppliesPressureInterrupt, v14.AppliesPressureInterrupt);
        Assert.Equal(
            v11.SupportPressureWeightBasisPoints,
            v14.SupportPressureWeightBasisPoints);
        Assert.Equal(
            v11.IncomingDamageWeightBasisPoints,
            v14.IncomingDamageWeightBasisPoints);
        Assert.Equal(
            v11.AllyCollapseWeightBasisPoints,
            v14.AllyCollapseWeightBasisPoints);

        Assert.NotEqual(v11.ContentHash, v14.ContentHash);
    }

    /// <summary>
    /// The gate and the three values the twenty-seed calibration sweep
    /// settled, pinned as literals rather than read back off the registry, so
    /// this test is an expectation about what V14 is and not a restatement of
    /// whatever it happens to hold. Changing any one of them is a behavioural
    /// change to a registered preset and needs a new preset version, not an
    /// edit here.
    /// </summary>
    [Fact]
    public void ContingentCohesionBeforeContactV14RegistersTheSettledTunables()
    {
        var v14 = V14;

        Assert.True(v14.GathersContingentsBeforeContact);
        Assert.Equal(1, v14.CohesionBandNumerator);
        Assert.Equal(3, v14.CohesionBandDenominator);
        Assert.Equal(6000, v14.CohesionSquareMarginBasisPoints);
    }

    /// <summary>
    /// The control half of the fact above. V13 leaves the gate closed and all
    /// three tunables at zero, which is what makes the A/B in every property
    /// test below a comparison of the new rule against the old one rather
    /// than of two settings of the new rule.
    /// </summary>
    [Fact]
    public void CohortLateralSpreadV13LeavesTheGateClosedAndTheTunablesZero()
    {
        var v13 = V13;

        Assert.False(v13.GathersContingentsBeforeContact);
        Assert.Equal(0, v13.CohesionBandNumerator);
        Assert.Equal(0, v13.CohesionBandDenominator);
        Assert.Equal(0, v13.CohesionSquareMarginBasisPoints);
    }

    // ----- Task 10, property 1 (R1): the band replaces the three-quarters test -----

    /// <summary>
    /// R1's whole behavioural claim, read off the production gate rather than
    /// off a restatement of it: a member standing between the registered band
    /// — one third of the cohesion radius — and the three-quarters distance
    /// every preset up to V13 uses is granted a cohesion destination in
    /// <see cref="ContingentState.Advance"/> under V14 and denied one under
    /// V13. The two flanking pairs are the band's own edges: inside one third
    /// neither preset calls the member a straggler, and beyond three quarters
    /// both already did, so the change is a widening of one interval and not
    /// a blanket grant.
    /// </summary>
    /// <remarks>
    /// The distance is expressed as a fraction of the cohesion radius rather
    /// than in raw units so the case list stays readable against the two
    /// fractions the claim is about; it is converted with the same integer
    /// truncation the simulation's own arithmetic uses, and every value is
    /// well clear of both boundaries, so truncation cannot decide a case.
    /// <para>
    /// <see cref="ProbeAdvanceMemberCohesionGrant"/> holds gates 5 and 6 open
    /// deliberately. Those two answer about the contingent's claimed square,
    /// not about this member's distance, and leaving them to whatever the
    /// deployment happened to produce would let a map-edge or overlap denial
    /// masquerade as the band's answer.
    /// </para>
    /// </remarks>
    [Theory]
    // Inside the registered band: a straggler to neither preset.
    [InlineData(1, 4, false, false)]
    [InlineData(3, 10, false, false)]
    // Between the registered band and three quarters: the R1 claim itself.
    [InlineData(2, 5, false, true)]
    [InlineData(1, 2, false, true)]
    [InlineData(3, 5, false, true)]
    [InlineData(7, 10, false, true)]
    // Beyond three quarters: a straggler to both presets already.
    [InlineData(4, 5, true, true)]
    [InlineData(9, 10, true, true)]
    public void AnAdvanceMemberIsGrantedCohesionByTheBandRatherThanByThreeQuarters(
        int distanceNumerator,
        int distanceDenominator,
        bool expectedUnderV13,
        bool expectedUnderV14)
    {
        var underV13 = ProbeAdvanceMemberCohesionGrant(
            MovementPresetId.CohortLateralSpreadV13,
            distanceNumerator,
            distanceDenominator);
        var underV14 = ProbeAdvanceMemberCohesionGrant(
            MovementPresetId.ContingentCohesionBeforeContactV14,
            distanceNumerator,
            distanceDenominator);

        Assert.Equal(expectedUnderV13, underV13);
        Assert.Equal(expectedUnderV14, underV14);
    }

    /// <summary>
    /// Places one synthetic <see cref="ContingentState.Advance"/> member at
    /// <paramref name="distanceNumerator"/> over
    /// <paramref name="distanceDenominator"/> of the cohesion radius from its
    /// own contingent's selected leader, and reports whether
    /// <c>BattleSimulation.TryResolveContingentCohesionAimPoint</c> — the
    /// production method that evaluates all six movement gates — grants it a
    /// cohesion destination.
    /// </summary>
    /// <remarks>
    /// The method is private, so it is reached by reflection, in the same
    /// shape and with the same loud failure message
    /// <c>ContingentCohesionCalibrationHarness</c> uses to reach the private
    /// per-slot arrays. Calling it is the point: a test that restated the
    /// straggler comparison instead would pass against any implementation of
    /// it, including a deleted one.
    /// </remarks>
    private static bool ProbeAdvanceMemberCohesionGrant(
        MovementPresetId preset,
        int distanceNumerator,
        int distanceDenominator)
    {
        var scenario = ControlScenario(preset);
        var simulation = BattleSimulation.Create(scenario);

        // One tick, so the per-slot leader, living-count, and square arrays
        // hold real values rather than their construction-time zeros.
        simulation.AdvanceOneTick();

        var rules = MovementPresetRegistry.Get(preset);
        var agentStates = ReadPrivateArray<AgentState[]>(
            simulation, AgentStatesFieldName);
        var leaderEntityIds = ReadPrivateArray<ulong[]>(
            simulation, LeaderEntityIdsFieldName);
        var livingCounts = ReadPrivateArray<int[]>(
            simulation, LivingCountsFieldName);
        var squareFitsMap = ReadPrivateArray<bool[]>(
            simulation, SquareFitsMapFieldName);
        var squareOverlapsAnother = ReadPrivateArray<bool[]>(
            simulation, SquareOverlapsFieldName);

        var slot = FirstLivingSlotWithALeader(livingCounts, leaderEntityIds);

        // Gates 5 and 6 held open on purpose: this probe is about gate 4.
        squareFitsMap[slot] = true;
        squareOverlapsAnother[slot] = false;

        var leader = AgentWithEntityId(agentStates, leaderEntityIds[slot]);

        var cohesionRadiusRaw = checked(
            (long)rules.CohesionRadiusMultiplier * scenario.BodyRadiusRaw);
        var distanceRaw = checked(
            (int)(cohesionRadiusRaw * distanceNumerator / distanceDenominator));

        var probe = new AgentState(
            entityId: ProbeEntityId,
            factionId: leader.FactionId,
            xRaw: checked(leader.XRaw + distanceRaw),
            yRaw: leader.YRaw,
            maximumHitPoints: leader.MaximumHitPoints,
            movementSpeedRaw: leader.MovementSpeedRaw,
            perceptionRangeRaw: leader.PerceptionRangeRaw,
            attackRangeRaw: leader.AttackRangeRaw,
            damagePerAttack: leader.DamagePerAttack,
            attackCooldownTicks: leader.AttackCooldownTicks,
            loadout: leader.Loadout,
            level: leader.Level,
            contingentId: leader.ContingentId)
        {
            ContingentState = ContingentState.Advance,
        };

        var tick = FirstTickWithAnOpenCohesionWindow(rules, slot);

        var method = typeof(BattleSimulation).GetMethod(
            CohesionAimPointMethodName,
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InvalidOperationException(
                $"BattleSimulation no longer declares a private method named " +
                $"'{CohesionAimPointMethodName}'. This test invokes it to read " +
                $"the six movement gates as production evaluates them; rename " +
                $"the constant beside this message to match rather than " +
                $"restating the gate here, which would pass against any " +
                $"implementation.");

        var arguments = new object?[] { probe, tick, 0, 0, 0UL };

        return (bool)method.Invoke(simulation, arguments)!;
    }

    // ----- Task 10, property 2 (R3): the claimed square shrinks -----

    /// <summary>
    /// R3's claim at the shape a real battle actually reaches: for every
    /// living count both a V13 run and a V14 run pass through, the half-side
    /// of the square V14's contingent claims is strictly smaller than V13's.
    /// Read out of <c>BattleSimulation</c>'s own per-slot margin array during
    /// two full battles, so it is the number gates 5 and 6 consumed rather
    /// than a number this file computed.
    /// </summary>
    [Fact]
    public void V14ClaimsAStrictlySmallerSquareThanV13AtEveryLivingCountBothRunsReach()
    {
        var v13 = V13ClaimedGeometry.Value;
        var v14 = V14ClaimedGeometry.Value;
        var shared = SharedLivingCounts(v13, v14);

        Assert.True(
            shared.Length >= MinimumSharedLivingCounts,
            $"Expected the two control runs to share at least " +
            $"{MinimumSharedLivingCounts} living counts before this property " +
            $"means anything; they shared {shared.Length}.");

        foreach (var livingCount in shared)
        {
            Assert.True(
                v14[livingCount].MarginRaw < v13[livingCount].MarginRaw,
                $"At {livingCount} living members V14 claimed a half-side of " +
                $"{v14[livingCount].MarginRaw} raw units and V13 claimed " +
                $"{v13[livingCount].MarginRaw}; V14's must be strictly " +
                $"smaller.");
        }
    }

    /// <summary>
    /// The same claim over the whole range task 7 names — every living count
    /// from 1 to 200 — rather than only the counts a battle happens to visit.
    /// It runs through <see cref="ClaimedSquareHalfSideRaw"/>, which
    /// <see cref="TheClaimedSquareHelperReproducesWhatBothSimulationsClaim"/>
    /// ties to the simulation's own array, so the sweep is not a restatement
    /// standing on its own.
    /// </summary>
    [Fact]
    public void V14ClaimsAStrictlySmallerSquareForEveryLivingCountFromOneToTwoHundred()
    {
        var bodyRadiusRaw = ControlBodyRadiusRaw;
        var v13 = V13;
        var v14 = V14;

        for (var livingCount = 1;
            livingCount <= LargestSweptLivingCount;
            livingCount++)
        {
            var v13HalfSideRaw = ClaimedSquareHalfSideRaw(
                v13, bodyRadiusRaw, livingCount);
            var v14HalfSideRaw = ClaimedSquareHalfSideRaw(
                v14, bodyRadiusRaw, livingCount);

            Assert.True(
                v14HalfSideRaw < v13HalfSideRaw,
                $"At {livingCount} living members V14's claimed half-side is " +
                $"{v14HalfSideRaw} raw units and V13's is {v13HalfSideRaw}; " +
                $"V14's must be strictly smaller at every count from 1 to " +
                $"{LargestSweptLivingCount}.");
        }
    }

    /// <summary>
    /// The anchor that makes the two sweeps above and the 10,000-basis-point
    /// facts below mean something: <see cref="ClaimedSquareHalfSideRaw"/> and
    /// <see cref="FormationRules.ComputeContingentJitterRaw"/> reproduce, for
    /// every living count either control run reached, exactly the jitter and
    /// margin <c>BattleSimulation</c> wrote into its own per-slot arrays —
    /// under the closed gate as well as the open one, so both branches of the
    /// production expression are covered.
    /// </summary>
    [Fact]
    public void TheClaimedSquareHelperReproducesWhatBothSimulationsClaim()
    {
        var bodyRadiusRaw = ControlBodyRadiusRaw;

        foreach (var (rules, observed) in new[]
        {
            (V13, V13ClaimedGeometry.Value),
            (V14, V14ClaimedGeometry.Value),
        })
        {
            Assert.NotEmpty(observed);

            foreach (var livingCount in observed.Keys.OrderBy(count => count))
            {
                Assert.Equal(
                    FormationRules.ComputeContingentJitterRaw(
                        bodyRadiusRaw, livingCount),
                    observed[livingCount].JitterRaw);
                Assert.Equal(
                    ClaimedSquareHalfSideRaw(rules, bodyRadiusRaw, livingCount),
                    observed[livingCount].MarginRaw);
            }
        }
    }

    // ----- Task 7's other clause: 10,000 basis points is bit-identical -----

    /// <summary>
    /// The preset-level half of task 7's "a registered value of 10,000 must
    /// be bit-identical to today": a ruleset that gathers its contingents
    /// before contact and registers
    /// <see cref="MovementRuleset.UnscaledCohesionSquareMarginBasisPoints"/>
    /// claims exactly the half-side V13 claims, for every living count from
    /// 1 to 200 — equal, not merely close, because the production arithmetic
    /// multiplies in <see langword="long"/> before it divides.
    /// </summary>
    [Fact]
    public void TenThousandBasisPointsClaimsExactlyTheUnscaledSquareAtPresetLevel()
    {
        Assert.Equal(
            10_000, MovementRuleset.UnscaledCohesionSquareMarginBasisPoints);

        var bodyRadiusRaw = ControlBodyRadiusRaw;
        var v13 = V13;
        var unscaledV14 = WithCohesionSquareMarginBasisPoints(
            V14, MovementRuleset.UnscaledCohesionSquareMarginBasisPoints);

        Assert.True(unscaledV14.GathersContingentsBeforeContact);

        for (var livingCount = 1;
            livingCount <= LargestSweptLivingCount;
            livingCount++)
        {
            Assert.Equal(
                ClaimedSquareHalfSideRaw(v13, bodyRadiusRaw, livingCount),
                ClaimedSquareHalfSideRaw(unscaledV14, bodyRadiusRaw, livingCount));
        }
    }

    /// <summary>
    /// The helper-level half of the same clause:
    /// <see cref="FormationRules.IsCohesionSquareWithinBoundsForMargin"/>,
    /// handed the unscaled margin, answers exactly what the jitter-taking
    /// <see cref="FormationRules.IsCohesionSquareWithinBounds"/> answers, for
    /// a grid of trail bases that straddles both map edges on both axes. The
    /// closing assertions require the grid to have produced both answers, so
    /// a helper that returned a constant could not pass this by agreeing
    /// everywhere.
    /// </summary>
    [Fact]
    public void TheMarginTakingBoundsHelperAgreesWithTheJitterTakingOne()
    {
        const int BodyRadiusRaw = 4096;
        const int MapWidthRaw = 200 * 1024;
        const int MapHeightRaw = 150 * 1024;

        var jitters = new[] { 1, BodyRadiusRaw, 5 * BodyRadiusRaw, 40 * BodyRadiusRaw };
        var coordinates = new[]
        {
            0,
            BodyRadiusRaw,
            10 * BodyRadiusRaw,
            MapWidthRaw / 2,
            MapHeightRaw - BodyRadiusRaw,
            MapWidthRaw - BodyRadiusRaw,
            MapWidthRaw,
        };

        var insideCount = 0;
        var outsideCount = 0;

        foreach (var jitterRaw in jitters)
        {
            foreach (var trailBaseXRaw in coordinates)
            {
                foreach (var trailBaseYRaw in coordinates)
                {
                    var byJitter = FormationRules.IsCohesionSquareWithinBounds(
                        trailBaseXRaw,
                        trailBaseYRaw,
                        jitterRaw,
                        BodyRadiusRaw,
                        MapWidthRaw,
                        MapHeightRaw);
                    var byMargin =
                        FormationRules.IsCohesionSquareWithinBoundsForMargin(
                            trailBaseXRaw,
                            trailBaseYRaw,
                            checked(jitterRaw + BodyRadiusRaw),
                            BodyRadiusRaw,
                            MapWidthRaw,
                            MapHeightRaw);

                    Assert.Equal(byJitter, byMargin);

                    if (byJitter)
                    {
                        insideCount++;
                    }
                    else
                    {
                        outsideCount++;
                    }
                }
            }
        }

        Assert.True(insideCount > 0, "The grid produced no fitting square.");
        Assert.True(outsideCount > 0, "The grid produced no overflowing square.");
    }

    // ----- Task 10, property 3: member spacing did not change -----

    /// <summary>
    /// The regression guard for the design's section 3 prohibition on
    /// regularizing member spacing, and the most important test in this file.
    /// <c>_contingentJitterRaw</c> is the only quantity that decides how far
    /// apart a contingent's members stand — it is what
    /// <c>ContingentOffset.Compute</c> is handed — and R3 scales the
    /// <em>claimed</em> margin only. So for every living count both control
    /// runs reach, V14's jitter must equal V13's exactly. If this ever fails,
    /// V14 has started moving warriors relative to one another, which is the
    /// one thing the design rules out on evidentiary rather than engineering
    /// grounds, and the correct response is to revert the change rather than
    /// to re-record anything.
    /// </summary>
    [Fact]
    public void V14JitterEqualsV13AtEveryLivingCountBothRunsReach()
    {
        var v13 = V13ClaimedGeometry.Value;
        var v14 = V14ClaimedGeometry.Value;
        var shared = SharedLivingCounts(v13, v14);

        Assert.True(
            shared.Length >= MinimumSharedLivingCounts,
            $"Expected the two control runs to share at least " +
            $"{MinimumSharedLivingCounts} living counts before this property " +
            $"means anything; they shared {shared.Length}.");

        foreach (var livingCount in shared)
        {
            Assert.Equal(v13[livingCount].JitterRaw, v14[livingCount].JitterRaw);
        }
    }

    // ----- Shared fixtures and reflection helpers -----

    /// <summary>
    /// The jitter radius and the claimed square's half-side a single
    /// contingent slot carried on one tick.
    /// </summary>
    private readonly record struct ClaimedGeometry(int JitterRaw, int MarginRaw);

    /// <summary>
    /// The private <c>BattleSimulation</c> members this file reads. Each is
    /// named once here so a rename fails in one place with a message that
    /// says what the test was reading and why.
    /// </summary>
    private const string AgentStatesFieldName = "_agentStates";

    private const string LeaderEntityIdsFieldName = "_contingentLeaderEntityIds";

    private const string LivingCountsFieldName = "_contingentLivingCounts";

    private const string JitterFieldName = "_contingentJitterRaw";

    private const string MarginFieldName = "_contingentMarginRaw";

    private const string SquareFitsMapFieldName = "_contingentSquareFitsMap";

    private const string SquareOverlapsFieldName =
        "_contingentSquareOverlapsAnother";

    private const string CohesionAimPointMethodName =
        "TryResolveContingentCohesionAimPoint";

    /// <summary>
    /// An entity id no spawned warrior can hold, so the synthetic probe agent
    /// is never mistaken for its own contingent's leader.
    /// </summary>
    private const ulong ProbeEntityId = ulong.MaxValue;

    /// <summary>The largest living count task 7's clause names.</summary>
    private const int LargestSweptLivingCount = 200;

    /// <summary>
    /// The bar the two observed-geometry properties hold themselves to. Both
    /// compare only living counts both runs reached, so without a floor a run
    /// that shared nothing would pass them vacuously.
    /// </summary>
    private const int MinimumSharedLivingCounts = 8;

    /// <summary>
    /// The tick ceiling on an observation run. Both control battles decide
    /// well inside it; it exists so a preset that stalled would fail with a
    /// bounded run rather than hang the suite.
    /// </summary>
    private const long ObservationTickCeiling = 3000;

    private static readonly Lazy<Dictionary<int, ClaimedGeometry>>
        V13ClaimedGeometry = new(() => ObserveClaimedGeometry(
            MovementPresetId.CohortLateralSpreadV13));

    private static readonly Lazy<Dictionary<int, ClaimedGeometry>>
        V14ClaimedGeometry = new(() => ObserveClaimedGeometry(
            MovementPresetId.ContingentCohesionBeforeContactV14));

    private static int ControlBodyRadiusRaw =>
        ControlScenario(MovementPresetId.CohortLateralSpreadV13).BodyRadiusRaw;

    /// <summary>
    /// The one control shape every run in this file uses: 200 warriors, seed
    /// 1, <c>PrecolonialPhilippinesV2</c>, with only the movement preset
    /// varying, so a V13 run and a V14 run differ in nothing else.
    /// </summary>
    private static Scenario ControlScenario(MovementPresetId preset)
    {
        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: 200) with
        {
            MovementPreset = preset,
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV2,
        };
        scenario.Validate();

        return scenario;
    }

    /// <summary>
    /// Runs one control battle to its own termination and records, for every
    /// living count any contingent slot passed through, the jitter and
    /// claimed half-side that slot carried. Both quantities are pure
    /// functions of the living count under a fixed body radius, so a second
    /// sighting of the same count must report the same pair; the run throws
    /// rather than overwriting if it ever does not, because that would mean
    /// one of them had started depending on something else.
    /// </summary>
    private static Dictionary<int, ClaimedGeometry> ObserveClaimedGeometry(
        MovementPresetId preset)
    {
        var simulation = BattleSimulation.Create(ControlScenario(preset));

        var livingCounts = ReadPrivateArray<int[]>(
            simulation, LivingCountsFieldName);
        var jitterRaw = ReadPrivateArray<int[]>(simulation, JitterFieldName);
        var marginRaw = ReadPrivateArray<int[]>(simulation, MarginFieldName);

        var observed = new Dictionary<int, ClaimedGeometry>();

        while (simulation.Outcome == BattleOutcome.Ongoing &&
            simulation.Tick < ObservationTickCeiling)
        {
            simulation.AdvanceOneTick();

            for (var slot = 0; slot < livingCounts.Length; slot++)
            {
                var livingCount = livingCounts[slot];
                if (livingCount == 0)
                {
                    continue;
                }

                var geometry = new ClaimedGeometry(
                    jitterRaw[slot], marginRaw[slot]);

                if (observed.TryGetValue(livingCount, out var alreadySeen))
                {
                    if (alreadySeen != geometry)
                    {
                        throw new InvalidOperationException(
                            $"Under {preset} a contingent of {livingCount} " +
                            $"living members claimed {geometry} on one tick " +
                            $"and {alreadySeen} on another. Both quantities " +
                            $"are supposed to be pure functions of the living " +
                            $"count under a fixed body radius.");
                    }

                    continue;
                }

                observed[livingCount] = geometry;
            }
        }

        return observed;
    }

    private static int[] SharedLivingCounts(
        Dictionary<int, ClaimedGeometry> left,
        Dictionary<int, ClaimedGeometry> right) =>
        [.. left.Keys.Where(right.ContainsKey).OrderBy(count => count)];

    /// <summary>
    /// The half-side of the square a contingent of
    /// <paramref name="livingCount"/> living members claims under
    /// <paramref name="rules"/>, in the same shape
    /// <c>BattleSimulation.ResolveContingentStates</c> computes it: the
    /// unscaled packing margin — the jitter radius plus one body radius —
    /// under a closed gate, and a basis-point fraction of that margin,
    /// multiplied in <see langword="long"/> before dividing, under an open
    /// one.
    /// </summary>
    private static int ClaimedSquareHalfSideRaw(
        MovementRuleset rules, int bodyRadiusRaw, int livingCount)
    {
        var unscaledMarginRaw = checked(
            FormationRules.ComputeContingentJitterRaw(
                bodyRadiusRaw, livingCount) + bodyRadiusRaw);

        return rules.GathersContingentsBeforeContact
            ? (int)((long)unscaledMarginRaw *
                    rules.CohesionSquareMarginBasisPoints /
                MovementRuleset.UnscaledCohesionSquareMarginBasisPoints)
            : unscaledMarginRaw;
    }

    /// <summary>
    /// <paramref name="source"/> restated field for field with a different
    /// <see cref="MovementRuleset.CohesionSquareMarginBasisPoints"/>, so the
    /// 10,000-basis-point fact can be stated about a real ruleset — one the
    /// constructor's own coupled validation accepted — rather than about a
    /// number.
    /// </summary>
    private static MovementRuleset WithCohesionSquareMarginBasisPoints(
        MovementRuleset source, int cohesionSquareMarginBasisPoints) =>
        new(
            id: source.Id,
            version: source.Version,
            cohesionRadiusMultiplier: source.CohesionRadiusMultiplier,
            closeRadiusMultiplier: source.CloseRadiusMultiplier,
            closeFractionNumerator: source.CloseFractionNumerator,
            closeFractionDenominator: source.CloseFractionDenominator,
            minimumCohesiveMembers: source.MinimumCohesiveMembers,
            cohesionCycleTicks: source.CohesionCycleTicks,
            cohesionDutyTicks: source.CohesionDutyTicks,
            arrivalTaperMultiplier: source.ArrivalTaperMultiplier,
            offsetUnit: source.OffsetUnit,
            narrowsCohesionScanToCohesionCapableContingents:
                source.NarrowsCohesionScanToCohesionCapableContingents,
            selectsLeaderByRank: source.SelectsLeaderByRank,
            usesEquipmentRelativeFootwork: source.UsesEquipmentRelativeFootwork,
            immediateRadiusBodyDiametersBasisPoints:
                source.ImmediateRadiusBodyDiametersBasisPoints,
            supportRadiusBodyDiametersBasisPoints:
                source.SupportRadiusBodyDiametersBasisPoints,
            loadoutMovementProfiles: source.LoadoutMovementProfiles,
            appliesPressureInterrupt: source.AppliesPressureInterrupt,
            supportPressureWeightBasisPoints:
                source.SupportPressureWeightBasisPoints,
            incomingDamageWeightBasisPoints:
                source.IncomingDamageWeightBasisPoints,
            allyCollapseWeightBasisPoints: source.AllyCollapseWeightBasisPoints,
            gathersContingentsBeforeContact:
                source.GathersContingentsBeforeContact,
            cohesionBandNumerator: source.CohesionBandNumerator,
            cohesionBandDenominator: source.CohesionBandDenominator,
            cohesionSquareMarginBasisPoints: cohesionSquareMarginBasisPoints);

    private static int FirstLivingSlotWithALeader(
        int[] livingCounts, ulong[] leaderEntityIds)
    {
        for (var slot = 0; slot < livingCounts.Length; slot++)
        {
            if (livingCounts[slot] > 0 && leaderEntityIds[slot] != 0)
            {
                return slot;
            }
        }

        throw new InvalidOperationException(
            "The control scenario produced no living contingent with a " +
            "selected leader after one tick, so there is nothing to probe.");
    }

    private static int FirstTickWithAnOpenCohesionWindow(
        MovementRuleset rules, int slot)
    {
        for (var tick = 0; tick < rules.CohesionCycleTicks; tick++)
        {
            if (MovementRules.IsCohesionWindowOpen(
                tick, slot, rules.CohesionCycleTicks, rules.CohesionDutyTicks))
            {
                return tick;
            }
        }

        throw new InvalidOperationException(
            $"No tick in a whole cohesion cycle of {rules.CohesionCycleTicks} " +
            $"opens the window for slot {slot}, so gate 3 would deny the " +
            $"probe for a reason that has nothing to do with the band.");
    }

    private static AgentState AgentWithEntityId(
        AgentState[] agentStates, ulong entityId)
    {
        foreach (var state in agentStates)
        {
            if (state.EntityId == entityId)
            {
                return state;
            }
        }

        throw new InvalidOperationException(
            $"No agent carries entity id {entityId}.");
    }

    /// <summary>
    /// Fetches one private array field of <see cref="BattleSimulation"/>, in
    /// the same shape and with the same loud failure message
    /// <c>ContingentCohesionCalibrationHarness</c> uses. Every field named
    /// here is <c>readonly</c>, so the reference cannot move underneath the
    /// caller.
    /// </summary>
    private static T ReadPrivateArray<T>(
        BattleSimulation simulation, string fieldName)
        where T : class
    {
        var field = typeof(BattleSimulation).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InvalidOperationException(
                $"BattleSimulation no longer declares a private field named " +
                $"'{fieldName}'. These tests read it by reflection to observe " +
                $"the cohesion geometry production actually used; rename the " +
                $"constant beside this message to match.");

        return field.GetValue(simulation) as T ??
            throw new InvalidOperationException(
                $"BattleSimulation's '{fieldName}' is no longer a " +
                $"{typeof(T).Name}. See the note above.");
    }

    // ----- Task 12: the blocked-streak deadlock guard -----

    /// <summary>
    /// Task 12's guard, and the counterpart to the termination sweep in
    /// <c>RangedTerminationTests</c>. Task 7 shrinks a contingent's claimed
    /// square to 6000 basis points of the margin the packing bound in
    /// <see cref="FormationRules.ComputeContingentJitterRaw"/> derives, so
    /// V14 parks its aim points closer together than any preset before it.
    /// The failure that buys is not a battle that never ends — the
    /// termination sweep would see that — it is a battle that ends normally
    /// while one warrior spent hundreds of ticks walking into an ally he
    /// could never get past. Only the blocked-streak metric sees that one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The bound, the configuration, and the measurement are all taken from
    /// <see cref="Hukbo.Core.Tests.LastStandFormationTests.AMaximumSizedLastStandNeverLeavesAWarriorBlockedTooLongAcrossSeedsOneThroughTwenty"/>
    /// rather than invented here: the same thirty-two warriors, the same
    /// maximum-sized last stand, the same twenty seeds, the same
    /// <c>BattleSimulation.LongestBlockedStreakTicks</c> reading, and the
    /// same 125-tick PROVISIONAL bound. Only the movement preset differs, so
    /// this test and that one are read against one yardstick and a
    /// difference between them is a difference V14 caused.
    /// </para>
    /// <para>
    /// Measured at the time of writing, across seeds 1 to 20 in this
    /// configuration: the V14 streak runs 3 to 49 ticks with a median of 20,
    /// worst on seed 9, against a bound of 125. The two neighbouring presets
    /// measured through the identical loop come out at 37 (V13, the shipped
    /// default) and 62 (V4, the preset the 125 was originally fitted to), so
    /// V14 sits between them: closer parking does cost roughly a dozen ticks
    /// of blocking over its immediate predecessor, which is exactly the trade
    /// finding 1 of the plan records, and it is nowhere near the bound.
    /// </para>
    /// <para>
    /// Twenty seeds is a sample and not a proof, and it is worth saying
    /// plainly what that leaves open. A permanent block runs into the
    /// thousands as it approaches the tick limit, so this bound has two
    /// orders of magnitude of detection power against the failure it names —
    /// but the neighbouring 200-seed sweep in that same file found a 272-tick
    /// transient block on seed 196 in a battle that resolved perfectly well,
    /// which is what a twenty-seed sample of this metric can miss. A pass
    /// here means no warrior in these twenty battles blocked for long enough
    /// to look stuck; it does not establish that none ever will.
    /// </para>
    /// <para>
    /// The configuration is deliberately the reference test's rather than a
    /// full two-hundred-warrior deployment, and that is a measured choice.
    /// Run through this same loop at two hundred warriors, every preset
    /// exceeds 125 — 173 for V4, 143 for V13, 126 for V14 — because a battle
    /// with an order of magnitude more bodies in it blocks for longer for
    /// reasons that have nothing to do with contingent cohesion. Transplanting
    /// the bound to that shape would produce a test that fails hardest on the
    /// shipped default, which would report nothing about V14 at all. V14
    /// being the lowest of the three at that size is the reassurance; the
    /// bound is held where it was fitted.
    /// </para>
    /// </remarks>
    [Fact]
    public void ContingentCohesionBeforeContactV14NeverLeavesAWarriorBlockedTooLongAcrossSeedsOneThroughTwenty()
    {
        const int MaximumAllowedBlockedStreakTicks = 125;
        var worstStreakTicks = 0;
        var worstDiagnostics = string.Empty;

        for (ulong seed = 1; seed <= 20; seed++)
        {
            // Sixteen warriors per faction at
            // FormationRules.MaximumLastStandThresholdAgents, the
            // square-packing bound, so both factions reach the tightest
            // formation the design permits — the configuration in which a
            // shrunken claimed square has the least room to be wrong in.
            var scenario = Scenario.CreateDefault(seed, totalAgents: 32) with
            {
                MovementPreset =
                    MovementPresetId.ContingentCohesionBeforeContactV14,
                LastStandThresholdAgents =
                    FormationRules.MaximumLastStandThresholdAgents,
            };
            scenario.Validate();

            var simulation = BattleSimulation.Create(scenario);

            // Bounded, for the reason the termination sweep gives: an
            // unbounded loop turns a stall into a suite that hangs with no
            // diagnosis rather than a test that fails and names the seed.
            while (simulation.Outcome == BattleOutcome.Ongoing &&
                simulation.Tick < scenario.TickLimit)
            {
                simulation.AdvanceOneTick();
            }

            if (simulation.LongestBlockedStreakTicks <= worstStreakTicks)
            {
                continue;
            }

            var livingFaction0 = simulation.Agents.Count(
                agent => agent.FactionId == 0 && agent.IsAlive);
            var livingFaction1 = simulation.Agents.Count(
                agent => agent.FactionId == 1 && agent.IsAlive);
            worstStreakTicks = simulation.LongestBlockedStreakTicks;
            worstDiagnostics =
                $"seed {seed} stopped at tick {simulation.Tick} of " +
                $"{scenario.TickLimit}, outcome {simulation.Outcome}, " +
                $"living counts [{livingFaction0}, {livingFaction1}]";
        }

        Assert.True(
            worstStreakTicks <= MaximumAllowedBlockedStreakTicks,
            $"Longest observed blocked streak was {worstStreakTicks} ticks " +
            $"across seeds 1 to 20 under " +
            $"{MovementPresetId.ContingentCohesionBeforeContactV14}, " +
            $"exceeding the {MaximumAllowedBlockedStreakTicks}-tick " +
            "PROVISIONAL bound the same sweep applies to the shipped " +
            "default. A failure here means V14's claimed square is shrunk " +
            "past what the collision resolver can absorb, which is a tuning " +
            "decision about the 6000 basis-point margin to revisit — not a " +
            $"bound to raise. Worst seed: {worstDiagnostics}.");
    }
}
