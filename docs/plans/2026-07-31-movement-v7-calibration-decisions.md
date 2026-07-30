# Movement V7 calibration — decisions taken

Date: 2026-07-31
Status: **decisions recorded, nothing executed.** No code was written, no test
was run, and the canonical gate was not invoked for this document. It records
six decisions so that the V7 design document and task plan can be written
against a settled brief instead of re-litigating them.

This is the input to a future
`docs/plans/YYYY-MM-DD-movement-v7-calibration-design.md`, not a substitute for
it.

## 1. Why these decisions were needed

Five weapon sessions — Itak, Kampilan, Kalis, Wasay, and the tall hardwood
shield — each completed their own slice of the weapon-relative movement program
and each ended by handing the same class of problem upward. Every one of the
sixteen fields per profile row reaches the `EquipmentRelativeFootworkV6` content
hash and the V6 trajectory digest, which are pinned in
`tests/Hukbo.Core.Tests/MovementPresetRegistryTests.cs` (the literal
`0x0FFE5D202B324D25`) and `tests/Hukbo.Core.Tests/MovementPresetFreezeTests.cs`.
A weapon session may not edit either file. No weapon session could therefore
tune its own row, and none did.

The result is that `EquipmentRelativeFootworkV6` is complete, fully tested, and
unusable as a default: every measured run at both 200 and 500 agents ends in a
`Draw` at the ten-thousand-tick limit with both sides substantially alive.

The evidence base for everything below is three session reports —
[`2026-07-30-weapon-movement-foundation-report.md`](2026-07-30-weapon-movement-foundation-report.md),
[`2026-07-30-wasay-movement-report.md`](2026-07-30-wasay-movement-report.md),
and
[`2026-07-30-tall-hardwood-shield-movement-report.md`](2026-07-30-tall-hardwood-shield-movement-report.md)
— together with the archived weapon plans under
`docs/archives/2026-07-31/movement/`.

**A note on the archive.** Several criteria referenced below currently exist
only in `docs/archives/2026-07-31/movement/README.md`. Per `CLAUDE.md` section 6
an archived document is reference material and may not be cited as authority for
a change. Any criterion V7 intends to keep must therefore be **restated in the
V7 design document in its own words**, not cross-referenced into the archive.
This applies most directly to the phase-flip ceiling in decision D3.

## 2. The decisions

### D1 — A pressure interrupt will be added so a committed warrior can be broken off

**Question.** `WeaponMovementRules.ResolveProvisionalFootwork`
(`src/Hukbo.Core/Movement/WeaponMovementRules.cs`, lines 192 to 280) evaluates
its ladder in a fixed order. Step 2 handles a continuing or expiring `Commit`
and returns. Step 3 handles a continuing `Recover` and returns. Only step 4 and
step 5 consult the support ratio to decide disengagement. A warrior in the
attack lifecycle therefore returns from the ladder before its disengage
threshold is ever read.

**Measured consequence.** Two independent sessions found the same thing. The
Wasay session observed an isolated Wasay spending nine to twelve ticks above
two-to-one local pressure and zero ticks in `Disengage`; pinning
`AttackCooldownRemaining` high before the run produced one hundred and thirty
pressure ticks and one hundred and twenty-nine `Disengage` ticks from identical
geometry. The shield session found the two shield rows worse still: both spend
three commitment ticks plus three recovery ticks, a six-tick lifecycle, while
combat preset `PrecolonialPhilippinesV2` reloads shielded Kalis in five ticks
and shielded Itak in four. Kampilan's seven-tick reload against the same
six-tick lifecycle leaves a one-tick window each cycle; the shield rows leave
none at all, and cycle `Commit, Commit, Commit, Recover, Recover, Commit`
indefinitely.

**Decision.** Add a pressure interrupt that can break an in-progress commitment,
rather than accepting the current ordering.

**Rationale.** The alternative was to keep the ordering and declare the
disengage thresholds non-contact-only by design. That was rejected because it
would make the count-sensitive posture table that all five weapon sessions
specified and tested undiscoverable by a spectator watching an engaged warrior,
which is precisely the question `SIMULATION-GAME-STANDARDS.md` section 10 asks
of every feature. It would also leave five sessions' worth of threshold work
describing a state that never occurs in a real battle.

**Consequence.** This is a change to shared runtime behaviour, not a scalar
tune. It reaches the content hash and the trajectory digest on its own, for
every row, whether or not any profile value is edited. It therefore forces a new
preset version by itself — see D5.

**Deliberately left open for the design document.** The interrupt's trigger
condition, whether it may fire during `Commit` as well as `Recover`, whether it
costs the agent anything, and whether the threshold is shared or per-row are all
design questions. This decision authorizes the mechanism, not a specific shape
for it.

### D2 — Termination bar and the single budget metric

**Question.** No definition of "calibrated" existed, so a tuning pass had no
stop condition. Worse, the performance budget was reported three ways and the
three disagree.

**Decision, part one — termination.** V7 is calibrated when seeds 1, 2, 3, 5,
and 8, at both 200 and 500 agents, each reach a decisive outcome — not `Draw` —
within **6,000 ticks**.

Six thousand was chosen against the measured `PersistentContingentsV4` spread,
which lands between 981 and 2,934 ticks across the same cells. The bound leaves
V7 room for the additional deliberation that weapon-relative footwork is
supposed to introduce, while making a standoff equilibrium a failure rather than
a curiosity.

**Decision, part two — the metric.** The budget is median `p50Milliseconds`
against `PersistentContingentsV4` on the same machine and the same seeds.
Ceilings are unchanged: 2.0× at 200 agents, 2.5× at 500. The
elapsed-divided-by-measured-ticks reading is **removed from the plan entirely**.

The three readings currently stand as follows:

| Reading | 200 agents | 500 agents | Verdict |
| --- | ---: | ---: | --- |
| Median elapsed | 8.57× | 7.84× | Fails |
| Median `p50Milliseconds` | 4.83× | 3.83× | Fails |
| Median elapsed ÷ median measured ticks | 1.75× | 2.30× | Passes |

The third row is deleted because it passes only by dividing V6's full
ten-thousand-tick run by V4's roughly two-thousand-tick run, which rewards V6
for never terminating. It cannot be a gate on a change whose entire purpose is
to make the simulation terminate.

**Measurement protocol.** Five seeds, one discarded warm run per cell, report
the median. This is the protocol both the Wasay and shield sessions already
used, and it is adopted rather than invented.

**Flagged before V7 begins.** `p50Milliseconds` is already a per-tick measure,
so fixing termination does not mechanically fix it. Part of the current 4.83×
and 3.83× is population rather than stage cost — V6 draws hold forty to
seventy-eight survivors per side alive at tick ten thousand while V4's late
ticks are nearly empty — so the ratio should fall once V7 terminates like V4.
It may not fall under 2.0×. `ResolveCollisions` already accounts for 58.11% to
77.44% of tick time per `docs/research/TICK-STAGE-PROFILE.md`. **If V7 meets the
termination bar and still fails `p50Milliseconds`, that is a performance pass
and separate work. It must not be recorded as a calibration failure.**

### D3 — The phase-flip metric is redefined; the 25% ceiling stands

**Question.** The shared acceptance criterion rejects a preset if phase or
posture flips on more than 25% of ticks after the first 100. The shipped rows do
not meet it, and cannot: a pure four-tick commitment plus four-tick recovery
rhythm produces exactly 25.0% on its own, before any genuine transition occurs.
Measured over ticks 101 to 400, Wasay ran 23.3% to 34.7%, Kalis reached 60%, and
Itak 50%.

**Decision.** Redefine the metric to count posture and intent changes only,
excluding the scripted `Commit` and `Recover` attack-lifecycle transitions. Keep
the 25% ceiling, applied to the redefined metric.

**Rationale.** A criterion that a legally specified rhythm fails on its own is
measuring the wrong thing. The redefinition preserves the criterion's actual
purpose — catching indecisive oscillation — and Kalis at 60% and Itak at 50%
remain real signal once the lifecycle is excluded.

**Consequence.** The criterion must be restated in full in the V7 design
document. Its only current home is the archive, which may not be cited as
authority. See the note in section 1.

### D4 — The shield entry thresholds are ratified as shipped

**Question.** `docs/research/movement/tall-hardwood-shield.md` proposes entering
disengagement at an ally-to-enemy ratio of 0.67 to 0.80 and leaving near 0.85 to
1.00. The shipped rows sit outside that band:

| Row | Shipped entry | As ally-to-enemy | Research band | Inside? |
| --- | --- | --- | --- | --- |
| `KS` | 17,500 basis points, 1.75 enemies per ally | 0.571 | 0.67 to 0.80 | No, below the band |
| `IS` | 15,000 basis points, 1.50 enemies per ally | 0.667 | 0.67 to 0.80 | At the bottom edge |
| Both | 11,000 basis points, 1.10 enemies per ally | 0.909 | 0.85 to 1.00 | Yes |

**Decision.** Ratify the shipped values. Both shield rows tolerating more
pressure before disengaging is the intended "protected deliberation" reading,
and it is now signed off as deliberate rather than sitting unowned.

**Consequence.** `docs/research/movement/tall-hardwood-shield.md` gains a note
recording the ratified entry values and the reasoning for their sitting below
the band it proposed. **The proposed band itself is not to be silently
overwritten** — it is a research finding, and the amendment records that
gameplay tuning departed from it, not that the research was wrong.

**No historical claim is affected.** Every value here carries its provisional
gameplay-tuning label under the policy in `CLAUDE.md` section 7, and none is
presented as a historical measurement.

### D5 — A single V7, covering every row at once

**Decision.** One new `MovementPresetId` entry covering all rows, rather than a
per-weapon V7, V8, V9 chain.

**Rationale.** D1 settles this on its own: a pressure interrupt in shared rules
moves every row's trajectory digest regardless of which scalars are touched. A
per-weapon chain would freeze an unshipped intermediate digest per weapon for no
benefit.

**Consequence.** `EquipmentRelativeFootworkV6 = 6` in
`src/Hukbo.Core/Movement/MovementPresetId.cs` stays frozen with its fixtures
intact. V7 is appended as a new value with its own content hash and its own
trajectory digest, per the determinism contract in
`SIMULATION-GAME-STANDARDS.md` section 4.

### D6 — The default does not move until D2 passes

**Decision.** `Scenario.MovementPreset` remains `PersistentContingentsV4`. The
flip to V7 is a separate decision, taken after the D2 termination bar and budget
are both met and evidenced.

## 3. What this authorizes

In dependency order. Each item is a candidate task for the V7 plan, not a
completed one.

1. Design the pressure interrupt: trigger condition, which phases it may
   preempt, its cost, and whether its threshold is shared or per-row.
2. Restate the redefined phase-flip criterion and the D2 acceptance bar in the
   V7 design document, in full, without citing the archive as authority.
3. Append `MovementPresetId.EquipmentRelativeFootworkV7`, leaving V6 and its
   fixtures byte-identical.
4. Implement the interrupt in `WeaponMovementRules`, gated so that only V7
   selects it and V1 through V6 replay unchanged.
5. Tune scalars against the D2 bar, after the interrupt lands — not before,
   because tuning thresholds no warrior can reach measures nothing.
6. Add the V7 content hash and trajectory digest fixtures.
7. Re-measure the five-seed, two-size workload and record the result against
   the D2 bar and the `p50Milliseconds` ceiling.
8. Amend `docs/research/movement/tall-hardwood-shield.md` with the D4
   ratification note.
9. Revisit D6 once, on evidence.

## 4. What this does not authorize

- Editing the V6 content hash, the V6 trajectory digest, or any V1 through V6
  fixture.
- Moving `Scenario.MovementPreset` off `PersistentContingentsV4`.
- Any performance work on `ResolveCollisions`. It is flagged in D2 as a likely
  separate pass, and flagging is not authorization.
- Rewriting the proposed band in `docs/research/movement/tall-hardwood-shield.md`
  rather than annotating it.
- Flipping any manual smoke-checklist row in `docs/development/testing.md`.
  Those remain `PENDING` and require a human at an interactive desktop.

## 5. Open items not decided here

- The shielded Kalis row was the weakest performer in the Wasay session's 8v8
  fixture, spending 162 of 400 ticks in `Refuse` and not reaching its first
  `Commit` until tick 259. Whether V7 addresses this is a tuning question for
  the design document.
- `MovementScenarioMatrix` has no group-roster generator, so group placements in
  every weapon session's fixtures are locally defined. If matrix-generated group
  rosters are wanted, that generator belongs in `MovementScenarioMatrix.cs`.
- No shared bound exists for minimum ally separation in mixed groups, for
  disengagement churn, or for a minimum number of landed attacks per fixture.
  All three were reported by the Wasay session as unasserted rather than
  smuggled into a test as an unreviewed threshold.
