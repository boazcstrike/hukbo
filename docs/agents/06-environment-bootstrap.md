# 06 — Environment Bootstrap

## Scope

Turn pinned prerequisites and package configuration into an idempotent,
repository-relative bootstrap without secrets or hidden machine mutation.

## Inputs inspected

- `global.json`, `NuGet.config`, project lock files.
- MonoGame content tool requirement from the Client workstream.
- Existing workstation SDK and package cache.

## Decisions and work

Bootstrap runs the doctor, restores a repository-local .NET tool manifest when
present, and restores the solution in locked mode. It installs nothing by
default. `-InstallSdk` is the only explicit machine-install path.

## Files

- `scripts/bootstrap.ps1`
- `scripts/_common.ps1`
- `docs/development/getting-started.md`

## Verification

`./scripts/bootstrap.ps1` passed in the delivery worktree and restored all four
locked project graphs. The integration branch's tool-manifest restore is owned
and verified by the orchestrator; it is not falsely claimed as this worktree's
result.

## Status

**COMPLETE**

## Limitations

First restore needs nuget.org access unless the exact packages are cached.

## Next action

Re-run bootstrap after the orchestrator integrates the pinned `dotnet-mgcb`
manifest.
