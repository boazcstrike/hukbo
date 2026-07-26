# 13 — Static Analysis and Quality

## Scope

Enforce formatting, nullable analysis, compiler warnings, deterministic builds,
dependency review, and non-destructive quality gates.

## Inputs inspected

- `.editorconfig`, `Directory.Build.props`, package configuration.
- Existing source/tests.
- Canonical workflow requirements.

## Decisions and work

Composed locked restore, format verification, Release build, tests, and
headless workload through `verify.ps1`. Kept analyzer behavior in the SDK and
did not add speculative quality packages.

## Files

- `scripts/format.ps1`
- `scripts/verify.ps1`
- `docs/development/coding-standards.md`

## Verification

Formatting verification passed and changed 0 of 17 files in this snapshot.
Warnings-as-errors applied to the passing Core test build. A live nuget.org
transitive audit reported no vulnerable packages. A final full build remains
pending.

## Status

**CONDITIONALLY COMPLETE**

## Limitations

No secret scanner, license scanner, duplication tool, or complexity budget was
added because repository/CI support for those gates was not established.

## Next action

Run the full integrated build and a current transitive vulnerability audit.
