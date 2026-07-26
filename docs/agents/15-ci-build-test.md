# 15 — Build and Test Automation

## Scope

Reproduce the canonical non-graphical gate locally on Windows and publish a
reviewable local Windows package.

## Inputs inspected

- Pinned SDK and package lock files.
- Canonical PowerShell workflows.
- Supported Windows x64 platform.

## Decisions and work

The original foundation snapshot included a GitHub Actions workflow. The
repository owner later chose not to consume hosted runner time, so that workflow
was removed. `scripts/verify.ps1` is the authoritative automated gate and
`scripts/package.ps1 -Runtime win-x64` creates the reviewable package locally.

## Files

- `scripts/verify.ps1`
- `scripts/package.ps1`

## Verification

The canonical local gate and self-contained Windows package passed on the
reference workstation. Hosted execution is intentionally not configured and is
not claimed or required.

## Status

**COMPLETE**

## Limitations

The automated lane is intentionally non-graphical and cannot replace the
interactive menu smoke. Verification depends on a developer or release operator
running the local commands.

## Next action

Run the canonical local gate before integration and package locally when a
reviewable Windows build is required.
