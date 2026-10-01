# Plan: mice, cheese and cheese hunters (+ a mousetrap)

Branch: `mice` (from `obstacles`, which isn't merged yet). **Read this file first when resuming**, continue
with the first unchecked step, tick + log + commit + push per step. Loop: tests first with the Scenario
helpers, `task test -- <Area>`, `task check`, commit (Co-Authored-By line), push. Godot only for
screenshots. Edit files with the Edit tool or Python with `newline=""` (CLAUDE.md is CRLF: Edit tool).
Art: workbench drawings or small generator scripts (tools/art/make_*.py) into tools/art/source/, then
`python tools/art/restyle.py` (palette + black outline).

## Design (from the user's request; * = my default, the user didn't object; treadmill/hunter confirmed)
- **Mousetrap** obstacle: a big wooden mousetrap lying flat on the floor (wooden base, spring bar, a
  piece of cheese as bait). `ObstacleKind.MouseTrap`, *3x2 tiles, one per room (appended last in
  RoomToys so the other toys keep their places). *Only an obstacle (it doesn't snap units).
- **Workers are mice** with different hats (sprites only; stats unchanged): builder mouse with a
  yellow hard hat, farmer mouse with a straw hat, scout mouse with an explorer hat (+ binoculars).
  Names: "Byggarmus", "Bondmus", "Spejarmus".
- **Resource cheese**: `ResourceType.Cheese` deposits (*a small patch in each room outside the base and
  battery corner, mirrored, *and one in the hall), found under the fog like the others.
  **Cheese melter** ("Ostsmältare", like the plastic melter): an extractor on cheese that makes
  `ItemType.MeltedCheese`. Needs explored ground (extractor rule).
- **Kitchen alternative**: food boxes from 2 crops (as now) OR *1 melted cheese. Kitchen takes both,
  cooks whichever it has a full set of (crops first).
- **Cheese hunter** (`UnitType.CheeseHunter`, "Ostjägare"): a mouse trained on a **treadmill**
  ("Löpband", *2x2 building) chasing cheese. The treadmill takes **one builder + melted cheese**:
  with cheese stocked (belt), it calls the nearest idle builder, who walks in and is used up; after the
  training time a cheese hunter comes out. **No power** (user: the training mouse runs it); like the
  other army factories it pauses while starving.
  The hunter fights in **melee**: hits enemy units and buildings. **Low tier, not very strong** (user):
  cheaper and weaker than a soldier (hp 20, 3 damage every 16 ticks, range ~1 tile, a bit faster),
  plastic armour, 1 food/min, not electric, light radius 4. Cost: one builder + 2 melted cheese.
- Bot: cheese melter on its cheese patch feeding a kitchen, treadmill fed with cheese (builders come by
  themselves), hunters as part of the army.

## Progress
- [x] 1. Mousetrap obstacle: kind, RoomToys (last), sprite (tools/art/make_mousetrap.py), ObstacleView. Tests: count, size, still connected.
- [ ] 2. Mouse workers: sprites builder/farmer/scout (workbench, same size/facing as now: 64x64 facing east), Texts names. Screenshot.
- [ ] 3. Cheese: ResourceType.Cheese + deposits (mirrored, kept clear of obstacles), ItemType.MeltedCheese, BuildingType.CheeseMelter (extractor), costs/Texts/menu, sprites (deposit tile, item, melter). Tests: melter only on explored cheese, produces melted cheese, deposits mirrored.
- [ ] 4. Kitchen: second recipe (melted cheese). Tests: cooks from cheese alone, from crops alone, crops first, InfoRows shows both.
- [ ] 5. Treadmill + cheese hunter (Sim): BuildingType.Treadmill (UnitFactory variant needing a builder), builder called + consumed, UnitType.CheeseHunter def, melee attack on units and buildings. Tests: needs both cheese and a builder, builder walks in and is gone, hunter spawns, hunts units and hits buildings, starvation pause (no power needed), determinism.
- [ ] 6. UI/vision: Texts, InfoRows for treadmill (cheese, builder on the way), light radius, minimap, menu tab, checksum fields. Tests.
- [ ] 7. Sprites: treadmill (wheel with cheese on a stick), cheese hunter mouse (headband, fists), cheese melter, melted cheese item, cheese deposit. Screenshot.
- [ ] 8. Bot: cheese module + treadmill. Tests: bot builds them and gets hunters; slow bot matches still healthy.
- [ ] 9. Balance (slow guards: hunter vs soldier duel, cheese food rate) + CLAUDE.md; commit, push, PR link.

## Log
- Step 1: ObstacleKind.MouseTrap (3x2, last in RoomToys), sprite by tools/art/make_mousetrap.py (wood board, snap bar, springs, cheese on the trigger), ObstacleView. 10 toys. 211/211.
