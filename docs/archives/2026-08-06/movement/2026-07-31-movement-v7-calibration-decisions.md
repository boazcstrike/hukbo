# Movement V7 calibration — decisions taken

> **Archived: reference only.** The movement V7 pressure-interrupt workstream
> finished and merged to main on 2026-08-06. V7 shipped as a registered, pinned,
> fully tested preset that is reachable only by explicit selection, and it does
> not meet the design section 2.1 termination bar at any tuning. Decision D6
> stands: `Scenario.MovementPreset` remains `PersistentContingentsV4`. Do not
> execute this plan; its task list, line numbers, and verification steps are
> historical. The dated annotations inside record where measurement overturned
> what the document originally claimed.

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

**The interrupt's shape, decided 2026-07-31.** An earlier revision of this
document left the four questions below open for the design document. They were
answered by the user the same day, after the first commit of this file, and are
recorded here so the repository carries them rather than only the conversation.

1. **Trigger.** A weighted sum of three signals — support ratio, incoming
   damage, and ally count collapse — where the combined weight crossing a
   threshold fires the interrupt. Not any one signal alone.
2. **Scope.** It may preempt `Commit`, not only `Recover`.
3. **Cost.** Breaking off re-charges `AttackCooldownRemaining` to full from the
   movement stage. This replaces an earlier phrasing, "forfeits the swing",
   which research showed is not implementable as written — see below.
4. **Threshold.** Per profile row.

**5. Weights.** Three weights, shared across all six rows, with only the trigger
threshold varying per row. This began as an assumption stated to the user and
not contradicted; it was explicitly confirmed on 2026-07-31 before phase 1 of
the plan began. The reason to settle it early rather than late: per-row weights
would move the V7 content hash and force a fresh trajectory digest, so changing
it after tuning is expensive, and it would introduce eighteen provisional values
instead of three — the pattern that produced the unsigned-off values D4 had to
ratify retroactively.

**Why "forfeits the swing" had to be redefined.** `FootworkPhase.Commit` is
post-swing follow-through, not wind-up. `GatherAndCommitAttacks` runs at
`src/Hukbo.Core/Simulation/BattleSimulation.cs:605` and fully resolves the blow —
damage applied, `Attack`, `Damage`, and `Death` events emitted — and only then
does `ApplyEquipmentAttackFootworkAndDeathCleanup` at `:611` stamp `Commit` on
surviving attackers who landed one (`:2603-2607`). A warrior inside `Commit` has
already swung. There is no pending swing to cancel, so the cost had to be
expressed as something other than cancellation.

Re-charging the cooldown was chosen over two alternatives: letting the disengage
step break attack range and suppress the next attack through the existing gate at
`:3392` (no new coupling, but the cost is only incurred when the step actually
breaks range), and setting a scratch flag read at the cooldown gate `:3398` (a
guaranteed cost, but it reverses the invariant documented at `:2580-2582`).

**The risk this choice accepts, and how the plan contains it.**
`AttackCooldownRemaining` today has exactly one non-decrement writer,
`ResolveComboTransition` at `:3724`, inside the combat stage. The interrupt adds
a second writer, in a different tick stage, with movement writing a combat field.
`ComboChainTests` cannot catch the resulting contract erosion, because its
fixtures run under `PersistentContingentsV4`, where `UsesEquipmentRelativeFootwork`
is false and `FootworkPhase` is `None` on every agent. The plan therefore carries
a task to add combo-chain coverage under a footwork preset, and the interrupt's
write must be V7-gated and documented at both writer sites.

**Consequences already established by research.** Two of the three trigger
signals are histories rather than queries over current state: incoming damage
needs a window, and ally count collapse needs a prior-tick comparison. Both
require new authoritative per-agent state that reaches the state hash. They do
not, however, touch save/resume: there is no save/resume path in this repository
at all. `BattleSnapshot` is an outbound record with no deserializer, and
`SIMULATION-GAME-STANDARDS.md` places save/resume equivalence in Gate 3, not yet
reached. Adding persistent state costs a trailing member on `AgentView`.

**The hardest constraint, found independently by two research agents.**
`MovementRuleset.ContentHash` **does** reach the state hash under V6:
`BattleSimulation.cs:654-656` passes it to `StateHasher.Compute`, which folds it
at `StateHasher.cs:81-84`. The class remarks at `MovementRuleset.cs:21-27` and
`MovementPresetRegistry.cs:18-23` both state the opposite in prose; that was true
through V5 and is false for V6. Consequently, adding any content-hash-folded
field — a per-row interrupt threshold, for instance — moves V6's `ContentHash`,
moves V6's state hash from tick 1, and breaks the frozen V6 digest that D5
requires stay byte-identical. The same trap exists on the per-agent side:
`StateHasher.cs:122` gates the five footwork fields on a condition that V7 will
also satisfy.

The resolution is conditional folding under a **V7-specific gate** —  not
`UsesEquipmentRelativeFootwork`, which V7 must also set. This follows the idiom
`StateHasher` already uses at `:118-126`, where `Rank` folds only when
`hasRankLevels` and the footwork fields only when the movement hash is non-null,
precisely so that legacy presets keep their byte layout. The stale remarks in
both files are corrected as part of this work.

### D2 — Termination bar and the single budget metric

**Question.** No definition of "calibrated" existed, so a tuning pass had no
stop condition. Worse, the performance budget was reported three ways and the
three disagree.

**Decision, part one — termination.** V7 is calibrated when seeds 1, 2, 3, 5,
and 8, at both 200 and 500 agents, each reach a decisive outcome — not `Draw` —
within **6,000 ticks**.

**Corrected 2026-07-31 against measured evidence.** Six thousand was originally
justified against a `PersistentContingentsV4` spread of "981 to 2,934 ticks".
That range was wrong: it mixed one endpoint from each combat preset. 981 is a
combat-`PrecolonialPhilippinesV4` figure, measured with no shield row fielded at
all, and 2,934 is a combat-V2 figure that turns out to be the *lowest* of the
five 500-agent readings rather than the highest.

The measured V4 spread under the pinned combat preset V2 that D2's own protocol
requires is **1,279 to 4,405 ticks**
(`docs/archives/2026-08-06/movement/2026-07-31-movement-v7-baseline.md`, twenty cells, commit
`b3ab856`). The bar therefore leaves roughly 36% headroom over the true
reference maximum, not the near-doubling the original figure implied.

The bound is retained at 6,000 on the corrected evidence. It still sits well
above every measured V4 cell and well below the 10,000-tick cap that defines the
standoff failure, so it continues to discriminate between "terminates" and
"deadlocks". Two qualifications now attach to it. A V7 result between 4,405 and
6,000 is a pass but is worth reading as a regression against V4 rather than a
clean win. Calibration should target the 500-agent cells, because that is where
the bar binds: the 500-agent group's *shortest* V4 run, 2,551 ticks, already
exceeds the 200-agent group's longest at 2,284.

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

**The workload runs under combat preset `PrecolonialPhilippinesV2`, pinned
explicitly.** The shipped combat default `PrecolonialPhilippinesV4` rosters four
solo loadouts and never pairs a shield with any weapon
(`src/Hukbo.Core/Combat/PhilippineCombatPresetV4.cs:194-197`), so a workload run
under the default would never field `KS` or `IS` — the two rows whose zero-window
attack lifecycle motivated the interrupt. The V6 freeze fixture already pins V2
for this same reason (`tests/Hukbo.Core.Tests/MovementPresetFreezeTests.cs:336`).

**A baseline must be recorded before V7 work begins.** No recorded seed-1
10,000-tick result corresponds to today's shipped default pair. The pairs in
`docs/development/testing.md` and in the `hukbo-determinism-change` skill were
each measured under earlier defaults. D2 is a comparison against
`PersistentContingentsV4`, so that comparison has no "before" until one is
measured and recorded.

**Four of six rows, not two, never reach the disengage test.** Fall-through ticks
per steady-state cycle are `max(0, cooldown - (commitment + recovery))`. Under
combat V2 that gives `KS` 5-6, `IS` 4-6, `WA` 8-8, and `IT` 4-4 — all zero. Only
`KP` (7-6) and `KA` (5-4) get a one-tick window. The problem is broader than the
two shield rows that surfaced it.

**Flagged before V7 begins.** `p50Milliseconds` is already a per-tick measure,
so fixing termination does not mechanically fix it. Part of the current 4.83×
and 3.83× is population rather than stage cost — V6 draws hold forty to
seventy-eight survivors per side alive at tick ten thousand while V4's late
ticks are nearly empty — so the ratio should fall once V7 terminates like V4.
It may not fall under 2.0×. `ResolveCollisions` already accounts for 58.11% to
77.44% of tick time per `docs/research/TICK-STAGE-PROFILE.md`. **If V7 meets the
termination bar and still fails `p50Milliseconds`, that is a performance pass
and separate work. It must not be recorded as a calibration failure.**

> **Annotation, 2026-08-06 (task F2). D2 is written as a bar V7 would meet. V7
> does not meet it, and no tuning of the V7 values can.**
>
> The decision itself is sound and is not withdrawn: the 6,000-tick bar remains
> the right discriminator between a preset that terminates and one that
> deadlocks, and the corrected 1,279-to-4,405 V4 spread above was reproduced
> exactly by task F2's own same-session V4 arm. What has changed is the outcome,
> not the standard.
>
> Measured under V7 at the final pinned values, **zero of ten cells reach a
> decisive outcome; all ten end `Draw` at the 10,000-tick limit.** Task E1
> measured six candidate tunings and every cell drew under every one of them,
> including a probe registering the minimum legal threshold on every row, which
> fires the predicate on every agent-tick it can ever fire on. Across those
> candidates the firing count ranged over a factor of 4.6 and no cell's terminal
> tick moved by a single tick.
>
> The `p50Milliseconds` half of D2 also fails, at 3.44× against a 2.0× ceiling
> at two hundred agents and 4.02× against 2.5× at five hundred. **D2's own
> provision that a `p50` failure is separate work applies only when the
> termination bar passes, so it is unavailable here** and both readings are
> recorded as plain failures. Section 7 of
> `docs/archives/2026-08-06/movement/2026-07-31-movement-v7-calibration-record.md` carries the full
> twenty-cell evidence.
>
> Neither failure is a defect in this decision. The cause sits upstream of
> anything V7 touches: warriors hold `FootworkPhase.Refuse` and the regroup
> posture for roughly 349 ticks out of every 350, so the interrupt's entire
> addressable population is about 0.3% of agent-ticks.

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

> **Annotation, 2026-08-06 (task F2). The condition this decision waits on can
> never be met by V7, so D6 is now permanent as far as V7 is concerned.**
>
> D6 is worded as a deferral — the default moves *once* D2 passes. Read today
> that wording invites a future session to re-run the matrix and check whether
> the bar has come within reach. It has not and it will not: task E1 measured
> six tunings including the maximum-intervention limit case, and the annotation
> on D2 above records that no tuning of the values V7 owns moves a single cell's
> terminal tick. There is no pending condition here, and re-measuring V7 to
> see whether it now passes is wasted work.
>
> `Scenario.MovementPreset` therefore stays `PersistentContingentsV4`, and the
> calibration record is evidence *against* flipping it rather than the evidence
> D6 anticipated collecting *for* it.
>
> This does not close the question of moving the default off V4 in general. It
> closes moving it to V7. A preset that addresses the refuse-and-regroup loop
> upstream could meet the D2 bar and would be judged by this same decision on
> its own evidence; it does not exist, and designing it is not authorized by
> this document.
>
> One consequence is recorded in `docs/development/testing.md`: because V7 is
> unreachable from the client and the default does not move, the nine smoke
> rows covering the interrupt's spectator channels are `BLOCKED` rather than
> `PENDING`. No human can run them.

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
