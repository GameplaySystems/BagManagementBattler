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
- **Current task:** 3A.2 — circular range and 2D chase (scope/tests: Queue #1 below, rule: `docs/Design.md` §3.7)
- **Status:** implemented and committed ("Task 3A.2: circular range and 2D chase"); waiting for the user to confirm the manual `GameScene` check.
- **Owner:** Claude
- **Last updated:** 2026-11-22 by Claude

## Handoff notes

- 2026-11-22 Claude: 3A.2 implemented, uncommitted. `CombatUnit`: public `EdgeGapTo` (2D centre distance minus both half-widths), steering `normalized × speed` toward the target, X-only advance without one, vertical step clamped to the band each fixed step, spawn Y clamped, band reset on `ResetForPool`. `Initialize(loadout, pos, minY = -inf, maxY = +inf)` rejects NaN or min > max. Both spawners have serialized `battlefieldMinY/MaxY`; `GameScene` sets −1.2 / 1.6 on both, estimated from the road in the background sprite (road ≈ y −1.43…1.87). Tune it in the scene check.
- New tests in `CombatUnitMovementTests`: a unit keeps moving when only the X edges meet, a Y-offset target is reached on the 2D gap, units never leave the band (separation plus a high target plus a spawn above the band), and an inverted band is rejected. `GameSceneSoldierSpawnerTests` now uses `EdgeGapTo` with +0.03 upper slack (see Found, not fixed).
- Run 2026-11-22 after the final edit: EditMode 35/35 passed, PlayMode 11/11 passed; `git diff --check` clean; scene diff is only the 4 band lines.
- After that run, the user moved `Enemy Spawn Point` to y 1.112 in `GameScene` (Editor save also wrote `spawnLaneSpacing: 0.15` explicitly). This is committed with 3A.2, but tests were **not** rerun on it (Editor was open). Rerun PlayMode first next session.
- Still open: the user's manual `GameScene` check (crowbar walks diagonally to the enemy and touches it; nobody leaves the band). Then commit, Done log, Design §3.4/§3.7 status, promote M1-1.
- 2026-11-22 Claude: 3A.1 closed after the user's scene check, which passed after the user's own config tuning (crowbar 2 s / range 0.15, rifle range 2). Design §1.9 now matches the configs, and §3.7 holds the new movement rule.
- `GameSceneSoldierSpawnerTests` now waits for the first crowbar and the first rifle unit instead of fixed seconds, so retuning durations no longer breaks it.
- Final 3A.1 run, after the user's tuning and the test rewrite (2026-11-22): EditMode 35/35 passed, PlayMode 7/7 passed; `git diff --check` clean.

## Found, not fixed

- `CrowbarLevel2` is still 3.4 s / range 0.7, but L1 is now 2 s / 0.15. L2 is slower and longer-ranged than L1, which breaks the 1.9 level rule (×0.85 → 1.7 s). Re-derive it when L2 matters (M3 content).
- Same-team separation only pushes units apart vertically, so a big simultaneous wave will spread tall. 3A.2 kept it as is. Inside the band, units pinned at the band edge can no longer split apart on Y and may overlap.
- Separation can nudge a stopped unit on Y just out of range (seen in the scene test: gap 0.152 vs range 0.15), so it steps back in on the next fixed step. That is harmless for movement, but M1-2's attack state should use a little range hysteresis so attacks don't flicker.

## Queue

1. **3A.2 — circular range and 2D chase** (rule: `docs/Design.md` §3.7)
   - **Scope:** in `CombatUnit`, range becomes 2D edge to edge (centre distance minus both collider radii, radius = half the collider width). With a target, steer straight toward it on both axes until in range. Without one, advance along X. Clamp Y to the battlefield band (serialized min/max Y, set in `GameScene`). Keep same-team separation. No pathfinding.
   - **Out of scope:** attacks, damage, states, `UnitSpawner` rename.
   - **Tests:**
     - a melee unit stops only when touching (edge distance ≤ its tiny range, not before);
     - a target offset on Y still gets reached (in range on the 2D distance);
     - Y never leaves the band, even with separation pushing.
   - **Acceptance:** those three tests; EditMode + PlayMode pass; `git diff --check` clean; manual `GameScene` check (crowbar walks to the enemy diagonally and touches it; nobody leaves the band).
2. M1-1 — rename `SoldierSpawner` → `UnitSpawner` (one instance per team); enemy spawning through it; delete nothing else yet.
3. M1-2 — `Health` + unit state machine (Idle, Walk, MeleeAttack, RangedAttack, Death); direct damage; white hit flash.
4. M1-3 — death → release at most once; `PlayerBase` + contact damage; WebGL portrait smoke build.

## Done log

| Task | Summary | Commit |
|---|---|---|
| 1A–1C | grid, drag/rollback, merge/replace | (in history) |
| 2A–2D | adjacency production, pooled spawning, fills, hand-anchored weapons | (in history) |
| 3A | shared `CombatUnit`, `UnitCatalog`, targeting and chase | `15c7028`, `2886553` |
| 3A.1 | field-scale `Visual` root, enemy mirroring, edge-to-edge X range, spawn lanes + separation, tuned rifle/crowbar configs, bag head sorting | commit "Task 3A.1: combat visual and movement fixes" |
