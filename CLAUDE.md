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
- Ström: batteriladdare äter batterier från band (1 batteri = 12000 energi) och laddar sitt nät.
  Leksaksmaster ger ström inom 5 rutor och kopplas med sladd till master/laddare/leksakslådan inom 8
  rutor (leksakslådan är en liten mast, radie 4). Ett nät = sammankopplade noder med gemensam energi;
  spelarnas nät kopplas aldrig ihop. Armefabriker och monteringsmaskiner drar ström medan de jobbar,
  torn per skott (2/tick av omladdningen); arbetarfabriker och resten drar inget. Utan ström: pausar.
  Golems/bilar har batteri: laddas på eget nät, töms utanför; låg (≤35%) → går tillbaka och laddar till
  90%, tomt → kryper i 25% fart. Anfaller inte när de går tillbaka/laddar/är tomma. Bilar når ~2x längre.
  Konstanter i `PowerStats` (Sim/Power.cs). V visar nätet; det syns också när man placerar strömgrejer.
- Dimma: per spelare och ruta Utforskad (för alltid) och Synlig (belyst nu), räknas om var 4:e tick.
  Allt man äger lyser i proportion till storlek (1x1 byggnad 3, 2x2 5, leksakslådan 10, byggplats 1,
  soldat/golem 5, arbetare 3, spejare 7, bil 3 + strålkastarkon 10 rutor framåt). Ljus går inte genom
  väggar (Bresenham-strålar, väggen själv lyses upp). Leksakslampa: radie 9, ingen ström.
  Fiendeenheter syns bara i ljus; fiendebyggnader minns som senast sedda (spöken). Bara utvinnare kräver
  utforskad mark (`PlaceError.Unexplored`); allt annat får byggas i mörker (byggarna lyser upp vägen).
  Spejare (från tält, 2 per tält, max 6, arbetare) söker brett: kantruta mot dimman med minst
  gångavstånd hemifrån + halva avståndet från spejaren, sprider sig 6 rutor, flyr 3 s från synliga
  fiender. Striderna i simuleringen är objektiva; dimman begränsar bara vad spelare/bot vet och var
  utvinnare får byggas. Minikarta uppe till höger (klicka för att flytta kameran). Konstanter i
  `VisionStats` (Sim/Vision.cs) och `ScoutStats` (Sim/Scouting.cs).
- Hinder: stora leksaker (nalle 4x4, ABC-klossar 3x3, tygdocka 3x4) = `TileType.Obstacle`: stoppar gång,
  flödesfält och bygge som väggar, men inte ljus. Placeras slumpat från ett frö (`MapLayout.CreateDefault
  (obstacles, seed)`, xorshift, speglas till högra rummet), aldrig i basen, vid resurser, i dörrgången,
  batterihörnet eller hallen; alltid en golvring runt och allt golv nåbart. `Scenario.Match()` = utan
  hinder, `Match(obstacles: true)` / `World.CreateMatch()` = med (bot-tester och -matcher). Sprites ritas
  snett uppifrån (en ruta överhäng) av `View/ObstacleView.cs`.
- Möss och ost: arbetarna är möss med hattar (byggarmus, bondmus, spejarmus). Ost (`ResourceType.Cheese`,
  ett fält per rum uppe i hörnet + ett i hallen, speglat) smälts av ostsmältaren (utvinnare) till smält ost.
  Köket gör matlådor av 2 morötter eller 1 smält ost (morötter först). Löpbandet (2x2, ingen ström,
  pausar vid svält) tar 2 smält ost + en byggarmus (kallas dit, aldrig den sista; ledig först, annars
  närmaste som bygger) och tränar en ostjägare: billig närkämpe (lågnivå, svagare än soldat) som slår
  på enheter och byggnader. Byggare på väg till ett löpband räknas inte; en kallad byggare som skulle
  bli den sista kliver inte in. Dör sista byggaren kommer en ny ur leksakslådan (`ReplaceLostBuilders`,
  av i `Scenario.NoWorkers()`). Musfälla = hinder. Närstrid (räckvidd ≤ 1,5 ruta) = `DamageKind.Punch`.
- Kortkommandon för bygge: siffra 1–7 väljer kategori (flik), sedan Z X C F G T byggnaden på den
  platsen. Bokstäverna krockar inte med kameran (WASD/QE), R (rotera) eller V (elnät).
  Logik i `Scripts/UI/BuildHotkeys.cs`, BuildMenu läser den i `_Input`.
- UI-stil: `View/UiTheme.cs` (halvgenomskinliga marinblå ramade paneler, rundade hörn, skugga, gul accent,
  textkontur; standardtypsnitt) sätts på varje UI-rot via `UiTheme.ApplyTo` (fönstrets tema når inte
  kontroller under CanvasLayers). Lite text: siffror vid små ikoner, förklaringar i verktygstips.
  Modeller i `Scripts/UI/Hud.cs` (ResourceBarModel, BuildCardModel, Toasts); vyerna ritar bara dem.
- Grafik: `tools/art/restyle.py` gör om alla sprites från `tools/art/source/` (palett + svarta konturer)
  och genererar golvet (stora brädor, 16x8 rutor). Nya sprites läggs i source och skriptet körs.

## Kodstruktur
- `Scripts/Sim/` ren C#, deterministisk: `World` (tick 20/s, byggare/bönder/strid/underhåll), byggnader,
  `PlayerState` (förråd, tak, matmätare), `Farming.cs` (odlingslåda, kök), `Warehouse.cs`.
- `Scripts/View/` Godot-noder som läser World och ritar; `Game.cs` kör tick-loopen.
- `Scripts/Sim/BotPlayer.cs` AI-motståndaren: moduler (ekonomi, försvar, arméer) med fasta platser
  i vänster rum (speglas), band dras automatiskt (BFS, korsningar vid behov). Bygger upp det som
  förstörs, lägger till laser/vattenpistol när den ser golems/radiobilar, odlar mer när den äter mer
  än den producerar. Svårighet: `armyDelayTicks` (0 = normal, högre = lättare).
- `Scripts/Sim/Power.cs` (PowerStats, Pylon, BatteryCharger, UnitPower), `Scripts/Sim/PowerGrid.cs` (nät,
  täckning, sladdar; byggs om med flödesfälten). `World.TryDrawPower/HasPower/NetworkOf`, `World.TickCharge`,
  `World.PowerField` (lat flödesfält mot närmaste strömruta). Tester: `World.FreePower` (`Scenario.Match()`
  = gratis ström, `.RealPower()` = riktig); bot-matcher kör alltid riktig ström.
- `Scripts/Sim/Vision.cs` (VisionStats, Vision: utforskat/synligt/minnen, Lamp), `Scripts/Sim/Scouting.cs`
  (World partial: spejare, HomeDistance, IsFrontier). `World.IsExplored/IsVisible/CanSee/RememberedBuildings`.
  Tester: `World.FullVision` (`Scenario.Match()` = ingen dimma, `.RealFog()` = riktig); bot-matcher kör
  alltid riktig dimma. `Scripts/UI/Fog.cs` (Knowledge, FogLevels, Minimap); `View/FogView.cs` (dimtextur,
  spöken, strålkastarkoner), `View/MinimapView.cs`. Vyerna har `LocalPlayer` och döljer det som inte syns.
- `Scripts/View/PowerView.cs` ritar sladdar, täckning, placeringsförhandsvisning, ikon utan ström, laddstaplar.
- `Scripts/UI/` ren C# utan Godot-typer: texter (`Texts`), bandkurvor (`ConveyorLook`), dragväg
  (`DragPath`), matmätaren (`FoodMeter`), hoverpanelens rader (`InfoRows`). View ritar bara det.
- `tests/FactoryTD.Sim.Tests/` xUnit, kompilerar `Scripts/Sim` + `Scripts/UI` direkt (internals syns).
  `Support/Scenario.cs` bygger scenarier (`Match().NoWorkers().Instant().Rich()`, `Place`, `Belt`,
  `Spawn`, `Feed`), `WorldRunner` kör tid (`Seconds`, `Until(villkor, maxSek, "vad")`).
  Långsamma helmatcher/balans har `[Trait("Speed","Slow")]` och skriver sina mätvärden.

## Planer
- Stora ändringar får en plan i `docs/plans/<namn>.md` med checklista + logg (överlever kontextslut):
  läs den först, fortsätt med första obockade steget, bocka av och logga i samma commit.
- Klara: `docs/plans/power.md` (elnät), `docs/plans/fog.md` (dimma, ljus, spejare, minikarta),
  `docs/plans/obstacles.md` (stora leksaker som hinder), `docs/plans/mice.md` (möss, ost, ostjägare, musfälla), `docs/plans/ui.md` (UI-stil, kortkommandon).

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