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

Package/config inspection is complete. The self-contained client opened a
1280x720 DesktopGL window on the reference Windows/NVIDIA machine, advanced the
simulation, and closed normally with exit code 0.

## Status

**COMPLETE**

## Limitations

Graphics initialization remains machine-specific; the recorded result applies
to the named reference machine.

## Next action

Repeat the window smoke on each newly supported hardware baseline.
