# Plan: animated units, mice on two legs, the soldier becomes a pirate mouse

Goal: units walk with animated legs instead of gliding. The mice (builder, farmer, scout, cheese hunter)
stand and walk upright on two legs. The plastic soldier becomes a plastic **pirate mouse**. Workers show
what they are doing (hammering, harvesting) and fighters show their attacks.

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
- The treadmill view runs the builder mouse with its side walk frames (no longer a rotated top view).

## Test loop
- `task test -- UnitAnimation` (facing, hysteresis, walk frames from distance, idle, action states).
- Art: `python tools/art/units/<name>.py && python tools/art/restyle.py <files>`, then
  `tools/art/sheet.py` (whole sheets at 1x + 2x). The walk cycle is checked as a strip of frames in one image.
- In game: VisualProbe (`PROBE_CAM`) a few frames apart, cropped; `task build` for the view.
- `task check` before every commit; the golden checksum must not change (nothing in the simulation changes).

## Steps
- [ ] 0. Branch `units` (from `sprites`). Baseline sheet of the 7 current unit sprites.
- [ ] 1. `UnitAnimation` (pure): facing with hysteresis, walk frames from distance, idle after 0.3 s, action
      frames from the attack timer / work state (read what Unit exposes; add read-only properties to the
      sim only where needed). Tests for each.
- [ ] 2. Sheet layout (pure): `UnitSheets` (cell size, rows, columns per type, which types are upright);
      a test that every sheet and icon file exists at the right size (Support/ArtFiles).
- [ ] 3. Art kit: `upright_mouse(pose, facing, hat, outfit, held)` with a walk cycle (contact, passing ×2),
      arm swing, a 1 px bob, the tail swinging the other way; `tools/art/units/sheet_strip.py` for review.
- [ ] 4. The four worker mice: builder (hard hat, hammer; action = hammering), farmer (straw hat, basket;
      action = picking), scout (cap, binoculars), cheese hunter (headband, boxing gloves; action = punches).
- [ ] 5. Pirate mouse (bandana/tricorn, eye patch, striped shirt, flintlock; action = aim + puff); `Texts`
      name "Piratmus"; soldier factory sprue redrawn with pirate figures.
- [ ] 6. Brick golem upright (stomping walk, swinging fist), RC car (wheel frames, antenna wobble, still rotated).
- [ ] 7. View: UnitView with sheets (custom data + shader, y-sort, feet anchor, shadow), RC car rotated as before;
      TreadmillView with the side walk frames; UI icons from `Units/<name>.png`. `task build` + screenshots.
- [ ] 8. In-game pass: speeds (feet don't slide), readability at normal zoom, fog/minimap unaffected,
      performance with a full army (still one draw call per type).
- [ ] 9. CLAUDE.md (theme: pirate mice; upright units; the art scripts), `task check`, push.

## Log
