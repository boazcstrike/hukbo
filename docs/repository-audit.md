# Repository Audit

**Snapshot:** 2026-07-26

**Scope:** foundation branch before simulation/client integration

## Repository shape

The repository began as documentation and now contains a four-project
`.slnx` solution:

```text
src/AutonomousArena.Core
src/AutonomousArena.Headless
src/AutonomousArena.Client
tests/AutonomousArena.Core.Tests
```

Core has no project or package dependency. Headless, Client, and Core.Tests
reference Core. Only Client references MonoGame. This is the intended inward
dependency direction.

## Configuration discovered

- `global.json` pins .NET SDK 10.0.302.
- `Directory.Build.props` sets `net10.0`, C# 14, nullable, warnings as errors,
  deterministic builds, and package lock generation.
- `Directory.Packages.props` centrally pins all NuGet versions.
- `NuGet.config` clears inherited feeds and uses nuget.org only.
- Every project has a committed `packages.lock.json`.
- `.editorconfig`, `.gitattributes`, and `.gitignore` establish repository
  conventions.

## Engine, assets, and native code

The Client uses MonoGame DesktopGL and the MonoGame content builder. The first
scene creates its dot texture at runtime. A SpriteFont is the only planned
compiled content for menu and diagnostics. No source binary assets, Git
submodules, native source projects, platform-specific source files, or
third-party binary drops were observed.

Git LFS is installed on the inspected workstation but is optional because the
current repository contains no required LFS-managed asset.

## Automation and tests

PowerShell workflows live under `scripts/`; Windows CI lives in
`.github/workflows/ci.yml`. Core tests use VSTest, xUnit, and the Visual Studio
runner. Interactive client behavior remains a separate smoke test.

## Constraints

- Windows x64 is the only supported v0.1 developer/runtime platform.
- Restore requires access to nuget.org unless the exact packages are already
  cached.
- The game is offline at runtime and has no external service.
- Existing repository-owner research documents are inputs, not generated
  outputs, and must be preserved.
- No license file was present in the inspected foundation snapshot; public
  distribution requires the owner to choose and add a project license.
