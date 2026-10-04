# Plan: an outdoor map (the garden) and choosing the map

Goal: a second map, **Trädgården**. Grass instead of floorboards, flower beds (rabatter) as the borders,
a garden path for the hall, and giant flowers and vegetables lying around instead of giant toys, so it
feels like a place for outdoor play. Players choose the map when starting a game (local, and the host
online; the client gets the host's choice).

## Approach (decided)
- **Same playable shape, new theme.** The bot's plan is laid out on the room's coordinates, so the garden
  keeps the geometry the room has: the two areas, the path between them, the doors, the deposits and the
  base. It differs in what it looks like and in its obstacles. The bot, the balance and all the rules work
  unchanged.
  - `Sim/MapTheme` (Nursery, Garden): `MapLayout.Theme`, `MapLayout.CreateDefault(obstacles, seed, theme)`,
    `World.CreateMatch(obstacles, seed, theme)`.
  - Garden obstacles (new ObstacleKinds, appended), placed by the same seeded, mirrored, bot-safe rules:
    a pumpkin 4x4, a sunflower 3x4, a cabbage 3x3, a giant carrot lying down 5x3, a tulip clump 3x2.
- **Looks** (code-drawn, restyled to the night palette):
  - Grass: one big repeating picture like the floorboards, with blades, clover, a few daisies and faint
    mowing stripes.
  - Path: gravel with stepping stones, in the hall.
  - Wall: a flower bed, a raised wooden planter with soil and flowers that tiles along the borders.
  - The five obstacles are drawn slanted from above with one tile of overhang, like the toys.
  - `MapView` takes its TileSet from the theme: `GardenTiles.tres` has grass, flower bed and path; the
    hall gets the path. `ObstacleView` maps each kind to its picture.
- **Choosing:**
  - `UI/MenuModel`: a list of maps (name, theme); `MenuSettings` remembers the last one.
  - The local page and the host page get a "Karta" option; `MatchSetup.Theme` carries it for local play.
  - The host's `Start` message carries the theme (`Protocol.Version` goes to 2). Sessions create worlds
    from `MatchSettings(Seed, Theme)`, so the client builds the same garden.
  - `LaunchArgs --map garden` for smoke tests. The menu background plays on the last chosen map.

## Test loop
- `task test -- "Map|Garden|Net|MenuModel"`: the garden has the same walls, deposits and zones as the room
  and only garden obstacles, every floor tile reachable, mirrored; a bot builds on it like in the room; the
  host's map reaches the client; the menu settings remember the map; `--map` parses.
- Art: `python tools/art/garden.py && python tools/art/restyle.py <files>`, sheet review; in game a
  screenshot of each map (VisualProbe `PROBE_MAP=garden`).
- `task check` and `task mp:smoke` (protocol change) before committing.

## Steps
- [x] 1. Sim: MapTheme, garden obstacle kinds and sizes, CreateDefault/CreateMatch take the theme; tests
      (same shape as the room, garden obstacles only, reachable, mirrored, a bot builds on it).
- [x] 2. Net + settings: MatchSettings(Seed, Theme) for world creation, Start carries the theme (protocol 2),
      MenuModel map list, MenuSettings.Map, LaunchArgs --map; tests.
- [x] 3. Art: grass, path, flower bed wall, and the five obstacles (tools/art/garden.py), restyled; sheet.
- [x] 4. View: GardenTiles.tres, MapView by theme (path in the hall), ObstacleView kinds; VisualProbe
      PROBE_MAP; screenshots of both maps.
- [x] 5. Menu: "Karta" on the local and host pages (remembered), MatchSetup.Theme, the menu background on
      the chosen map; mp:smoke with --map garden.
- [ ] 6. CLAUDE.md, `task check`, push, PR link.

## Log
- Step 1: MapTheme (Nursery, Garden) in MapLayout.cs, MapLayout.Theme, CreateDefault/CreateMatch take it (default Nursery: golden unchanged). ObstacleKind + Pumpkin 4x4, Sunflower 3x4, Cabbage 3x3, Carrot 5x3, Tulips 3x2 (GardenPlants), placed by the same seeded rules. GardenTests: same walls/deposits/zones as the room, only plants, mirrored, all floor reachable, a bot builds at least 80% as much in 90 s.
- Step 2: Net: MatchSettings(Seed, Theme) (Protocol.cs); sessions create worlds from it; HostSession.Start(seed, theme); Start carries the theme byte (client accepts only known themes); Protocol.Version 2; DesyncReport names the map. MenuModel.Maps (Barnrummet, Trädgården) + MapIndex; MenuSettings.Map (saved as map=Garden, unknown -> Nursery); LaunchArgs --map. Tests: the host's garden is the client's (same checksums), maps list, --map, settings.
- Step 3: tools/art/garden.py: lawn 16x8 tiles (stripes, patches, blades, clover, daisies; strokes over the edge drawn again on the other side, or they become lines across the whole picture), gravel path 4x4 tiles with stepping stones, flower bed tile (wooden planter, soil, eight little flowers), pumpkin (ribs clipped to the body), sunflower, cabbage (explicit yellow-greens: lighter greens mute to teal), carrot lying down, three tulips. All restyled.
- Step 4: Assets/TileSets/GardenTiles.tres (0 lawn 16x8, 1 flower bed, 2 path 4x4); MapView picks the set by MapLayout.Theme and lays the path on the garden's hall floor; ObstacleView reads pictures from a kind -> file table; MinimapView: garden colours (lawn green, flower beds, path in the hall, plants). MatchSetup.Theme (local play and the menu background); VisualProbe PROBE_MAP=garden (children are ready before Game makes the world). Gravel toned down (it outshone the lawn). Screens: lawn under the fog with the pumpkin, flower beds along the room and hall walls, the gravel path with stepping stones; the nursery unchanged.
- Step 5: MainMenu: a "Karta" option (MenuModel.Maps) on the local and host pages, remembered in the settings; choosing one sets MatchSetup.Theme and rebuilds the menu background on that map; the host starts with its map (also --map from the command line). mp_smoke.ps1 -Map nursery|garden; the smoke result now names the map each side played ("600 <checksum> Garden"), so a map that didn't reach the client would fail. MP OK on Garden and on Nursery (same checksum: the checksum doesn't hash the map, and in 30 s the bots only build in their bases, where no obstacle may lie).
