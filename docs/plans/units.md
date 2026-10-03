# Plan: animated units, mice on two legs, the soldier becomes a pirate mouse

Goal: units walk with animated legs instead of gliding. The mice (builder, farmer, scout, cheese hunter)
stand and walk upright on two legs. The plastic soldier becomes a plastic **pirate mouse**. Workers show
what they are doing (hammering, harvesting) and fighters show their attacks. Units and buildings get
**real shadows from the light sources** instead of shadows painted into the sprites.

## Approach (decided unless noted)
- **Upright units are drawn at an angle from the front, not rotated.** This is the same view the
  obstacles already use. A unit looks one of four ways: towards the camera, away from it, right, or left
  (right mirrored). A turn changes the picture instead of spinning the sprite. That is the only way legs
  read as legs, and hats and faces stay the right way up. Upright: the four mice, the pirate mouse and
  the brick golem (stomping). The **RC car stays top-down and rotates** like now (a vehicle can turn on
  the spot); it gets turning wheels and a wobbling antenna instead.
- **One sprite sheet per unit:**
  - Rows: towards, away, side.
  - Columns: idle, 4 walk frames, 2 action frames (work or attack).
  - Size: 48x48 px per cell; the golem is bigger.
  - A separate picture `Units/<name>.png` (idle, facing the camera) stays for the UI icons.
- **Animation state is pure C#** (`Scripts/UI/UnitAnimation.cs`, tested):
  - Facing: from the direction of movement, with hysteresis so a unit walking diagonally doesn't flicker
    between two pictures.
  - Walk frame: from the distance walked (feet match the speed; a crawling empty golem steps slowly).
  - Idle: after standing still for a moment.
  - Action frames: while the unit attacks (from its attack timer) or works (builder at a construction
    site, farmer at a field).
- **Drawing stays fast:** still one MultiMesh per unit type (one draw call however many there are). Each
  instance carries its cell (row, column, mirror) in the MultiMesh custom data, and a small canvas shader
  picks that cell from the sheet. The team tint stays the instance colour. Units are sorted by y so the
  ones in front cover the ones behind. The sprite stands on its feet at the unit's position, with a
  small shadow.
- **Art is drawn by code** (PixelLab is out of generations), like the buildings:
  - The kit gets an upright mouse (`kit.upright_mouse`): big head with round ears, body, two legs, two
    arms, tail, a hat per job, and something in its hands.
  - The pose (legs, arms, bob) is computed per frame.
  - Scripts live in `tools/art/units/`; `restyle.py` gives the palette and outlines as before.
- **Pirate mouse:**
  - Looks: a red bandana or a tricorn hat, an eye patch, a striped shirt and a flintlock pistol.
  - Attack frames: aim, then a puff from the barrel. It is still made of plastic, so it is still weak
    against area damage.
  - Its name becomes "Piratmus" (in `Texts`); the type in the code stays `PlasticSoldier`.
  - The soldier factory's press stamps little plastic pirates on the sprue instead of army men.
- **Shadows are computed, never painted into sprites** (a painted shadow turns with the sprite):
  - A unit or building in reach of a light gets a soft round shadow at its feet / the bottom of its footprint.
  - It never turns with the sprite. **No light, no shadow**: in the dark there is none at all.
  - Light pushes the shadow the other way. The lights are the same list the fog glow uses
    (`UI/LightSources`: toybox, lamps, pylons, chargers, shots, crowds of units).
  - Only lights that reach count: within their radius and with a clear ray. Walls block light (the
    same Bresenham rays as vision); the big toys don't, as in the fog design.
  - A near light gives a short, dark shadow; further away the shadow gets longer and fainter. Several
    lights add up by strength; two lights from opposite sides give a short, round shadow.
  - It changes smoothly: the direction and length ease over time, so passing shots and walking units
    don't make the shadows twitch.
  - The model is pure and tested: `Scripts/UI/Shadows.cs` gives each thing an offset, an angle, a
    stretch and an opacity.
  - The view `View/ShadowView.cs` draws two MultiMeshes of one generated soft disc, under the
    buildings and units and over the floor. Things hidden by fog have no shadow.
  - Painted shadows in the current sprites are removed (the units' ovals); the new sprites are drawn
    without them.
- The treadmill view runs the builder mouse with its side walk frames (no longer a rotated top view).

## Test loop
- `task test -- "UnitAnimation|Shadows"` (shadows: no light → no shadow, light to the west → shadow
  east, further → longer and fainter, a wall in between → the light doesn't count, opposite lights → short;
  facing, hysteresis, walk frames from distance, idle, action states).
- Art: `python tools/art/units/<name>.py && python tools/art/restyle.py <files>`, then
  `tools/art/sheet.py` (whole sheets at 1x + 2x). The walk cycle is checked as a strip of frames in one image.
- In game: VisualProbe (`PROBE_CAM`) a few frames apart, cropped; `task build` for the view.
- `task check` before every commit; the golden checksum must not change (nothing in the simulation changes).

## Steps
- [x] 0. Branch `units` (from `sprites`). Baseline sheet of the 7 current unit sprites.
- [x] 1. `UnitAnimation` (pure): facing with hysteresis, walk frames from distance, idle after 0.3 s, action
      frames from the attack timer / work state (read what Unit exposes; add read-only properties to the
      sim only where needed). Tests for each.
- [x] 2. Sheet layout (pure): `UnitSheets` (cell size, rows, columns per type, which types are upright);
      a test that every sheet and icon file exists at the right size (Support/ArtFiles).
- [x] 3. Art kit: `upright_mouse(pose, facing, hat, outfit, held)` with a walk cycle (contact, passing ×2),
      arm swing, a 1 px bob, the tail swinging the other way; `tools/art/units/sheet_strip.py` for review.
- [x] 4. The four worker mice: builder (hard hat, hammer; action = hammering), farmer (straw hat, basket;
      action = picking), scout (cap, binoculars), cheese hunter (headband, boxing gloves; action = punches).
- [x] 5. Pirate mouse (bandana/tricorn, eye patch, striped shirt, flintlock; action = aim + puff); `Texts`
      name "Piratmus"; soldier factory sprue redrawn with pirate figures.
- [x] 6. Brick golem upright (stomping walk, swinging fist), RC car (wheel frames, antenna wobble, still rotated).
- [x] 7. Shadows (pure): `Shadows.For(world, player, lights)` per unit and building (offset, angle, stretch,
      opacity; no reaching light = no shadow; lights within their radius and with a clear ray; summed by strength;
      eased over time); lights in a tile grid so it stays cheap with hundreds of things. Tests as above.
- [x] 8. Shadows (view): `ShadowView` (a generated soft disc, one MultiMesh for buildings, one for units, under
      both, hidden in fog); painted shadows removed from the old sprites. Screenshot: shadows lean away from
      a lamp, none where it's dark.
- [x] 9. View: UnitView with sheets (custom data + shader, y-sort, feet anchor, shadow), RC car rotated as before;
      TreadmillView with the side walk frames; UI icons from `Units/<name>.png`. `task build` + screenshots.
- [x] 10. In-game pass: speeds (feet don't slide), readability at normal zoom, fog/minimap unaffected,
      performance with a full army (still one draw call per type).
- [x] 11. CLAUDE.md (theme: pirate mice; upright units; computed shadows; the art scripts), `task check`, push.

## Log
- Step 0: baseline: all 7 are top-down and turned; the mice crawl on four legs with a painted oval shadow under them (it turns with them).
- Step 1: UI/UnitAnimation.cs: facing from MoveX/MoveY (walk or aim direction), hysteresis 1.25 near diagonals; walk steps from the distance walked (4 steps per tile); idle after 4 still ticks; act for 6 ticks after a shot/punch (AttackCooldown, internal = visible to UI) or while working (builder facing its unfinished site, farmer WorkTimer > 0), hammering in 5-tick beats; units seen for the first time count as moving. Tests with real scenarios (walking soldiers both ways, a shooting soldier, a builder walking -> hammering -> idle at a warehouse).
- Step 2: UI/UnitSheets.cs: 7 columns (idle, walk x4, act x2) x 3 rows (towards, away, side; left = mirrored), 48 px cells (golem 64), feet 6 px above the cell bottom; RC car = vehicle (turned, 2 wheel frames). The sheets-exist test comes with the art (step 6).
- Step 3: kit.upright_mouse(facing, pose, step, hat, shirt, held, act, extra, stripes) + unit_sheet(cell_fn, columns, cell): chibi mouse (big head, round ears, belly, snout and whiskers from the front, profile from the side, ears and back from behind), walk cycle (foot lifts, 1 px bob, arm swing, side stride 4 px), tail behind or in front. No painted shadow. Lessons: 2 px lines become all outline (tails/feet need 3 px); back-view ears need a darker fur or they melt into the head. The review is tools/art/sheet.py plus a 2x composite (a separate strip tool was not needed).
- Step 4: tools/art/units/mice.py: builder (hard hat, orange vest, hammer: up/down), farmer (straw hat, blue overalls, carrot: down/up), scout (green cap, binoculars to the eyes), cheese hunter (headband, two boxing gloves: wind up/hit).
- Step 5: tools/art/units/pirate.py: tricorn with a skull badge, eye patch, red/white striped shirt, belt with a gold buckle, flintlock (hangs down; aim; puff). Texts: Piratmus, Piratfabrik (descriptions and the catapult hint updated). Soldier factory sprue: little plastic pirate mice with tricorns.
- Step 6: tools/art/units/golem_car.py: brick golem 64 px (stud bricks, yellow eyes, stomping 4 px lifts, fist raised/slammed); RC car 48 px from above, 2 frames (treads move, antenna tip wobbles). UnitSheets.FileName/SheetSize + test: every unit has its sheet at the right size and an icon (PNG header via Support/ArtFiles). Old Units/soldier.png goes in step 9 with the view switch.
- Step 7: UI/Shadows.cs: Shadows.Cast (lights within radius, not closer than 0.4 tiles = the thing's own torch/glow, ray not through walls via the new Vision.LightReaches; weight = strength x nearness, stretch grows with distance; summed, so opposite lights give a short round shadow; null when the summed light < 0.03 = no light, no shadow), ShadowCaster (every visible unit and shown building, belts/junctions are flat and get none, eased over 0.25 s, fades out when the light goes). The light-grid idea was not needed: a bounding-box filter over the light list is enough. ShadowsTests 7.
- Step 8: View/ShadowView.cs: generated radial soft disc, two MultiMeshes (buildings, units), inserted before the Buildings node (over the floor, under everything standing). VisualProbe PROBE_SCENE=shadows: a lamp on open floor with assemblers and builder mice round it. Seen: shadows lean away from the lamp on every side. The mice still cover most of theirs because their old sprites are centred on the position: fixed by the feet anchor in step 9. Painted shadows go with the old unit sprites in step 9 (obstacles keep theirs: they don't turn).
- Step 9: UnitView: one MultiMesh per type with custom data (column, row, mirror) and a canvas shader picking the cell (gotcha: in Godot 4 the fragment COLOR already holds the texture sampled at UV, so the instance colour goes through a varying); upright units lifted so the feet line (FeetY) sits on the position, sorted by y within their type; the RC car turned as before with its wheel frame. BuildingVisuals.GetUnitTexture/GetUnitSheet from UnitSheets.FileName (old soldier.png removed). Treadmill: the builder runs upright with its side walk frames (mirrored when the treadmill faces west). CombatView: health bars above the head, carried carrots at the side, bullets at hand height (16 px). Screens: the lamp scene shows mice standing on their feet with shadows leaning away from the lamp; a bot match shows pirates and an upright golem walking.
- Step 10: in game: the treadmill mouse runs upright towards the cheese with changing legs; builders stand upright at the toolbox; pirates and the golem walk mid-map; enemy units keep the red tint; shadows lean away from lamps and vanish in the dark. Sim untouched (golden unchanged, all tests green).
- Step 11: CLAUDE.md: theme (piratmöss), upright units, moving building parts, computed shadows, the art scripts and their pitfalls, probe env vars, both plans listed. Done.
- After the plan (user): the mice go back to top-down (turned the way they go, like before) but keep two legs: kit.topdown_mouse (nose east; the trailing foot steps out behind the body, left and right taking turns; idle shows both heels; arms swing; pink ears either side of the head, pink tail wiggling), smaller hats seen from above, one-row sheets (7x1). UnitSheets.UnitLayout: TopDown (mice), Upright (golem), Vehicle (car). The upright mouse code is gone; the pirate is drawn in mice.py with the others.
