# Plans — active work

Everything in this folder is **live**. Finished plans move to
[`docs/archives/`](../archives/README.md) with an "Archived: reference only"
banner, so a document still sitting here has not been retired.

"Live" does not mean "implemented." A design document in this folder may be
approved, argued over, and complete while no line of code exists for it. Check
the `Status:` line at the top of a design before assuming the game behaves the
way it describes.

Two document kinds, per `CLAUDE.md` section 6:

| Suffix | What it is | Authorizes code? |
| --- | --- | --- |
| `-design.md` | The reasoning, the alternatives, the rejected options | **No** |
| no suffix | The ordered task list and verification criteria | Yes |

## The combat preset chain

Four documents describe one dependent sequence of changes to
`Hukbo.Core.Combat`. They are the most likely thing in this folder to be
misread, because each one describes a game that does not exist yet, and each
depends on the one before it.

**As of 2026-07-28, stage 1 (V2, plus its clash-integration follow-on) is
implemented and the game runs it. Stages 2 and 3 are design only.**

| Order | Document | Status |
| --- | --- | --- |
| 1 | [`docs/archives/2026-07-28/2026-07-27-weapon-identity-and-attributes-design.md`](../archives/2026-07-28/2026-07-27-weapon-identity-and-attributes-design.md) — preset V2 | Archived: implemented and complete |
| 1 | [`docs/archives/2026-07-28/2026-07-27-weapon-identity-and-attributes.md`](../archives/2026-07-28/2026-07-27-weapon-identity-and-attributes.md) — preset V2 task list | Archived: implemented and complete |
| 2 | [`docs/archives/2026-07-28/2026-07-27-combat-preset-v3-combos-design.md`](../archives/2026-07-28/2026-07-27-combat-preset-v3-combos-design.md) — preset V3 | Archived: implemented and complete — `src/Hukbo.Core/Combat/CombatPresetRegistry.cs:16,61` registers `PrecolonialPhilippinesV3`, documented at `src/Hukbo.Core/Combat/CombatIdentity.cs:105-113` |
| 2 | [`docs/archives/2026-07-28/2026-07-27-combat-preset-v3-combos.md`](../archives/2026-07-28/2026-07-27-combat-preset-v3-combos.md) — preset V3 task list | Archived: implemented and complete |
| 3 | [`docs/archives/2026-07-27/SHIELDS.md`](../archives/2026-07-27/SHIELDS.md) — shields as a stat-variant layer | Design complete, no plan document |

What each stage does:

- **V2** renames the four weapons to Filipino pair-form labels and gives every
  weapon its own damage, reach, and cooldown, split by grip — a one-handed
  weapon fought solo is mechanically distinct from the same weapon fought with a
  shield.
- **V3** adds attack combinations: a chance to open a chain, a chance to
  continue it, and a maximum length governed by a placeholder fighter level.
- **Shields** replaces the current flat targeting multiplier with a per-shield
  modification of a weapon's paired profile.

The order is not a preference. V3 builds on the `WeaponProfile` record that V2
introduces, and the shield work modifies the paired profiles that V2 authors and
the combination fields that V3 adds. Implementing them out of order means
rewriting the same three files in `Hukbo.Core/Combat` three times.

### What is true of the game today

So that nobody reads the four documents above and reports the current build
incorrectly:

- The combat preset is `PrecolonialPhilippinesV2`. `PrecolonialPhilippinesV1`
  stays registered, frozen, and unmodified so its replays remain reproducible.
- There are four weapons, each with a Filipino pair-form player-facing label
  and a recorded evidence tier: `Kampilan — Great Blade`, `Wasay — War Axe`,
  `Kalis — Thrusting Blade`, `Itak — Work Blade`.
- Each weapon has its own damage, reach, and attack cooldown, split by grip.
  `Kampilan` and `Wasay` are two-handed (one profile each, no shield
  permitted); `Kalis` and `Itak` are one-handed (a solo profile and a
  shield-paired profile each). An accepted attack additionally resolves
  against a five-way `AttackResolution` — `Landed`, `ShieldBlocked`,
  `Parried`, `Deflected`, `Evaded` — from the weapon-clash system merged on
  top of V2, so a landed hit is no longer unconditional.
- The roster has six entries — one per weapon-and-grip combination — and the
  army composition panel has six rows.
- Shields are still `None` and `TallHardwood`, and a shield's targeting
  effect is still halving chest and abdomen targeting weight; per-shield
  attribute modification (beyond the existing paired-profile values) has not
  been built.
- There is still no attack combination and no fighter level — those are
  preset V3, design only, no plan document yet.
- Per-weapon balance (mean time to kill, per-faction win rate) has been
  measured but not tuned against; see "T32 — weapon balance measurement" in
  `docs/development/testing.md`. The attribute values above are provisional
  gameplay tuning, not settled.

## The performance hardening workstream — complete and archived

Both documents were archived on 2026-07-28, the day the work completed. They are
reference only; do not execute either. The evidence the workstream produced is
live and stays where it is, in
[docs/development/testing.md](../development/testing.md),
[docs/research/TICK-STAGE-PROFILE.md](../research/TICK-STAGE-PROFILE.md), and
the performance technique inventory in `SIMULATION-GAME-STANDARDS.md`.

| Document | Status |
| --- | --- |
| [`docs/archives/2026-07-28/2026-07-28-arch-informed-performance-hardening-design.md`](../archives/2026-07-28/2026-07-28-arch-informed-performance-hardening-design.md) | Archived. Design only; it never authorized implementation, and the profile later overturned its ranking of the structural candidates. |
| [`docs/archives/2026-07-28/2026-07-28-arch-informed-performance-hardening.md`](../archives/2026-07-28/2026-07-28-arch-informed-performance-hardening.md) | Archived. Carries the ordered task list, the verification criteria, the Gate A verdict, and the completion record. |

This workstream is hash-neutral by construction: every task in the plan is
required to leave the seed-1 200-agent pair unchanged, and it is unchanged —
`stateHash 71211929A44A16CA`, `eventHash A2DC3ECA3F7345ED`. **No ECS, archetype
system, or chunk system was adopted, and no package was added.** `CLAUDE.md`
section 9's prohibition on a general-purpose ECS before a profiler demands one
stands exactly as it did before this workstream started.

What it delivered:

- A `Hukbo.Core`-only per-tick allocation figure, separated from the
  whole-loop harness total the headless runner already reported.
- A four-point agent-count scaling curve — 200, 500, 1,000, and 2,000 agents
  at seed 1 — recorded in `docs/development/testing.md`.
- A per-stage tick profile in
  [`docs/research/TICK-STAGE-PROFILE.md`](../research/TICK-STAGE-PROFILE.md),
  sampled from the unmodified Release headless seed-1 workload.
- Removal of per-tick allocations that were visible in the source without that
  profile.
- One previously undocumented invariant, the no-copy contract on
  `CollisionResolver.Grow<T>`, written down at the symbol and pinned by a
  test.
- A performance technique inventory added to `SIMULATION-GAME-STANDARDS.md`
  section 15, recording which of the techniques an external research pass
  found in the Arch library are usable in Hukbo as-is, which need a named
  discipline, and which are forbidden outright.

The plan's Gate A closed three of the four structural candidates it gated —
spatial acceleration for target selection, a dense identifier-to-index map in
place of `Dictionary<ulong,int>`, and the `AgentState` memory-layout question —
and authorized the fourth, an axis-delta rejection ahead of the existing
squared-distance check in target selection. The same stage profile that decided
those four verdicts points at collision resolution as the next candidate for
attention; that stage is explicitly out of scope for this plan and needs its own
design document before anyone touches it.

## The formation and movement realism workstream — complete and archived

Both documents were archived on 2026-07-28, the day the last task (T19) closed
out the ordered list. They are reference only; do not execute either. The
evidence this workstream produced is live and stays where it is, in
[docs/development/testing.md](../development/testing.md) and
`SIMULATION-GAME-STANDARDS.md`.

| Document | Status |
| --- | --- |
| [`docs/archives/2026-07-28/2026-07-28-formation-movement-realism-design.md`](../archives/2026-07-28/2026-07-28-formation-movement-realism-design.md) | Archived. Design only; it never authorized implementation. |
| [`docs/archives/2026-07-28/2026-07-28-formation-movement-realism.md`](../archives/2026-07-28/2026-07-28-formation-movement-realism.md) | Archived. Carries the ordered task list (T1 through T19) and the verification criteria. |

What it delivered:

- A new `MovementPresetId.PersistentContingentsV2 = 2`, registered alongside
  the frozen `IndependentPursuitV1 = 1`, both reachable through
  `--movement-preset` on the headless runner and `-MovementPreset` on
  `scripts/benchmark.ps1`. `PersistentContingentsV2` carries its own pinned
  `ContentHash` literal (`0xE5AC42AA7FC19301`), distinct from V1's, and its own
  pinned seed-1 state and event hashes.
- `Scenario.MovementPreset` now defaults to `PersistentContingentsV2` (T15),
  so every battle the game runs without an explicit preset selection uses
  persistent, cycling contingents rather than pure independent pursuit.
  `IndependentPursuitV1` stays registered, frozen, and byte-identical: the
  seed-1/200-agent/10,000-tick trajectory captured before this workstream
  began (`eventHash 2A9F2D7054CD1805`, `stateHash AFEBC0431554BCBB`,
  `Faction1Victory`, survivors 0 and 2, `measuredTicks 1710`) still reproduces
  exactly when that preset is selected explicitly.
- A ninth tick stage, `ResolveContingentStates`, between
  `SelectTargetsAndIntents` and `GatherMovementProposals`, cycling each
  faction's up to eight contingents between gathering on their own leader and
  advancing independently for the whole battle — not only during the
  last-stand rally that already existed. The state machine, the duty cycle,
  the leader scan, the straggler gate, the arrival-slowdown taper, and the two
  geometric cohesion gates (the map-edge test and the cross-contingent overlap
  test) are all covered by dedicated unit-level test files
  (`ContingentStateMachineTests`, `ArrivalTaperTests`,
  `PersistentContingentTests`) plus three deliberately engineered deadlock
  geometries (`ContingentDeadlockTests`) that the twenty-seed liveness sweep
  alone cannot guarantee it will ever construct.
- An inspector row (`Contingent: <n> — <state>`) and a per-contingent ground
  tint helper, so the mechanism is discoverable by a spectator without reading
  source, per `SIMULATION-GAME-STANDARDS.md` section 10's ninth question.
  Wiring the tint into actual pawn rendering was identified during the work
  but is not itself covered by any task in the archived plan; a future task
  would need to thread the active theme and the agent's contingent fields down
  to `PawnRenderer.Draw`.
- Updated documentation: `SIMULATION-GAME-STANDARDS.md`'s tick-stage order and
  `docs/research/TICK-STAGE-PROFILE.md` both now show nine stages, and
  `docs/development/testing.md` carries the new baseline, the T16
  before/after performance tables, and a new "Persistent contingent smoke"
  section (rows 102 through 114).

What is still outstanding:

- **Every new smoke-checklist row is `PENDING`.** The automated suite proves
  the state machine, the two geometric gates, and the three engineered
  deadlock geometries all resolve correctly, both standalone and inside a
  running simulation, but none of it proves the resulting movement reads as a
  group of warriors gathering and advancing together to a person watching it.
  Only a human running `./scripts/run.ps1` on an interactive desktop may flip
  one of those rows to `PASS`, per `docs/development/testing.md`'s own rule,
  and no task in this workstream did.
- **The canonical gate, `./scripts/verify.ps1`, has not yet been run as a
  single pass over the fully integrated tree.** Each task verified against
  the scope its own instructions named — `./scripts/test.ps1`, a targeted
  `./scripts/benchmark.ps1` run, or `./scripts/verify.ps1 -SkipBootstrap` at
  specific checkpoints (T5, T5b) — consistent with the plan's own rule that
  the full gate "runs once, after integration" and is never delegated to a
  sub-agent. Whoever integrates this branch still owes that one run and its
  literal pasted output.

## The contingent Close latch workstream — in progress

Design:
[`docs/plans/2026-07-28-contingent-close-latch-design.md`](2026-07-28-contingent-close-latch-design.md).
Plan: [`docs/plans/2026-07-28-contingent-close-latch.md`](2026-07-28-contingent-close-latch.md).
Neither document is archived yet — the workstream still has a human step
outstanding, so both stay live in this folder rather than moving to
`docs/archives/`.

The formation and movement realism workstream above shipped a defect along
with its feature: transition rule 3 put a whole contingent into
`ContingentState.Close` the moment a single member reached contact, and the
state never lifted. That is the cause recorded behind smoke rows 104 and 114
in `docs/development/testing.md`. This plan's eleven tasks fix it.

T1 through T8 are committed. In order: T1 froze `PersistentContingentsV2`'s
simulated trajectory in a digest fixture before touching any production code;
T2 added two inert fraction fields to `MovementRuleset`; T3 and T4 rewrote rule
3 to count members in contact rather than take a minimum distance, still
behaviour-inert because every registered preset kept the fraction that
reproduces the old rule exactly; T5 registered a third preset,
`PersistentContingentsV3`, carrying a non-zero close fraction; T6 flipped
`Scenario`'s shipped default to it, the one task at which the seed-1
200-agent pair actually moved; T7 re-measured the contingent shape harness
under the new default; T8 recorded performance and allocation.

Registered movement presets are now three: `IndependentPursuitV1 = 1`,
`PersistentContingentsV2 = 2`, `PersistentContingentsV3 = 3`. Seed 1, 200
agents, 10,000 requested ticks, shipped default, old and new side by side:

| Field | `PersistentContingentsV2` (was) | `PersistentContingentsV3` (now) |
| --- | --- | --- |
| `measuredTicks` | 1064 | 1334 |
| outcome | `Faction0Victory` | `Faction1Victory` |
| survivors (faction0/faction1) | 8 / 0 | 0 / 1 |
| `eventHash` | `8E819FF7B378FEFD` | `C0379769F4483553` |
| `stateHash` | `C79B76AE81C300CB` | `0682C6BCED57224D` |

Both `IndependentPursuitV1` and `PersistentContingentsV2` now carry a per-tick
trajectory digest fixture under `tests/Hukbo.Core.Tests/Fixtures/`, replayed
byte-identically by `MovementPresetFreezeTests` — the mechanism that makes a
preset's *simulated behaviour* freezable, as distinct from the pinned
`ContentHash` literals in `MovementPresetRegistryTests`, which only prove a
preset's declared fields have not moved.

**The fix works at its narrowest stated purpose and no further, and that is
recorded honestly rather than oversold.** T7's re-measurement found Hold
episodes beginning after a contingent's first `Close` went from zero to one
across fifty contingent-battles, and `Close` occupancy fell from 63.69 % to
53.11 % of contingent-ticks. Rule 2 (attrition) rose to 30.45 % and is now the
ceiling on mid-battle gathering. The `Hold` aspect-ratio tail got **worse**
rather than better — p99 from 3.06 to 5.04, maximum from 5.17 to 14.21 —
which misses the plan's own acceptance criterion that the distribution be no
worse than before. T8 found no measurable performance or allocation movement
beyond run-to-run noise at 200 or 500 agents.

Smoke rows 104 and 114 remain unresolved and await a human. T9 (this
documentation pass) is done; T10 will reset those two rows to `PENDING` for
re-observation under `PersistentContingentsV3` and record the historical
cause; T11 archives both documents once T10's human pass has actually
happened, and not before.

## Where the live contract lives

| Question | Source |
| --- | --- |
| How agents work in this repo | `CLAUDE.md` |
| Naming and logging, for non-Claude agents | `AGENTS.md` |
| Determinism, tick order, reviewer checklist | `SIMULATION-GAME-STANDARDS.md` |
| Verification and evidence | `docs/development/testing.md` |
| Task procedures | `.claude/skills/` |
| Why something was built this way | `docs/archives/` |
