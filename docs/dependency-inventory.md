# Dependency Inventory

| Dependency | Version | Category | Used by | Notes |
| --- | --- | --- | --- | --- |
| .NET SDK | 10.0.302 | System prerequisite/build tool | All projects | Pinned in `global.json`; SDK, not runtime-only, is required to build |
| PowerShell | 7+ | Build-time tool | `scripts/` | Required for canonical workflows |
| Git | Current supported | System prerequisite | Developer/CI | No submodules discovered |
| Git LFS | Optional | Optional developer tool | Future large assets | No current LFS asset requirement |
| MonoGame.Framework.DesktopGL | 3.8.5 | Managed engine package with native runtime assets | Client | Windowing, input, drawing; no Core reference |
| MonoGame.Content.Builder.Task | 3.8.5 | Content pipeline tool | Client build | Compiles `.mgcb` content |
| Microsoft.NET.Test.Sdk | 18.8.1 | Test platform | Core.Tests | VSTest integration |
| xunit | 2.9.3 | Managed NuGet test package | Core.Tests | Unit/regression tests |
| xunit.runner.visualstudio | 3.1.5 | Managed NuGet test adapter | Core.Tests | Private test tooling asset |
| nuget.org | v3 feed | Build-time external service | Restore | Only configured package source |
| Windows graphics driver/OpenGL | Vendor supplied | Runtime system prerequisite | Client | Required only for interactive client execution |

The packaged `win-x64` client includes its .NET runtime and does not require a
separate runtime installation on the player machine.

There are no selected physics, audio-domain, networking, serialization,
dependency-injection, logging, telemetry, mocking, assertion-extension, or
benchmark-framework packages. The headless runner uses platform APIs and JSON
support from the .NET base class library.

NuGet versions are centralized in `Directory.Packages.props`; resolved
transitive graphs are locked per project.
