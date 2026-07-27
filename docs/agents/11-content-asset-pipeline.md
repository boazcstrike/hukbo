# 11 — Content and Asset Pipeline

## Scope

Provide the smallest reproducible content build needed for labels and
diagnostics while avoiding unnecessary binary assets.

## Inputs inspected

- Client `.mgcb` content definition.
- MonoGame Content Builder Task package.
- Menu requirement for a SpriteFont.
- Client workstream report that `dotnet-mgcb` requires a pinned local tool
  manifest.

## Decisions and work

The dot texture remains runtime-generated. The only compiled content is the
SpriteFont used for menu and diagnostics. Bootstrap restores the
repository-local dotnet-mgcb 3.8.5 tool.

## Files

- `src/Hukbo.Client/Content/**` (owned by Client/Menu workstream)
- `.config/dotnet-tools.json` (owned by orchestrator)
- `scripts/bootstrap.ps1`
- `docs/dependency-risk-register.md`

## Verification

Tool restore passed. The Content Builder compiles six baked SpriteFont
descriptors — `UiCaption`, `UiBody`, `UiLabel`, `UiSubtitle`, `UiTitle`, and
`UiDisplay` — from two vendored typefaces, Rajdhani SemiBold and Bebas Neue
Regular, both licensed under the SIL Open Font License 1.1, during the
zero-warning Release build and self-contained package publish.

License texts travel with the build. `src/Hukbo.Client/Hukbo.Client.csproj`
copies `Content/Fonts/OFL-*.txt` and `Content/Fonts/README.md` to the output
directory with `CopyToOutputDirectory="PreserveNewest"`. The Open Font License
requires its text to accompany any distribution of the fonts, and the fonts
travel inside the compiled SpriteFont atlases whether or not the source
TrueType files ship, so the license has to be present in the build output for a
packaged build to be compliant on its own.

The `.ttf` files themselves are deliberately not copied. The content pipeline
consumes them at build time to rasterize the atlases, and the game never reads
them at runtime.

Verified 2026-07-27 in both locations. After a zero-warning Release build,
`src/Hukbo.Client/bin/Release/net10.0/win-x64/Content/Fonts/` contains
`OFL-Rajdhani.txt`, `OFL-BebasNeue.txt`, `README.md`, and the six compiled
atlases `UiCaption.xnb`, `UiBody.xnb`, `UiLabel.xnb`, `UiSubtitle.xnb`,
`UiTitle.xnb`, and `UiDisplay.xnb`, totalling roughly 669 kilobytes. After
`./scripts/package.ps1 -Runtime win-x64`, which reported
`[PASS] Windows package published`, the same nine files are present in
`artifacts/packages/client-win-x64/Content/Fonts/`.

This gap was found by the documentation pass, which recorded it as unresolved
rather than asserting the acceptance criterion was met. The copy item was added
afterwards and the criterion is now genuinely satisfied.

## Status

**COMPLETE**

## Limitations

None. The font licensing exposure this document previously recorded is
resolved: both typefaces are vendored into the repository at
`src/Hukbo.Client/Content/Fonts/` with their SIL Open Font License 1.1 texts
and a provenance README recording the upstream repository, path, commit, and
retrieval date.

## Next action

None.
