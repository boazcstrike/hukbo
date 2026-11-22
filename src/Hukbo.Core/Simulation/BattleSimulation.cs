using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Hukbo.Core.Combat;
using Hukbo.Core.Determinism;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;

namespace Hukbo.Core.Simulation;

/// <summary>
/// Authoritative, deterministic, fixed-tick battle state.
/// </summary>
public sealed partial class BattleSimulation
{
    private static readonly ReadOnlyCollection<BattleEvent> EmptyEvents =
        Array.AsReadOnly<BattleEvent>([]);

    /// <summary>
    /// The number of factions a battle has. Two, everywhere: faction 0 and
    /// faction 1. Named so the per-faction observability buffers in
    /// <see cref="GatherAndCommitAttacks"/> do not carry a bare literal.
    /// </summary>
    private const int FactionCount = 2;

    private readonly CombatRuleset _rules;
    private readonly MovementRuleset _movementRules;
    private readonly AgentState[] _agentStates;
    private readonly AgentIndexMap _agentIndexes;

    // Faction of each agent, indexed by entity ID, for the count-only collision
    // metrics. Empty when the IDs are too sparse for an array, which sends
    // MeasureCollision down the pair-list path instead. Snapshotted once in the
    // constructor: that is valid only because AgentState.FactionId is get-only,
    // and a mutable faction would silently desync this array.
    private readonly int[] _factionByEntityId;
    private readonly int[] _damageTotals;

    // Armor identity: parallel to _damageTotals, one slot per agent,
    // cleared and populated on the same schedule. _armorSavedTargets[i]
    // is set the first time, within a tick, that a landed blow against
    // agent i would have been lethal at its raw damage but was not once
    // armor mitigation was applied; _armorSavedPoints[i] then holds the
    // number of damage points that specific blow's armor absorbed. Only
    // the first qualifying blow against a target in a tick is recorded,
    // in the fixed iteration order the two damage-accumulation loops
    // below already use, so a target hit by two such blows in the same
    // tick still reports exactly one ArmorSaved event.
    private readonly bool[] _armorSavedTargets;
    private readonly int[] _armorSavedPoints;
    private readonly (int XRaw, int YRaw, ulong TargetId)?[] _movementProposals;
    private readonly (int SourceIndex, int TargetIndex, BodyPart HitLocation,
        AttackResolution Resolution, int? ComboPosition)[] _attackProposals;
    private readonly AgentView[] _agentViews;
    private readonly ReadOnlyCollection<AgentView> _agents;
    private readonly CollisionScratch _collision;

    // The projectile pool (ranged-units plan RU-17): a flat array sized once
    // here from Scenario.MaximumProjectilesInFlight, with a live count.
    // Launch appends at the count; a resolved or missed shot compacts the
    // tail forward, preserving order, in GatherAndCommitAttacks's new pass
    // A0. Sized zero, exactly like the V6/V7 scratch above, under any
    // ruleset that fields no ranged weapon -- every preset up to and
    // including PrecolonialPhilippinesV4 -- so a legacy battle carries no
    // pool storage at all and never allocates one. _hasRangedWeapon is
    // computed once here, from the ruleset's roster, and reused by both the
    // pool sizing below and the state-hash gate ComputeStateHash passes to
    // StateHasher.Compute, following the hasRankLevels precedent at
    // StateHasher.cs:136-139.
    private readonly bool _hasRangedWeapon;
    private readonly Projectile[] _projectiles;
    private int _projectileLiveCount;

    // A launch attempted while the pool already holds
    // Scenario.MaximumProjectilesInFlight live projectiles is refused
    // outright: the shot does not occur and the launching warrior's cooldown
    // is not charged. Derived observability in the same mould as
    // _movementConflictDenials: never hashed, never snapshotted, never read
    // by any simulation stage.
    private long _projectileLaunchRefusals;

    // Persistent-contingent movement state, resolved once per tick by
    // ResolveContingentStates and consumed later the same tick by
    // GatherMovementProposals's cohesion branch. Every array is sized once
    // here, to FormationPlanner.MaximumContingents * 2 (one slot per
    // faction-and-contingent pair), so a warm tick allocates nothing. Slot
    // index is always FactionId * FormationPlanner.MaximumContingents +
    // ContingentId, matching design section 3.4. Under
    // MovementPresetId.IndependentPursuitV1, ResolveContingentStates returns
    // on its first line and none of these arrays is ever written or read.
    private const int ContingentSlotCount = FormationPlanner.MaximumContingents * 2;
    private readonly int[] _contingentInitialCounts;
    private readonly ulong[] _contingentLeaderEntityIds;
    private readonly int[] _contingentLivingCounts;
    private readonly long[] _contingentSpreadSquared;
    private readonly int[] _contingentContactCounts;
    private readonly int[] _contingentJitterRaw;
    private readonly int[] _contingentTrailBaseXRaw;
    private readonly int[] _contingentTrailBaseYRaw;
    private readonly int[] _contingentMarginRaw;
    private readonly bool[] _contingentSquareFitsMap;
    private readonly bool[] _contingentSquareOverlapsAnother;
    private readonly ContingentState[] _contingentResolvedStates;

    // Per-faction last-stand state, recomputed by one forward scan at the top
    // of every SelectTargetsAndIntents call. Allocated once here so the scan
    // never allocates per tick. Index 0 is faction 0, index 1 is faction 1.
    // A rally entity ID of 0 means the faction has no living agent this tick;
    // 0 is never a valid EntityId (AgentState rejects it), so it is a safe
    // sentinel.
    private readonly int[] _factionLivingCounts;
    private readonly ulong[] _factionRallyEntityIds;

    // Whether each faction's rally agent is itself within its own weapon reach
    // of a living enemy this tick, under the same perception filter target
    // selection applies. Derived scratch beside the two arrays above: never
    // hashed, never snapshotted, recomputed from scratch every tick before any
    // intent is assigned, and always false under every preset before
    // MovementPresetId.LastStandEngagementV11. It exists as a pre-pass rather
    // than as a read of the rally agent's own Intent because
    // SelectTargetsAndIntents is a single forward scan: a rally agent can sit
    // after its own follower in _agentStates, so reading its intent mid-scan
    // would make a follower's intent depend on array order.
    private readonly bool[] _factionRallyEngaged;

    // Equipment-relative local-context scratch (weapon-relative movement
    // design, section 7). One row per scenario agent, allocated once here,
    // cleared and overwritten every tick by SelectTargetsAndIntents, and
    // never hashed, never snapshotted, never grown. Sized zero under every
    // legacy preset so a V1-through-V5 battle carries no per-agent context
    // storage at all. The per-faction surviving compositions are the two
    // fixed faction slots of design section 7.5, derived in the same
    // pre-movement stage and equally scratch. The derivation counter is
    // observability only — the test seam proving legacy presets never invoke
    // context accumulation — and is never read by any simulation stage.
    private readonly LocalMovementContext[] _localMovementContexts;
    private readonly LoadoutCompositionCounts[] _factionSurvivingCompositions;
    private long _localMovementContextDerivations;

    // Ranged-retreat threat-observation scratch (battlefield realism design,
    // section 5.3). One long per scenario agent, allocated once here, sized
    // zero under every preset except BattlefieldRealismV10 so a V1-through-V9
    // battle carries no per-agent storage for a rung it never runs. Cleared
    // and refilled every tick by SelectTargetsAndIntents, fused into the same
    // candidate loop that already computes each candidate's squared distance
    // for target selection and (under V6) the local-context accumulation --
    // no second scan. The value is the squared distance to the nearest living
    // enemy whose weapon is melee, observed only for an actor whose own
    // weapon is ranged, or long.MaxValue when no such enemy is observed; a
    // plain minimum over a totally ordered domain needs no EntityId
    // tie-break, because the reduction is order-independent. Derived scratch
    // in the same mould as _localMovementContexts: never hashed, never
    // snapshotted, never carried across ticks -- this is recomputed from
    // nothing every tick inside an existing loop, not a target cache.
    private readonly long[] _nearestMeleeThreatSquared;

    /// <summary>
    /// One nearest-enemy grid per faction, indexed by <see cref="AgentState.FactionId"/>
    /// (Hukbo fields exactly two). Derived scratch, rebuilt in full at the top
    /// of <see cref="SelectTargetsAndIntents"/> and never hashed, snapshotted,
    /// or carried across ticks. See
    /// <c>docs/plans/2026-11-21-ring-search-targeting-design.md</c>.
    /// </summary>
    private readonly TargetingGrid[] _targetingGrids;

    /// <summary>
    /// The current actor's local-context accumulator, reused for each actor of
    /// <see cref="SelectTargetsAndIntents"/> and re-seeded at the top of every
    /// actor's turn. Held here rather than inside <see cref="TargetSearch"/> so
    /// the ring search, which derives no local context, does not zero-fill and
    /// copy the accumulator's large state for every actor of every tick. Read
    /// only under a preset that derives local context.
    /// </summary>
    private MovementContextAccumulator _contextAccumulator;

    /// <summary>
    /// Per-agent scratch for the evasive-footwork post-pass. Zero-length under
    /// every preset but <see cref="MovementPresetId.EvasiveFootworkV14"/> and
    /// <see cref="MovementPresetId.SeparatingContactV17"/>, following the
    /// <see cref="_nearestMeleeThreatSquared"/> precedent. The pass resolves
    /// into this array and commits it onto
    /// <see cref="AgentState.EvasiveAction"/> only once the whole loop has
    /// finished, so no agent can read another agent's already-updated value.
    /// </summary>
    private readonly EvasiveAction[] _evasiveActionScratch;

    // Equipment-relative footwork pipeline scratch (weapon-relative movement
    // design, sections 8 through 11). Every array is allocated once here and
    // sized zero under every legacy preset, so a V1-through-V5 battle
    // carries none of this storage and no V6 stage ever runs for it. None
    // of these values is hashed or snapshotted: the authoritative results —
    // posture, phase, timer, facing, retained pace — live on AgentState and
    // are written by the stages that own them. The conflict-denial counter
    // is derived observability in the same mould as the collision counters:
    // never read by any simulation stage, reported by T9's wiring.
    private readonly TacticalPosture[] _contingentPostures;
    private readonly FootworkPhase[] _provisionalFootworkPhases;
    private readonly int[] _provisionalFootworkTicks;
    private readonly int[] _proposedPaceRaw;
    private readonly bool[] _attackAcceptedThisTick;
    private readonly int[] _factionLocalIndexes;
    private readonly FriendlyClearanceProposal[] _conflictProposals;
    private readonly int[] _conflictProposalAgentIndexes;
    private readonly bool[] _conflictAccepted;
    private long _movementConflictDenials;

    // The four-way split of TryProposeEquipmentRoute's route-refusal reasons
    // (ranged-units plan RU-06, F-A): derived observability in the same
    // mould as _movementConflictDenials just above. A denied route's reason
    // is not recoverable from an AgentView the way a phase is, so
    // TryProposeEquipmentRoute records which reason applied to
    // _pendingRouteRefusalReasons below rather than incrementing directly:
    // ApplyEquipmentAttackFootworkAndDeathCleanup can still overwrite this
    // same tick's Refuse with Commit, for an agent whose gathered attack the
    // combat stage accepted after the route was already rejected, and a
    // reason recorded for a tick that never actually surfaces as Refuse must
    // not be counted. ReconcileRouteRefusalReasonCounters resolves each
    // pending reason against the tick's now-final FootworkPhase and
    // increments exactly one of the four counters below, so together they
    // decompose MovementBehaviorMetrics.RefuseAgentTicks exactly, never
    // inflated by a reason a later stage overrode.
    private long _routeRefusalNoCandidatesBuilt;
    private long _routeRefusalStepEndpointRejected;
    private long _routeRefusalDirectCandidateOmitted;
    private long _routeRefusalLaneNotClear;

    // One slot per scenario agent: which of the four reasons, if any,
    // TryProposeEquipmentRoute recorded for the agent this tick, pending
    // ReconcileRouteRefusalReasonCounters resolving it after the
    // accepted-attack override has had its say. Allocated only when
    // usesFootwork, matching every other V6-only scratch array above.
    private readonly RouteRefusalReason[] _pendingRouteRefusalReasons;

    // Pressure-interrupt scratch (V7 design section 3, question 2). One slot
    // per scenario agent, allocated once here and sized zero under every preset
    // whose MovementRuleset.AppliesPressureInterrupt is false — which is every
    // preset from V1 through V6 — so a legacy battle carries no storage for a
    // feature it never runs. The answer is produced exactly once per living
    // agent per tick, by the single ShouldPressureInterrupt call in
    // ResolveEquipmentPosturesAndProvisionalFootwork, and is read twice in that
    // same iteration: once by the cost the interrupt charges and once by the
    // footwork ladder it preempts. One computation and one authority is what
    // keeps those two from ever disagreeing. The value is derived scratch in
    // the same mould as _provisionalFootworkPhases: never hashed, never
    // snapshotted, never persisted, because the authoritative results the
    // interrupt produces live on AgentState.
    private readonly bool[] _pressureInterruptFired;

    // The same scratch expressed as a number a spectator can read: the
    // weighted pressure sum divided back down by
    // WeaponMovementRules.RatioBasisPointScale, so the stored value is in the
    // same basis-point unit as this row's
    // LoadoutMovementProfile.PressureInterruptThresholdBasisPoints and the two
    // can be shown side by side without further arithmetic. Unlike the flag
    // above it is written for every living agent, not only for one whose
    // interrupt fired, because design section 3, question 8, channel 3
    // requires the inspector row on every tick — that is what lets a spectator
    // predict a break-off rather than only witness one. It is allocated,
    // sized, and cleared exactly as the flag is, and it is derived scratch in
    // the same category: never hashed, never snapshotted, never persisted.
    private readonly int[] _pressureBasisPoints;

    // Double-buffered event storage: each tick writes into whichever of these
    // two lists is not currently exposed through _lastEvents, so a caller
    // that retains one tick's LastEvents value keeps seeing that tick's data,
    // untouched, until this same buffer comes back around to be reused. The
    // matching ReadOnlyCollection wrapper for each buffer is built once, here,
    // never per tick, since List<T>.AsReadOnly would otherwise allocate a new
    // wrapper every tick even though the pair of backing lists never changes.
    private readonly List<BattleEvent> _eventBufferA;
    private readonly List<BattleEvent> _eventBufferB;
    private readonly ReadOnlyCollection<BattleEvent> _eventViewA;
    private readonly ReadOnlyCollection<BattleEvent> _eventViewB;
    private bool _nextEventBufferIsA = true;
    private ReadOnlyCollection<BattleEvent> _lastEvents;
    private long _eventSequence;
    private CollisionTickMetrics _lastTickCollision;
    private CombatMetrics _lastTickCombat;
    private FactionCombatMetrics _lastTickCombatByFaction;

    // Same contract as Dictionary.TryGetValue: false for unknown IDs; 0 is found only if an agent owns it.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryGetAgentIndex(ulong entityId, out int index) =>
        _agentIndexes.TryGetIndex(entityId, out index);

    // Same contract as the Dictionary indexer: throws KeyNotFoundException.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AgentIndexOf(ulong entityId) =>
        _agentIndexes.IndexOf(entityId);

    private BattleSimulation(
        Scenario scenario,
        AgentState[] agents,
        CombatRuleset rules)
    {
        Scenario = scenario;
        _rules = rules;
        _movementRules = MovementPresetRegistry.Get(scenario.MovementPreset);
        _agentStates = agents;
        _damageTotals = new int[agents.Length];
        _armorSavedTargets = new bool[agents.Length];
        _armorSavedPoints = new int[agents.Length];
        _movementProposals =
            new (int XRaw, int YRaw, ulong TargetId)?[agents.Length];
        _hasRangedWeapon = DetermineHasRangedWeapon(rules);
        // Sized for the worst case a single tick could buffer: every agent
        // resolving a melee blow (at most agents.Length) plus every pooled
        // projectile arriving the same tick (at most
        // MaximumProjectilesInFlight, pass A0's ceiling), so neither source
        // can ever overrun this array. Zero extra capacity, and therefore
        // byte-identical sizing to before this feature, under any ruleset
        // that fields no ranged weapon.
        _attackProposals =
            new (int SourceIndex, int TargetIndex, BodyPart HitLocation,
                AttackResolution Resolution, int? ComboPosition)[
                    agents.Length +
                        (_hasRangedWeapon ? scenario.MaximumProjectilesInFlight : 0)];
        _projectiles = _hasRangedWeapon
            ? new Projectile[scenario.MaximumProjectilesInFlight]
            : [];
        _agentViews = new AgentView[agents.Length];
        _agents = Array.AsReadOnly(_agentViews);
        _collision = new CollisionScratch(scenario, agents.Length, _movementRules);
        FillSeparationConstants();
        // Agents are clamped to the map, so no faction's bounding box is wider
        // than the longer map side and the grids never outgrow this size.
        var mapExtentRaw =
            (long)Math.Max(scenario.MapWidth, scenario.MapHeight) * FixedPoint.Scale;
        _targetingGrids =
        [
            new(TargetingGrid.DefaultCellSizeRaw, mapExtentRaw),
            new(TargetingGrid.DefaultCellSizeRaw, mapExtentRaw),
        ];
        _factionLivingCounts = new int[2];
        _factionRallyEntityIds = new ulong[2];
        _factionRallyEngaged = new bool[2];
        _localMovementContexts = _movementRules.UsesEquipmentRelativeFootwork
            ? new LocalMovementContext[agents.Length]
            : [];
        // Gated on preset identity, not on a movement-ruleset field, matching
        // the V10 deployment branch in Create: V10's registered ruleset is a
        // verbatim copy of V8's, so no ruleset flag distinguishes it.
        _nearestMeleeThreatSquared = UsesBattlefieldRealism(scenario.MovementPreset)
            ? new long[agents.Length]
            : [];
        _evasiveActionScratch =
            scenario.MovementPreset is MovementPresetId.EvasiveFootworkV14
                or MovementPresetId.SeparatingContactV17
                ? new EvasiveAction[agents.Length]
                : [];
        _factionSurvivingCompositions = new LoadoutCompositionCounts[2];
        var usesFootwork = _movementRules.UsesEquipmentRelativeFootwork;
        _contingentPostures = usesFootwork
            ? new TacticalPosture[ContingentSlotCount]
            : [];
        _provisionalFootworkPhases = usesFootwork
            ? new FootworkPhase[agents.Length]
            : [];
        _provisionalFootworkTicks = usesFootwork ? new int[agents.Length] : [];
        _proposedPaceRaw = usesFootwork ? new int[agents.Length] : [];
        _attackAcceptedThisTick = usesFootwork ? new bool[agents.Length] : [];
        _pendingRouteRefusalReasons = usesFootwork
            ? new RouteRefusalReason[agents.Length]
            : [];
        _factionLocalIndexes = usesFootwork ? new int[agents.Length] : [];
        _conflictProposals = usesFootwork
            ? new FriendlyClearanceProposal[agents.Length]
            : [];
        _conflictProposalAgentIndexes =
            usesFootwork ? new int[agents.Length] : [];
        _conflictAccepted = usesFootwork ? new bool[agents.Length] : [];
        _pressureInterruptFired = _movementRules.AppliesPressureInterrupt
            ? new bool[agents.Length]
            : [];
        _pressureBasisPoints = _movementRules.AppliesPressureInterrupt
            ? new int[agents.Length]
            : [];
        _eventBufferA = new List<BattleEvent>(agents.Length * 2);
        _eventBufferB = new List<BattleEvent>(agents.Length * 2);
        _eventViewA = new ReadOnlyCollection<BattleEvent>(_eventBufferA);
        _eventViewB = new ReadOnlyCollection<BattleEvent>(_eventBufferB);
        _contingentInitialCounts = new int[ContingentSlotCount];
        _contingentLeaderEntityIds = new ulong[ContingentSlotCount];
        _contingentLivingCounts = new int[ContingentSlotCount];
        _contingentSpreadSquared = new long[ContingentSlotCount];
        _contingentContactCounts = new int[ContingentSlotCount];
        _contingentJitterRaw = new int[ContingentSlotCount];
        _contingentTrailBaseXRaw = new int[ContingentSlotCount];
        _contingentTrailBaseYRaw = new int[ContingentSlotCount];
        _contingentMarginRaw = new int[ContingentSlotCount];
        _contingentSquareFitsMap = new bool[ContingentSlotCount];
        _contingentSquareOverlapsAnother = new bool[ContingentSlotCount];
        _contingentResolvedStates = new ContingentState[ContingentSlotCount];

        var entityIds = new ulong[agents.Length];
        for (var index = 0; index < agents.Length; index++)
        {
            entityIds[index] = agents[index].EntityId;
        }

        _agentIndexes = AgentIndexMap.Create(entityIds);
        _factionByEntityId = _agentIndexes.IsDense
            ? BuildFactionByEntityId(agents)
            : [];

        for (var index = 0; index < agents.Length; index++)
        {
            var agent = agents[index];
            var slot = checked(
                (agent.FactionId * FormationPlanner.MaximumContingents) +
                agent.ContingentId);
            _contingentInitialCounts[slot]++;
        }

        if (usesFootwork)
        {
            // Initial V6 facing (design section 6.3): East for faction 0,
            // West for faction 1, written once at simulation creation. The
            // faction-local index (design section 10.3) is the stable
            // ascending-EntityId rank within the agent's own faction,
            // computed here by explicit comparison so it never depends on
            // the incidental storage order; it is derived scratch and is
            // neither hashed nor snapshotted.
            for (var index = 0; index < agents.Length; index++)
            {
                var agent = agents[index];
                agent.Facing = agent.FactionId == 0
                    ? Facing16.East
                    : Facing16.West;

                var rank = 0;
                foreach (var other in agents)
                {
                    if (other.FactionId == agent.FactionId &&
                        other.EntityId < agent.EntityId)
                    {
                        rank++;
                    }
                }

                _factionLocalIndexes[index] = rank;
            }
        }

        UpdateViews();
        _lastEvents = EmptyEvents;
    }

    public Scenario Scenario { get; }

    public long Tick { get; private set; }

    public BattleOutcome Outcome { get; private set; }

    public IReadOnlyList<AgentView> Agents => _agents;

    /// <summary>
    /// Every event emitted by the tick just completed, in emission order.
    /// </summary>
    /// <remarks>
    /// The returned collection is owned by the simulation and is overwritten
    /// by a future <see cref="AdvanceOneTick"/> call once its backing buffer
    /// comes back around for reuse. Callers read it within the tick that
    /// produced it and never retain it.
    /// </remarks>
    public IReadOnlyList<BattleEvent> LastEvents => _lastEvents;

    /// <summary>
    /// Derived collision counters for the tick just completed. Observability
    /// only: never hashed, never snapshotted, never persisted.
    /// </summary>
    internal CollisionTickMetrics LastTickCollision => _lastTickCollision;

    /// <summary>
    /// Test-only seam: makes <see cref="SelectTargetsAndIntents"/> run its
    /// brute-force scan instead of the ring search. Both implementations
    /// select the same targets; this exists so a differential test can prove
    /// it. Not reachable from <see cref="Scenario"/> or the client.
    /// </summary>
    internal bool ForcesBruteForceTargeting { get; set; }

    /// <summary>
    /// The number of per-agent local-context derivations performed since the
    /// battle started. Observability only — the test seam proving legacy
    /// presets never invoke the V6 context accumulation — and never read by
    /// any simulation stage.
    /// </summary>
    internal long LocalMovementContextDerivationsForTesting =>
        _localMovementContextDerivations;

    /// <summary>
    /// The number of movement proposals the friendly-clearance conflict pass
    /// (weapon-relative movement design, section 10.6) has rejected since
    /// the battle started. Derived observability in the same mould as the
    /// collision counters: never hashed, never snapshotted, never read by
    /// any simulation stage, and zero forever under every legacy preset,
    /// which never runs the pass at all. A denied agent is indistinguishable
    /// from a blocked one in the view, so this counter is the only honest
    /// record.
    /// </summary>
    /// <remarks>
    /// Public for the same reason <see cref="LastTickCombat"/> is: a
    /// reporting layer carries the authoritative count instead of guessing at
    /// what a view cannot show. Reading it cannot affect the simulation — it
    /// is written only by <see cref="ResolveFriendlyClearanceConflicts"/> and
    /// never read back by any simulation stage.
    /// </remarks>
    public long MovementConflictDenials => _movementConflictDenials;

    /// <inheritdoc cref="MovementConflictDenials"/>
    internal long MovementConflictDenialsForTesting =>
        MovementConflictDenials;

    /// <summary>
    /// The number of <c>TryProposeEquipmentRoute</c> calls, since the battle
    /// started, that finalised <see cref="Movement.FootworkPhase.Refuse"/>
    /// because <c>BuildEquipmentRouteCandidates</c> produced zero candidates
    /// for the tick. One of the four rejection-reason counters (ranged-units
    /// plan RU-06, F-A) that together decompose
    /// <see cref="Simulation.MovementBehaviorMetrics.RefuseAgentTicks"/>.
    /// Derived observability in the same mould as
    /// <see cref="MovementConflictDenials"/>: never hashed, never
    /// snapshotted, never read by any simulation stage.
    /// </summary>
    public long RouteRefusalNoCandidatesBuilt =>
        _routeRefusalNoCandidatesBuilt;

    /// <summary>
    /// The number of <c>TryProposeEquipmentRoute</c> calls, since the battle
    /// started, whose last attempted candidate was rejected because
    /// <c>MovementRouteRules.StepEndpoint</c> found no legal step for it. One
    /// of the four rejection-reason counters decomposing
    /// <see cref="Simulation.MovementBehaviorMetrics.RefuseAgentTicks"/>. See
    /// <see cref="RouteRefusalNoCandidatesBuilt"/> for the shared derived
    /// observability contract.
    /// </summary>
    public long RouteRefusalStepEndpointRejected =>
        _routeRefusalStepEndpointRejected;

    /// <summary>
    /// The number of <c>TryProposeEquipmentRoute</c> calls, since the battle
    /// started, whose last attempted candidate was rejected because
    /// <c>ShouldOmitDirectCandidate</c> ruled out a direct approach subject
    /// to second-threat omission. One of the four rejection-reason counters
    /// decomposing
    /// <see cref="Simulation.MovementBehaviorMetrics.RefuseAgentTicks"/>. See
    /// <see cref="RouteRefusalNoCandidatesBuilt"/> for the shared derived
    /// observability contract.
    /// </summary>
    public long RouteRefusalDirectCandidateOmitted =>
        _routeRefusalDirectCandidateOmitted;

    /// <summary>
    /// The number of <c>TryProposeEquipmentRoute</c> calls, since the battle
    /// started, whose last attempted candidate was rejected because
    /// <c>IsLaneClearOfAllies</c> found the route too close to a friendly
    /// agent. One of the four rejection-reason counters decomposing
    /// <see cref="Simulation.MovementBehaviorMetrics.RefuseAgentTicks"/>. See
    /// <see cref="RouteRefusalNoCandidatesBuilt"/> for the shared derived
    /// observability contract.
    /// </summary>
    public long RouteRefusalLaneNotClear =>
        _routeRefusalLaneNotClear;

    /// <summary>
    /// The number of ranged-weapon launches refused, since the battle
    /// started, because the projectile pool already held
    /// <see cref="Scenario.MaximumProjectilesInFlight"/> live projectiles.
    /// A refused launch does not occur — no projectile enters the pool, no
    /// <see cref="BattleEventKind.Release"/> event is emitted, and the
    /// launching warrior's cooldown is not charged — so this is the only
    /// record that it was ever attempted. Derived observability in the same
    /// mould as <see cref="MovementConflictDenials"/>: never hashed, never
    /// snapshotted, never read by any simulation stage, and zero forever
    /// under every ruleset that fields no ranged weapon.
    /// </summary>
    public long ProjectileLaunchRefusals => _projectileLaunchRefusals;

    /// <summary>
    /// The local movement context derived for one agent by the tick just
    /// completed, for tests comparing the production observation against the
    /// naive oracle. Derived scratch: never hashed, never snapshotted, never
    /// persisted. Throws under a preset without equipment-relative footwork,
    /// which derives no context at all.
    /// </summary>
    internal LocalMovementContext LocalMovementContextForTesting(
        ulong entityId)
    {
        if (!_movementRules.UsesEquipmentRelativeFootwork)
        {
            throw new InvalidOperationException(
                "No local movement context is derived under movement preset " +
                $"{_movementRules.Id}.");
        }

        return _localMovementContexts[AgentIndexOf(entityId)];
    }

    /// <summary>
    /// The nearest-melee-threat scratch derived for one agent by the tick
    /// just completed (battlefield realism design, section 5.3): the squared
    /// distance to the nearest living enemy whose weapon is melee, or
    /// <see cref="long.MaxValue"/> when the agent's own weapon is not ranged
    /// or no such enemy was observed. Derived scratch: never hashed, never
    /// snapshotted, never persisted. Throws under any preset other than
    /// <see cref="MovementPresetId.BattlefieldRealismV10"/>, which observes
    /// no threat at all.
    /// </summary>
    internal long NearestMeleeThreatSquaredForTesting(ulong entityId)
    {
        if (!UsesBattlefieldRealism(Scenario.MovementPreset))
        {
            throw new InvalidOperationException(
                "No nearest-melee-threat scratch is derived under movement " +
                $"preset {Scenario.MovementPreset}.");
        }

        return _nearestMeleeThreatSquared[AgentIndexOf(entityId)];
    }

    /// <summary>
    /// The global surviving composition of one faction as derived by the
    /// tick just completed (weapon-relative movement design, section 7.5).
    /// Derived scratch: never hashed, never snapshotted, never persisted,
    /// and all-zero under a preset without equipment-relative footwork.
    /// </summary>
    internal LoadoutCompositionCounts SurvivingCompositionForTesting(
        int factionId) => _factionSurvivingCompositions[factionId];

    /// <summary>
    /// Derived attack-resolution counters for the tick just completed.
    /// Observability only: never hashed, never snapshotted, never persisted.
    /// </summary>
    /// <remarks>
    /// Public so a presentation layer can report authoritative counts rather
    /// than re-deriving them from the event stream. Reading this cannot affect
    /// the simulation: it is assigned once at the end of
    /// <see cref="GatherAndCommitAttacks"/> and never read back by any
    /// simulation stage.
    /// </remarks>
    public CombatMetrics LastTickCombat => _lastTickCombat;

    /// <summary>
    /// The same counters as <see cref="LastTickCombat"/>, split by the faction
    /// of the attacker. <see cref="FactionCombatMetrics.Total"/> equals
    /// <see cref="LastTickCombat"/> by construction — the undivided value is
    /// computed from this one, not counted separately.
    /// </summary>
    /// <remarks>
    /// This is one tick. A caller wanting battle totals sums it across ticks;
    /// the simulation deliberately holds no running total, because that would
    /// be mutable run-scoped state existing only for observability. See
    /// <see cref="FactionCombatMetrics"/>.
    /// </remarks>
    public FactionCombatMetrics LastTickCombatByFaction =>
        _lastTickCombatByFaction;

    /// <summary>
    /// Longest run of consecutive blocked ticks any single agent has reached.
    /// </summary>
    internal int LongestBlockedStreakTicks => _collision.LongestBlockedStreakTicks;

    public void AdvanceOneTick()
    {
        if (Outcome != BattleOutcome.Ongoing)
        {
            _lastEvents = EmptyEvents;
            return;
        }

        Tick = checked(Tick + 1);

        // Write into whichever buffer is not currently exposed through
        // _lastEvents, so a caller still holding a prior tick's LastEvents
        // value keeps seeing that tick's data unchanged. See the field
        // comment above _eventBufferA for the full scheme.
        var events = _nextEventBufferIsA ? _eventBufferA : _eventBufferB;
        events.Clear();

        DecrementCooldowns();
        SelectTargetsAndIntents();
        ResolveContingentStates();
        if (_movementRules.UsesEquipmentRelativeFootwork)
        {
            // Design section 11.2: posture, then the provisional footwork
            // phase, resolved between the contingent-state stage and
            // proposal gathering, under the opt-in flag only.
            ResolveEquipmentPosturesAndProvisionalFootwork();
        }

        GatherMovementProposals();
        if (_movementRules.UsesEquipmentRelativeFootwork)
        {
            // Design section 10.6: the friendly-clearance conflict pass runs
            // after every proposal exists and before anything is committed,
            // preserving the no-peeking invariant GatherMovementProposals
            // documents.
            ResolveFriendlyClearanceConflicts();
        }

        ResolveCollisions();
        CommitMovement(events);
        if (_movementRules.AppliesSeparatingContact)
        {
            ApplySeparatingContact();
        }

        MeasureCollision();
        GatherAndCommitAttacks(events);
        if (_movementRules.UsesEquipmentRelativeFootwork)
        {
            // Design section 9.6: surviving accepted attackers enter Commit,
            // and agents killed by the gathered exchange take death cleanup,
            // before any outcome, hash, or snapshot work.
            ApplyEquipmentAttackFootworkAndDeathCleanup();

            // RU-06, F-A: resolved only after the accepted-attack override
            // just above has had its say, so a route rejection whose Refuse
            // was overwritten to Commit this same tick is never counted.
            ReconcileRouteRefusalReasonCounters();
        }

        ResolveOutcome(events);

        UpdateViews();
        if (events.Count == 0)
        {
            _lastEvents = EmptyEvents;
        }
        else
        {
            _lastEvents = _nextEventBufferIsA ? _eventViewA : _eventViewB;
            _nextEventBufferIsA = !_nextEventBufferIsA;
        }
    }

    public ulong ComputeStateHash() => ComputeStateHash(_rules.ContentHash);

    /// <summary>
    /// Computes the state hash folding a caller-supplied ruleset content hash
    /// in place of this simulation's own.
    /// </summary>
    /// <remarks>
    /// The parameterless overload unconditionally folds the running ruleset's
    /// content hash, so there is no way through it to reproduce a hash recorded
    /// before that content hash moved. Reaching
    /// <see cref="StateHasher.Compute"/> directly is not an option either: it
    /// needs the agent states, which are private.
    /// </remarks>
    /// <param name="contentHash">The content hash to fold.</param>
    internal ulong ComputeStateHash(ulong contentHash) =>
        StateHasher.Compute(
            Scenario,
            Tick,
            Outcome,
            _eventSequence,
            _agentStates,
            contentHash,
            _rules.HasRankLevels,
            // V1 through V5 pass null so the fold stays byte-for-byte the
            // legacy layout; only an equipment-relative-footwork preset (V6)
            // folds its movement content hash and the five footwork fields.
            _movementRules.UsesEquipmentRelativeFootwork
                ? _movementRules.ContentHash
                : null,
            // A gate of its own, not a reuse of the one above: V6 already
            // passes a non-null movement content hash, so folding the three
            // pressure-interrupt fields inside that block would move V6's
            // per-agent byte layout. Only V7 registers this true.
            _movementRules.AppliesPressureInterrupt,
            // A gate of its own, not a reuse of the pressure-interrupt flag
            // above: a preset could carry pressure interrupt without
            // authoring shield block recovery, and folding the new field
            // inside that gate would move such a preset's per-agent byte
            // layout for no reason. Only the shield-projectile-block design's
            // movement preset registers this true.
            _movementRules.AppliesShieldBlockRecovery,
            // A ruleset with no ranged roster entry folds nothing at all for
            // the projectile pool -- not even a zero -- which is what keeps
            // every preset up to and including PrecolonialPhilippinesV4
            // exactly where its pinned hash already is.
            _hasRangedWeapon,
            new ReadOnlySpan<Projectile>(_projectiles, 0, _projectileLiveCount),
            // A third gate of its own, for the same reason the second is not
            // folded inside the first. Only V14, and V17 which is V14 plus
            // separating contact, register this true, so every preset from V1
            // to V13 folds nothing here and keeps the hash it already has.
            Scenario.MovementPreset is MovementPresetId.EvasiveFootworkV14
                or MovementPresetId.SeparatingContactV17);

    public BattleSnapshot CreateSnapshot()
    {
        var agents = Array.AsReadOnly(_agents.ToArray());
        var events = Array.AsReadOnly(_lastEvents.ToArray());
        var projectiles = Array.AsReadOnly(
            new ReadOnlySpan<Projectile>(_projectiles, 0, _projectileLiveCount)
                .ToArray());
        return new BattleSnapshot(
            Tick,
            Outcome,
            agents,
            events,
            ComputeStateHash(),
            projectiles);
    }

    /// <summary>
    /// The single approved battlefield-realism gate. The three behaviours the
    /// battlefield realism design introduced — cohort deployment, the
    /// nearest-melee-threat scratch, and the ranged retreat rung — are gated on
    /// preset identity rather than on a <see cref="MovementRuleset"/> field,
    /// because <see cref="MovementPresetId.BattlefieldRealismV10"/>'s
    /// registered ruleset is a verbatim copy of
    /// <see cref="MovementPresetId.RangedStandoffV8"/>'s and no flag
    /// distinguishes it. Every later preset that inherits those behaviours has
    /// to be admitted at every one of their call sites at once, so the set is
    /// named here rather than spelled out six times: a preset added to the
    /// enum and missed at one call site would silently lose one behaviour and
    /// keep the other two, which no single-file reading would catch.
    /// <see cref="MovementPresetId.ContingentShapeV12"/> is admitted for the
    /// same reason: it is defined as a strict superset of
    /// <see cref="MovementPresetId.LastStandEngagementV11"/>'s behaviour, so it
    /// inherits all three of these battlefield-realism behaviours unchanged
    /// before any contingent-shaping behaviour of its own is layered on top.
    /// <see cref="MovementPresetId.CohortLateralSpreadV13"/> is admitted for
    /// the same reason: it is defined as a strict superset of V11's
    /// behaviour too, so it inherits all three battlefield-realism
    /// behaviours unchanged before the lateral-riffle cohort traversal of
    /// its own is layered on top.
    /// <see cref="MovementPresetId.ContingentCohesionBeforeContactV15"/> is
    /// admitted for the same reason again: it is defined as a strict superset
    /// of <see cref="MovementPresetId.CohortLateralSpreadV13"/>'s behaviour, so
    /// it inherits all three battlefield-realism behaviours unchanged before
    /// the contingent-cohesion gate of its own is layered on top.
    /// <see cref="MovementPresetId.SeparatingContactV17"/> is admitted because
    /// it is defined as V14 plus separating contact and nothing else, so it
    /// inherits every V14 behaviour and the experiment changes one variable.
    /// </summary>
    private static bool UsesBattlefieldRealism(MovementPresetId preset) =>
        preset is MovementPresetId.BattlefieldRealismV10
            or MovementPresetId.LastStandEngagementV11
            or MovementPresetId.ContingentShapeV12
            or MovementPresetId.CohortLateralSpreadV13
            or MovementPresetId.EvasiveFootworkV14
            or MovementPresetId.ContingentCohesionBeforeContactV15
            or MovementPresetId.SeparatingContactV17;

    /// <summary>
    /// The single approved reach test. Attack range is measured centre to
    /// centre, never surface to surface, so intent selection and attack
    /// gathering cannot disagree about who can strike whom.
    /// </summary>
    private static bool IsWithinAttackRange(AgentState source, AgentState target) =>
        IsWithinAttackRange(source, SquaredDistance(source, target));

    private static bool IsWithinAttackRange(
        AgentState source,
        long squaredDistance) =>
        squaredDistance <= checked(
            (long)source.AttackRangeRaw * source.AttackRangeRaw);

    private static long SquaredDistance(AgentState left, AgentState right)
    {
        var deltaX = (long)right.XRaw - left.XRaw;
        var deltaY = (long)right.YRaw - left.YRaw;
        return checked((deltaX * deltaX) + (deltaY * deltaY));
    }

    private static long IntegerSquareRoot(long value) =>
        FixedPoint.IntegerSquareRoot(value);

    private void AddEvent(
        List<BattleEvent> events,
        BattleEventKind kind,
        ulong sourceEntityId,
        ulong? targetEntityId,
        int value,
        int? factionId)
    {
        _eventSequence = checked(_eventSequence + 1);
        events.Add(
            BattleEvent.NonAttack(
                _eventSequence,
                Tick,
                kind,
                sourceEntityId,
                targetEntityId,
                value,
                factionId));
    }

    /// <summary>
    /// Emits one attack event.
    /// </summary>
    /// <remarks>
    /// <paramref name="resolution"/> is required, unlike the optional
    /// parameter on the public <see cref="BattleEvent.Attack"/> factory. The
    /// factory keeps its default so that the twenty call sites in tests and
    /// presentation code that do not care about defensive resolution keep
    /// compiling; here the default would be a way for production code to emit
    /// an unresolved attack as though it had landed, and nothing downstream
    /// would notice.
    /// </remarks>
    private void AddAttackEvent(
        List<BattleEvent> events,
        ulong sourceEntityId,
        ulong targetEntityId,
        int damage,
        int factionId,
        WeaponId weapon,
        ShieldId shield,
        BodyPart hitLocation,
        AttackResolution resolution,
        int? comboPosition)
    {
        _eventSequence = checked(_eventSequence + 1);
        events.Add(
            BattleEvent.Attack(
                _eventSequence,
                Tick,
                sourceEntityId,
                targetEntityId,
                damage,
                factionId,
                weapon,
                shield,
                hitLocation,
                resolution,
                comboPosition));
    }

    /// <summary>
    /// Projects every <see cref="AgentState"/> onto its presentation-facing
    /// <see cref="AgentView"/>, including <see cref="AgentView.IsLeader"/>,
    /// derived here rather than stored: an agent leads exactly when its
    /// <see cref="AgentState.EntityId"/> equals the leader entity id this
    /// tick's <see cref="ResolveContingentStates"/> already recorded for its
    /// <c>(FactionId, ContingentId)</c> slot. Under
    /// <see cref="MovementPresetId.IndependentPursuitV1"/>,
    /// <see cref="ResolveContingentStates"/> returns before the leader scan
    /// ever runs, so every slot in <see cref="_contingentLeaderEntityIds"/>
    /// stays at its constructor-time value of <c>0</c> — never a valid
    /// <see cref="AgentState.EntityId"/> — for the whole battle, and every
    /// real agent's comparison below is therefore <see langword="false"/>
    /// with no extra preset check needed.
    /// </summary>
    /// <remarks>
    /// The three pressure-interrupt members are projected here rather than by
    /// <see cref="AgentState.ToView"/>, because only one of the three is
    /// authoritative agent state. <see cref="AgentView.PressureBasisPoints"/>
    /// comes from <see cref="_pressureBasisPoints"/>, the derived per-tick
    /// scratch this simulation owns, and
    /// <see cref="AgentView.PressureThresholdBasisPoints"/> comes from the
    /// running ruleset's profile row; an <see cref="AgentState"/> holds
    /// neither, and giving it either would turn scratch into stored state that
    /// the hash and the snapshot would then have to exclude by hand.
    /// <para>
    /// All three are written only for a living agent under a preset whose
    /// <see cref="MovementRuleset.AppliesPressureInterrupt"/> is
    /// <see langword="true"/>, and are left at their constructor defaults
    /// otherwise. That gate is doing three separate jobs. It keeps the index
    /// off <see cref="_pressureBasisPoints"/>, which is zero-length under every
    /// preset from V1 through V6. It keeps
    /// <see cref="MovementRuleset.ResolveLoadoutProfile"/> — which throws for
    /// every key under a preset without equipment-relative footwork — off the
    /// legacy path entirely. And it keeps a corpse at zero: the scratch slot
    /// still holds the value computed before the killing blow landed, since it
    /// is not cleared until the next tick's footwork stage, so reading it for a
    /// dead agent would show pressure on a warrior that can no longer feel any.
    /// The other three fields death cleanup clears — pace, posture, and phase —
    /// behave the same way by the tick's end.
    /// </para>
    /// <para>
    /// The projection is a <c>with</c> expression on a
    /// <see langword="readonly"/> <see langword="record"/>
    /// <see langword="struct"/>, so it copies on the stack and allocates
    /// nothing, and the profile lookup returns a row the registry built once at
    /// startup rather than a new object.
    /// </para>
    /// </remarks>
    private void UpdateViews()
    {
        var appliesPressureInterrupt = _movementRules.AppliesPressureInterrupt;
        for (var index = 0; index < _agentStates.Length; index++)
        {
            var agent = _agentStates[index];
            var slot = checked(
                (agent.FactionId * FormationPlanner.MaximumContingents) +
                agent.ContingentId);
            var isLeader = agent.EntityId == _contingentLeaderEntityIds[slot];
            var view = agent.ToView(isLeader);

            // Derived projection only — design section 8.1. Reads the
            // attack-cooldown pair the tick has already produced and the
            // weapon already resolved at spawn; nothing new is stored,
            // hashed, or snapshotted, and nothing here queries anything the
            // tick would not otherwise make.
            var (rangedPhase, rangedPhaseTicksRemaining) = RangedPhaseProjection.Derive(
                agent.Loadout.Weapon,
                agent.AttackCooldownRemaining,
                agent.AttackCooldownTicks);
            view = view with
            {
                RangedPhase = rangedPhase,
                RangedPhaseTicksRemaining = rangedPhaseTicksRemaining,
            };

            if (appliesPressureInterrupt && agent.IsAlive)
            {
                // The running value, on every tick rather than only on a tick
                // the interrupt fired: design section 3, question 8, channel 3
                // is explicit that a spectator has to be able to predict a
                // break-off, and a row that only appeared on a firing tick
                // would be blank almost always. ResolveEquipmentPosturesAnd-
                // ProvisionalFootwork writes the slot for every living agent
                // for exactly that reason, and this reads back the number it
                // wrote — no second computation and no second formula.
                view = view with
                {
                    BrokeOffUnderPressure = agent.BrokeOffUnderPressure,
                    PressureBasisPoints = _pressureBasisPoints[index],
                    PressureThresholdBasisPoints = _movementRules
                        .ResolveLoadoutProfile(agent.Loadout)
                        .PressureInterruptThresholdBasisPoints,
                };
            }

            _agentViews[index] = view;
        }
    }
}
