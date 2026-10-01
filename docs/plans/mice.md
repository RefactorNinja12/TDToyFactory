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
- [x] 2. Mouse workers: sprites builder/farmer/scout (workbench, same size/facing as now: 64x64 facing east), Texts names. Screenshot.
- [x] 3. Cheese: ResourceType.Cheese + deposits (mirrored, kept clear of obstacles), ItemType.MeltedCheese, BuildingType.CheeseMelter (extractor), costs/Texts/menu, sprites (deposit tile, item, melter). Tests: melter only on explored cheese, produces melted cheese, deposits mirrored.
- [x] 4. Kitchen: second recipe (melted cheese). Tests: cooks from cheese alone, from crops alone, crops first, InfoRows shows both.
- [x] 5. Treadmill + cheese hunter (Sim): BuildingType.Treadmill (UnitFactory variant needing a builder), builder called + consumed, UnitType.CheeseHunter def, melee attack on units and buildings. Tests: needs both cheese and a builder, builder walks in and is gone, hunter spawns, hunts units and hits buildings, starvation pause (no power needed), determinism.
- [x] 6. UI/vision: Texts, InfoRows for treadmill (cheese, builder on the way), light radius, minimap, menu tab, checksum fields. Tests.
- [x] 7. Sprites: treadmill (wheel with cheese on a stick), cheese hunter mouse (headband, fists), cheese melter, melted cheese item, cheese deposit. Screenshot.
- [x] 8. Bot: cheese module + treadmill. Tests: bot builds them and gets hunters; slow bot matches still healthy.
- [x] 9. Balance (slow guards: hunter vs soldier duel, cheese food rate) + CLAUDE.md; commit, push, PR link.

## Log
- Step 1: ObstacleKind.MouseTrap (3x2, last in RoomToys), sprite by tools/art/make_mousetrap.py (wood board, snap bar, springs, cheese on the trigger), ObstacleView. 10 toys. 211/211.
- Step 2: mouse workers (workbench 6122d711.. builder hard hat, feae4cc6.. farmer straw hat, 1d06a7f5.. scout pith helmet + binoculars) replace tools/art/source/Units/{builder,farmer,scout}.png, restyled; Texts: Byggarmus/Bondmus/Spejarmus, toolbox text. Screenshot ok (small, same 40 px size as before).
- Step 3: ResourceType.Cheese deposits (room 50..52,10..12 + hall 72..73,34..35, mirrored; toys moved away from them), ItemType.MeltedCheese (last in enum/Items.All), BuildingType.CheeseMelter (extractor on cheese, 10 brick + 10 plastic), Texts, menu (Produktion), ResourceTiles source 3, minimap Cheese kind. Art: tools/art/make_cheese.py (wedge, puddle, melter = recoloured plastic melter); restyle VIVID set keeps cheese bright yellow. 216/216.
- Step 4: Kitchen.CheeseRecipe (1 melted cheese -> 1 food) with its own Crafter (CheeseCrafter, buffer 5); one pot: finish current, crops first, then cheese; hashed; InfoRows shows both ("eller"); Texts. 219/219.
- Step 5: Sim/Treadmill.cs (cheese crafter 2/hunter, HasTrainee, no power, starving pause), World.CallBuilder (nearest idle builder, never the last), TickBuilder walks a called builder in and uses it up (Health 0). UnitType.CheeseHunter (hp 20, dmg 3/16 ticks, range 1 tile, speed 28, sight 6, light 4). Melee = Punch when Range <= 1.5 tiles (golem unchanged). Placeholder art: treadmill = toolbox, hunter = builder (step 7). 226/226.
- Step 6: InfoRows Treadmill (needs cheese, trainee/on the way/waiting for a free builder/waiting for cheese, no power). Texts/menu/light/checksum were done in step 5. 227/227.
- Step 7: workbench cheese hunter 7d4ca28a.. (mouse, red headband, boxing gloves), treadmill c8c60519.. (belt with slats, rails, rollers, panel, cheese on a string), replacing the placeholders; cheese melter/item/deposit came in step 3. 227/227.
- Step 8: bot module "ostjägare" right after "soldater" (army +2 min): treadmill 53,10 + melters 52,10 / 52,11 filling it directly (normal module growth stalls after golems for lack of bricks, so it had to come early). CallBuilder now takes the nearest busy builder if none is idle (bot builders are never idle). Bot vs bot: hunters 5 / 3, p0 wins 703 s; guard >= 1 hunter each. 228/228.
- Step 9: slow Balance_OneCheeseMelter_KeepsAKitchenCooking (20 food/min, guard >= 15); hunter < soldier is a fast test. CLAUDE.md (Möss och ost). PLAN DONE.
