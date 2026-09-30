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
- Byggare (uppdragsrobotar, från verktygslådan) bygger alla byggplatser; en byggnad fungerar inte förrän den är klar.
- Lagring: leksakslådan + lager (leksakshylla) är ett gemensamt förråd med tak per varusort
  (`PlayerState.CoreCapacity` + `WarehouseCapacity` per lager). Fullt förråd tar inte emot → banden köar.
- Mat/underhåll: alla enheter äter mat per minut (`UnitStats.FoodPerMinute`). Kedjan:
  odlingslåda (växer på timer, mogen → väntar) → bonde skördar och bär morötter → kök (2 morötter →
  1 matlåda) → band/förråd. Lediga bönder hämtar lagrade morötter till kök med plats.
  Slut på mat = svält: armefabriker står still (arbetarfabriker går), enheter tappar hälsa.
  Matmätaren (resursraden) visar in/min, äts/min, netto och hur länge maten räcker.
- Arbetare (byggare, bönder) slåss inte och räknas inte som armé; bondgården gör max 1 bonde per 3 odlingslådor.

## Kodstruktur
- `Scripts/Sim/` ren C#, deterministisk: `World` (tick 20/s, byggare/bönder/strid/underhåll), byggnader,
  `PlayerState` (förråd, tak, matmätare), `Farming.cs` (odlingslåda, kök), `Warehouse.cs`.
- `Scripts/View/` Godot-noder som läser World och ritar; `Game.cs` kör tick-loopen.
- `Scripts/Sim/BotPlayer.cs` AI-motståndaren: moduler (ekonomi, försvar, arméer) med fasta platser
  i vänster rum (speglas), band dras automatiskt (BFS, korsningar vid behov). Bygger upp det som
  förstörs, lägger till laser/vattenpistol när den ser golems/radiobilar, odlar mer när den äter mer
  än den producerar. Svårighet: `armyDelayTicks` (0 = normal, högre = lättare).
- `Scripts/UI/` ren C# utan Godot-typer: texter (`Texts`), bandkurvor (`ConveyorLook`), dragväg
  (`DragPath`), matmätaren (`FoodMeter`), hoverpanelens rader (`InfoRows`). View ritar bara det.
- `tests/FactoryTD.Sim.Tests/` xUnit, kompilerar `Scripts/Sim` + `Scripts/UI` direkt (internals syns).
  `Support/Scenario.cs` bygger scenarier (`Match().NoWorkers().Instant().Rich()`, `Place`, `Belt`,
  `Spawn`, `Feed`), `WorldRunner` kör tid (`Seconds`, `Until(villkor, maxSek, "vad")`).
  Långsamma helmatcher/balans har `[Trait("Speed","Slow")]` och skriver sina mätvärden.

## Arbetsflöde (tester är feedbackloopen)
- `Taskfile.yaml` (go-task) samlar kommandona; `task` listar dem.
- Kör `task test -- <Område>` (namnfilter, t.ex. `Farming`, `Bot`, `Tower`) före och efter en
  ändring. Utskriften är kort: FAIL-block med meddelande + rad, sedan `PASS n/n`.
  (Samma sak utan task: `bash tests/run.sh <Område>`.)
- `task test` = alla snabba (~8 s). `task test:slow` = helmatcher + balans (~17 s).
  `task test:report` = samma + balansmätvärdena (~330 tokens), bara vid balansarbete.
- Allt skriver bara problem + en sammanfattningsrad (grön körning ≈ 10 tokens). Fel med samma
  meddelande grupperas, lint grupperas per fil/regel, max ~10 rader. Läs inte råutdata från
  `dotnet test/format/build` (en CRLF-fil = ~34k tokens rått).
- `task check` (lint + alla tester) före varje commit. `task fmt` fixar formatering.
- Lint = `dotnet format` mot `.editorconfig` (tabbar, LF) + analyzers; 0 varningar är normalläget.
  `.gitattributes` håller `.cs` i LF trots `core.autocrlf=true`.
- Buggfix: skriv först ett test som fallerar, sedan fixen. Ny funktion: tester i samma ändring.
- Ny logik hamnar i `Scripts/Sim` eller `Scripts/UI` (testbart), inte i View.
- Balansändring: kör `task test:report` och uppdatera trösklarna medvetet om designen ändrats.
- Lint FAIL med ENDOFLINE/WHITESPACE → `task fmt`, sedan `task lint`. Analyzer-varningar (t.ex.
  xUnit2013) fixas för hand. `task build` bara för View-ändringar (testerna bygger inte View).
- Godot bara för det visuella: `Scripts/_Test/VisualProbe.cs` + skärmdump (headless `--write-movie`).
  `Scenes/_Probe*.tscn` är gitignorerade.

## Assets
PixelLab MCP för sprites. Stilprompt: "top-down view, cute toy aesthetic,
plastic toy materials, bright saturated colors, soft shading, clean dark
outline, 64x64 game sprite, transparent background"
Lagfärger via shader, inte separata sprites.