# Projectile props and embedded projectiles smoke — PARTIAL 2026-08-13

**Archived: reference only.** This is a finished record of manual testing that
already happened. Never execute it, never treat it as a live task list, and
never cite it as the reason to make a change. The live contract for this project
remains `CLAUDE.md` and `docs/development/smoke-checklist.md`; nothing in this
file is outstanding and nothing in it is an instruction.

Seven of this family's eight rows are lifted into this record as `PASS`. One
row — `PP-3` — did not pass and stays live in the checklist. Read the "What did
not close" section below before assuming this family is done with.

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-13 |
| Machine/platform | Not recorded |
| Source commit | Not recorded |
| Launch path (`source` or package path) | Not recorded |
| Optional screenshot paths | None recorded |

## What these rows were for

The system under test is the projectile prop and embedded-projectile rendering
added in `Hukbo.Client`. The automated tests already proved that the three
weapon silhouettes are mutually distinct, that the prop is centred on the shot
rather than anchored at the launcher, that it rotates to the direction of
travel, that every one of the thirteen body parts resolves to an anchor inside
the host's own visual bounds, that a shield block attaches to the shield rather
than to the body part it also carries, that the pool never exceeds 256 slots
and evicts oldest-first, and that the quad budget still fits.

None of that proves that a spectator can tell a spear from an arrow from a lead
ball while a battle is running, or that a stuck arrow reads as stuck rather than
as a smear, or that the in-flight prop and the embedded prop behave correctly
across the zoom range. That is what a person at an interactive Windows desktop
was needed to close.

## The rows that closed

A human tester ran the family interactively on 2026-08-13. `PP-1` and `PP-2`
each carry a distinct pass note. `PP-4` through `PP-8` were reported together
as "good" and passing, with no separate observation recorded for any individual
row among them. The `Actual` column below says exactly that and no more.
Nothing here should be read as a detailed finding that was never made.

| # | Step | Expected | Actual | Status |
| --- | --- | --- | --- | --- |
| PP-1 | Watch a single Bangkaw or Busog shot from release to impact at default zoom, following the projectile with your eye | The drawn object is a small prop of fixed length that travels from launcher to target, and its length does not change during the flight. Failure is a line that grows out of the thrower and is longest at the moment of impact, which is the defect this change exists to fix | 2026-08-13, tester at the desktop. Passed. | PASS |
| PP-2 | Compare a Bangkaw shot, a Busog shot, and an Arquebus shot in flight, at default zoom | The three are distinguishable in the air without seeing who fired them: the spear is the longest and carries a visible head at its leading end, the arrow is markedly shorter with a pale fletched tail, and the arquebus shot is a small ball with no shaft at all. Failure is any two of the three reading as the same object in flight | 2026-08-13, tester at the desktop. Passed. | PASS |
| PP-4 | Watch a Busog shot land on an unshielded warrior, at a zoom close enough to see the pawn's body clearly | An arrow is left standing in the warrior, at the part of the body the shot struck, with its fletched tail outward. Failure is no arrow appearing, or one appearing somewhere unrelated to where the blow landed | 2026-08-13, tester at the desktop. Run as part of the whole family; reported good, passed, no separate note recorded. | PASS |
| PP-5 | Watch a Bangkaw or Busog shot that a tall hardwood shield blocks | The projectile is left standing in the shield face rather than in the warrior behind it. Failure is an arrow appearing in the body of a warrior whose shield stopped the shot | 2026-08-13, tester at the desktop. Run as part of the whole family; reported good, passed, no separate note recorded. | PASS |
| PP-6 | Find a warrior carrying at least one embedded projectile and follow it while it walks across the field | The projectile rides with the warrior, holding its position on the body and its angle, rather than staying behind at the spot where the hit occurred or sliding around the pawn. Failure is a projectile that detaches, drifts, or re-rolls its angle from frame to frame | 2026-08-13, tester at the desktop. Run as part of the whole family; reported good, passed, no separate note recorded. | PASS |
| PP-7 | Watch a warrior carrying embedded projectiles while zooming out to a wide view of the whole battle, then zoom back in | The embedded projectiles stop being drawn as the camera pulls out and reappear on zooming back in, and the warriors themselves are unaffected. Failure is embedded projectiles still drawing at the widest zoom — they are deliberately detail-gated, unlike the in-flight prop — or the pawns changing in any other way as the gate crosses | 2026-08-13, tester at the desktop. Run as part of the whole family; reported good, passed, no separate note recorded. | PASS |
| PP-8 | Watch an Arquebus shot land on a warrior | Nothing is left standing in the wound. Failure is a shaft appearing for a weapon that fires a lead ball | 2026-08-13, tester at the desktop. Run as part of the whole family; reported good, passed, no separate note recorded. | PASS |

## What did not close

**`PP-3`, "Watch a shot in flight while zooming from close in to fully zoomed
out", did not pass.** The tester's finding was that the in-flight projectile
"looks too big even when zoomed in." That row stayed in the live checklist and
is not lifted into this record. If a fix is made in response, the row must be
re-run by a person afterward; nothing in this file may be read as evidence that
row is closed.

## What a later reader should be careful of

- **No agent may later enrich the `Actual` cells above.** The tester gave one
  verdict for `PP-1` and `PP-2` individually and one combined verdict for
  `PP-4` through `PP-8`. An invented per-row observation is worse than a thin
  one.
- **These seven passes record the build as it stood on 2026-08-13.** They are
  not evidence about any later build, including whatever build fixes `PP-3`.
  If the in-flight or embedded projectile rendering is retuned again, these
  rows say nothing about the new values.
