# BagManagementBattler (Bag War) — Claude Code notes

Local working guidance. Keep it concise; do not turn it into a project history or task log.

## What this repo is

A small 2D Unity 6000.3.17f1 (URP 17.3.0, Input System 1.19.0) portfolio auto-battler, working title "Bag War". Loop: prepare a bag grid, produce soldiers through item adjacency, fight an enemy wave, receive a reward, prepare again. One main scene holds the bag, battlefield and HUD. Not built on Puzzle Framework.

The design baseline is `docs/Design.md` (game design, asset list, script map, milestones; supersedes `docs/Architecture.md`): it defines each component's responsibility and boundary, scope exclusions, and current status. Read the relevant section before adding or changing a component, and keep new code inside the owner's stated boundary.

## Session protocol (shared with Codex)

Claude and Codex alternate on this repo. `docs/Progress.md` is the shared handoff: read it and run `git status` before any work, keep its handoff notes current during the task, and update it before stopping, finished or not. Its rules override habit. `AGENTS.md` points Codex at this file, so the working rules below apply to both agents.

Plugins (Superpowers, Ponytail) are welcome, but Codex does not have them, so their outputs must not become a second source of truth:
- `docs/Progress.md` is the only plan and task-tracking file. Do not create `docs/plans/`, separate plan or spec files, or todo files; put a task's step plan in its `Progress.md` section instead.
- No git worktrees or branches (see working rules). Skills that call for them: skip that step.
- Brainstorming is for undecided design only. If the current task in `Progress.md` already has scope and acceptance, implement it; raise real design gaps as questions instead of redesigning.
- Design decisions go into `docs/Design.md`, never into skill-generated documents.

## Working rules

- Preserve unrelated user changes; inspect `git status` before editing and do not reset, revert, delete or overwrite unrelated files. This repo often has uncommitted work in progress.
- Do not commit, push, create branches or rewrite history unless asked.
- Never modify Unity-generated folders: `Library`, `Temp`, `Obj`, `Logs`, `UserSettings`.
- Do not change scenes, prefabs, ScriptableObject configs, project settings, input actions, render-pipeline setup or packages unless the task includes that scope.
- Gameplay state lives in plain C# rule owners (`BagGrid`, `BagManager`, production session, spawner); views and drag input submit requests and display results, they never edit grid occupancy directly.
- Respect the "Not in v1" list in `docs/Design.md` (no projectiles, item rotation, multiple soldier types, advanced pathfinding, etc.) unless the task changes them.
- Verify with the narrowest relevant test first, and stop at the requested scope.

## Quality gates

Bag War hazards to check before calling a change done:

- Drag (bag ↔ container): every path that begins, moves, cancels or ends a drag — invalid drop, Escape/focus loss, pointer released over UI, bag locked mid-drag — must restore the item to its source and leave grid, container and visuals in agreement. Cover new paths with a test.
- Merge/replacement: validate the full result before committing; a rejected move changes nothing.
- Config definitions (`BagItemDefinition`, `UnitDefinition`, `UnitCatalog`, later wave/shop configs): `Validate()` enforces required references and ranges (e.g. a soldier item must reference a friendly catalog entry), and each rule has an EditMode test.
- Keep at least one test per system running against the real `Assets/Game/Configs` assets, not only `CreateTransient*` fixtures.
- Pooled units: release at most once (death and wave cleanup can overlap); recycle clears targets, velocity, timers, tweens and callbacks so nothing leaks into the next reuse.
- When prefabs, serialized fields or `GameScene` change, check references still resolve, and keep `GameScene` the only build scene.

## Layout

- `Assets/Game/Scripts/Bag/` — bag grid, items, drag, container, production (`BagManagementBattler.Bag`).
- `Assets/Game/Scripts/Combat/` — production controller, combat soldier, spawner (`BagManagementBattler.Combat`).
- All runtime code is one assembly: `Assets/Game/Scripts/BagManagementBattler.Runtime.asmdef`.
- `Assets/Game/Tests/EditMode/` and `Tests/PlayMode/` (`BagManagementBattler.Tests`); both suites matter.
- `Assets/Game/Configs/` — ScriptableObject item/soldier configs. `Assets/Game/Prefabs/`.
- Main scene: `Assets/Game/Scenes/GameScene.unity`. `Assets/Scenes/SampleScene.unity` is the template leftover.
- `Assets/Plugins/Demigiant/` — third-party DOTween; do not edit.

## Running tests

Close the project in the Unity Editor first (batch mode cannot open a project that is already open). Then, from this folder, run each platform:

```
"C:\Program Files\Unity\Hub\Editor\6000.3.17f1\Editor\Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults TestResults-EditMode.xml -logFile Unity-EditMode.log
"C:\Program Files\Unity\Hub\Editor\6000.3.17f1\Editor\Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform PlayMode -testResults TestResults-PlayMode.xml -logFile Unity-PlayMode.log
```

While iterating, run only the relevant fixture(s) with `-testFilter` (full class names, `;`-separated or a regex), then the full suites once at the end:

```
"C:\Program Files\Unity\Hub\Editor\6000.3.17f1\Editor\Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform PlayMode -testFilter "BagManagementBattler.Tests.CombatUnitMovementTests" -testResults TestResults-PlayMode.xml -logFile Unity-PlayMode.log
```

Before each run, check no stray `Unity.exe` is running (`tasklist /FI "IMAGENAME eq Unity.exe"`). If the log stops growing for 3 minutes (a licensing hang shows Unity at ~0.3 MB, 0% CPU), kill it and report instead of waiting.

If the XML is not at the requested path, check the "Saving results to" line in the log (it has landed under `%USERPROFILE%\AppData\LocalLow\DefaultCompany\<product>\` in sibling projects). Do not commit result or log files.

Keep context small: never read `Unity-*.log`, `TestResults-*.xml`, `*.unity` scenes, prefabs or `*.csproj` files in full. Search them instead: test totals and failures (`grep -E 'test-run|result="Failed"'` on the XML; `grep -iE 'error|exception'` on the log), and only the YAML blocks a task changes in scenes/prefabs. Read source files by the needed line range when the location is known.

## Tools in this setup

- `csharp-lsp` for definitions/references/diagnostics; prefer it over text search for symbol questions.
- Unity plugin skills: `unity:create-2d-physics` (units move through Rigidbody2D), `unity:ui` for HUD/shop, `unity:unity-cli` / `unity:unity-pipeline` for driving the Editor.
- `unity-perf` skills for pooling, spawning and per-frame combat allocation work.
- Context7 for Unity and DOTween API docs.
