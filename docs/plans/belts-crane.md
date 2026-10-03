# Plan: animated belts (with splitter, sorter, junction to match) and a claw crane on stilts

Goal:
- Conveyor belts look like running belts: the treads move at exactly the speed the items move.
- The splitter, sorter and junction get the same rails and belt, so a line reads as one connected system.
- A **claw crane** (klokran) on a rail on stilts. It reaches over a resource field, picks items from the
  extractors in the middle of the field, and drops them onto an ordinary belt outside the field's edge.

## Approach (decided)
- **Belts are animated by a shader, not by frames.**
  - The sprites are drawn by code (`tools/art/logistics/belts.py`, kit style): rubber belt, side rails,
    rollers, and stubs that line up at every tile edge.
  - One shared shader moves tread lines across the belt surface at the item speed (`Conveyor.Speed` /
    `Conveyor.Length` tiles per tick: 80 px/s): straight along the belt, round the curve's centre in a
    curve. It looks smooth at any frame rate, costs no CPU per belt, and needs no frame strips.
  - The speed comes from a pure helper (`UI/BeltLook.cs`, tested).
- **Splitter, sorter and junction are redrawn** with the same rails and belt stubs on their sides, so
  belts meet them seamlessly:
  - the splitter gets a turning paddle wheel (a BuildingParts part);
  - the sorter gets a flipping arm and a small window showing its filter (the icon already there);
  - the junction gets two belts crossing, both animated by the same shader.
  - They move while items pass (the Activity signature is extended to "held item changed").
- **The claw crane** (`BuildingType.ClawCrane`, Logistik, hotkey G):
  - **The building:** a 1x1 base on stilts at the edge of a field, facing into the field. Its rail reaches
    `CraneStats.Reach` = 5 tiles forward. The rail is only drawn; it occupies no tiles, it stands on
    stilts over whatever is below.
  - **The claw** runs along the rail:
    - it picks the nearest extractor under the rail that has items stored (1 item per trip, a new
      `Extractor.TryTakeOne`);
    - it carries the item back and drops it out of the back of the base (the opposite of its facing)
      onto a belt or into a building;
    - if the output is blocked, it waits holding the item;
    - it moves in sub-tiles at a fixed speed. Everything is integers, part of the checksum, deterministic.
  - No power (a wind-up toy crane).
  - Cost and build time in `BuildingRules`; texts (`Texts`), info rows ("Bär: kloss", reach, how many
    extractors it serves).
  - While placing it, the rail's reach and the extractors it would serve are highlighted.
  - **Art:** the base on stilts, the rail with stilts every tile, the claw (open/closed), the carried
    item hanging under it. `View/CraneView.cs` draws the rail and the claw over the buildings (it is up
    on stilts). The claw position is interpolated between ticks.
  - The bot doesn't use cranes yet (optional later).

## Test loop
- `task test -- "Crane|BeltLook|Activity"`; the golden checksum is unchanged until the crane exists (the
  bot never builds one, so it should stay unchanged after too).
- Art: `python tools/art/logistics/<x>.py && python tools/art/restyle.py <files>`, `tools/art/sheet.py`.
- In game: `PROBE_SCENE=belts` (extended with a splitter, a sorter, a junction and a crane over a field),
  a few frames apart, cropped.
- `task check` before every commit; **check the branch before committing** (work on `belts-crane`, PR to main).

## Steps
- [x] 0. Branch `belts-crane` from main. Baseline sheet of the current belt, curve, splitter, sorter, junction.
- [x] 1. Belt art: straight and curve (rubber surface in one marked colour for the shader, rails, rollers,
      edge stubs), restyled; splitter, sorter, junction redrawn with matching stubs; sheet review.
- [x] 2. `UI/BeltLook.cs` (tread speed in px/s from Conveyor.Speed/Length, tread spacing) + tests; the belt
      shader (straight: scroll along x in sprite space; curve: angle round the corner pivot), a ShaderMaterial on
      every conveyor/junction sprite in BuildingView; curves keep their FlipV/rotation. Screenshot frames.
- [x] 3. Splitter paddle wheel, sorter arm (parts); Activity: splitter/sorter work while their held item
      changes. Tests.
- [ ] 4. Crane sim: ClawCrane building + CraneStats (Reach 5, speed, grab/drop ticks), Extractor.TryTakeOne,
      claw state machine, checksum, BuildingRules (cost, size 1x1, build time, menu). Tests: carries from
      extractors 1..5 tiles in to a belt behind; nearest stocked first; waits when the output is blocked; ignores
      extractors beside the rail and beyond the reach; deterministic.
- [ ] 5. Crane UI: Texts, InfoRows, BuildMenu entry (Logistik, G), placement preview of the reach and served
      extractors (pure helper `Crane.Served(world, x, y, facing)` used by the preview, tested).
- [ ] 6. Crane art + CraneView: base on stilts, rail and stilts over the field, claw with open/closed and the
      carried item, interpolated; screenshot.
- [ ] 7. CLAUDE.md (belts, crane), `task check`, push, PR link.

## Log
- Step 0: baseline: a plain brown strip with arrows, a brown quarter ring, and green boxes with brown stubs that don't match the belt.
- Step 1: tools/art/logistics/belts.py: yellow toy rails with bolts and dark rubber, straight (rails y 14-17/46-49) and curve (the same radii round the corner (0, 64)); splitter (red box, paddle wheel part), sorter (blue box with a window for the filter icon, flap part), junction (north-south belt bridging east-west). restyle: belts now get outlines too (restyle never outlines along a picture's own border, so tiles still join without a seam).
- Step 2: UI/BeltLook.cs (80 px/s = item speed, ridges every 12 px, rubber 18-46) + tests (treads as fast as an item really moves); View/BeltMaterials.cs: one shader per straight/curve, ridges moving along x or round the corner's arc, in sprite pixels (turning/mirroring carry over); set on every conveyor sprite with its look. Junctions stay still (two directions at once). Frames 1/20 s apart show the ridges moving with the items.
- Step 3: OneItemRouter.Held (read-only); Activity signature for splitter/sorter = the held item (changes as items pass, still when jammed); parts: splitter paddle wheel spins, sorter flap swings (corner, clear of the filter icon). BuildingVisuals.TurnsWithFacing: splitters, sorters, junctions never turn - used by BuildingView and now also BuildingAnimator (their parts were turning with the facing).
