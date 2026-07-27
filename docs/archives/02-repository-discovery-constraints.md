# 02 — Repository Discovery and Constraints

## Scope

Inventory the existing project layout, configuration, assets, automation,
dependencies, tests, and repository constraints without modifying source.

## Inputs inspected

- Solution and all project files.
- Root SDK, build, package, NuGet, editor, Git, and lock configuration.
- Existing plans, Core primitive source/tests, and content definition.
- Repository file listing and Git status.

## Decisions and work

Recorded the four-project inward dependency structure, central package policy,
locked restore, lack of native source/binary assets/submodules, current test
scope, Windows-only support, and the requirement to preserve owner research
documents.

## Files

- `docs/repository-audit.md`
- `docs/dependency-inventory.md`
- `docs/platform-support-matrix.md`

## Verification

Targeted file inspection confirmed four committed package lock files and no
Core package/project references. Discovery was performed against current
source rather than inferred from plans alone.

## Status

**COMPLETE**

## Limitations

The audit predates integration of the simulation and client workstreams.

## Next action

Have the orchestrator check the final diff for new dependencies, native assets,
or platform assumptions.
