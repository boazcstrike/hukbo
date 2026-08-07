<div align="center">

# Hukbo

**A deterministic, offline battle simulator of pre-colonial Philippine warfare.**

Two autonomous factions meet on an open field. You do not command them — you
watch, pause, inspect any warrior, and replay the exact same battle as many
times as you like.

[![.NET](https://img.shields.io/badge/.NET-10.0.302-512BD4?logo=dotnet&logoColor=white)](global.json)
[![MonoGame](https://img.shields.io/badge/MonoGame-3.8.5%20DesktopGL-E73C00?logo=monogame&logoColor=white)](Directory.Packages.props)
[![C#](https://img.shields.io/badge/C%23-14-239120?logo=csharp&logoColor=white)](src)
[![Platform](https://img.shields.io/badge/platform-Windows%20x64-0078D6?logo=windows&logoColor=white)](docs/platform-support-matrix.md)

[![Tests](https://img.shields.io/badge/tests-5448%20passing-3FB950)](docs/development/testing.md)
[![Determinism](https://img.shields.io/badge/determinism-verified-3FB950)](SIMULATION-GAME-STANDARDS.md)
[![Gate](https://img.shields.io/badge/gate-local%20only-8B949E)](scripts/verify.ps1)
[![Offline](https://img.shields.io/badge/offline-no%20telemetry-8B949E)](#offline-by-design)
[![Milestone](https://img.shields.io/badge/milestone-v0.1%20tactical%20layer-FFA657)](#direction)

[Run it](#run-the-game) ·
[Controls](#controls) ·
[Determinism](#determinism) ·
[Architecture](#architecture) ·
[History policy](#historical-accuracy) ·
[Docs](#documentation)

</div>

---

## What this is

Hukbo is a 2D spectator battle built with .NET 10 and MonoGame DesktopGL. Two
autonomous factions fight without player input. Every decision — who to target,
whether a blow lands, where it lands, who dies — is made by the simulation, and
the same seed on the same build produces a byte-identical battle every time.

| | |
| --- | --- |
| **Nobody is driving** | Both factions are autonomous. There are no orders, no unit selection, no player side. The spectator controls time and the camera, never the battle. |
| **The same seed is the same battle** | Integer ticks, fixed-point math, a pinned SplitMix64 stream, and a total order on every query. Same seed plus same build gives the same state hash, event hash, winner, and ordered event stream. |
| **It runs with the network cable out** | No telemetry, no accounts, no content downloads, no hosted CI. The build, the tests, and the game are fully offline. |
| **The history is labelled, not asserted** | Every cultural claim carries an evidence tier. Weapons and ranks appear in pair form — `Kampilan — Great Blade`, `Timawa — Bound Freeman` — never as a bare label. |

## Direction

Hukbo is being built toward a 4X strategy game about warfare in the
pre-colonial and early-contact Philippines, roughly the 1500s. What exists today
is the tactical layer of that game: a deterministic battle that a future
campaign layer will configure and score.

| Layer | Status |
| --- | --- |
| **Tactical battle** — autonomous factions, spectator controls, deterministic replay | shipped as v0.1 |
| **Campaign** — explore islands, expand polities, exploit trade, exterminate rivals | not started |

The campaign layer is gated behind Gate 3 in
[Simulation game standards](SIMULATION-GAME-STANDARDS.md): scenario, snapshot,
replay verification, save and resume equivalence, and a reported 500-agent
stress result. Until that gate passes, no campaign, economy, or diplomacy state
enters `Hukbo.Core`. When the campaign layer does start, it becomes a separate
project that *produces* `Scenario` values and *consumes* `BattleOutcome`. The
battle core never learns what a barangay is.

## Run the game

Requirements: Windows x64, PowerShell 7, Git, and .NET SDK 10.0.302 (pinned in
[`global.json`](global.json)).

```powershell
git clone https://github.com/boazcstrike/hukbo.git
cd hukbo
./scripts/bootstrap.ps1     # prerequisite checks and a locked restore
./scripts/run.ps1           # launch
```

The fallback launch command, if you would rather skip the scripts:

```powershell
dotnet run --project src/Hukbo.Client -c Release
```

The game starts paused. Press Play, or press Space.

<details>
<summary><b>Every supported script</b></summary>

```powershell
./scripts/bootstrap.ps1                        # prerequisites and locked restore
./scripts/run.ps1                              # launch the game
./scripts/verify.ps1                           # the canonical gate
./scripts/verify.ps1 -SkipBootstrap            # the gate without re-restoring
./scripts/test.ps1 -Configuration Release      # tests only
./scripts/format.ps1 -Verify                   # formatting check only
./scripts/benchmark.ps1 -Agents 200 -Ticks 10000 -Seed 1
./scripts/package.ps1 -Runtime win-x64         # self-contained client
./scripts/doctor.ps1                           # diagnose a broken environment
./scripts/sfx.ps1 -List                        # sound slots and which have a file
```

`scripts/` holds the only supported entry points. `tools/` holds hand-run
measurement harnesses; they are not in the solution and not in the gate.

</details>

## Controls

| Input | Action |
| --- | --- |
| `Escape` | Open or close the control menu |
| `Space` | Toggle play and pause while the menu is closed |
| `1` `2` `4` | Set simulation speed |
| `R` | Start the next round, advance the deterministic seed, and pause |
| `Shift+R` | Reset to 0-0, seed 1, 1x, camera fit, and a paused match |
| `WASD` or arrow keys | Pan the camera |
| Mouse wheel over the arena | Zoom |
| Mouse wheel over the event log | Scroll battle-event history |
| Click a warrior | Select it for persistent inspection |
| Click empty arena | Clear the selection |
| `F9` | Show or hide the sound log panel |
| `Tab` / `Shift+Tab` | Move keyboard focus forward and backward inside the menu |
| Arrow keys or `WASD` in the menu | Move keyboard focus between menu controls |
| `Enter` | Activate the focused menu control |

On-screen controls: **Play** resumes and, from the modal, also closes the menu.
**Pause** pauses and, from the modal, leaves the menu visible. **Menu** pauses
and opens the modal. The modal carries **Next Round**, **Full Reset**, and
**Exit Game**.

<details>
<summary><b>What the spectator layer guarantees</b></summary>

- Play, Pause, and Menu stay visible above the arena at all times.
- The selected-agent inspector retains a dead warrior's final authoritative
  state, so a death can be read after the fact.
- The battle event feed retains at most 200 ordered events.
- A terminal summary shows the winner, survivors, tick, simulated duration,
  seed, and Next Round.
- Opening the menu pauses logical simulation advancement.
- The HUD and the window title show session-local wins for Team A (Blue) and
  Team B (Red).
- **Next Round** records exactly one win for the outgoing terminal victor,
  advances to a distinct deterministic seed, clears disposable presentation
  state, and starts paused while preserving score, speed, and camera. An
  abandoned round or a draw adds no win, but still advances the seed.
- **Full Reset** clears both win totals, restores seed 1 and 1x speed, fits the
  camera, clears disposable presentation state, and starts paused.

</details>

## What the simulation models

Everything below lives in `Hukbo.Core`, reaches the state hash, and is
reproducible from a seed. None of it is presentation.

**Combat.** An attack is resolved against a target's defences and then against
a hit location. The five outcomes are `Landed`, `ShieldBlocked`, `Parried`,
`Deflected`, and `Evaded`; the seed-1 gate run above resolves 2 244 accepted
attacks into 1 671 landed blows with roughly a quarter of the remainder
attributable to defence. A landed blow picks one of thirteen body parts —
weapon arm, shield arm, shoulder, head, neck, face, chest, abdomen, thigh,
knee, shin, hands, feet — and that part is recorded on the battle event and
decides which impact sound plays. It is metadata only: damage comes from the
attacker's weapon profile alone, and the location struck changes no damage,
health capacity, cooldown, later action, or death. There is no per-part health,
wound, or crippling model.
Landed blows can open a combination chain whose maximum length depends
on the fighter, and a live chain shortens the attack cooldown.

**Equipment.** Four fielded weapons — Kampilan, Wasay, Kalis, Itak — each with
its own damage, reach, and cooldown, carried either alone or paired with a
`TallHardwood` shield. Armour is a single `LightOrganic` entry. Weapon and
shield identities are numbered and pinned: renumbering one is a content-hash
change, not an edit.

**Rank.** Four ranks — Datu, Maharlika, Timawa, Aliping Namamahay — select
clash profiles and roster composition. Rank carries no combat-strength bonus of
its own, for the reason given under [Historical accuracy](#historical-accuracy).

**Movement and formation.** Warriors hold a tactical posture (`Advance`,
`Hold`, `Yield`, `Regroup`, `Pursue`, `Withdraw`) and step through a footwork
lifecycle (`Approach`, `Engage`, `Commit`, `Recover`, `Refuse`, `Disengage`,
`Regroup`, `Pursue`) with sixteen-way facing. Contingents hold formation and
latch on contact, a rally corridor pulls stragglers back, a stall-escape rule
breaks a deadlock that has lasted 192 ticks, an approach sidestep keeps
warriors from queueing single file into the same gap, and a faction reduced to
six or fewer survivors falls into a last stand.

**Collision.** A uniform grid produces candidate pairs, a fairness rule orders
contested moves, and penetration is held at zero — the seed-1 run reports
98 076 candidate pairs, 6 020 contacts, 84 515 accepted moves, and a maximum
penetration of 0. There is no rigid-body physics; distance checks and hitscan
are the model.

### Rulesets and versions

Rulesets are versioned rather than edited, so an old replay keeps reproducing.
Every earlier version stays registered and unmodified.

| Ruleset | Versions | Default | What the newest version added |
| --- | --- | --- | --- |
| Combat preset | V1 – V4 | **V4** | Rank identity, per-rank clash profiles |
| Movement preset | V1 – V6 | **V4** | Equipment-relative footwork (V6) |

The movement default deliberately lags the newest registered preset. V6 is
built and tested, and a `Scenario` can name it, but the client never does: the
default only moves once a battle under the newer preset terminates inside the
tick budget, and it does not yet. The measured evidence is in
[`docs/plans/`](docs/plans/) — see the movement V7 baseline and calibration
records, which located the cause upstream of the tuning that was tried.

## What the client shows and hears

The client draws what the simulation already decided, and nothing here can
change an outcome.

**Panels.** Agent inspector, filterable battle event log with per-event detail,
army composition editor with steppers, a battle report carrying per-faction
combat metrics, a terminal match summary, and a sound log (`F9`). Warriors
carry personal names drawn client-side from the researched sixteenth-century
pool in
[`docs/names/HISTORICAL_1500s_PERSONAL_NAMES.md`](docs/names/HISTORICAL_1500s_PERSONAL_NAMES.md).

**Effects.** Blood, dust, trample marks, grass sway over a plains backdrop,
swing animation posed per weapon, and shield-clash effects. A detail-tier gate
and conservative culling keep the effect load bounded at high agent counts.

**Audio.** 70 generated sound files: 40 attack cues keyed by weapon and body
region, 16 shield-clash cues, 10 death lines, per-faction victory, a draw cue,
and a UI click. A voice ledger and a cue budget cap simultaneous playback so a
mass casualty does not become noise.

**Themes and settings.** Six themes — `command`, `field-manual`, `signal`,
`broadcast`, `high-contrast`, and the default `datu-court` — each filling the
same set of semantic colour roles rather than hard-coded colours. Choices
persist to `%LOCALAPPDATA%\Hukbo\settings.json` (schema version 8): selected
theme, army composition, gore intensity (`Off`, `Stylized`, `Full`), motion
intensity (`Off`, `Reduced`, `Full`), auto-camera mode (`Off`, `Assisted`,
`Follow`), UI scale (`Auto`, 100%, 125%, 150%, 200%), and startup display mode
(windowed or fullscreen).

## Determinism

Determinism is the load-bearing property of this repository, not a nice
property it happens to have. The full contract is §4 of
[Simulation game standards](SIMULATION-GAME-STANDARDS.md).

| Rule | Why |
| --- | --- |
| Authoritative time is an integer tick | Wall-clock time is not reproducible |
| Fixed tick-stage order | Incidental call order must never decide an outcome |
| Every multi-result query has a total order | Ties break on stable `EntityId` |
| `System.Random` is banned | Its sequence is not guaranteed across .NET majors — `Determinism/SplitMix64.cs` is |
| Fixed-point math for anything hashed | `Mathematics/FixedPoint.cs`; floating point drifts |
| Hash-set iteration order may not reach gameplay | Enumeration order is an implementation detail |

Two independent hashes are computed over each run: a **state hash** over
authoritative simulation state and an **event hash** over the ordered event
stream. Changing an enum value, an enum's order, a roster, a weight, or a hash
mixer requires a **new preset version** plus new golden expectations — the old
preset stays registered and unmodified so its replays keep reproducing.

Latest gate run on this machine, 200 agents, 10,000 requested ticks, seed 1:

```
Core     Total tests: 2504   Passed: 2504
Client   Total tests: 2944   Passed: 2944
measuredTicks 981   outcome Faction1Victory   survivors 0 / 6
stateHash 1B73FC5923879AA0   eventHash AC55684F24D39344   deterministic true
coreAllocatedBytes 161168   p50 0.1239 ms   p95 1.05 ms   p99 1.2013 ms
```

## Verify the repository

```powershell
./scripts/verify.ps1
```

The canonical gate runs five stages in order:

1. prerequisite validation and a locked restore;
2. formatting verification;
3. Release solution build;
4. Core and GPU-independent Client tests;
5. a 200-agent, 10,000-tick, seed-1 headless determinism workload.

**Verification is deliberately local-only.** This repository has no GitHub
Actions workflow and no hosted CI service, and it is not going to acquire one.
Run the gate on the integration workstation and record its exact output.
Interactive behavior is proven only by the manual checklist in
[`docs/development/testing.md`](docs/development/testing.md) — compiling is not
a passing test run, and a passing test run is not a smoke check.

Package the self-contained Windows client with:

```powershell
./scripts/package.ps1 -Runtime win-x64
```

Output lands in `artifacts/packages/client-win-x64/`.

## Architecture

```mermaid
flowchart TD
    Client["Hukbo.Client<br/><i>MonoGame DesktopGL</i><br/>rendering · camera · HUD · themes · input · audio"]
    Headless["Hukbo.Headless<br/><i>no window</i><br/>determinism runner · benchmarks"]
    Core["Hukbo.Core<br/><b>authoritative simulation</b><br/>tick pipeline · combat · movement · collision<br/>events · SplitMix64 · fixed point · hashing"]
    Diag["Hukbo.Diagnostics<br/><i>JSON Lines debug log</i>"]

    Client -->|reads state, sends no decisions| Core
    Headless -->|runs, hashes, compares| Core
    Client --> Diag
    Headless --> Diag
    Core -. forbidden .-x Diag
```

| Project | Responsibility |
| --- | --- |
| `src/Hukbo.Core` | The authoritative simulation. Tick pipeline, agents, combat, movement, collision, events, RNG, hashing. |
| `src/Hukbo.Client` | The MonoGame shell. Rendering, camera, UI, themes, input, audio. |
| `src/Hukbo.Headless` | Determinism and benchmark runner. No window. |
| `src/Hukbo.Diagnostics` | JSON Lines debug log shared by Client and Headless. |
| `tests/` | `Hukbo.Core.Tests` and `Hukbo.Client.Tests` |
| `scripts/` | The only supported entry points. PowerShell 7. |
| `docs/` | Design, plans, research, agent-role evidence. |

Two boundaries are enforced by tests, not by convention:

- **`Hukbo.Core` may not reference** MonoGame, the filesystem, the network,
  windowing, audio, the wall clock, or `Hukbo.Diagnostics`. The simulation is
  denied the filesystem and the clock; the logger needs both. Observe the
  simulation from outside, reading state the caller already holds.
- **`Hukbo.Client` may not decide** targeting, damage, retreat, or victory. It
  draws what the simulation already decided.

Client presentation tests never construct an `ArenaGame`, a graphics device, a
sprite batch, or a window, and never depend on GPU, audio, focus, network, or
the wall clock. That is what lets 5 448 tests finish in seconds rather than
minutes.

## Debug logging

A `Debug` run writes `artifacts/logs/hukbo-<utc>-<pid>.jsonl` with no flags at
all — one JSON object per line, so a run leaves behind a record an agent or a
teammate can read without having watched the screen.

```powershell
./scripts/run.ps1 -Configuration Debug -LogLevel trc -LogChannels audio,input
```

```powershell
Get-Content (Get-ChildItem artifacts/logs -Filter *.jsonl |
  Sort-Object Name | Select-Object -Last 1) |
  ConvertFrom-Json | Where-Object ch -eq 'audio' | Select-Object -First 40
```

Every line carries `seq`, `t`, `ms`, `lvl`, `ch`, `ev` first, in that order,
followed by flat `camelCase` payload fields. `ev` is a stable dotted machine
key declared as a `const` on `LogEvents` — never a sentence, never carrying a
value. Levels are `err`, `warn`, `inf`, `dbg`, `trc`, defaulting to `dbg` in
`Debug` and `off` in `Release`. A disabled call allocates nothing, and a test
proves that logging cannot change a simulation by running the seed-1 workload
with logging off and at `trc` and requiring identical hashes.

The binding rules are §5 of [`CLAUDE.md`](CLAUDE.md); the working procedure —
turning the log on, reading it back, adding an event to `LogEvents` — is the
[`hukbo-debug-logging`](.claude/skills/hukbo-debug-logging/SKILL.md) skill.

## Historical accuracy

This is a game about a real place and real people, built on contested
colonial-era sources. The rules in
[Historical weapons research](docs/research/HISTORICAL_1500s_WEAPONS.md) are
binding, not aspirational:

- Every claim is labelled **Documented**, **Documented, form uncertain**, or
  **Provisional reconstruction**.
- A cultural identification reaches player-facing UI only in **pair form** —
  the Filipino name, an em dash, and a plain English descriptor — with its
  evidence tier recorded in metadata and shown in the agent inspector. It never
  appears as a bare, unqualified label.
- A name whose earliest attestation postdates the depicted period by more than
  a century is not used at all. That clause is what keeps the policy
  load-bearing rather than decorative: it is why the *panabas*, whose first
  documented mentions are nineteenth-century, is absent, and the next weapon
  added has to clear the same bar.
- Spanish accounts are evidence about equipment, not neutral ethnography. The
  Boxer Codex guides silhouette and color, not exact technical cataloging.
- Gameplay tuning values are marked provisional in code and tests, never
  presented as historical measurements.
- One region or decade is never generalized to "the Philippines".

The four fielded weapons and the rank ladder, with their tiers:

| Symbol | Player-facing pair form | Evidence tier |
| --- | --- | --- |
| `Kalis` | Kalis — Thrusting Blade | Documented — the best-attested of the four (Pigafetta 1521, then vocabularies from 1612) |
| `Kampilan` | Kampilan — Great Blade | Documented, form uncertain |
| `Wasay` | Wasay — War Axe | Documented, form uncertain |
| `Itak` | Itak — Work Blade | Provisional reconstruction |
| `Datu` | Datu — Chief | Documented (Plasencia 1589) |
| `Maharlika` | Maharlika — Sworn Freeman | Documented (Plasencia 1589) |
| `Timawa` | Timawa — Bound Freeman | Documented (Loarca 1582, Visayan only) |
| `Aliping Namamahay` | Aliping Namamahay — Householder | Documented (Plasencia 1589) |

Rank carries no combat-strength value of its own; no sixteenth-century source
grades fighting ability by social class, and the simulation does not invent one.

## Offline by design

No telemetry. No accounts. No content downloads. No hosted CI. The build, the
tests, the gate, and the game all run with the network cable out.

One script is the exception and it is never part of a pipeline:
`scripts/sfx.ps1` calls the ElevenLabs text-to-sound-effects API, and only when
a person asks for a sound. It reads `ELEVENLABS_API_KEY` from the environment
or from an untracked `.env`. That key never belongs in a tracked file, in
output, or in a commit message.

## Documentation

**Contracts** — read these before changing anything

- [Simulation game standards](SIMULATION-GAME-STANDARDS.md) — determinism
  contract, tick order, benchmark workloads, reviewer checklist, the gates
- [Agent instructions](CLAUDE.md) — the contract coding agents work under
- [`AGENTS.md`](AGENTS.md) — the standalone version for non-Claude tools
- [Testing and verification](docs/development/testing.md) — the gate, the
  recorded baselines, the manual smoke checklist

**Research**

- [Weapons, 1500s](docs/research/HISTORICAL_1500s_WEAPONS.md) —
  sixteenth-century sources with confidence labels
- [Ranks, 1500s](docs/research/HISTORICAL_1500s_RANKS.md) ·
  [personal names](docs/names/HISTORICAL_1500s_PERSONAL_NAMES.md) ·
  [warrior gender](docs/research/HISTORICAL_1500s_WARRIOR_GENDER.md)
- [Army composition](docs/research/ARMY-COMPOSITION.md) ·
  [weapon clash](docs/research/WEAPON_CLASH_1500s.md)
- [Formation and collision mechanics](docs/research/FORMATION_AND_COLLISION_MECHANICS.md) ·
  [large-scale simulation architecture](docs/research/LARGE-SCALE-SIMULATION-ARCHITECTURE.md)
- [Research brief](RESEARCHED.md) — product direction evidence, and the
  language and runtime decision

**Development**

- [Getting started](docs/development/getting-started.md) ·
  [prerequisites](docs/development/prerequisites.md) ·
  [coding standards](docs/development/coding-standards.md)
- [Platform decision](docs/architecture/platform-decision.md) ·
  [platform support matrix](docs/platform-support-matrix.md) ·
  [native dependencies](docs/development/native-dependencies.md)
- [Collision policy decision](docs/decisions/2026-07-27-collision-policy.md)
- [Dependency inventory](docs/dependency-inventory.md) ·
  [decisions](docs/dependency-decisions.md) ·
  [risk register](docs/dependency-risk-register.md)

Active plans live in `docs/plans/`, and the work that has been explicitly
parked — with the decision that parked it — is listed in
[`docs/plans/TODO.md`](docs/plans/TODO.md).

**`docs/archives/` is deprecated by definition** — it is the dump for finished
and abandoned work, kept only so a past decision can be traced to its
reasoning. Never execute an archived plan and never cite one as justification
for a change. Anything an archived plan shipped that a reader still needs to
know is described in this file instead.

## Scope and limits

v0.1 supports **Windows x64 only**. Deliberately deferred, each behind a gate:
networking, persistence, terrain, pathfinding, morale, projectile ammunition,
store distribution, mod APIs, and non-Windows packaging.

Also deliberately absent: rigid-body physics — distance checks and hitscan are
the model — and any general-purpose ECS.

## License

No license file has been chosen yet, so default copyright applies and the code
is not yet licensed for reuse. Two vendored typefaces, **Rajdhani SemiBold** and
**Bebas Neue Regular**, are under the SIL Open Font License 1.1; their license
texts and provenance ship in
[`src/Hukbo.Client/Content/Fonts/`](src/Hukbo.Client/Content/Fonts/).
