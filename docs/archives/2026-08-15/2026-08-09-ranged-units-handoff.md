# Ranged units — session handoff, 2026-08-09

**Archived: reference only.** The ranged units package is closed. It merged to
`main` on 2026-08-09 at `9daa271`, its eleven `RG-*` smoke rows were run by a
person at an interactive desktop and passed on 2026-08-14, the sixty sound takes
are generated and committed, the listening acceptance closed, `RG-11`'s open
question was answered, and the default composition moved onto the calibrated
proportions on the same day. Never execute this handoff, never treat it as a
live task list, and never cite it as the reason to make a change. The live
contract remains `CLAUDE.md`, `SIMULATION-GAME-STANDARDS.md`,
`docs/development/testing.md`, and `docs/development/smoke-checklist.md`.

Three things survive it and are carried in `docs/plans/TODO.md` rather than only
here: the V9 termination gap, which the user accepted with the gap recorded
rather than fixed; Phase 2's line of sight and friendly fire, which the ranged
design defers by construction; and section 6's `HUKBO_AUTOPLAY` proposal, which
was put twice and never taken up.

Supersedes the 2026-08-08 ranged units handoff, which is frozen at wave 4 and
whose every count, commit and status is stale. Read that one only for durable
reasoning.

**The package's goal has been reached.** A ranged battle has been played by a
person, with sound, without crashing. That had never happened before today.

Branch `ranged-units` was merged to `main` on 2026-08-09 at `9daa271`. The tag
`pre-main-merge-2026-08-09` marks the pre-merge state. **Corrected on
2026-08-10:** this document originally said the merge had not been done, which
was true when it was written and false a few hours later. Every other statement
in it still holds.

## 1. What this session did

| Commit | What |
| --- | --- |
| `dd43281` | Corrected the calibrated Timawa share from 54 per cent to 44 |
| `7f22c57` | Rewrote the sound prompts that were instructing the model to be quiet |
| `de4441a` | Added `scripts/sfx-ranged.ps1`, the driver for RU-31's sixty takes |
| `6258ae5` | Fixed the weapon-label and grip-resolution crashes |
| `055ff84` | Fixed the blood-spray crash, and swept for its siblings |
| `78fc9ed` | Design document for projectile props and embedded projectiles |

The full narrative, with the measurements, is in section 9 of the ranged units
plan — archived on 2026-08-14 under the title "Ranged units — plan" — beneath
that section's 2026-08-09 heading. It is the record; this document is the index
to it.

## 2. Verified state, re-established from scratch this session

The canonical gate passes in full, run by the orchestrator, never delegated:

```
Formatted 0 of 707 files.        [PASS] Formatting verification completed.
Total tests: 2433   Passed: 2433
Total tests: 3348   Passed: 3348
eventHash AC55684F24D39344  stateHash 1B73FC5923879AA0  combat 4 / movement 4
eventHash F709A345E2F7370E  stateHash C8023D3B5BEB005E  combat 5 / movement 8
[PASS] Canonical repository verification completed.
```

Core is unchanged at 2,433. The Client suite rose from 3,333 to 3,348 as the two
crash regression guards landed. Every hash is identical to the figure recorded
before this session began, which is the proof that nothing done here reached a
simulation.

All seven registered preset combinations were re-measured at 200 agents, 10,000
ticks, seed 1 and reproduced their recorded pairs byte for byte. Use named
parameters — `-Preset` and `-MovementPreset` — never positional ones. A ranged
roster runs under V4 or V8 movement and nothing else.

## 3. The ranged sound takes are generated, and the listening check is now closed

All sixty sound files exist. `./scripts/sfx.ps1 -List` reports zero missing of
twenty-six slots. **The user ran every generating command; no agent generated a
sound, and none may.**

The acceptance criterion is not "the files exist". It is that a person has heard
at least one take from each of the thirteen new slots.

**Closed on 2026-08-14.** The user listened and accepted the takes. That is the
only form of evidence this item was ever able to take, and no agent supplied it
or could have.

**2026-08-13.** A `Hukbo.Tools.RenderProbe` run drove a ranged battle to tick
3,584 with zero `err` lines, and all thirteen ranged slots were submitted to the
real mixer and reported `Played` — `ReleaseBangkaw` 1,565, `ReleaseBusog` 1,483,
`ReleaseArquebus` 387, the three `Attack` slots, the three `Miss` slots and the
three `ClashShield` slots. That proves every slot is reachable and loadable. It
was not a person listening, and it was not what closed this item; the listening
session recorded above is.

Two operational facts worth keeping. The model returns an inaudible take roughly
one time in four, and `scripts/sfx-ranged.ps1` retries only that failure while
rethrowing anything else immediately, so a missing key does not become sixty
pointless requests. And a slot that stays quiet across every attempt is a prompt
problem, not a threshold problem: the words "thin", "soft", "grazing" and
"shallow" read to the model as instructions to be quiet. Never reach for
`-AllowQuiet`; RU-20 measured the mix headroom assuming real levels.

## 4. The lesson this session paid for, which is the one worth carrying

Three crashes, all in one play session, all the same shape: an exhaustive switch
over a weapon that nobody extended when the ranged three landed. That brings this
package's count of that defect to nine.

**The gate cannot see any of them, and that is structural rather than
accidental.** The headless workload never formats an event, never opens an
inspector, and never draws blood. Those three subsystems are outside every
automated check the repository has, and each defect was found only by a person
pressing play. Two of the three were found only *after* fixing the one above
them, so the count was not knowable in advance.

The third one is the most instructive. `BloodGeometry.GetSprayProfile` had no arm
for any ranged weapon, and **this package's plan does not contain the word
"blood" anywhere**. No task ever owned the file, so no amount of executing the
plan correctly would have found it.

After the third fix, every weapon-keyed switch in `Hukbo.Client` was swept rather
than waiting for a fourth crash — anchored on both `Itak =>` and `Kampilan =>`
arms, and again at file level. Five further candidates all turned out complete.
That sweep is the strongest static claim available, and it is still not a
substitute for playing the game.

## 5. What is open

**The merge to `main` happened on 2026-08-09 at `9daa271`,** after this section
was written.

**As of 2026-08-14 this section records almost nothing that is still open.** The
package's remaining questions were put to the user on that date and answered:
the listening acceptance closed, RG-11's question answered, and the default
composition moved onto the calibrated proportions. What survives is the V9
termination gap, which the user accepted with the gap recorded rather than
fixed, and Phase 2, which the design defers by construction rather than leaves
undone. Each entry below is kept in place and marked with what happened to it,
because a closure is only trustworthy if the thing it closed is still legible.

- **The eleven `RG-*` rows were closed `PASS` on 2026-08-14.** They had lived in
  `docs/development/smoke-checklist.md`, not in `docs/development/testing.md` as
  this document originally said, and only a human at an interactive desktop may
  flip such a row — no agent may flip one, for any reason, including a passing
  test. That is exactly what happened: a person at an interactive Windows
  desktop ran all eleven rows on 2026-08-14 and every one passed. The ranged
  units smoke section was then deleted whole from the smoke checklist, and its
  record now lives in the 2026-08-14 archive titled "Ranged units smoke —
  closed 2026-08-14". One caveat survived that closure and has since been
  resolved: RG-11 was never a pass/fail check, only an open question about
  whether a projectile visibly passing through a friendly warrior looks wrong,
  and it was first closed with no written observation recorded. **The
  observation was supplied by the user on 2026-08-14: it does not look wrong.**
  A spectator watching at the pace and scale of a real battle does not notice
  the projectile passing through the friendly warrior, so the Phase 1 gap is
  not visible on the one channel that could have exposed it. That does not make
  line of sight unnecessary — it is still owed, and Phase 2 still carries it —
  but it does mean the gap is not costing the spectator anything today, and it
  removes the argument for treating Phase 2 as urgent rather than merely owed.
- **The sixty WAV files are committed.** `src/Hukbo.Client/Content/Audio` holds
  130 tracked `.wav` files — the 70 that predate this package plus RU-31's 60,
  spread over the thirteen new slots. Re-rolling a take after listening is still
  fine; it is now an ordinary edit rather than a first commit.
- **The V9 termination gap.** 14 of 20 decisive seeds against a bar of 19. V9 is
  opt-in, V4 remains the shipped default, and the user accepted it with the gap
  recorded. A second cause exists and is unidentified. Do not retune to chase it;
  the refusal counters at `BattleSimulation.cs:437` are the instrument for a fresh
  investigation.
- **The default composition has been moved onto the calibrated proportions.**
  It previously played a 14 per cent ranged share against a calibrated 25, on an
  even four-way split of 250 per team. Every plan band passed at 14, measured
  rather than assumed, so the move was a preference rather than a correction.
  **The user took the decision on 2026-08-14 and the calibrated basis was
  adopted.** The rank counts are 250 x `[19, 19, 44, 18] / 100` — Datu 48,
  Maharlika 47, Timawa 110, Aliping Namamahay 45 — and note the 44, not the 54
  this plan used to say. `ArmyComposition.Default` is a `Hukbo.Client` settings
  value and reaches no state hash, no event hash, and no preset, so the change
  needed no new preset version; what it did need was a schema-version bump, on
  the precedent of the 5-to-6 bump, because a saved composition always wins over
  the default and an existing settings file would otherwise have pinned the old
  army forever.
- **Projectile props and embedded projectiles — no longer open.** This was
  parked in [`TODO.md`](TODO.md) when this handoff was written. Both halves
  shipped at `3ec5523` on 2026-08-11, the five open decisions were answered by
  the plan, and all eight `PP-*` smoke rows have since closed `PASS`. The design
  and plan are archived under the titles "Projectile props and embedded
  projectiles — design" and "Projectile props and embedded projectiles — plan".
  The quad budget `SubmissionCount.cs` warns about by name is still the live
  constraint on the next feature wanting a per-pawn quad.

## 6. Still unsolved, and still worth solving

**A scripted launch cannot start the battle.** `PlaybackController.IsPlaying`
defaults to `false` (`PlaybackController.cs:5`) and the only producer of `Play()`
is `ClientCommand.Play` at `ArenaGame.cs:1259`, reachable only from input. So an
agent-driven run renders a paused battle forever and `simTicks` stays 0.

That is what made this session's crash hunt cost three rounds of a person pressing
play. An `HUKBO_AUTOPLAY=1` opt-in, read once at construction exactly as
`HUKBO_RENDER_PROBE` already is at `ArenaGame.cs:248`, plus a `-AutoPlay` switch on
`run.ps1`, would let a scripted Debug run drive a battle to completion and surface
the next defect of this class without a person in the loop. It was proposed twice
this session and not taken up; it is not authorized, and it would be a new
Client feature needing its own row.

It would not let an agent flip a smoke row. Only a person may do that. It would
only mean the agent finds the crash first.
