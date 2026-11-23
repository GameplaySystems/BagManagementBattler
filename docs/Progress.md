# Progress — shared handoff for Claude and Codex

**Every session starts here and ends here.** This file says *where we are*; `docs/Design.md` says *what and how*. Git is the ground truth for code: if this file and `git status` / `git log` disagree, trust git and fix this file.

## Rules for every agent

**Start of session**
1. Read this file top to bottom, then the `docs/Design.md` sections the current task names.
2. Run `git status` and `git log --oneline -5`. Uncommitted changes may be a previous agent's unfinished work — read the handoff notes before touching them. Never reset or discard them.
3. Set `Owner` below to yourself and `Status` to `in progress` before editing code.

**During the task**
- Stay inside the current task's scope. Anything else you notice goes under *Found, not fixed*.
- After each meaningful step, update *Handoff notes* (one line is enough). Usage can run out at any moment; the next agent must be able to continue from here.

**End of session (finished or not)**
1. Update *Current task* status and *Handoff notes*: what is done, what is not, exact test results (counts, which suite), files touched, anything half-done.
2. Never write "tests pass" unless they were run in this session; write "not run" instead.
3. When a task is fully done and the user has confirmed the scene check: move it to *Done log* with the commit hash, update status columns in `docs/Design.md` §3.4 in the same commit, and promote the next task from *Queue*.
4. Do not commit unless the user asked. If uncommitted, say so in the handoff notes.

---

## Now

- **Milestone:** M1 Combat (Nov 24–28)
- **Current task:** M1-2b (Queue #1 below).
- **Status:** not started
- **Owner:** —
- **Last updated:** 2026-11-23 by Claude

## Handoff notes

- 2026-11-23 Claude: M1-2 committed and pushed after the user's scene check (see Done log). Next is M1-2b. Open question before starting it: `CombatUnitCombatTests` names `MeleeAttackState`/`RangedAttackState` (3 asserts), so "one Attack state" and "tests unedited" conflict; raise it with the user first. Config values (speeds, rifle range 1) are testing values; Design §1.9 now says configs are authoritative until M3 balance, so don't sync them into the doc.

## Found, not fixed

- `CrowbarLevel2` is still 3.4 s / range 0.7, but L1 is now 2 s / 0.15. L2 is slower and longer-ranged than L1, which breaks the 1.9 level rule (×0.85 → 1.7 s). Re-derive it when L2 matters (M3 content).
- Separation can nudge a stopped unit just out of range (seen in the scene test: gap 0.152 vs range 0.15), so it steps back in on the next fixed step. Addressed by 3A.4 hysteresis (margin 0.05); recheck it in the 3A.4 scene check.
- The soldier art exists twice: the separate `Assets/Game/Art/Soldier_Sprites/PNG/` files and `soldier_atlas_512.png`. The configs only use `PNG/`; decide whether to delete the atlas or switch to it (e.g. before the M3 content pass).

## Queue

1. **M1-2b — real State pattern for unit states**
   - **Why:** M1-2's state classes are one-line forwarders to `CombatUnit.Enter*/Tick*`; transitions are also forced from outside (`RefreshTargetNow`, `RefreshTargetIfNeeded`), and the two attack states differ only by an enum.
   - **Scope:** each state class owns its own logic and transitions (Walk: advance/chase/wait trigger; Waiting: the 3A.4 rule; Attack: hit timing, range hysteresis, retarget; Death: release delay). One `AttackState` replaces Melee/Ranged and reads `AttackStyle` from the weapon. `CombatUnit` is the context: it exposes the data and helpers states need (target, gap, body, separation result, attack) and runs the machine; it keeps no per-state flags or `Enter*/Tick*` methods. Remove Idle, or give it a real purpose (e.g. the pooled/uninitialised state) and say which in the handoff.
   - **Acceptance:** behaviour unchanged; every existing EditMode and PlayMode test passes **without editing the tests**. (Tests that name `MeleeAttackState`/`RangedAttackState` need a compatibility decision first: raise it in this file before changing anything.)
2. **3A.5 — attack slots and retarget** (rule: `docs/Design.md` §3.7)
   - **Scope:** 6–8 melee slots on a ring around each target; a melee unit claims the nearest free slot and steers to that point; no free slot → retarget to the nearest enemy that has one; wait only if no enemy has a free slot. Ranged units never take slots. Slots are released on death, retarget and pool release (release-once).
   - **Why:** the 3A.4 waiting rule chains through ranks, so later units wait behind the crowd instead of moving to another target (accepted trade-off for now).
   - **Acceptance:** evaluate after M1-2 with real kills; close 3A.5 without building it if crowds already resolve.
3. M1-3 — death → release at most once; `UnitSpawner.RecycleAll` (wave cleanup: release every active unit once, safe when a death release lands in the same frame); `PlayerBase` + contact damage; WebGL portrait smoke build.
4. **Asset/polish backlog** (`docs/Design.md` §1.12 feel; not yet scheduled, cut first if a milestone slips)
   - Death poof: pooled smoke particle on the Effects layer, triggered from the Death state through `UnitFeedback` (M1-2 has a placeholder squash + fade over the 0.5 s death delay).

## Done log

| Task | Summary | Commit |
|---|---|---|
| 1A–1C | grid, drag/rollback, merge/replace | (in history) |
| 2A–2D | adjacency production, pooled spawning, fills, hand-anchored weapons | (in history) |
| 3A | shared `CombatUnit`, `UnitCatalog`, targeting and chase | `15c7028`, `2886553` |
| 3A.1 | field-scale `Visual` root, enemy mirroring, edge-to-edge X range, spawn lanes + separation, tuned rifle/crowbar configs, bag head sorting | commit "Task 3A.1: combat visual and movement fixes" |
| 3A.2 | 2D edge-to-edge range (`EdgeGapTo`), diagonal chase, battlefield Y band on both spawners (−1.2…1.6 in `GameScene`), enemy spawn moved to y 1.112 | `237cb29` |
| 3A.3 | collider-distance range, 2D throttled same-team separation, band Gizmos, sorting layers + `SortingGroup` + custom sort axis | `95e86fc` |
| 3A.4 | crowd stability: engagement hysteresis, velocity smoothing, separation dead zone, opponent non-overlap (push + per-step guard), waiting behind resting teammates | commit "Task 3A.4: crowd stability" |
| M1-1 | `SoldierSpawner` → `UnitSpawner` (GUID kept), one instance per team with a team check in `Spawn(CombatUnitLoadout)`, test enemy spawned through the enemy `UnitSpawner`, `BagProductionController.unitSpawner` (`FormerlySerializedAs`) | commit "M1-1: UnitSpawner per team" |
| M1-2 | `Health`, unit state machine (forwarder states, see M1-2b), direct melee/ranged damage, death + 0.5 s pooled release with squash/fade, knockback/area hit, `SpriteFlash` material + MPB hit flash, `TestEnemySpawner` count/interval/band spread | commit "M1-2: health, states, direct attacks, death, hit flash" |
