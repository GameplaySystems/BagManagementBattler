# BagManagementBattler — Design

Single source of truth for what the game is and how it is built.
Supersedes `docs/Architecture.md` and the private GPT game/technical design notes.
When code and this document disagree, fix one of them in the same change.

Status legend used below: **[done]** exists in the repo and is tested, **[next]** the current milestone, **[planned]** later milestone.

---

## Part 1 — Game design

### 1.1 Pitch

A portrait 2D bag-management auto-battler. Between waves you buy soldier and weapon items and arrange them in a small bag. During battle, every weapon that touches soldier items fills up and, on each completed fill, sends one armed soldier per touching soldier item out of your tent. You never control units; your bag *is* your strategy.

**The test sentence for every feature:**
> "I put this rifle beside two soldiers. It fills up, and two rifle soldiers leave my tent every time it completes. The next wave beat me, so I need another connection, a merge, a better weapon, or more space."

If a feature does not make that sentence clearer or more interesting, it is out.

### 1.2 Run structure

- A **run** is 10 waves. Waves 5 and 10 are boss waves. Clearing wave 10 wins the run.
- The player has **3 lives** per run.
- **Loss** (base HP reaches 0): lose 1 life, keep bag, container and gold, receive a small consolation reward, retry the same wave from preparation.
- **0 lives**: run over → Run End screen → new run from wave 1.
- **Win** a wave: wave reward, base HP restored to full, reroll cost resets, advance to the next wave's preparation.
- Target run length: 20–30 minutes for a competent player.

### 1.3 Phases

**Preparation** (bag unlocked, camera frames bag + shop)
- Buy offers from the shop row, reroll offers, buy bag expansions.
- Drag items between shop purchases → container → bag. Move, merge, replace.
- Press **Battle**.

**Battle** (bag locked, camera frames tent → road → enemy side; bag stays visible and shows fills)
1. Input locks; duplicate starts are ignored.
2. Every loose container item sells for its sell value; the container clears.
3. Weapon–soldier adjacency resolves once from the final bag.
4. Camera transitions; on completion, production timers start at zero and the wave schedule starts.
5. Battle ends once: **win** when every scheduled enemy has spawned and none remain alive while the base lives; **loss** when base HP hits 0.

**Result** — shows the outcome and why (see 1.10), then returns to Preparation or Run End.

### 1.4 The bag

- Grid max size **7 columns × 6 rows** (rectangular, tunable in `BagGridDefinition`). Starting unlocked area **5 × 4** (20 cells, the current `DefaultBagGrid`), bottom-left anchored.
- Items have fixed footprints and never rotate.
- Expansions unlock a piece of locked cells that shares an edge with unlocked cells. Piece shapes: **1×1, 1×2, 2×1, 1×3, 3×1**. Each preparation offers one expansion piece; price scales with cell count and rises with each purchase.
- The **container** is the unlimited scrollable list under the bag. It is safe storage during preparation; it is never wiped by rerolls. It is sold off at battle start (that is the pressure to commit).

### 1.5 Connection rule

- A weapon is **active** only if it shares at least one orthogonal edge with a soldier item. Diagonals never count.
- Each unique touching soldier item counts once, however many edges it shares.
- On every completed fill, an active weapon produces **one unit per unique touching soldier item**.
- Several weapons may touch the same soldier item; each produces for it independently.
- Each weapon has one timer regardless of how many soldiers it touches.

### 1.6 Move, merge, replace

- Drop on empty valid cells → place.
- Drop on an item with the same type and level that has a next level → **merge**; the target upgrades, the dragged item is consumed.
- Drop on one different item (or a max-level duplicate) → **replace**; the displaced item goes to the end of the container. Not a swap.
- Drop overlapping two or more items, locked cells or out of bounds → reject and restore.
- The full result is validated before anything changes.

### 1.7 Merge balance rule (tunable guideline)

A level N+1 item should produce **about 1.6–1.8×** the combat output of a level N item, never 2× or more. All values are per-level config, so the ratio is tuned in playtests, not hardcoded. Two separate level-1 weapons can touch more soldiers than one level-2 weapon, so "merge now or keep two production lines" stays a real decision. Every balance pass checks this ratio.

### 1.8 Content (launch)

**Soldier item** — 1 type ("Recruit"), levels 1–3, footprint 1×2. Level raises unit HP (×1.7 per level) and changes head art (plain → bandana → cap, blue).

**Weapons** — 4 families × 3 levels. Art tiers already exist: black (L1), blue (L2), gold (L3).

| Family | Style | Footprint | Notes |
|---|---|---|---|
| Knife | melee | 1×1 | cheap, fast production, low damage, fast walk |
| Crowbar | melee | 1×1 | slower production, higher damage |
| Pistol | ranged | 1×1 | short range, cheap |
| Rifle | ranged | 2×1 | long range, slow production, slower walk |

Post-MVP, in order (art already exists for the first three): SMG, shotgun, sniper, then throwables (grenade). If M3 finishes early, SMG/shotgun/sniper may move into the MVP.

**Enemies** — red body, built from the same unit pieces.

Variety comes from head, weapon and stats, not scale. Bosses are scaled-up default enemies for the MVP; unique boss visuals are post-MVP.

| Enemy | Look | Role |
|---|---|---|
| Grunt | plain head + knife/crowbar | melee fodder |
| Gunner | bandana head + pistol/rifle | ranged pressure |
| Brute | cap head + gold crowbar, high HP | tanky melee |
| Boss 1 (wave 5) | default enemy at 2× scale, gold crowbar | HP-sponge test with a big health bar |
| Boss 2 (wave 10) | default enemy at 2× scale, gold rifle, Grunt escorts | sustained-output test |

### 1.9 Starting numbers (tuning baseline, all in config assets)

| Value | Start |
|---|---|
| Starting gold | 10 |
| Starting items | 1 Recruit L1 + 1 Crowbar L1 in the container |
| Shop offers per row | 3 |
| Offer price L1 / L2 | 3 / 7 (L2 offers appear from wave 4) |
| Reroll costs per wave | 1, 2, 3, 4, then 5 forever |
| Sell value | 50% of buy price, rounded down |
| Expansion prices | 4, 6, 8, 10, … (+2 each) |
| Wave reward | 8 + wave number; boss waves +5 |
| Loss consolation | 4 |
| Base HP | 20; enemy contact deals 1 (boss 5), then the enemy is recycled |
| Level scaling | weapon damage ×1.4, production duration ×0.85 per level (≈1.65× output) |

| Weapon L1 | Damage | Attack cooldown | Range | Production | Speed × |
|---|---|---|---|---|---|
| Knife | 3 | 0.6 s | 0.6 | 3.0 s | 1.2 |
| Crowbar | 6 | 1.0 s | 0.15 | 2.0 s | 1.0 |
| Pistol | 3 | 0.8 s | 3.5 | 4.0 s | 1.0 |
| Rifle | 4 | 0.6 s | 2.0 | 6.0 s | 0.85 |

Range is edge to edge (see 3.7). Crowbar and Rifle range/production/speed are the tuned config values after the 3A.1 scene check (field units at 0.5 scale, collider 0.21 × 0.4). Damage and cooldown are not in config yet (M1). Knife and Pistol are not authored yet.

### 1.10 Waves and teaching

Waves teach the game; there is no separate tutorial system, only short hint banners on waves 1–4.

| Wave | Composition | Teaches / hint |
|---|---|---|
| 1 | 4 Grunts, slow | "Drag the soldier and the crowbar into the bag so they touch." |
| 2 | 6 Grunts | "Weapons fill while touching soldiers. Full = a soldier leaves the tent." |
| 3 | Grunts + 2 Gunners | "One weapon touching two soldiers makes two units." |
| 4 | mixed, two sub-waves | "Drop an item on an identical one to merge it." (shop guarantees a duplicate) |
| 5 | **Boss 1** + Grunts | sustained output |
| 6–9 | mixed, Brutes appear, shorter gaps | expansions, rerolls, layouts |
| 10 | **Boss 2** + escorts | final test |

**Result screen** shows: outcome, units produced per weapon, enemies defeated, base damage taken, and gold earned. That is how the player sees why they lost.

### 1.11 Screens

Main menu (Play, Settings, Credits, Quit on desktop) · Preparation HUD (gold, lives, wave, Battle button, shop row, reroll, expansion) · Battle HUD (base HP, wave progress, boss bar) · Pause (resume, settings, restart run, main menu) · Settings (music volume, SFX volume, fullscreen toggle) · Result · Run End (win/lose, waves cleared, best wave) · Credits (all asset licenses and attributions).

### 1.12 Feel and audio (minimum)

- Unit hit: **white flash on every hit** (default). Knockback/recoil only where a weapon config enables it (planned for shotgun hits and sniper shots, post-MVP). Death: squash, fade, small puff.
- Weapon fill: fill sweep inside the item background; scale punch on completion; tent scale punch on each spawn.
- Merge: scale punch + sparkle. Invalid drop: red tint + snap back.
- Base hit: camera shake (small) + base flash.
- Audio: menu and battle music loops; SFX for pick up, drop, merge, invalid, buy, reroll, spawn, melee hit, gunshot, death, base hit, win, lose, button click.

### 1.13 Platform and persistence

- **WebGL**, portrait, 540×960 itch embed, mouse and single touch. Desktop build optional.
- **PlayerPrefs only**: music volume, SFX volume, best wave reached. No mid-run save.
- itch.io AI disclosure: graphics (ChatGPT) and code (Claude/Codex) are AI-assisted; tag honestly.

### 1.14 Not in v1

**Never:** manual unit control, multiplayer, meta-progression, item rotation, pathfinding.
**Later (post-MVP):** mid-run save, SMG/shotgun/sniper, throwables, unique boss visuals, weapon-specific knockback.

---

## Part 2 — Asset list

| Asset | Status | Source / action |
|---|---|---|
| Weapon icons, 7 families × 3 tiers | **have** | `Cartoon Weapon Sprite Sheet.png` (only 4 families used at launch) |
| Unit bodies blue/red, 3 heads × 2 colors | **have** | `Soldier_Sprites` |
| Battlefield road background | **have** | `Cartoon battlefield with yellow dirt road.png` |
| Blue tent (player base) | **have** | `Friendly blue base tent sprite-1.png` |
| Bag cell + item backgrounds 1×1, 1×2, 2×1 | **have** | `Bag_UI_Sprites` |
| Locked cell, expansion-piece highlight | need | tint of existing cell sprite (no new art) |
| Enemy spawn side marker (optional) | need | skip unless the field reads badly |
| Coin, heart (lives), base-HP, wave icons | need | Kenney CC0 UI/game icons, recolored to match outlines |
| Panels, buttons, banner frames | need | Kenney CC0 UI pack, or generate in the existing style |
| Font | need | one rounded bold OFL font (e.g. from Google Fonts) |
| Particles: puff, sparkle, hit spark, coin pop | need | Kenney CC0 particle pack |
| Music (menu, battle) | need | CC0 loops (e.g. OpenGameArt, Kenney music jingles) |
| SFX (list in 1.12) | need | Kenney CC0 audio packs |
| itch cover image, screenshots, GIF | need | captured from the game in M5 |

Every external asset gets a row in `docs/Credits.md` (name, author, license, URL) when it is imported, not at the end.

---

## Part 3 — Technical design

### 3.1 Principles

1. **Rules in plain C#, presentation in MonoBehaviours.** `BagGrid`, `BagManager`, `ItemContainer`, `BagProductionSession`, `Health`, `Wallet`, `Shop`, `RunState` have no Unity scene dependencies and are unit-tested in EditMode. Views observe them and submit requests; views never mutate rule state.
2. **ScriptableObjects are read-only config.** Every level is its own definition asset. No stat formulas in code; runtime instances own mutable state.
3. **Explicit references, no globals.** No singletons, service locators, `FindObjectOfType`, or global event bus. One scene composition root wires references in the inspector.
4. **Direct calls for ordered gameplay steps; C# events only for independent observers** (UI, audio, feedback).
5. **Transactions validate fully, then commit.** A rejected operation changes nothing.
6. **Pools release a unit at most once** and fully reset it.
7. **Each owner has one job.** If a class needs "and" to describe it, split it.

### 3.2 Patterns used and where

| Pattern | Where | Why |
|---|---|---|
| Flyweight via ScriptableObjects | `BagItemDefinition`, `UnitDefinition`, wave/shop/run definitions | shared immutable data, many instances |
| Humble Object / model–view split | `BagGrid`+`BagManager` vs `BagBoardView`/`ItemContainerView` | logic testable without a scene |
| Memento-style snapshot + rollback | `BagManager.DragSnapshot` | exact restore on cancel/invalid drop |
| State pattern | unit states (Idle/Walk/MeleeAttack/RangedAttack/Death); game phases in `GameFlowController` | explicit transitions, no flag soup |
| Object Pool | `UnitSpawner` | no per-spawn allocation in battle |
| Value-object request | `ProductionRequest` | production decoupled from spawning; no live instance references |
| Observer (C# events) | `Wallet.Changed`, `Health.Depleted`, `RunState.LivesChanged` → views/audio | UI reacts without gameplay knowing UI exists |
| Composition root | `GameSceneBootstrap` (grows from `BagInteractionBootstrap`) | one place that builds and wires plain C# owners |

### 3.3 Dependency direction

```
Definitions (ScriptableObjects)
        ▲
Rules (plain C#): BagGrid, BagManager, ItemContainer, BagProductionSession,
                  Health, Wallet, Shop, RunState, BattleStats
        ▲
Controllers (MonoBehaviours): BagProductionController, UnitSpawner, WaveRunner,
                  PlayerBase, CombatUnit, GameFlowController, CameraDirector
        ▲
Views / feedback / audio: BagBoardView, ItemContainerView, ShopView, HudView,
                  ResultView, UnitFeedback, AudioPlayer
```
Arrows mean "depends on". Nothing depends upward. `GameFlowController` is the only class that calls across controllers in order.

### 3.4 Script map

Namespaces: `BagManagementBattler.Bag`, `.Combat`, `.Flow`, `.Economy`, `.UI`, `.Presentation`. One runtime assembly.

**Bag** (`Scripts/Bag/`)

| Script | Kind | Responsibility | Status |
|---|---|---|---|
| `BagGridDefinition` | SO | max size, initially unlocked cells | done |
| `BagGrid` | C# | occupancy, footprint validation, failure reason, unlock cells | done (unlock: planned) |
| `PlacementFailureReason` | enum | OutOfBounds / Locked / Occupied | done |
| `BagItemDefinition` | SO | type id, level, next level, role, footprint, visuals, weapon stats, hand pose | done (damage/cooldown/range/melee flag/price: next) |
| `BagItemInstance` | C# | instance id, current definition, location | done |
| `ItemContainer` | C# | ordered loose items, insert-at-index rollback | done |
| `BagManager` | C# | drag transactions, merge, replace, rollback, lock | done |
| `BagDragController` | MB | pointer/touch → drag requests | done |
| `BagBoardView`, `ItemContainerView`, `ContainerSlotView` | MB | rendering, ghost, fills | done |
| `BagProductionSession` | C# | adjacency resolve, timers, emits requests | done |
| `ProductionRequest`, `WeaponProductionProgress` | value | request snapshot / read-only progress | done |
| `BagProductionProgressView` | MB | fill display | done |
| `BagInteractionBootstrap` | MB | current composition root | done → becomes `GameSceneBootstrap` |

**Combat** (`Scripts/Combat/`)

| Script | Kind | Responsibility | Status |
|---|---|---|---|
| `UnitDefinition`, `UnitCatalog` | SO | per-team/archetype/level body stats and visuals | done (HP: next) |
| `CombatUnitLoadout` | value | unit def + weapon def → final stats | done |
| `CombatUnit` | MB | one unit: initialize, own state machine, movement, targeting; mirrors its visual root for the enemy team; movement per 3.7 | movement and targeting done (3A.2); states/attacks: M1-2 |
| `UnitVisualLayout` | static | visual sizing helper | done |
| `BagProductionController` | MB | session lifecycle, ticking, forwards requests | done |
| `SoldierSpawner` → **`UnitSpawner`** | MB | pool, acquire/init/release, track active, recycle all; one instance per team | rename + generalize: next |
| `TestEnemySpawner` | MB | debug fixture | delete when `WaveRunner` lands |
| `Health` | C# | current/max, damage, `Depleted` event | next |
| `UnitStateMachine`, `IUnitState` + 5 states | C# | Idle, Walk, MeleeAttack, RangedAttack, Death | next |
| `PlayerBase` | MB | base `Health`, enemy contact | next |
| `WaveDefinition`, `WaveSpawnEntry` | SO / data | start time, enemy unit def, weapon def, count, interval, boss flag | planned |
| `WaveRunner` | MB | runs one wave schedule through the enemy `UnitSpawner`, reports schedule complete | planned |

**Flow** (`Scripts/Flow/`)

| Script | Kind | Responsibility | Status |
|---|---|---|---|
| `RunDefinition` | SO | ordered waves, lives, starting gold, starting items | planned |
| `RunState` | C# | wave index, lives, best wave; events | planned |
| `GameFlowController` | MB | phase machine (Menu, Preparation, ToBattle, Battle, Result, RunEnd); ordered battle start/end; outcome resolved once | planned |
| `BattleStats` | C# | per-battle counters for the result screen | planned |
| `CameraDirector` | MB | DOTween position + ortho size for prep/battle | planned |
| `GameSceneBootstrap` | MB | builds plain C# owners, wires references | planned |

**Economy** (`Scripts/Economy/`)

| Script | Kind | Responsibility | Status |
|---|---|---|---|
| `ShopDefinition` | SO | offer pools per wave band + weights, prices, reroll cost list, sell ratio, expansion prices | planned |
| `Wallet` | C# | balance, CanAfford, Spend, Grant, `Changed` | planned |
| `Shop` | C# | current offers, Buy (→ container), Reroll; never touches the container otherwise | planned |
| `BagExpansionShop` | C# | expansion pieces + price; calls `BagGrid` unlock through `BagManager` | planned |

**UI / Presentation / Persistence**

| Script | Responsibility | Status |
|---|---|---|
| `ShopView`, `HudView`, `ResultView`, `RunEndView`, `PauseView`, `SettingsView`, `MainMenuView`, `CreditsView`, `HintBannerView` | screens in 1.11 | planned |
| `UnitFeedback` | hit flash (MaterialPropertyBlock), DOTween knockback/death; kills tweens on release | planned |
| `AudioPlayer` + `SoundLibrary` (SO) | plays named cues; referenced explicitly | planned |
| `SettingsStore`, `ProgressStore` | PlayerPrefs read/write | planned |

### 3.5 Key flows

**Battle start** (`GameFlowController`): lock `BagManager` → `Shop` closes → sell container via `Wallet` → `BagProductionController.BeginProduction(grid)` prepared → `CameraDirector.ToBattle()` → on complete: start production ticking + `WaveRunner.Begin(wave)`.

**Production → unit**: `BagProductionSession.Tick` → `ProductionRequest` → `BagProductionController` → friendly `UnitSpawner.Spawn(loadout)` → `CombatUnit.Initialize`.

**Unit death**: `Health.Depleted` → state machine enters `Death` → feedback plays → `CombatUnit` reports finished → its `UnitSpawner.Release` (at most once).

**Battle end**: outcome check (schedule complete ∧ no active enemies ∧ base alive → win; base HP 0 → loss), resolved once → stop production + wave → both spawners `RecycleAll` → reward/life change → `ResultView` → Preparation or RunEnd.

### 3.6 Tests

EditMode for every rule owner and every config `Validate()`; at least one test per system against the real `Assets/Game/Configs` assets. PlayMode for scene wiring, unit movement/targeting/combat, pooling reuse, and one full scripted wave win and loss. No tests for pure visuals.

### 3.7 Combat movement rule

Owned by `CombatUnit` and applied through its kinematic `Rigidbody2D` velocity every fixed step.

- **Range** is a 2D edge-to-edge distance: centre distance minus both units' collider radii (half the collider width). A unit is in range when that distance ≤ its weapon's engagement range. Melee ranges are therefore near zero and mean "touching".
- **With a target:** the unit steers straight toward the target on both axes (normalised direction × movement speed) until in range, then stops.
- **Without a target:** the unit advances along X only (friendly +X, enemy −X).
- **Battlefield band:** Y is clamped to the battlefield's min/max Y every step (spawn Y included); no unit ever leaves the band. The band is serialized on each spawner (`battlefieldMinY`/`battlefieldMaxY`, set in `GameScene`) and passed to `CombatUnit.Initialize`.
- **Same-team separation** stays: a small push between nearby units of the same team, added on top of the move.
- **No pathfinding** (see 1.14): no obstacle avoidance or routing, just straight steering plus separation.

Status: implemented in Task 3A.2; scene check passed 2026-11-22.

---

## Part 4 — Working process

- **Ownership gate:** no change merges until the owner (you) can explain every new script: its job, its collaborators, and why it lives in its layer. If not, it gets rewritten or explained before commit.
- **Per task:** read the relevant section here → scoped implementation → narrowest tests → full EditMode + PlayMode → `git diff --check` → manual scene check → commit.
- Codex and Claude both take tasks from Part 5; whoever implements writes the test and updates the status column above.

## Part 5 — Milestones (hard dates)

| Milestone | Dates | Exit criteria |
|---|---|---|
| **M0 Reset** | Nov 22–24 | Task 3A.1 fixes (see 5.1) pass manual check; local work committed and pushed; this doc adopted; GPT working docs retired |
| **M1 Combat** | Nov 24–28 | `UnitSpawner` rename; Health, states, melee/ranged direct damage, death, recycle-once; `PlayerBase` + contact damage; WebGL portrait smoke build runs in a browser |
| **M2 Loop** | Nov 29 – Dec 4 | `WaveRunner` + definitions; `GameFlowController` phases, locks, camera, result; `Wallet`, `Shop`, reroll, sell-on-battle; expansion; lives and run end. **Ugly but complete 10-wave run.** |
| **M3 Content** | Dec 5–10 | 4 weapons × 3 levels, Recruit × 3, 3 enemies, 2 bosses, 10 waves authored; first balance pass against 1.7 and 1.9 |
| **M4 Shell** | Dec 11–15 | all screens in 1.11, hint banners, PlayerPrefs, audio in, credits file complete |
| **M5 Ship** | Dec 16–20 | feedback/juice from 1.12; 3–5 outside playtesters; bug fixes; itch page with GIF, screenshots and AI disclosure; README with play link. **Publish Dec 20.** |

Scope rule: if a milestone slips by more than one day, cut from M3 content or 1.12 juice — never extend the dates.

### 5.1 Task 3A.1 — fixes found in the 3A scene check

| Symptom | Cause (verified in code/config) | Fix |
|---|---|---|
| Friendly units look like they hold two weapons | Not a production bug. Rifle and crowbar both have `productionDurationSeconds: 1` and `movementSpeedMultiplier: 1`, so both units spawn on the same frame at the same tent point and move at the same speed: two units, perfectly stacked. Units have no separation. | Set real durations from 1.9; add a small per-spawn vertical lane offset at the tent; add simple same-team separation in movement. Add a test: two simultaneous requests → two distinct units, not overlapping after N fixed steps. |
| Units stop face to face | Engagement ranges are placeholders (rifle 1.2, crowbar 0.6) and are measured centre-to-centre on X, so body width eats most of the range. | Measure range between collider edges (or subtract both half-widths); set ranges from 1.9 after the unit resize. |
| Units are huge | Field units reuse the bag's `visualCellSize` (0.65), so they are sized like bag items. | Give `CombatUnit` its own field scale on a `Visual` child root, independent of bag cell size; resize collider to match. |
| Enemy faces the wrong way | No mirroring exists anywhere. | Flip the `Visual` child root's X scale for `CombatTeam.Enemy` (includes `HandPoint` and weapon); keep collider/rigidbody on the unflipped root. Reset on pool release. |
| Bag soldier head/body sorting wrong | `BagBoardView.CreateItemVisualPart` gives every part `sortingOrder = 22`, so head and body tie. | Pass a sorting order per part (body 22, head 23). Container view is unaffected (UI sibling order). |

Status: all fixes done; scene check passed after tuning (2026-11-22). Follow-up Task 3A.2 (circular range and 2D chase, see 3.7) is done too.
