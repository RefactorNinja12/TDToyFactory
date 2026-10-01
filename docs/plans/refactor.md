# Plan: optimise code, remove duplication, raise test coverage, cheaper test feedback

Branch: `refactor` (from `ui`). **Read this file first when resuming**, continue with the first unchecked
step, tick + log + commit + push per step.

## Rules for this plan
- **Behaviour must not change** in the refactor steps (2–3). Safety net: the existing tests plus a new
  **golden checksum test** (step 1): a 3-minute bot-vs-bot match must give exactly the recorded checksum.
  If a refactor changes it, the refactor changed behaviour → find out why before going on. The golden
  value is only updated on purpose (and logged here) when a step is *meant* to change behaviour.
- Small commits: one duplication removed per commit, `task check` green before each.
- Token loop: `task test -- <Area>` while working, `task check` before commit; read only the compact
  runner output; grep for anchors instead of reading whole files; no screenshots unless View output
  changes (then one cropped one).

## Known duplication / hot spots (from the code)
- Grid searches written four times: `World.FindPathTo` (BFS to a building), `World.FindPathToTile`
  (Scouting.cs, BFS to a tile), `World.HomeDistance` (BFS distances), `MapLayout.AllFloorConnected`
  (flood fill). One `GridSearch` helper (BFS with a "walkable" and "goal" predicate, path or distances).
- Path following written twice: `World.WalkTo` (building) and `World.WalkToTile` (scouts).
- "Any tile of a footprint" loops: `Vision.AnyVisible`, `Knowledge.ShowBuilding`/`Ghosts`,
  `World.NetworkOf`, `World.CheckLocation`, obstacle `SetObstacle/CanHold`. One footprint iterator.
- Army-factory gating (starving, power, caps) in `UnitFactory.Tick` and `Treadmill.Tick`.
- `CountUnits` / `CountBuildings` scan every unit/building and are called every tick by factories,
  treadmills, CallBuilder, ReplaceLostBuilders and the bot: cache per tick (counts per owner/type,
  rebuilt once per tick or kept up to date on spawn/remove).
- View: icon `TextureRect` built the same way in ResourceBar, BuildMenu, InfoPanel; small badge/box
  styles; texture loads by path in several views. One `UiTheme.Icon(...)`/`UiTheme.Badge(...)`.

## Progress
- [x] 1. Baseline + tools:
      a) golden checksum test (3 min bot vs bot, fixed seed) in DeterminismTests;
      b) `task coverage`: coverlet collector + a tiny PowerShell summary (total line % for Sim and UI,
         then the 10 least covered files, one line each — never the raw XML);
      c) `task test:timing` (runner `-Timing`): the 10 slowest tests, one line each;
      d) sim speed guard (slow test): ms per simulated minute of bot vs bot, logged.
      Record baseline numbers (coverage %, fast/slow suite seconds, sim ms/min) in the log.
- [ ] 2. Sim dedup (golden checksum must stay equal):
      a) `Sim/GridSearch.cs` (BFS path + distances + connected), used by the four searches;
      b) one path follower for buildings and tiles;
      c) `Building.Footprint()` / footprint helpers, used by Vision, Knowledge, NetworkOf, CheckLocation;
      d) shared army-factory gate for UnitFactory and Treadmill;
      e) per-tick unit/building count cache (perf; compare sim ms/min before/after).
- [ ] 3. View/UI dedup: `UiTheme.Icon`, `UiTheme.Badge`, texture loading through BuildingVisuals only;
      remove leftover unused code (grep for unreferenced private members / old constants). `task build`.
- [ ] 4. Coverage: tests for the least covered Sim/UI code from `task coverage` (expected: bot routing
      edge cases, splitter/sorter/junction corners, projectiles/area damage, power grid edges, texts,
      info rows for every building type). Target: Sim + UI line coverage ≥ 85 % (log before/after).
- [ ] 5. Cheaper feedback:
      a) `task test:changed`: runs only the test areas for files changed since the last commit
         (a small map file → test filter, e.g. Power*.cs → Power, Vision/Scouting → Fog|Vision|Scout);
      b) slowest fast tests (from step 1c) made cheaper: share expensive setups via fixtures, shorter
         simulated times where the assertion allows, merge tiny tests with the same setup;
      c) check xUnit parallelism (classes in parallel; long bot fixtures not serialised behind others);
      d) runner: one-line PASS stays; FAIL blocks already grouped — add the test class to the summary
         line when only one class failed.
      Log fast/slow suite times before/after.
- [ ] 6. CLAUDE.md (workflow: coverage, timing, test:changed, golden checksum rule), final `task check`,
      commit, push, PR link.

## Log
- Step 1: golden checksum (2 min bot vs bot, 0x299E99DC031BB7E0) in DeterminismTests; task coverage (coverlet, IncludeTestAssembly: Sim code is linked into the test assembly; ~100 s, summary only); task test:timing (10 slowest); slow SimSpeed test. BASELINE: coverage Sim 97% UI 86.5% (Texts 55%, InfoRows 86%, Logistics 87%, Treadmill 82%); fast suite see next line; slow 28.4 s; sim 1266 ms per simulated minute (53 units, 419 buildings); slowest: BotVsBot 15 min 14.1 s, determinism 5 min 11.1 s, SimSpeed 6.2 s, bot vs idle 3.8 s, BotRuns fixture ~3.7 s.
- Baseline fast suite 9.8 s.
- Step 2a: Sim/GridSearch.cs (PathTo, Distances; arrays, same neighbour order) replaces FindPathTo, FindPathToTile, HomeDistance BFS, AllFloorConnected flood fill. Golden unchanged.
- Step 2b: StepAlongPath shared by WalkTo (buildings) and WalkToTile (scouts); when to re-plan stays separate (different rules). Golden unchanged.
