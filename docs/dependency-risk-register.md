# Dependency Risk Register

| Risk | Impact | Current control | Remaining action |
| --- | --- | --- | --- |
| MonoGame native runtime fails on a workstation graphics stack | Client cannot open | Windows x64 target and separate runtime smoke | Test on intended hardware |
| NuGet feed unavailable | Clean restore fails | Package locks and local restore evidence | Do not vendor packages without a decision |
| Package vulnerability appears after pinning | Security/maintenance issue | Locked explicit versions | Run current vulnerability audit during final integration and periodically |
| .NET 10.0.302 removed from a developer machine | Build cannot resolve SDK | `global.json` and doctor diagnostics | Install exact SDK |
| Self-contained runtime increases artifact size and patch surface | Larger download and bundled-runtime maintenance | Pin SDK/runtime and package only `win-x64` | Rebuild for patched .NET servicing releases |
| Font/content source license is unclear | Distribution risk | Use only redistributable content with provenance | Record font license before public distribution |

No critical dependency conflict is known from the foundation snapshot. This
register is not a substitute for a current advisory scan after final restore.
