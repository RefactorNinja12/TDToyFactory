# Projekt: Leksakskrig (arbetsnamn)

## Koncept
PvP fabrik + tower defense. Två spelare bygger fabriker, försvarar sin
leksakslåda (kärna) och producerar trupper som automatiskt anfaller motståndaren.

## Tema
Leksaksvärld: två barnrum i krig. Resurser: klossar, plast, batterier.
Torn: skumpilar, vattenpistol, katapult. Trupper: plastsoldater,
klossgolems, radiostyrda bilar. Kontringar: plast svag mot område,
elektronik svag mot vatten, klossar svaga mot laser.

## Karta
Två rum (~60x60 rutor) förbundna med en hall (~30–40 rutor).
Dörren är enda ingången i början. Rika resurser i hallen.
Kompakt fabrik, max 3 nivåer i produktionskedjan, matcher 15–25 min.

## Teknik
- Godot 4 (.NET) med C#, 2D top-down, 64x64 rutor
- Simulering separerad från Godot-noder (ren C#)
- Deterministisk lockstep-nätverk: fast tick, heltal/fixed-point, inga floats i logiken
- Trupprörelse: flow field med kostnad per ruta, byggnader = dyra (krossbara), aldrig oframkomliga
- Trupper ritas med MultiMeshInstance2D

## Design (beslutat)
- Leksakslådan (kärnan) = bank för byggkostnader. Allt annat går fysiskt på band:
  fabriker och torn hämtar aldrig direkt från lådan.
- Logistik: transportband, delare, sorterare (senare korsning).
- Produktionskedja (max 3 nivåer): råvara → monteringsmaskin (kugghjul/fjäder/kretskort) → truppfabrik.
- Truppfabriker producerar automatiskt när materialet finns; trupperna går själva mot
  motståndarens leksakslåda.
- Torn fylls med ammunition via band (skumpil ← plast, katapult ← klossar,
  vattenpistol ← batterier, laser ← batterier). Utan ammo skjuter tornet inte.
- Byggkostnader och balans ligger samlat i `BuildingRules` (Scripts/Sim/Building.cs).

## Kodstruktur
- `Scripts/Sim/` ren C#, deterministisk: `World` (tick 20/s), byggnader, `PlayerState`.
- `Scripts/View/` Godot-noder som läser World och ritar; `Game.cs` kör tick-loopen.
- `Scripts/Sim/BotPlayer.cs` AI-motståndaren: moduler (ekonomi, försvar, arméer) med fasta platser
  i vänster rum (speglas), band dras automatiskt (BFS, korsningar vid behov). Bygger upp det som
  förstörs, lägger till laser/vattenpistol när den ser golems/radiobilar. Svårighet: `armyDelayTicks` (0 = normal, högre = lättare).

## Assets
PixelLab MCP för sprites. Stilprompt: "top-down view, cute toy aesthetic,
plastic toy materials, bright saturated colors, soft shading, clean dark
outline, 64x64 game sprite, transparent background"
Lagfärger via shader, inte separata sprites.