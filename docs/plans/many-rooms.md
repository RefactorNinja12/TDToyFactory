# Plan: a second map layout of many small rooms (a long, winding way to the enemy)

Goal: besides today's layout (two home rooms + a straight hall), a layout **"Många rum"**: small rooms with resources
between the bases, where the way goes up and down and meets in the middle. It works with both themes and online.

## Approach (decided; open questions at the end)
- **Same map size and home rooms** (160x62, rooms 60x60, the home doors at x 61/98, y 29..32), so the bot's base modules
  and belts fit as they are. **The hall is replaced by small rooms** in the middle region (x 62..97, y 1..60).
- **The way to the enemy is not straight**: out of the home door into a small entry room, then **up or down** through
  two rooms, across into the middle column, and back towards the middle, where **both sides meet in the centre room**.
  The same again on the enemy's side. A longer way with more variation, and two ways round (north and south).

```
 x: 62-68  69  70-78 79-80 81-89  90  91-97        (# = wall, D = door)
  y 1-12  [ L1 ]#D#[ C1L ] ## [ C1R ]#D#[ R1 ]       top row: from the side rooms into the middle column
     13    D        D              D        D
 y 14-24  [ L2 ]   [ C2L ] ## [ C2R ]   [ R2 ]
     25    D        D              D        D
 y 26-35 D[ LM ]  #[    centre room   ]#  [ RM ]D    LM/RM = entry rooms (the home door), no door to the centre
     36    D        D              D        D
 y 37-47  [ L4 ]   [ C4L ] ## [ C4R ]   [ R4 ]
     48    D        D              D        D
 y 49-60  [ L5 ]#D#[ C5L ] ## [ C5R ]#D#[ R5 ]
```
  - Left: home door → LM → L2 → L1 → C1L → C2L → **centre**, or LM → L4 → L5 → C5L → C4L → **centre**. The right is the
    mirror image. Doors are 4 tiles wide, in the middle of their wall.
  - The middle column is split in two (walls at x 79..80) above and below the centre, so the sides only meet in the
    centre room.
- **`MapLayoutKind { TwoRooms, ManyRooms }`** (Sim), separate from the theme: `MapLayout.CreateDefault(obstacles, seed,
  theme, layout)`, `World.CreateMatch(..., layout)`. `TwoRooms` builds exactly today's map (**golden unchanged**).
- **Zones:** all the small rooms are contested ground, `Zone.Hall` (both may build).
- **Resources:** a cluster in every small room, mirrored left/right. On each side: L1 bricks, L2 plastic, L4 plastic,
  L5 bricks, C1L/C5L cheese, C2L/C4L batteries; the centre room gets the big battery field worth fighting over.
- **Obstacles:** none in the small rooms (too narrow); the home rooms keep theirs. Every floor tile stays reachable.
- **Bot:** its armies follow the flow fields, so they find the long way by themselves. Its growth counts the side
  rooms nearest it (x < 70) as its own ground (extractors there).
  - **Early defence at the exits (decided by the user), so an early win is harder.** A ManyRooms module
    "utgångsförsvar": wall layers across both exits of its entry room LM, with towers right behind them.
    - Layer 1 in the door gaps themselves (y 25 and y 36, x 64..67).
    - Layer 2 across the side rooms just outside (L2 at y 24, L4 at y 37, x 62..68).
    - **Every layer leaves a small opening (2 tiles), at alternate ends from one layer to the next (decided by the
      user).** The enemy never finds a sealed base, so its mice walk the opening instead of beating on the walls, and
      several layers make a **labyrinth**: a zig-zag in front of the towers. Golems still smash through when that is
      shorter than the zig-zag (as today).
    - **And gates in the middle of every layer (decided by the user)**, lined up straight, so the bot's own waves go
      straight out through all the layers (an own gate is open floor to its own units) while the enemy, for whom a
      gate is a wall, has to walk the zig-zag.
    - Two towers in LM by each exit (foam ← plastic from L2/L4, catapult ← bricks), fed through the growth helpers
      (`Feed`/`Route`, searched deposits), on the grid (`EnsurePower`).
    - It comes right after "frontförsvar" (`Ready` ~3 minutes in), and **at once** when it sees the first enemies
      (`Emergency`).
    - Growth on ManyRooms puts its wall layers here too (instead of the hall line): layer 3 in L2/L4 one row further
      out, electric fence once there are batteries to spare. Its towers go into LM (anchor between the exits).
  - **TwoRooms gets the same early defence too (decided by the user: early walls and towers make the bot a little
    better).** The module "utgångsförsvar" there builds the first wall layer across the hall (x 70, y 25..36, with its
    opening and a gate in the middle) and two towers behind it (the growth's tower anchor) at ~3 minutes, or at once when enemies come. The
    growth's later layers follow, now also with openings at alternate ends (y 25..26, then y 35..36, ...) as well as
    the gates (y 30..31): the hall becomes a labyrinth for the enemy and a straight road for the bot's own army.
  - The hall pylon chain ("frammaster") is planned only where it can stand (bad steps are skipped, as today).
- **Art/view:** the rooms are ordinary floor/walls in the tileset (Nursery boards, Garden lawn + hedges); check floor
  decor, clover, the minimap.
- **Menu/online:**
  - the start menu gets a second choice, **"Planlösning"**: "Två rum" / "Många rum". It is saved like the map choice
    (`MenuModel`);
  - the host sends it in Start (`MatchSettings.Layout`, protocol version +1);
  - `--layout many-rooms` on the command line (`LaunchArgs`); `mp_smoke.ps1 -Layout`.

## Test loop
- `task test -- "Map|Garden|Menu|Net|Rooms"`; golden unchanged throughout (the default layout is untouched).
- New `RoomsTests`:
  - the rooms exist with the doors of the drawing;
  - no straight way: the shortest walk between the home doors goes through the centre room and is much longer than
    TwoRooms' hall;
  - every floor tile is reachable from both cores;
  - the deposits are inside the rooms and mirrored;
  - obstacles keep a ring and reachability (several seeds);
  - a placement in a room is allowed for both players;
  - TwoRooms = today's map, tile for tile.
- Slow: bot vs bot on ManyRooms ends within 25 min with a healthy economy (reuse BotVsBot with the layout).
- `task mp:smoke` with `-Layout many-rooms` (checksums equal). Visual: `PROBE_LAYOUT=many-rooms` + minimap screenshot,
  both themes.

## Steps
- [x] 1. Sim: MapLayoutKind, the rooms + doors (Zone.Hall), deposits, obstacles allowed area; CreateDefault/CreateMatch
      parameter; RoomsTests. Golden unchanged.
- [ ] 2. Net + menu: MatchSettings.Layout (protocol), ClientSession/HostSession, MenuModel (choice, saved, LaunchArgs
      `--layout`), MainMenu option, MatchSetup; Net/Menu tests; mp_smoke `-Layout`.
- [ ] 3. View check: tileset walls/floors round the new rooms in both themes, floor decor/clover, minimap; VisualProbe
      `PROBE_LAYOUT`; screenshots; fix what looks wrong.
- [ ] 4. Bot exit defence on ManyRooms: the module (wall layers with alternating openings + gates in a straight line + towers, early/emergency), growth's wall layers and
      tower anchor per layout. Tests: every layer has its opening (alternate ends) and a gate in line; its own units go
      straight through the gates (their way out is no longer than without walls); an
      enemy army walks the zig-zag without hitting a wall (soldiers) and takes clearly longer than without walls, towers stand within range of the exits; the same early defence on TwoRooms
      (hall). The golden checksum changes only if the module lands within its 2 minutes (then deliberately); the bot
      should do at least as well as before in `task test:report` (bot vs bot and vs an idle player still end in time).
- [ ] 5. Slow test bot vs bot on ManyRooms; `task test:report`; `task mp:smoke -Layout many-rooms`.
- [ ] 6. CLAUDE.md (Karta: the layouts), `task check`, remove this plan, push.

## Open questions (default chosen if nobody answers)
1. Should both ways (north and south) be open, or should one be longer? **Default: both alike (mirrored up/down).**
2. ~~Should the bot wall its exits on ManyRooms?~~ **Decided: yes, early, with towers right behind.**
3. ~~Early walls on TwoRooms too?~~ **Decided: yes, early wall layers and towers on both layouts.**

## Log
- Step 1: MapLayoutKind {TwoRooms, ManyRooms}, MapLayout.Layout; Sim/MapRooms.cs (partial): SmallRooms/CentreRoom/
  SmallRoomDoors as in the drawing, mirrored; the home doors open into the entry rooms; the middle region is Zone.Hall;
  AddSmallRoomDeposits (a cluster per side room, none in the entry room, the battery field in the centre); the hall's
  deposits moved to AddHallDeposits (TwoRooms only). Obstacles needed nothing (only ever in the home rooms).
  CreateDefault/CreateMatch/Scenario.Match get a layout parameter. RoomsTests: the shortest door-to-door walk is ≥3x the
  hall's and goes through the centre, no door from the entry room to the centre, mirrored, all floor reachable (also
  with obstacles, several seeds), small rooms shared ground, a soldier finds the long way. Golden unchanged.
