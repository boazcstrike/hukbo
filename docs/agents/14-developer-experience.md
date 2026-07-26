# 14 — Developer Experience

## Scope

Make prerequisite checks, restore, build, test, run, benchmark, formatting,
packaging, and verification one-command operations with exact onboarding.

## Inputs inspected

- Pinned SDK/solution/projects.
- Headless CLI and Client control contracts.
- Windows-only platform boundary.

## Decisions and work

Added strict PowerShell workflows that resolve paths from `$PSScriptRoot`,
inspect native exit codes, use locked restore, and never clean/reset/stash or
delete user files. Documented primary and fallback launch commands and all
controls.

## Files

- `scripts/_common.ps1`
- `scripts/doctor.ps1`, `bootstrap.ps1`, `build.ps1`, `test.ps1`
- `scripts/run.ps1`, `benchmark.ps1`, `format.ps1`, `package.ps1`, `verify.ps1`
- `README.md`
- `docs/development/**`

## Verification

All PowerShell files parsed successfully. Doctor, bootstrap, test, and format
verification ran; doctor/restore passed, 7/7 available Core tests passed, and
formatting changed 0 files. Source-dependent build/run/benchmark/package
results remain integration gates.

## Status

**COMPLETE**

## Limitations

The workflows can only validate behavior implemented in the integrated source.

## Next action

Run every workflow after integration and update readiness evidence with actual
results.
