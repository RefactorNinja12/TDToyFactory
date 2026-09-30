# Plan: electricity / power grid

Branch: `power`. **Read this file first when resuming** (after a context reset): check the progress
list, read the log at the bottom, and continue with the first unchecked step. Update the checklist and
log after every step and commit together with the step.

## Loop for every step (keeps tokens low)
1. Write failing tests first (Scenario helpers, `tests/FactoryTD.Sim.Tests/PowerTests.cs` etc.).
2. Implement until `task test -- <Area>` passes (Area = test class name part, e.g. `Power`, `Charge`).
3. `task check` (lint + all tests). Lint ENDOFLINE/WHITESPACE → `task fmt`.
4. Tick the box below, add one log line, commit: `Power step N: <what>` + Co-Authored-By line.
Only grep/read the parts of files needed. Godot only in step 10.
Edit files with the Edit tool; Python `open(..., "w")` on Windows writes CRLF (use `newline=""`), else `task fmt`.

## Design (decided with the user)
- **Battery charger** (1x1, toy battery charger): takes batteries from belts (input buffer 2),
  1 battery → `EnergyPerBattery` = 300 EU, stores up to `ChargerCapacity` = 1000 EU.
- **Pylon** (1x1, toy feel): supplies tiles within `PylonRadius` = 5 (Chebyshev or Euclidean² —
  use squared Euclidean, integer), links with a cord to pylons/chargers within `LinkRange` = 8.
- **Toybox (core)** acts as a pylon with radius `CoreRadius` = 4 (always a grid root).
- **Network** = pylons + chargers + core connected by cords (union-find). One shared EU pool per
  network. Players' grids never connect. Tile coverage map per player: tile → network id (or -1).
- **Consumers**: unit factories + assemblers (EU per tick while working), towers (EU per shot),
  golems/cars while charging. NOT: belts, extractors, farms, kitchen, warehouse, soldiers, workers.
- **Shortage**: each tick each network serves consumers in stable order (buildings by list order,
  then units by id); the rest are unpowered this tick. Unpowered factory = no progress (like
  starving), unpowered tower = no shot.
- **Unit charge** (golem, car): `MaxCharge`, `DrainPerTick` off-grid, `ChargePerTick` on own covered
  tile (drawn from that network). States: Normal → Returning (charge ≤ ReturnPercent, walks to
  nearest covered tile via a per-player "power flow field") → Charging (stays until ≥ 90%) → Normal.
  Charge 0 = Empty: speed 25%, heads home. No attacks while Returning/Charging/Empty.
  Car faster + lower drain → about 2x golem's off-grid range.
- Consequence (intended): golems must be supported by forward pylons in the hall.
- **Visuals**: cords = sagging curly Line2D in team colour between linked nodes; coverage radius
  overlay while placing a power building or consumer, toggle key V; crossed-lightning icon on
  unpowered buildings; charge bar on golems/cars; power meter in the resource bar.
- Tests keep their meaning: `World.FreePower` (internal) = everything always powered, units never
  drain. `Scenario.Match()` sets it; power tests call `.RealPower()`. Bots/slow tests use real power.

Starting constants (tune in step 11, all in `Sim/Power.cs` `PowerStats`):
factory 4 EU/s, assembler 2 EU/s, tower 3 EU/shot, unit charging 10 EU/s;
golem MaxCharge 600 EU drain 10 EU/s; car MaxCharge 600 EU drain 5 EU/s; ReturnPercent 35.

## Progress
- [x] 1. `World.FreePower` + Scenario `.RealPower()`; existing tests unchanged (114/114).
- [x] 2. Data model: `BuildingType.Pylon`, `BatteryCharger`, costs/size/build time/menu, `Sim/Power.cs`
      (`PowerStats`). Scenario helpers `Pylon(x,y)`, `Charger(x,y,energy:)`. Tests: placement, cost, zones.
- [x] 3. `PowerGrid` (pure C#): networks via union-find over link range, coverage map per player,
      rebuilt when dirty. Tests: link/no link, split on removal, radius edge, enemies don't link, core root.
- [x] 4. Supply: charger takes batteries → EU; per-network pool; stable-order serving. Tests: 1 battery =
      300 EU, no charger = nothing powered, shortage cuts same consumers, networks don't share.
- [x] 5. Consumers: `Building.Powered`; factories/assemblers progress only when powered; towers need EU
      per shot. Tests: outside grid never produces, tower with ammo but no power silent, resume after
      shortage, starvation and power independent.
- [x] 6. Unit charge: UnitDef MaxCharge/Drain/ChargeRate, states, power flow field (multi-source
      `FlowField.Build`). Tests: drain/recharge rates, no attack while Returning/Charging/Empty,
      returns to coverage in X s, Empty speed 25%, car range ≥ 1.8x golem.
- [x] 7. Determinism: checksum includes charger EU, unit charge + state. New real-power determinism test.
- [x] 8. UI logic (`Scripts/UI`): `PowerMeter` (like FoodMeter), InfoRows for charger/pylon/consumers,
      `PowerOverlay` (circles, cord pairs + sag points). Tests in UiTests.
- [x] 9. Bot (FIRST: ScenarioTests bot matches back to .RealPower(), see TODO): "ström" module (charger on battery belt, pylons over base, forward pylon chain in hall
      before golems/cars), rebuild. Tests: plan has charger+pylons, factories powered after N min;
      slow: bot vs idle < 12 min, bot vs bot reports unpowered ticks.
- [ ] 10. Assets + view: sprites via `pixelart_workbench` (pylon: stacked toy rings with battery top +
      plug; charger: big toy charger with AA batteries; crossed lightning icon; menu icon).
      `PowerView`: cords, overlay (V / while placing), no-power icon, charge bars, ghost preview,
      power meter. `task build` + one headless screenshot (scratchpad shot.sh).
- [ ] 11. Balance with `task test:report`; slow guards: golem from hall pylon reaches enemy toybox and
      fights ≥ 10 s; 1 battery/min runs 2 factories + 2 towers.
- [ ] 12. CLAUDE.md design + code structure for power; `task check`; commit.

## Log
(one line per finished step: what changed, test count, anything surprising)
- Step 1: World.FreePower (internal) + Scenario.Match() free by default, .RealPower() opt-in; bot scenarios (ScenarioTests bot matches, BotTests) already use RealPower. 115/115.
- Step 2: Pylon/BatteryCharger in Sim/Power.cs (+PowerStats), BuildingRules cost/hp/buildtime/Create/Buildable, Texts names. View has NO textures/menu for them yet (BuildingVisuals.TexturePaths would throw if one is placed in-game) -> step 10. Scenario.Pylon/Charger(energy:). 121/121.
- Step 3: Sim/PowerGrid.cs (PowerNetwork: Owner/Nodes/Chargers/Energy; PowerGrid: NetworkAt(player,x,y), Networks, Cords = Kruskal spanning tree). World.Power rebuilds with the flow fields (_fieldsDirty). Chargers link but cover nothing. 130/130.
- Step 4: BatteryCharger takes batteries (buffer 2), converts 1/tick while it fits. World.TryDrawPower(player,x,y,amount): all-or-nothing from the network at that tile, FreePower -> true. Order = tick order (buildings list, then units), no separate serving pass. 136/136.
- Step 5: Building.NoPower; Crafter.CanWork; army UnitFactory (not workers) draws FactoryEnergyPerTick=2 while working, Assembler 1, Tower HasPower check + TowerShotEnergy=30 per shot. World.TryDrawPower(Building)/HasPower/NetworkOf. Rescaled: battery 6000 EU, charger cap 20000. Bot slow matches temporarily FreePower (TODO step 9). 143/143.
- Step 6: PowerStats.MaxCharge/DrainPerTick (golem 12000/10, car 8000/10), UnitChargePerTick 40, ReturnPercent 35, ChargedPercent 90, EmptySpeedPercent 25. Unit.Charge/PowerState (UnitPower enum in Power.cs). World.TickCharge in TickUnit; low+on grid -> Charging too. FlowField.Build(world, owner, isTarget) multi-source; power fields LAZY per player (World.PowerField) - eager rebuild cost 8s->13s fast suite. 150/150.
- Step 7: checksum hashes unit Charge + PowerState (charger Energy/Batteries in step 2/4). Determinism: Checksum_SeesSmallChanges covers charge + charger energy; RealPower_ChargingAndReturning_IsDeterministic (90 s). 151/151.
- Step 8: PlayerState EnergyCharged/UsedLastMinute (same 60 s ring as food); PowerGrid.EnergyStored. UI: PowerMeter (1 bolt = 100 EU, battery = 60), InfoRows power rows (consumers: Ingen ström/Nätet tomt/ok; charger + pylon network rows), PowerOverlay Circles/Cords/CordPoints (sag 12%)/PreviewLinks. 159/159.
- Step 9: bot modules "ström" (charger 18,31 fed by battery extractor 40,47 via Route; pylons 8,26 8,36 15,26 15,34 21,30; also emergency), "frammaster" (pylons y=30 x=29..53 and 60..92 step 8, after soldiers + 2 min), car corner own grid (extractor 42,48 -> charger 43,48, pylon 45,45). Bot matches back on RealPower. BotTests share runs via IClassFixture<BotRuns> (Left=p0 120 s, Right=p1 240 s). BotVsBot reports/guards consumers without power <=10% in first 8 min (2%/2%). Still lopsided: p1 wins at 745 s, army 17 vs 90 (was so before power). 160/160.
