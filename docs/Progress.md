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

- **Milestone:** M2 Loop
- **Current task:** none selected; 3A.5 remains in Queue
- **Status:** ready for M2 task selection — M1 complete
- **Owner:** unassigned
- **Last updated:** 2026-11-23 by Codex

## Handoff notes

- 2026-11-23 Codex: User confirmed the rebuilt WebGL targeting behavior in the browser. This commit closes the targeting fix, M1-3 and M1 as `M1-3: fix target reacquisition and close M1`. No automated tests rerun in this closure turn because gameplay implementation did not change after the recorded verification; carried-forward results are full EditMode 46/46 and PlayMode 36/36, with the requested regression red 0/1 then green 1/1. Decision on 3A.5: keep queued because units can still wait in chains behind a saturated front line; bounded reacquisition does not implement attack slots or saturated-target retargeting. M2 is next, but its first task is not yet selected.
- 2026-11-23 Codex: Started the user-reported target reacquisition fix after pushing M1-3 step 4 as `84199db`. Root cause confirmed: `CombatUnit.RefreshTargetIfNeeded` returns while any current target remains valid, and `FindNearestTarget` hard-prioritizes units over the base. Added the requested base-to-unit switch regression before production changes; focused red result: 0/1 passed, expected the new friendly but the enemy still held the base. Implementing the narrow rule: while walking, rescan every 0.1 s and select the nearest opposing `ICombatTarget` within a serialized 3.5-unit acquisition radius; while attacking, retain the target until invalid or beyond engagement release range; no target means advance. This task must remain uncommitted for the user's browser check.
- 2026-11-23 Codex: Target reacquisition implementation complete. `CombatUnit` rescans only in `WalkState`, `AttackState` remains sticky, unit/base candidates share nearest-distance selection, and the code default + real prefab acquisition radius are 3.5. Updated Design §3.7 and added coverage for base→new-unit switching, nearest base selection, out-of-range non-acquisition, and attack-target retention. Results so far: requested regression red 0/1 then green 1/1; `PlayerBaseTests` 8/8; combined targeting + field + movement + combat + base run before the last retention test 33/33. Full suites pending; uncommitted by user request.
- 2026-11-23 Codex: Full EditMode passed 46/46. First full PlayMode run passed 35/36: `EnemyWithoutFriendlyUnit_TargetsAndSiegesBase` saw base HP 88 instead of 96 because the preceding `GameSceneSoldierSpawnerTests` left `GameScene` and its debug enemies active; periodic reacquisition exposed that existing fixture leak. Added `UnityTearDown` to switch to an empty scene and unload the scene test's live combat before later fixtures. Exact sequence and full PlayMode reruns pending.
- 2026-11-23 Codex: Target reacquisition fix is ready for the user's browser check and remains uncommitted. Final verification: scene-test→base-test sequence 9/9, full EditMode 46/46, full PlayMode 36/36, `git diff --check` clean. Fresh WebGL build completed with exit 0 / `Build Finished, Result: Success` in `Builds/WebGL`. Intended changes: `CombatUnit.cs`, real `CombatUnit.prefab`, `PlayerBaseTests.cs`, scene-test cleanup in `GameSceneSoldierSpawnerTests.cs`, Design §3.7, and this handoff. Reload `http://127.0.0.1:8000/` before checking so the browser uses the rebuilt WASM.
- 2026-11-23 Codex: User confirmed the M1-3 WebGL browser check. Closing M1-3 in commit `M1-3 step 4: WebGL portrait smoke build`, pushing it, and promoting 3A.5. Step 4 verification this session: Unity WebGL build exit 0 / `Build Finished, Result: Success`; served output returned HTTP 200; browser canvas 540×960, loading overlay hidden, active combat visible, 0 console warnings/errors. Unit suites not rerun for the Player Settings/editor-build-only step.
- 2026-11-23 Codex: M1-3 step 4 complete after Unity 6000.3.17f1's `Web` module was installed (`WebGLSupport` playback engine is present). Set Web canvas 540×960 and enabled decompression fallback; `GameScene` is the only enabled build scene. Added minimal `WebGLBuild.Build` for reproducible CLI builds because Unity 6000.3.17f1 rejects direct `-build` without a saved Build Profile. Final unrestricted incremental build: exit 0, `Build Finished, Result: Success`; output is under `Builds/WebGL`. Served with `uv run python -m http.server 8000 --directory Builds/WebGL`; browser smoke passed at 540×960 with the loading overlay hidden, active combat visibly running, and 0 console warnings/errors. Build/log artifacts are ignored; intended uncommitted files are `ProjectSettings/ProjectSettings.asset`, `Assets/Editor.meta`, `Assets/Editor/WebGLBuild.cs` + meta, and this handoff.
- 2026-11-23 Codex: M1-3 steps 1–3 complete under the revised siege design and included in the user-requested commit/push from `9d9168a`. Added idempotent `UnitSpawner.RecycleAll` plus same-frame death/cleanup coverage. Added minimal `ICombatTarget` implemented by `CombatUnit` and the 100-HP `PlayerBase`: enemies preserve nearest-friendly-unit priority, fall back to the base only with no friendly unit target, then use the same chase/range/`AttackState`/weapon damage/cooldown and remain active; friendly units cannot target their base. Added `PlayerBaseHealthBar` as a separate world-space presentation component. Updated Design §1.9, script map, movement/base rules and M1 exit wording. Final tests: focused UnitSpawner 6/6, release-once 1/1, PlayerBase 5/5, combined base + targeting + movement + combat 27/27, destroyed-target regression 1/1, scene wiring 1/1; full EditMode 46/46 and PlayMode 33/33 — all passed. `git diff --check` clean. Step 4 remains blocked because Unity 6000.3.17f1 has no `WebGLSupport` playback engine; Player Settings/build remain unattempted.
- 2026-11-23 Codex: M1-2b scene check passed with unchanged behavior; closing in commit `M1-2b: real unit state pattern` and promoting M1-3. Removed Idle, consolidated melee/ranged into one config-driven `AttackState`, and moved behavior/transitions/timers into the states. Only the 3 approved class-name assertions changed. Tests this session: focused combat 11/11, focused movement + targeting 11/11, full EditMode 45/45, full PlayMode 27/27 — all passed.
- 2026-11-23 Claude: M1-2 committed and pushed after the user's scene check (see Done log). Next is M1-2b. User decision 2026-11-23: the 3 asserts in `CombatUnitCombatTests` that name `MeleeAttackState`/`RangedAttackState` may be updated to the single `AttackState`; every other test stays unedited. Config values (speeds, rifle range 1) are testing values; Design §1.9 now says configs are authoritative until M3 balance, so don't sync them into the doc.

## Found, not fixed

- `CrowbarLevel2` is still 3.4 s / range 0.7, but L1 is now 2 s / 0.15. L2 is slower and longer-ranged than L1, which breaks the 1.9 level rule (×0.85 → 1.7 s). Re-derive it when L2 matters (M3 content).
- Separation can nudge a stopped unit just out of range (seen in the scene test: gap 0.152 vs range 0.15), so it steps back in on the next fixed step. Addressed by 3A.4 hysteresis (margin 0.05); recheck it in the 3A.4 scene check.
- The soldier art exists twice: the separate `Assets/Game/Art/Soldier_Sprites/PNG/` files and `soldier_atlas_512.png`. The configs only use `PNG/`; decide whether to delete the atlas or switch to it (e.g. before the M3 content pass).
- `CombatUnit` (~800 lines) should later split into `UnitTargeting`, `UnitMovement` and `UnitVisuals`, with `CombatUnit` coordinating. Do this after M2, when combat behavior is stable.

## Queue

1. **3A.5 — attack slots and retarget** (rule: `docs/Design.md` §3.7)
   - **Scope:** 6–8 melee slots on a ring around each target; a melee unit claims the nearest free slot and steers to that point; no free slot → retarget to the nearest enemy that has one; wait only if no enemy has a free slot. Ranged units never take slots. Slots are released on death, retarget and pool release (release-once).
   - **Why:** the 3A.4 waiting rule chains through ranks, so later units wait behind the crowd instead of moving to another target (accepted trade-off for now).
   - **Acceptance:** evaluated after M1 with real kills on 2026-11-23; keep queued because units still wait in chains behind a saturated front line.
2. **Asset/polish backlog** (`docs/Design.md` §1.12 feel; not yet scheduled, cut first if a milestone slips)
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
| M1-2b | real State pattern: Walk/Waiting/Attack/Death own behavior and transitions; one config-driven Attack state; Idle removed | commit "M1-2b: real unit state pattern" |
| M1-3 | release-once + `RecycleAll`; shared unit/base targeting and base health bar; bounded nearest-target reacquisition with sticky attack targets; portrait 540×960 WebGL build verified in browser | `349d09f`, `84199db`, commit "M1-3: fix target reacquisition and close M1" |
| M1 | Combat milestone complete through M1-3; automated suites, fresh WebGL build and browser acceptance passed | commit "M1-3: fix target reacquisition and close M1" |
