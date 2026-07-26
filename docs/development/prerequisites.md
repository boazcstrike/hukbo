# Development Prerequisites

## Required

- Windows x64.
- PowerShell 7 or newer.
- Git.
- .NET SDK 10.0.302. The runtime alone is insufficient because restore,
  compilation, tests, publishing, and local tools require the SDK.
- Internet access to nuget.org for the first restore, unless the exact locked
  packages already exist in the local NuGet cache.
- A working OpenGL-capable graphics driver and interactive desktop to run the
  MonoGame client.

Install the command-line prerequisites from an elevated terminal if needed:

```powershell
winget install --id Microsoft.PowerShell --exact
winget install --id Git.Git --exact
winget install --id Microsoft.DotNet.SDK.10 --exact
```

The repository requires the exact SDK feature band pinned in `global.json`.
After installation, verify the workstation:

```powershell
./scripts/doctor.ps1
```

## Optional

- Git LFS, reserved for future large binary assets.
- Visual Studio, Rider, or VS Code with C# support.
- A debugger or GPU diagnostic tool for interactive client investigation.

No IDE is required by the canonical workflows.

## Local tools and packages

`./scripts/bootstrap.ps1` restores a pinned repository-local .NET tool manifest
when present, then performs a locked NuGet restore. It does not install machine
software unless `-InstallSdk` is explicitly supplied.

The current repository does not use credentials, private feeds, environment
variables, or developer secrets. Do not add secrets to source-controlled
configuration.
