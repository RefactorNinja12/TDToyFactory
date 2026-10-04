# Projekt: Leksakskrig (arbetsnamn)

## Koncept
PvP fabrik + tower defense. Två spelare bygger fabriker, försvarar sin
leksakslåda (kärna) och producerar trupper som automatiskt anfaller motståndaren.

## Tema
Leksaksvärld: två barnrum i krig. Resurser: klossar, plast, batterier.
Torn: skumpilar, vattenpistol, katapult. Trupper: piratmöss av plast,
klossgolems, radiostyrda bilar. Kontringar: plast svag mot område,
elektronik svag mot vatten, klossar svaga mot laser.

## Karta
Två rum (~60x60 rutor) förbundna med en hall (~30–40 rutor).
Dörren är enda ingången i början. Rika resurser i hallen.
Två kartor med samma spelbara form (`MapTheme`): Barnrummet (brädgolv, väggar, jätteleksaker) och Trädgården
(gräsmatta med klöverfläckar, häckar som väggar, grusgång i hallen, jätteväxter: pumpa, solros, kål, morot, blomsterplätt; var och en i en jordbädd med stenar, `plantbed.png`, som
ObstacleView ritar först, över fotavtrycket + en halv ruta runt). Formen
är densamma så boten och alla regler passar båda; temat styr tileset (`RoomTiles`/`GardenTiles.tres`), hinder,
minikartans färger. Klövern: `UI/Lawn.cs` (mjukt heltalsbrus → täthet 0–3 per gräsruta, klöverblad
som får sticka över rutkanten, bara på gräs i rummen) ritas av `View/CloverView.cs` (barn till MapView). Väljs i startmenyn ("Karta", sparas); värden skickar den i Start (protokoll 2).
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
  Band byggs klick–klick: första klicket = start (på en byggnad: bredvid den), vägen följer musen
  (`UI/BeltPlanner.cs`: billigaste vägen över ledigt golv, svängar kostar lite extra, aldrig över byggnader,
  korsar egna raka band med automatiska korsningar), andra klicket bygger den. Högerklick/mellanslag avbryter.
  Band ritas som leksaksband (gula skenor, gummiband) som skarvas ihop med delare/sorterare/korsning; räfflorna
  rör sig i föremålens fart via en shader (`UI/BeltLook.cs`, `View/BeltMaterials.cs`).
  Delare/sorterare/korsning kan byggas direkt på ett eget band (`UI/BeltReplace.cs`: bandet rivs med återbetalning,
  den nya biten placeras; två vanliga kommandon). Spöket blir grönt över egna band.
- Klokran (`Sim/Crane.cs`, Logistik, G): bas på styltor vid ett resursfälts kant, vänd in mot fältet; rälsen
  når `CraneStats.Reach` = 5 rutor (tar inga rutor). Klon hämtar en sak i taget från närmaste egna utvinnare
  under rälsen med något i lager och släpper den bakåt på ett band (väntar om det är fullt). Ingen ström.
  `View/CraneView.cs` ritar räls och klo; förhandsvisning av räls och nådda utvinnare vid placering.
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
- Kortkommandon för bygge, bara siffror: siffra 1–7 öppnar en kategori (flik), sedan väljer en siffra kortet på
  den platsen (1 = första). Kategorin står kvar öppen; mellanslag, eller samma siffra som det valda kortet igen, avslutar direkt (släpper
  bygget och bandets start, stänger kategorin, nästa siffra väljer kategori). Esc = pausmenyn.
  Bokstäver tas aldrig.
  Logik i `Scripts/UI/BuildHotkeys.cs`, BuildMenu läser den i `_Input`.
- UI-stil: `View/UiTheme.cs` (halvgenomskinliga marinblå ramade paneler, rundade hörn, skugga, gul accent,
  textkontur; standardtypsnitt) sätts på varje UI-rot via `UiTheme.ApplyTo` (fönstrets tema når inte
  kontroller under CanvasLayers). Lite text: siffror vid små ikoner, förklaringar i verktygstips.
  Modeller i `Scripts/UI/Hud.cs` (ResourceBarModel, BuildCardModel, Toasts); vyerna ritar bara dem.
- Startmenyns bakgrund: en levande match bot mot bot (`Game.Preview`, full sikt, inget spelargränssnitt eller
  input, dubbel fart, 40 s försprång) i en egen SubViewport i halv upplösning bakom en mörk slöja; kameran glider
  mellan baserna (`UI/MenuPreview.cs`). Inte headless (röktester).
- Matchstart: nedräkning 3, 2, 1, "Kör!" (`UI/Countdown.cs`); simuleringen står still under tiden (man kan titta
  och placera), kameran flyger in från rummet till den egna leksakslådan. Röktest/VisualProbe hoppar över den.
- Multiplayer (online, 2 spelare): deterministisk lockstep där värden är servern. Bara kommandon går över
  nätet (`PlayerCommand`: bygg/riv/ställ in; allt spelare och bot gör går via `ICommandSink` → `World.Apply`).
  Kommando skickat vid steg s körs vid s + InputDelay (3–12 steg efter ping) på båda maskinerna; värden
  slår ihop båda spelarnas kommandon till en tur per steg, ingen kör ett steg utan sin tur (sen tur = vänta).
  Kontrollsumma var 20:e steg, olika = matchen stoppas + `user://desync-TICK.txt`. Värden väljer port
  (standard 7777) och lösenord (minst 4 tecken); lösenordet går aldrig över nätet (HMAC-SHA256 på en ny
  utmaning varje gång), 3 fel på 60 s = IP:n spärras 30 s. Samma bygge krävs (`Protocol.BuildId` = assemblyns
  MVID, lika på alla plattformar i exporten, annat i editorn). ENet (UDP) som rått paketrör. Spelet öppnar ALDRIG
  portar i routern (ingen UPnP, användaren tyckte det kändes osäkert): olika nätverk = Tailscale (100.64–127.x). Startmeny (`Scenes/
  Menu.tscn`, huvudscen): spela lokalt (Lätt/Normal), hosta, anslut. Online går inte att pausa; 10 s tystnad =
  anslutningen bröts. Sim-koden får inte ha flyttal, klockor, slump, hashkoder eller trådar (`DeterminismGuardTests`).
- Enheter: mössen (byggare, bonde, spejare, ostjägare, piratmusen `PlasticSoldier` "Piratmus" med tricorn/
  ögonlapp/flintlås, byggs i Piratskeppet) ses ovanifrån och vrids dit de går, nosen österut i spriten, på två
  ben: rosa fötter som tar steg bakom kroppen, rosa öron och svans. Ark 7x1 (stå, gå x4, handla x2). Klossgolemen
  står upprätt (ark 7x3, vrids aldrig, fötterna på positionen); RC-bilen ovanifrån med hjulbilder.
  `UI/UnitSheets.cs` (`UnitLayout`), `UI/UnitAnimation.cs` (gångsteg efter sträcka, anfall/arbete, riktning
  med hysteres för golemen), UnitView (MultiMesh + shader som väljer rutan).
- Byggnader rör sig när de arbetar (framsteg ändras senaste 0,5 s, `UI/Activity.cs`): rörliga delar ovanpå
  stillbilden (`UI/BuildingParts.cs`: snurra, gunga, gå fram och tillbaka, glida, runda, blinka, puffa, flyta;
  `Always` = rör sig även när den står still; `KeepsUpright` = piratskeppet ses från sidan, vrids aldrig, speglas mot väster;
  `View/BuildingAnimator.cs`).
- Skuggor räknas ut, målas aldrig i sprites: ingen skugga utan ljus. Ljuskällorna = samma som dimmans
  (`UI/LightSources`); ljus inom radien och utan vägg emellan (`Vision.LightReaches`) skjuter en mjuk rund
  skugga bort från sig, längre och svagare på avstånd; band/korsningar ligger platt (ingen skugga).
  `UI/Shadows.cs` (Cast, ShadowCaster), `View/ShadowView.cs`.
- Grafik: `tools/art/restyle.py` gör om alla sprites från `tools/art/source/` (palett + svarta konturer)
  och genererar golvet (stora brädor, 16x8 rutor). Nya sprites läggs i source och skriptet körs
  (`restyle.py <filer>` = bara de). Byggnader och enheter ritas av kod: `tools/art/kit.py` (plastformer, mus på
  två ben, ark), `tools/art/buildings/*.py`, `tools/art/units/*.py`, `tools/art/logistics/*.py`, `tools/art/garden.py` (gräs, klöver, grus, häck, växter; en bild i `tools/art/reference/<namn>.png`, t.ex. användarens pumpa och kål, används i stället: bakgrund och skugga klipps bort, halv upplösning med hårda pixlar, 16 platta färger (octree, behåller små ytors färg), restylas; `painting()` = användarens färdiga sprite med genomskinlig bakgrund (pumpan uppifrån, `pumpkin_top.png`): passas på fotavtrycket, halv upplösning, 32 platta färger, gröna delar → palettens gröna, svarta linjer kvar; `KEEP` i restyle = egna färger, bara kontur (solrosen, moroten, kålen, blomsterplätten: användarens färdiga bilder, paletten saknar deras färger); `painting(shadow=True)` klipper bort en målad grå skugga + lösa prickar, `pixel=1` = full upplösning (blomsterplätten); `SOFT` i restyle = lätt dämpad; `HAND_EDITED` i `kit.py` = sprites användaren gjort klart för hand, skripten skriver aldrig över dem); granska med `tools/art/sheet.py`. Under
  luminans 0,2 blir kontursvart, 2 px-linjer blir helt kontur, ljusa pasteller blir grå (välj mättat); paletten har två rosa för mössens öron, tassar och svansar.

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
- `Scripts/Net/` ren C# (testas): `Protocol` (meddelanden, `PacketWriter/Reader` som aldrig kastar, lösenordsbevis),
  `MatchSession` (lockstep, ping, timeout, desync), `HostSession`, `ClientSession`, `ITransport`. Godot-sidan:
  `View/Net/ENetTransport.cs`, `MatchSetup` (statisk: sessionen genom scenbytet);
  `View/MainMenu.cs` (logik/texter i `UI/MenuModel.cs`, även `LaunchArgs`), `View/MatchOverlay.cs` (vänta, slut, Esc).
- `tests/FactoryTD.Sim.Tests/` xUnit, kompilerar `Scripts/Sim` + `Scripts/UI` direkt (internals syns).
  `Support/Scenario.cs` bygger scenarier (`Match().NoWorkers().Instant().Rich()`, `Place`, `Belt`,
  `Spawn`, `Feed`), `WorldRunner` kör tid (`Seconds`, `Until(villkor, maxSek, "vad")`).
  Långsamma helmatcher/balans har `[Trait("Speed","Slow")]` och skriver sina mätvärden.

## Planer
- Stora ändringar får en plan i `docs/plans/<namn>.md` med checklista + logg (överlever kontextslut):
  läs den först, fortsätt med första obockade steget, bocka av och logga i samma commit.
- Klara: `docs/plans/power.md` (elnät), `docs/plans/fog.md` (dimma, ljus, spejare, minikarta),
  `docs/plans/obstacles.md` (stora leksaker som hinder), `docs/plans/mice.md` (möss, ost, ostjägare, musfälla), `docs/plans/ui.md` (UI-stil, kortkommandon).
  `docs/plans/multiplayer.md` (online-lockstep, lösenord, startmeny, export).
  `docs/plans/refactor.md` (GridSearch, smutsflaggor för fält, rumslig målsökning, test:changed).
  `docs/plans/animations.md` (fabriker ritade av kod, rörliga delar), `docs/plans/units.md` (upprätta enheter,
  piratmus, uträknade skuggor), `docs/plans/belts-crane.md` (animerade band, klokran), `docs/plans/garden.md` (trädgårdskartan, kartval).

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
- `task check` (lint + dubblettkoll + alla tester) före varje commit. `task fmt` fixar formatering.
- Lint = `dotnet format` mot `.editorconfig` (tabbar, LF) + analyzers; 0 varningar är normalläget.
  `.gitattributes` håller `.cs` i LF trots `core.autocrlf=true`.
- Snabbast: `task test:changed` kör bara testerna för ändrade .cs-filer sedan senaste commit
  (tabell i `tests/changed.ps1`; kärnfiler som World → alla snabba; View → build). Flera områden: `task test -- "A|B"`.
- Golden-checksumman (`DeterminismTests.GoldenBotMatch`, 2 min bot mot bot) vaktar refaktorer: får bara
  ändras när beteendet medvetet ändrats (felet skriver ut det nya värdet). Ren refaktor = oförändrad.
- `task coverage` = täckning Sim/UI + 10 sämsta filer (~100 s); `-File X.cs -Reuse` (ps1) visar otestade rader.
  `task test:timing` = 10 långsammaste testerna. Sim-hastighet: slow-testet `SimSpeed_*` (ms per sim-minut).
- Dubblettkoll: `task dupes` (jscpd 5.4.0 via npx, `.jscpd.json`) underkänner varje block på 50+ tokens som
  finns två gånger (spel- och testkod). Skriver `DUPE n lines A.cs:x-y ~ B.cs:x-y`. Åtgärd: bryt ut en
  gemensam hjälpare/basklass (t.ex. `StoreBuilding`, `OneItemRouter`, `GridSearch.Flood`), höj inte gränsen.
- Buggfix: skriv först ett test som fallerar, sedan fixen. Ny funktion: tester i samma ändring.
- Ny logik hamnar i `Scripts/Sim` eller `Scripts/UI` (testbart), inte i View.
- View: en textur som ritas med Draw* måste hållas kvar (fält, nod eller `BuildingVisuals`-cachen). Laddas den
  bara lokalt i `_Draw` frigör .NET:s GC den efter ritningen och Godot ritar en vit ruta (hindren, garden-map).
- Balansändring: kör `task test:report` och uppdatera trösklarna medvetet om designen ändrats.
- Lint FAIL med ENDOFLINE/WHITESPACE → `task fmt`, sedan `task lint`. Analyzer-varningar (t.ex.
  xUnit2013) fixas för hand. `task build` bara för View-ändringar (testerna bygger inte View).
- Nätverk: `task test -- Net` (falskt nät med latens/jitter i `Support/FakeNetwork.cs`, `NetPlayer` kör en maskin).
  `task mp:smoke` (~40 s) = två headless spel via localhost (riktig meny, ENet, lösenord, botar) ska sluta med
  samma kontrollsumma; kör före commits som rör nät/lockstep. Startargument: `-- --host --port N --password X
  --bot --steps N --out FIL --map garden` / `-- --join ip:port ...`. `mp_smoke.ps1 -Map garden` spelar trädgården.
- `task export` = zip per plattform i `builds/` (Godot .NET export templates krävs, `tools/build/zip_build.py`
  behåller körrättigheter, `docs/LAS-MIG.txt` följer med). Båda spelarna måste köra samma zip.
- Godot bara för det visuella: `Scripts/_Test/VisualProbe.cs` + skärmdump (headless `--write-movie`);
  `PROBE_CAM="x,y,zoom"` tittar var som helst, `PROBE_SCENE=shadows` = lampa med byggnader och möss,
  `PROBE_GC=1` = tvingar en GC efter några bilder (texturer som ingen håller kvar blir vita rutor),
  `PROBE_MAP=garden` = trädgårdskartan, `PROBE_SCENE=belts` = ett band ritat klick–klick (startklick + musen vid målet), `PROBE_SCENE=crane` = klokran
  över klossfältet som matar ett band genom en delare.
  `Scenes/_Probe*.tscn` är gitignorerade.

## Assets
PixelLab MCP för sprites. Stilprompt: "top-down view, cute toy aesthetic,
plastic toy materials, bright saturated colors, soft shading, clean dark
outline, 64x64 game sprite, transparent background"
Lagfärger via shader, inte separata sprites.