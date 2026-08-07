# Third-party notices — Sandata.Core/Navigation

This directory contains code ported from third-party sources. Each entry
below records the source, its licence, and what that licence requires of
this project.

## DotRecast

`Funnel.cs` ports Recast/Detour's "simple stupid funnel" string-pull
algorithm — the loop inside `DtNavMeshQuery.FindStraightPath` — from
[DotRecast](https://github.com/ikpil/DotRecast), a C# port of
[Recast Navigation](https://github.com/recastnavigation/recastnavigation).

- **Source:** https://github.com/ikpil/DotRecast
- **Licence:** zlib
- **What the licence requires:** attribution only. The zlib licence permits
  use, modification, and redistribution, in source or binary form, without
  further permission or royalty, on the sole conditions that the origin of
  the software is not misrepresented and that the licence notice is not
  removed from the source distribution. This file, together with the header
  comment at the top of `Funnel.cs`, is that attribution.

The algorithm was rewritten from floating-point arithmetic over navmesh
polygon portals to integer arithmetic over `NavGrid` cell-edge portals; no
DotRecast source file, package, or binary is included or referenced by this
project, and no NuGet dependency was added to make this port.
