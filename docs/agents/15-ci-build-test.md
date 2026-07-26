# 15 — CI Build and Test

## Scope

Reproduce the canonical non-graphical gate on Windows and publish a reviewable
Windows package for manual/tagged runs.

## Inputs inspected

- Pinned SDK and package lock files.
- Canonical PowerShell workflows.
- Supported Windows x64 platform.

## Decisions and work

Created a `windows-2025` workflow with `contents: read`, concurrency
cancellation, timeouts, NuGet caching, and full commit-SHA pins for checkout,
setup-dotnet, and artifact upload. Pull requests plus `master` and `main` pushes run verification;
manual/tagged runs additionally package and upload the client.

## Files

- `.github/workflows/ci.yml`

## Verification

The YAML and pinned action references were inspected locally. The workflow has
not run on GitHub in this snapshot, so hosted CI and artifact upload are not
claimed as passed.

## Status

**CONDITIONALLY COMPLETE**

## Limitations

The primary CI lane is intentionally non-graphical and cannot replace the
interactive menu smoke.

## Next action

Open or push the integrated branch, confirm the hosted `verify` job, then run a
manual package job and inspect its artifact.
