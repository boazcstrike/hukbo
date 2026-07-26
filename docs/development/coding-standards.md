# Coding and Quality Standards

- Follow `.editorconfig`; verify with `./scripts/format.ps1 -Verify`.
- Nullable references and warnings-as-errors are repository-wide.
- Keep Core deterministic and independent from MonoGame, filesystem, network,
  windowing, audio, and wall clock.
- Iterate authoritative entities and events in explicit stable order.
- Validate external inputs before allocation or simulation.
- Add a focused regression test for behavioral fixes.
- Do not weaken tests or warnings to obtain a green build.
- Keep package versions in `Directory.Packages.props` and regenerate lock files
  only for reviewed dependency changes.
- Do not commit credentials, local paths, generated `bin/obj`, package output,
  or debugging artifacts.
- Use Conventional Commits and keep diffs attributable to the requested change.

Dependency advisories are temporally unstable. Run a current post-restore
audit before releases:

```powershell
dotnet list AutonomousArena.slnx package --vulnerable --include-transitive
```

Treat audit network failures separately from a clean advisory result.
