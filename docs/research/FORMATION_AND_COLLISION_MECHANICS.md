# Formation Tactics and Collision Mechanics for Hukbo

## Purpose

This document connects three different kinds of knowledge:

1. what the historical record supports about formations and crowding;
2. what spatial behavior can reasonably be inferred for a tactical simulation;
3. what Hukbo would need to invent as an explicit game rule.

It is research input for a Requirements Agent and a Task Planner Agent. It does
not select hard, soft, or faction-dependent collision behavior on the user's
behalf.

## Executive conclusion

Hukbo should not begin with named or rigid formation templates. The historical
research supports terrain-constrained frontage, local cooperation, irregular
spacing, contingent cohesion, and crowded close contact more strongly than it
supports any exact rank, file, or drill
([formations research](./battles/03-deep-past-formations-and-tactics.md)).
Collision rules should therefore create conditions from which local geometry
can emerge, rather than force agents into supposedly historical shapes.

The current simulation has deterministic fixed-point movement proposals but no
body radius or collision test. The safest engineering direction is:

1. define shared collision semantics and deterministic neighbor ordering;
2. prototype one hard-contact resolver and one bounded soft-contact resolver
   against the same scenario matrix;
3. choose between them using formation readability, crowd behavior,
   determinism, and performance evidence;
4. add anticipatory steering only if reactive contact resolution is visibly
   insufficient; and
5. defer ORCA unless Hukbo later needs smooth, collision-free traffic rather
   than crowded melee.

This is a recommendation about sequencing, not a decision about whether agents
must be solid.

## Evidence boundaries

The following labels are used throughout:

- **Historical evidence** — supported directly by the repository's cited
  archaeological, linguistic, or ethnohistorical sources.
- **Tactical simulation inference** — follows from geometry, bodies occupying
  space, weapon reach, terrain, or local coordination, but is not a recovered
  historical drill.
- **Game-design invention** — a rule chosen for clarity, determinism,
  performance, or play; it must not be presented as historical fact.

### What the history supports

The existing research establishes a deliberately narrow basis:

- elevated fortified sites and restricted approaches support the importance of
  position and limited frontage;
- boat transport supports practical contingents, not standardized naval units;
- spearheads and reconstructed spear-related vocabulary support weapon
  affordances, not a spear wall or volley drill;
- leader-centered clusters, access-point defense, loose missile-capable
  frontage, and close-contact clusters are safe simulation envelopes only; and
- no current evidence attests regular files, fixed rank depth, a shield wall, a
  phalanx-like block, a formal reserve, or a standardized archipelago-wide
  formation system.

Those conclusions and their primary historical bibliography are documented in
[Deep Past, Depth 3: Formations and Tactics](./battles/03-deep-past-formations-and-tactics.md).

### What collision can model without making a historical claim

The following are **tactical simulation inferences**:

- two bodies cannot occupy exactly the same physical space;
- a restricted approach limits how many bodies can engage side by side;
- crowding interferes with movement and access to a target;
- weapon reach and body spacing affect usable frontage;
- local companions can impede or channel one another; and
- a dense group has less freedom of movement than a dispersed one.

Exact radii, allowed penetration, right-of-way, collision priorities, solver
iterations, separation weights, and deadlock escape rules are all
**game-design inventions**.

## Current Hukbo constraints

### Existing movement contract

`BattleSimulation` currently:

- stores Q22.10-style positions with 1,024 raw units per logical unit
  ([`FixedPoint`](../../src/Hukbo.Core/Mathematics/FixedPoint.cs));
- selects a target, gathers movement proposals from the pre-commit state, then
  commits proposals in the agent array's stable order
  ([`GatherAndCommitMovement`](../../src/Hukbo.Core/Simulation/BattleSimulation.cs));
- moves directly toward the target by at most `MovementSpeedRaw`;
- stops based on center-to-center `AttackRangeRaw`;
- clamps final positions to the map; and
- hashes every authoritative agent position
  ([`StateHasher`](../../src/Hukbo.Core/Determinism/StateHasher.cs)).

`AgentState` has coordinates, speed, perception range, and attack range, but no
body radius, velocity, mass, or collision state
([`AgentState`](../../src/Hukbo.Core/Simulation/AgentState.cs)). Consequently,
multiple agents can currently finish a tick at the same coordinates, cross
through each other, or exchange positions.

The simulation standard anticipated a positive `Body.radius`, but explicitly
described it as display/range data rather than rigid-body collision
([simulation standards](../../SIMULATION-GAME-STANDARDS.md)). Implementing
collision changes that baseline and must update the behavioral contract,
tests, hashing/persistence assumptions, and performance measurements.

### Important integration consequences

- **Radius changes combat geometry.** Requirements must decide whether weapon
  range remains center-to-center or becomes surface gap:
  `distance_between_centers - radius_a - radius_b`.
- **Collision results are authoritative.** Corrected positions affect
  same-tick attacks, later target selection, events, snapshots, and state
  hashes.
- **Movement order becomes gameplay.** A sequential resolver can grant
  persistent priority to lower `EntityId` values unless fairness is designed
  explicitly.
- **A spatial index becomes shared infrastructure.** The standards already
  call for a deterministic uniform grid. Collision should reuse that derived,
  rebuildable neighbor-query layer rather than add a separate unbounded cache.
- **Melee is intentional convergence.** A generic crowd solver that tries to
  keep enemies apart can prevent combat unless ally and enemy interaction
  semantics are explicit.

## Collision and avoidance approaches

### Comparison

| Approach | Contact character | Formation effect | Deterministic fixed-point fit | Dense-crowd and deadlock behavior | Relative cost |
| --- | --- | --- | --- | --- | --- |
| Proposal rejection with deterministic slide | Hard: invalid destinations are rejected or replaced | Produces strong frontage and queues, but can look gridlocked | **High**; integer segment and disc tests fit the current proposal/commit pipeline | Stable but priority-biased; head-on swaps, doorways, and packed fronts require explicit rules | Low to medium |
| Reynolds-style separation and predictive steering | Soft unless backed by a hard resolver | Produces loose spacing, local cohesion, and organic clusters | **Medium**; possible with integer vectors, but normalization, weights, and blending need careful rounding | Can oscillate or compress when desired motion overwhelms repulsion; local rules do not guarantee passage | Low to medium with a grid |
| RVO/ORCA reciprocal velocity avoidance | Anticipatory, normally hard within its model assumptions | Produces smooth traffic lanes and orderly counterflow | **Low to medium** for Hukbo now; it adds velocity state, time horizons, half-plane geometry, and a linear program | Handles many moving agents well, but purely local avoidance can still stop at globally blocked layouts | Medium to high |
| Position-based overlap correction | Hard after convergence; soft and compressible with bounded iterations/compliance | Produces physical packing, pressure-like fronts, and emergent local clusters | **Medium to high** with stable pair order, fixed iterations, and integer correction rules | Robust in dense contact; order and iteration limits affect residual overlap and can inject motion | Medium, proportional to contact pairs and iterations |

All four approaches need a broad phase. A naive all-pairs implementation is
quadratic. A rebuilt uniform grid changes practical work to the agents plus
nearby candidate pairs, while worst-case work remains quadratic if every agent
occupies the same few cells.

### 1. Proposal rejection and sliding

This is the closest fit to Hukbo's current gather/commit architecture.

For each proposal, a resolver could test a swept disc against tick-start
positions and already reserved destinations. If the full move is invalid, it
could try a fixed list of alternatives, such as:

1. a tangent or lateral slide;
2. a deterministically truncated prefix of the proposed segment; and
3. no movement.

Candidate order must be explicit. Choosing positive X before negative X, for
example, creates a directional map bias. Choosing by `EntityId` is repeatable
but can create permanent per-agent privilege.

Strengths:

- smallest conceptual change to the existing pipeline;
- easy to express with checked integers and squared distances;
- a clear no-overlap invariant is possible;
- predictable movement budgets; and
- straightforward failure reasons such as `blocked_by_agent`.

Risks:

- a destination-only test allows fast agents to tunnel or swap through each
  other, so the test must cover the movement segment or reserve crossing paths;
- sequential commits make resolution order visible in outcomes;
- strict rejection produces queues and frozen opposing fronts; and
- axis-only sliding creates unnatural cardinal-direction artifacts.

This approach is a **Hukbo design candidate**, not a claim about human movement.

### 2. Local separation and predictive steering

Craig Reynolds separates action selection, steering, and locomotion, and
describes local separation, cohesion, alignment, and predictive collision
avoidance for autonomous characters. Separation sums repulsion from nearby
agents, while predictive avoidance estimates the nearest future approach
([Reynolds 1999](https://www.red3d.com/cwr/steer/gdc99/)).

For Hukbo, a preferred movement vector toward the combat target could be
combined with:

- short-range separation from allies;
- a weaker or differently capped response to enemies;
- optional cohesion toward a leader or contingent center; and
- predictive avoidance for crossing movement.

Strengths:

- readable, organic spacing without exact formation slots;
- local cohesion and leader following can be added independently;
- the same uniform grid can supply the neighborhood; and
- good fit for the historical preference for emergent local geometry.

Risks:

- steering reduces collision probability but does not by itself guarantee
  non-overlap;
- weighted blends are tuning-sensitive;
- integer division can zero small forces or create directional bias;
- exact overlap has no usable separation direction without a defined fallback;
- opposing steering forces can jitter or stall; and
- many parameters can hide the actual tactical rule.

A steering layer should therefore be treated as anticipatory motion, not as the
sole contact invariant, unless the product explicitly accepts temporary
overlap.

### 3. RVO and ORCA

Reciprocal Velocity Obstacles assume nearby agents make compatible avoidance
decisions. The original RVO work reports safe, oscillation-free motion in its
model for hundreds of agents
([van den Berg, Lin, and Manocha 2008](https://gamma-web.iacs.umd.edu/RVO/)).
ORCA divides pairwise avoidance responsibility and reduces each agent's action
selection to a low-dimensional linear program
([van den Berg et al. 2011](https://gamma-web.iacs.umd.edu/ORCA/)).

Strengths:

- anticipates future collisions rather than repairing penetration;
- designed for independent moving agents in dense 2D and 3D scenarios;
- supports smooth counterflow better than simple rejection; and
- has a clear preferred-velocity input.

Risks for Hukbo:

- current agents have no authoritative velocity or acceleration;
- velocity obstacles, time horizons, line intersections, and the linear
  program add a much larger numeric and testing surface;
- the published guarantees depend on the model's assumptions and should not be
  copied uncritically into a discrete fixed-point melee simulation;
- intentional enemy contact conflicts with the objective of collision-free
  navigation; and
- it solves local avoidance, not formation command, global pathfinding, or
  fair passage through a blocked front.

ORCA is valuable reference material, but it is not the smallest first
implementation for the current core.

### 4. Position-based correction

Position-Based Dynamics updates predicted positions by projecting violated
constraints toward valid positions. The foundational method emphasizes direct
position control and collision constraint handling
([Müller et al. 2007](https://doi.org/10.1016/j.jvcir.2007.01.005)).
Later crowd work represents agents as particles with planning velocities,
short- and long-range collision constraints, frictional contact, and cohesion
([Weiss et al. 2017](https://diglib.eg.org/items/1a538cbb-d42a-4a19-82e5-e85b27b228ff)).

A Hukbo version could:

1. gather preferred positions from tick-start state;
2. build sorted potentially overlapping pairs;
3. run a fixed number of pair-correction passes;
4. clamp corrections to the map and movement/correction budget; and
5. commit the corrected positions once.

Strengths:

- works naturally as a post-proposal phase;
- handles dense contact and packing more gracefully than pure rejection;
- can express hard contact, limited compression, friction, or cohesion as
  separate constraints; and
- formation-like shapes emerge from goal motion, space, and local contact.

Risks:

- pair processing order affects a finite-iteration result;
- splitting an odd raw-unit correction requires a deterministic remainder rule;
- exact co-location needs an ID-stable fallback normal;
- too few iterations leave overlap, while more iterations cost time;
- projection can move an agent farther than its requested movement; and
- correction can transmit motion through a packed group, which may be
  desirable pressure or unwanted pinball behavior.

Dense-crowd research also shows that very dense populations lose individual
freedom and may benefit from aggregate incompressibility constraints
([Narain et al. 2009](https://doi.org/10.1145/1661412.1618468)). That is useful
context, not a recommendation to add continuum crowd physics to Hukbo's first
collision milestone.

## Formation implications

Collision rules do more than prevent visual overlap. They determine tactical
geometry.

| Mechanic | Likely emergent result | Evidence category |
| --- | --- | --- |
| Positive body radius | Finite frontage and local queues | Tactical simulation inference |
| Equal hard contact for all agents | Stable lines, blocked rear ranks, strong choke effects | Game-design invention built on physical exclusion |
| Softer ally contact than enemy contact | Friendly groups compress and pass through one another more easily | Game-design invention |
| Strong ally separation plus cohesion | Loose contingent clusters with readable spacing | Game-design invention compatible with historical uncertainty |
| Enemy avoidance outside attack distance | Curving approaches and fewer direct contacts | Game-design invention; may undermine melee |
| Post-move position correction | Packing, pressure propagation, irregular front edges | Tactical simulation inference shaped by solver invention |
| Leader attraction | Leader-centered clusters | Plausible historical inference; exact radius and weight are invented |
| Terrain/access constraints | Narrow frontage and local numerical advantage | Strong mechanical reconstruction; exact behavior invented |

Rigid slot assignments would create visually legible formations, but they
would exceed the deep-past evidence unless presented plainly as a generic game
abstraction. Local spacing, cohesion, and contact are better foundations.

## Deterministic integration contract

Any selected method should satisfy the following independent of visual style.

### Numbers and ordering

- Use checked `long` intermediates for disc tests:
  `dx*dx + dy*dy` compared with `(radiusA + radiusB)^2`.
- Enumerate grid cells in a fixed coordinate order.
- Emit each unordered pair once as
  `(min(EntityId), max(EntityId))`, then sort pairs by that key.
- Use fixed solver iteration counts, not convergence based on wall-clock time.
- Specify rounding for normalization, division, odd correction remainders, and
  clamping.
- Resolve exact co-location using prior relative position when available and a
  documented `EntityId`-based fallback otherwise.
- Never let dictionary, hash-set, task scheduling, or renderer order determine
  a contact result.

### State and stages

- Body radius and collision policy belong to validated immutable rules or
  authoritative scenario data.
- The uniform grid and pair buffers are derived caches, rebuilt from
  authoritative positions.
- Preferred movement, resolved movement, and any correction must have distinct
  meanings.
- Collision resolution must be an explicit tick stage between movement intent
  and attack resolution.
- State hashes must include every new authoritative field. The current
  `BattleSnapshot` is a completed-tick render snapshot and does not duplicate
  immutable `Scenario` configuration; if snapshots later become standalone
  persistence artifacts, their format must carry the collision configuration
  and compatibility version explicitly.
- Events or inspector fields should explain `blocked`, `slid`, `separated`, or
  `corrected` movement without emitting unbounded per-pair event spam.

### Contact invariants to decide

Requirements must define:

- whether zero overlap is mandatory after every tick;
- whether limited penetration is allowed and, if so, the maximum raw amount;
- whether allies, enemies, dead agents, and map boundaries share the same
  collision policy;
- whether correction may exceed an agent's normal movement budget;
- whether agents may swap or cross paths in one tick;
- whether spawn positions must be collision-free;
- what happens when no valid movement exists; and
- how long an agent may be blocked before a fairness or escape rule applies.

## Staged recommendation

### Stage 0: resolve product semantics

Do not implement a solver until Requirements answers the hard-versus-soft
choice and the contact invariants above. In particular, decide whether:

- agents are solid discs, soft discs with bounded compression, or
  faction-dependent;
- attack range is center distance or surface gap; and
- friendly and enemy bodies use the same rule.

### Stage 1: build the common deterministic foundation

This stage is useful for every approach:

- one uniform body radius in raw fixed-point units;
- a rebuilt uniform grid;
- stable neighbor-pair generation;
- overlap reference tests, plus swept-disc tests only when the approved
  crossing policy forbids tunneling or swapping;
- collision metrics and the approved bounded observability mechanism; and
- deterministic scenarios for head-on, crossing, packed-front, and boundary
  contact.

Start with one radius rather than loadout-specific sizes. Variable radii can be
added only after a single-radius model proves useful.

### Stage 2: run a bounded A/B spike

Implement two isolated prototypes behind a test-only or experimental policy:

- **Hard candidate:** proposal validation plus deterministic tangent/truncated
  sliding.
- **Soft candidate:** fixed-iteration position-based correction with an
  explicit maximum penetration/correction budget.

Evaluate both on the same seeds and record:

- maximum and p95 overlap depth;
- accepted movement ratio;
- agents blocked for consecutive ticks;
- front width and depth;
- number of agents able to attack;
- neighbor pairs and correction passes per tick;
- p50/p95/p99 tick time and allocation; and
- repeated-run state-hash equality.

The user can then choose the contact feel using evidence rather than solver
terminology.

### Stage 3: add anticipation only if needed

If reactive hard or soft contact creates visible jitter, add a small
Reynolds-style separation or predicted-nearest-approach term before replacing
the whole resolver. Keep contact resolution as the invariant backstop.

### Stage 4: reconsider ORCA only for a demonstrated need

Consider ORCA when Hukbo has velocity state, obstacle/path semantics, and a
requirement for smooth crowd traffic or complex crossing flows. It should not
be introduced merely to solve two-disc overlap.

## Handoff to Requirements Agent

The Requirements Agent should produce explicit answers for:

1. **User-visible goal:** hard bodies, soft compression, or
   faction-dependent contact.
2. **Body model:** one radius for all live agents or definition-specific
   radii.
3. **Interaction matrix:** ally, enemy, corpse, map edge, and future obstacle
   behavior.
4. **Combat distance:** center-to-center or surface-to-surface weapon reach.
5. **Same-tick conflicts:** destination collision, crossing, swapping, and
   simultaneous convergence.
6. **Fairness:** whether stable ID priority is acceptable and how blocked
   agents recover.
7. **Movement budget:** whether collision response can add displacement.
8. **Compression bound:** exact maximum overlap if soft contact is chosen.
9. **Spawn validity:** reject, relocate, or initially resolve overlaps.
10. **Observability:** required reason codes, metrics, and spectator cues.
11. **Persistence:** compatibility expectations for new radius/policy fields.
12. **Performance:** accepted 200-agent budget and required 500-agent report.

Suggested binary acceptance criteria after those decisions:

- same scenario and build produce identical ordered events and state hashes;
- post-tick overlap never exceeds the approved bound;
- no agent exceeds the approved movement/correction budget;
- dead-agent and boundary behavior match the interaction matrix;
- reference all-pairs and grid pair generation agree on generated small worlds;
- exact co-location, head-on swaps, crossings, packed fronts, and map corners
  have named regression tests; and
- the 200-agent workload passes its budget with a reported 500-agent result.

## Handoff to Task Planner Agent

After Requirements resolves the semantic gates, split implementation into
granular, dependency-ordered tasks:

1. add radius/policy configuration, validation, and hash coverage;
2. add deterministic disc-overlap geometry and, only when required by the
   approved crossing policy, swept-disc reference geometry;
3. add a naive all-pairs reference neighbor query;
4. add the rebuilt uniform-grid derived cache;
5. add stable, duplicate-free pair generation and reference equivalence tests;
6. add proposal/resolution scratch buffers without hot-loop allocation;
7. implement the selected resolver with explicit pair/candidate order;
8. integrate the collision stage before attack gathering;
9. update attack-distance semantics;
10. implement spawn, death, boundary, and exact-co-location policies;
11. add movement reason codes and bounded diagnostics;
12. add focused regression and determinism tests;
13. add packed-front and stress benchmarks;
14. verify snapshots/replays or document why they are unaffected;
15. run the relevant unit, integration, format, build, and benchmark checks;
16. inspect the final diff for unrelated changes; and
17. request independent review of determinism, fairness, and overflow risks.

The planner should not schedule both production solvers unless the Requirements
Agent explicitly retains the A/B spike. Each writable file or subsystem should
have one owner.

## Technical bibliography

1. Craig W. Reynolds. 1999.
   ["Steering Behaviors For Autonomous Characters."](https://www.red3d.com/cwr/steer/gdc99/)
   *Game Developers Conference*, pages 763-782.
2. Jur van den Berg, Ming C. Lin, and Dinesh Manocha. 2008.
   ["Reciprocal Velocity Obstacles for Real-Time Multi-Agent Navigation."](https://gamma-web.iacs.umd.edu/RVO/)
   *IEEE International Conference on Robotics and Automation*.
3. Jur van den Berg, Stephen J. Guy, Ming Lin, and Dinesh Manocha. 2011.
   ["Reciprocal n-body Collision Avoidance."](https://gamma-web.iacs.umd.edu/ORCA/)
   *Robotics Research*, volume 70, pages 3-19.
4. Matthias Müller, Bruno Heidelberger, Marcus Hennix, and John Ratcliff. 2007.
   ["Position Based Dynamics."](https://doi.org/10.1016/j.jvcir.2007.01.005)
   *Journal of Visual Communication and Image Representation* 18(2):
   109-118.
5. Tomer Weiss, Alan Litteneker, Chenfanfu Jiang, and Demetri Terzopoulos.
   2017.
   ["Position-Based Multi-Agent Dynamics for Real-Time Crowd Simulation."](https://diglib.eg.org/items/1a538cbb-d42a-4a19-82e5-e85b27b228ff)
   *ACM SIGGRAPH/Eurographics Symposium on Computer Animation*.
6. Rahul Narain, Abhinav Golas, Sean Curtis, and Ming C. Lin. 2009.
   ["Aggregate Dynamics for Dense Crowd Simulation."](https://doi.org/10.1145/1661412.1618468)
   *ACM Transactions on Graphics* 28(5).
