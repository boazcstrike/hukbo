# 04 — Native Dependencies and Platform SDK

## Scope

Separate native requirements needed to compile, run, package, or execute CI.

## Inputs inspected

- MonoGame DesktopGL and Content Builder package references.
- Windows x64 platform decision.
- Project files and absence of native source projects.

## Decisions and work

Documented that no C++ workload, Windows SDK customization, Vulkan SDK,
Steamworks SDK, SDL/OpenAL manual installation, mobile SDK, or console SDK is
required. The interactive client requires a Windows graphics driver capable of
the DesktopGL runtime.

## Files

- `docs/development/native-dependencies.md`
- `scripts/doctor.ps1`

## Verification

Package/config inspection is complete. An interactive window and graphics
driver smoke was not run in this delivery worktree.

## Status

**CONDITIONALLY COMPLETE**

## Limitations

Graphics initialization is machine-specific and cannot be proven by package
restore or compilation.

## Next action

Perform the documented interactive smoke on the target Windows hardware after
Client integration.
