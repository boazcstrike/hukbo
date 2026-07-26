# Platform and Engine Decision

**Decision:** MonoGame DesktopGL 3.8.5 on .NET 10.0.302

**Initial platform:** Windows x64

**Game shape:** offline, single-player, 2D spectator arena

**Status:** approved

## Selected approach

The game uses a plain `net10.0` simulation library and a MonoGame DesktopGL
presentation client. The Core library owns deterministic state; the client owns
windowing, input, drawing, menus, and process exit. This keeps authoritative
combat testable without a window, GPU, audio device, or engine object.

DesktopGL supplies the cross-platform SDL/OpenGL runtime surface, but v0.1 only
commits to Windows x64. The initial package is a self-contained `win-x64`
standalone publish, not a Steam or Microsoft Store submission.

## Required tooling

- .NET SDK 10.0.302, pinned by `global.json`.
- PowerShell 7 for repository workflows.
- Git.
- A Windows x64 interactive desktop and working OpenGL-capable graphics driver
  to run the client.
- MonoGame Framework DesktopGL and Content Builder Task 3.8.5, restored from
  NuGet.

No Visual Studio workload, C++ compiler, Android SDK, Xcode, console SDK,
Steamworks SDK, Vulkan SDK, or separate SDL/OpenAL installation is required for
the current source build.

## Rejected alternatives

- **Godot C#:** stronger editor/UI workflow, but an unnecessary editor boundary
  before the simulation proof.
- **Unity or Stride:** more engine surface and project metadata than the first
  deterministic dot-based milestone needs.
- **Custom SDL/OpenGL/Vulkan/DirectX:** duplicates window, input, rendering, and
  packaging work already handled by MonoGame.
- **3D:** adds asset, camera, lighting, and performance complexity without
  improving the initial combat proof.

## Testing limitations

Core and headless verification are non-graphical. A successful client build
does not prove graphics initialization, menu hit-testing, input focus, or clean
window shutdown; those require a separate interactive Windows smoke test.

## Deferred decisions

Linux, Steam Deck, macOS, mobile, console, web, multiplayer, store packaging,
self-contained publishing, trimming, and native AOT are out of scope until the
Windows simulation proof is accepted.
