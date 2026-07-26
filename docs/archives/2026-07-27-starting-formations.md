# Starting Formations Implementation Plan

> **Archived: reference only.** This document is deprecated. Do not execute it, and do not treat its steps, versions, or tooling references as current. The live contract is `CLAUDE.md` plus the skills in `.claude/skills/`.

Date: 2026-07-27
Design: [2026-07-27-starting-formations-design.md](2026-07-27-starting-formations-design.md)
Branch: `feature/starting-formations`

**Goal:** Replace the random spawn cloud in `BattleSimulation.Create` with a
deterministic contingent deployment that is identical, mirrored, for both
factions.

## Tasks

1. **Add `src/Hukbo.Core/Simulation/FormationPlanner.cs`.** — done.
   Internal static class, pure, no state. Assembly-internal entry point
   `PlanFactionDeployment(Scenario, ref SplitMix64)` returns
   `(int XRaw, int YRaw)[]` in faction-local index order for the left half.
   Contingent sizing, the shared `Lattice`, the crowded-map fallback, integer
   square roots and the historical caveats all live in that one file.

2. **Wire it into `BattleSimulation.Create`.** — done.
   Faction 0 takes the planned positions; faction 1 takes
   `MapWidthRaw - x` with the same `y`. `ResolveSpawnPlacement` is unchanged
   and its comment now describes it as a safety net.

3. **Add `tests/Hukbo.Core.Tests/FormationPlannerTests.cs`.** — done, 25 tests.

4. **Run the canonical gate.** — done. `./scripts/verify.ps1` passed all five
   stages; 351 Core and 513 Client tests passed; the seed-1 / 200-agent /
   10,000-tick workload reported `deterministic: true`. Recorded in
   [docs/development/testing.md](../development/testing.md).

5. **Record the moved hashes.** — done. State hash `DC7F2E7A107C885A`, event
   hash `6C641E90DDF0B943`; the previous pair is listed as superseded.

6. **Archive both plan documents.** — done on merge.

## Review outcome

An independent review of the first implementation found, and reproduction
confirmed, three defects that are fixed in the merged version:

- the fit check and the placement computed a contingent's width by two
  different formulas, so on a narrow map (for example 182x720 with 60 agents)
  members spilled across the centre line and the repair pass silently destroyed
  the mirror. One shared `Lattice` value now serves both;
- the degenerate-region collapse could place a centre below one body radius,
  whose mirror then clamped in the opposite direction and left the two sides
  unequal;
- the crowded-map fallback bounded its columns against the region width but not
  its rows against the region height.

The review also measured a real behavioural consequence that is **not** fixed
here and is recorded instead: an exactly symmetric deployment makes the
entity-ID ordering rule the only asymmetry in the simulation, and the seed win
distribution moves from 4/16 to 1/19 in faction 1's favour. See the design
document and `docs/development/testing.md`. Correcting it means changing a tick
rule and needs its own decision record.

## Verification criteria

| Criterion | Method | Result |
| --- | --- | --- |
| Mirror symmetry | Unit test comparing faction 0 and faction 1 positions | Pass |
| Clear of contact at spawn | Unit tests over five seed/size combinations | Pass |
| In bounds | Unit tests including minimum, maximum and narrow-half maps | Pass |
| Determinism preserved | `verify.ps1` headless stage | `deterministic: true` |
| Preset untouched | `CombatRuleset.ContentHash` | `0x59FB4CA563D87A49` |
| No regressions | Full Core and Client suites in Release | 351 + 513 pass |
| Interactive readability | Manual smoke rows 53-56 | PENDING, not run |

## Out of scope

Leader entities, cohesion, formation state transitions, terrain, a scenario knob
for alternative deployments, any client-side rendering change, and the
entity-ID ordering bias described above.
