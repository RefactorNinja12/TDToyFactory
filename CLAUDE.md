# Projekt: Leksakskrig (arbetsnamn)

PvP fabrik + tower defense i en leksaksvärld: två spelare bygger fabriker, försvarar sin leksakslåda (kärna) och
producerar trupper som själva anfaller motståndaren. Resurser: klossar, plast, batterier (+ ost). Kontringar: plast
svag mot område, elektronik mot vatten, klossar mot laser. Matcher 15–25 min, max 3 nivåer i produktionskedjan.
Siffror och balans står i koden (`BuildingRules`, `UnitStats`, `*Stats`-klasserna), inte här.

## Karta
Två rum (~60x60) och en hall emellan; dörren är enda ingången. Speglad, vänster rum = spelare 0.
Två teman med samma form (`MapTheme`): Barnrummet och Trädgården. Temat styr bara utseendet (tileset, hinder,
minikarta, `UI/Lawn.cs`, `UI/FloorDecor.cs`), så boten och reglerna passar båda. Väljs i startmenyn.
Hinder (stora leksaker/växter) = `TileType.Obstacle`: stoppar gång och bygge, inte ljus; placeras från ett frö
(`MapLayout.CreateDefault(obstacles, seed)`). `Scenario.Match()` = utan hinder, `Match(obstacles: true)` = med.

## Teknik och regler som inte får brytas
- Godot 4 (.NET) C#, 2D top-down, 64 px rutor. Simuleringen (`Scripts/Sim`) är ren C# utan Godot.
- Deterministisk lockstep: heltal, inga flyttal/klockor/slump/hashkoder/trådar i Sim (`DeterminismGuardTests`).
  Nytt tillstånd ska in i kontrollsumman (`World.Checksum`, `HashState`); lägg bara till hash-värden villkorat om
  golden-checksumman ska vara oförändrad.
- Allt spelare och bot gör går som `PlayerCommand` via `ICommandSink` → `World.Apply`.
- Online: värden är server, bara kommandon över nätet (ENet). Spelet öppnar ALDRIG portar i routern (ingen UPnP,
  användarens beslut); olika nätverk = Tailscale. Lösenordet går aldrig över nätet (HMAC på en utmaning).

## Design (beslutat)
- Leksakslådan = bank för byggkostnader. Allt annat går fysiskt på band; fabriker/torn hämtar aldrig från lådan.
  Lådan + lager = gemensamt förråd med tak per varusort; fullt → banden köar.
- Band byggs klick–klick (`UI/BeltPlanner.cs`, korsar egna band med korsningar). Delare/sorterare/korsning kan
  byggas på ett eget band (`UI/BeltReplace.cs`). Andra byggnader: håll inne och dra = en rad utan luckor (`DragPath`).
- Klokran (`Sim/Crane.cs`): hämtar från utvinnare under rälsen och släpper bakåt på ett band.
- Kedja: råvara → monteringsmaskin (kugghjul/fjäder/kretskort) → truppfabrik (producerar själv). Torn får ammo
  via band; utan ammo inget skott.
- Byggare bygger allt; en byggplats fungerar inte förrän den är klar. Dör sista byggaren kommer en ny.
- Mat: alla enheter äter. Odlingslåda → bonde → kök → matlådor. Svält: armefabriker stannar, enheter tappar hälsa.
- Ost: ostsmältare → smält ost → kök eller löpband (tar en byggarmus, aldrig den sista) → ostjägare (närkämpe).
- Ström (`Sim/Power.cs`, `PowerGrid.cs`): laddare äter batterier, master/sladdar bildar nät per spelare.
  Armefabriker, monteringsmaskiner, torn och elstängsel drar ström; utan ström pausar de. Golems/bilar har batteri
  och går hem och laddar.
- Dimma (`Sim/Vision.cs`): Utforskad/Synlig per spelare, ljus från allt man äger, inte genom väggar. Striden är
  objektiv; dimman begränsar bara vad spelare/bot vet och var utvinnare får byggas. Spejare (`Sim/Scouting.cs`).
- Flödesfält (`Sim/FlowField.cs`): fiendebyggnader är dyra men aldrig oframkomliga (skjuts ned).
- Murar och fällor (`Sim/Walls.cs`, `Sim/Traps.cs`): klossvägg, grind, elstängsel stoppar ALLA enheter när de är
  klara, även ägarens; egen grind släpper igenom (`World.BlocksUnit`). Egen mur = oframkomlig i fältet; fiendemur =
  `SealedWallPenalty` (går alltid runt, slår sig bara igenom inmurad bas), golems eget fält med låg straffkostnad.
  Murar väljs aldrig som mål; enheter slår på muren bara när vägen går igenom. Stängslet ger stöt mot den som slår.
  Fallucka/pendel: fienden ser dem inte (inget mål, golvkostnad), de slits per fångst. Pendeln är 4x1 och vrids:
  storlek beror på riktning (`BuildingRules.Size(type, facing)`). Boten bygger murlager (inga fällor).
- Kortkommandon bara siffror (`UI/BuildHotkeys.cs`): siffra = kategori, sedan siffra = kort; mellanslag eller samma
  siffra avslutar; Esc = paus. Bokstäver tas aldrig (utom R vrid, V nät).
- UI: `View/UiTheme.cs` sätts på varje UI-rot med `UiTheme.ApplyTo`. Lite text, förklaringar i verktygstips.
  Modeller i `Scripts/UI` (`Hud.cs`, `InfoRows`, `Texts`); vyerna ritar bara dem.
- Skuggor räknas ut från ljuskällorna (`UI/Shadows.cs`), målas aldrig i sprites.
- Byggnader rör sig när de arbetar (`UI/Activity.cs`, `UI/BuildingParts.cs`, `View/BuildingAnimator.cs`).
- Enheter: möss ovanifrån som vrids, nosen österut i spriten; golemen står upprätt och vrids aldrig
  (`UI/UnitSheets.cs`, `UI/UnitAnimation.cs`).

## Kodstruktur
- `Scripts/Sim/`: `World` (20 tick/s) i partials: `World.cs` (placering, tick-ordning, kontrollsumma), `WorldPower`,
  `WorldVision`, `Workers`, `Fighting`, `Upkeep`, `Scouting`, `Separation`. Läs bara den del som berörs.
  Boten: `BotPlayer.cs` (tänkandet), `BotModules.cs` (moduler med fasta platser i vänster rum, speglas),
  `BotRouting.cs` (band), `BotGrowth.cs` (när modulerna är klara växer den för evigt: var 20:e s ett steg i
  turordning ekonomi/armé/torn/murlager, mat först vid brist; platser söks (`FindSpot`, `FreeDeposits`, även hallen
  fram till murlinjen x 70), band kan sluta i sidan av ett band till lådan (`RouteToCore`) och tappa av ett rent
  råvaruband med en delare (`Tap`) när fyndigheterna är slut; `EnsurePower` drar master). Ett steg den väntat
  färdigt på hindrar aldrig planen igen (`_gaveUpOn`). Svårighet: `armyDelayTicks`.
  Test: `Bot_KeepsGrowing_TwentyFiveMinutes` (`World.CoresInvulnerable`, armén dör varje minut).
- `Scripts/UI/`: ren C# utan Godot-typer (testbar). `Scripts/View/`: Godot-noder som läser World och ritar;
  `Game.cs` kör tick-loopen. `Scripts/Net/`: ren C# (lockstep, protokoll); `View/Net/ENetTransport.cs`.
- `tests/FactoryTD.Sim.Tests/`: xUnit, kompilerar Sim + UI direkt. `Support/Scenario.cs`
  (`Match().NoWorkers().Instant().Rich()`, `.RealPower()`, `.RealFog()`, `Place`, `Belt`, `Spawn`, `Feed`),
  `WorldRunner` (`Seconds`, `Until(villkor, maxSek, "vad")`). Långsamma tester har `[Trait("Speed","Slow")]`.

## Arbetsflöde
- `task` listar kommandona. `task test -- <Område>` (namnfilter, `"A|B"` för flera) före och efter en ändring;
  `task test:changed` = bara det som berörs. `task test` ~9 s, `task test:slow` ~40 s, `task test:report` vid balans.
- `task check` (lint + dubbletter + alla tester) före varje commit. Lint FAIL (WHITESPACE/ENDOFLINE) → `task fmt`.
  Läs aldrig råutdata från `dotnet test/format/build`.
- Läs billigt: Grep `^\t(public|internal|private|protected)` för översikt, sedan Read med offset/limit.
- Golden-checksumman (`DeterminismTests.GoldenBotMatch`) får bara ändras när beteendet medvetet ändrats.
- Dubbletter (`task dupes`, 50+ tokens): bryt ut en gemensam hjälpare, höj inte gränsen.
- Buggfix: först ett test som fallerar. Ny funktion: tester i samma ändring. Ny logik i Sim/UI, inte View.
- `task build` för View-ändringar (testerna bygger inte View).
- View: en textur som ritas med Draw* måste hållas kvar i ett fält/cache; laddad lokalt i `_Draw` blir den en vit ruta.
- Nät: `task test -- Net`; `task mp:smoke` (~40 s, två headless spel) före commits som rör nät/lockstep.
- `task export` = zip per plattform i `builds/`; båda spelarna måste köra samma zip.
- Stora ändringar får en plan i `docs/plans/<namn>.md` (checklista + logg): läs den först, fortsätt med första
  obockade steget, bocka av och logga i samma commit. När planen är klar och sammanfattad här tas filen bort
  (finns kvar i git-historiken).
- Visuell kontroll i Godot: `Scripts/_Test/VisualProbe.cs`. Kör `task build` först, lägg en scen
  `Scenes/_ProbeVisual.tscn` (instans av `Main.tscn` + en nod med VisualProbe-skriptet; gitignorerad) och kör
  `Godot*mono*console.exe --path factory-td --write-movie OUT.png --fixed-fps 20 --quit-after 40
  res://Scenes/_ProbeVisual.tscn` (Godot ligger under ~/Downloads). Miljövariabler: `PROBE_SCENE=` belts, crane,
  shadows, toys, walls; `PROBE_CAM="x,y,zoom"`, `PROBE_MAP=garden`, `PROBE_GC=1`.

## Grafik
- `tools/art/restyle.py` gör om sprites från `tools/art/source/` (palett + svarta konturer); `restyle.py <filer>` =
  bara de. Byggnader/enheter ritas av kod: `tools/art/kit.py` + `tools/art/{buildings,units,logistics,defense}/*.py`,
  `garden.py`, `rugs.py`. Granska med `tools/art/sheet.py OUT.png <filer>`.
- `HAND_EDITED` i `kit.py` = sprites användaren gjort klart för hand: skripten skriver aldrig över dem.
  Användarens egna bilder ligger i `tools/art/reference/`; `KEEP`/`SOFT` i restyle styr deras färger.
- Under luminans 0,2 blir kontursvart, ljusa pasteller blir grå: välj mättade färger. Lagfärger via shader.
- PixelLab MCP finns för sprites (stil: top-down, cute toy, plastic, bright saturated, clean dark outline, 64x64).
