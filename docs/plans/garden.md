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
- [ ] 1. Sim: MapTheme, garden obstacle kinds and sizes, CreateDefault/CreateMatch take the theme; tests
      (same shape as the room, garden obstacles only, reachable, mirrored, a bot builds on it).
- [ ] 2. Net + settings: MatchSettings(Seed, Theme) for world creation, Start carries the theme (protocol 2),
      MenuModel map list, MenuSettings.Map, LaunchArgs --map; tests.
- [ ] 3. Art: grass, path, flower bed wall, and the five obstacles (tools/art/garden.py), restyled; sheet.
- [ ] 4. View: GardenTiles.tres, MapView by theme (path in the hall), ObstacleView kinds; VisualProbe
      PROBE_MAP; screenshots of both maps.
- [ ] 5. Menu: "Karta" on the local and host pages (remembered), MatchSetup.Theme, the menu background on
      the chosen map; mp:smoke with --map garden.
- [ ] 6. CLAUDE.md, `task check`, push, PR link.

## Log
