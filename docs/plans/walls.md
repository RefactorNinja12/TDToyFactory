# Plan: walls, electric fences and traps (trapdoor, pendulum)

Goal:
- Players build **walls** that block enemy units' pathing. Units walk round them through any gap.
- **Golems** can punch through: 5 golem punches per wall brick. They do it whenever that is
  shorter than walking round.
- If a player seals their base off completely (no gap anywhere), **every** unit attacks the walls,
  picking the cheapest place to break through.
- Walls block **everyone**, own units too; a **gate** lets own units through (enemies treat it as a wall).
- Several wall types: a **brick wall** (Lego bricks, cheap) and an **electric fence** (costs batteries to
  build and power to run; it zaps units that hit it).
- **Traps** that trigger periodically and cost gears and springs:
  - the **trapdoor** (2x2): opens on a timer, and enemies standing on it fall in;
  - the **pendulum** (4x1, turnable): a long arm whose weight swings from one end to the other and back,
    shoving the enemies it passes in the swing direction.

## Current state (what this builds on)
- `FlowField` (Sim/FlowField.cs): Dijkstra from the enemy core. Only `TileType.Floor` is walkable.
  Enemy buildings add `BuildingPenalty` = 60 and never block. Units walk onto enemy buildings and shoot
  them, because `SelectTarget` sees any enemy building in range.
- `TryStep` (Fighting.cs) and `Separation.Shift` only check `Map[x,y] == Floor`.
- Workers and scouts path with `GridSearch.PathTo(... IsFloor ...)` (Workers.cs:369, Scouting.cs:157/181).
- `World.MarkChanged` marks the enemies' flow fields dirty. `_powerFields` (the way back to the grid)
  are built from the same rules.
- `BuildingRules.Size(type)` is fixed and does not depend on facing. The pendulum needs a 4x1 that turns.
- Golem: Damage 12 × BuildingDamagePercent 200 = 24 per punch, AttackTicks 30, Range 1.5 tiles (Punch).

## Approach (decided; open questions at the end)
### Walls in the pathing
- New `BuildingType.BrickWall` and `BuildingType.ElectricFence`, both 1x1, in a new menu category
  **Försvar** with the towers (or next to them). `Building.IsWall` (static `BuildingRules.IsWall(type)`).
- **Walls block everyone, own units too** (decided by the user). Own units and workers never attack their
  own walls; to them an own wall is like a real wall (impassable, `FlowField.Unreachable`).
- So a player needs a way out: a **gate** (`BuildingType.Gate`, 1x1, the same health as a brick wall).
  Own units and workers walk through it; to enemies it is a wall like any other (they go round or break it).
  Cheap, and built in the wall line like any wall piece.
- Consequences that need handling:
  - An army factory/toolbox enclosed by own walls without a gate: its units spawn but have no way.
    They stand still (the field is Unreachable) and the hover panel/a toast says "Instängd: bygg en grind".
    A pure helper `World.IsEnclosed(player, building)` (flood fill from the building's sides) is used by
    InfoRows and the toast.
  - A wall finished on a tile where units stand: they are pushed out to the nearest free floor tile
    (`GridSearch` from the tile, deterministic order) the tick the wall completes.
  - Builders must reach a construction site: the site is reachable when a free tile next to it is
    (as today). A wall site enclosed by other walls can't be built; the builder skips it like an
    unreachable site today.
  - Farmers, scouts and the power field (golems/cars going back to charge) follow the same rule.
- **The flow field gets one cost rule per kind of walker**: `FlowField.Build(world, target, owner, WallCost wallCost)`.
  - Army field (everyone except golems): enemy wall tile = `SealedWallPenalty` (1 000 000). Dijkstra then
    always takes any open way round, however long. Only if no way exists does it cross the fewest walls.
    This is the "sealed base" rule with no separate detection. Max path cost (160×60 tiles ×14
    + a few walls × 1e6) stays well within `int`.
  - Golem field: enemy wall tile = `GolemWallPenalty`, worth about the time 5 punches take
    (5 × 30 ticks ≈ 150 ticks ≈ 8 tiles of golem walking → ~80 + BuildingPenalty). Golems go through
    when the way round is longer than ~8 tiles. The constant lives in `WallStats`, with a test for both cases.
  - With no walls, both fields are identical to today → **the golden checksum is unchanged** (a
    regression test for the refactor step).
  - `World` keeps `_fields[p]` (army) + `_golemFields[p]`, dirtied together. `GetFlowField(player, unitType)`.
    The power field (back to the grid) uses the same rule as the unit (golems/cars).
  - Own walls are Unreachable in every field of that owner (own units never break through their own walls).
    Only enemy walls get the penalties above.
- **Stepping:** `TryStep` and `Separation.Shift` refuse to step into a wall tile
  (`World.BlocksUnit(owner, x, y)`: not floor, or any wall except the owner's own gate). Corners can't be
  cut past walls, the same check as `IsWalkable` in FlowField.
- **Attacking walls:**
  - `SelectTarget` **skips walls** (otherwise soldiers would shoot every wall they pass on the way to a gap).
  - In `TickUnit`, before following the flow field: if `NextTile` is an enemy wall (the field chose to go
    through), the unit attacks *that wall*.
  - Walls are hit **at contact**: the unit walks up until it is within `WallStats.HitReach` (1.2 tiles)
    and hits it, and the hit counts as a punch (DamageKind.Punch) even for soldiers. This makes the
    electric fence matter ("units that hit it").
  - **Hits, not HP balance:** a wall brick has `WallStats.BrickHealth` = 120 (= 5 golem punches of 24).
    Other units do their own Damage × a `WallDamagePercent` per unit type (soldier, cheese hunter and
    car are much weaker against walls). Tested: exactly 5 golem punches break one brick.
  - Towers and projectiles are not affected: shots fly over walls, and walls do not block light (like
    obstacles).
- **Workers/scouts:** all workers (own and enemy) use `BlocksUnit` in their `GridSearch` predicates
  (Workers.cs:371, Scouting.cs:128/157/181). Workers never attack walls; if there is no way, they wait
  (as today when there's no path).

### Electric fence
- Build cost includes batteries (e.g. 4 bricks + 2 plastic + 2 batteries). Health a little higher than a
  brick wall.
- Needs grid coverage (like a tower): `World.TryDrawPower(this, WallStats.ZapEnergy)`.
- When an enemy hits it and it has power: the attacker gets `ZapDamage` (DamageKind.Electric? or
  a fixed amount regardless of armour, decide in step 3), with a cooldown per fence (`ZapCooldownTicks`) so it
  isn't free. Without power it's just a wall (and shows the "no power" icon).
- Electronic armour (RC car) takes double damage from the fence (fits "electronics weak to…": a
  small decision, see questions).

### Traps (new `Sim/Traps.cs`: `TrapStats`, `Trapdoor`, `Pendulum`)
- Common: built from gears and springs (+ some bricks), no ammo, no power. They run on a fixed
  timer from completion (`Phase` = ticks since built, part of the checksum).
  - **Enemy units don't see them:** `SelectTarget` skips traps, the flow field charges them as floor
    (no BuildingPenalty), and units walk over them.
  - Consequence: traps can only be destroyed by area damage (catapult splash) or never; decide in step 4
    (suggestion: they get `Health` and take damage from what passes over them, like wear: each catch costs 1/N).
  - Both affect only enemy units; own units walk over them safely.
- **Trapdoor (2x2):**
  - cycle `TrapdoorPeriod` (e.g. 160 ticks = 8 s): closed → `WarnTicks` blinking (shown in the view) → open
    `OpenTicks` (20) → closed.
  - Every enemy unit whose centre is on the footprint while it's open falls: it is removed (killed, no refund).
    That includes golems (decide: perhaps golems get stuck and climb out with damage instead).
  - Cost e.g. 4 gears + 2 springs + 10 bricks.
- **Pendulum (4x1, turned with R):**
  - First size that depends on facing: `BuildingRules.Size(type, facing)` (East/West = 4x1,
    North/South = 1x4). Every call site of `Size(type)` gets the facing (placement, BuildController preview,
    bot, Footprint); every other building ignores it.
  - The weight runs along the long axis: from tile 0 to tile 3 in `SwingTicks` (e.g. 30), pauses, and back.
    The position is integer sub-tiles, `Pendulum.BobX/BobY` (interpolated in the view).
  - Enemy units within the weight's radius (~0.6 tile) get a **push**: new fields on `Unit`
    (`PushX, PushY, PushTicks`), `PushDistance` (e.g. 3 tiles) along the swing direction, spread over a few
    ticks. The push goes through `TryStep` (so never into walls/obstacles), and the unit doesn't walk or attack
    while being pushed. Golems are pushed half as far (heavy). A small amount of damage per hit.
  - Combo: placed across the path with a trapdoor beside it, it shoves enemies into the trapdoor.
  - Cost e.g. 3 gears + 4 springs + 10 bricks.

### UI / placement
- Walls are placed **click–click like belts**: the first click starts, the line follows the mouse, the second
  click builds a whole wall line. Reuse `DragPath`/`BeltPlanner` with the rule "free floor, no belt crossings,
  straight or L-shaped". One `Place` command per tile (the same as belts in the command flow).
- Menu **Försvar**: brick wall, gate, electric fence, trapdoor, pendulum (hotkeys = digits in the category,
  `BuildHotkeys`). Texts (`Texts`), InfoRows (health "x/5 punches", fence: power/zaps, trap: next trigger in s).
- Placement preview: pendulum shows its swing path; the fence shows coverage like other power buildings.

### Art (`tools/art/defense/*.py`, kit style, restyled)
- Gate: the brick wall with a swinging toy door in the player's colour (opens when an own unit passes, part).
- Brick wall: a 2x4 Lego brick seen from above at an angle, studs; damage states (3 levels by health)
  as frames. Wall pieces join up with neighbouring walls (4-bit neighbour mask like the belt stubs,
  or simply studs at the edges so pieces look joined).
- Electric fence: plastic posts with a striped wire, a battery box; spark part (BuildingParts `Blink`) on a zap.
- Trapdoor: 2x2 wooden lid (toy box lid) with hinges; open frame = dark hole; blinking warning stripes.
- Pendulum: a toy rail on two stands with a hanging weight (a wooden ball), the weight a separate sprite
  drawn at BobX/BobY (`View/TrapView.cs`, like CraneView).
- Units falling: shrink+fade in the view (no sim state needed; the view sees the unit disappear
  on a trapdoor tile → plays a short falling sprite).

### Bot
- Its armies get walls/traps for free via the flow fields (they go round, golems break through).
- Building walls/traps itself: last and optional (e.g. a wall in front of the door with a gap + a pendulum in the gap
  once it sees golems). The golden checksum changes then, deliberately.

## Test loop
- `task test -- "Wall|Trap|FlowField|Fighting"`; the golden checksum must be **unchanged** through steps 1–5
  (the bot doesn't build walls/traps). Change it only in step 8 if the bot starts building them.
- New `tests/.../WallTests.cs`, `TrapTests.cs` with Scenario: `Place(BuildingType.BrickWall, …)`, a line of walls
  across the hall, `Spawn` soldiers/golems, `WorldRunner.Until`.
- Visual: `PROBE_SCENE=walls` (a wall line with a gap, a fence, a trapdoor, a pendulum, a few enemies walking in).
- `task check` before every commit; work on the `walls` branch, PR to main.

## Steps
- [x] 0. Branch `walls` from main (garden-map merged first, or branch from garden-map if it isn't merged yet).
      Read FlowField, Fighting (TickUnit/TryStep/SelectTarget), Separation, World.MarkChanged/RebuildFieldsIfDirty.
- [x] 1. **Refactor (no behaviour change):** `World.BlocksUnit(owner, x, y)` used by FlowField.IsWalkable,
      TryStep, Separation.Shift and the worker/scout predicates; `FlowField.Build` takes a cost rule (`WallCost`).
      Golden unchanged.
- [x] 2. **Brick wall + gate sim:** BuildingType.BrickWall, BuildingType.Gate (enum last), BuildingRules (cost, health 120, build time,
      Buildable), `IsWall`; enemy walls block steps; army field with SealedWallPenalty; golem field with
      GolemWallPenalty + `_golemFields`; SelectTarget skips walls; TickUnit attacks the wall when NextTile is
      a wall (contact reach, Punch); WallDamagePercent per unit type. Tests:
      - soldiers walk round a wall with a gap and never hit it;
      - golems break through when the way round is long, and go round when the gap is close;
      - exactly 5 golem punches per brick;
      - a sealed ring round the core → soldiers break through at the thinnest place;
      - own units can't pass their own walls but go through their own gate; enemies treat the gate as a wall;
      - own units never attack their own walls; a factory enclosed without a gate → units wait, `IsEnclosed` true;
      - units on a tile when a wall completes are pushed out to free floor;
      - scouts/workers (own and enemy) go round, own workers use the gate;
      - the flow field is rebuilt when a wall falls; deterministic (checksum with walls).
- [x] 3. **Electric fence:** BuildingType.ElectricFence, cost with batteries, power per zap (`TryDrawPower`), zap
      the attacker on contact with a cooldown, nothing without power, counter-armour decision. Tests: zaps and
      draws energy; no power = no zap; the RC car takes more; a golem dies against a powered fence line
      (balance: how many fences per golem, written as a measurement).
- [x] 4. **Facing-dependent size:** `BuildingRules.Size(type, facing)` + every call site (Sim, UI, View, bot);
      tests that every building except the pendulum is unchanged and that 4x1 ↔ 1x4 with R. Golden unchanged.
- [x] 5. **Traps sim** (`Sim/Traps.cs`): TrapStats, Trapdoor (cycle, enemies fall, own units safe),
      Pendulum (BobX/BobY, push via Unit.PushX/Y/Ticks through TryStep, golems half as far, small damage);
      SelectTarget skips traps, the flow field charges traps as floor; decide trap health/wear;
      checksum (Phase, push). Tests:
      - the trapdoor only takes units that are on it while it's open;
      - the cycle times are right;
      - the pendulum pushes in the swing direction at both ends;
      - a push stops at a wall/obstacle;
      - pendulum + trapdoor combo kills;
      - own units are unaffected.
- [ ] 6. **UI:** menu category Försvar (BuildHotkeys), Texts, InfoRows, click–click wall line (DragPath/
      planner variant, pure helper + tests), previews (pendulum swing path, fence coverage), toasts.
- [ ] 7. **Art + views:** tools/art/defense/*.py (brick wall with damage states and joined pieces, fence,
      trapdoor closed/warning/open, pendulum stand + weight), restyle, sheet review; `View/TrapView.cs`
      (weight interpolated, trapdoor state), falling animation, spark; shadows (walls throw short shadows,
      the trapdoor lies flat); `PROBE_SCENE=walls`, screenshot.
- [ ] 8. **Bot (optional):** a wall with a gap in front of the door + a pendulum/trapdoor in the gap when it sees
      golems/many soldiers; the golden checksum changes deliberately; `task test:report` (balance:
      the bot's matches must still end within 15–25 min).
- [ ] 9. `task mp:smoke` (new state in the checksum), CLAUDE.md (Design: walls/traps, plan listed), `task check`,
      push, PR link.

## Open questions (default chosen if nobody answers)
1. ~~Should own walls block own units?~~ **Decided: yes, with a gate for own units.**
2. Do ranged units hit walls from a distance or at contact? **Default: at contact (punch), so the fence's
   zap matters.**
3. Does the trapdoor kill golems too? **Default: yes, everything that is on it.**
4. Can traps be destroyed? **Default: units don't see them; they wear out (N catches) and then break.**
5. Do traps cost ongoing material (gears/springs via belt per trigger) or only build cost?
   **Default: only build cost.**

## Log
- Step 0: branch `walls` from garden-map (not merged into main yet). Baseline 430/430.
- Steps 1+2 (one commit): Sim/Walls.cs (WallStats, Wall for brick wall + gate; `Blocks(player)` = built and not the
  player's own gate). FlowField: `_open` (floor without a built wall) for the no-corner-cutting rule; own walls
  impassable, enemy walls + wallPenalty (army SealedWallPenalty 1e6, golems GolemWallPenalty 80), overflow guard.
  World: `_golemFields`, `FieldFor`, `BlocksUnit`/`WallBlocks`; MarkChanged dirties every player's field for walls.
  TryStep/Separation/worker+scout paths use BlocksUnit (worker paths are dropped when a step is refused); TryStep
  refuses diagonal slips between built walls only (map walls unchanged → golden unchanged). SelectTarget skips
  walls; TickUnit punches the wall when NextTile is an enemy wall within HitReach (`Attack` helper). WallRaised
  (builder finishes a wall): fields dirty, units on it pushed to the nearest free tile.
  Deviation: no separate WallDamagePercent: walls take the usual building damage (golem 24 = 5 punches,
  soldier 4 = 30 hits); simpler and already balanced by BuildingDamagePercent. WallTests 11, golden unchanged.
- Step 3: ElectricFence = a third Wall type (health 120, cost 4 bricks + 2 plastic + 2 batteries). `Wall.HitBy` when
  a unit's punch is launched at it: powered (TryDrawPower 300 = 40 zaps/battery) and off cooldown (20 ticks) →
  10 damage to the attacker, double to electronics (no new DamageKind). NoPower flag in Tick. Zaps/cooldown hashed.
  Measured: a soldier dies after 3 zaps; a golem pays 5 zaps (50) per fence, so 3 fences in a row kill one.
  Shots still fly over walls (a soldier next to the fence shoots a pylon 3 tiles behind it).
- Step 4: `BuildingRules.Size(type, facing)` (north/south swap width and height; `Size(type)` = facing east).
  Building ctor, CheckLocation/CheckPlace/CanPlace (facing, default East), TryPlace, bot steps, BuildController
  ghost/sent previews use it; BeltPlanner and Shadows read the building's own Width/Height. Golden unchanged.
  The 4x1 ↔ 1x4 test comes with the pendulum in step 5 (every building so far is square).
- Step 5: Sim/Traps.cs: Trap base (Phase from completion, hashed; OutputsToward none), Trapdoor (2x2, period 160:
  warning 30 then open 20; enemies whose tile is on it fall = Health 0), Pendulum (4x1 / 1x4: Swing 0..1000 over 30
  ticks, pause 10, back; Bob in sub-tiles; enemies within 0.6 tile get Unit.PushX/Y/Ticks = 3 tiles over 8 ticks,
  golems 4 ticks, 3 damage). World: TickPush instead of acting (TryStep, so walls/obstacles stop it; keeps facing);
  push state only hashed while pushed (golden unchanged); traps wear (trapdoor 10 per catch = 12, pendulum 5 per hit
  = 20) via `WearTrap`, broken traps destroyed after the building loop. SelectTarget skips traps, FlowField
  charges them as floor. Costs: trapdoor 10 bricks + 4 gears + 2 springs, pendulum 10 bricks + 3 gears + 4 springs.
  Scenario.Rich now also gives gears and springs (one storage test empties gears first). TrapTests 8.
