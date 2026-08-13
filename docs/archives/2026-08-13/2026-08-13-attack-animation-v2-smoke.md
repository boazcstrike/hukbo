# Attack animation V2 smoke — closed 2026-08-13

**Archived: reference only.** This is a finished record of manual testing that
already happened. Never execute it, never treat it as a live task list, and
never cite it as the reason to make a change. The live contract for this project
remains `CLAUDE.md` and `docs/development/smoke-checklist.md`; nothing in this
file is outstanding and nothing in it is an instruction.

**The family closed whole**, so its section was deleted from the live checklist
rather than left there as a record. All twenty-four rows have passed.

**One caution before this file is read as an all-clear.** `AA-23` closed on a
run in which nothing had been fixed, and the two causes measured against it that
same day are still true of the build. They are recorded in the "What closed
without a fix" section below, and they are worth reading before anyone concludes
that a warrior's stride reads correctly under every camera station.

| Field | Value |
| --- | --- |
| Rows in the family | 24, numbered `AA-1` to `AA-24` |
| Rows closed `PASS` and lifted here | 18 — `AA-5`, `AA-7` to `AA-16`, and `AA-18` to `AA-24` |
| Rows lifted earlier | 6 — `AA-1`, `AA-2`, `AA-3`, `AA-4`, `AA-6`, and `AA-17`, which left on 2026-08-11 into the record titled "Closed rows lifted out of families that are still open" |
| Rows still open in the live checklist | None. The section was deleted |
| Lifted on | 2026-08-13 |
| Live checklist | `docs/development/smoke-checklist.md` |

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-13 |
| Machine/platform | Not recorded |
| Source commit | Not recorded. The working tree at the time was `8da5d92` plus uncommitted lethal-blow legibility, auto-camera, and documentation changes |
| Launch path (`source` or package path) | Not recorded |
| Optional screenshot paths | None recorded |

## What these rows were for

The attack animation V2 system lives entirely in `Hukbo.Client`. Its automated
tests already proved the weapon-motion catalog, the contact-latched timeline,
the target-local geometry, the articulated arm rectangles, the defender
reaction offsets, the shield overlay legality, the motion-intensity policy, the
quad accounting, and the conservative cull's containment of all of it.

None of that proves that a Kampilan reads differently from a Kalis on screen,
that a blow appears to land on the warrior it names, or that a dense battle of
two hundred warriors striking at once reads as combat rather than as noise.
That is the only thing these rows were ever for, and it is why only a person at
an interactive Windows desktop could close one. Design:
`docs/plans/2026-08-08-attack-animation-v2-design.md`.

## The two earlier runs, and why they closed nothing

Two interactive runs were made on 2026-08-09 against `codex/attack-animation-v2`
at `3a63bb1`, both `Debug`/`dbg`, fullscreen 2048x1152, on the shipped 500-agent
default scenario. Logs: `artifacts/logs/hukbo-20260808-214856-3108.jsonl` (one
battle, 107 s) and `artifacts/logs/hukbo-20260808-215507-26172.jsonl` (two
battles, 224 s, three pause cycles, one Next Round). Across both: 6 386 Itak,
4 934 Kalis, 4 284 Kampilan and 2 805 Wasay attack cues, 1 478 deaths, and no
`warn` or `err` line of any kind — in particular no
`render.attack.contact.collapsed`, so the five-bundle per-attacker buffer never
overflowed.

Those runs were made at 500 agents, and the observer's recurring report was that
individual exchanges could not be attributed at that density. Several rows below
therefore carry a 2026-08-09 observation that was *not* a pass, followed by the
2026-08-13 verdict that closed them. Both are kept. The 2026-08-09 text is the
reason the row stayed open for four days, and deleting it would make the closure
look easier than it was.

The render probe measured the attack path directly at 200, 500, and 1 000 agents
across all three camera stations, with every station recording at least one frame
holding an active attack pose, peaks of 2 to 20 poses per frame:
`artifacts/attack-animation-v2/render-matrix.json`. That is a performance
measurement, not a visual one, and it flipped no row.

## The rows that closed

The tester reported the 2026-08-13 run as a block rather than row by row: every
row from `AA-5` through `AA-21` passed, then `AA-24` passed, and then `AA-22`
and `AA-23` passed in a second pass later the same day. No separate observation
was recorded for any individual passing row, so the `Actual` column below says
exactly that and no more. Nothing here should be read as a detailed finding that
was never made.

| ID | Action | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| AA-5 | Watch each of the four weapons at 1x, 2x, and 4x | Every blow stays individually visible; nothing blurs into a single continuous motion at 4x | 2026-08-13, tester at the desktop. Run as part of the `AA-5` to `AA-21` block; passed, with no separate note recorded. | PASS |
| AA-7 | Watch a blow a shield blocks | The defender braces into the contact rather than being driven back, and the clash reads on the shield | 2026-08-09: outcomes looked distinct, but the observer could not follow which outcome resolved which exchange in a live 500-agent battle, so the row was not certifiable at that density. **2026-08-13, tester at the desktop: passed**, with no separate note recorded. | PASS |
| AA-8 | Watch a parried blow | Attacker and defender weapons visibly meet and redirect across the line of the blow | 2026-08-09: as `AA-7` — distinctness observed, individual attribution not possible at that density. **2026-08-13, tester at the desktop: passed**, with no separate note recorded. | PASS |
| AA-9 | Watch a deflected blow | A shallower glance than the parry, continuing rather than reversing | 2026-08-09: as `AA-7` — distinctness observed, individual attribution not possible at that density. **2026-08-13, tester at the desktop: passed**, with no separate note recorded. | PASS |
| AA-10 | Watch an evaded blow | Full follow-through with no blood, no clash cross, and no contact recoil | 2026-08-09: as `AA-7` — distinctness observed, individual attribution not possible at that density. **2026-08-13, tester at the desktop: passed**, with no separate note recorded. | PASS |
| AA-11 | Watch a two-blow combo from one warrior | The second contact installs a new blow rather than restarting the first; the return side changes | 2026-08-13, tester at the desktop. Run as part of the block; passed, with no separate note recorded. | PASS |
| AA-12 | Watch a lethal blow at close zoom | The victim stays visible long enough for the weapon to reach it, then falls; it does not vanish before contact | 2026-08-13, tester at the desktop. Run as part of the block; passed, with no separate note recorded. | PASS |
| AA-13 | Watch a shielded Kalis warrior strike (registered V2 replay) | The block stays between the defender and the weapon line; the weapon arm does not cross or hide it | 2026-08-13, tester at the desktop. Run as part of the block; passed, with no separate note recorded. | PASS |
| AA-14 | Watch a shielded Itak warrior strike (registered V2 replay) | As `AA-13`, with the compact chop rather than the thrust | 2026-08-13, tester at the desktop. Run as part of the block; passed, with no separate note recorded. | PASS |
| AA-15 | Watch attacks at Low, Medium, and High detail | Low keeps direction and outcome with no arms and no trail; Medium and High draw the full rig | 2026-08-09: articulated arms were present but reported as "not significantly seen" at the zoom used, and the three tiers were never compared against each other. **2026-08-13, tester at the desktop: passed**, with no separate note recorded. | PASS |
| AA-16 | Set motion to Full, then Reduced, then Off | All three keep direction, reach, and which outcome resolved the blow; Reduced damps the body; Off removes the trail entirely | 2026-08-13, tester at the desktop. Run as part of the block; passed, with no separate note recorded. | PASS |
| AA-18 | Pause during a catch-up burst, then resume | Queued contacts resume in order and none is duplicated or lost | 2026-08-13, tester at the desktop. Run as part of the block; passed, with no separate note recorded. | PASS |
| AA-19 | Next Round, then Full Reset, during active combat | Every attack pose, pending contact, reaction, and transient effect is cleared by both | 2026-08-09: Next Round was exercised and the second battle ran clean, but Full Reset was never triggered, so half the row was unproven. **2026-08-13, tester at the desktop: passed**, with no separate note recorded. | PASS |
| AA-20 | Watch a 200-warrior battle at close zoom | Individual exchanges are readable; the arms and trails do not obscure who is fighting whom | 2026-08-13, tester at the desktop. Run as part of the block; passed, with no separate note recorded. | PASS |
| AA-21 | Watch a 200-warrior battle at default fit | The formation still reads as a formation | 2026-08-13, tester at the desktop. Run as part of the block; passed, with no separate note recorded. | PASS |
| AA-22 | Watch a 500-warrior stress battle at minimum, default-fit, and maximum zoom | Frame pacing stays comfortable and the field does not turn into visual noise at any of the three | 2026-08-09, over two full 500-agent battles: `FAIL` — the animations overlapped and the battle read as chaos; the observer could not tell what was happening. Frame pacing was never the difficulty. **2026-08-13, tester at the desktop: passed**, with no separate note recorded. Nothing between the two runs changed the 500-agent density — see below. | PASS |
| AA-23 | Watch a warrior strike while moving | The attack plants the stance and composes with the stride; the body does not jump between two poses | 2026-08-13, first attempt: `FAIL` — no warrior visibly striking while walking. **Later the same day, tester at the desktop: passed**, with no separate note recorded. No fix was made between the two attempts — see below. | PASS |
| AA-24 | Watch a warrior at the edge of the arena panel strike outward | The weapon does not pop in or out at the panel edge as the blow extends | 2026-08-13, tester at the desktop; passed, with no separate note recorded. | PASS |

## What closed without a fix

**Three rows in this family closed against a build nobody changed for them.**
That is a legitimate outcome — a row is a question about what a person can see,
and the answer can be yes on a second look. It is recorded here because the
alternative reading, that each of these was repaired, is false and would send a
later reader looking for a commit that does not exist.

**`AA-22` passed after failing.** Between the 2026-08-09 `FAIL` and the
2026-08-13 `PASS`, no change was made to the 500-agent density, the trail count,
or the arm gating. What did change is the rest of the family: `AA-20` and
`AA-21` were run at 200 agents for the first time on 2026-08-13 and both passed.
The 2026-08-09 backlog's own hypothesis — that the chaos was the density rather
than the choreography — is therefore the reading the evidence supports, and the
backlog's section 1 still describes the two contributors it identified, arms
close to sub-pixel at fit zoom and trails multiplying at density.

**`AA-23` passed after failing, and its two measured causes are unchanged.**
When it failed earlier the same day, the cause was measured rather than guessed,
and both halves are still true of the build:

- **At the default camera fit a pawn has no legs.** The default 1280 x 720
  window gives an arena panel of 826 x 640, so
  `horizontalZoom = 826 * 0.88 / 1280 = 0.5682` wins the axis fit and
  `apparentScale = 0.5682 * 1.35 = 0.767`, below `MediumDetailScale = 0.95`.
  That resolves `PawnDetailTier.Low`, and `PawnGeometry.CreateLegsAndFeet`
  returns four empty rectangles at `Low`. Legs first exist two mouse-wheel
  notches in, at `cameraZoom >= 0.7037`. The passing run was therefore made
  somewhere above the default fit; the row never named a zoom station, so that
  is within what it asks.
- **A closing attacker's stride phase is effectively frozen.** Stride phase
  advances by distance travelled, one cycle per 6 000 raw units, while the
  arrival taper drives a closing attacker's step down to 1 raw unit per tick.
  That is one stride cycle every 300 seconds at 20 Hz, and the mode still reads
  `Walk` because only exactly zero resolves `Stance`.

Neither was repaired. Both are written up, with the full arithmetic and the
options for addressing them, in
`docs/plans/2026-08-13-strike-while-moving-legibility-design.md`, which is a
live document in `docs/plans/` and is the thing to read rather than this
paragraph. **Do not treat this row's `PASS` as evidence that the default camera
fit draws a leg. It does not.**

**`AA-24` passed against a feature that was never built.**
`ConservativePawnCull` has no production caller — the only two references to it
under `src/` are doc comments in `src/Hukbo.Client/Rendering/PawnGeometry.cs`,
at lines 2136 and 2241. The 2026-08-09 backlog's section 2 concluded from that
that `AA-24` had no implementation and that closing it meant widening the live
pose-blind path. A person nevertheless passed the row at the desktop. Both
records are true as written: the live cull may simply be wide enough in practice
for what the tester watched. The `PASS` is not evidence that the cull was wired.

## What a later reader should be careful of

- **The eighteen rows above passed against the 2026-08-13 working tree**, which
  carried the uncommitted lethal-blow legibility change. That change raises the
  number of primitives a kill draws. `AA-12`, `AA-20`, `AA-21`, and `AA-22` are
  the rows most sensitive to it; if the attack or blood presentation is retuned
  again, they say nothing about the new values.
- **Three of the rows closed without a repair**, and the section above says
  which and why. `AA-23` in particular passed while the two causes measured
  against it stood unchanged.
- **The 2026-08-09 observations preserved above are not passes.** Where a row
  carries both, the 2026-08-09 text records why the row could not be certified
  at 500 agents and the 2026-08-13 line is the one that closed it. Do not quote
  the earlier text as the row's result.
- **The `Actual` column here is deliberately thin.** The tester gave one verdict
  for a block of rows. No agent may enrich these cells later; an invented
  observation is worse than a thin one.
- **The six rows lifted on 2026-08-11 were observed at 2048x1152**, the
  virtualised viewport a DPI-unaware process was handed on that display. The DPI
  awareness fix of 2026-08-11 means the same run today would be at 2560x1440.
  That caveat applies to those six, not to the sixteen above, which were run
  after the fix.
