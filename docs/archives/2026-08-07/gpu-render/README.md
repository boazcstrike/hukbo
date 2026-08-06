# GPU-Instanced Arena Rendering

> **Archived: reference only.** This workstream finished on 2026-07-29 and is
> closed. Do not execute either document in this folder.

Work on making a 1,000-unit battle (500 per team) render inside a 60 Hz frame,
and on deciding — from measurement rather than intuition — whether a
GPU-instanced rendering backend is the thing that gets it there.

## Files in this folder

| File | What it is |
| --- | --- |
| [`2026-07-28-gpu-render-design.md`](2026-07-28-gpu-render-design.md) | The design of record. Explains the measurement, the three phases, the full instanced-backend design, and the decision record. Authorizes nothing. |
| [`2026-07-28-gpu-render.md`](2026-07-28-gpu-render.md) | The plan. Ordered task table GPU-001 through GPU-038, verification boundaries, rollback, and the determinism statement. |

## Status — closed 2026-07-29

**Phases 1 and 2 shipped. Phase 3 was never built, and never will be under this
plan.**

| Phase | Tasks | Outcome |
| --- | --- | --- |
| 1 — establish measurement truth | GPU-001 to GPU-012 | Complete. Merged to main. |
| 2 — remove per-agent CPU cost | GPU-013 to GPU-023 | Complete. Merged to main. GPU-016 dropped, GPU-021 measured and deliberately no code change. |
| 3 — the instanced backend | GPU-024 to GPU-038 | **Never started.** The go/no-go trigger returned NO-GO. |

The result Phase 2 reached is the good outcome rather than a disappointing one:
at 1,000 units, default fit, seed 1, Release, retrace disabled, the `Draw` p95
is **3 276.6 us (3.28 ms)** against an 8.0 ms budget. The 1,000-unit target is
met by the existing `SpriteBatch` backend, so instancing had nothing left to buy.

What is still outstanding is interactive confirmation, not code. The five smoke
rows GR-1 through GR-5 live in
[`docs/development/testing.md`](../../development/testing.md) and are all
`PENDING` — nobody has yet watched a 1,000-unit battle in a real window. Only a
human at an interactive desktop may flip one.

Two implementation notes worth carrying forward:

- `ConservativePawnCull` and its containment proof are live, tested, and
  referenced by nothing. GPU-016 never adopted them and the plan records the
  decision to drop it. Deleting them is a separate reviewable change.
- Phase 2 changed `Hukbo.Client` only. The seed-1 headless figures the plan
  pins in section 9 are unmoved.

## The three decisions

- **D1 — Phased, with instancing gated.** The recorded data shows render cost
  scaling with total agent count rather than with the number of quads drawn, so
  building the thing that changes quad submission first would be optimising
  against a guess.
- **D2 — Target 1,000 units total, 500 per team.** Past 1,000 agents the binding
  constraint leaves the renderer entirely and becomes collision resolution,
  which is a different design's problem; a renderer budget beyond that point is
  a budget for frames the simulation cannot feed.
- **D3 — Keep the `SpriteBatch` fallback, with a latched runtime capability
  probe.** MonoGame exposes no public API for asking whether instancing is
  supported, so detection must be empirical, and empirical detection needs
  somewhere to fall back to.

## The go/no-go trigger for Phase 3 — evaluated, NO-GO

The trigger fired on 2026-07-29 against
`docs/development/render-baselines/render-matrix-phase2-2026-07-29.json`. Clause
1 failed: `Draw` p95 was 3.28 ms against the 8.0 ms threshold, so the budget was
met. Clause 2 failed as reported: `submitMicroseconds` p95 was 766.7 us, 23.4
percent of the frame, against a 50 percent threshold. Both clauses are required,
so the verdict is NO-GO. Full reasoning in the plan's section 4.1a. The
statement of the trigger is preserved below as it was written.

Phase 3 would have been authorized if and only if **both** held on the Phase 2
re-measurement, at 1,000 units, seed 1, Release configuration, 120 frames per
station, vertical retrace disabled, at the default-fit camera station:

1. The `Draw` p95 exceeds **8.0 ms**; **and**
2. The Tier 1 `submitMicroseconds` p95 at that same station is at least
   **50 percent** of the total `Draw` p95 there.

Clause 2 is the important one: a budget miss alone is not evidence that
instancing helps. Clause 2 cannot be evaluated at all until task GPU-004
disaggregates the Submit span, for the reason given below.

## The central measurement, and two things it does not say

At 500 units all three camera stations land within six hundredths of a
millisecond of one another — 5.27, 5.30, and 5.33 ms — while drawing 9,326,
9,326, and 1,028 quads. That is a floor rather than a curve, and it is the
finding the whole phasing is built around. Two caveats travel with it and must
not be dropped. First, the widely quoted "96 percent of the frame is
unattributed" figure is **station-dependent and true only at maximum zoom**: at
the 500-unit default-fit station the instrumented spans account for roughly 96
percent of the frame, not 4 percent, and the unattributed share across the six
recorded cells ranges from about 4 percent to about 96 percent. Second, and more
importantly, the two instrumented spans are **conflated**: the span reported as
`submitMicroseconds` wraps `DrawArenaLayer`, which both builds the real per-pawn
geometry and issues the `SpriteBatch.Draw` calls, while the span reported as
`geometryBuildMicroseconds` times only the probe's own duplicate counting pass.
So the 4.85 ms sitting inside `submitMicroseconds` at the 500-unit default-fit
station might be CPU geometry construction (Phase 2 work) or submission (Phase 3
work), and **nothing recorded in this repository today can tell the two apart.**
That, more than anything else, is why Phase 3 is gated and why disaggregating
the span is scheduled early.
