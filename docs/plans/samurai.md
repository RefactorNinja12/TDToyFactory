# Plan: samurai mouse (sword sweep in front of it)

Goal: a new army unit, the samurai mouse (`UnitType.Samurai`, "Samurajmus"). Up close it swings a katana in an arc
in front of it and hits **every** enemy in that arc (soldiers, cheese hunters, cars), not just one. It is the counter
to crowds of small units. It is weak to area damage (plastic armour) and loses to golems (heavy, brick).

## Approach (decided; open questions at the end)
- **Sim (`Scripts/Sim`)**
  - `UnitType.Samurai` (enum last). `UnitDef`: Plastic, MaxHealth 45, Speed 26, Damage 7, AttackTicks 24,
    Range 1.3 tiles, Sight 7, TargetsUnits, BuildingDamagePercent 100. Recipe 3 plastic + 1 spring + 1 gear,
    BuildTicks 180. Food per minute like a soldier. Light 5 (like a soldier, `Vision`).
  - **The sweep** (new in `Fighting.cs`, `Attack` gets a sweep branch): the attack direction is the vector to the
    target (`MoveX/MoveY`). Every living enemy unit whose centre is within `SamuraiStats.SweepReach` (1.4 tiles)
    and inside the arc (±60°: integer test `dot > 0 && 4·dot² ≥ |v|²·|f|²`) takes the damage. That includes the
    target. Units are taken in spawn order (deterministic), at most `SweepMaxTargets` (5).
  - Damage lands at once via a short projectile per target (`ShotTravelTicks` 2, `DamageKind.Slash`). New
    `DamageKind.Slash`: 100% against plastic, 125% against electronics, 50% against brick (golems shrug it off).
    `Combat.DamagePercent` gets the new rows.
  - Buildings and walls: one hit on the target only (no arc), counts as a punch on walls (`Wall.HitBy`, so the
    electric fence zaps it).
  - `Unit.SwingCount` (or `LastSwingTick`) for the view: counts up per swing and goes into the checksum.
  - **Building:** a new **dojo** (`BuildingType.Dojo`, 2x2, Armé, `UnitFactory` with `UnitType.Samurai`), powered like
    the other army factories, fed plastic/springs/gears on belts. Cost 30 bricks + 20 plastic + 5 gears? (Gears in
    the build cost need the core to hold them: decide in step 1; otherwise 35 bricks + 25 plastic.)
  - `SamuraiStats` holds the numbers (`SweepReach`, `SweepHalfAngle` as a cos² ratio, `SweepMaxTargets`).
- **UI (`Scripts/UI`)**: `Texts` (unit name, dojo name/description), `InfoRows` (the dojo like other army factories),
  `UnitSheets.FileName` → "samurai", `UnitLayout` top-down mouse 7x1 like the pirate. `UnitAnimation`: act frames
  while it swings (as today for attacks).
- **Art** (`tools/art/units/mice.py`, kit):
  - a mouse on two legs with a red/black kabuto (helmet with crest from above), a dark gi, and a katana;
  - act frames = the sword drawn back → swept across in front;
  - icon `samurai.png`, sheet `samurai_sheet.png`, restyled;
  - **The dojo is its own animated building, shaped like a pagoda** (decided by the user). `tools/art/buildings/army.py`,
    2x2 seen from above: three stacked roof tiers in red lacquer, each smaller than the one below, with upturned gold
    tips at the corners, dark wooden beams between them and a gold finial on top. In front (the door side) a small
    tatami yard with a red door frame. Moving parts on top of the still picture (`UI/BuildingParts.cs`, separate part
    sprites):
    - **sensei mouse at the door**: an old mouse with a white beard, a dark robe and a walking stick. It stands by the
      door all the time and **turns right and left** (`Swing`, `Always`, slow, about ±0.35 rad), watching over the yard;
    - **the sensei shouts now and then**: a small speech bubble ("Hai!" or "!") pops up beside its head for about half
      a second, about every 4 seconds. That needs a new motion **`Motion.Pop`**: visible for a short part of each beat
      (`Amount` = the share), grows quickly and fades out, otherwise hidden. Pure, in `BuildingParts`, tested. The game
      has no sound yet, so the shout is only seen (a sound can come with an audio plan later);
    - **trainee mouse** with a wooden sword stepping back and forth in the yard (`Bob`), while it works;
    - **paper lanterns** at the roof corners glowing softly all the time (`Blink`, `Always`, out of step);
    - **the door** slides open when a samurai is finished and walks out (`Slide`, a short pulse on a new unit).
    `Activity` already says "working" when the progress changes. The "unit finished" pulse for the door needs a
    signature like the other factories' (the number of units made); decide in step 4.
- **Effect:** an arc ("slash") drawn by the view for a few frames in front of the samurai when `SwingCount` changes
  (`View/CombatView` or UnitView: a crescent sprite in the swing direction, fading). Pure helper for the arc
  position/angle in `Scripts/UI` (tested).
- **Bot:** a "samurajer" module after the soldiers (dojo by the plastic patch, assembler for springs + gears),
  `Emergency` when it sees many soldiers (`SoldiersSeen >= 8`). It also goes into the growth rotation (`GrowArmy`
  alternates soldier/golem/samurai). The golden checksum changes on purpose in that step.

## Test loop
- `task test -- "Samurai|Combat|EveryType|UnitSheet"`; golden unchanged until the bot step.
- New `tests/.../SamuraiTests.cs`:
  - the sweep hits every enemy in the arc, not those behind it or outside the reach;
  - max targets; own units untouched;
  - golems take half (Slash vs brick);
  - the dojo makes samurai from plastic/springs/gears and pauses without power;
  - deterministic (checksum).
- Balance (slow, `task test:report`): 1 samurai vs 4 soldiers clumped (the samurai should win), 1 samurai vs 1 golem
  (the golem should win), dojo cost vs soldier factory.
- Visual: `PROBE_SCENE=samurai` (a samurai in a crowd of enemy pirates, a few frames apart).

## Steps
- [x] 1. Sim: UnitType.Samurai, UnitDef, DamageKind.Slash + DamagePercent, SamuraiStats, sweep in Attack,
      SwingCount in the checksum; BuildingType.Dojo (UnitFactory), cost/health/build time/size, Buildable;
      Texts (the every-type tests ask for names). Tests above (sweep, targets, armour, dojo). Golden unchanged.
- [x] 2. Balance: slow tests 1 vs 4 soldiers / 1 vs 1 golem; tune numbers; `task test:report`.
- [x] 3. UI: InfoRows, BuildMenu (Armé), UnitSheets/UnitLayout, UnitAnimation act frames on a swing, slash-arc helper
      + tests.
- [ ] 4. Art: samurai sheet + icon (mice.py); pagoda dojo (army.py): still picture + part sprites (sensei, speech
      bubble, trainee, door, lanterns); slash crescent; restyle, sheet review. `Motion.Pop` + tests; BuildingParts
      entries for the dojo (+ tests: every part sprite exists; only the sensei, its bubble and the lanterns move while
      idle).
- [ ] 5. View: slash effect; dojo parts through BuildingAnimator (door pulse on a new samurai);
      `PROBE_SCENE=samurai` (a dojo working + a samurai in a crowd of pirates), frames a few ticks apart.
- [ ] 6. Bot: "samurajer" module + growth rotation; golden updated on purpose; `task test:report` (bot vs bot still
      ends in time, healthy economy); `task mp:smoke`.
- [ ] 7. CLAUDE.md (a short Design line), `task check`, remove this plan, push.

## Open questions (default chosen if nobody answers)
1. ~~Its own building or a setting on the soldier factory?~~ **Decided: its own animated dojo, a pagoda with a sensei
   mouse at the door that turns right and left and shouts now and then.**
2. Should the sweep also hit buildings in the arc? **Default: no, only the target building (otherwise it clears belts
   too fast).**
3. Recipe with gears (a 3-level chain: two assemblers) or simpler (plastic + springs)? **Default: plastic + spring +
   gear.**
4. Should the swing also push enemies back a little? **Default: no (the pendulum already pushes).**

## Log
- Step 1: UnitType.Samurai (45 hp, speed 26, 7 dmg / 24 ticks, range 1.3 tiles; 3 plastic + 1 spring + 1 gear, 180 ticks;
  light 5), SamuraiStats (SweepReach 1.4 tiles, ±60° arc as an integer cos² test, max 5) in Unit.cs; Fighting.Attack
  sweeps for samurai vs units (one Slash projectile per hit, target first, then spawn order), single hit on buildings/walls
  (walls stay punches); DamageKind.Slash (50% brick, 125% electronic); Unit.SwingCount hashed only for samurai (golden
  unchanged). BuildingType.Dojo ("Samurajdojo": the every-type test wants a name that isn't the enum's), 2x2 UnitFactory,
  35 bricks + 25 plastic, health/time like the army factories, PowerView counts it. The samurai sheet + icon came
  forward from step 4 (every unit must have its sheet): kabuto over the back of the head with gold horns, blue gi,
  katana (3 px white blade: 2 px lines turn into outline), act = drawn back / swept with a white slash arc.
  SamuraiTests 7.
- Step 2: tuned to 60 hp, 10 dmg / 20 ticks, sweep reach 1.6 tiles (at 1.4 the soldiers spread just out of the arc).
  Measured: vs 3 clumped soldiers 36 hp left after 2 s, vs 4 soldiers 8 hp left after 4 s, a golem takes 27 s to fell
  (it ignores units, so it just walks on). Slow tests Balance_Samurai_*.
- Step 3: InfoRows needed nothing (the dojo is a UnitFactory: recipe, progress, power). UnitSheets: file name and
  top-down layout come from the defaults; UnitAnimation already shows the act frames from AttackCooldown. Menu Armé
  (after the radio car factory), texture path Buildings/dojo.png (drawn in step 4). UI/SlashLook (arc round the samurai
  through the unit hit, ±0.45 rad, fade) + test; CombatView draws Slash shots as that arc (glow + bright line).
