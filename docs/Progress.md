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

- **Milestone:** M0 Reset (ends Nov 24)
- **Current task:** M1-2 (Queue #1 below).
- **Status:** not started
- **Owner:** Claude
- **Last updated:** 2026-11-22 by Claude

## Handoff notes

- 2026-11-22 Claude: user confirmed the M1-1 scene check. Saving the scene did not rewrite the `soldierSpawner` key (Unity only reserializes dirty objects), so I renamed it to `unitSpawner` in the YAML by hand; `FormerlySerializedAs` stays for safety. Final run: **EditMode 38/38, PlayMode 16/16**; `git diff --check` clean. M1-1 moved to the Done log, committed and pushed. Next: M1-2.

- 2026-11-22 Claude: `BagProductionController.soldierSpawner` → `unitSpawner` with `[FormerlySerializedAs("soldierSpawner")]`; the `GameScene` YAML key is left as `soldierSpawner` on purpose (the scene test passing proves the mapping; Unity rewrites it on the next scene save). `UnitSpawner.Spawn` on a wrong-team or default loadout throws `ArgumentException` (not silent, Unity logs it as an error), so no extra log added. M1-3 now lists `UnitSpawner.RecycleAll`; Design §3.4 row says recycle all: M1-3. Filtered run: EditMode `BagProductionControllerTests`+`UnitSpawnerTests` 7/7, PlayMode `GameSceneSoldierSpawnerTests` 1/1; full suites last ran before this rename (38/38, 16/16). Waiting for the user's scene check.

- 2026-11-22 Claude, M1-1 done in code (not committed): `SoldierSpawner` → `UnitSpawner` via `git mv` (script GUID kept, so `GameScene` refs resolve); also `SoldierSpawnerTests` → `UnitSpawnerTests`. `UnitSpawner` has a serialized `team` (also the first `Initialize` param) and `Spawn(CombatUnitLoadout)`, which throws on another team or a default loadout and changes nothing; `BagProductionController` passes `request.ToLoadout()` (its serialized field is still named `soldierSpawner`, kept to avoid breaking the scene ref). `TestEnemySpawner` now only picks the enemy loadout and spawns it through an enemy `UnitSpawner` (team 1, prewarm 1, band −1.2…1.6, anchor `Enemy Spawn Point`) added to the `Test Enemy Spawner` GameObject in `GameScene`; its own prefab/anchor/band fields are gone (the band Gizmo comes from that `UnitSpawner`). Tests: new `Spawn_RejectsAnotherTeamsLoadout_AndChangesNothing` (EditMode); the `GameScene` test checks both spawner teams and that the enemy is in the enemy spawner's active set. Run 2026-11-22: **EditMode 38/38, PlayMode 16/16**; `git diff --check` clean; `GameScene` is the only build scene. Not built: "recycle all" (no caller yet). User check: enemy still appears at `Enemy Spawn Point` and fights; band Gizmo visible on both spawners.

- 2026-11-22 Claude: started M1-1. Plan: `git mv` SoldierSpawner → UnitSpawner (GUID kept, so `GameScene` refs survive); serialized `team` + team check in `Spawn(CombatUnitLoadout)` (callers pass `request.ToLoadout()`); `TestEnemySpawner` keeps choosing the enemy loadout but spawns through a new enemy `UnitSpawner` on its GameObject (prefab, anchor, band move there); rename tests to match.

- 2026-11-22 Claude: user confirmed the 3A.3 scene check; 3A.3 moved to the Done log. Known trade-off accepted: the waiting rule chains through ranks, so later units wait behind the crowd; queued 3A.5 (attack slots and retarget) after M1-2, and M1-2 now makes "Waiting" a proper unit state. `CLAUDE.md` removed from `.git/info/exclude` and committed with 3A.4 (amended, then pushed).

- 2026-11-22 Claude: user confirmed the 3A.4 scene check (jitter and opponent overlap fixed; the waiting trade-off — a thinner ring in large mixed fights — is accepted). 3A.4 moved to the Done log and committed; not pushed. Next: M1-1.

- 2026-11-22 Claude, 3A.4 finished in code (not committed): the diagnostic log showed the outer units, which can't reach the target, sliding back and forth forever (≈0.1 u/s) and bumping the engaged ring. Fix in `CombatUnit`: a **waiting** state. A unit touching a resting (engaged or waiting) teammate that is closer to its own target, with a slide under 0.5 × speed, stops steering and stays stopped while that contact lasts (`IsResting`, `IsCloserToTarget`, `WaitSlideShareOfSpeed`). Resting units use the overlap-only teammate threshold. Waiting is reset wherever `engaged` is. New test `OneVersusEightMeleeCrowd_WaitingUnitsAdvanceOnceTheFrontRowLeaves`. The jitter test failed before the fix (4 flips) and passes after it with 0 flips. All `JITTERDBG` logs removed. Run 2026-11-22: **EditMode 37/37, PlayMode 16/16**; `git diff --check` clean. Design §3.7 updated. `CLAUDE.md` gained the `-testFilter` command; that file is in `.git/info/exclude`, so git does not track it. Not done: the user's `GameScene` check, commit.

- 2026-11-22 Claude (resumed 3A.4 after an interrupted session; nothing committed): uncommitted work in `CombatUnit.cs` + `CombatUnitMovementTests.cs`. Done in code: (1) hysteresis — `engaged` flag, engage at gap ≤ range, release at gap > range + `engagementMargin` 0.05, reset on target change/clear/Initialize/ResetForPool, exposed as `IsEngaged`; (2) opponents in the separation query (circle = body extent + `separationRadius`), push away only on true overlap past `separationDeadZone` 0.02 with a steep ramp, capped at full move speed; plus a per-step guard `WithoutPushIntoOpponents` that limits approach along `ColliderDistance2D.normal` (oriented toward the opponent) to half the remaining gap; (3) velocity smoothing — `MoveTowards` at `MovementSpeed × velocityResponse` (10)/s; (4) dead zone on all separation thresholds; (5) engaged units push teammates only on true overlap (threshold 0, not `separationGap`); moving units drop the step component into touching teammates (`crowdBlockDirection`). Tests added: `OneVersusEightMeleeCrowd_SettlesWithoutJitter` (≤3 direction flips per unit over 3 s after 3 s settle), `OneVersusEightMeleeCrowd_OpponentsNeverOverlapBeyondTheDeadZone`, a TearDown that destroys leftover units; the advance test waits 10 steps for the ramp. A `JITTERDBG` Debug.Log is still in the jitter test (to remove). Last batch run hung (Unity at 0.3 MB / 0% CPU, likely licensing); no test result for this code yet.
- 2026-11-22 Claude, later: removed the old JITTERDBG log; Design §3.7 now describes the 3A.4 rules; CLAUDE.md "Running tests" has the `-testFilter` command and the stray-Unity/3-minute-stall rule. Filtered run (`CombatUnitMovementTests`, PlayMode) worked, licensing was fine: **8/9 passed**, `OneVersusEightMeleeCrowd_SettlesWithoutJitter` failed (unit #2: 4 direction flips, limit 3). All other new tests passed, including opponent non-overlap. Added a **temporary** JITTERDBG log at the flip counter to find the cause; it has not run yet because the user then opened the project in the Editor (batch mode is blocked). Next: run the jitter test alone, read the JITTERDBG lines, fix the root cause, remove the log, then run the full EditMode + PlayMode suites. Full suites not run.

- 2026-11-22 Claude: 3A.3 implemented and committed + pushed at the user's request (commit "Task 3A.3: …"); scene check not yet confirmed, so it stays out of the Done log. Queued 3A.4 (crowd stability) as next; hysteresis removed from M1-2. `CombatUnit`: `EdgeGapTo` = `Physics2D.Distance` (HalfWidth removed); separation is 2D, gap-based (`separationGap` 0.05), recomputed every `separationInterval` 0.1 s, capped at 0.5 × move speed; `Initialize`/`ResetForPool` also set `body.position` and clear the separation cache. Band Gizmos via `CombatUnit.DrawBandGizmo` from both spawners. Sorting layers Background/Battlefield/Effects/Bag/UI in TagManager (Default stays first); Renderer2D sort mode CustomAxis (0,1,0); `SortingGroup` (Battlefield) on the prefab root; tent → Battlefield, battlefield image → Background; `BagBoardView` renderers → Bag. Tests: new `DepthSortingTests` (EditMode, real assets), crowd-of-20 and tall-collider gap tests (PlayMode); melee-touch test moved its target to y 0.8 (capsule ends); GameScene test now checks layers and "not steering" instead of `velocity.x == 0`, since later crowbars push the first one. Run 2026-11-22: EditMode 37/37, PlayMode 13/13 (the only later edit: stripped the trailing space Unity wrote on the prefab's empty `m_Name`); `git diff --check` clean; `GameScene` is the only build scene.
- 2026-11-22 Claude: queued 3A.3 (2D separation, band Gizmos, depth sorting) ahead of M1-1; M1-2 gained knockback/area-hit fields and attack-range hysteresis. Docs only, not committed.
- 2026-11-22 Claude: 3A.2 closed after the user's scene check passed. Design §3.4 `CombatUnit` row and §3.7 status updated. Last test run (EditMode 35/35, PlayMode 11/11) was **before** the user moved `Enemy Spawn Point` to y 1.112; rerun PlayMode at the start of 3A.3.
- 2026-11-22 Claude: 3A.1 closed after the user's scene check, which passed after the user's own config tuning (crowbar 2 s / range 0.15, rifle range 2). Design §1.9 now matches the configs, and §3.7 holds the new movement rule.
- `GameSceneSoldierSpawnerTests` now waits for the first crowbar and the first rifle unit instead of fixed seconds, so retuning durations no longer breaks it.
- Final 3A.1 run, after the user's tuning and the test rewrite (2026-11-22): EditMode 35/35 passed, PlayMode 7/7 passed; `git diff --check` clean.

## Found, not fixed

- `CrowbarLevel2` is still 3.4 s / range 0.7, but L1 is now 2 s / 0.15. L2 is slower and longer-ranged than L1, which breaks the 1.9 level rule (×0.85 → 1.7 s). Re-derive it when L2 matters (M3 content).
- Separation can nudge a stopped unit just out of range (seen in the scene test: gap 0.152 vs range 0.15), so it steps back in on the next fixed step. Addressed by 3A.4 hysteresis (margin 0.05); recheck it in the 3A.4 scene check.
- The soldier art exists twice: the separate `Assets/Game/Art/Soldier_Sprites/PNG/` files and `soldier_atlas_512.png`. The configs only use `PNG/`; decide whether to delete the atlas or switch to it (e.g. before the M3 content pass).

## Queue

1. M1-2 — `Health` + unit state machine (Idle, Walk, MeleeAttack, RangedAttack, Death); direct damage; white hit flash; weapon knockback and area-hit fields (with `Validate()` ranges and tests). "Waiting" (3A.4) becomes a proper unit state, not a flag. (Attack-range hysteresis moved to 3A.4.)
2. **3A.5 — attack slots and retarget** (rule: `docs/Design.md` §3.7)
   - **Scope:** 6–8 melee slots on a ring around each target; a melee unit claims the nearest free slot and steers to that point; no free slot → retarget to the nearest enemy that has one; wait only if no enemy has a free slot. Ranged units never take slots. Slots are released on death, retarget and pool release (release-once).
   - **Why:** the 3A.4 waiting rule chains through ranks, so later units wait behind the crowd instead of moving to another target (accepted trade-off for now).
   - **Acceptance:** evaluate after M1-2 with real kills; close 3A.5 without building it if crowds already resolve.
3. M1-3 — death → release at most once; `UnitSpawner.RecycleAll` (wave cleanup: release every active unit once, safe when a death release lands in the same frame); `PlayerBase` + contact damage; WebGL portrait smoke build.

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
