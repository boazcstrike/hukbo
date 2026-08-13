# Agent inspector row wrapping — plan

Design: `docs/plans/2026-08-14-inspector-row-wrapping-design.md`. Read it first;
it carries the decisions D1 through D6 that the tasks below execute and the
reasoning for the two alternatives that were rejected.

Closes the horizontal half of smoke row `BR-10`. **It does not close the row** —
only a person at an interactive desktop may do that, and the row's vertical
question is explicitly left open by design decision D5.

## Scope boundary

Presentation only. Every file below is under `src/Hukbo.Client/UI/` or
`tests/Hukbo.Client.Tests/`. No `Hukbo.Core` file, no preset, no ruleset, no
state or event hash, and no simulation behaviour is touched, so the gate's
determinism workload is unaffected by this work.

**Files this plan may edit, and no others:**

- `src/Hukbo.Client/UI/AgentInspectorContent.cs`
- `src/Hukbo.Client/UI/AgentInspectorPanel.cs`
- `tests/Hukbo.Client.Tests/AgentInspectorContentTests.cs`

**Files this plan may not edit**, because another workstream holds them:
`ClientSettingsStore.cs`, `BattleSimulation.cs`, `CohortDeploymentAssignment.cs`,
anything under `src/Hukbo.Client/Presentation/` or `src/Hukbo.Client/Rendering/`,
and `docs/development/smoke-checklist.md`.

## Tasks

| # | Task | Files | Verification | Depends on |
| --- | --- | --- | --- | --- |
| 1 | Add a wrapping helper that applies a hanging indent to continuation lines. It takes the text, the width budget, the measure delegate, and an indent string; it returns the first line unchanged and every later line prefixed with the indent, each still measured to fit the budget. Reuse the existing `WrapText` word-splitting rather than writing a second one | `AgentInspectorContent.cs` | New unit tests: a short string returns one line unchanged; a long string returns lines where only the first is unindented; no returned line exceeds the budget under the measure delegate | — |
| 2 | Split the combo-attributes row into one row per value group instead of one 99-character packed string, and split the attributes row the same way (design D3) | `AgentInspectorContent.cs` | The formatter returns several short strings; existing tests that assert on these rows updated to the new shape; each returned string measures under the budget at 8 px per character | — |
| 3 | Route the four top-detail rows through the task-1 helper before drawing | `AgentInspectorPanel.cs` | No top-detail row reaches `DrawText` unmeasured. Verified by reading the draw path; there is no GPU test for this | 1 |
| 4 | Route every row from `BuildLowerLines` through the task-1 helper before drawing, preserving row order and the existing vertical bounds refusal | `AgentInspectorPanel.cs` | Row order unchanged; a row that would fall past the panel bottom is still refused rather than drawn | 1, 2 |
| 5 | Raise the panel's reserved lower-row budget to account for wrapping, so the panel is sized for the wrapped worst case rather than silently dropping rows it could have fitted. Update `MaximumLowerRowCount`'s doc comment to say what it now counts | `AgentInspectorContent.cs` | The height arithmetic test still passes; the comment matches the constant | 2, 4 |
| 6 | Add the test the family never had (design D6): take every string the row formatters can produce at their longest realistic values, wrap each at the 277-pixel budget across the existing 5, 6, 7 and 8 px-per-character theory range, and assert no returned line exceeds the budget | `AgentInspectorContentTests.cs` | The test fails if task 1 or task 4 is reverted. **Prove that** — revert one locally, watch it go red, restore it. A test that passes both ways is worthless here | 1, 2 |

## Verification

Run and paste the real output of:

```powershell
./scripts/test.ps1 -Configuration Release
```

The Client suite is the one that matters here, but both run. Note that a
`scripts/*.ps1` edit can redden the Client suite through `ScriptDefaultsTests`;
this plan edits no script, so a failure there means something else moved.

Then the canonical gate, once, after integration:

```powershell
./scripts/verify.ps1
```

`./scripts/verify.ps1` with no flag runs Hukbo only, which is correct here —
this change touches no Sandata file.

## What this plan does not do

- It does not widen the panel (design D4).
- It does not fix the panel's vertical fit at the minimum window (design D5).
  That is a pre-existing defect with its own open question in design section 6.
- It does not flip `BR-10` to `PASS`, or edit the smoke checklist at all. The row
  keeps its `FAIL` observation in `Actual` and returns to `PENDING` so the re-run
  is judged against what was actually seen.
