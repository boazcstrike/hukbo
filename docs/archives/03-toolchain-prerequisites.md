# 03 — Toolchain Prerequisites

## Scope

Define and validate the required SDK, shell, source-control tools, and optional
developer tools with actionable diagnostics.

## Inputs inspected

- `global.json`
- Repository workflow requirements.
- Installed workstation commands and versions.

## Decisions and work

Required Windows x64, PowerShell 7+, Git, and the exact .NET SDK feature band.
Kept Git LFS and IDEs optional. Implemented a non-destructive prerequisite
doctor and documented official package-manager commands.

## Files

- `scripts/doctor.ps1`
- `docs/development/prerequisites.md`

## Verification

`./scripts/doctor.ps1` passed on Windows x64 with PowerShell 7.6.4, Git
2.55.0.windows.3, Git LFS, and .NET SDK 10.0.302. It also confirmed lock files
and centrally pinned MonoGame packages.

## Status

**COMPLETE**

## Limitations

The doctor cannot validate IDE integrations or graphics window creation.

## Next action

Run the doctor on each supported developer workstation and CI image.
