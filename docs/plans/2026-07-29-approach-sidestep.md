# Approach sidestep — plan

Design: [`2026-07-29-approach-sidestep-design.md`](2026-07-29-approach-sidestep-design.md).
Baseline commit: `a47219e`.

Status: T1, T2, T3, T4, T6 and T7 complete. **T5 did not meet its acceptance
criterion** — thresholds 7 and 8 fell from 5 and 8 stalls to 2 and 3, not to
zero. Per T5's own instruction nothing was tuned to close the gap; the residual
was reclassified instead, and the diagnosis is in design section 10.1. The
trigger that gates both this escape and the rally escape is the cause, and
whether to attempt a third trigger design — after the two already rejected on
measurement — is a decision left to the user.

Task order matters. T5 is a measurement that can refute the design, and it runs
before the documentation tasks rather than after, so that a negative result is
found while the work is still open rather than after it has been written up as
finished.

## T1 — Provisional span constants in `FormationRules`

**File:** `src/Hukbo.Core/Simulation/FormationRules.cs`

Add the sidestep's lateral magnitude bounds as named constants next to
`StallEscapeStreakTicks`, expressed as multiples of the body radius.

A warrior of radius R walking directly behind a stationary comrade of radius R
needs more than 2R of lateral clearance before its own body can pass. The span
therefore starts at 2R rather than at zero: an offset smaller than that redraws
the aim point without changing whether the approach is blocked, which is a
generation spent for nothing. The upper bound is 4R, far enough to clear a
second body standing beside the first and near enough that the warrior is still
recognisably walking at its enemy.

Both numbers are game-design choices, not measurements. Mark them PROVISIONAL in
the doc comment, in the same words the other tuning constants in this file use.

**Verification:** compiles; `./scripts/format.ps1 -Verify` passes.

## T2 — `ApproachSidestep`, the pure helper

**File:** `src/Hukbo.Core/Simulation/ApproachSidestep.cs` (new)

A stateless internal static class mirroring `RallyOffset`, with one method:

```
internal static (int XRaw, int YRaw) Compute(
    ulong seed,
    ulong entityId,
    int bodyRadiusRaw,
    int generation,
    long deltaXRaw,
    long deltaYRaw,
    long distanceRaw)
```

`deltaXRaw`/`deltaYRaw`/`distanceRaw` describe the agent's approach vector to its
target. The return value is the offset to add to the target's position.

Rules the implementation must hold:

- **Generation 0 returns `(0, 0)` without hashing anything.** Not "hashes to
  approximately zero" — returns before the hash is built. This is the entire
  hash-neutrality argument of design section 5 and it must be structural rather
  than probabilistic.
- `distanceRaw <= 0` returns `(0, 0)`. A zero-length approach vector has no
  perpendicular.
- The key is `(tag, seed, entityId, generation)` through `Fnv1a` into
  `SplitMix64`, following `RallyOffset.Compute` exactly, with its own distinct
  tag constant so the two escapes cannot draw correlated values for one agent.
- Magnitude is drawn from the closed span `[2R, 4R]` of T1. The side is drawn
  from a separate draw off the same generator, so the two perpendiculars are
  equally likely and the choice is stable for a given generation.
- The perpendicular is `(-deltaY, deltaX)` scaled by magnitude over distance, in
  integer arithmetic. No floating point. Widen to `long` before multiplying —
  `deltaY * magnitude` overflows `int` on a large map.
- Negative `generation` throws, matching `RallyOffset.Compute`.

**Tests:** `tests/Hukbo.Core.Tests/ApproachSidestepTests.cs` (new)

- Generation 0 returns exactly `(0, 0)` for a spread of seeds, entity ids and
  approach vectors.
- The same inputs always return the same offset.
- Different generations for one agent return different offsets.
- The offset is perpendicular to the approach vector within the rounding the
  integer division permits — assert the dot product's magnitude is bounded by
  the truncation error, not that it is exactly zero.
- The offset's length lies inside `[2R, 4R]` allowing for the same truncation.
- A zero-length approach vector returns `(0, 0)`.
- Negative generation throws `ArgumentOutOfRangeException`.

**Verification:** the new Facts pass; no other test changes.

## T3 — Wire it into the pursuit branch

**File:** `src/Hukbo.Core/Simulation/BattleSimulation.cs`

In the movement-proposal loop, the ordinary pursuit branch currently reads:

```
var target = _agentStates[_agentIndexes[enemyTargetId]];
_movementProposals[index] = BuildMovementProposal(agent, target);
```

Read `_collision.StallGeneration(index)`. At generation 0, leave the call
exactly as it is — do not route it through a new code path that happens to
produce the same answer, because that is harder to prove unchanged than not
touching it. At a non-zero generation, compute the approach vector to the
target, call `ApproachSidestep.Compute`, and build the proposal against the
offset aim point instead.

Do not touch the contingent cohesion branch above it. Design section 4.1 gives
the reason and it is not settled by this plan.

The XML doc on the method gains a remark in the shape
`BuildRegroupingProposal`'s stall-escape remark already uses, naming the design
document.

**Verification:** compiles under `TreatWarningsAsErrors`; whole suite still
passes.

## T4 — The regression Fact

**File:** `tests/Hukbo.Core.Tests/LastStandFormationTests.cs`

A Fact that builds the geometry directly rather than fishing for it across
seeds: a pursuing agent, a stationary comrade placed exactly on its line of
approach at tangency, an enemy beyond them both. Drive the simulation and assert
that the pursuer is blocked while its generation is 0, and that after
`FormationRules.StallEscapeStreakTicks` blocked ticks it takes a different aim
point and stops being blocked.

Compute every distance in the fixture from `Scenario.BodyRadiusRaw` and the
`FormationRules` helpers rather than writing literals. The landmine that cost a
commit on this branch already is a fixture built at body radius 4.0 against a
tree at 4.25; a fixture that derives its geometry cannot acquire that fault.

**Verification:** this Fact fails on `a47219e` and passes after T3. Record both
results — a regression test that was never seen red proves nothing.

## T5 — Measurement, and the point at which the design can be refuted

**Tool:** `tools/Hukbo.Tools.DeadlockProbe`

Run the 200-seed survey at all four thresholds, at radius 4352, 18 agents:

```
dotnet run --project tools/Hukbo.Tools.DeadlockProbe -c Release -- `
  --survey --first-seed 1 --last-seed 200 --radius-raw 4352 --threshold <t>
```

Baseline at `a47219e`, already measured: threshold 6 → 0, 7 → 5, 8 → 8, 9 → 0.

**Acceptance:** thresholds 7 and 8 reach 0, and 6 and 9 stay at 0.

**If they do not**, stop and do not tune anything to close the gap. Reclassify a
still-stalling seed with the probe's single-seed mode and report the intent split
of the blocked agents, exactly as was done for seed 16. Design section 8 question
1 anticipates this outcome: a residual lock that is `Regrouping`-only means the
rally escape's own trigger is the thing to revisit and this design is finished
but insufficient, which is a result to report rather than a problem to hide by
adjusting T1's span until the number moves.

## T6 — Hash verification

Design section 5 predicts no recorded hash moves. Verify rather than assume.

- `./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1` and compare against
  the recorded baseline: `stateHash 2410DD94F26C82E2`,
  `eventHash 56F66BBC10E69F0E`.
- The determinism fixtures and the movement preset freeze tests must pass
  untouched.

**If a hash moves**, do not recapture anything. `MovementPresetFreezeTests` are
frozen oracles and recapturing them deletes the only thing they do. A moved hash
means the sidestep fired in a battle nobody expected it to fire in, which is a
finding: report which fixture moved and stop. Shipping it would then need a new
preset version and new golden expectations under CLAUDE.md section 5, and
whether to flip the default is the user's call.

## T7 — Canonical gate and write-up

- `./scripts/verify.ps1 -SkipBootstrap`, output pasted, no exceptions.
- Fold the T5 numbers into the design document as a results section, alongside
  the baseline table already in its section 2.
- Leave design section 8 questions 2 and 3 open and say so. Question 3, the
  inspector field, is deferred rather than answered; record that explicitly so it
  does not lapse into having been silently dropped.
- Do not flip any row in `docs/development/testing.md`. Nothing here is proved
  by a human at an interactive desktop.

## Out of scope

Everything design section 9 lists. In particular this plan does not widen
`NoLastStandBattleStallsAtTheTickLimitAcrossSeedsOneThroughTwoHundred` to 200 seeds;
that is separate agreed work and mixing it in would make T5's before-and-after
unreadable.
