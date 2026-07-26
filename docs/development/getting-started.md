# Getting Started

## 1. Verify the workstation

Open PowerShell 7 in the repository root:

```powershell
./scripts/doctor.ps1
```

The command reports required failures separately from optional Git LFS.

## 2. Restore pinned tools and packages

```powershell
./scripts/bootstrap.ps1
```

Bootstrap is non-destructive. It restores a repository-local tool manifest when
one exists and runs NuGet in locked mode. It does not reset, clean, stash, or
delete working-tree files.

## 3. Build and test

```powershell
./scripts/build.ps1
./scripts/test.ps1
```

Run every non-graphical gate with:

```powershell
./scripts/verify.ps1
```

## 4. Run the game

```powershell
./scripts/run.ps1
```

Fallback:

```powershell
dotnet run --project src/AutonomousArena.Client -c Release
```

The client opens a 1280×720 resizable spectator window. Press Escape to open
the menu:

- **Play** resumes logical simulation and closes the menu.
- **Pause** keeps logical simulation stopped and leaves the menu visible.
- **Exit Game** closes the client cleanly.

Space toggles play/pause when the menu is closed. Use `1`, `2`, or `4` for
speed, `R` to replay the same seed, WASD/arrows to pan, and the mouse wheel to
zoom. Opening the Escape menu always pauses simulation scheduling.

## Other workflows

```powershell
./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1
./scripts/format.ps1 -Verify
./scripts/package.ps1 -Runtime win-x64
```

The self-contained package is written to
`artifacts/packages/client-win-x64/`; the target machine does not need a
separate .NET runtime.

## Troubleshooting

- If the SDK is missing, install .NET 10 and ensure `dotnet --list-sdks`
  includes 10.0.302.
- If locked restore fails, do not delete lock files. Confirm nuget.org is
  reachable and update dependencies through an explicit review.
- If the client builds but no window opens, record the standard-error message
  and verify the interactive graphics driver/session separately.
- If content compilation cannot find `dotnet-mgcb`, run
  `dotnet tool restore` from the repository root and confirm the committed tool
  manifest is present.
