# 07 — Solution Architecture

## Scope

Establish the smallest dependency boundaries that keep gameplay deterministic
and independently testable.

## Inputs inspected

- Approved foundation design.
- `Hukbo.slnx` and all project references.
- Planned Core contract consumed by Client and Headless.

## Decisions and work

Retained four projects: Core, Headless, Client, and Core.Tests. Rejected
Application/Infrastructure/Platform layers, generic ECS, DI, telemetry,
asset-tool, benchmark, and end-to-end projects until a measured requirement
exists.

## Files

- `docs/archives/2026-07-26-hukbo-foundation-design.md` (input)
- `Hukbo.slnx` and project files (inspected, not modified here)
- `docs/repository-audit.md`

## Verification

Current project references point only inward to Core; Core has no package or
project dependency. Final integration must preserve this graph.

## Status

**COMPLETE**

## Limitations

The final public Core contract is reconciled only after source branches merge.

## Next action

Reject any Client-to-Headless or Core-to-MonoGame dependency during final diff
review.
