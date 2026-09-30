# Plan: fog of war, light, scouts and minimap

Branch: `fog` (from `power`). **Read this file first when resuming** (after a context reset): check the
progress list, read the log at the bottom, and continue with the first unchecked step. Update the
checklist and log after every step and commit together with the step.

## Loop for every step (keeps tokens low)
1. Write failing tests first (Scenario helpers; `tests/FactoryTD.Sim.Tests/FogTests.cs`, `ScoutTests.cs` ...).
2. Implement until `task test -- <Area>` passes (Area = test class name part: `Vision`, `Scout`, `Minimap` ...).
3. `task check` (lint + all tests). Lint ENDOFLINE/WHITESPACE → `task fmt`.
4. Tick the box below, add one log line, commit: `Fog step N: <what>` + Co-Authored-By line.
Only grep/read the parts of files needed. Godot only in step 11. Edit files with the Edit tool, or Python
with `newline=""` (plain `open(..., "w")` on Windows writes CRLF). CLAUDE.md is CRLF: use the Edit tool.

## Design (from the user's request; decisions confirmed by the user 2026-09-30)
- **Per player, per tile**: `Explored` (ever lit, never lost) and `Visible` (lit right now). Sim state,
  deterministic, integer only. Recomputed every `VisionTicks` = 4 ticks (5 Hz); explored grows then.
- **Light sources** (radius in tiles, disc of tile centres, squared-integer test, precomputed masks):
  every own unit and finished building. Proportional to size: 1x1 building 3, 2x2 building 5,
  toybox 10 (must light the brick and plastic patches at the start), soldier 5, golem 5, builder/farmer 3,
  scout 7, car: disc 3 + **headlight cone** 10 tiles ahead, half-angle ~35°, along its last movement
  direction (8 directions, precomputed cone masks). Construction sites light 1.
  **Light does NOT pass walls** (user): line of sight per lit tile = integer Bresenham ray from the source
  tile; the ray stops at the first wall (the wall tile itself is lit, nothing behind it). Precompute the
  ray (tile offsets) per radius/cone mask entry so a recompute is just table walks.
- **Lamp** ("Leksakslampa", 1x1, cheap, radius 9): building whose only job is light. Needs no power (user).
- **Enemies**: enemy units are known only while on a Visible tile. Enemy buildings: a remembered snapshot
  (type, footprint, owner) is stored when seen and shown on explored tiles until the tile is seen again
  (then updated or removed, also if it was destroyed meanwhile).
  The simulation itself stays objective (units/towers fight what is in range); fog only limits what the
  player (UI) and the bot know and where they may build.
- **Placement**: ONLY extractors need every footprint tile Explored (deposits must be discovered) →
  `PlaceError.Unexplored`. Everything else may be built in the dark (user): builders walking to the site
  light (and explore) the way — a cheap way to scout with workers. The ghost must not reveal whether an
  unexplored tile has a deposit (in the dark, WrongResource must not leak: check Unexplored first).
- **Scout** (`UnitType.Scout`, toy explorer, worker: doesn't fight, not army, eats 1 food/min, fast,
  big light radius 7). Lives in a **tent** ("Tält", 1x1 or 2x2 unit factory, cheap: plastic), max 2 per
  tent, `MaxScouts` 6.
  - Searches **wide, not deep**: target = the frontier tile (explored floor tile next to an unexplored
    tile) with the lowest walking distance from its own toybox (BFS over explored floor), so the explored
    area grows in rings and scouts walk along the edge of the fog. Scouts spread out: skip frontier
    tiles within 6 tiles of another scout's target. Retarget when the target stops being frontier.
    Nothing left to explore → idle at home.
  - **Flees**: if an enemy unit is Visible within its light radius, run straight away from it
    (opposite direction, flow field fallback around walls) for `FleeTicks` = 3 s, then retarget.
  - Lamps speed discovery because scouts never need to walk over already explored ground.
- **Test switch**: `World.FullVision` (internal): everything explored and visible for everyone.
  `Scenario.Match()` sets it (existing tests keep their meaning); fog tests call `.RealFog()`.
  Bot matches (BotTests, ScenarioTests bot matches, DeterminismTests bot) use real fog.
- **Minimap** (corner of the screen): 1 pixel per tile; unexplored black, explored terrain dimmed,
  visible terrain bright, deposits coloured when explored, own buildings team colour, remembered enemy
  buildings enemy colour, visible units as dots, camera rectangle; click to move the camera.
- **Fog rendering**: a fog texture (1 texel per tile: black / half dark / clear) scaled over the map with
  linear filtering → soft light edges ("light sources"). Car cones get a warm light-cone glow sprite.
  Enemy units hidden outside Visible; unseen enemy buildings hidden; remembered ones drawn as ghosts.

Constants in `Sim/Vision.cs` `VisionStats`.

## Progress
- [x] 1. Branch `fog`; `World.FullVision` + Scenario `.RealFog()`; existing tests unchanged.
- [x] 2. `Sim/Vision.cs`: VisionStats radii, disc/cone masks with precomputed rays, `Vision` per player
      (Explored/Visible arrays, `IsExplored/IsVisible(p,x,y)`), recompute every VisionTicks in World.Tick.
      Tests: toybox lights both near deposits at start, radius per size, walls block light (unit next to a
      room wall doesn't light the hall/other side; light passes the door), walking unit explores its path,
      Visible drops when it leaves but Explored stays, car cone sees 10 ahead but not 10 behind,
      construction site radius 1.
- [x] 3. Placement: `PlaceError.Unexplored` for extractors only (checked before WrongResource), Texts error.
      Tests: battery patch not buildable at start, buildable after a unit walked past, FullVision ignores it,
      a belt/lamp can be placed in the dark, the builder walking there explores the site.
- [x] 4. Knowledge: `World.CanSee(player, unit)`, remembered enemy buildings (`Vision.Remembered`:
      snapshot list), updated on sight. Tests: enemy unit hidden outside light / shown inside; enemy
      building remembered after the unit leaves; destroyed while unseen stays remembered until re-seen;
      own buildings always known.
- [x] 5. Lamp building (`BuildingType.Lamp`): cost, hp, build time, Buildable, Texts. Tests: lights radius 9,
      explores beyond the known area, only while built.
- [x] 6. Tent + scout: `BuildingType.Tent` (UnitFactory producing `UnitType.Scout`), UnitDef (fast,
      no damage), worker (no power, eats food), caps. Tests: tent makes scouts up to its cap, scouts
      don't fight, count as workers.
- [x] 7. Scout AI (`World.TickScout`): frontier BFS from home, spread between scouts, walk along the fog
      edge, retarget, flee from visible enemies. Tests: one scout finds the battery patch within X s;
      frontier target is the nearest to home (wide before deep); two scouts pick targets ≥6 apart;
      explored area grows faster with two; lamps near the fog shorten the time to the battery patch;
      scout flees (distance to enemy grows for 3 s); nothing left → returns home.
- [x] 8. Determinism: checksum hashes Explored/Visible (e.g. running hash of bit arrays), remembered
      buildings, scout target/flee state. Determinism tests with real fog + scouts.
- [x] 9. UI logic (`Scripts/UI`): `Minimap` (colour index per tile + camera rect ↔ tile mapping + click →
      tile), `FogLevels` (per tile 0/1/2 for the fog texture), `Knowledge` helpers (which enemy units /
      buildings / ghosts to draw), InfoRows: nothing for unseen enemies, placement error text.
      Tests in UiTests.
- [x] 10. Bot: tent + scout module early; modules with extractors are Ready only when their deposit tiles
      are explored (battery patch etc.); Observe uses visibility. Tests: bot explores the battery patch and
      builds a battery extractor within N min; BotTests fixture with real fog; slow bot vs idle still wins,
      bot vs bot healthy (starvation, power, army) — record times in the log.
- [ ] 11. Assets + view: sprites via `pixelart_workbench` draw (tent: toy camping tent; scout: wind-up toy
      explorer with binoculars/hat, like the other unit sprites; lamp: toy night light/lantern; light cone
      glow). FogView (ImageTexture 1 px/tile, linear filter, over the map, under UI), hide unseen enemy
      units (UnitView/CombatView projectiles from hidden units ok) and buildings (BuildingView), ghosts for
      remembered ones, car cone glow, minimap (CanvasLayer corner, click to jump, camera rect), menu entries
      (tent, lamp), placement error. `task build` + headless screenshot(s) (scratchpad shot.sh + VisualProbe).
- [ ] 12. Balance (slow, `task test:report`): time for 1 scout to find the battery patch, with lamps,
      bot matches still end in 15–25 min range; guards for these.
- [ ] 13. CLAUDE.md design + code structure (Edit tool); `task check`; commit.

## Log
(one line per finished step: what changed, test count, anything surprising)
- Step 1: branch fog; World.FullVision (internal) + Scenario.Match() full vision by default, .RealFog() opt-in. Bot matches still full vision until step 10 (World.CreateMatch in DeterminismTests is real). 165/165.
- Step 2: Sim/Vision.cs (VisionStats; Vision with Explored/Visible bool[] per player; precomputed Bresenham rays per disc radius and 8 cones, walls stop rays, wall tile lit). World.IsExplored/IsVisible (FullVision -> true), recompute every 4 ticks + at CreateMatch. Unit.LightDirection from MoveX/Y (Vision.DirectionIndex, 22.5 deg sectors). Building source tile = X+W/2,Y+H/2. 179/179.
- Step 3: PlaceError.Unexplored (appended to enum) for extractors only, checked before WrongResource (no leak); Texts message. Walls/Occupied still "leak" in the dark (accepted). 182/182.
- Step 4: RememberedBuilding record (type, footprint, owner, facing); Vision keeps a list per player, refreshed where visible each recompute. World.CanSee(player, unit), World.RememberedBuildings(player) (FullVision: all enemy buildings now). 185/185.
- Step 5: BuildingType.Lamp (class Lamp in Vision.cs), cost 3 brick + 3 plastic, hp 40, build 30, radius 9 when built. View has NO texture/menu for Lamp yet (step 11). 186/186.
- Step 6: UnitType.Scout (worker: IsWorker, no power, eats 1; hp 18, speed 36, recipe 3 plastic, 160 ticks; light 7), MaxScouts 6, ScoutsPerTent 2 (UnitFactory rule). BuildingType.Tent 1x1 (5 brick 10 plastic) -> UnitFactory(Scout). World.TickScout stub (stands still). Texts names. View: no sprites for Tent/Scout yet (step 11). 188/188.
- Step 7: Sim/Scouting.cs (World partial): target = frontier tile minimising homeDistance + manhattan(scout)/2 (pure nearest-home ping-ponged across the base: 717 tiles/60 s -> 2825/180 s), spread 6 tiles, retarget on vision ticks, flee 3 s from visible enemy units (5 directions tried), home when nothing left. World.HomeDistance (BFS over explored floor, cached per vision tick), IsFrontier, ExploredCount, ExploreAll (test). One scout finds the battery patch in 139 s (guard 180). 194/194.
- Step 8: Vision.HashInto (explored/visible as 64-bit words, remembered list), unit hash + LightDirection, scout target/flee. Checksum_SeesSmallChanges covers explored; RealFog_ScoutsFleeingAndExploring_IsDeterministic (90 s). 195/195.
- Step 9: UI/Fog.cs: Knowledge.ShowBuilding/Ghosts, FogLevels (0/1/2), Minimap (kind in low bits + LitBit; terrain, deposits, remembered enemy buildings, own buildings, visible units; CameraRect, ToTiles). Hover filter for unseen enemies is done in the view (InfoPanel.Hovered) in step 11. 200/200.
- Step 10: bot module "spejare" (Tent at 14,36 fed directly by plastic extractor 13,36); extractors on unexplored ground are skipped by MaintainPlan until explored (scouts + builders laying the battery belt light it); Observe counts only CanSee enemies. BotTests + bot matches use RealFog. Bot has a scout by 2 min and a battery extractor by 4 min. Bot vs bot: p0 wins 732 s (army 68 vs 15, still lopsided, now the other way); bot vs idle 354 s. 201/201.
