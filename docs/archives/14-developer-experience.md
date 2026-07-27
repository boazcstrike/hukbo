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

All PowerShell files parsed successfully. After correcting named-parameter
forwarding in the shared helper, the canonical verify workflow passed doctor,
tool/locked restore, formatting, zero-warning Release build, 42 tests, and the
200-agent workload. Self-contained packaging also passed.

## Status

**COMPLETE**

## Limitations

Direct menu interaction still requires a real desktop because synthetic input
did not reach SDL in this environment.

## Next action

Run `./scripts/run.ps1`, complete the short manual menu checklist, and record
the result.
