# Leader marker and inspector annotation smoke — six rows closed 2026-08-13

**Archived: reference only.** This is the record of six rows that closed. It is
not a checklist, it is not current, and nothing outside `docs/archives/` may
link to it. The one row of this family that did not close stayed in
`docs/development/smoke-checklist.md`.

## What this family covered

The leader-rank change (leader rank plan tasks L4 and L5) added two spectator
channels: a leader mark drawn above the head of the ranking member of each
contingent, and a `(leading)` suffix on the agent inspector's contingent line.
`ExactlyOneLivingLeaderPerNonEmptyContingentAcrossEveryRegisteredMovementPreset`
in `BattleSimulationTests`, together with the `AgentInspectorContentTests`
assertions, already proved that `AgentView.IsLeader` was wired correctly and
that the suffix appeared exactly when it should. None of that proved the marker
read as intended on a real battlefield, which is what these rows were for.

The family stood entirely `PENDING` from the day it was written until
2026-08-13.

## The run

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-13 |
| Machine/platform | Not recorded |
| Source commit | Not recorded |
| Launch path | `source`, via `./scripts/run.ps1` |
| Optional screenshot paths | None recorded |
| Movement preset actually running | `BattlefieldRealismV10`, the client's hardcoded default (`src/Hukbo.Client/ArenaGame.cs:1442`) |

The tester at the desktop reported the family as passing. Six of the seven rows
are recorded below as `PASS` on that report.

## The six rows that closed

| # | Step | Expected | Status |
| --- | --- | --- | --- |
| L-1 | Look at the battlefield at default zoom under a contingent-aware movement preset | Exactly one warrior per visible, non-empty contingent shows the leader mark above its head | PASS |
| L-2 | Watch a contingent whose leader is killed | The leader mark visibly moves to a different warrior once the next scan reassigns leadership | PASS |
| L-3 | Select the current leader | The selection ring and the leader mark are both visible at once, not fighting for the same screen space | PASS |
| L-4 | Watch the leader die in the event feed, before the next scan | The dead mark (crossed lines) and the leader mark are both visible on that one warrior for that one tick | PASS |
| L-5 | Click the current leader to open the inspector | The contingent line reads `Contingent: {id} — {label} (leading)` | PASS |
| L-6 | Click a non-leader member of the same contingent | The contingent line carries no `(leading)` suffix | PASS |

## Read L-1's preset wording before citing this record

L-1 as written named `PersistentContingentsV2` through `V5`. The client cannot
select any of them: `BuildScenario` hardcodes `BattlefieldRealismV10`. The row
was therefore observed under V10, which is contingent-aware and assigns exactly
one leader per non-empty contingent like every other registered preset, but
which sets `selectsLeaderByRank: false`
(`src/Hukbo.Core/Movement/MovementPresetRegistry.cs:555`) where V2 through V5
and V6 through V9 differ among themselves on that flag.

The observation is sound for the question the row was asking — whether the mark
reads on a real battlefield — and that is why the row closed. It is not evidence
about rank-based leader selection specifically. If a later question turns on
that, write a fresh row rather than citing this one.

## Why L-7 did not close, and where it went

L-7 read: *launch under `IndependentPursuitV1`; no warrior ever shows the leader
mark, and no inspector contingent line ever carries `(leading)`.* It is the
family's gating row — the one that proves the feature is off when it should be
off.

It could not be run. `IndependentPursuitV1` is unreachable from the client for
exactly the reason the nine `P` rows were `BLOCKED`: `ArenaGame.BuildScenario`
overrides the preset to `BattlefieldRealismV10` and no preset selector is
exposed. A tester at the desktop has no supported route to a V1 battle, so the
row was reclassified `BLOCKED` rather than recorded as passing on a preset
nobody ran.

It stays in `docs/development/smoke-checklist.md`, moved into the section that
carries the other rows blocked by the same missing selector. The design that
addresses it is `2026-08-13-pressure-interrupt-observability-design.md`.

## What is still not proven by anything

The rows above were observed at default zoom in one pass. Nothing here proves
the mark's behaviour at the Low detail tier, under heavy overdraw, or against
the adornment accent at maximum zoom — `LC-2` and `LC-8` of the leader
identification family cover those and were not part of this run.
