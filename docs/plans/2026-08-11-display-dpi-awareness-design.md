# Display DPI awareness — design

Status: proposed, 2026-08-11.

## 1. The defect

Text in the Hukbo client is visibly pixelated whenever the window fills the
screen. This was reported by a person running the game on 2026-08-11 and it
failed three rows of the interactive smoke checklist — `UI-2`, `UI-4`, and
`UI-6`, recorded in `docs/development/smoke-checklist.md` under "Responsive
menu, startup display, and UI motion smoke", finding 1.

**The typography pipeline is not at fault, and changing it would not help.**
`UiFontRamp` bakes twenty-four separate `SpriteFont` atlases — six roles at
each of four tiers — and both text entry points, `UiPrimitives.DrawText` and
`UiPrimitives.DrawCenteredText`, draw at a hardcoded scale of `1f` from a
whole-pixel origin snapped by `UiTextGeometry.SnapToPixel`. There is no render
target on the UI path, no float resampling, and no scale multiplier. Every
glyph is crisp at the moment it is submitted.

The resampling happens after the frame leaves the process. Nothing in this
repository declares a DPI awareness level:

- `src/Hukbo.Client/Hukbo.Client.csproj` has no `ApplicationManifest`.
- There is no `app.manifest` anywhere in the tree.
- No code calls `SetProcessDpiAwarenessContext` or `SetProcessDPIAware`.
- Neither the client nor `scripts/run.ps1` sets SDL's
  `SDL_WINDOWS_DPI_AWARENESS` hint, and MonoGame's DesktopGL backend does not
  set it either.

Windows treats a process that declares nothing as DPI-unaware. It reports a
virtualised desktop size, lets the application render at that size, and then
bitmap-stretches the finished frame up to the physical panel. On the reporting
machine the display is 2560x1440 and Windows display scaling is 125%
(`HKCU:\Control Panel\Desktop\WindowMetrics\AppliedDPI` is `120`), so the
process is told the desktop is 2048x1152 and its output is stretched by a
non-integer factor of 1.25. That stretch is the pixelation.

### The second consequence

`UiScalePolicy.Resolve` picks a font tier from the viewport in pixels. Its
thresholds are 1920x1080 for `Percent125`, 2560x1440 for `Percent150`, and
3840x2160 for `Percent200`. A fabricated 2048x1152 viewport clears the first
and misses the second, so **Auto resolves to `Percent125` on a display that
should be getting `Percent150`**. The policy is correct; it is being fed a
number the operating system invented.

This is why `UI-4` fails alongside the two rows about window size. It is not a
separate bug and it needs no separate fix — a DPI-aware process reports
2560x1440 and the existing policy selects the right tier on its own.

## 2. History

This is the second half of a decision already recorded. Typography row 75,
"Display scaling", is marked `DECLINED` in the smoke checklist: the 100%
reading was taken during implementation and the 150% Windows-scaling reading
was declined on 2026-07-28, the remedy it gated being judged unnecessary at the
time. That decision is why the awareness declaration was never written. The
defect was latent until someone ran the game on a scaled display.

Row 75 stays `DECLINED`. It asked for a measurement to justify building this;
the justification arrived instead as three failed rows, which is better
evidence than the measurement would have been.

## 3. The remedy

Declare per-monitor v2 DPI awareness once, in the client's entry point, before
any window or graphics device exists.

Two mechanisms were considered.

**An `ApplicationManifest` declaring `PerMonitorV2`.** This is the canonical
Windows approach and needs no runtime call ordering. Rejected as the primary
mechanism because the awareness level then becomes invisible to the debug log —
there is no point at which the process can report what it asked for and what it
got, and this repository's standard is that a development run leaves behind a
record an agent can read without having watched the screen.

**A `SetProcessDpiAwarenessContext` call at the top of `Program.Main`.**
Chosen. It runs before `new ArenaGame(log)`, therefore before
`GraphicsDeviceManager` is constructed and before SDL creates a window, which
is the ordering requirement. It returns a boolean, so the outcome is
observable, and the entry point already holds the `DiagnosticLog` needed to
record it. It matches the `LibraryImport` source-generated P/Invoke pattern
`ArenaGame` already uses for its SDL window-chrome calls — a plain `DllImport`
raises SYSLIB1054 under this repository's repo-wide `TreatWarningsAsErrors`,
which `CLAUDE.md` forbids suppressing.

`DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2` is the pseudo-handle `-4`. The
function is `user32.dll`, available from Windows 10 version 1703. The call is
guarded by `OperatingSystem.IsWindows()`, both because `Hukbo.Client` is
DesktopGL and nominally portable and because the platform-compatibility
analyzer requires the guard under `TreatWarningsAsErrors`.

A failure is not fatal. If the call returns false — an already-set awareness
level, or a Windows build older than 1703 — the game runs exactly as it does
today, pixelated on a scaled display but functional. The failure is logged at
`warn` with the Win32 error code and the run continues.

## 4. What changes on screen

On a display with Windows scaling at 100%, nothing changes at all: an unaware
process and an aware one are handed the same numbers.

On a scaled display, three things change together.

1. **Text becomes crisp**, because the frame is presented at the panel's own
   resolution and the operating system no longer resamples it.
2. **`UiScalePolicy` selects a larger tier**, because the viewport it is given
   is now the real one. On the reporting machine that is `Percent150` rather
   than `Percent125`, which is the tier the ramp was built to serve at 1440p.
3. **The startup window is smaller in physical millimetres.**
   `InitialWindowWidth` and `InitialWindowHeight` are 1280x720 and they now
   mean 1280x720 real pixels rather than 1280x720 virtual ones. This is the
   correct behaviour and it is what every DPI-aware application does, but it is
   a visible difference on first launch and it is the one thing here a person
   might mistake for a regression.

Point 3 is why `UI-5` must be re-run even though it passed: it asserts the
window opens at 1280x720 and cannot be resized below 1024x720. Both remain
true in pixels, but the row should be confirmed against the aware build rather
than assumed.

## 5. Answers to the nine questions in `SIMULATION-GAME-STANDARDS.md` section 10

1. **User-visible outcome.** Text is crisp at every window size on a display
   with Windows scaling other than 100%, and the automatic UI-scale tier is
   chosen from the real viewport.
2. **Tick stage and state read/written.** None. This change does not touch
   `Hukbo.Core`, does not run inside a tick, and reads and writes no simulation
   state.
3. **Numeric units and bounds, same-tick conflict rule.** Not applicable. The
   one numeric value is the pseudo-handle `-4`, a fixed constant of the Win32
   ABI.
4. **Total ordering and random-stream policy.** Not applicable. No ordering
   decision and no random stream.
5. **Cache source and invalidation.** No cache. The awareness level is process
   state set once before the graphics device exists and never re-read.
6. **Save, event, or version effect.** **Presentation only.** No state hash, no
   event hash, no preset version, no persisted settings field, no golden
   expectation moves. The determinism workload in the canonical gate runs
   headless with no window and is untouched.
7. **Worst-case complexity and benchmark workload.** One P/Invoke at process
   start, O(1), unmeasurable. No benchmark workload applies. A DPI-aware
   process on a scaled display renders more pixels per frame than a virtualised
   one — 2560x1440 against 2048x1152 is 1.56 times the fill — which is a real
   GPU cost borne by the change and is the correct cost, since the alternative
   is the OS doing the same upscale worse.
8. **Spectator explanation.** The awareness outcome is written to the debug log
   at boot as `boot.dpi.awareness`, and the existing `LogViewport` snapshot
   already reports client bounds, viewport, back-buffer size, display mode, and
   the active scale tier on every viewport change. A person comparing one log
   line before and after the fix sees the fabricated 2048x1152 become the real
   2560x1440. A spectator with no log sees crisp text, which is the whole point
   and needs no explanation.
9. **Tests that fail before and pass after.** See section 6.

## 6. What is testable, and what is honestly not

**Not testable in this repository:** whether the P/Invoke succeeds. It sets
process-wide state, it cannot be undone once set, and asserting on it inside a
test host would either fight the test runner's own awareness level or leak
across tests. A test that called it would be testing Windows, not Hukbo. No
such test will be written, and the absence is deliberate rather than an
oversight.

**Testable, and where the value is:** the decision to attempt the call, and the
tier the policy selects from a real viewport. Both go through pure helpers.

- `ProcessDpiAwareness.ShouldAttempt(bool isWindows)` — a pure predicate, so
  the platform gate is pinned rather than buried in an `if` nobody can reach.
- `ProcessDpiAwareness.DescribeOutcome(...)` — turns the boolean and the Win32
  error code into the log payload, so the recorded evidence has a shape a test
  can assert.
- `UiScalePolicyTests` gains the two viewports this defect turns on: 2048x1152
  must resolve to `Percent125` and 2560x1440 to `Percent150`. The first pins
  the wrong answer the fabricated viewport produces, so that if anyone later
  changes the thresholds the connection to this defect is not lost.

The real verification of this change was `UI-2`, `UI-4`, and `UI-6` re-run by a
person at a scaled display. **No automated test could close them and no agent
could flip them.** They were returned to `PENDING` with their `FAIL`
observation preserved, the same pattern the `CL` clash rows used across the
combat-cadence change, and a person then re-ran and closed all three `PASS` on
2026-08-13. The rows and their history now live in the archive record
"Responsive menu, startup display, and UI motion smoke — closed 2026-08-13",
since the family was deleted from the live checklist that day.

## 7. Scope: Hukbo only

`Sandata.Client` has exactly the same defect — its `Program.cs` declares no
awareness either, so its map is drawn at a virtualised size and stretched. It
is deliberately **not** fixed here.

Sandata is a separate product whose binding document is
`docs/plans/2026-08-07-sandata-scaffold-design.md`, and `CLAUDE.md` section 3
forbids either game reaching into the other. The remedy cannot be shared
without a home for it: `Hukbo.Shared.Core` is tier 1 and is exactly four
determinism primitives, a P/Invoke is not one; `Hukbo.Diagnostics` is the debug
log and DPI awareness is not diagnostics; and the tier-2 client extraction is
deferred by design. So fixing Sandata means a second copy of the helper in
`Sandata.Client`, which is a decision for Sandata's own plan rather than a
side effect of a Hukbo bug fix.

Sandata's client also ships no font and makes no `DrawString` call at all, so
the reported symptom — pixelated text — cannot occur there yet. Recorded so it
is not rediscovered.

## 8. Tasks

1. Add `src/Hukbo.Client/Settings/ProcessDpiAwareness.cs`: the `LibraryImport`
   declaration, the `PerMonitorAwareV2` constant, the pure `ShouldAttempt` and
   `DescribeOutcome` helpers, and an `Apply` method returning the outcome.
2. Add `LogEvents.BootDpiAwareness = "boot.dpi.awareness"` to
   `src/Hukbo.Diagnostics/LogEvents.cs`, in the boot block, alphabetically.
3. Call it from `src/Hukbo.Client/Program.cs` before `new ArenaGame(log)` and
   write the outcome line. Success at `inf`, failure at `warn` with `msg`.
4. Add `ProcessDpiAwarenessTests` to `tests/Hukbo.Client.Tests/`.
5. Extend `UiFontRampTests`' `UiScalePolicy` theory with the 2048x1152 and
   2560x1440 viewports.
6. ~~Return `UI-2`, `UI-4`, and `UI-6` to `PENDING` in
   `docs/development/smoke-checklist.md`, preserving the `FAIL` observation in
   `Actual`, and note that `UI-5` wants a re-run for the physical-size
   change.~~ Done, and superseded on 2026-08-13: a person re-ran and closed
   all three rows `PASS`, and the family then left the checklist for the
   archive record "Responsive menu, startup display, and UI motion smoke —
   closed 2026-08-13".
7. Run `./scripts/verify.ps1` and record the real output.

## 9. Verification criteria

- The canonical gate is green, with its output pasted rather than summarised.
- Both test suites pass; `Hukbo.Client.Tests` gains the new cases.
- A `Debug` run writes one `boot.dpi.awareness` line, and the following
  `render.viewport` line reports the display's real resolution rather than the
  virtualised one.
- `UI-2`, `UI-4`, `UI-6` were `PENDING`, not `PASS`, until a person at a
  scaled display closed them — the principle this criterion states, and the
  same person did exactly that on 2026-08-13, closing all three `PASS` and
  retiring the family to the archive record "Responsive menu, startup
  display, and UI motion smoke — closed 2026-08-13".
