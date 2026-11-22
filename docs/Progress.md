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
- **Current task:** none. Next is Queue #1 (M1-1): promote it when starting.
- **Status:** 3A.2 done and committed (`237cb29`); scene check passed
- **Owner:** —
- **Last updated:** 2026-11-22 by Claude

## Handoff notes

- 2026-11-22 Claude: 3A.2 closed after the user's scene check passed. Design §3.4 `CombatUnit` row and §3.7 status updated. Last test run (EditMode 35/35, PlayMode 11/11) was **before** the user moved `Enemy Spawn Point` to y 1.112; rerun PlayMode at the start of M1-1.
- 2026-11-22 Claude: 3A.1 closed after the user's scene check, which passed after the user's own config tuning (crowbar 2 s / range 0.15, rifle range 2). Design §1.9 now matches the configs, and §3.7 holds the new movement rule.
- `GameSceneSoldierSpawnerTests` now waits for the first crowbar and the first rifle unit instead of fixed seconds, so retuning durations no longer breaks it.
- Final 3A.1 run, after the user's tuning and the test rewrite (2026-11-22): EditMode 35/35 passed, PlayMode 7/7 passed; `git diff --check` clean.

## Found, not fixed

- `CrowbarLevel2` is still 3.4 s / range 0.7, but L1 is now 2 s / 0.15. L2 is slower and longer-ranged than L1, which breaks the 1.9 level rule (×0.85 → 1.7 s). Re-derive it when L2 matters (M3 content).
- Same-team separation only pushes units apart vertically, so a big simultaneous wave will spread tall. 3A.2 kept it as is. Inside the band, units pinned at the band edge can no longer split apart on Y and may overlap.
- Separation can nudge a stopped unit on Y just out of range (seen in the scene test: gap 0.152 vs range 0.15), so it steps back in on the next fixed step. That is harmless for movement, but M1-2's attack state should use a little range hysteresis so attacks don't flicker.

## Queue

1. M1-1 — rename `SoldierSpawner` → `UnitSpawner` (one instance per team); enemy spawning through it; delete nothing else yet.
2. M1-2 — `Health` + unit state machine (Idle, Walk, MeleeAttack, RangedAttack, Death); direct damage; white hit flash.
3. M1-3 — death → release at most once; `PlayerBase` + contact damage; WebGL portrait smoke build.

## Done log

| Task | Summary | Commit |
|---|---|---|
| 1A–1C | grid, drag/rollback, merge/replace | (in history) |
| 2A–2D | adjacency production, pooled spawning, fills, hand-anchored weapons | (in history) |
| 3A | shared `CombatUnit`, `UnitCatalog`, targeting and chase | `15c7028`, `2886553` |
| 3A.1 | field-scale `Visual` root, enemy mirroring, edge-to-edge X range, spawn lanes + separation, tuned rifle/crowbar configs, bag head sorting | commit "Task 3A.1: combat visual and movement fixes" |
| 3A.2 | 2D edge-to-edge range (`EdgeGapTo`), diagonal chase, battlefield Y band on both spawners (−1.2…1.6 in `GameScene`), enemy spawn moved to y 1.112 | `237cb29` |
