# Plan: livelier factory buildings (new sprites + animation)

Goal: the 11 factory buildings get new sprites that tell what they do in the toy world, run by mice,
and they move while they work (and stand still when they don't: starving, no power, full, nothing to do).

## Approach (decided)
- **Sprites drawn by code.** PixelLab is out of generations, so every sprite is a Python script in
  `tools/art/buildings/` built on a small drawing kit (`tools/art/kit.py`: shapes with toy-plastic
  shading, wood/fabric/metal fills, a mouse with a hat). The scripts write to `tools/art/source/`;
  `restyle.py` then applies the palette and black outlines as before. Reproducible and free.
- **Animation = moving parts over a still base**, like the treadmill: the base sprite stays and small
  part sprites (gear, wind-up key, pinwheel, piston, bubble, steam puff, mouse) move on top. No frame
  strips to keep consistent; movement is smooth at any frame rate and can speed up or slow down.
- **The motion lives in pure C#** (`Scripts/UI/BuildingParts.cs`): per building type a table of parts
  (sprite, anchor in sprite pixels, motion: spin, bob, slide along a path, blink, puff) and
  `Pose(part, t)` → offset, angle, scale, alpha. Tested: loops are seamless, nothing leaves the footprint.
  The view (`View/BuildingAnimator.cs`, one node drawing everything, like TreadmillView) only draws it.
- **Working or not** (`Scripts/UI/Activity.cs`): a building counts as working while its progress
  (extractor or crafter) moved within the last half second. Starving, no power, a full output or missing
  input = still (the existing "no power" icon stays). Tested with real scenarios.
- **Only what's seen:** buildings hidden in the fog or off screen are not animated.
- Review with one contact sheet per batch (`tools/art/sheet.py`, all new sprites and parts on one small
  image) plus a short in-game screenshot, to keep the token cost down.

## The buildings (toy world, mice)
| Building | Idea | Moves while working |
|---|---|---|
| Klossutvinnare (brick) | toy digger with a mouse in a hard hat | the arm digs, a brick hops up |
| Plastutvinnare (plastic) | gumball machine sucking up plastic beads | the beads swirl in the dome, the crank turns |
| Batteriutvinnare (battery) | toy magnet crane over battery cells | the magnet bobs, charge bars blink |
| Ostsmältare (cheese) | fondue pot on a toy stove, a mouse stirring | bubbles pop, the spoon goes round |
| Monteringsmaskin (assembler) | wind-up workbench | the big wind-up key turns, gears mesh |
| Kök (kitchen) | play kitchen stove | the lid rattles, steam puffs rise |
| Soldatfabrik (2x2) | plastic-kit press stamping green soldiers on a sprue | the press stamps, a sprue slides out |
| Golemverkstad (2x2) | toy crane stacking ABC blocks | the hook swings, a block drops on the stack |
| Bilfabrik (2x2) | slot-car track with a paint booth | a little car laps the track |
| Verktygslåda (2x2, builder mice) | open toolbox with a mouse hole | a hard-hat mouse peeks out, the saw saws |
| Bondgård (2x2, farmer mice) | dollhouse barn with a pinwheel | the pinwheel spins, a mouse waves from the hatch |

## Test loop
- `task test -- Activity|BuildingParts` (pure, fast).
- `python tools/art/buildings/<name>.py && python tools/art/restyle.py` → `python tools/art/sheet.py` →
  look at one image. In game: VisualProbe screenshot, cropped.
- `task build` for the view; `task check` before each commit.

## Steps
- [x] 0. Branch `sprites` (from `multiplayer`). Baseline contact sheet of the current 11 sprites.
- [x] 1. Activity (pure): `Activity.Track(world)` per tick, `IsWorking(building)`; tests: fed assembler works,
      unfed/unpowered/starving one doesn't, extractor works until full, kitchen/treadmill/unit factory.
- [x] 2. Parts and motion (pure): `BuildingParts` table + `Pose(part, t)` (spin, bob, slide path loop,
      blink, puff rising and fading); tests: periodic, within the footprint, every animated type has parts
      whose sprites exist in `BuildingVisuals`.
- [ ] 3. Art kit + sheet tool: `tools/art/kit.py` (shaded plastic shapes, outlines left to restyle, mouse
      with hats), `tools/art/sheet.py`; restyle handles `source/Parts/`.
- [ ] 4. View: `BuildingAnimator` draws the parts for working, visible buildings (rotation with the
      building, team tint where the base has it); TreadmillView keeps its own special drawing.
- [ ] 5. Extractors + cheese melter (4 sprites + parts), sheet + screenshot.
- [ ] 6. Assembler + kitchen.
- [ ] 7. The three army factories (2x2).
- [ ] 8. Toolbox + farmhouse.
- [ ] 9. Polish pass in game (speeds, sizes, readability at normal zoom), CLAUDE.md, `task check`, push.

## Log
- Step 0: baseline sheet: the 11 are flat coloured squares with one round detail each (extractors 64 px, factories 128 px).
- Step 1: UI/Activity.cs: working = extractor/crafter progress moved within 0.5 s (kitchen: both crafters); first sight = not working; forgets removed buildings. ActivityTests 5 (fed/unfed assembler, extractor until full, no power, kitchen, site).
- Step 2: UI/BuildingParts.cs: Part record (anchor, Motion Spin/Swing/Bob/Slide/Orbit/Blink/Puff, speed, travel, phase), PoseAt, table per type (filled in steps 5-8); AnimationClocks (runs while working, eases in/out over 0.3 s, per-building start offset). Tests: loops seamless, motions as named, every part stays on its building and has Parts/<sprite>.png (Support/ArtFiles).
