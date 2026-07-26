# 01 — Game Platform and Engine Decision

## Scope

Select the initial game shape, engine, target framework, runtime platform,
rendering surface, and distribution boundary before scaffolding.

## Inputs inspected

- Repository-owner role prompt.
- Approved foundation and orchestration/menu designs.
- Initial dot-based deterministic arena requirements.

## Decisions and work

Selected offline 2D MonoGame DesktopGL on Windows x64 with a package-free
deterministic Core. Pinned .NET 10.0.302 and MonoGame 3.8.5. Chose a
self-contained `win-x64` standalone publish for the first package. Rejected
Godot, Unity/Stride, and custom graphics stacks for the first proof.

## Files

- `docs/architecture/platform-decision.md`
- `docs/platform-support-matrix.md`
- `docs/plans/2026-07-26-autonomous-arena-foundation-design.md` (input)

## Verification

The approved design and root configuration agree on MonoGame DesktopGL,
`net10.0`, SDK 10.0.302, and Windows x64. No interactive runtime claim is made
here.

## Status

**COMPLETE**

## Limitations

Non-Windows platforms and store distribution were evaluated only as deferred
options.

## Next action

Revisit platform scope only after the Windows acceptance workload and runtime
smoke pass.
