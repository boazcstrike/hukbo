# Visual improvement smoke (VIS-041 and VIS-043) — passed rows

**Archived: reference only.** These rows were moved out of
`docs/development/smoke-checklist.md` on 2026-08-11, the day they were run.
Every row in this file is `PASS`; nothing here is outstanding and nothing here
is an instruction. It is kept so that a later reader can trace what a person
actually saw when the improve-visuals package was checked on a screen for the
first time.

**Three rows from the same session are not here, and are not `PASS`.** Rows
128 and 129 (`VIS-023`, armor bulk and adornment accents) and row 131
(`VIS-028`, trampled ground) failed. A fix for all three shipped the same day,
so each was reopened to `PENDING` keeping its failure observation, per the live
checklist's own reopening rule, and all three stay there under "Visual
improvement smoke — the three open rows" until a person re-runs them. Burying
open work in this folder would hide a defect behind a directory nobody is
allowed to cite.

The live checklist is `docs/development/smoke-checklist.md`. Do not re-run
these rows from this file. If a later change touches weapon tints, shield
skins, the appearance roster, the grass ground, the sway setting, or the
visual-catalog fallback path, write fresh rows in the live checklist rather
than reviving these.

---

## The session

Run by a person at an interactive Windows desktop on 2026-08-11, against the
protocol both sections carried: a fresh `./scripts/run.ps1` at seed 1, the
three named camera stations (minimum zoom, default fit, maximum zoom), and the
default and high-contrast themes cycled through the in-game selector.

Both sections had stood **entirely `PENDING` since they were written.** This
was the first time any of the thirty-two rows had been attempted.

| Evidence field | Recorded value |
| --- | --- |
| Date | 2026-08-11 |
| Machine/platform | Microsoft Windows 10.0.26200 (Windows 11 Pro) x64 |
| Source commit | `4fbbdf9`, the repository head at the time of the run |
| Launch path (`source` or package path) | `source`, via `./scripts/run.ps1` |
| Optional screenshot paths | None recorded |

Twenty-nine of the thirty-two rows passed. The tester's report on the three
that did not was the same word each time — the effect is not clear — and it is
recorded in the live checklist rather than here.

The `Actual` column below reads `Observed as expected` for each row. That is
what was reported: the tester worked down both tables and called out only the
rows that did not read correctly. It is not a claim that each row was written
up individually.

## Visual improvement milestone smoke (VIS-041)

Covered the first milestone of the improve-visuals implementation plan draft:
the Kalis tint family, the S1 shield skin, the five levy clothing presets, the
grass ground and its sway, the motion-intensity gate, and the visual-catalog
fallback placeholder.

Rows marked with a dagger (†) instantiated a requirement traced elsewhere in
that plan; the notes that followed the table are reproduced under it.

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| 102. Kalis tints at minimum zoom | At the minimum-zoom station, default theme, MotionIntensity Full, Kalis-armed pawns remain classifiable as Kalis-wielders; the `freshIron`/`wellOiled` tint difference is invisible or below the threshold of notice at this distance. | Observed as expected | PASS |
| 103. Kalis tints at default fit | At the default-fit station, default theme, compare a `freshIron` and a `wellOiled` Kalis pawn side by side. The tint reads as material variation on the same weapon, never as a different weapon. | Observed as expected | PASS |
| 104. Kalis tints at maximum zoom | At the maximum-zoom station, default theme, close in on a single Kalis pawn. The tint is visible without breaking weapon-role recognition — it still reads unmistakably as a Kalis. | Observed as expected | PASS |
| 105. S1 shield distinguishable at minimum zoom | At the minimum-zoom station, default theme, compare a shield-bearing pawn (S1 `mactanThin`) against an unshielded pawn of the same weapon. Shield bearers are distinguishable from solo warriors without zooming in or clicking either. | Observed as expected | PASS |
| 106. S1 shield reads as the same equipment † | At the default-fit station, default theme, examine an S1 `mactanThin` shield bearer. The skin reads as ordinary shield equipment, not as a different or a visibly reduced piece of equipment compared to an unshielded pawn's absence of one. | Observed as expected | PASS |
| 107. Levy presets read as varied but coherent | At the default-fit station, default theme, observe the five levy clothing presets across the roster. The five read as visibly varied from one another while still reading as clothing belonging to the same army, not as unrelated or mismatched equipment. | Observed as expected | PASS |
| 108. Levy presets do not misread faction or equipment | At the default-fit station, default theme, compare warriors wearing different levy presets across both factions. No preset reads as belonging to the other faction, and no preset reads as a different weapon or equipment identity than the pawn actually carries. | Observed as expected | PASS |
| 109. Grass reads as grassland, not a checkerboard | Cycle through the minimum-zoom, default-fit, and maximum-zoom stations, default theme, observing the battlefield ground at each. At every station the ground reads as living grassland with grass clusters scattered across it, not as a flat repeating checkerboard tile pattern. | Observed as expected | PASS |
| 110. Arena border still reads as the strongest line | At the default-fit station, default theme, compare the arena border against the new grass ground. The border remains the visually strongest line on the field; the grass rendering does not compete with it or make it harder to find. | Observed as expected | PASS |
| 111. Sway reads as alive, not as noise | At the default-fit station, default theme, MotionIntensity Full, watch the grass during a busy engagement (multiple pawns fighting on screen at once). The sway reads as gentle, organic motion — alive — rather than as flicker or visual noise. | Observed as expected | PASS |
| 112. No sway motion visible at minimum zoom | At the minimum-zoom station, default theme, MotionIntensity Full. No grass motion is visible at this distance — the detail-tier gate suppresses sway at minimum zoom regardless of the motion setting. | Observed as expected | PASS |
| 113. High-contrast theme shows zero grass motion | At the default-fit station, high-contrast theme, MotionIntensity Full. The high-contrast theme shows zero grass motion, independent of the MotionIntensity setting. | Observed as expected | PASS |
| 114. Motion setting is operable and gates sway exactly † | Open Menu, locate the Motion Intensity control, and cycle it through `Off`, `Reduced`, and `Full` while watching the grass at the default-fit station, default theme. The control is reachable and operable from the menu. `Off` shows exactly zero grass motion — the off switch is exact, not merely reduced. `Reduced` shows visibly damped motion. `Full` shows the full sway amplitude. | Observed as expected | PASS |
| 115. Forced-failure placeholder is conspicuous | Run the forced-failure debug configuration that exercises the visual-catalog resolver's fallback path. Observe the affected element's position, then inspect the session's debug log on the `assets` channel. The diagnostic placeholder is conspicuously visible at the affected element's position — not blended in, not easy to miss — and the `assets` channel logs the fallback event exactly once for that identifier. | Observed as expected | PASS |

**Row 106** instantiated R-X.12's false-cause guard (no equipment reading as
less mechanical coverage than another) for the milestone's single shipped
shield skin. The full multi-skin comparison it deferred is row 121 below.

**Row 114** was the milestone completion condition the plan's milestone section
called "sway off-switch exact": the row required observing that
`MotionIntensity Off` produces literally zero grass motion, not merely a damped
one, in addition to confirming the control is reachable from the menu.

## Visual improvement full-package smoke (VIS-043)

Covered every post-milestone task whose own "Manual visual verification"
section called for a row the milestone checklist had not already created.
Fifteen of the eighteen rows are below; rows 128, 129, and 131 are in the live
checklist, not here.

| Check | Expected observation | Actual | Status |
| --- | --- | --- | --- |
| 116. Weapon variants at minimum zoom, all four weapons | At the minimum-zoom station, default theme, with 200+ pawns: each of the four weapon roles (Kampilan, Wasay, Kalis, Itak) remains classifiable, and tint variation across all four is invisible or below the threshold of notice at this distance. | Observed as expected | PASS |
| 117. Weapon variants at default fit, all four weapons | At the default-fit station, default theme: each pawn's weapon role is identifiable at a glance across all four weapons, and every tint reads as material variation on the same weapon, never as a different weapon. | Observed as expected | PASS |
| 118. Weapon variants at maximum zoom, including the Wasay lashing band | At the maximum-zoom station, default theme, close in on pawns carrying each of the four weapons in turn. Tint and wear variation is visible without breaking role recognition for any of the four; the Wasay's rattan lashing band at the head-haft junction reads as a lashed band, not as damage or a new weapon part. | Observed as expected | PASS |
| 119. Weapon inspector shows label, tier, and note | Select a pawn carrying each of the four weapons in turn. The inspector shows the unchanged pair-form weapon label, the selected variant's evidence tier, and its note; for a weapon with inspector-only entries (Kampilan k2, Kalis l2/l3), those appear labelled as later-or-provisional forms, never as anything the selected pawn is shown wearing. | Observed as expected | PASS |
| 120. Pawns render identically to the pre-package build at all three zoom stations | Compare a pawn's rendered appearance today against the pre-package build at the minimum-zoom, default-fit, and maximum-zoom stations. Weapon grip position, shield position, and layer draw order all look unchanged — this task only added anchor fields and empty layer slots, it drew nothing new. | Observed as expected | PASS |
| 121. Shield skins at default fit: four skins read as variation, S5 accent reads as binding † | At the default-fit station, default theme, compare shield-bearing pawns across all four shipped skins (S1 `mactanThin`, S2 `morgaFullBody`, S3 `boxerCagayan`, S5 `visayanKalasag`). All four read as variation of one shield, not as different pieces of equipment; on an S5-skinned pawn, the horizontal rattan accent reads as a binding detail, not as damage. | Observed as expected | PASS |
| 122. Shield skins at maximum zoom: face tones, curvature, edge step, and angled posture | At the maximum-zoom station, default theme, close in on shield-bearing pawns across all four skins. Face tones, the S3 curvature, and the High-tier edge-tone step are all visible; the shield's angled forward posture (S12) reads as an active stance, not as a layout bug, for every skin. | Observed as expected | PASS |
| 123. Shield skins under the high-contrast theme remain unambiguous | Switch to the high-contrast theme at the default-fit station. The shield block remains unambiguous against both torso and ground for all four skins — no skin blends into its background or becomes hard to identify as a shield. | Observed as expected | PASS |
| 124. Shield inspector shows label, anchor tag, tier, note, and pending flags | Select a shield-bearing pawn for each of the four skins in turn. The inspector shows the plain label `Tall Hardwood Shield`, the skin's anchor tag, its evidence tier, and its note, including the pending-verification flags on the *kalasag* (S5) and any *palisay* reference — with neither name appearing as a bare player-facing label anywhere in the panel. | Observed as expected | PASS |
| 125. Fifty-plus presets read as varied but coherent at normal zoom | At the default-fit station, default theme, observe the full roster (levy plus Visayan, Tagalog, and Northern Luzon blocks) across both factions. The fifty-plus presets read as visibly varied from one another while still reading as clothing belonging to the same two armies, not as unrelated or mismatched equipment. | Observed as expected | PASS |
| 126. Elite figures read as denser in gold and dye, not larger | At the default-fit station, default theme, compare an elite- or datu-marked preset (gold accents, richer dye) against an ordinary preset from the same block. The elite figure reads as denser in gold and dye detail; it never reads as a physically larger pawn. | Observed as expected | PASS |
| 127. At minimum zoom, faction and weapon role remain the dominant reads | At the minimum-zoom station, default theme, with 200+ pawns drawn from the full roster across all blocks. Faction (by ground-ring color) and weapon role remain the dominant, most legible reads on the field; no preset's clothing or color competes with either for attention at this distance. | Observed as expected | PASS |
| 130. Appearance inspector shows preset name, scope tag, tier, and component notes | Select any pawn from the full roster. The inspector shows the preset's plain-English name, its scope tag, its evidence tier, a per-component tier list with must-not-generalize notes, pending-verification flags where applicable, and any non-renderable flavor lines — with no bare Filipino term appearing unpaired anywhere in the panel. | Observed as expected | PASS |
| 132. Dust reads as impact punctuation, not weather | During a busy engagement, observe the brief dust puffs spawned on `Death` events. The dust reads as a short, localized punctuation of an individual impact, not as ambient weather or a persistent haze across the field. | Observed as expected. VIS-029 did ship this pass, so the row's own not-shipped fallback to `BLOCKED` did not apply. | PASS |
| 133. With 200+ pawns, faction remains readable by ring shape and position, hue disregarded † | At the default-fit station, default theme, with 200+ pawns on the field. A human with typical color vision judges the faction ring's shape-and-position channel alone, disregarding hue, and finds faction still distinguishable by that channel. | Observed as expected | PASS |

**Row 121** instantiated R-X.12's false-cause guard for the full four-skin
shield roster, completing the comparison row 106 deferred.

**Row 133** was an honest partial check: it held only the no-regression floor
that no new garment, tint, skin tone, or ground shade introduced by the package
had become a competing faction signal. It was never colour-blind verification,
and OD-7's stronger shape-redundant faction marker remains a backlog item in
`docs/plans/TODO.md`. This `PASS` does not close that item.

## Still outstanding, and not closed by this session

**The line-by-line historical review of the preset roster has not been
performed.** Both the implementation plan draft and `warrior-appearance-design.md`
called for the full preset roster table to be read against
`docs/research/improve-visuals/warrior-appearance-historical-research.md`. That
is a human read-through of one document against another, not an observation of
the running game, so it never was a checklist row and none of the `PASS` results
above bear on it. A failure found during it routes to a content-correction task.
It is carried forward in the live checklist.
