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
- **Current task:** 3A.3 (Queue #1 below)
- **Status:** committed and pushed at the user's request; user scene check not yet confirmed. Next: 3A.4 (Queue #2).
- **Owner:** Claude
- **Last updated:** 2026-11-22 by Claude

## Handoff notes

- 2026-11-22 Claude: 3A.3 implemented and committed + pushed at the user's request (commit "Task 3A.3: …"); scene check not yet confirmed, so it stays out of the Done log. Queued 3A.4 (crowd stability) as next; hysteresis removed from M1-2. `CombatUnit`: `EdgeGapTo` = `Physics2D.Distance` (HalfWidth removed); separation is 2D, gap-based (`separationGap` 0.05), recomputed every `separationInterval` 0.1 s, capped at 0.5 × move speed; `Initialize`/`ResetForPool` also set `body.position` and clear the separation cache. Band Gizmos via `CombatUnit.DrawBandGizmo` from both spawners. Sorting layers Background/Battlefield/Effects/Bag/UI in TagManager (Default stays first); Renderer2D sort mode CustomAxis (0,1,0); `SortingGroup` (Battlefield) on the prefab root; tent → Battlefield, battlefield image → Background; `BagBoardView` renderers → Bag. Tests: new `DepthSortingTests` (EditMode, real assets), crowd-of-20 and tall-collider gap tests (PlayMode); melee-touch test moved its target to y 0.8 (capsule ends); GameScene test now checks layers and "not steering" instead of `velocity.x == 0`, since later crowbars push the first one. Run 2026-11-22: EditMode 37/37, PlayMode 13/13 (the only later edit: stripped the trailing space Unity wrote on the prefab's empty `m_Name`); `git diff --check` clean; `GameScene` is the only build scene.
- 2026-11-22 Claude: queued 3A.3 (2D separation, band Gizmos, depth sorting) ahead of M1-1; M1-2 gained knockback/area-hit fields and attack-range hysteresis. Docs only, not committed.
- 2026-11-22 Claude: 3A.2 closed after the user's scene check passed. Design §3.4 `CombatUnit` row and §3.7 status updated. Last test run (EditMode 35/35, PlayMode 11/11) was **before** the user moved `Enemy Spawn Point` to y 1.112; rerun PlayMode at the start of 3A.3.
- 2026-11-22 Claude: 3A.1 closed after the user's scene check, which passed after the user's own config tuning (crowbar 2 s / range 0.15, rifle range 2). Design §1.9 now matches the configs, and §3.7 holds the new movement rule.
- `GameSceneSoldierSpawnerTests` now waits for the first crowbar and the first rifle unit instead of fixed seconds, so retuning durations no longer breaks it.
- Final 3A.1 run, after the user's tuning and the test rewrite (2026-11-22): EditMode 35/35 passed, PlayMode 7/7 passed; `git diff --check` clean.

## Found, not fixed

- `CrowbarLevel2` is still 3.4 s / range 0.7, but L1 is now 2 s / 0.15. L2 is slower and longer-ranged than L1, which breaks the 1.9 level rule (×0.85 → 1.7 s). Re-derive it when L2 matters (M3 content).
- Separation can nudge a stopped unit just out of range (seen in the scene test: gap 0.152 vs range 0.15), so it steps back in on the next fixed step. Fix queued as 3A.4 (attack-range hysteresis).
- The soldier art exists twice: the separate `Assets/Game/Art/Soldier_Sprites/PNG/` files and `soldier_atlas_512.png`. The configs only use `PNG/`; decide whether to delete the atlas or switch to it (e.g. before the M3 content pass).

## Queue

1. **3A.3 — crowd separation, band Gizmos, depth sorting** (rule: `docs/Design.md` §3.7)
   - **Scope:**
     - 2D same-team separation: push apart on both axes (not only Y), throttled (not every fixed step), neighbour query into a reused buffer (no per-step allocation), weaker than target steering so melee units spread around a target instead of stalling. The band clamp still wins.
     - Battlefield band: Gizmo lines at min/max Y in the Scene view.
     - Depth sorting: sorting layers Background / Battlefield / Effects / Bag / UI; a `SortingGroup` on the `CombatUnit` root; custom transparency sort axis (0, 1, 0) on the URP 2D renderer asset; the tent on the Battlefield layer.
   - **In scope:** project settings (sorting layers, Graphics) and the 2D renderer asset; the unit prefab and `GameScene` for the Sorting Group and layer assignments.
   - **Added by the user (2026-11-22):** `CombatUnit` collider 0.25 × 0.6 (offset 0, 0) and the `head_cap_*` sprite pivots (0.43, 0.5); range and separation use real collider distance (`Physics2D.Distance`) instead of the half-width circle.
   - **Out of scope:** attacks, damage, states, `UnitSpawner` rename, pathfinding.
   - **Tests:**
     - in a crowd of 20 same-team units, no two overlap after N steps;
     - a unit lower on screen renders in front of one higher up.
   - **Acceptance:** those tests; EditMode + PlayMode pass; `git diff --check` clean; prefab/scene references resolve and `GameScene` stays the only build scene; manual `GameScene` check (melee units fan out around a target, lower units draw in front, band lines visible in the Scene view).
2. **3A.4 — crowd stability** (rule: `docs/Design.md` §3.7; update it with the new rules)
   - **Scope:**
     - Opposing units never overlap: include opponents in the separation query, but push away from an opponent only while the collider gap is negative (beyond the dead zone). Same-team rules stay as in 3A.3.
     - Attack-range hysteresis now, not in M1-2: a unit becomes engaged at gap ≤ range and disengages only at gap > range + margin. Engaged units stop steering (separation still applies).
     - Velocity smoothing: the body velocity moves toward the desired velocity (move + separation) instead of jumping to it each step. The band clamp still wins.
     - A small overlap dead zone for separation, so units that barely touch do not jitter.
   - **Out of scope:** attacks, damage, states, `UnitSpawner` rename, pathfinding.
   - **Tests:**
     - in a 1-vs-8 melee crowd, once settled, no unit's position changes direction more than a small number of times over 3 seconds;
     - opposing units never overlap beyond the dead zone.
   - **Acceptance:** those tests; EditMode + PlayMode pass; `git diff --check` clean; manual `GameScene` check (crowd settles without jitter, no unit passes through an opponent).
3. M1-1 — rename `SoldierSpawner` → `UnitSpawner` (one instance per team); enemy spawning through it; delete nothing else yet.
4. M1-2 — `Health` + unit state machine (Idle, Walk, MeleeAttack, RangedAttack, Death); direct damage; white hit flash; weapon knockback and area-hit fields (with `Validate()` ranges and tests). (Attack-range hysteresis moved to 3A.4.)
5. M1-3 — death → release at most once; `PlayerBase` + contact damage; WebGL portrait smoke build.

## Done log

| Task | Summary | Commit |
|---|---|---|
| 1A–1C | grid, drag/rollback, merge/replace | (in history) |
| 2A–2D | adjacency production, pooled spawning, fills, hand-anchored weapons | (in history) |
| 3A | shared `CombatUnit`, `UnitCatalog`, targeting and chase | `15c7028`, `2886553` |
| 3A.1 | field-scale `Visual` root, enemy mirroring, edge-to-edge X range, spawn lanes + separation, tuned rifle/crowbar configs, bag head sorting | commit "Task 3A.1: combat visual and movement fixes" |
| 3A.2 | 2D edge-to-edge range (`EdgeGapTo`), diagonal chase, battlefield Y band on both spawners (−1.2…1.6 in `GameScene`), enemy spawn moved to y 1.112 | `237cb29` |
