# Native Dependencies

| Item | Compile | Run client | Package | CI | Notes |
| --- | --- | --- | --- | --- | --- |
| Windows x64 | Yes for supported lane | Yes | Yes | Yes | Only v0.1 target |
| Graphics driver with OpenGL support | No | Yes | No | No for non-graphical CI | Vendor/Windows supplied |
| MonoGame DesktopGL native runtime assets | Restored by NuGet | Yes | Included by publish resolution | Resolved during build | Do not install separately |
| .NET 10 runtime | SDK supplies locally | Bundled in the self-contained package | Included in package | SDK supplies | Player machine needs no separate runtime |
| Windows SDK/C++ build tools | No | No | No for current publish | No | No native source compilation |
| SDL/OpenAL system install | No | No separate install expected | No | No | MonoGame package resolves supported runtime assets |
| Steamworks/console/mobile SDKs | No | No | No | No | Deferred platforms |

`scripts/doctor.ps1` validates the supported operating system, architecture,
shell, Git, SDK, lock files, and centrally pinned MonoGame packages. It cannot
prove that a graphics driver will successfully create a window; use the
interactive smoke checklist after non-graphical verification passes.
