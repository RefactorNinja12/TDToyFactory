# Plan: big toy obstacles (teddy bear, ABC blocks, rag doll)

Branch: `obstacles` (from `main`). **Read this file first when resuming**, continue with the first unchecked
step, tick + log + commit per step (same loop as docs/plans/fog.md: tests first, `task test -- <Area>`,
`task check`, commit, push).

## Design (from the user's request)
- Big toys lying in the rooms as obstacles: **teddy bear** (4x4 tiles), **stacked wooden ABC blocks**
  (3x3), **old-fashioned sewn rag doll** (3x4). Pixel style in the room's colours (restyle palette),
  drawn at a slight angle from above (a bit of the front side shows, the sprite reaches one tile above
  its footprint) without breaking the top-down view.
- They behave like walls for movement and building: `TileType.Obstacle` (not Floor → no walking, no flow
  field, no building, no belts). Light passes them (Vision only stops at `TileType.Wall`).
- Placed at random from a seed (`MapLayout.CreateDefault(seed)`), mirrored to the other room (fair).
  Never: in the base area around the toybox, on or next to deposits, in the door lane (y 26..35 from
  x 20 to the door), in the battery/car corner, in the hall. Each keeps a 1-tile floor ring around it,
  and every floor tile must stay reachable (checked with a flood fill after each one).
- Tests keep their meaning: `Scenario.Match()` = map without obstacles; `Scenario.Match(obstacles: true)`
  and `World.CreateMatch()` (bots, real games) have them. Bot tests + bot matches use obstacles.
- View: floor is drawn under obstacle tiles, `ObstacleView` draws the sprites (above units so units behind a
  toy are partly hidden, under the fog); minimap colour; fog shader outlines them like walls.

## Progress
- [x] 1. Sim: TileType.Obstacle, ObstacleKind/Obstacle, generation with keep-outs + connectivity; World.CreateMatch(obstacles), Scenario opt-in. Tests.
- [x] 2. Sprites (pixelart_workbench) into tools/art/source/Obstacles + restyle.
- [x] 3. View: MapView floor under obstacles, ObstacleView, minimap, fog outline. Screenshot.
- [x] 4. Bot + slow matches on the obstacle map; CLAUDE.md; commit, push, PR link.

## Log
- Step 1: TileType.Obstacle, Sim/MapObstacles.cs (MapLayout partial: ObstacleKind, Obstacle, xorshift seed placement, keep-outs, floor ring, AllFloorConnected). CreateDefault(obstacles, seed), World.CreateMatch(obstacles, seed); Scenario.Match(obstacles: false default); bot tests + bot matches use obstacles. Seed 7: Teddy(35,21) RagDoll(38,54) AbcBlocks(3,9),(26,9) + mirrors. 209/209.
- Step 2: workbench drawings teddy 5091f8f2.. (256x320), blocks d780c616.. (192x256), doll 14e0c1d3.. (192x320) in tools/art/source/Obstacles, restyled into Assets/Sprites/Obstacles.
- Step 3: MapView draws floor under toys, View/ObstacleView (footprint + 1 tile overhang, ZIndex 1, mirrored toys flipped), minimap colour (Minimap.Toy), no fog outline for toys (looked like a box). Screenshot ok.
- Later: sock (5x2, workbench 9c574be6.., redrawn sockier d0eddd95.., then flat one-colour knitted by tools/art/make_sock.py), added last in RoomToys so the others kept their places; minimap unknown = fog navy.
- Later: lollipop (5x3, flat swirl candy + stick, tools/art/make_lollipop.py), last in RoomToys.
- Step 4: bot tests + slow bot matches already on the obstacle map since step 1, all pass (bot vs bot p0 wins 657 s, bot vs idle 354 s, batteries found 130 s). CLAUDE.md updated. PLAN DONE.
