using System.Collections.Immutable;
using System.Collections.ObjectModel;
using Hukbo.Core.Combat;
using Hukbo.Core.Determinism;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;

namespace Hukbo.Core.Simulation;

/// <summary>
/// Authoritative, deterministic, fixed-tick battle state.
/// </summary>
public sealed class BattleSimulation
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
    private readonly Dictionary<ulong, int> _agentIndexes;
    private readonly int[] _damageTotals;
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

    private BattleSimulation(
        Scenario scenario,
        AgentState[] agents,
        CombatRuleset rules)
    {
        Scenario = scenario;
        _rules = rules;
        _movementRules = MovementPresetRegistry.Get(scenario.MovementPreset);
        _agentStates = agents;
        _agentIndexes = new Dictionary<ulong, int>(agents.Length);
        _damageTotals = new int[agents.Length];
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
        _collision = new CollisionScratch(scenario, agents.Length);
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

        for (var index = 0; index < agents.Length; index++)
        {
            if (!_agentIndexes.TryAdd(agents[index].EntityId, index))
            {
                throw new ArgumentException(
                    $"Duplicate entity ID {agents[index].EntityId}.",
                    nameof(agents));
            }

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

        return _localMovementContexts[_agentIndexes[entityId]];
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

        return _nearestMeleeThreatSquared[_agentIndexes[entityId]];
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

    public static BattleSimulation Create(Scenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        return Create(scenario, CombatPresetRegistry.Get(scenario.CombatPreset));
    }

    /// <summary>
    /// Builds a full spawn-placed battle on a caller-supplied ruleset, so a
    /// test can run the canonical workload against a tuning variant of the
    /// scenario's own preset.
    /// </summary>
    /// <remarks>
    /// The other testing factory takes explicit agents and never runs spawn
    /// placement, and no agents can be lifted out of a simulation built here:
    /// <see cref="Agents"/> and <see cref="CreateSnapshot"/> both return
    /// <see cref="AgentView"/> and the underlying states are private. A
    /// 200-agent seeded battle is therefore only reachable through this
    /// overload.
    /// </remarks>
    /// <param name="scenario">The scenario to build.</param>
    /// <param name="rules">The ruleset the simulation runs on.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="rules"/> declares a different roster from the registered
    /// entry for the scenario's combat preset. Scenario validation checks
    /// roster counts against the <em>registry</em>, so a differing roster would
    /// leave the scenario validated against one roster while the simulation ran
    /// on another.
    /// </exception>
    internal static BattleSimulation Create(Scenario scenario, CombatRuleset rules)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(rules);
        scenario.Validate();
        AssertRosterMatchesRegisteredPreset(scenario, rules);

        var random = new SplitMix64(scenario.Seed);
        var agents = new AgentState[scenario.TotalAgents];
        var mapWidthRaw = checked(scenario.MapWidth * FixedPoint.Scale);

        var rosterCountsAreEmpty = scenario.RosterCounts.IsDefaultOrEmpty;
        var expandedRosterIndices = rosterCountsAreEmpty
            ? ImmutableArray<int>.Empty
            : RosterCountExpansion.Expand(scenario.RosterCounts);

        // One resolution rule serves the spawn loops and the V6 deployment
        // assignment below, so the assignment can never rank a warrior by
        // one loadout and spawn it with another.
        CombatLoadout ResolveSpawnLoadout(ulong entityId, int localIndex) =>
            rosterCountsAreEmpty
                ? rules.ResolveLoadout(entityId)
                : rules.Roster[expandedRosterIndices[localIndex]];

        // Faction 0's own fielded Datu count, resolved before deployment is
        // planned so FormationPlanner can size
        // MovementPresetId.ContingentShapeV12's contingents off it without
        // ever learning what a Datu is (tasks 6 and 7 of
        // the contingent shape task plan). One deployment is
        // planned once and mirrored onto both factions below, so faction 0's
        // chief count is the one value the planner needs; every preset
        // before V12 ignores it entirely.
        var fieldedChiefCount = 0;
        for (var index = 0; index < scenario.AgentsPerFaction; index++)
        {
            if (ResolveSpawnLoadout(checked((ulong)index + 1), index).Rank == RankId.Datu)
            {
                fieldedChiefCount++;
            }
        }

        // One deployment is planned and mirrored across the vertical centre
        // line, so the two armies open in exactly the same shape. Both are
        // drawn from the same roster, so any positional difference at tick 0
        // would be seed noise that the battle then amplifies.
        var deployment = FormationPlanner.PlanFactionDeployment(
            scenario,
            fieldedChiefCount,
            ref random);

        // V6 alone reassigns warriors across the slots already planned
        // above (weapon-relative movement design, section 12). The
        // permutation is a pure function of the deployment and the resolved
        // loadouts — `random` is not consulted, so the SplitMix64 stream is
        // exactly what the planner left it — and it ranks the canonical,
        // unmirrored slots before the faction-1 mirror below, each faction
        // pairing its own loadouts against that same canonical ranking, so
        // equal faction-local loadout multisets keep the exact mirror.
        var faction0Deployment = deployment;
        var faction1Deployment = deployment;
        var movement = MovementPresetRegistry.Get(scenario.MovementPreset);
        if (movement.UsesEquipmentRelativeFootwork)
        {
            var faction0Loadouts = new CombatLoadout[scenario.AgentsPerFaction];
            var faction1Loadouts = new CombatLoadout[scenario.AgentsPerFaction];
            for (var index = 0; index < scenario.AgentsPerFaction; index++)
            {
                faction0Loadouts[index] = ResolveSpawnLoadout(
                    checked((ulong)index + 1), index);
                faction1Loadouts[index] = ResolveSpawnLoadout(
                    checked((ulong)(scenario.AgentsPerFaction + index) + 1),
                    index);
            }

            faction0Deployment = EquipmentDeploymentAssignment.AssignForFaction(
                deployment, faction0Loadouts, movement);
            faction1Deployment = EquipmentDeploymentAssignment.AssignForFaction(
                deployment, faction1Loadouts, movement);
        }
        else if (UsesBattlefieldRealism(scenario.MovementPreset))
        {
            // Gated on preset identity, not on a movement-ruleset field:
            // V10's registered ruleset is a verbatim copy of V8's, so
            // UsesEquipmentRelativeFootwork is false for it and the V6
            // branch above never fires for V10. This mirrors the V8 gate at
            // the retreat rung further down (Scenario.MovementPreset ==
            // MovementPresetId.RangedStandoffV8). CohortDeploymentAssignment
            // never draws — random is not consulted here either — so the
            // SplitMix64 stream is exactly what the planner left it, and it
            // runs on the canonical, unmirrored deployment before the
            // faction-1 mirror below, each faction pairing its own
            // loadouts against that same canonical ranking (battlefield
            // realism design, sections 4.2 to 4.6).
            var faction0Loadouts = new CombatLoadout[scenario.AgentsPerFaction];
            var faction1Loadouts = new CombatLoadout[scenario.AgentsPerFaction];
            for (var index = 0; index < scenario.AgentsPerFaction; index++)
            {
                faction0Loadouts[index] = ResolveSpawnLoadout(
                    checked((ulong)index + 1), index);
                faction1Loadouts[index] = ResolveSpawnLoadout(
                    checked((ulong)(scenario.AgentsPerFaction + index) + 1),
                    index);
            }

            // MovementPresetId.ContingentCohesionBeforeContactV14 is admitted
            // here for the same reason it is admitted to the
            // battlefield-realism and last-stand gates: it is defined as a
            // strict superset of V13's behaviour, so it inherits the
            // lateral-riffle cohort traversal unchanged before the
            // contingent-cohesion gate of its own is layered on top.
            var spreadCohortsLaterally =
                scenario.MovementPreset is MovementPresetId.CohortLateralSpreadV13
                    or MovementPresetId.ContingentCohesionBeforeContactV14;
            faction0Deployment = CohortDeploymentAssignment.AssignForFaction(
                deployment, faction0Loadouts, rules, spreadCohortsLaterally);
            faction1Deployment = CohortDeploymentAssignment.AssignForFaction(
                deployment, faction1Loadouts, rules, spreadCohortsLaterally);
        }

        for (var index = 0; index < scenario.AgentsPerFaction; index++)
        {
            var entityId = checked((ulong)index + 1);
            var loadout = ResolveSpawnLoadout(entityId, index);
            var (xRaw, yRaw, contingentId) = faction0Deployment[index];
            agents[index] = CreateAgent(
                entityId,
                factionId: 0,
                xRaw,
                yRaw,
                scenario,
                rules,
                loadout,
                contingentId);
        }

        for (var index = 0; index < scenario.AgentsPerFaction; index++)
        {
            var (leftXRaw, leftYRaw, contingentId) = faction1Deployment[index];
            var rightX = checked(mapWidthRaw - leftXRaw);
            var rightY = leftYRaw;
            var stateIndex = scenario.AgentsPerFaction + index;
            var entityId = checked((ulong)stateIndex + 1);
            // Faction-local index, not entityId/stateIndex: RosterCounts
            // describes one faction, and reusing the global index would
            // continue faction 1's category offset from wherever faction 0
            // stopped, silently giving the two factions different armies.
            // The same reasoning applies to contingentId, mirrored from the
            // one deployment plan alongside the position: it is the
            // faction-local dealing index, not a value tied to faction 0.
            var loadout = ResolveSpawnLoadout(entityId, index);
            agents[stateIndex] = CreateAgent(
                entityId,
                factionId: 1,
                rightX,
                rightY,
                scenario,
                rules,
                loadout,
                contingentId);
        }

        ResolveSpawnPlacement(agents, scenario);

        return new BattleSimulation(scenario, agents, rules);
    }

    internal static BattleSimulation CreateForTesting(
        Scenario scenario,
        params AgentState[] agents)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        return CreateForTesting(
            scenario,
            CombatPresetRegistry.Get(scenario.CombatPreset),
            agents);
    }

    /// <summary>
    /// Builds a battle from explicit agents on a caller-supplied ruleset. This
    /// is the only sanctioned way to give a test a clash-neutral
    /// configuration: no shipped loadout pairing is clash-neutral, and
    /// hand-picking seeds and entity identifiers whose roll happens to land
    /// would be silently invalidated by any later tuning or mixer change.
    /// </summary>
    /// <param name="scenario">The scenario to build.</param>
    /// <param name="rules">The ruleset the simulation runs on.</param>
    /// <param name="agents">The agents to place, in any order.</param>
    /// <exception cref="ArgumentException">
    /// No agent was supplied, or <paramref name="rules"/> declares a different
    /// roster from the registered entry for the scenario's combat preset.
    /// </exception>
    internal static BattleSimulation CreateForTesting(
        Scenario scenario,
        CombatRuleset rules,
        params AgentState[] agents)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(agents);
        scenario.Validate();
        AssertRosterMatchesRegisteredPreset(scenario, rules);

        if (agents.Length == 0)
        {
            throw new ArgumentException(
                "At least one agent is required.",
                nameof(agents));
        }

        var orderedAgents = agents.OrderBy(agent => agent.EntityId).ToArray();
        return new BattleSimulation(scenario, orderedAgents, rules);
    }

    /// <summary>
    /// Rejects an injected ruleset whose roster disagrees with the registered
    /// entry for the scenario's combat preset.
    /// </summary>
    /// <remarks>
    /// <see cref="Scenario.Validate"/> checks roster counts against the
    /// registry and is deliberately left alone, so injecting a differently
    /// rostered ruleset would validate the scenario against one roster and run
    /// it on another. The sanctioned use is a tuning variant of the same
    /// preset, where the roster is identical.
    /// </remarks>
    private static void AssertRosterMatchesRegisteredPreset(
        Scenario scenario,
        CombatRuleset rules)
    {
        var registered = CombatPresetRegistry.Get(scenario.CombatPreset);
        if (rules.Roster.SequenceEqual(registered.Roster))
        {
            return;
        }

        throw new ArgumentException(
            "The supplied ruleset declares a different roster from the " +
            $"registered entry for combat preset {scenario.CombatPreset}. " +
            "Scenario validation checks roster counts against the registry, " +
            "so the scenario and the simulation would disagree.",
            nameof(rules));
    }

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
            // A ruleset with no ranged roster entry folds nothing at all for
            // the projectile pool -- not even a zero -- which is what keeps
            // every preset up to and including PrecolonialPhilippinesV4
            // exactly where its pinned hash already is.
            _hasRangedWeapon,
            new ReadOnlySpan<Projectile>(_projectiles, 0, _projectileLiveCount));

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
    /// Makes the initial placement collision-free before the first tick.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a safety net rather than the primary repair.
    /// <see cref="FormationPlanner"/> already keeps every planned pair at least
    /// one raw unit clear of tangency, and the two factions occupy opposite
    /// halves of the map, so this pass normally finds nothing to move and the
    /// mirror survives intact. It re-engages only where a crowded map pushes
    /// the planner's fallback lattice past the edge of its own half, and there
    /// an approximate mirror is the correct degradation.
    /// </para>
    /// <para>
    /// Overlaps are repaired deterministically: agents are placed in ascending
    /// entity ID, and an agent that lands on an occupied spot is relocated by
    /// scanning rings of the eight compass offsets at increasing radius in one
    /// fixed order. The random stream is never consulted during relocation, so
    /// repairing a spawn cannot shift the seed sequence for anything that
    /// follows.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// An agent could not be placed. Scenario validation rejects impossible
    /// densities up front, so reaching this means the ring scan was exhausted
    /// and the placement is genuinely infeasible rather than merely crowded.
    /// </exception>
    private static void ResolveSpawnPlacement(
        AgentState[] agents,
        Scenario scenario)
    {
        var bodyRadiusRaw = scenario.BodyRadiusRaw;
        var stepRaw = checked(2 * bodyRadiusRaw);
        var mapWidthRaw = checked(scenario.MapWidth * FixedPoint.Scale);
        var mapHeightRaw = checked(scenario.MapHeight * FixedPoint.Scale);
        var maximumRing = checked(
            (Math.Max(mapWidthRaw, mapHeightRaw) / stepRaw) + 1);
        var grid = new CollisionUniformGrid(stepRaw);

        foreach (var agent in agents)
        {
            agent.XRaw = CollisionGeometry.ClampCenterToBounds(
                agent.XRaw,
                mapWidthRaw,
                bodyRadiusRaw);
            agent.YRaw = CollisionGeometry.ClampCenterToBounds(
                agent.YRaw,
                mapHeightRaw,
                bodyRadiusRaw);

            var isOccupied = grid.AnyContact(
                agent.XRaw,
                agent.YRaw,
                bodyRadiusRaw,
                agent.EntityId);
            if (isOccupied &&
                !TryRelocateSpawn(
                    grid,
                    agent,
                    bodyRadiusRaw,
                    stepRaw,
                    mapWidthRaw,
                    mapHeightRaw,
                    maximumRing))
            {
                throw new InvalidOperationException(
                    $"Entity {agent.EntityId} could not be placed without " +
                    "overlapping another body. Reduce the agent count or " +
                    "enlarge the map.");
            }

            grid.Insert(
                new CollisionBody(
                    agent.EntityId,
                    agent.XRaw,
                    agent.YRaw,
                    agent.IsAlive));
        }
    }

    private static bool TryRelocateSpawn(
        CollisionUniformGrid grid,
        AgentState agent,
        int bodyRadiusRaw,
        int stepRaw,
        int mapWidthRaw,
        int mapHeightRaw,
        int maximumRing)
    {
        ReadOnlySpan<(int X, int Y)> offsets =
        [
            (1, 0), (1, 1), (0, 1), (-1, 1),
            (-1, 0), (-1, -1), (0, -1), (1, -1),
        ];
        var originX = agent.XRaw;
        var originY = agent.YRaw;

        for (var ring = 1; ring <= maximumRing; ring++)
        {
            var spanRaw = checked((long)ring * stepRaw);

            foreach (var offset in offsets)
            {
                var candidateX = originX + (offset.X * spanRaw);
                var candidateY = originY + (offset.Y * spanRaw);

                if (candidateX < bodyRadiusRaw ||
                    candidateX > mapWidthRaw - bodyRadiusRaw ||
                    candidateY < bodyRadiusRaw ||
                    candidateY > mapHeightRaw - bodyRadiusRaw)
                {
                    continue;
                }

                var nextX = checked((int)candidateX);
                var nextY = checked((int)candidateY);
                if (grid.AnyContact(nextX, nextY, bodyRadiusRaw, agent.EntityId))
                {
                    continue;
                }

                agent.XRaw = nextX;
                agent.YRaw = nextY;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Resolves this warrior's damage, reach, and cooldown once, at spawn,
    /// from the weapon and shield they carry, and writes them into the
    /// per-agent fields that already exist and are already hashed.
    /// </summary>
    /// <remarks>
    /// This is the only place a weapon profile is read. Every tick stage goes
    /// on reading exactly the <see cref="AgentState"/> fields it read before,
    /// including the single approved reach test in
    /// <c>IsWithinAttackRange</c>, so intent selection and attack gathering
    /// still cannot disagree and no second reach path exists.
    /// <para>
    /// A preset that declares no profiles — version 1 — falls back to the
    /// scenario's global values, which is what its replays were recorded
    /// against.
    /// </para>
    /// </remarks>
    private static AgentState CreateAgent(
        ulong entityId,
        int factionId,
        int xRaw,
        int yRaw,
        Scenario scenario,
        CombatRuleset rules,
        CombatLoadout loadout,
        int contingentId)
    {
        var profile = rules.HasWeaponProfiles
            ? rules.ResolveWeaponProfile(loadout.Weapon, loadout.Shield)
            : new WeaponProfile(
                scenario.DamagePerAttack,
                scenario.AttackRangeRaw,
                scenario.AttackCooldownTicks);

        var level = rules.HasRankLevels
            ? rules.ResolveLevel(loadout.Rank)
            : scenario.PlaceholderFighterLevel;

        return new AgentState(
            entityId,
            factionId,
            xRaw,
            yRaw,
            scenario.MaximumHitPoints,
            scenario.MovementSpeedRaw,
            scenario.PerceptionRangeRaw,
            profile.AttackRangeRaw,
            profile.DamagePerAttack,
            profile.AttackCooldownTicks,
            loadout,
            level,
            contingentId);
    }

    /// <summary>
    /// Resolves the <see cref="WeaponProfile"/> that governs one warrior's
    /// attack-combination attributes at the moment of an attack attempt.
    /// Mirrors the fallback <see cref="CreateAgent"/> uses for damage, reach,
    /// and cooldown: a preset that declares no weapon profiles — version 1 —
    /// falls back to a synthetic profile built from the scenario's global
    /// values, whose combo fields default to the record's own no-op
    /// defaults (see <see cref="WeaponProfile"/>), so version 1 rolls the
    /// same combo checks every other preset does and they simply never
    /// succeed.
    /// </summary>
    /// <remarks>
    /// Resolved fresh on every attack attempt rather than cached on
    /// <see cref="AgentState"/> at spawn, unlike damage/reach/cooldown:
    /// those three are read every tick regardless of whether an attack is
    /// attempted, but the combo fields are read only on the attempts that
    /// pass the pre-check gate, which per-tick is a small fraction of the
    /// full roster.
    /// </remarks>
    private WeaponProfile ResolveAttackerWeaponProfile(CombatLoadout loadout) =>
        _rules.HasWeaponProfiles
            ? _rules.ResolveWeaponProfile(loadout.Weapon, loadout.Shield)
            : new WeaponProfile(
                Scenario.DamagePerAttack,
                Scenario.AttackRangeRaw,
                Scenario.AttackCooldownTicks);

    /// <summary>
    /// Whether <paramref name="rules"/> fields at least one ranged weapon —
    /// "this combat ruleset fields at least one ranged weapon" — computed
    /// once at construction and cached on <see cref="_hasRangedWeapon"/>
    /// rather than re-derived per tick. A weapon is ranged exactly when
    /// <see cref="WeaponProfile.StandoffDistanceRaw"/> is non-zero, the same
    /// test <see cref="WeaponProfile.ValidateRangedFields"/> uses; every
    /// registered preset up to and including
    /// <see cref="CombatPresetId.PrecolonialPhilippinesV4"/> declares no
    /// weapon profiles at all, so this returns <see langword="false"/> for
    /// every one of them without resolving a single profile.
    /// </summary>
    private static bool DetermineHasRangedWeapon(CombatRuleset rules)
    {
        if (!rules.HasWeaponProfiles)
        {
            return false;
        }

        foreach (var loadout in rules.Roster)
        {
            var profile = rules.ResolveWeaponProfile(loadout.Weapon, loadout.Shield);
            if (profile.StandoffDistanceRaw != 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Clears an attacker's active attack combination. Called by the pre-
    /// check gate in <see cref="GatherAndCommitAttacks"/> per plan section
    /// 3(a): the moment this attacker discovers — on any tick, whether or
    /// not it is itself off cooldown — that it has no living target or that
    /// its target is out of reach, its chain is over. A no-op when no chain
    /// is active, so callers do not need to test
    /// <see cref="AgentState.ComboStepsRemaining"/> first.
    /// </summary>
    private static void ClearActiveComboChain(AgentState attacker)
    {
        if (attacker.ComboStepsRemaining <= 0)
        {
            return;
        }

        attacker.ComboStepsRemaining = 0;
        attacker.ComboTargetEntityId = null;
    }

    private void DecrementCooldowns()
    {
        foreach (var agent in _agentStates)
        {
            if (agent.IsAlive && agent.AttackCooldownRemaining > 0)
            {
                agent.AttackCooldownRemaining--;
            }
        }
    }

    private void SelectTargetsAndIntents()
    {
        ComputeRallyAgents();
        ComputeRallyEngagement();

        // Equipment-relative local context (weapon-relative movement design,
        // section 7) is derived inside this same observation, fused into the
        // candidate loop below, only under a preset that opts in. Both radii
        // are materialized once per call through the section 4.4 arithmetic;
        // the body radius is scenario-wide, so they are identical for every
        // actor.
        var derivesLocalContext = _movementRules.UsesEquipmentRelativeFootwork;

        // The ranged-retreat threat observation (battlefield realism design,
        // section 5.3) is gated on preset identity, matching
        // _nearestMeleeThreatSquared's own sizing in the constructor: V10's
        // registered ruleset is a verbatim copy of V8's, so no
        // MovementRuleset flag distinguishes it from every other preset that
        // derives no local context either.
        var observesNearestMeleeThreat =
            UsesBattlefieldRealism(Scenario.MovementPreset);
        Int128 immediateRadiusSquared = 0;
        Int128 supportRadiusSquared = 0;
        if (derivesLocalContext)
        {
            immediateRadiusSquared = MovementContextQuery.SquaredContextRadius(
                MovementContextQuery.ContextRadiusRaw(
                    Scenario.BodyRadiusRaw,
                    _movementRules.ImmediateRadiusBodyDiametersBasisPoints));
            supportRadiusSquared = MovementContextQuery.SquaredContextRadius(
                MovementContextQuery.ContextRadiusRaw(
                    Scenario.BodyRadiusRaw,
                    _movementRules.SupportRadiusBodyDiametersBasisPoints));
            _factionSurvivingCompositions[0] = default;
            _factionSurvivingCompositions[1] = default;
        }

        for (var agentIndex = 0; agentIndex < _agentStates.Length; agentIndex++)
        {
            var agent = _agentStates[agentIndex];
            if (!agent.IsAlive)
            {
                agent.TargetEntityId = null;
                agent.Intent = AgentIntent.Dead;
                if (derivesLocalContext)
                {
                    // Dead agents count nowhere and carry no context; clear
                    // the row so a stale value from the tick this agent died
                    // on can never be read back.
                    _localMovementContexts[agentIndex] = default;
                }

                if (observesNearestMeleeThreat)
                {
                    // Dead agents observe no threat; clear the row so a stale
                    // value from the tick this agent died on can never be
                    // read back, matching the local-context clear just above.
                    _nearestMeleeThreatSquared[agentIndex] = long.MaxValue;
                }

                continue;
            }

            var contextAccumulator = derivesLocalContext
                ? new MovementContextAccumulator(
                    agent.Loadout, immediateRadiusSquared, supportRadiusSquared)
                : default;
            if (derivesLocalContext)
            {
                _factionSurvivingCompositions[agent.FactionId] =
                    _factionSurvivingCompositions[agent.FactionId]
                        .Add(agent.Loadout);
            }

            AgentState? selectedTarget = null;
            var selectedDistance = long.MaxValue;
            var perceptionSquared = checked(
                (long)agent.PerceptionRangeRaw * agent.PerceptionRangeRaw);

            // Battlefield realism design, section 5.3: the trigger reads the
            // nearest living enemy whose weapon is melee, observed only when
            // the actor's own weapon is ranged (StandoffDistanceRaw != 0 --
            // the same test GatherMovementProposals' V8 standoff arm already
            // uses). Resolved once per actor, not per candidate, since it
            // does not depend on the candidate.
            var nearestMeleeThreatSquared = long.MaxValue;
            var actorObservesMeleeThreats = observesNearestMeleeThreat &&
                ResolveAttackerWeaponProfile(agent.Loadout).StandoffDistanceRaw != 0;

            foreach (var candidate in _agentStates)
            {
                if (!candidate.IsAlive || candidate.FactionId == agent.FactionId)
                {
                    // A living candidate can only land here as a same-faction
                    // one, so this V6-only branch observes exactly the living
                    // allies. Allies never reach the perception test below —
                    // the observation never perceives them at all — so their
                    // deltas are computed here, on the actor's own dime, and
                    // target selection reads nothing this accumulation
                    // writes.
                    if (derivesLocalContext &&
                        candidate.IsAlive &&
                        candidate.EntityId != agent.EntityId)
                    {
                        var allyDeltaX = (long)candidate.XRaw - agent.XRaw;
                        var allyDeltaY = (long)candidate.YRaw - agent.YRaw;
                        contextAccumulator.ObserveAlly(
                            candidate.EntityId,
                            candidate.Loadout,
                            checked(
                                (allyDeltaX * allyDeltaX) +
                                (allyDeltaY * allyDeltaY)));
                    }

                    continue;
                }

                // Cheap axis-aligned rejection before the squared-distance
                // check below: if |dx| already exceeds the (unsquared)
                // perception range, then dx*dx alone exceeds range*range,
                // so dx*dx + dy*dy > range*range necessarily (dy*dy is
                // never negative). Every candidate rejected here would also
                // have been rejected by the perception test that follows,
                // so the surviving candidate set and its scan order are
                // unchanged. Written as two comparisons instead of an
                // absolute value so no negation of deltaX/deltaY is needed.
                var perceptionRangeRaw = (long)agent.PerceptionRangeRaw;
                var deltaX = (long)candidate.XRaw - agent.XRaw;
                if (deltaX > perceptionRangeRaw || deltaX < -perceptionRangeRaw)
                {
                    continue;
                }

                var deltaY = (long)candidate.YRaw - agent.YRaw;
                if (deltaY > perceptionRangeRaw || deltaY < -perceptionRangeRaw)
                {
                    continue;
                }

                var distance = SquaredDistance(agent, candidate);
                if (distance > perceptionSquared)
                {
                    continue;
                }

                // The V6 context hook of design section 7.3: after the
                // perception test passes, before the comparison block, so
                // the already-computed squared distance is reused and only
                // perceivable enemies are ever observed. The comparison
                // block below reads only distance, selectedDistance, and
                // candidate.EntityId, so nothing written here can affect
                // tie-breaking, and target selection stays byte-identical.
                if (derivesLocalContext)
                {
                    contextAccumulator.ObserveEnemy(
                        candidate.EntityId, candidate.Loadout, distance);
                }

                // The ranged-retreat threat fuse of design section 5.3, beside
                // the V6 context fuse above: the same already-computed,
                // already-perception-filtered distance is minimised into
                // nearestMeleeThreatSquared when the candidate's weapon is
                // melee (StandoffDistanceRaw == 0). No new scan, no new
                // distance computation -- distance is exactly the value target
                // selection below also reads. A plain minimum over squared
                // distances needs no EntityId tie-break: the reduction is
                // order-independent regardless of scan order.
                if (actorObservesMeleeThreats &&
                    distance < nearestMeleeThreatSquared &&
                    ResolveAttackerWeaponProfile(candidate.Loadout).StandoffDistanceRaw == 0)
                {
                    nearestMeleeThreatSquared = distance;
                }

                if (distance < selectedDistance ||
                    (distance == selectedDistance &&
                        (selectedTarget is null ||
                            candidate.EntityId < selectedTarget.EntityId)))
                {
                    selectedTarget = candidate;
                    selectedDistance = distance;
                }
            }

            agent.TargetEntityId = selectedTarget?.EntityId;
            if (derivesLocalContext)
            {
                _localMovementContexts[agentIndex] =
                    contextAccumulator.Complete(selectedTarget?.EntityId);
                _localMovementContextDerivations = checked(
                    _localMovementContextDerivations + 1);
            }

            if (observesNearestMeleeThreat)
            {
                _nearestMeleeThreatSquared[agentIndex] = nearestMeleeThreatSquared;
            }

            if (selectedTarget is null)
            {
                agent.Intent = AgentIntent.Idle;
                continue;
            }

            // An agent keeps advancing until its body meets the target's, even
            // once the target is already inside reach. Attacking is reserved
            // for an agent that has arrived. One that strikes while still
            // closing is re-marked Attacking by attack gathering, so a
            // spectator still sees it fighting.
            agent.Intent = selectedDistance <= CollisionGeometry
                .ContactSquaredDistance(Scenario.BodyRadiusRaw)
                ? AgentIntent.Attacking
                : AgentIntent.Moving;

            // Regrouping only overrides an intent that would otherwise be
            // Moving: Attacking beats Regrouping (the same-tick conflict
            // rule), and the rally agent itself is exempt and keeps its
            // ordinary nearest-enemy intent.
            if (Scenario.LastStandThresholdAgents > 0 &&
                agent.Intent == AgentIntent.Moving &&
                _factionLivingCounts[agent.FactionId] <=
                    Scenario.LastStandThresholdAgents &&
                agent.EntityId != _factionRallyEntityIds[agent.FactionId] &&
                !YieldsRegroupingToEngagement(agent, selectedDistance))
            {
                agent.Intent = AgentIntent.Regrouping;
            }
        }
    }

    /// <summary>
    /// One forward scan over the agent array, computing the living count and
    /// the lowest living <see cref="AgentState.EntityId"/> per faction. The
    /// comparison is against <see cref="AgentState.EntityId"/> explicitly, so
    /// the result does not depend on the incidental order of
    /// <see cref="_agentStates"/>. Runs before any intent is assigned, so no
    /// warrior's intent can depend on scan order either.
    /// </summary>
    /// <summary>
    /// Whether a follower that would otherwise be marked
    /// <see cref="AgentIntent.Regrouping"/> yields and keeps pursuing its own
    /// enemy instead, under either of the two conditions
    /// <see cref="MovementPresetId.LastStandEngagementV11"/> introduces: its
    /// faction's rally agent is itself engaged, or the follower's own selected
    /// enemy is already inside the follower's own weapon reach. Always
    /// <see langword="false"/> under every earlier preset, whose last-stand
    /// behaviour is therefore byte-identical to what it was before this
    /// method existed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The yield deliberately does no destination work of its own. Leaving the
    /// intent at <see cref="AgentIntent.Moving"/> routes the follower down the
    /// ordinary pursuit path, which already aims it at the target selection
    /// resolved for it a few lines above — nearest first, ties broken on
    /// <see cref="AgentState.EntityId"/> — so no second scan is written and no
    /// new ordering rule enters the tick. The standoff, cohesion, and
    /// sidestep behaviours that path carries keep applying to a follower that
    /// is now fighting rather than gathering.
    /// </para>
    /// <para>
    /// Both conditions test weapon reach rather than body contact. The
    /// existing override already yields at body contact by construction —
    /// <see cref="AgentIntent.Attacking"/> is assigned at two body radii a few
    /// lines above and beats <see cref="AgentIntent.Regrouping"/> — and it is
    /// precisely the band between reach and contact in which a warrior who can
    /// already strike is dragged away from the enemy it could strike.
    /// </para>
    /// </remarks>
    private bool YieldsRegroupingToEngagement(
        AgentState agent,
        long selectedDistance) =>
        YieldsLastStandEngagement(Scenario.MovementPreset) &&
        (_factionRallyEngaged[agent.FactionId] ||
            IsWithinAttackRange(agent, selectedDistance));

    /// <summary>
    /// The preset gate for both last-stand regroup yields, named once for the
    /// same reason <see cref="UsesBattlefieldRealism"/> is: the last-stand code
    /// it guards is shared and unversioned, so the set of presets that opt into
    /// the new behaviour is stated in one place rather than at each call site.
    /// <see cref="MovementPresetId.ContingentShapeV12"/> is admitted for the
    /// same reason <see cref="UsesBattlefieldRealism"/> admits it: it is
    /// defined as a strict superset of
    /// <see cref="MovementPresetId.LastStandEngagementV11"/>'s behaviour, so
    /// it must keep V11's last-stand regroup yield unchanged before any
    /// contingent-shaping behaviour of its own is layered on top.
    /// <see cref="MovementPresetId.CohortLateralSpreadV13"/> is admitted for
    /// the same reason: it is a strict superset of V11's behaviour too, so
    /// it must keep the same last-stand regroup yield unchanged before its
    /// own lateral-riffle cohort traversal is layered on top.
    /// <see cref="MovementPresetId.ContingentCohesionBeforeContactV14"/> is
    /// admitted for the same reason again: it is a strict superset of
    /// <see cref="MovementPresetId.CohortLateralSpreadV13"/>'s behaviour, so it
    /// must keep the same last-stand regroup yield unchanged before its own
    /// contingent-cohesion gate is layered on top.
    /// </summary>
    private static bool YieldsLastStandEngagement(MovementPresetId preset) =>
        preset is MovementPresetId.LastStandEngagementV11
            or MovementPresetId.ContingentShapeV12
            or MovementPresetId.CohortLateralSpreadV13
            or MovementPresetId.ContingentCohesionBeforeContactV14;

    /// <summary>
    /// Derives <see cref="_factionRallyEngaged"/> for both factions: whether
    /// each faction's rally agent has a living enemy inside its own weapon
    /// reach. Runs immediately after <see cref="ComputeRallyAgents"/> and
    /// before any intent is assigned, so no follower's intent can depend on
    /// where its own rally agent happens to sit in the agent array.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reduction is existential — does any qualifying enemy exist — which
    /// is order-independent by construction and needs no tie-break: which
    /// enemy satisfies it cannot change the answer, so the scan may stop at
    /// the first one it finds.
    /// </para>
    /// <para>
    /// The perception filter matches the one target selection applies, so this
    /// answers exactly "is the rally agent's own selected target within its
    /// reach" rather than a slightly different question about an enemy the
    /// rally agent cannot see. It costs nothing under any preset that does not
    /// opt in, and nothing in a battle with no faction at or below the
    /// last-stand threshold, because both are rejected before the scan.
    /// </para>
    /// </remarks>
    private void ComputeRallyEngagement()
    {
        _factionRallyEngaged[0] = false;
        _factionRallyEngaged[1] = false;

        if (Scenario.LastStandThresholdAgents <= 0 ||
            !YieldsLastStandEngagement(Scenario.MovementPreset))
        {
            return;
        }

        for (var faction = 0; faction < 2; faction++)
        {
            if (_factionLivingCounts[faction] >
                    Scenario.LastStandThresholdAgents ||
                _factionRallyEntityIds[faction] == 0 ||
                !_agentIndexes.TryGetValue(
                    _factionRallyEntityIds[faction], out var rallyIndex))
            {
                continue;
            }

            var rallyAgent = _agentStates[rallyIndex];
            var perceptionSquared = checked(
                (long)rallyAgent.PerceptionRangeRaw * rallyAgent.PerceptionRangeRaw);

            foreach (var candidate in _agentStates)
            {
                if (!candidate.IsAlive || candidate.FactionId == faction)
                {
                    continue;
                }

                var distance = SquaredDistance(rallyAgent, candidate);
                if (distance <= perceptionSquared &&
                    IsWithinAttackRange(rallyAgent, distance))
                {
                    _factionRallyEngaged[faction] = true;
                    break;
                }
            }
        }
    }

    private void ComputeRallyAgents()
    {
        _factionLivingCounts[0] = 0;
        _factionLivingCounts[1] = 0;
        _factionRallyEntityIds[0] = 0;
        _factionRallyEntityIds[1] = 0;

        foreach (var candidate in _agentStates)
        {
            if (!candidate.IsAlive)
            {
                continue;
            }

            var faction = candidate.FactionId;
            _factionLivingCounts[faction]++;

            if (_factionRallyEntityIds[faction] == 0 ||
                candidate.EntityId < _factionRallyEntityIds[faction])
            {
                _factionRallyEntityIds[faction] = candidate.EntityId;
            }
        }
    }

    /// <summary>
    /// The ninth tick stage: resolves every living contingent's
    /// <see cref="AgentState.ContingentState"/> for this tick under every
    /// persistent-contingent preset, and the two
    /// geometric gates <see cref="GatherMovementProposals"/>'s cohesion
    /// branch reads as array lookups rather than recomputing. Under
    /// <see cref="MovementPresetId.IndependentPursuitV1"/> this returns on
    /// its first line and touches no contingent array at all, which is why
    /// the frozen preset's trajectory is untouched by this stage's
    /// existence. See
    /// the formation and movement realism design
    /// sections 3.4 and 3.5.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two forward passes, in that order, because the second cannot start
    /// until the first has finished: pass one is
    /// <see cref="MovementRules.ScanContingentLeadersAndLivingCounts"/>,
    /// which every later step needs a leader entity ID from; pass two walks
    /// every living agent once more to accumulate each contingent's
    /// <c>spreadSquared</c>, which needs this tick's leader to already be
    /// known, and its <c>contactCount</c> — a plain count of how many living
    /// members have a selected target within the close radius, not a
    /// minimum over any distance.
    /// </para>
    /// <para>
    /// The two geometric gates are computed once per contingent per tick,
    /// never once per agent: gate 5 (the map-edge open-ground test) in a
    /// single pass over the sixteen slots, and gate 6 (the cross-contingent
    /// overlap test) in a pairwise scan restricted to living same-faction
    /// slots, outer index ascending and inner index ascending from
    /// <c>outer + 1</c>, at most <c>C(8, 2) = 28</c> pairs per faction and 56
    /// in total. A slot with no living member is excluded from the pairwise
    /// scan entirely — its leader, trail base and margin are stale values
    /// from whichever tick it last had a living member, and comparing
    /// against them would deny cohesion on the strength of a square that no
    /// longer exists. Because
    /// <see cref="FormationRules.DoCohesionSquaresOverlap"/> is symmetric,
    /// both contingents of an overlapping pair are flagged together; there is
    /// no tie-break and none is needed.
    /// </para>
    /// <para>
    /// Either geometric denial forces <see cref="ContingentState.Advance"/>
    /// rather than <see cref="ContingentState.Hold"/>, the same way a shut
    /// duty-cycle window does, so the inspector never reports a contingent as
    /// <see cref="ContingentState.Hold"/> while its members are in fact
    /// pursuing independently. Gates 5 and 6 can therefore only ever remove a
    /// cohesion destination from a contingent that would otherwise have
    /// received one; they can never grant one.
    /// </para>
    /// <para>
    /// Every array this method writes is preallocated at construction and
    /// sized to <see cref="ContingentSlotCount"/>, so a warm tick allocates
    /// nothing.
    /// </para>
    /// </remarks>
    private void ResolveContingentStates()
    {
        if (Scenario.MovementPreset == MovementPresetId.IndependentPursuitV1)
        {
            return;
        }

        MovementRules.ScanContingentLeadersAndLivingCounts(
            _agentStates,
            _contingentLeaderEntityIds,
            _contingentLivingCounts,
            _movementRules.SelectsLeaderByRank);

        for (var slot = 0; slot < ContingentSlotCount; slot++)
        {
            _contingentSpreadSquared[slot] = 0;
            _contingentContactCounts[slot] = 0;
        }

        // closeRadiusSquared does not vary by slot, so it is derived once
        // here rather than per slot, in the same checked long arithmetic the
        // per-slot cohesionRadiusRaw derivation below already uses.
        var closeRadiusRaw = checked(
            (long)_movementRules.CloseRadiusMultiplier * Scenario.BodyRadiusRaw);
        var closeRadiusSquared = checked(closeRadiusRaw * closeRadiusRaw);

        // Pass two: for every living agent, fold its squared distance to its
        // own contingent's leader into that slot's spread (skipping the
        // leader itself, which is always zero and never the farthest
        // non-leader member), and increment that slot's contact count when
        // its squared distance to its own selected target, if any, is at or
        // under closeRadiusSquared.
        for (var index = 0; index < _agentStates.Length; index++)
        {
            var agent = _agentStates[index];
            if (!agent.IsAlive)
            {
                continue;
            }

            var slot = checked(
                (agent.FactionId * FormationPlanner.MaximumContingents) +
                agent.ContingentId);
            var leaderEntityId = _contingentLeaderEntityIds[slot];

            if (leaderEntityId != 0 &&
                agent.EntityId != leaderEntityId &&
                _agentIndexes.TryGetValue(leaderEntityId, out var leaderIndex))
            {
                var memberSquared = SquaredDistance(agent, _agentStates[leaderIndex]);
                if (memberSquared > _contingentSpreadSquared[slot])
                {
                    _contingentSpreadSquared[slot] = memberSquared;
                }
            }

            if (agent.TargetEntityId is { } targetId &&
                _agentIndexes.TryGetValue(targetId, out var targetIndex))
            {
                var distanceSquared = SquaredDistance(agent, _agentStates[targetIndex]);
                if (distanceSquared <= closeRadiusSquared)
                {
                    _contingentContactCounts[slot]++;
                }
            }
        }

        // Gate 5 (map-edge) and the trail-base/jitter/margin geometry gate 6
        // needs, once per living contingent.
        var mapWidthRaw = checked(Scenario.MapWidth * FixedPoint.Scale);
        var mapHeightRaw = checked(Scenario.MapHeight * FixedPoint.Scale);

        for (var slot = 0; slot < ContingentSlotCount; slot++)
        {
            _contingentSquareOverlapsAnother[slot] = false;

            if (_contingentLivingCounts[slot] == 0)
            {
                _contingentSquareFitsMap[slot] = false;
                continue;
            }

            var leader = _agentStates[_agentIndexes[_contingentLeaderEntityIds[slot]]];
            var jitterRaw = FormationRules.ComputeContingentJitterRaw(
                Scenario.BodyRadiusRaw,
                _contingentLivingCounts[slot]);
            var trailRaw = FormationRules.ComputeContingentTrailRaw(
                Scenario.BodyRadiusRaw,
                jitterRaw);
            var direction = ComputeRallyDirection(leader);
            var (trailBaseXRaw, trailBaseYRaw) = ComputeRallyTrailBase(
                leader,
                direction,
                trailRaw);

            _contingentJitterRaw[slot] = jitterRaw;
            _contingentTrailBaseXRaw[slot] = trailBaseXRaw;
            _contingentTrailBaseYRaw[slot] = trailBaseYRaw;

            // The half-side of the square this contingent claims. Without the
            // gate it is the unscaled packing margin -- the jitter radius plus
            // one body radius -- which is what every preset up to V13 claims.
            // With the gate it is a basis-point fraction of that margin, so a
            // contingent claims less ground than the packing bound alone would
            // give it and Hold stays reachable under a crowded deployment. The
            // arithmetic is a long multiply followed by an integer divide, so a
            // registered UnscaledCohesionSquareMarginBasisPoints reproduces the
            // unscaled margin exactly rather than approximately.
            //
            // Only the claimed margin is scaled. _contingentJitterRaw above is
            // left alone deliberately: it feeds the per-member offset in
            // ResolveContingentDestinations, and scaling it would change how
            // far apart members stand, which this change is expressly not
            // allowed to do.
            var unscaledMarginRaw = checked(jitterRaw + Scenario.BodyRadiusRaw);
            var marginRaw = _movementRules.GathersContingentsBeforeContact
                ? (int)((long)unscaledMarginRaw *
                        _movementRules.CohesionSquareMarginBasisPoints /
                    MovementRuleset.UnscaledCohesionSquareMarginBasisPoints)
                : unscaledMarginRaw;

            _contingentMarginRaw[slot] = marginRaw;

            // Gate 5 is handed the same half-side gate 6 reads out of
            // _contingentMarginRaw below, so the map-edge test and the
            // cross-contingent test cannot disagree about the square's size.
            _contingentSquareFitsMap[slot] =
                FormationRules.IsCohesionSquareWithinBoundsForMargin(
                    trailBaseXRaw,
                    trailBaseYRaw,
                    marginRaw,
                    Scenario.BodyRadiusRaw,
                    mapWidthRaw,
                    mapHeightRaw);
        }

        // A slot the narrowed scan excludes is also denied outright, and the
        // excluded set is exactly two states wide: a tick-start state of Close
        // or Break, and nothing else. MovementRules
        // .ParticipatesInCrossContingentScan tests those two values and no
        // other condition, so this loop is not a blanket denial that happens
        // to catch the two states -- it marks Close and Break slots, and there
        // is no third reason a living slot is ever marked here. Such a slot is
        // never tested against anyone, so granting it a cohesion destination
        // would park aim points inside a square no pair ever measured, which
        // is exactly the combined-density statement gate 6 exists to hold.
        // Marking it costs its neighbours nothing: a square absent from the
        // pairwise loop below can only ever produce fewer overlap findings for
        // its neighbours, never more. The denial answers for the excluded
        // slot's own square alone.
        //
        // The denial resolves it to Advance through transition rule 4 -- rules 1
        // and 3 still win first, so a Break stays Break and a latched Close
        // stays Close -- and an Advance takes part in the scan normally on the
        // next tick. Under a preset that does not narrow the scan this loop
        // marks nothing.
        for (var slot = 0; slot < ContingentSlotCount; slot++)
        {
            if (_contingentLivingCounts[slot] != 0 &&
                !TakesPartInCrossContingentScan(slot))
            {
                _contingentSquareOverlapsAnother[slot] = true;
            }
        }

        // Gate 6: pairwise same-faction overlap, restricted to living slots,
        // outer index ascending and inner index ascending from outer + 1.
        // Under a preset that narrows the scan, a living slot whose tick-start
        // state is Close or Break takes no part on either side of the pair.
        for (var faction = 0; faction < 2; faction++)
        {
            var baseSlot = faction * FormationPlanner.MaximumContingents;

            for (var outer = 0; outer < FormationPlanner.MaximumContingents; outer++)
            {
                var outerSlot = baseSlot + outer;
                if (_contingentLivingCounts[outerSlot] == 0 ||
                    !TakesPartInCrossContingentScan(outerSlot))
                {
                    continue;
                }

                for (var inner = outer + 1;
                    inner < FormationPlanner.MaximumContingents;
                    inner++)
                {
                    var innerSlot = baseSlot + inner;
                    if (_contingentLivingCounts[innerSlot] == 0 ||
                        !TakesPartInCrossContingentScan(innerSlot))
                    {
                        continue;
                    }

                    if (!FormationRules.DoCohesionSquaresOverlap(
                        _contingentTrailBaseXRaw[outerSlot],
                        _contingentTrailBaseYRaw[outerSlot],
                        _contingentMarginRaw[outerSlot],
                        _contingentTrailBaseXRaw[innerSlot],
                        _contingentTrailBaseYRaw[innerSlot],
                        _contingentMarginRaw[innerSlot]))
                    {
                        continue;
                    }

                    _contingentSquareOverlapsAnother[outerSlot] = true;
                    _contingentSquareOverlapsAnother[innerSlot] = true;
                }
            }
        }

        // The six priority-ordered transition rules, per living slot. The
        // previous state is read from this slot's own current leader before
        // this loop overwrites it, because the leader is, by construction, a
        // living member and therefore carries the value forward.
        var tick = checked((int)Tick);

        for (var slot = 0; slot < ContingentSlotCount; slot++)
        {
            if (_contingentLivingCounts[slot] == 0)
            {
                continue;
            }

            var leader = _agentStates[_agentIndexes[_contingentLeaderEntityIds[slot]]];
            var previousState = leader.ContingentState;
            var windowOpen = MovementRules.IsCohesionWindowOpen(
                tick,
                slot,
                _movementRules.CohesionCycleTicks,
                _movementRules.CohesionDutyTicks);
            var geometricGatesPass =
                _contingentSquareFitsMap[slot] && !_contingentSquareOverlapsAnother[slot];
            var cohesionRadiusRaw = checked(
                (long)_movementRules.CohesionRadiusMultiplier * Scenario.BodyRadiusRaw);

            _contingentResolvedStates[slot] = MovementRules.ResolveContingentState(
                previousState,
                _contingentLivingCounts[slot],
                _contingentInitialCounts[slot],
                _contingentSpreadSquared[slot],
                _contingentContactCounts[slot],
                cohesionRadiusRaw,
                _movementRules.CloseFractionNumerator,
                _movementRules.CloseFractionDenominator,
                _movementRules.MinimumCohesiveMembers,
                windowOpen,
                geometricGatesPass);
        }

        // Write each slot's resolved state onto every one of its living
        // members. The authoritative store is per agent; there is no
        // parallel per-contingent array the state hash cannot see.
        foreach (var agent in _agentStates)
        {
            if (!agent.IsAlive)
            {
                continue;
            }

            var slot = checked(
                (agent.FactionId * FormationPlanner.MaximumContingents) +
                agent.ContingentId);
            agent.ContingentState = _contingentResolvedStates[slot];
        }
    }

    /// <summary>
    /// Whether the living contingent in <paramref name="slot"/> takes part in
    /// movement gate 6's cross-contingent scan this tick. Always true under a
    /// preset that does not narrow the scan, so the two frozen
    /// persistent-contingent presets keep the behaviour their own recorded
    /// expectations pin; under a narrowing preset, a contingent whose
    /// tick-start state is Close or Break is skipped, because it can park no
    /// cohesion aim point.
    /// </summary>
    /// <remarks>
    /// The caller has already established that this slot has a living member,
    /// so <c>_contingentLeaderEntityIds[slot]</c> is a real entity id and the
    /// two lookups below cannot miss. The state read is the leader's, which
    /// still carries the previous tick's value: the loop that overwrites every
    /// living member's <c>ContingentState</c> runs after the gate that calls
    /// this.
    /// </remarks>
    /// <param name="slot"><c>FactionId * MaximumContingents + ContingentId</c>.</param>
    /// <returns>
    /// <see langword="true"/> when this contingent's bias square participates.
    /// </returns>
    private bool TakesPartInCrossContingentScan(int slot)
    {
        if (!_movementRules.NarrowsCohesionScanToCohesionCapableContingents)
        {
            return true;
        }

        var leader = _agentStates[_agentIndexes[_contingentLeaderEntityIds[slot]]];
        return MovementRules.ParticipatesInCrossContingentScan(leader.ContingentState);
    }

    /// <summary>
    /// Reads tick-start state only. Nothing is committed here, so no agent can
    /// see another agent's move while proposals are still being formed.
    /// </summary>
    /// <remarks>
    /// Under every persistent-contingent preset, a
    /// <see cref="AgentIntent.Moving"/> agent that passes all six movement
    /// gates of design section 3.5
    /// (the formation and movement realism design)
    /// takes a
    /// contingent cohesion destination instead of its ordinary nearest-enemy
    /// pursuit; every agent that fails even one of the six gates takes
    /// ordinary pursuit exactly as it does under
    /// <see cref="MovementPresetId.IndependentPursuitV1"/>. The same-tick
    /// conflict order is unchanged: <c>Dead &gt; Attacking &gt; Regrouping
    /// &gt; contingent cohesion &gt; ordinary pursuit</c>, so this branch is
    /// only ever reached for an agent whose <see cref="AgentIntent"/> is
    /// already <see cref="AgentIntent.Moving"/>.
    /// </remarks>
    private void GatherMovementProposals()
    {
        if (_movementRules.UsesEquipmentRelativeFootwork)
        {
            // The equipment-relative route pipeline (design sections 10 and
            // 11) replaces this whole stage under the opt-in flag; every
            // legacy preset continues below untouched.
            GatherEquipmentRelativeMovementProposals();
            return;
        }

        Array.Clear(_movementProposals);

        // Every persistent-contingent preset takes the cohesion branch. The
        // test is written against IndependentPursuitV1, the one preset that
        // has no contingent behaviour, rather than against a list of the
        // presets that do: ResolveContingentStates returns on its first line
        // under exactly the same condition, and a new persistent-contingent
        // preset must not silently lose cohesion by not being named here.
        var cohesionActive =
            Scenario.MovementPreset != MovementPresetId.IndependentPursuitV1;
        var tick = checked((int)Tick);

        for (var index = 0; index < _agentStates.Length; index++)
        {
            var agent = _agentStates[index];
            if (!agent.IsAlive)
            {
                continue;
            }

            if (agent.Intent == AgentIntent.Moving &&
                agent.TargetEntityId is { } enemyTargetId)
            {
                var target = _agentStates[_agentIndexes[enemyTargetId]];

                // Ranged standoff. Confined to RangedStandoffV8 and
                // BattlefieldRealismV10 — battlefield realism design section
                // 5.2 widens the preset equality test from a single value to
                // this two-value predicate so V10 inherits the standoff hold
                // unchanged, and adds its own retreat rung beneath it — so
                // every other registered preset — PersistentContingentsV4
                // included — still takes the unmodified path below regardless
                // of which combat preset supplied the roster. A melee
                // weapon's StandoffDistanceRaw is always 0, so a melee-only
                // roster under V8 or V10 falls through this block untouched
                // and is byte-identical to V4. Checked ahead of contingent
                // cohesion below: cohesion's aim-point pursuit would
                // otherwise close a held ranged warrior straight onto its
                // target's body-contact ring, since that branch runs for every
                // preset other than IndependentPursuitV1 and never consults
                // StandoffDistanceRaw.
                if (Scenario.MovementPreset == MovementPresetId.RangedStandoffV8 ||
                    UsesBattlefieldRealism(Scenario.MovementPreset))
                {
                    var standoffRaw =
                        ResolveAttackerWeaponProfile(agent.Loadout).StandoffDistanceRaw;
                    if (standoffRaw != 0)
                    {
                        // Battlefield realism design section 5.2, rung 1: a
                        // melee enemy inside the threat radius backs the
                        // shooter directly away, ahead of the V8 hold rung
                        // below. _nearestMeleeThreatSquared is populated only
                        // under V10 (task 7), so RangedStandoffV8 never takes
                        // this branch — its scratch row is zero-length and
                        // this whole arm is gated on preset identity again,
                        // not merely on the array being readable.
                        if (UsesBattlefieldRealism(Scenario.MovementPreset))
                        {
                            var nearestMeleeThreatSquared =
                                _nearestMeleeThreatSquared[index];
                            var threatRadiusRaw =
                                RangedRetreatRules.ThreatRadiusRaw(standoffRaw);
                            if (RangedRetreatRules.IsThreatened(
                                nearestMeleeThreatSquared, threatRadiusRaw))
                            {
                                var threat = FindNearestMeleeThreatPosition(agent);
                                if (TryBuildRetreatProposal(
                                        agent,
                                        threat.XRaw,
                                        threat.YRaw,
                                        threat.EntityId,
                                        out var retreatProposal))
                                {
                                    // Design section 5.5, hazard two: the
                                    // stall generation is never consulted on
                                    // this rung. A blocked retreat simply
                                    // fails to move this tick instead of
                                    // starting a blocked streak that would
                                    // route it into the sidestepping-pursuit
                                    // branch below — straight back toward the
                                    // very thing it is running from.
                                    agent.Intent = AgentIntent.BackingAway;
                                    _movementProposals[index] = retreatProposal;
                                }
                                else
                                {
                                    // Design section 5.5, hazard one: the
                                    // reflected destination clamped to the
                                    // map bounds on at least one axis, so the
                                    // shooter cannot actually step back. No
                                    // proposal is written —
                                    // _movementProposals[index] is already
                                    // null from the Array.Clear above — and
                                    // the cornered warrior stands and fights
                                    // instead of reading a "backing away"
                                    // intent that would be a lie the
                                    // spectator can see.
                                    agent.Intent = AgentIntent.Holding;
                                }

                                continue;
                            }
                        }

                        if (SquaredDistance(agent, target) <=
                            checked((long)standoffRaw * standoffRaw))
                        {
                            // At or inside the weapon's standoff distance: the
                            // warrior deliberately holds rather than closing to
                            // body contact. No proposal is written —
                            // _movementProposals[index] is already null from the
                            // Array.Clear above — so the collision stage
                            // resolves it to MovementResolution.None rather than
                            // Blocked, and the blocked streak never starts.
                            agent.Intent = AgentIntent.Holding;
                            continue;
                        }

                        var rangedStallGeneration = _collision.StallGeneration(index);
                        _movementProposals[index] = rangedStallGeneration == 0
                            ? BuildMovementProposal(
                                agent,
                                target.XRaw,
                                target.YRaw,
                                target.EntityId,
                                standoffRaw)
                            : BuildSidesteppingPursuitProposal(
                                agent,
                                target,
                                rangedStallGeneration,
                                standoffRaw);
                        continue;
                    }
                }

                if (cohesionActive &&
                    TryResolveContingentCohesionAimPoint(
                        agent,
                        tick,
                        out var aimXRaw,
                        out var aimYRaw,
                        out var leaderEntityId))
                {
                    var squaredDistanceToAim = CollisionGeometry.SquaredDistance(
                        agent.XRaw,
                        agent.YRaw,
                        aimXRaw,
                        aimYRaw);
                    if (squaredDistanceToAim <=
                        CollisionGeometry.ContactSquaredDistance(Scenario.BodyRadiusRaw))
                    {
                        // The arrived-guard: the same one BuildRegroupingProposal
                        // applies. _movementProposals[index] is already null from
                        // the Array.Clear above.
                        continue;
                    }

                    _movementProposals[index] = BuildMovementProposal(
                        agent,
                        aimXRaw,
                        aimYRaw,
                        leaderEntityId);
                    continue;
                }

                // The pursuit-path stall escape. At generation 0 — every agent
                // in every battle that is merely crowded — this is the same
                // call it has always been, reached by the same path, so the aim
                // point is unchanged rather than recomputed to the same value.
                var stallGeneration = _collision.StallGeneration(index);
                if (stallGeneration == 0)
                {
                    _movementProposals[index] = BuildMovementProposal(agent, target);
                    continue;
                }

                _movementProposals[index] = BuildSidesteppingPursuitProposal(
                    agent,
                    target,
                    stallGeneration);
                continue;
            }

            if (agent.Intent == AgentIntent.Regrouping)
            {
                _movementProposals[index] = BuildRegroupingProposal(agent, index);
            }
        }
    }

    /// <summary>
    /// The equipment-relative posture-and-provisional-footwork stage of the
    /// weapon-relative movement design, sections 8 and 9, inserted between
    /// <see cref="ResolveContingentStates"/> and
    /// <see cref="GatherMovementProposals"/> per design section 11.2 and
    /// reached only under a preset whose
    /// <see cref="MovementRuleset.UsesEquipmentRelativeFootwork"/> is
    /// <see langword="true"/>. Postures resolve once per contingent from the
    /// global living totals, this tick's already-resolved
    /// <see cref="ContingentState"/>, and the two factions' role coverage,
    /// and are written to every living member. The provisional phase and
    /// timer stay in scratch: design section 9.4 commits them exactly once,
    /// after route generation and lane clearance finalise them.
    /// </summary>
    /// <remarks>
    /// This is also the one place the V7 pressure interrupt is evaluated, under
    /// <see cref="MovementRuleset.AppliesPressureInterrupt"/> and therefore
    /// never under any preset from V1 through V6. It is the only stage that
    /// both charges the interrupt's cost and feeds its answer into the footwork
    /// ladder, which is why the answer is computed once, here, rather than
    /// twice — see the V7 design document, sections 3 and 4.4.
    /// </remarks>
    private void ResolveEquipmentPosturesAndProvisionalFootwork()
    {
        for (var slot = 0; slot < ContingentSlotCount; slot++)
        {
            if (_contingentLivingCounts[slot] == 0)
            {
                _contingentPostures[slot] = TacticalPosture.None;
                continue;
            }

            var factionId = slot / FormationPlanner.MaximumContingents;
            _contingentPostures[slot] = WeaponMovementRules.ResolveTacticalPosture(
                _factionLivingCounts[factionId],
                _factionLivingCounts[1 - factionId],
                _contingentResolvedStates[slot],
                _factionSurvivingCompositions[factionId].RoleCoverage,
                _factionSurvivingCompositions[1 - factionId].RoleCoverage);
        }

        var appliesPressureInterrupt = _movementRules.AppliesPressureInterrupt;
        for (var index = 0; index < _agentStates.Length; index++)
        {
            var agent = _agentStates[index];
            if (!agent.IsAlive)
            {
                _provisionalFootworkPhases[index] = FootworkPhase.None;
                _provisionalFootworkTicks[index] = 0;
                if (appliesPressureInterrupt)
                {
                    // Both slots are cleared rather than left stale, but
                    // neither the predicate nor ComputeWeightedPressure is
                    // called here: neither of them guards any of its own
                    // arguments, and both divide by counts that only a living
                    // agent is guaranteed to carry.
                    _pressureInterruptFired[index] = false;
                    _pressureBasisPoints[index] = 0;
                }

                continue;
            }

            var slot = checked(
                (agent.FactionId * FormationPlanner.MaximumContingents) +
                agent.ContingentId);
            agent.TacticalPosture = _contingentPostures[slot];

            var profile = _movementRules.ResolveLoadoutProfile(agent.Loadout);
            var context = _localMovementContexts[index];
            var hasTarget = false;
            var targetAtOrInsidePreferredDistance = false;
            if (agent.TargetEntityId is { } targetId &&
                _agentIndexes.TryGetValue(targetId, out var targetIndex))
            {
                var target = _agentStates[targetIndex];
                hasTarget = true;

                // Design section 9.1, step 8: the offset-adjusted preferred
                // distance, compared inclusively on squared values. The
                // target's loadout is read from authoritative state here, at
                // the moment the offset is applied, so no second copy of the
                // target's identity is cached anywhere.
                var preferredRaw =
                    MovementRouteRules.EffectivePreferredDistanceRaw(
                        agent.AttackRangeRaw,
                        profile,
                        MovementRouteRules.CanonicalOpponentIndex(
                            target.Loadout));
                targetAtOrInsidePreferredDistance =
                    (Int128)SquaredDistance(agent, target) <=
                    checked((Int128)preferredRaw * preferredRaw);
            }

            if (appliesPressureInterrupt)
            {
                // Design section 4, the pressure interrupt: one evaluation per
                // living agent per tick, on tick-start authoritative state plus
                // the scratch SelectTargetsAndIntents finalised before this
                // loop began. No agent's answer reads another agent's answer,
                // so the loop order cannot decide an outcome and the no-peeking
                // invariant GatherEquipmentRelativeMovementProposals documents
                // is preserved. No random stream is consulted.
                //
                // ComputeWeightedPressure guards no argument of its own: it
                // divides by supportAllies, by maximumHitPoints, and — when
                // that count is non-zero — by priorSupportAllies.
                // LocalMovementContext's ally count includes the actor itself
                // and Scenario.Validate proves MaximumHitPoints is at least 1,
                // so both divisors are non-zero by construction for a living
                // agent. That is why the call sits in the living-agent path
                // only, and why the dead branch above clears the slot instead
                // of asking.
                //
                // Design section 4.2 requires the weighted sum to be computed
                // exactly once per agent per tick, and this is that one
                // computation. The value it produces feeds both consumers
                // below: the spectator's pressure row and the interrupt
                // predicate, whose single answer the charged cost and the
                // footwork ladder then share. Three long divisions and six
                // multiplications are not worth running twice in a stage the
                // tick budget is measured against.
                long weightedPressure =
                    WeaponMovementRules.ComputeWeightedPressure(
                        context.SupportAllies,
                        context.SupportEnemies,
                        agent.PriorSupportAllies,
                        agent.DamageTakenLastTick,
                        agent.MaximumHitPoints,
                        _movementRules.SupportPressureWeightBasisPoints,
                        _movementRules.IncomingDamageWeightBasisPoints,
                        _movementRules.AllyCollapseWeightBasisPoints);

                // The spectator's pressure row, design section 3, question 8,
                // channel 3. The predicate below answers whether the interrupt
                // fires, which its two guards can decide without ever weighing
                // the signals; the inspector needs the weighed number itself,
                // on every tick, whatever phase the warrior is in. Those are
                // two different questions asked of the one sum above, which is
                // why this write sits outside the predicate and runs for every
                // living agent under the interrupt preset, including one whose
                // guards reject the interrupt.
                //
                // Dividing by the ratio scale converts the weighted sum back
                // into the basis-point unit the profile's threshold is
                // registered in, so the inspector can print the two against
                // each other. The quotient is at most SignalCeilingBasisPoints,
                // because the three weights sum to one whole unit under a
                // preset that applies the interrupt, so the int cast cannot
                // truncate.
                _pressureBasisPoints[index] = (int)(
                    weightedPressure
                    / WeaponMovementRules.RatioBasisPointScale);

                // What is left of the predicate is its two guards and one
                // comparison against this row's own threshold; the number it
                // weighs is the sum above rather than a second copy of it.
                _pressureInterruptFired[index] =
                    WeaponMovementRules.ShouldPressureInterrupt(
                        agent.FootworkPhase,
                        profile.PressureInterruptThresholdBasisPoints,
                        weightedPressure);

                if (_pressureInterruptFired[index])
                {
                    // The cost, design section 4.4. This is the second
                    // non-decrement writer of AgentState.AttackCooldownRemaining;
                    // the other is ResolveComboTransition, which writes it from
                    // the combat stage. That is a deliberate, documented
                    // inversion of the invariant
                    // ApplyEquipmentAttackFootworkAndDeathCleanup's summary
                    // records: combat still reads nothing from movement, and
                    // the post-attack footwork pass still never blocks an
                    // attack the combat gates accepted. What changes is the
                    // reverse direction — the movement stage writing a field
                    // the combat stage owns — and the write is unreachable
                    // under every preset from V1 through V6, because the gate
                    // above is false for all of them.
                    //
                    // AttackCooldownTicks rather than the weapon profile's
                    // value, matching the reasoning ResolveComboTransition's
                    // remarks already record: the two are bit-identical for
                    // every agent CreateAgent produces, and the cached field is
                    // the one every other tick stage reads.
                    //
                    // The timing is exact. DecrementCooldowns runs before this
                    // stage, so the value written here is not decremented on
                    // the tick it is written: the first decrement lands at the
                    // start of the next tick and the attack gate reopens
                    // exactly AttackCooldownTicks ticks later. On this tick
                    // GatherAndCommitAttacks sees a non-zero cooldown and the
                    // warrior lands nothing. That is the cost, and it is the
                    // observable point of the whole feature.
                    agent.AttackCooldownRemaining = agent.AttackCooldownTicks;

                    // The chain is cleared, not preserved, and this is a
                    // decision rather than an oversight: a warrior whose
                    // cooldown was just reset to the full normal value is by
                    // definition not continuing a chain, and leaving
                    // ComboStepsRemaining above zero would let the next blow
                    // claim a chain position across the interruption — an event
                    // field reporting a continuity that did not happen. These
                    // are the same two writes ClearActiveComboChain performs.
                    agent.ComboStepsRemaining = 0;
                    agent.ComboTargetEntityId = null;

                    // The spectator channel of design section 8. It is set here
                    // and cleared by ApplyEquipmentAttackFootworkAndDeathCleanup
                    // the moment this tick's finalised phase is anything but the
                    // Disengage this interrupt produced, so the pawn mark
                    // persists for a readable number of ticks rather than
                    // pulsing for one.
                    agent.BrokeOffUnderPressure = true;
                }
            }

            var (phase, ticksRemaining) =
                WeaponMovementRules.ResolveProvisionalFootwork(
                    isAlive: true,
                    agent.FootworkPhase,
                    agent.FootworkTicksRemaining,
                    agent.TacticalPosture,
                    context.SupportAllies,
                    context.SupportEnemies,
                    profile.DisengageEnemyToAllyBasisPoints,
                    profile.ReengageEnemyToAllyBasisPoints,
                    profile.RecoveryTicks,
                    hasTarget,
                    targetAtOrInsidePreferredDistance,
                    // The same answer the cost above was charged from, read
                    // back from the one slot that holds it. The gate is what
                    // keeps the index off a zero-length array under every
                    // preset that does not apply the interrupt, and the
                    // parameter's own default is the legacy ladder exactly.
                    pressureInterruptFired: appliesPressureInterrupt &&
                        _pressureInterruptFired[index]);
            _provisionalFootworkPhases[index] = phase;
            _provisionalFootworkTicks[index] = ticksRemaining;
        }
    }

    /// <summary>
    /// The equipment-relative proposal-gathering stage, replacing the legacy
    /// stage wholesale under the opt-in flag. Reads tick-start state only,
    /// exactly like the legacy stage: nothing is committed here except each
    /// agent's own facing and finalised footwork phase, neither of which any
    /// other agent's route reads this tick — every route reads tick-start
    /// positions and the scratch the pre-movement stages produced.
    /// </summary>
    private void GatherEquipmentRelativeMovementProposals()
    {
        Array.Clear(_movementProposals);
        Array.Clear(_proposedPaceRaw);
        var tick = checked((int)Tick);

        for (var index = 0; index < _agentStates.Length; index++)
        {
            var agent = _agentStates[index];
            if (!agent.IsAlive)
            {
                continue;
            }

            GatherOneEquipmentProposal(index, agent, tick);
        }
    }

    /// <summary>
    /// Routes one living agent through the destination precedence chain of
    /// design section 11.1 — dead, body-contact Attacking hold, last-stand
    /// rally, V6 disengage route, contingent cohesion, the remaining V6
    /// phase routes, then ordinary pursuit — and commits its footwork phase
    /// and timer exactly once through the two-step finalisation of design
    /// section 9.4. A destination taken from a source above the V6 routes
    /// counts as a surviving candidate: only a route whose own candidates
    /// were exhausted, by absence or by lane clearance, can finalise
    /// <see cref="FootworkPhase.Refuse"/>.
    /// </summary>
    private void GatherOneEquipmentProposal(int index, AgentState agent, int tick)
    {
        var profile = _movementRules.ResolveLoadoutProfile(agent.Loadout);
        var provisionalPhase = _provisionalFootworkPhases[index];
        var provisionalTicks = _provisionalFootworkTicks[index];
        var threat = ResolveLivingThreat(agent);

        bool phaseSurvives;
        if (agent.Intent == AgentIntent.Attacking)
        {
            // Body-contact Attacking hold: no movement is proposed, but the
            // warrior still turns toward its threat — turning in place is
            // permitted because facing commits independently of movement.
            TurnFacingInPlace(agent, profile, provisionalPhase, threat);
            phaseSurvives = true;
        }
        else if (agent.Intent == AgentIntent.Regrouping)
        {
            // The existing last-stand destination, pace-constrained per
            // design section 11.1's closing rule.
            if (TryComputeRegroupingAimPoint(agent, index) is { } rally)
            {
                ProposeEquipmentStepTowardPoint(
                    index,
                    agent,
                    profile,
                    provisionalPhase,
                    threat,
                    rally.XRaw,
                    rally.YRaw,
                    rally.RallyEntityId);
            }
            else
            {
                TurnFacingInPlace(agent, profile, provisionalPhase, threat);
            }

            phaseSurvives = true;
        }
        else if (provisionalPhase == FootworkPhase.Disengage)
        {
            phaseSurvives = TryProposeEquipmentRoute(
                index, agent, profile, provisionalPhase, threat);
        }
        else if (agent.Intent == AgentIntent.Moving &&
            agent.TargetEntityId is not null &&
            TryResolveContingentCohesionAimPoint(
                agent,
                tick,
                out var aimXRaw,
                out var aimYRaw,
                out var leaderEntityId))
        {
            // The existing contingent-cohesion destination, with the same
            // arrived-guard the legacy branch applies, pace-constrained.
            var squaredDistanceToAim = CollisionGeometry.SquaredDistance(
                agent.XRaw,
                agent.YRaw,
                aimXRaw,
                aimYRaw);
            if (squaredDistanceToAim <=
                CollisionGeometry.ContactSquaredDistance(Scenario.BodyRadiusRaw))
            {
                TurnFacingInPlace(agent, profile, provisionalPhase, threat);
            }
            else
            {
                ProposeEquipmentStepTowardPoint(
                    index,
                    agent,
                    profile,
                    provisionalPhase,
                    threat,
                    aimXRaw,
                    aimYRaw,
                    leaderEntityId);
            }

            phaseSurvives = true;
        }
        else if (provisionalPhase is FootworkPhase.Approach
            or FootworkPhase.Engage
            or FootworkPhase.Commit
            or FootworkPhase.Recover
            or FootworkPhase.Regroup
            or FootworkPhase.Pursue)
        {
            phaseSurvives = TryProposeEquipmentRoute(
                index, agent, profile, provisionalPhase, threat);
        }
        else if (agent.Intent == AgentIntent.Moving && threat is not null)
        {
            // Ordinary pursuit, the chain's floor. Defensive under V6: a
            // Moving agent has a target, so its provisional phase is one of
            // the route phases above and this branch is not reachable by
            // the phase table — it exists so a future phase addition
            // degrades to pursuit instead of to standing still.
            ProposeEquipmentStepTowardPoint(
                index,
                agent,
                profile,
                provisionalPhase,
                threat,
                threat.XRaw,
                threat.YRaw,
                threat.EntityId);
            phaseSurvives = true;
        }
        else
        {
            TurnFacingInPlace(agent, profile, provisionalPhase, threat);
            phaseSurvives = true;
        }

        // Design section 9.4: FootworkPhase and FootworkTicksRemaining are
        // written exactly once, here, after finalisation.
        var (finalPhase, finalTicks) = WeaponMovementRules.FinalizeFootwork(
            provisionalPhase, provisionalTicks, phaseSurvives);
        agent.FootworkPhase = finalPhase;
        agent.FootworkTicksRemaining = finalTicks;
    }

    /// <summary>
    /// Which of the four route-refusal reasons (ranged-units plan RU-06,
    /// F-A) <c>TryProposeEquipmentRoute</c> recorded for an agent's current
    /// tick, pending <see cref="ReconcileRouteRefusalReasonCounters"/>.
    /// </summary>
    private enum RouteRefusalReason : byte
    {
        None = 0,
        NoCandidatesBuilt = 1,
        StepEndpointRejected = 2,
        DirectCandidateOmitted = 3,
        LaneNotClear = 4,
    }

    /// <summary>
    /// Generates the provisional phase's route candidates per design section
    /// 10.4, clearance-tests them in order per section 10.5, and proposes
    /// the first survivor. Returns whether any candidate survived; on
    /// failure the agent emits no proposal and only turns in place toward
    /// its threat.
    /// </summary>
    private bool TryProposeEquipmentRoute(
        int index,
        AgentState agent,
        LoadoutMovementProfile profile,
        FootworkPhase provisionalPhase,
        AgentState? threat)
    {
        Span<EquipmentRouteCandidate> candidates =
            stackalloc EquipmentRouteCandidate[3];
        var count = BuildEquipmentRouteCandidates(
            index, agent, profile, provisionalPhase, threat, candidates);
        var context = _localMovementContexts[index];
        var actorClearanceSquared = SquaredClearanceRadius(profile);
        var mapWidthRaw = checked(Scenario.MapWidth * FixedPoint.Scale);
        var mapHeightRaw = checked(Scenario.MapHeight * FixedPoint.Scale);

        // Hoisted out of the candidate loop below because it depends only on
        // the scenario's registered preset, never on the candidate: RU-30
        // (F-B) reaches the monotone lane-clearance rule under exactly one
        // preset identity, the same way BattleSimulation.cs's ranged-standoff
        // branch gates on Scenario.MovementPreset == RangedStandoffV8.
        var usesMonotoneAllyClearance =
            Scenario.MovementPreset == MovementPresetId.MonotoneAllyClearanceV9;

        // WeaponMovementRules.FinalizeFootwork only turns a failed route into
        // FootworkPhase.Refuse for these three provisional phases; a failed
        // Commit/Recover/Regroup/Disengage route keeps its own phase and
        // timer instead (design section 9.4's "a blocked lane must not erase
        // a safety or attack lifecycle"). The four counters below decompose
        // MovementBehaviorMetrics.RefuseAgentTicks specifically, so they must
        // count only the calls FinalizeFootwork actually turns into Refuse —
        // gating on the identical condition keeps the four-way sum equal to
        // RefuseAgentTicks exactly, never inflated by a non-Refuse failure.
        var finalizesRefuseOnFailure = provisionalPhase is FootworkPhase.Approach
            or FootworkPhase.Engage
            or FootworkPhase.Pursue;

        // No candidate table at all: BuildEquipmentRouteCandidates found no
        // threat, no facing, or no non-zero delta to route toward, so the
        // loop below never runs. Counted here, once, rather than inside the
        // loop, so this reason is mutually exclusive with the three below —
        // together the four counters decompose RefuseAgentTicks with exactly
        // one increment per finalised Refuse, never more (RU-06, F-A).
        if (count == 0)
        {
            if (finalizesRefuseOnFailure)
            {
                _pendingRouteRefusalReasons[index] =
                    RouteRefusalReason.NoCandidatesBuilt;
            }

            TurnFacingInPlace(agent, profile, provisionalPhase, threat);
            return false;
        }

        for (var candidateIndex = 0; candidateIndex < count; candidateIndex++)
        {
            var candidate = candidates[candidateIndex];
            var facing = ResolveCandidateFacing(
                agent,
                profile,
                provisionalPhase,
                threat,
                candidate.DeltaXRaw,
                candidate.DeltaYRaw);
            var paceRaw = ResolveProposedPace(
                agent,
                profile,
                provisionalPhase,
                facing,
                candidate.DeltaXRaw,
                candidate.DeltaYRaw);

            // A point destination is somewhere to arrive; the step is the
            // smaller of the resulting pace and the remaining distance
            // (design section 6.5). A direction candidate — an oblique, a
            // facing vector, an escape vector — has no arrival point, so the
            // full pace applies.
            var effectivePaceRaw = paceRaw;
            if (candidate.IsPointDestination)
            {
                var distanceRaw = IntegerSquareRoot(checked(
                    (candidate.DeltaXRaw * candidate.DeltaXRaw) +
                    (candidate.DeltaYRaw * candidate.DeltaYRaw)));
                effectivePaceRaw = (int)Math.Min(paceRaw, distanceRaw);
            }

            if (MovementRouteRules.StepEndpoint(
                agent.XRaw,
                agent.YRaw,
                candidate.DeltaXRaw,
                candidate.DeltaYRaw,
                effectivePaceRaw,
                mapWidthRaw,
                mapHeightRaw,
                Scenario.BodyRadiusRaw) is not { } endpoint)
            {
                // Only the last candidate tried this call decides the
                // reason: an earlier candidate's continue is superseded the
                // moment a later one is attempted, so counting only the
                // final iteration keeps this counter mutually exclusive with
                // the other three and the four-way sum equal to
                // RefuseAgentTicks exactly (never a double count).
                if (finalizesRefuseOnFailure && candidateIndex == count - 1)
                {
                    _pendingRouteRefusalReasons[index] =
                        RouteRefusalReason.StepEndpointRejected;
                }

                continue;
            }

            if (candidate.SubjectToSecondThreatOmission &&
                ShouldOmitDirectCandidate(agent, context, endpoint))
            {
                if (finalizesRefuseOnFailure && candidateIndex == count - 1)
                {
                    _pendingRouteRefusalReasons[index] =
                        RouteRefusalReason.DirectCandidateOmitted;
                }

                continue;
            }

            if (!IsLaneClearOfAllies(
                index, agent, endpoint, actorClearanceSquared, usesMonotoneAllyClearance))
            {
                if (finalizesRefuseOnFailure && candidateIndex == count - 1)
                {
                    _pendingRouteRefusalReasons[index] =
                        RouteRefusalReason.LaneNotClear;
                }

                continue;
            }

            agent.Facing = facing;
            _movementProposals[index] =
                (endpoint.XRaw, endpoint.YRaw, candidate.EventTargetId);
            _proposedPaceRaw[index] = paceRaw;
            return true;
        }

        TurnFacingInPlace(agent, profile, provisionalPhase, threat);
        return false;
    }

    /// <summary>
    /// Resolves every pending route-refusal reason
    /// <c>TryProposeEquipmentRoute</c> recorded this tick against each
    /// agent's now-final <see cref="Movement.FootworkPhase"/>, incrementing
    /// exactly one of the four RU-06 counters per agent whose tick still
    /// finalises as <see cref="Movement.FootworkPhase.Refuse"/>. Must run
    /// after <see cref="ApplyEquipmentAttackFootworkAndDeathCleanup"/>: that
    /// pass can overwrite this same tick's Refuse with Commit for an agent
    /// whose gathered attack the combat stage accepted after the route was
    /// already rejected, and a reason recorded for a tick that never
    /// actually surfaces as Refuse must not be counted.
    /// </summary>
    private void ReconcileRouteRefusalReasonCounters()
    {
        for (var index = 0; index < _agentStates.Length; index++)
        {
            var reason = _pendingRouteRefusalReasons[index];
            if (reason == RouteRefusalReason.None)
            {
                continue;
            }

            _pendingRouteRefusalReasons[index] = RouteRefusalReason.None;

            if (_agentStates[index].FootworkPhase != FootworkPhase.Refuse)
            {
                continue;
            }

            switch (reason)
            {
                case RouteRefusalReason.NoCandidatesBuilt:
                    _routeRefusalNoCandidatesBuilt =
                        checked(_routeRefusalNoCandidatesBuilt + 1);
                    break;
                case RouteRefusalReason.StepEndpointRejected:
                    _routeRefusalStepEndpointRejected =
                        checked(_routeRefusalStepEndpointRejected + 1);
                    break;
                case RouteRefusalReason.DirectCandidateOmitted:
                    _routeRefusalDirectCandidateOmitted =
                        checked(_routeRefusalDirectCandidateOmitted + 1);
                    break;
                case RouteRefusalReason.LaneNotClear:
                    _routeRefusalLaneNotClear =
                        checked(_routeRefusalLaneNotClear + 1);
                    break;
            }
        }
    }

    /// <summary>
    /// The candidate table of design section 10.4, one phase per arm. Every
    /// candidate is a delta from the actor's tick-start position; the span
    /// is filled in the phase's own preference order.
    /// </summary>
    private int BuildEquipmentRouteCandidates(
        int index,
        AgentState agent,
        LoadoutMovementProfile profile,
        FootworkPhase provisionalPhase,
        AgentState? threat,
        Span<EquipmentRouteCandidate> candidates)
    {
        var count = 0;
        var sideAClockwise = MovementRouteRules.SideAIsWorldClockwise(
            _factionLocalIndexes[index], agent.FactionId);

        switch (provisionalPhase)
        {
            case FootworkPhase.Approach:
            case FootworkPhase.Engage:
                {
                    if (threat is null)
                    {
                        return 0;
                    }

                    var deltaXRaw = (long)threat.XRaw - agent.XRaw;
                    var deltaYRaw = (long)threat.YRaw - agent.YRaw;
                    if (deltaXRaw == 0 && deltaYRaw == 0)
                    {
                        return 0;
                    }

                    // The preferred distance is not a stop line: both phases
                    // continue toward the target's centre so the existing
                    // post-movement reach test stays authoritative.
                    var direct = new EquipmentRouteCandidate(
                        deltaXRaw,
                        deltaYRaw,
                        IsPointDestination: true,
                        SubjectToSecondThreatOmission: true,
                        threat.EntityId);
                    var (sideAX, sideAY) = MovementRouteRules.RotateOblique(
                        deltaXRaw, deltaYRaw, sideAClockwise);
                    var (sideBX, sideBY) = MovementRouteRules.RotateOblique(
                        deltaXRaw, deltaYRaw, !sideAClockwise);
                    var sideA = new EquipmentRouteCandidate(
                        sideAX,
                        sideAY,
                        IsPointDestination: false,
                        SubjectToSecondThreatOmission: false,
                        threat.EntityId);
                    var sideB = new EquipmentRouteCandidate(
                        sideBX,
                        sideBY,
                        IsPointDestination: false,
                        SubjectToSecondThreatOmission: false,
                        threat.EntityId);

                    // Engage against two or more occupied enemy loadout buckets
                    // prefers the obliques; a homogeneous composition, and every
                    // approach, goes direct first.
                    var sidesFirst = provisionalPhase == FootworkPhase.Engage &&
                        MovementRouteRules.OccupiedLoadoutBuckets(
                            _localMovementContexts[index].EnemyComposition) >= 2;
                    if (sidesFirst)
                    {
                        candidates[count++] = sideA;
                        candidates[count++] = sideB;
                        candidates[count++] = direct;
                    }
                    else
                    {
                        candidates[count++] = direct;
                        candidates[count++] = sideA;
                        candidates[count++] = sideB;
                    }

                    return count;
                }

            case FootworkPhase.Commit:
                {
                    if (threat is not null)
                    {
                        var deltaXRaw = (long)threat.XRaw - agent.XRaw;
                        var deltaYRaw = (long)threat.YRaw - agent.YRaw;
                        if (deltaXRaw == 0 && deltaYRaw == 0)
                        {
                            return 0;
                        }

                        candidates[count++] = new EquipmentRouteCandidate(
                            deltaXRaw,
                            deltaYRaw,
                            IsPointDestination: true,
                            SubjectToSecondThreatOmission: false,
                            threat.EntityId);
                        return count;
                    }

                    if (agent.Facing == Facing16.None)
                    {
                        return 0;
                    }

                    var (facingX, facingY) = FacingRules.SectorVector(agent.Facing);
                    candidates[count++] = new EquipmentRouteCandidate(
                        facingX,
                        facingY,
                        IsPointDestination: false,
                        SubjectToSecondThreatOmission: false,
                        agent.EntityId);
                    return count;
                }

            case FootworkPhase.Recover:
                {
                    long baseXRaw;
                    long baseYRaw;
                    ulong eventTargetId;
                    if (agent.Facing != Facing16.None)
                    {
                        var opposite = (Facing16)(((int)agent.Facing + 8) % 16);
                        var (oppositeX, oppositeY) =
                            FacingRules.SectorVector(opposite);
                        baseXRaw = oppositeX;
                        baseYRaw = oppositeY;
                        eventTargetId = threat?.EntityId ?? agent.EntityId;
                    }
                    else if (threat is not null)
                    {
                        baseXRaw = (long)agent.XRaw - threat.XRaw;
                        baseYRaw = (long)agent.YRaw - threat.YRaw;
                        if (baseXRaw == 0 && baseYRaw == 0)
                        {
                            return 0;
                        }

                        eventTargetId = threat.EntityId;
                    }
                    else
                    {
                        return 0;
                    }

                    candidates[count++] = new EquipmentRouteCandidate(
                        baseXRaw,
                        baseYRaw,
                        IsPointDestination: false,
                        SubjectToSecondThreatOmission: false,
                        eventTargetId);
                    var (sideAX, sideAY) = MovementRouteRules.RotateOblique(
                        baseXRaw, baseYRaw, sideAClockwise);
                    var (sideBX, sideBY) = MovementRouteRules.RotateOblique(
                        baseXRaw, baseYRaw, !sideAClockwise);
                    candidates[count++] = new EquipmentRouteCandidate(
                        sideAX,
                        sideAY,
                        IsPointDestination: false,
                        SubjectToSecondThreatOmission: false,
                        eventTargetId);
                    candidates[count++] = new EquipmentRouteCandidate(
                        sideBX,
                        sideBY,
                        IsPointDestination: false,
                        SubjectToSecondThreatOmission: false,
                        eventTargetId);
                    return count;
                }

            case FootworkPhase.Disengage:
            case FootworkPhase.Regroup:
                {
                    var context = _localMovementContexts[index];
                    var anchorExists = false;
                    if (context.NearestAllyEntityId is { } allyId &&
                        _agentIndexes.TryGetValue(allyId, out var allyIndex))
                    {
                        var ally = _agentStates[allyIndex];
                        if (ally.IsAlive)
                        {
                            anchorExists = true;
                            var deltaXRaw = (long)ally.XRaw - agent.XRaw;
                            var deltaYRaw = (long)ally.YRaw - agent.YRaw;
                            if (deltaXRaw != 0 || deltaYRaw != 0)
                            {
                                candidates[count++] = new EquipmentRouteCandidate(
                                    deltaXRaw,
                                    deltaYRaw,
                                    IsPointDestination: true,
                                    SubjectToSecondThreatOmission: false,
                                    allyId);
                            }
                        }
                    }

                    var slot = checked(
                        (agent.FactionId * FormationPlanner.MaximumContingents) +
                        agent.ContingentId);
                    var leaderEntityId = _contingentLeaderEntityIds[slot];
                    if (leaderEntityId != 0 &&
                        leaderEntityId != agent.EntityId &&
                        _agentIndexes.TryGetValue(leaderEntityId, out var leaderIndex))
                    {
                        var leader = _agentStates[leaderIndex];
                        if (leader.IsAlive)
                        {
                            anchorExists = true;
                            var deltaXRaw = (long)leader.XRaw - agent.XRaw;
                            var deltaYRaw = (long)leader.YRaw - agent.YRaw;
                            if (deltaXRaw != 0 || deltaYRaw != 0)
                            {
                                candidates[count++] = new EquipmentRouteCandidate(
                                    deltaXRaw,
                                    deltaYRaw,
                                    IsPointDestination: true,
                                    SubjectToSecondThreatOmission: false,
                                    leaderEntityId);
                            }
                        }
                    }

                    // Regroup stops at ally-then-leader; only Disengage owns the
                    // escape fallback, and only when neither anchor exists at
                    // all (design section 10.4).
                    if (provisionalPhase == FootworkPhase.Regroup || anchorExists)
                    {
                        return count;
                    }

                    if (threat is null)
                    {
                        return 0;
                    }

                    var escapeFromNearestX = (long)agent.XRaw - threat.XRaw;
                    var escapeFromNearestY = (long)agent.YRaw - threat.YRaw;
                    long escapeFromSecondX = 0;
                    long escapeFromSecondY = 0;
                    if (context.SecondThreatEntityId is { } secondThreatId &&
                        _agentIndexes.TryGetValue(secondThreatId, out var secondIndex))
                    {
                        var second = _agentStates[secondIndex];
                        if (second.IsAlive)
                        {
                            escapeFromSecondX = (long)agent.XRaw - second.XRaw;
                            escapeFromSecondY = (long)agent.YRaw - second.YRaw;
                        }
                    }

                    var escapeXRaw = checked(escapeFromNearestX + escapeFromSecondX);
                    var escapeYRaw = checked(escapeFromNearestY + escapeFromSecondY);
                    if (escapeXRaw == 0 && escapeYRaw == 0)
                    {
                        if (escapeFromNearestX == 0 && escapeFromNearestY == 0)
                        {
                            return 0;
                        }

                        (escapeXRaw, escapeYRaw) =
                            MovementRouteRules.PerpendicularVector(
                                escapeFromNearestX,
                                escapeFromNearestY,
                                sideAClockwise);
                    }

                    candidates[count++] = new EquipmentRouteCandidate(
                        escapeXRaw,
                        escapeYRaw,
                        IsPointDestination: false,
                        SubjectToSecondThreatOmission: false,
                        threat.EntityId);
                    return count;
                }

            case FootworkPhase.Pursue:
                {
                    if (threat is null ||
                        !HasAllyWithinPursuitSupport(index, agent, profile))
                    {
                        return 0;
                    }

                    var deltaXRaw = (long)threat.XRaw - agent.XRaw;
                    var deltaYRaw = (long)threat.YRaw - agent.YRaw;
                    if (deltaXRaw == 0 && deltaYRaw == 0)
                    {
                        return 0;
                    }

                    candidates[count++] = new EquipmentRouteCandidate(
                        deltaXRaw,
                        deltaYRaw,
                        IsPointDestination: true,
                        SubjectToSecondThreatOmission: false,
                        threat.EntityId);
                    return count;
                }

            default:
                return 0;
        }
    }

    /// <summary>
    /// The second-threat rule of design section 10.4: with two or more
    /// immediate enemies, the direct candidate is omitted only when its
    /// endpoint sits strictly closer to the second threat than the actor's
    /// tick-start position does. Exact equality keeps the direct candidate.
    /// </summary>
    private bool ShouldOmitDirectCandidate(
        AgentState agent,
        in LocalMovementContext context,
        (int XRaw, int YRaw) endpoint)
    {
        if (context.ImmediateEnemies < 2 ||
            context.SecondThreatEntityId is not { } secondThreatId ||
            !_agentIndexes.TryGetValue(secondThreatId, out var secondIndex))
        {
            return false;
        }

        var second = _agentStates[secondIndex];
        if (!second.IsAlive)
        {
            return false;
        }

        var endpointSquared = CollisionGeometry.SquaredDistance(
            endpoint.XRaw, endpoint.YRaw, second.XRaw, second.YRaw);
        var startSquared = CollisionGeometry.SquaredDistance(
            agent.XRaw, agent.YRaw, second.XRaw, second.YRaw);
        return endpointSquared < startSquared;
    }

    /// <summary>
    /// The lane-clearance test of design section 10.5: one stable scan over
    /// every living same-faction agent's tick-start position, rejecting a
    /// candidate endpoint that sits at squared distance strictly less than
    /// the square of the larger of the actor's and that ally's clearance
    /// radii. Exact equality is clear, matching the collision stage's
    /// strict-less tangency convention. No neighbours are stored and
    /// nothing allocates.
    ///
    /// <paramref name="isMonotone"/> -- <see langword="true"/> only under
    /// <see cref="MovementPresetId.MonotoneAllyClearanceV9"/> -- narrows that
    /// absolute rule to a monotonicity constraint (design section 10.3, plan
    /// task RU-30, F-B): a candidate whose endpoint is still inside a given
    /// ally's clearance radius is rejected only when it also moves the actor
    /// strictly closer to that same ally than the actor's own tick-start
    /// position already was. An actor already standing inside an ally's
    /// clearance radius may hold, sidestep, or retreat; only a step that
    /// tightens that specific, pre-existing violation is refused. This is
    /// what F-A's root-cause finding named: the absolute rule punished
    /// movement out of a violation, not the violation itself. V1 through V8
    /// are unaffected -- <paramref name="isMonotone"/> is <see langword="false"/>
    /// for every one of them, so this method's decision is byte-identical to
    /// the rule above for every preset except V9.
    /// </summary>
    private bool IsLaneClearOfAllies(
        int selfIndex,
        AgentState agent,
        (int XRaw, int YRaw) endpoint,
        Int128 actorClearanceSquared,
        bool isMonotone)
    {
        var actorXRaw = agent.XRaw;
        var actorYRaw = agent.YRaw;

        for (var otherIndex = 0; otherIndex < _agentStates.Length; otherIndex++)
        {
            if (otherIndex == selfIndex)
            {
                continue;
            }

            var ally = _agentStates[otherIndex];
            if (!ally.IsAlive || ally.FactionId != agent.FactionId)
            {
                continue;
            }

            var required = Int128.Max(
                actorClearanceSquared,
                SquaredClearanceRadius(
                    _movementRules.ResolveLoadoutProfile(ally.Loadout)));
            var separation = (Int128)CollisionGeometry.SquaredDistance(
                endpoint.XRaw, endpoint.YRaw, ally.XRaw, ally.YRaw);
            if (separation >= required)
            {
                continue;
            }

            if (!isMonotone)
            {
                return false;
            }

            // Actor's tick-start separation to this same ally. Depends only
            // on the actor's fixed tick-start position (hoisted above, out
            // of this per-ally loop's repeated field reads) and the ally's
            // own tick-start position -- never on the candidate endpoint --
            // so it is the monotonicity baseline every candidate this call's
            // caller tries is compared against.
            var currentSeparation = (Int128)CollisionGeometry.SquaredDistance(
                actorXRaw, actorYRaw, ally.XRaw, ally.YRaw);
            if (separation < currentSeparation)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Whether at least one living ally stands within the actor's
    /// pursuit-support distance, inclusive at the exact radius like every
    /// context ring — the condition under which <see cref="FootworkPhase.Pursue"/>
    /// keeps proposing a direct route (design section 10.4).
    /// </summary>
    private bool HasAllyWithinPursuitSupport(
        int selfIndex,
        AgentState agent,
        LoadoutMovementProfile profile)
    {
        var supportSquared = MovementContextQuery.SquaredContextRadius(
            MovementRouteRules.ClearanceRadiusRaw(
                Scenario.BodyRadiusRaw,
                profile.PursuitSupportBodyDiametersBasisPoints));

        for (var otherIndex = 0; otherIndex < _agentStates.Length; otherIndex++)
        {
            if (otherIndex == selfIndex)
            {
                continue;
            }

            var ally = _agentStates[otherIndex];
            if (!ally.IsAlive || ally.FactionId != agent.FactionId)
            {
                continue;
            }

            if ((Int128)SquaredDistance(agent, ally) <= supportSquared)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Builds one pace-constrained proposal toward a point destination
    /// selected above the V6 routes in the precedence chain — the last-stand
    /// rally aim, the contingent-cohesion aim, or the pursuit floor. Facing
    /// and phase pace constrain the one-tick step per design section 11.1;
    /// no lane clearance applies, because these destinations are the
    /// existing sources the chain preserves.
    /// </summary>
    private void ProposeEquipmentStepTowardPoint(
        int index,
        AgentState agent,
        LoadoutMovementProfile profile,
        FootworkPhase provisionalPhase,
        AgentState? threat,
        int destinationXRaw,
        int destinationYRaw,
        ulong eventTargetId)
    {
        var deltaXRaw = (long)destinationXRaw - agent.XRaw;
        var deltaYRaw = (long)destinationYRaw - agent.YRaw;
        if (deltaXRaw == 0 && deltaYRaw == 0)
        {
            TurnFacingInPlace(agent, profile, provisionalPhase, threat);
            return;
        }

        var facing = ResolveCandidateFacing(
            agent, profile, provisionalPhase, threat, deltaXRaw, deltaYRaw);
        var paceRaw = ResolveProposedPace(
            agent, profile, provisionalPhase, facing, deltaXRaw, deltaYRaw);
        var distanceRaw = IntegerSquareRoot(checked(
            (deltaXRaw * deltaXRaw) + (deltaYRaw * deltaYRaw)));
        var effectivePaceRaw = (int)Math.Min(paceRaw, distanceRaw);
        var mapWidthRaw = checked(Scenario.MapWidth * FixedPoint.Scale);
        var mapHeightRaw = checked(Scenario.MapHeight * FixedPoint.Scale);

        if (MovementRouteRules.StepEndpoint(
            agent.XRaw,
            agent.YRaw,
            deltaXRaw,
            deltaYRaw,
            effectivePaceRaw,
            mapWidthRaw,
            mapHeightRaw,
            Scenario.BodyRadiusRaw) is not { } endpoint)
        {
            TurnFacingInPlace(agent, profile, provisionalPhase, threat);
            return;
        }

        agent.Facing = facing;
        _movementProposals[index] = (endpoint.XRaw, endpoint.YRaw, eventTargetId);
        _proposedPaceRaw[index] = paceRaw;
    }

    /// <summary>
    /// Turns a warrior that proposes no movement this tick toward its
    /// selected living threat. With no threat there is no destination
    /// either, so facing is retained (design section 6.3).
    /// </summary>
    private static void TurnFacingInPlace(
        AgentState agent,
        LoadoutMovementProfile profile,
        FootworkPhase provisionalPhase,
        AgentState? threat)
    {
        if (threat is null)
        {
            return;
        }

        var desired = FacingRules.FromDelta(
            (long)threat.XRaw - agent.XRaw,
            (long)threat.YRaw - agent.YRaw,
            agent.FactionId);
        if (desired == Facing16.None)
        {
            return;
        }

        agent.Facing = TurnTowardWithBudget(
            agent, profile, provisionalPhase, desired);
    }

    /// <summary>
    /// The committed facing for one candidate: toward the selected living
    /// threat when one exists, else toward the candidate's own travel
    /// direction, advanced by at most the phase-appropriate turn budget
    /// (design section 6.3).
    /// </summary>
    private static Facing16 ResolveCandidateFacing(
        AgentState agent,
        LoadoutMovementProfile profile,
        FootworkPhase provisionalPhase,
        AgentState? threat,
        long travelDeltaXRaw,
        long travelDeltaYRaw)
    {
        var desired = threat is not null
            ? FacingRules.FromDelta(
                (long)threat.XRaw - agent.XRaw,
                (long)threat.YRaw - agent.YRaw,
                agent.FactionId)
            : FacingRules.FromDelta(
                travelDeltaXRaw, travelDeltaYRaw, agent.FactionId);
        if (desired == Facing16.None)
        {
            return agent.Facing;
        }

        return TurnTowardWithBudget(agent, profile, provisionalPhase, desired);
    }

    /// <summary>
    /// One bounded turn: at most the profile's ordinary facing steps, or its
    /// committed facing steps while the phase is <see cref="FootworkPhase.Commit"/>.
    /// A warrior somehow still holding <see cref="Facing16.None"/> — never
    /// the case for a living V6 agent, whose facing is initialised at
    /// creation — snaps directly to the desired sector rather than feeding
    /// the sentinel into sector arithmetic.
    /// </summary>
    private static Facing16 TurnTowardWithBudget(
        AgentState agent,
        LoadoutMovementProfile profile,
        FootworkPhase provisionalPhase,
        Facing16 desired)
    {
        if (agent.Facing == Facing16.None)
        {
            return desired;
        }

        var budget = provisionalPhase == FootworkPhase.Commit
            ? profile.CommittedFacingStepsPerTick
            : profile.MaximumFacingStepsPerTick;
        return FacingRules.TurnToward(
            agent.Facing, desired, budget, agent.FactionId);
    }

    /// <summary>
    /// The direction-band pace pipeline of design sections 6.4 and 6.5: the
    /// separation between the committed facing and the travel direction
    /// selects the band cap, <see cref="FootworkPhase.Commit"/> clamps it
    /// further, the capped basis points convert to a desired raw pace bounded
    /// by <c>MovementSpeedRaw</c>, and the retained pace advances toward it
    /// by one bounded acceleration or deceleration step.
    /// </summary>
    private static int ResolveProposedPace(
        AgentState agent,
        LoadoutMovementProfile profile,
        FootworkPhase provisionalPhase,
        Facing16 committedFacing,
        long travelDeltaXRaw,
        long travelDeltaYRaw)
    {
        var travelSector = FacingRules.FromDelta(
            travelDeltaXRaw, travelDeltaYRaw, agent.FactionId);
        int paceCapBasisPoints;
        if (committedFacing == Facing16.None || travelSector == Facing16.None)
        {
            paceCapBasisPoints = profile.ForwardPaceBasisPoints;
        }
        else
        {
            var separation = FacingRules.SectorSeparation(
                committedFacing, travelSector);
            paceCapBasisPoints = FacingRules.DirectionBandPaceCapBasisPoints(
                profile, separation);
        }

        if (provisionalPhase == FootworkPhase.Commit)
        {
            paceCapBasisPoints = Math.Min(
                paceCapBasisPoints, profile.CommittedPaceBasisPoints);
        }

        var desiredPaceRaw = MovementRouteRules.DesiredPaceRaw(
            agent.MovementSpeedRaw, paceCapBasisPoints);
        return MovementRouteRules.AdvanceRetainedPaceRaw(
            agent.MovementPaceRaw,
            desiredPaceRaw,
            MovementRouteRules.PaceStepRaw(
                agent.MovementSpeedRaw,
                profile.AccelerationBasisPointsPerTick),
            MovementRouteRules.PaceStepRaw(
                agent.MovementSpeedRaw,
                profile.DecelerationBasisPointsPerTick));
    }

    /// <summary>
    /// The clearance radius of one profile, squared through
    /// <see cref="Int128"/> so no comparison can overflow.
    /// </summary>
    private Int128 SquaredClearanceRadius(LoadoutMovementProfile profile) =>
        MovementContextQuery.SquaredContextRadius(
            MovementRouteRules.ClearanceRadiusRaw(
                Scenario.BodyRadiusRaw,
                profile.AllyClearanceBodyDiametersBasisPoints));

    /// <summary>
    /// The agent's selected target, resolved to a living state, or
    /// <see langword="null"/> when no living threat is selected.
    /// </summary>
    private AgentState? ResolveLivingThreat(AgentState agent) =>
        agent.TargetEntityId is { } targetId &&
        _agentIndexes.TryGetValue(targetId, out var targetIndex) &&
        _agentStates[targetIndex].IsAlive
            ? _agentStates[targetIndex]
            : null;

    /// <summary>
    /// The friendly-clearance conflict pass of design section 10.6, run per
    /// faction between proposal gathering and collision resolution. Every
    /// living proposer enters in ascending <c>EntityId</c> order —
    /// guaranteed by agent storage order — carrying its committed phase; a
    /// rejected proposal becomes a no-move with zero retained pace and
    /// increments the derived denial counter. No reroute, no phase change.
    /// </summary>
    private void ResolveFriendlyClearanceConflicts()
    {
        for (var faction = 0; faction < FactionCount; faction++)
        {
            var count = 0;
            for (var index = 0; index < _agentStates.Length; index++)
            {
                var agent = _agentStates[index];
                if (!agent.IsAlive || agent.FactionId != faction)
                {
                    continue;
                }

                if (_movementProposals[index] is not { } proposal)
                {
                    continue;
                }

                _conflictProposals[count] = new FriendlyClearanceProposal(
                    agent.EntityId,
                    agent.FootworkPhase,
                    proposal.XRaw,
                    proposal.YRaw,
                    SquaredClearanceRadius(
                        _movementRules.ResolveLoadoutProfile(agent.Loadout)));
                _conflictProposalAgentIndexes[count] = index;
                count++;
            }

            MovementRouteRules.AcceptFriendlyClearanceConflicts(
                _conflictProposals.AsSpan(0, count),
                _conflictAccepted.AsSpan(0, count));

            for (var accepted = 0; accepted < count; accepted++)
            {
                if (_conflictAccepted[accepted])
                {
                    continue;
                }

                var agentIndex = _conflictProposalAgentIndexes[accepted];
                _movementProposals[agentIndex] = null;
                _proposedPaceRaw[agentIndex] = 0;
                _movementConflictDenials = checked(_movementConflictDenials + 1);
            }
        }
    }

    /// <summary>
    /// The post-attack footwork pass of design section 9.6, run between
    /// <see cref="GatherAndCommitAttacks"/> and <see cref="ResolveOutcome"/>:
    /// surviving accepted attackers enter <see cref="FootworkPhase.Commit"/>
    /// with the profile's commitment duration — interrupting
    /// <see cref="FootworkPhase.Recover"/>, because movement recovery never
    /// suppresses an attack the combat gates accepted — and every agent the
    /// gathered exchange killed takes death cleanup: pace, posture, phase,
    /// and timer clear atomically before any outcome, hash, or snapshot
    /// work, while the final facing is retained as readable spectator
    /// information. The writes are idempotent for agents that died on an
    /// earlier tick, whose four fields are already clear.
    /// </summary>
    /// <remarks>
    /// Under <see cref="MovementRuleset.AppliesPressureInterrupt"/> this pass
    /// also stamps the two one-tick histories the interrupt reads on the
    /// following tick and maintains the break-off flag it sets, and clears all
    /// three on a dead agent alongside the four above. The stamps land here,
    /// after the footwork stage has already read the previous tick's values,
    /// which is exactly what makes a single integer per signal sufficient (V7
    /// design section 4.5). Nothing here reads the interrupt's decision; it
    /// only records the state the next tick's decision will be made from.
    /// <para>
    /// The summary's invariant survives the interrupt: combat still reads
    /// nothing from movement, and this pass still never suppresses an attack
    /// the combat gates accepted. What V7 adds is the reverse direction — the
    /// movement stage writing <see cref="AgentState.AttackCooldownRemaining"/>,
    /// a field the combat stage owns — at the one gated site in
    /// <see cref="ResolveEquipmentPosturesAndProvisionalFootwork"/>.
    /// </para>
    /// </remarks>
    private void ApplyEquipmentAttackFootworkAndDeathCleanup()
    {
        var appliesPressureInterrupt = _movementRules.AppliesPressureInterrupt;
        for (var index = 0; index < _agentStates.Length; index++)
        {
            var agent = _agentStates[index];
            if (!agent.IsAlive)
            {
                agent.MovementPaceRaw = 0;
                agent.TacticalPosture = TacticalPosture.None;
                agent.FootworkPhase = FootworkPhase.None;
                agent.FootworkTicksRemaining = 0;
                if (appliesPressureInterrupt)
                {
                    // Idempotent for an agent that died on an earlier tick,
                    // exactly as the four writes above are.
                    agent.DamageTakenLastTick = 0;
                    agent.PriorSupportAllies = 0;
                    agent.BrokeOffUnderPressure = false;
                }

                continue;
            }

            if (_attackAcceptedThisTick[index])
            {
                var profile = _movementRules.ResolveLoadoutProfile(agent.Loadout);
                agent.FootworkPhase = FootworkPhase.Commit;
                agent.FootworkTicksRemaining = profile.CommitmentTicks;
            }

            if (appliesPressureInterrupt)
            {
                // Signal B's history: this tick's accumulated damage, read on
                // the next tick as the damage taken on the previous one.
                // _damageTotals is cleared at the top of GatherAndCommitAttacks
                // and is still populated here, so this is a stamp rather than a
                // new query or a second pass.
                agent.DamageTakenLastTick = _damageTotals[index];

                // Signal C's history: this tick's support-ring ally count,
                // including the actor, taken from the context
                // SelectTargetsAndIntents derived at the top of the tick.
                agent.PriorSupportAllies =
                    _localMovementContexts[index].SupportAllies;

                // The break-off flag's lifetime, design section 8, channel 1.
                // It is not a single-tick pulse: it survives every tick the
                // warrior stays in the Disengage the interrupt produced, and
                // clears the moment this tick's finalised phase is anything
                // else — including the Commit an accepted attack writes just
                // above — and on death, in the branch above. That persistence
                // is what makes the pawn mark visible at 1x speed. This runs
                // after the attack-footwork write on purpose, so the flag is
                // always judged against the phase the tick actually ends in.
                agent.BrokeOffUnderPressure = agent.BrokeOffUnderPressure &&
                    agent.FootworkPhase == FootworkPhase.Disengage;
            }
        }
    }

    /// <summary>
    /// One route candidate of design section 10.4: a delta from the actor's
    /// tick-start position, whether it is a point to arrive at or a pure
    /// direction, whether the second-threat rule may omit it, and the entity
    /// the resulting Move event names.
    /// </summary>
    private readonly record struct EquipmentRouteCandidate(
        long DeltaXRaw,
        long DeltaYRaw,
        bool IsPointDestination,
        bool SubjectToSecondThreatOmission,
        ulong EventTargetId);

    /// <summary>
    /// Evaluates the six movement gates of design section 3.5 for one
    /// <see cref="AgentIntent.Moving"/> agent under every
    /// persistent-contingent preset and, when every
    /// gate permits, computes its cohesion aim point — the give-way escape
    /// when the agent stands in its own leader's forward corridor, otherwise
    /// the trail base plus this member's personal offset from
    /// <see cref="ContingentOffset.Compute"/> — matching
    /// <see cref="BuildRegroupingProposal"/>'s own trail-then-offset shape.
    /// </summary>
    /// <remarks>
    /// Gates 5 and 6 are read here as the array values
    /// <see cref="ResolveContingentStates"/> already computed this tick, not
    /// recomputed: gate 6 in particular cannot be evaluated correctly inside
    /// a per-agent loop, because the loop reaches one contingent's members
    /// before it has reached every other contingent whose squares decide the
    /// answer.
    /// </remarks>
    /// <param name="agent">The moving agent to evaluate.</param>
    /// <param name="tick">The current tick, truncated to <see cref="int"/>.</param>
    /// <param name="aimXRaw">
    /// The computed aim point's X, valid only when this method returns
    /// <see langword="true"/>.
    /// </param>
    /// <param name="aimYRaw">
    /// The computed aim point's Y, valid only when this method returns
    /// <see langword="true"/>.
    /// </param>
    /// <param name="leaderEntityId">
    /// This agent's contingent leader's <see cref="AgentState.EntityId"/>,
    /// valid only when this method returns <see langword="true"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when all six gates permit a cohesion
    /// destination for this agent this tick.
    /// </returns>
    private bool TryResolveContingentCohesionAimPoint(
        AgentState agent,
        int tick,
        out int aimXRaw,
        out int aimYRaw,
        out ulong leaderEntityId)
    {
        aimXRaw = 0;
        aimYRaw = 0;

        var slot = checked(
            (agent.FactionId * FormationPlanner.MaximumContingents) +
            agent.ContingentId);
        leaderEntityId = _contingentLeaderEntityIds[slot];

        if (leaderEntityId == 0 ||
            !_agentIndexes.TryGetValue(leaderEntityId, out var leaderIndex))
        {
            return false;
        }

        var state = agent.ContingentState;
        var isLeader = agent.EntityId == leaderEntityId;
        var windowOpen = MovementRules.IsCohesionWindowOpen(
            tick,
            slot,
            _movementRules.CohesionCycleTicks,
            _movementRules.CohesionDutyTicks);

        var leader = _agentStates[leaderIndex];
        var straggling = false;
        if (state == ContingentState.Advance)
        {
            var memberSquared = SquaredDistance(agent, leader);
            var cohesionRadiusRaw = checked(
                (long)_movementRules.CohesionRadiusMultiplier * Scenario.BodyRadiusRaw);

            // memberSquared is an unscaled squared world distance bounded
            // only by Scenario.MaximumMapDimension, so 16 * memberSquared can
            // exceed long.MaxValue on a wide map. Widening to Int128 makes
            // the comparison exact and total: it reproduces the checked long
            // comparison bit-for-bit whenever that comparison would not have
            // overflowed, and for the inputs that would have overflowed it
            // returns the answer implied by unbounded integer arithmetic — a
            // member that far from its leader is unambiguously straggling.
            if (_movementRules.GathersContingentsBeforeContact)
            {
                // The registered band replaces the hardcoded three-quarters
                // fraction: a member is straggling while its squared distance
                // from its leader exceeds the square of the band fraction of
                // the cohesion radius. Cross-multiplying by the squared
                // denominator keeps the test in exact integers instead of
                // dividing, exactly as the unregistered statement below does.
                // The widening argument above covers this form too: the
                // denominator is an int, so its square is below 2^62, and the
                // product with memberSquared stays far inside Int128's range.
                var bandNumerator = (Int128)_movementRules.CohesionBandNumerator;
                var bandDenominator =
                    (Int128)_movementRules.CohesionBandDenominator;

                straggling = bandDenominator * bandDenominator * memberSquared >
                    bandNumerator * bandNumerator *
                        cohesionRadiusRaw * cohesionRadiusRaw;
            }
            else
            {
                straggling = (Int128)16 * memberSquared >
                    (Int128)9 * cohesionRadiusRaw * cohesionRadiusRaw;
            }
        }

        if (!MovementRules.IsCohesionEligible(
            state,
            isLeader,
            windowOpen,
            straggling,
            _contingentSquareFitsMap[slot],
            _contingentSquareOverlapsAnother[slot]))
        {
            return false;
        }

        var direction = ComputeRallyDirection(leader);
        if (direction.DistanceRaw > 0 &&
            TryComputeGiveWayAimPoint(agent, leader, direction) is
            { XRaw: var giveWayXRaw, YRaw: var giveWayYRaw })
        {
            aimXRaw = giveWayXRaw;
            aimYRaw = giveWayYRaw;
            return true;
        }

        var (offsetXRaw, offsetYRaw) = ContingentOffset.Compute(
            Scenario.Seed,
            agent.EntityId,
            _contingentJitterRaw[slot]);

        var mapWidthRaw = checked(Scenario.MapWidth * FixedPoint.Scale);
        var mapHeightRaw = checked(Scenario.MapHeight * FixedPoint.Scale);
        aimXRaw = CollisionGeometry.ClampCenterToBounds(
            SaturateToInt32(checked(
                (long)_contingentTrailBaseXRaw[slot] + offsetXRaw)),
            mapWidthRaw,
            Scenario.BodyRadiusRaw);
        aimYRaw = CollisionGeometry.ClampCenterToBounds(
            SaturateToInt32(checked(
                (long)_contingentTrailBaseYRaw[slot] + offsetYRaw)),
            mapHeightRaw,
            Scenario.BodyRadiusRaw);

        return true;
    }

    /// <summary>
    /// Builds the movement proposal that closes a regrouping follower on its
    /// faction's rally agent plus that follower's own fixed positional bias
    /// (<see cref="RallyOffset"/>). Returns <c>null</c> — propose no movement
    /// — in two cases: the arrived-guard, when the follower's squared distance
    /// to its aim point is already at or inside
    /// <see cref="CollisionGeometry.ContactSquaredDistance"/>, which stops the
    /// one-raw-unit movement-floor twitch and the resulting <c>Move</c>-event
    /// flood a settled cluster would otherwise emit every tick; and the
    /// defensive case where the rally entity ID is the zero sentinel or the
    /// rally agent cannot be resolved to a living agent, which falls back to
    /// no movement rather than throwing.
    /// </summary>
    /// <remarks>
    /// The follower's aim point trails <see cref="FormationRules.RallyTrailRadiusMultiplier"/>
    /// body radii behind the rally agent, opposite the rally agent's own
    /// direction of travel, before the jitter offset is applied. Without the
    /// trail, a follower whose jitter offset happens to point along the rally
    /// agent's forward arc parks permanently in front of its own leader and
    /// blocks it — the rally agent is exempt from regrouping and never routes
    /// around its own formation, so that block never clears. Two factions
    /// doing this simultaneously deadlock the whole battle at the tick limit.
    /// See the type-level remarks on <see cref="FormationRules"/> for the
    /// clearance derivation.
    /// </remarks>
    /// <remarks>
    /// The trail alone is not enough: a follower can start the tick already
    /// ahead of the rally agent, in which case its trail-behind aim point
    /// sits on the far side of the leader's own body, and reaching it in a
    /// straight line means walking backward through the leader. See
    /// <see cref="TryComputeGiveWayAimPoint"/> for the sideways escape this
    /// method checks first.
    /// </remarks>
    /// <remarks>
    /// A follower whose aim point lies beyond an ally walks to exact tangency
    /// and then pushes against it forever, because tangency is a legal resting
    /// position and closing further is strict penetration. The stall generation
    /// is the escape: once this agent has been blocked for
    /// <see cref="FormationRules.StallEscapeStreakTicks"/> consecutive ticks it
    /// draws a different aim point. It is 0, and therefore inert, in every
    /// battle that is merely crowded.
    /// </remarks>
    /// <summary>
    /// The pursuit-path counterpart of this type's rally stall escape: a
    /// warrior walking at an enemy that a comrade's body has refused for
    /// <see cref="FormationRules.StallEscapeStreakTicks"/> consecutive ticks
    /// aims beside its enemy rather than at its enemy's centre, which puts it
    /// on a different line of approach and out from behind the body that was
    /// refusing it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only reached at a non-zero stall generation. The design is in
    /// the 2026-07-29 approach sidestep design, and its section
    /// 2 records why the rally escape alone was not enough: that escape lives in
    /// <see cref="BuildRegroupingProposal"/> and a pursuing warrior never
    /// reaches it, so a locked cluster could contain warriors with a way out and
    /// warriors without one at the same time.
    /// </para>
    /// <para>
    /// The contingent cohesion branch above this one is deliberately left
    /// alone. Its aim point sits inside a bias square whose combined
    /// aim-point density statement movement gate 6 exists to hold, and
    /// displacing an agent out of that square would invalidate the statement.
    /// A contingent that cannot make progress resolves to
    /// <see cref="ContingentState.Advance"/> and its members fall back to this
    /// path anyway.
    /// </para>
    /// </remarks>
    /// <param name="agent">The blocked pursuer.</param>
    /// <param name="target">The enemy it is walking at.</param>
    /// <param name="stallGeneration">
    /// The pursuer's current stall generation, which the caller has already
    /// established is non-zero.
    /// </param>
    /// <param name="stopShortRaw">
    /// Overrides the stopping distance used when the offset resolves to zero
    /// and this falls back to closing straight on <paramref name="target"/>.
    /// <see langword="null"/> keeps the original two-body-radius stop; a
    /// ranged pursuer beyond its standoff distance passes that distance here
    /// instead, so a stalled ranged agent's fallback still stops at standoff
    /// rather than walking in to body contact.
    /// </param>
    /// <returns>The pursuer's movement proposal against the offset aim point.</returns>
    private (int XRaw, int YRaw, ulong TargetId) BuildSidesteppingPursuitProposal(
        AgentState agent,
        AgentState target,
        int stallGeneration,
        int? stopShortRaw = null)
    {
        var deltaXRaw = (long)target.XRaw - agent.XRaw;
        var deltaYRaw = (long)target.YRaw - agent.YRaw;
        var distanceRaw = IntegerSquareRoot(
            checked((deltaXRaw * deltaXRaw) + (deltaYRaw * deltaYRaw)));

        var (offsetXRaw, offsetYRaw) = ApproachSidestep.Compute(
            Scenario.Seed,
            agent.EntityId,
            Scenario.BodyRadiusRaw,
            stallGeneration,
            deltaXRaw,
            deltaYRaw,
            distanceRaw);

        if (offsetXRaw == 0 && offsetYRaw == 0)
        {
            return stopShortRaw is { } directStopShortRaw
                ? BuildMovementProposal(
                    agent, target.XRaw, target.YRaw, target.EntityId, directStopShortRaw)
                : BuildMovementProposal(agent, target);
        }

        // Saturated and clamped the same way BuildRegroupingProposal handles its
        // own aim point: the offset is bounded by a few body radii, but the
        // target may already stand against a map edge.
        var mapWidthRaw = checked(Scenario.MapWidth * FixedPoint.Scale);
        var mapHeightRaw = checked(Scenario.MapHeight * FixedPoint.Scale);
        var aimXRaw = CollisionGeometry.ClampCenterToBounds(
            SaturateToInt32(checked((long)target.XRaw + offsetXRaw)),
            mapWidthRaw,
            Scenario.BodyRadiusRaw);
        var aimYRaw = CollisionGeometry.ClampCenterToBounds(
            SaturateToInt32(checked((long)target.YRaw + offsetYRaw)),
            mapHeightRaw,
            Scenario.BodyRadiusRaw);

        return stopShortRaw is { } aimStopShortRaw
            ? BuildMovementProposal(agent, aimXRaw, aimYRaw, target.EntityId, aimStopShortRaw)
            : BuildMovementProposal(agent, aimXRaw, aimYRaw, target.EntityId);
    }

    private (int XRaw, int YRaw, ulong TargetId)? BuildRegroupingProposal(
        AgentState agent,
        int agentIndex)
    {
        if (TryComputeRegroupingAimPoint(agent, agentIndex) is not
            { } aim)
        {
            return null;
        }

        return BuildMovementProposal(
            agent,
            aim.XRaw,
            aim.YRaw,
            aim.RallyEntityId);
    }

    /// <summary>
    /// The destination half of <see cref="BuildRegroupingProposal"/>,
    /// extracted so the equipment-relative footwork pipeline can constrain
    /// the same last-stand aim point with its own facing-and-pace step
    /// while every legacy preset keeps building the identical proposal
    /// through the wrapper above. All of the original selection logic —
    /// the rally resolution, the give-way corridor escape, the
    /// trail-plus-jitter aim point, and the arrived-guard — lives here
    /// unchanged and in the original order.
    /// </summary>
    private (int XRaw, int YRaw, ulong RallyEntityId)?
        TryComputeRegroupingAimPoint(AgentState agent, int agentIndex)
    {
        var rallyEntityId = _factionRallyEntityIds[agent.FactionId];
        if (rallyEntityId == 0 ||
            !_agentIndexes.TryGetValue(rallyEntityId, out var rallyIndex))
        {
            return null;
        }

        var rallyAgent = _agentStates[rallyIndex];
        if (!rallyAgent.IsAlive)
        {
            return null;
        }

        var direction = ComputeRallyDirection(rallyAgent);

        if (direction.DistanceRaw > 0 &&
            TryComputeGiveWayAimPoint(agent, rallyAgent, direction) is
            { XRaw: var giveWayXRaw, YRaw: var giveWayYRaw })
        {
            return (giveWayXRaw, giveWayYRaw, rallyAgent.EntityId);
        }

        var trailRaw = FormationRules.ComputeRallyTrailRaw(Scenario.BodyRadiusRaw);
        var (trailBaseXRaw, trailBaseYRaw) = ComputeRallyTrailBase(
            rallyAgent,
            direction,
            trailRaw);

        var (offsetXRaw, offsetYRaw) = RallyOffset.Compute(
            Scenario.Seed,
            agent.EntityId,
            Scenario.BodyRadiusRaw,
            _collision.StallGeneration(agentIndex));

        // The aim point is computed in long and saturated into int the same
        // way CollisionResolver.ToCoordinate saturates a candidate coordinate
        // before its own boundary clamp: safe, because ClampCenterToBounds
        // immediately pulls anything outside the map back to the edge.
        var mapWidthRaw = checked(Scenario.MapWidth * FixedPoint.Scale);
        var mapHeightRaw = checked(Scenario.MapHeight * FixedPoint.Scale);
        var aimXRaw = CollisionGeometry.ClampCenterToBounds(
            SaturateToInt32(checked((long)trailBaseXRaw + offsetXRaw)),
            mapWidthRaw,
            Scenario.BodyRadiusRaw);
        var aimYRaw = CollisionGeometry.ClampCenterToBounds(
            SaturateToInt32(checked((long)trailBaseYRaw + offsetYRaw)),
            mapHeightRaw,
            Scenario.BodyRadiusRaw);

        var squaredDistanceToAim = CollisionGeometry.SquaredDistance(
            agent.XRaw,
            agent.YRaw,
            aimXRaw,
            aimYRaw);
        if (squaredDistanceToAim <=
            CollisionGeometry.ContactSquaredDistance(Scenario.BodyRadiusRaw))
        {
            return null;
        }

        // The event log names the rally agent as the target, not the enemy
        // the follower would otherwise be chasing, so a spectator reads
        // "entity N moved toward entity <rally>" during a last stand.
        return (aimXRaw, aimYRaw, rallyAgent.EntityId);
    }

    /// <summary>
    /// Computes a leader's direction of travel — the vector from the leader
    /// to its own enemy target, plus the integer distance between them —
    /// used both by <see cref="ComputeRallyTrailBase"/> and by
    /// <see cref="TryComputeGiveWayAimPoint"/> for the corridor test. Returns
    /// a zero <c>DistanceRaw</c> sentinel, meaning "no direction", when the
    /// leader has no target, when the target cannot be resolved to a living
    /// agent, or when the leader is already exactly at its target's
    /// position.
    /// </summary>
    /// <remarks>
    /// Generalised from the last-stand rally agent to any leader: the rally
    /// path calls this with the faction's rally agent, and the
    /// persistent-contingent cohesion path (design section 3.5) calls it
    /// with a contingent's own leader. Neither this method nor the two below
    /// it read the faction rally state directly.
    /// </remarks>
    private (long DeltaXRaw, long DeltaYRaw, long DistanceRaw) ComputeRallyDirection(
        AgentState leader)
    {
        if (leader.TargetEntityId is not { } targetId ||
            !_agentIndexes.TryGetValue(targetId, out var targetIndex))
        {
            return (0, 0, 0);
        }

        var target = _agentStates[targetIndex];
        var deltaXRaw = (long)target.XRaw - leader.XRaw;
        var deltaYRaw = (long)target.YRaw - leader.YRaw;
        var distanceRaw = IntegerSquareRoot(
            checked((deltaXRaw * deltaXRaw) + (deltaYRaw * deltaYRaw)));

        return (deltaXRaw, deltaYRaw, distanceRaw);
    }

    /// <summary>
    /// Computes the point <paramref name="trailRaw"/> raw units behind the
    /// leader, opposite the leader's own direction of travel — the point a
    /// follower's jitter offset is added to. Falls back to the leader's raw
    /// position (no trail) when <paramref name="direction"/> carries the "no
    /// direction" sentinel (<c>DistanceRaw == 0</c>), since there is no
    /// direction of travel to trail behind. That fallback preserves the
    /// pre-fix behaviour in that corner, which is otherwise untouched by
    /// this method's own logic.
    /// </summary>
    /// <remarks>
    /// The trail distance is a parameter rather than computed here so this
    /// method has no opinion on which formula produced it: the last-stand
    /// rally caller passes <see cref="FormationRules.ComputeRallyTrailRaw"/>
    /// and the persistent-contingent cohesion caller (design section 3.5)
    /// passes <see cref="FormationRules.ComputeContingentTrailRaw"/>.
    /// </remarks>
    private (int XRaw, int YRaw) ComputeRallyTrailBase(
        AgentState leader,
        (long DeltaXRaw, long DeltaYRaw, long DistanceRaw) direction,
        int trailRaw)
    {
        if (direction.DistanceRaw == 0)
        {
            return (leader.XRaw, leader.YRaw);
        }

        var trailXRaw = SaturateToInt32(checked(
            leader.XRaw - (direction.DeltaXRaw * trailRaw / direction.DistanceRaw)));
        var trailYRaw = SaturateToInt32(checked(
            leader.YRaw - (direction.DeltaYRaw * trailRaw / direction.DistanceRaw)));

        return (trailXRaw, trailYRaw);
    }

    /// <summary>
    /// Checks whether a regrouping follower's tick-start position falls
    /// inside its own leader's forward give-way corridor and, if so, returns
    /// a pure-sideways aim point that clears it. Returns
    /// <see langword="null"/> when the follower is not in the corridor, in
    /// which case <see cref="BuildRegroupingProposal"/> falls back to the
    /// ordinary trail-plus-jitter aim point.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Let <c>L</c> be the leader's tick-start position, <c>d</c> the unit
    /// direction from <c>L</c> toward the leader's own target (the same
    /// direction <see cref="ComputeRallyTrailBase"/> trails behind), <c>F</c>
    /// the follower's tick-start position, and <c>r = F - L</c>.
    /// <c>forward</c> is the scalar projection of <c>r</c> onto <c>d</c>;
    /// <c>lateral</c> is the scalar projection of <c>r</c> onto the
    /// perpendicular of <c>d</c>. The follower is in the corridor when
    /// <c>forward &gt; 0</c> (ahead of the leader) and
    /// <c>|lateral| &lt; corridor half-width</c>.
    /// </para>
    /// <para>
    /// The escape point is <c>F</c> plus a step purely along the perpendicular
    /// of <c>d</c>, signed toward the side the follower is already on
    /// (<c>sign(lateral)</c>) so the step can only move it further from the
    /// corridor centre, never across it. A follower sitting exactly on the
    /// leader's axis (<c>lateral == 0</c>) always steps toward the same,
    /// fixed perpendicular side — never decided by iteration or array order.
    /// Because the step has no component along <c>d</c>, the follower's
    /// forward position is unchanged: this is sideways motion only, so the
    /// follower can never re-enter the corridor by taking this step.
    /// </para>
    /// </remarks>
    private (int XRaw, int YRaw)? TryComputeGiveWayAimPoint(
        AgentState agent,
        AgentState leader,
        (long DeltaXRaw, long DeltaYRaw, long DistanceRaw) direction)
    {
        var relativeXRaw = (long)agent.XRaw - leader.XRaw;
        var relativeYRaw = (long)agent.YRaw - leader.YRaw;

        var forwardRaw = checked(
            (relativeXRaw * direction.DeltaXRaw) +
            (relativeYRaw * direction.DeltaYRaw)) / direction.DistanceRaw;
        if (forwardRaw <= 0)
        {
            return null;
        }

        var lateralRaw = checked(
            (relativeXRaw * direction.DeltaYRaw) -
            (relativeYRaw * direction.DeltaXRaw)) / direction.DistanceRaw;
        var corridorHalfWidthRaw =
            FormationRules.ComputeRallyCorridorHalfWidthRaw(Scenario.BodyRadiusRaw);
        if (Math.Abs(lateralRaw) >= corridorHalfWidthRaw)
        {
            return null;
        }

        // Tie-break: exactly on the leader's axis always steps toward the
        // fixed "+" perpendicular side, so the escape direction never
        // depends on which side of zero a rounding error happened to land
        // on, or on the order agents were supplied in.
        var perpendicularSign = lateralRaw < 0 ? -1L : 1L;
        var stepOutRaw = checked(corridorHalfWidthRaw + Scenario.BodyRadiusRaw);

        var aimXRaw = SaturateToInt32(checked(
            agent.XRaw +
            (perpendicularSign * direction.DeltaYRaw * stepOutRaw /
                direction.DistanceRaw)));
        var aimYRaw = SaturateToInt32(checked(
            agent.YRaw -
            (perpendicularSign * direction.DeltaXRaw * stepOutRaw /
                direction.DistanceRaw)));

        var mapWidthRaw = checked(Scenario.MapWidth * FixedPoint.Scale);
        var mapHeightRaw = checked(Scenario.MapHeight * FixedPoint.Scale);
        aimXRaw = CollisionGeometry.ClampCenterToBounds(
            aimXRaw,
            mapWidthRaw,
            Scenario.BodyRadiusRaw);
        aimYRaw = CollisionGeometry.ClampCenterToBounds(
            aimYRaw,
            mapHeightRaw,
            Scenario.BodyRadiusRaw);

        return (aimXRaw, aimYRaw);
    }

    /// <summary>
    /// Saturates a long coordinate into <see cref="int"/>, mirroring
    /// <c>CollisionResolver.ToCoordinate</c>. Safe because the caller always
    /// runs the result through <see cref="CollisionGeometry.ClampCenterToBounds"/>
    /// immediately afterward.
    /// </summary>
    private static int SaturateToInt32(long valueRaw) =>
        (int)Math.Clamp(valueRaw, int.MinValue, int.MaxValue);

    /// <summary>
    /// Hands every living agent to the solid-disc resolver. Agents without a
    /// proposal are still submitted: they occupy space, and a stationary body
    /// would otherwise have its ground taken by a mover resolved before it.
    /// Each mover carries this tick's contested-ground priority, which is a
    /// pure hash of the seed, the tick, and the agent rather than a draw from
    /// any stream.
    /// </summary>
    private void ResolveCollisions()
    {
        _collision.BeginTick();

        for (var index = 0; index < _agentStates.Length; index++)
        {
            var agent = _agentStates[index];
            if (!agent.IsAlive)
            {
                continue;
            }

            var proposal = _movementProposals[index];
            _collision.Requests.Add(
                new CollisionMoveRequest(
                    agent.EntityId,
                    agent.XRaw,
                    agent.YRaw,
                    proposal?.XRaw ?? agent.XRaw,
                    proposal?.YRaw ?? agent.YRaw,
                    proposal is not null,
                    // Only a mover is ordered by its key, so a standing agent
                    // does not pay for a mix it will never be sorted by.
                    proposal is null
                        ? 0
                        : CollisionPriority.Resolve(
                            Scenario.Seed,
                            Tick,
                            agent.EntityId)));
        }

        _collision.Resolver.Resolve(_collision.Requests);
    }

    /// <summary>
    /// The single position commit for the tick. Resolver results are ordered to
    /// match the living agents in ascending entity ID, which is the same order
    /// they were submitted in.
    /// </summary>
    private void CommitMovement(List<BattleEvent> events)
    {
        var results = _collision.Resolver.Results;
        var resultIndex = 0;
        var commitsRetainedPace = _movementRules.UsesEquipmentRelativeFootwork;

        for (var index = 0; index < _agentStates.Length; index++)
        {
            var agent = _agentStates[index];
            if (!agent.IsAlive)
            {
                agent.MovementResolution = MovementResolution.None;
                _collision.RecordBlocked(index, isBlocked: false);
                continue;
            }

            var result = results[resultIndex];
            resultIndex++;

            var previousX = agent.XRaw;
            var previousY = agent.YRaw;
            agent.XRaw = result.XRaw;
            agent.YRaw = result.YRaw;
            agent.MovementResolution = result.Resolution;
            _collision.RecordBlocked(
                index,
                result.Resolution == MovementResolution.Blocked);

            var deltaX = (long)agent.XRaw - previousX;
            var deltaY = (long)agent.YRaw - previousY;
            if (deltaX == 0 && deltaY == 0)
            {
                if (commitsRetainedPace)
                {
                    // Design section 6.5: a blocked, rejected, or refused
                    // move leaves zero retained pace rather than fictitious
                    // momentum. Turning in place is still permitted, because
                    // facing committed independently during gathering.
                    agent.MovementPaceRaw = 0;
                }

                continue;
            }

            var movedRaw = checked((int)IntegerSquareRoot(
                checked((deltaX * deltaX) + (deltaY * deltaY))));
            if (commitsRetainedPace)
            {
                // Design section 6.5: the retained pace commits as the
                // smaller of the pace this tick's proposal was built on and
                // the distance the collision stage actually granted.
                agent.MovementPaceRaw =
                    Math.Min(_proposedPaceRaw[index], movedRaw);
            }

            AddEvent(
                events,
                BattleEventKind.Move,
                agent.EntityId,
                _movementProposals[index]?.TargetId,
                movedRaw,
                agent.FactionId);
        }
    }

    /// <summary>
    /// Derives this tick's collision counters from committed positions. Pure
    /// observation: nothing here writes agent state, and none of it is hashed.
    /// </summary>
    private void MeasureCollision()
    {
        _collision.Bodies.Clear();
        foreach (var agent in _agentStates)
        {
            _collision.Bodies.Add(
                new CollisionBody(
                    agent.EntityId,
                    agent.XRaw,
                    agent.YRaw,
                    agent.IsAlive));
        }

        _collision.Grid.Rebuild(
            _collision.Bodies,
            _collision.ContactBandRadiusRaw);

        var contactDistanceRaw = checked(2 * Scenario.BodyRadiusRaw);
        var contactPairs = 0;
        var penetrationRaw = 0;
        var minimumX = int.MaxValue;
        var maximumX = int.MinValue;
        var minimumY = int.MaxValue;
        var maximumY = int.MinValue;

        foreach (var pair in _collision.Grid.PairsList)
        {
            var left = _agentStates[_agentIndexes[pair.LowEntityId]];
            var right = _agentStates[_agentIndexes[pair.HighEntityId]];
            var separationRaw = checked((int)IntegerSquareRoot(
                CollisionGeometry.SquaredDistance(
                    left.XRaw,
                    left.YRaw,
                    right.XRaw,
                    right.YRaw)));
            penetrationRaw = Math.Max(
                penetrationRaw,
                contactDistanceRaw - separationRaw);

            if (left.FactionId == right.FactionId)
            {
                continue;
            }

            contactPairs++;
        }

        // The front spans agents holding an enemy in reach, not strictly
        // touching bodies. The resolver leaves every living pair at or beyond
        // the contact distance, so strict touching means a squared distance of
        // exactly (2R)^2 — a Pythagorean coincidence on an integer lattice — and
        // a span built on it would read zero through an entire battle. Contact
        // pairs are counted separately, over a proximity band.
        var attackCapableAgents = 0;
        foreach (var agent in _agentStates)
        {
            if (!agent.IsAlive || agent.TargetEntityId is not { } targetId)
            {
                continue;
            }

            var target = _agentStates[_agentIndexes[targetId]];
            if (!target.IsAlive || !IsWithinAttackRange(agent, target))
            {
                continue;
            }

            attackCapableAgents++;
            minimumX = Math.Min(minimumX, agent.XRaw);
            maximumX = Math.Max(maximumX, agent.XRaw);
            minimumY = Math.Min(minimumY, agent.YRaw);
            maximumY = Math.Max(maximumY, agent.YRaw);
        }

        _lastTickCollision = new CollisionTickMetrics(
            _collision.Grid.Pairs.Count,
            contactPairs,
            _collision.Resolver.AcceptedMoveCount,
            _collision.Resolver.BlockedCount,
            attackCapableAgents,
            attackCapableAgents == 0 ? 0 : checked(maximumY - minimumY),
            attackCapableAgents == 0 ? 0 : checked(maximumX - minimumX),
            penetrationRaw);
    }

    private void GatherAndCommitAttacks(List<BattleEvent> events)
    {
        Array.Clear(_damageTotals);
        var proposalCount = 0;
        var marksAcceptedAttackers = _movementRules.UsesEquipmentRelativeFootwork;
        if (marksAcceptedAttackers)
        {
            // Reusable scratch (design section 9.6): one bit per agent, set
            // on the accept path below after the existing prechecks pass,
            // read by ApplyEquipmentAttackFootworkAndDeathCleanup after this
            // method returns, and never hashed or snapshotted.
            Array.Clear(_attackAcceptedThisTick);
        }

        // Derived observability counters for this tick. Locals rather than
        // state, folded into the reported value once at the end of the loop,
        // so nothing here can be read by the simulation itself.
        //
        // Each counter is indexed by the attacking agent's faction so the same
        // loop yields both the undivided total and the per-faction split, with
        // no second pass and no new query. Two factions exist, so these are
        // fixed-size stack arrays; the totals are summed at the end rather than
        // tracked separately, which makes the split a partition by
        // construction rather than by assertion.
        Span<int> accepted = stackalloc int[FactionCount];
        Span<int> landed = stackalloc int[FactionCount];
        Span<int> shieldBlocked = stackalloc int[FactionCount];
        Span<int> parried = stackalloc int[FactionCount];
        Span<int> deflected = stackalloc int[FactionCount];
        Span<int> evaded = stackalloc int[FactionCount];

        // Pass A0 (ranged-units plan RU-17): advance every pooled
        // projectile's flight countdown and resolve any arrival, before the
        // melee gather loop below even starts. A projectile's clash and
        // hit-location roll folds its LAUNCH tick, not this arrival tick, so
        // a shot's outcome was already fixed the instant it left its
        // weapon; only whether the result is visible yet was ever in
        // question. Order-preserving compaction: a projectile that survives
        // is copied down to close the gap left by one that resolved this
        // tick, exactly as CollisionScratch's own compaction preserves
        // order elsewhere. _projectileLiveCount is zero, and this loop a
        // single no-op comparison, under any ruleset that fields no ranged
        // weapon.
        var projectileWriteIndex = 0;
        for (var readIndex = 0; readIndex < _projectileLiveCount; readIndex++)
        {
            var projectile = _projectiles[readIndex];
            var ticksRemaining = projectile.TicksRemaining - 1;
            if (ticksRemaining > 0)
            {
                _projectiles[projectileWriteIndex] =
                    projectile with { TicksRemaining = ticksRemaining };
                projectileWriteIndex++;
                continue;
            }

            // Arrived. The launching warrior's FactionId, Loadout, and
            // EntityId never change once created, even after death, so
            // reading them here from a source that may itself have died
            // since launch is exactly as safe as the shared code below
            // already treats a melee source.
            var arrivalSourceIndex = _agentIndexes[projectile.SourceEntityId];
            var arrivalSource = _agentStates[arrivalSourceIndex];
            var arrivalTargetIndex = _agentIndexes[projectile.TargetEntityId];
            var arrivalTarget = _agentStates[arrivalTargetIndex];

            if (!arrivalTarget.IsAlive)
            {
                // Phase 1's only miss: the recorded target died between
                // launch and arrival. This shot produces no accepted attack,
                // so it never enters _attackProposals.
                AddEvent(
                    events,
                    BattleEventKind.Miss,
                    projectile.SourceEntityId,
                    projectile.TargetEntityId,
                    0,
                    arrivalSource.FactionId);
                continue;
            }

            var arrivalHitLocation = HitLocationResolver.Resolve(
                _rules,
                arrivalSource.Loadout,
                arrivalTarget.Loadout,
                Scenario.Seed,
                projectile.LaunchTick,
                projectile.SourceEntityId,
                projectile.TargetEntityId);
            var arrivalResolution = ClashResolver.Resolve(
                _rules.ClashProfile,
                Scenario.Seed,
                projectile.LaunchTick,
                projectile.SourceEntityId,
                projectile.TargetEntityId,
                arrivalSource.Loadout.Weapon,
                arrivalTarget.Loadout.Weapon,
                arrivalTarget.Loadout.Shield);

            // An impact needs no new BattleEventKind: it is buffered here
            // exactly as a melee blow is, and pass B below emits it as an
            // ordinary Attack event carrying weapon, shield, hit location,
            // and resolution. Ranged weapons never open a combo chain
            // (PhilippineCombatPresetV5.RangedProfile fixes their
            // combo-open chance at zero), so the buffered combo position is
            // unconditionally null rather than routed through
            // ResolveComboTransition, which would also incorrectly rewrite
            // the cooldown this shot already charged at launch.
            _attackProposals[proposalCount] = (
                arrivalSourceIndex,
                arrivalTargetIndex,
                arrivalHitLocation,
                arrivalResolution,
                null);
            proposalCount++;

            // The damage recorded at launch, not the launcher's current
            // DamagePerAttack, so a later loadout or preset change could
            // never retroactively alter a shot already in flight. The two
            // are bit-identical for every agent CreateAgent ever produces —
            // AgentState.DamagePerAttack is a get-only property, written
            // once at spawn — so this is not a behavioural difference today,
            // only which of two equal sources the code commits to.
            if (arrivalResolution == AttackResolution.Landed)
            {
                _damageTotals[arrivalTargetIndex] = checked(
                    _damageTotals[arrivalTargetIndex] + projectile.DamageAtLaunch);
            }

            // Credited to the launcher's faction, in the same shape the
            // melee loop below credits its own resolutions, so the impact
            // tick — not the launch tick — is what counts toward this
            // tick's CombatMetrics.
            var arrivalFaction = arrivalSource.FactionId;
            accepted[arrivalFaction]++;
            switch (arrivalResolution)
            {
                case AttackResolution.Landed:
                    landed[arrivalFaction]++;
                    break;
                case AttackResolution.ShieldBlocked:
                    shieldBlocked[arrivalFaction]++;
                    break;
                case AttackResolution.Parried:
                    parried[arrivalFaction]++;
                    break;
                case AttackResolution.Deflected:
                    deflected[arrivalFaction]++;
                    break;
                default:
                    evaded[arrivalFaction]++;
                    break;
            }
        }

        _projectileLiveCount = projectileWriteIndex;

        for (var sourceIndex = 0;
             sourceIndex < _agentStates.Length;
             sourceIndex++)
        {
            var source = _agentStates[sourceIndex];
            if (!source.IsAlive)
            {
                continue;
            }

            // Plan section 3(a): a chaining attacker's pre-check runs ahead
            // of the cooldown check below, not after it, so that an attacker
            // still mid-cooldown discovers a dead or out-of-reach target — and
            // clears its chain — on the very tick that becomes true, rather
            // than only once its own cooldown next lets it act.
            if (source.TargetEntityId is not { } targetId)
            {
                ClearActiveComboChain(source);
                continue;
            }

            var target = _agentStates[_agentIndexes[targetId]];
            if (!target.IsAlive)
            {
                ClearActiveComboChain(source);
                continue;
            }

            if (!IsWithinAttackRange(source, target))
            {
                ClearActiveComboChain(source);
                continue;
            }

            if (source.AttackCooldownRemaining != 0)
            {
                continue;
            }

            // Ranged-units plan RU-17: a ranged weapon launches a
            // projectile here instead of resolving immediately. The
            // precheck ladder above -- alive, has a target, target alive,
            // within reach, cooldown ready -- is unchanged and load-bearing
            // for both weapon families; only what happens once every
            // precheck has passed differs. Phase 1: a projectile passes
            // through allies and through every enemy but its target -- there
            // is no line of sight and no interception.
            var weaponProfile = ResolveAttackerWeaponProfile(source.Loadout);
            if (weaponProfile.StandoffDistanceRaw != 0)
            {
                if (_projectileLiveCount >= Scenario.MaximumProjectilesInFlight)
                {
                    // Launch at the ceiling is refused outright: the shot
                    // does not occur, the cooldown is not charged, and the
                    // refusal is counted so it is visible rather than
                    // silently dropped.
                    _projectileLaunchRefusals =
                        checked(_projectileLaunchRefusals + 1);
                    continue;
                }

                source.Intent = AgentIntent.Attacking;
                _projectiles[_projectileLiveCount] = new Projectile(
                    source.EntityId,
                    target.EntityId,
                    Tick,
                    weaponProfile.FlightTickCeiling,
                    source.XRaw,
                    source.YRaw,
                    source.Loadout.Weapon,
                    weaponProfile.DamagePerAttack);
                _projectileLiveCount++;

                // The cooldown charges on launch, not on arrival, mirroring
                // the existing non-landed-attack cooldown-reset behaviour.
                // Every ranged weapon this preset family declares fixes its
                // combo-open chance at zero
                // (PhilippineCombatPresetV5.RangedProfile), so it can never
                // open a chain and this writes the normal cooldown directly
                // rather than routing through ResolveComboTransition.
                source.AttackCooldownRemaining = source.AttackCooldownTicks;

                AddEvent(
                    events,
                    BattleEventKind.Release,
                    source.EntityId,
                    target.EntityId,
                    weaponProfile.FlightTickCeiling,
                    source.FactionId);

                if (marksAcceptedAttackers)
                {
                    _attackAcceptedThisTick[sourceIndex] = true;
                }

                continue;
            }

            // 3(b): resolve the attack. Unchanged from before this feature —
            // the cooldown write that used to happen here moves below, after
            // the combo transition, because which cooldown to write now
            // depends on the combo outcome.
            source.Intent = AgentIntent.Attacking;
            var targetIndex = _agentIndexes[target.EntityId];
            var hitLocation = HitLocationResolver.Resolve(
                _rules,
                source.Loadout,
                target.Loadout,
                Scenario.Seed,
                Tick,
                source.EntityId,
                target.EntityId);

            // Resolved inline, immediately after the hit location, in the same
            // pass. A second pass over the proposals would be a second place
            // the attack tuple has to stay consistent, and a per-target buffer
            // would be state whose staleness nothing checks. The clash costs no
            // draw from any generator, so nothing downstream shifts merely
            // because this call was added.
            var resolution = ClashResolver.Resolve(
                _rules.ClashProfile,
                Scenario.Seed,
                Tick,
                source.EntityId,
                target.EntityId,
                source.Loadout.Weapon,
                target.Loadout.Weapon,
                target.Loadout.Shield);

            var comboPosition = ResolveComboTransition(source, target, resolution);

            _attackProposals[proposalCount] =
                (sourceIndex, targetIndex, hitLocation, resolution, comboPosition);
            proposalCount++;
            if (marksAcceptedAttackers)
            {
                // Design section 9.6: the accept path is the only place an
                // attack is known to have passed every precheck, so the
                // scratch bit is set here, alongside the proposal buffering,
                // and the accumulation, damage, cooldown, combo, and event
                // work below continues unchanged.
                _attackAcceptedThisTick[sourceIndex] = true;
            }

            // Only a landed blow reaches the damage total. Every other
            // resolution still emitted its attack event above and still burned
            // the attacker's cooldown; it simply carries no damage.
            if (resolution == AttackResolution.Landed)
            {
                _damageTotals[targetIndex] = checked(
                    _damageTotals[targetIndex] + source.DamagePerAttack);
            }

            // Credited to the attacker's faction. Every accepted attack has
            // exactly one attacker, so this is a partition of proposalCount.
            var attackerFaction = source.FactionId;
            accepted[attackerFaction]++;

            switch (resolution)
            {
                case AttackResolution.Landed:
                    landed[attackerFaction]++;
                    break;
                case AttackResolution.ShieldBlocked:
                    shieldBlocked[attackerFaction]++;
                    break;
                case AttackResolution.Parried:
                    parried[attackerFaction]++;
                    break;
                case AttackResolution.Deflected:
                    deflected[attackerFaction]++;
                    break;
                default:
                    evaded[attackerFaction]++;
                    break;
            }
        }

        _lastTickCombatByFaction = new FactionCombatMetrics(
            new CombatMetrics(
                accepted[0],
                landed[0],
                shieldBlocked[0],
                parried[0],
                deflected[0],
                evaded[0]),
            new CombatMetrics(
                accepted[1],
                landed[1],
                shieldBlocked[1],
                parried[1],
                deflected[1],
                evaded[1]));

        // Derived from the split rather than counted separately, so the two can
        // never disagree. proposalCount is asserted equal to the summed
        // accepted count below: it is maintained by the loop for indexing
        // _attackProposals, so it is an independent witness that the per-faction
        // credit above missed nothing.
        _lastTickCombat = _lastTickCombatByFaction.Total;

        if (_lastTickCombat.AcceptedAttacks != proposalCount)
        {
            throw new InvalidOperationException(
                "Per-faction attack accounting lost or duplicated an accepted " +
                "attack: the faction split sums to " +
                $"{_lastTickCombat.AcceptedAttacks} but {proposalCount} " +
                "attacks were resolved this tick.");
        }

        for (var index = 0; index < proposalCount; index++)
        {
            var proposal = _attackProposals[index];
            var source = _agentStates[proposal.SourceIndex];
            var target = _agentStates[proposal.TargetIndex];
            AddAttackEvent(
                events,
                source.EntityId,
                target.EntityId,
                proposal.Resolution == AttackResolution.Landed
                    ? source.DamagePerAttack
                    : 0,
                source.FactionId,
                source.Loadout.Weapon,
                source.Loadout.Shield,
                proposal.HitLocation,
                proposal.Resolution,
                proposal.ComboPosition);
        }

        for (var index = 0; index < _damageTotals.Length; index++)
        {
            var damage = _damageTotals[index];
            if (damage == 0)
            {
                continue;
            }

            var target = _agentStates[index];
            target.HitPoints = Math.Max(0, target.HitPoints - damage);
            AddEvent(
                events,
                BattleEventKind.Damage,
                target.EntityId,
                target.EntityId,
                damage,
                target.FactionId);
        }

        for (var index = 0; index < _damageTotals.Length; index++)
        {
            var agent = _agentStates[index];
            if (_damageTotals[index] == 0 || agent.IsAlive)
            {
                continue;
            }

            agent.TargetEntityId = null;
            agent.Intent = AgentIntent.Dead;
            AddEvent(
                events,
                BattleEventKind.Death,
                agent.EntityId,
                null,
                0,
                agent.FactionId);
        }
    }

    /// <summary>
    /// Plan section 3(c): the full combo-transition algorithm, run
    /// immediately after one attack attempt's <see cref="AttackResolution"/>
    /// is known and immediately before the attack proposal is buffered.
    /// Mutates <paramref name="source"/>'s
    /// <see cref="AgentState.ComboStepsRemaining"/> and
    /// <see cref="AgentState.ComboTargetEntityId"/>, writes the cooldown this
    /// blow earns into <see cref="AgentState.AttackCooldownRemaining"/> —
    /// replacing the unconditional pre-resolution write this preset family
    /// used before attack combinations existed — and returns this attack
    /// event's chain-position value, or <c>null</c> when the blow is not
    /// part of a chain.
    /// </summary>
    /// <remarks>
    /// The six numbered steps below are plan section 3(c)'s six numbered
    /// steps, in the same order, and must not be reordered or simplified:
    /// this is the determinism-critical core of the feature.
    /// <para>
    /// Every branch below that writes the plan's "normal" (non-combo)
    /// cooldown reads it from <see cref="AgentState.AttackCooldownTicks"/>
    /// rather than from <c>weaponProfile.AttackCooldownTicks</c>. The two are
    /// bit-identical for every agent <see cref="CreateAgent"/> ever produces
    /// — both are the same ruleset lookup against the same immutable
    /// loadout — so this is not a behavioural choice, only which of two
    /// equal sources to read. <see cref="AgentState.AttackCooldownTicks"/>
    /// is preferred because it is the single cached value every other tick
    /// stage already reads (see <see cref="CreateAgent"/>'s remarks on why a
    /// second reach path must never exist), rather than opening a second,
    /// redundant profile lookup that could only ever disagree with it by
    /// construction error.
    /// </para>
    /// </remarks>
    private int? ResolveComboTransition(
        AgentState source,
        AgentState target,
        AttackResolution resolution)
    {
        var weaponProfile = ResolveAttackerWeaponProfile(source.Loadout);

        // Step 1.
        var wasChaining = source.ComboStepsRemaining > 0;

        // Step 2: Question 1's strict target-binding check. The retarget, if
        // any, already happened earlier this same tick in
        // SelectTargetsAndIntents.
        var retargeted =
            wasChaining && source.ComboTargetEntityId != source.TargetEntityId;

        // Step 3.
        if (retargeted)
        {
            source.ComboStepsRemaining = 0;
            source.ComboTargetEntityId = null;
            wasChaining = false;
        }

        int? comboPosition;
        int cooldown;

        if (resolution != AttackResolution.Landed)
        {
            // Step 4: a non-landed attempt is neither a roll nor a break.
            // ComboStepsRemaining and ComboTargetEntityId are left exactly as
            // they are post step 3.
            comboPosition = null;
            cooldown = wasChaining
                ? weaponProfile.ComboCooldownTicks
                : source.AttackCooldownTicks;
        }
        else if (!wasChaining)
        {
            // Step 5: an unchained landed blow, eligible to open a chain.
            var openRoll = (int)(ComboResolver.MixCombo(
                Scenario.Seed,
                Tick,
                source.EntityId,
                target.EntityId,
                source.Loadout.Weapon,
                comboStepsRemaining: 0,
                ComboResolver.ComboOpenTag) % ClashProfile.BasisPointScale);

            if (openRoll < weaponProfile.ComboOpenChanceBasisPoints)
            {
                var maxSteps = Math.Min(source.Level, weaponProfile.ComboMaxSteps);
                comboPosition = 1;
                source.ComboStepsRemaining = maxSteps - 1;
                source.ComboTargetEntityId = target.EntityId;
                cooldown = source.ComboStepsRemaining > 0
                    ? weaponProfile.ComboCooldownTicks
                    : source.AttackCooldownTicks;
            }
            else
            {
                comboPosition = null;
                cooldown = source.AttackCooldownTicks;
            }
        }
        else
        {
            // Step 6: a continuing blow candidate.
            var maxSteps = Math.Min(source.Level, weaponProfile.ComboMaxSteps);
            var thisPosition = maxSteps - source.ComboStepsRemaining + 1;
            var continueRoll = (int)(ComboResolver.MixCombo(
                Scenario.Seed,
                Tick,
                source.EntityId,
                target.EntityId,
                source.Loadout.Weapon,
                source.ComboStepsRemaining,
                ComboResolver.ComboContinueTag) % ClashProfile.BasisPointScale);
            var continuationSucceeded =
                continueRoll < weaponProfile.ComboContinueChanceBasisPoints;

            // Mirrors however _damageTotals's accumulation reads
            // target.HitPoints for this same purpose: against the value as
            // it stands before the end-of-pass-1 damage-application loop
            // applies it, not adjusted for any other attacker's damage
            // accumulated this same tick.
            var killedByThisBlow =
                target.HitPoints - source.DamagePerAttack <= 0;

            // The blow landed, so it always counts, regardless of what
            // happens next.
            comboPosition = thisPosition;

            // Design 3.2's exact order: check 1 (continuation), check 2 (max
            // length reached), check 3 (the target dies on this blow). Check
            // 4 ("target out of reach") cannot fire here — the pre-check
            // above already guaranteed the target is in range for this
            // tick's attempt — it fires on a later tick via that pre-check's
            // clearing clause instead.
            var chainSurvives = continuationSucceeded &&
                thisPosition < maxSteps &&
                !killedByThisBlow;

            if (chainSurvives)
            {
                // Guaranteed > 0 here, since thisPosition < maxSteps was
                // just confirmed.
                source.ComboStepsRemaining -= 1;
                cooldown = weaponProfile.ComboCooldownTicks;
            }
            else
            {
                source.ComboStepsRemaining = 0;
                source.ComboTargetEntityId = null;
                cooldown = source.AttackCooldownTicks;
            }
        }

        // One of exactly two non-decrement writers of
        // AgentState.AttackCooldownRemaining. The other is the V7 pressure
        // interrupt in ResolveEquipmentPosturesAndProvisionalFootwork, which
        // re-charges this same field from the movement stage under
        // MovementRuleset.AppliesPressureInterrupt and is therefore unreachable
        // under every preset from V1 through V6. That site carries the matching
        // comment and the argument for why the inversion is deliberate.
        source.AttackCooldownRemaining = cooldown;
        return comboPosition;
    }

    private void ResolveOutcome(List<BattleEvent> events)
    {
        var faction0Alive = false;
        var faction1Alive = false;

        foreach (var agent in _agentStates)
        {
            if (!agent.IsAlive)
            {
                continue;
            }

            if (agent.FactionId == 0)
            {
                faction0Alive = true;
            }
            else
            {
                faction1Alive = true;
            }
        }

        Outcome = (faction0Alive, faction1Alive) switch
        {
            (false, false) => BattleOutcome.Draw,
            (true, false) => BattleOutcome.Faction0Victory,
            (false, true) => BattleOutcome.Faction1Victory,
            _ when Tick >= Scenario.TickLimit => BattleOutcome.Draw,
            _ => BattleOutcome.Ongoing,
        };

        if (Outcome == BattleOutcome.Ongoing)
        {
            return;
        }

        var winningFaction = Outcome switch
        {
            BattleOutcome.Faction0Victory => 0,
            BattleOutcome.Faction1Victory => 1,
            _ => (int?)null,
        };
        AddEvent(
            events,
            BattleEventKind.Outcome,
            sourceEntityId: 0,
            targetEntityId: null,
            value: (int)Outcome,
            factionId: winningFaction);
    }

    /// <summary>
    /// Builds a movement proposal that closes an agent on an enemy it is
    /// fighting. Delegates to the point-taking overload, stopping short by
    /// one body diameter so bodies come to rest touching rather than
    /// overlapping — see the point-taking overload's remarks for why a rally
    /// point is handled differently.
    /// </summary>
    private (int XRaw, int YRaw, ulong TargetId) BuildMovementProposal(
        AgentState agent,
        AgentState target) =>
        BuildMovementProposal(
            agent,
            target.XRaw,
            target.YRaw,
            target.EntityId,
            // Agents close to body contact, not merely to weapon reach.
            // Stopping at reach left four world units of permanent air
            // between opposing front ranks, so bodies never touched and the
            // collision stage only ever saw allies queueing. Attacks still
            // resolve at reach, which is wider than the diameter, so a rank
            // pressed into contact fights and the rank behind it can reach
            // past.
            stopShortRaw: checked(2 * Scenario.BodyRadiusRaw));

    /// <summary>
    /// Builds a movement proposal toward an arbitrary destination point
    /// rather than another agent's live position. Used for last-stand rally
    /// movement, where a follower wants to actually reach its aim point
    /// rather than stop short of it the way it would closing on an enemy
    /// body — so this overload walks all the way in (<c>stopShortRaw: 0</c>)
    /// instead of duplicating the enemy-closing overload's stopping-distance
    /// arithmetic.
    /// </summary>
    private (int XRaw, int YRaw, ulong TargetId) BuildMovementProposal(
        AgentState agent,
        int destinationXRaw,
        int destinationYRaw,
        ulong targetId) =>
        BuildMovementProposal(
            agent,
            destinationXRaw,
            destinationYRaw,
            targetId,
            stopShortRaw: 0);

    private (int XRaw, int YRaw, ulong TargetId) BuildMovementProposal(
        AgentState agent,
        int destinationXRaw,
        int destinationYRaw,
        ulong targetId,
        int stopShortRaw) =>
        BuildMovementProposal(
            agent, destinationXRaw, destinationYRaw, targetId, stopShortRaw, out _);

    /// <summary>
    /// The full implementation every other overload delegates to. Identical
    /// arithmetic and the identical <see cref="CollisionGeometry.ClampCenterToBounds"/>
    /// call as before battlefield realism design section 5.5 — the only
    /// addition is capturing the pre-clamp centre so
    /// <paramref name="boundsClampChangedResult"/> can report whether the
    /// bounds clamp changed either coordinate, which the retreat builder
    /// below uses for its hazard-one check. Every pre-existing caller reaches
    /// this through the five-argument overload above, which discards the new
    /// output and is otherwise byte-identical to what it called before.
    /// </summary>
    private (int XRaw, int YRaw, ulong TargetId) BuildMovementProposal(
        AgentState agent,
        int destinationXRaw,
        int destinationYRaw,
        ulong targetId,
        int stopShortRaw,
        out bool boundsClampChangedResult)
    {
        var deltaX = (long)destinationXRaw - agent.XRaw;
        var deltaY = (long)destinationYRaw - agent.YRaw;
        var distanceSquared = checked((deltaX * deltaX) + (deltaY * deltaY));
        var distance = IntegerSquareRoot(distanceSquared);

        var desiredMovement = Math.Max(1, distance - stopShortRaw);
        // The arrival taper is on under every persistent-contingent preset,
        // and off only under IndependentPursuitV1, whose trajectory is frozen.
        // Testing against the frozen preset rather than naming each preset
        // that has the taper keeps a newly registered preset from silently
        // losing it.
        var movement = Scenario.MovementPreset != MovementPresetId.IndependentPursuitV1
            ? MovementRules.ComputeArrivalStepRaw(
                desiredMovement,
                agent.MovementSpeedRaw,
                checked((long)_movementRules.ArrivalTaperMultiplier * Scenario.BodyRadiusRaw))
            : Math.Min(agent.MovementSpeedRaw, desiredMovement);
        var moveX = checked(deltaX * movement / Math.Max(1, distance));
        var moveY = checked(deltaY * movement / Math.Max(1, distance));

        if (moveX == 0 && moveY == 0)
        {
            if (Math.Abs(deltaX) >= Math.Abs(deltaY))
            {
                moveX = Math.Sign(deltaX);
            }
            else
            {
                moveY = Math.Sign(deltaY);
            }
        }

        var unclampedNextX = checked(agent.XRaw + (int)moveX);
        var unclampedNextY = checked(agent.YRaw + (int)moveY);
        var nextX = CollisionGeometry.ClampCenterToBounds(
            unclampedNextX,
            checked(Scenario.MapWidth * FixedPoint.Scale),
            Scenario.BodyRadiusRaw);
        var nextY = CollisionGeometry.ClampCenterToBounds(
            unclampedNextY,
            checked(Scenario.MapHeight * FixedPoint.Scale),
            Scenario.BodyRadiusRaw);
        boundsClampChangedResult = nextX != unclampedNextX || nextY != unclampedNextY;
        return (nextX, nextY, targetId);
    }

    /// <summary>
    /// Battlefield realism design section 5.4's retreat builder: reflects the
    /// melee threat through the actor — <c>(2 * agentX - threatX, 2 * agentY
    /// - threatY)</c>, the point directly opposite the threat at the same
    /// distance — and reuses the ordinary paced, tapered, clamped
    /// movement-proposal arithmetic toward that reflected point. No new
    /// movement arithmetic and no new clamping path: this only supplies a
    /// different destination to the same <see cref="BuildMovementProposal(AgentState,int,int,ulong,int,out bool)"/>
    /// used everywhere else.
    /// </summary>
    /// <returns>
    /// <see langword="false"/>, writing no <paramref name="proposal"/>, when
    /// the bounds clamp changed either coordinate (design 5.5, hazard one) —
    /// the reflected destination would clamp to where the agent already
    /// stands, so the caller assigns <see cref="AgentIntent.Holding"/>
    /// instead of proposing a step that would silently do nothing.
    /// </returns>
    private bool TryBuildRetreatProposal(
        AgentState agent,
        int threatXRaw,
        int threatYRaw,
        ulong targetId,
        out (int XRaw, int YRaw, ulong TargetId) proposal)
    {
        var destinationXRaw = checked((2 * agent.XRaw) - threatXRaw);
        var destinationYRaw = checked((2 * agent.YRaw) - threatYRaw);

        proposal = BuildMovementProposal(
            agent,
            destinationXRaw,
            destinationYRaw,
            targetId,
            stopShortRaw: 0,
            out var boundsClampChanged);

        if (boundsClampChanged)
        {
            proposal = default;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Battlefield realism design section 5.3's threat position: the live
    /// position and id of the nearest living, opposing-faction, melee-weapon
    /// agent — the same candidate <c>SelectTargetsAndIntents</c> already
    /// reduced into <see cref="_nearestMeleeThreatSquared"/> by squared
    /// distance alone. That scratch keeps only the distance, not the
    /// position, so the retreat builder re-derives the position here. No
    /// perception-range re-test is needed: perception filtering only ever
    /// excludes a candidate whose distance exceeds
    /// <see cref="AgentState.PerceptionRangeRaw"/>, so any candidate at or
    /// inside the finite scratch distance that made this call reachable was
    /// already perceived, and a plain global minimum over "alive, opposing
    /// faction, melee weapon" reproduces exactly the entity that distance
    /// came from. Ties break on the lower <see cref="AgentState.EntityId"/>,
    /// matching every other multi-result query in this file. Throws if none
    /// is found — reachable only after
    /// <see cref="RangedRetreatRules.IsThreatened"/> has already read a
    /// finite scratch value for this agent, so a miss here is a defect in
    /// that invariant, never a real battlefield state.
    /// </summary>
    private (int XRaw, int YRaw, ulong EntityId) FindNearestMeleeThreatPosition(
        AgentState agent)
    {
        AgentState? nearest = null;
        var nearestSquared = long.MaxValue;
        foreach (var candidate in _agentStates)
        {
            if (!candidate.IsAlive || candidate.FactionId == agent.FactionId)
            {
                continue;
            }

            if (ResolveAttackerWeaponProfile(candidate.Loadout).StandoffDistanceRaw != 0)
            {
                continue;
            }

            var distance = SquaredDistance(agent, candidate);
            if (distance < nearestSquared ||
                (distance == nearestSquared &&
                    (nearest is null || candidate.EntityId < nearest.EntityId)))
            {
                nearest = candidate;
                nearestSquared = distance;
            }
        }

        if (nearest is null)
        {
            throw new InvalidOperationException(
                "No nearest melee threat was found for an agent already " +
                "reported as threatened.");
        }

        return (nearest.XRaw, nearest.YRaw, nearest.EntityId);
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
    /// <see cref="MovementPresetId.ContingentCohesionBeforeContactV14"/> is
    /// admitted for the same reason again: it is defined as a strict superset
    /// of <see cref="MovementPresetId.CohortLateralSpreadV13"/>'s behaviour, so
    /// it inherits all three battlefield-realism behaviours unchanged before
    /// the contingent-cohesion gate of its own is layered on top.
    /// </summary>
    private static bool UsesBattlefieldRealism(MovementPresetId preset) =>
        preset is MovementPresetId.BattlefieldRealismV10
            or MovementPresetId.LastStandEngagementV11
            or MovementPresetId.ContingentShapeV12
            or MovementPresetId.CohortLateralSpreadV13
            or MovementPresetId.ContingentCohesionBeforeContactV14;

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
