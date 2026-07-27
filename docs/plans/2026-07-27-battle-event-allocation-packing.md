# Battle event allocation packing

Status: deferred debt. Not authorized for implementation yet — this file records
a measured regression and the conditions under which it must be paid down.

## What happened

The Philippine combat configuration work added two nullable fields to
`BattleEvent`:

```csharp
WeaponId? Weapon
BodyPart? HitLocation
```

Each nullable enum costs eight bytes including padding, so every emitted event
grew by sixteen bytes. `BattleSimulation` pre-sizes its per-tick event list at
`_agentStates.Length * 2`, which doubles the effect across the tick.

## Measured cost

The canonical seed-1, 200-agent, 10,000-tick headless workload was measured
before and after the change on the same machine and the same pinned SDK. Both
columns below were taken on the pre-collision build, whose seed-1 battle ended
at tick 235. They are a historical record of the change that created this debt,
and they are not comparable to the current baseline described further down:

| Metric | Before | After | Change |
| --- | --- | --- | --- |
| `allocatedBytes` | 12,108,304 | 15,128,696 | +3,020,392 (+24.9%) |
| Outcome | `Faction1Victory` @ tick 235 | `Faction1Victory` @ tick 235 | unchanged |
| Deterministic | yes | yes | unchanged |

`SIMULATION-GAME-STANDARDS.md` §8 requires that an allocation change above ten
percent be reported rather than absorbed silently. This document is that report.

The 15,122,504-byte figure that `docs/development/testing.md` recorded shortly
after this measurement is itself superseded. The current canonical seed-1,
200-agent baseline is `Faction1Victory` at terminal tick 1154 allocating
71,704,672 bytes, and the report-only 500-agent workload allocates 416,546,128
bytes at terminal tick 2668. Both figures live in `docs/development/testing.md`,
which is the single source of truth for the oracle.

Those later figures are not a like-for-like regression against the table above.
The collision and formation work that landed in between changed how long a
seed-1 battle runs, so the current workload pays for roughly five times as many
ticks as the tick-235 build did. `docs/development/testing.md` makes the same
point about its own superseded numbers and deliberately declines to state a
ratio between them.

No allocation ceiling survives in `docs/development/testing.md`. The
19,856,712-byte figure that this document previously cited now exists only in
`docs/archives/`, which is reference-only by repository policy and may not be
used as a live budget. This therefore remains debt rather than a blocker on a
different and still-current ground: the cost is a fixed sixteen bytes per
emitted event, it scales linearly with the number of events a battle emits
rather than compounding, and no gated stage fails because of it. The
measurement varies by a few thousand bytes between runs, so compare against it
with that tolerance in mind.

## Why it was not fixed in place

The two candidate fixes both change how an event is encoded into the event hash:

1. Store the fields as non-nullable `WeaponId` and `BodyPart` with an explicit
   `None = 0` member, removing the `ulong.MaxValue` null sentinel that
   `HeadlessRunner` currently folds.
2. Pack both fields into two `byte` fields.

Either one changes the event-hash encoding. Per `CLAUDE.md` §5 that requires a
new preset version plus new golden expectations, which is a deliberate
determinism change and not something to fold into a branch consolidation.

## Conditions for paying this down

Do this work when one of the following is true, and open it with a design
document first:

- a preset version bump is already being made for another reason, so the golden
  re-record is paid once rather than twice;
- the 500-agent stress workload's allocation grows materially against the
  416,546,128-byte figure recorded in `docs/development/testing.md`, compared at
  a similar battle length so the comparison means something;
- Gate 3 in `SIMULATION-GAME-STANDARDS.md` is being prepared, since the
  save/resume equivalence and 500-agent stress report both read this budget.

## Verification criteria when it is done

- New preset version registered, with the previous version still resolvable.
- New golden state hash and event hash recorded in
  `docs/development/testing.md`, with the old values kept alongside and labelled
  by preset version.
- `allocatedBytes` for the seed-1, 200-agent workload measured and recorded.
- `./scripts/verify.ps1` passes, with the actual output pasted into the plan.
- The ordered field list in `.claude/skills/hukbo-determinism-change/SKILL.md`
  updated to match the new encoding.
