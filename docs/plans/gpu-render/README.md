# GPU-Instanced Arena Rendering

Work on making a 1,000-unit battle (500 per team) render inside a 60 Hz frame,
and on deciding — from measurement rather than intuition — whether a
GPU-instanced rendering backend is the thing that gets it there.

## Files in this folder

| File | What it is |
| --- | --- |
| [`2026-07-28-gpu-render-design.md`](2026-07-28-gpu-render-design.md) | The design of record. Explains the measurement, the three phases, the full instanced-backend design, and the decision record. Authorizes nothing. |
| [`2026-07-28-gpu-render.md`](2026-07-28-gpu-render.md) | The plan. Ordered task table GPU-001 through GPU-038, verification boundaries, rollback, and the determinism statement. |

## Status

**Phases 1 and 2 are authorized. Phase 3 is not.** The Phase 3 tasks
(GPU-024 through GPU-038) are written out in the plan so the work is legible and
estimable, but none of them may be started until the go/no-go trigger below
fires on a recorded, committed Phase 2 re-measurement. No task in any phase has
been started yet.

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

## The go/no-go trigger for Phase 3

Phase 3 is authorized if and only if **both** hold on the Phase 2
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
