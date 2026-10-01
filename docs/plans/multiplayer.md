# Plan: online multiplayer (host = server, lockstep), start menu, cross-platform build

Goal: a friend installs one download (Windows/Linux/macOS), starts the game, picks "Anslut" in the start
menu, types the host's address and plays against me. One player's game is the host (the server);
there is no separate server program. "Spela lokalt" keeps today's game against the bot.

## Approach (decided unless noted)
- **Deterministic lockstep, host relays.** Both machines run the full simulation. Only player
  commands go over the network (place, remove, configure: today the view's only three calls into World).
  A command made on tick T runs on tick T + InputDelay on both machines. The host collects every
  player's commands for a tick and sends the bundle to everyone. A machine only steps tick T when it has
  that tick's bundle. The traffic is tiny (a few bytes per tick) and cheating by editing state is
  impossible: any change shows up in the checksum.
- **Checksums against desync:** every 20 ticks each side sends `World.Checksum()`, and the host compares.
  On a mismatch the game stops and shows "Ur synk vid tick N" (and writes a log file), so a
  determinism bug is never played through silently.
- **Transport: Godot's built-in ENet (UDP)**, used as a raw packet pipe (`ENetMultiplayerPeer`,
  reliable ordered channel), not RPCs. It works the same on every platform. All protocol logic is pure C#
  in `Scripts/Net/` behind an `ITransport` interface, so it is tested with a fake network in xUnit,
  like the rest of the sim.
- **Reaching the host:**
  - On the same LAN: connect to the host's IP (the menu shows it), port 7777.
  - Over the internet: the host tries UPnP (Godot's `Upnp` class) to open the port automatically. If
    the router refuses: forward UDP 7777 by hand, or both players join a virtual LAN (Tailscale/ZeroTier,
    free) and connect as on a LAN.
  - NOT in this plan: a relay server or NAT hole punching. That would need a server on the internet,
    which conflicts with "host = server"; it can be added later as its own plan.
- **Bot stays local only** (local mode). An online match is 2 humans.

## Test loop (token budget)
- `task test -- Net` = the pure network/lockstep tests (fake network with latency and jitter, seeded).
- The golden checksum must stay unchanged throughout: local play goes through the same command path
  with delay 0, so it must play exactly as now.
- `task mp:smoke` (step 7) = two headless Godot instances over localhost with bots on both sides. It
  prints one line: `MP OK 1200 ticks, checksums equal` or `MP FAIL desync at tick N`.

## Steps
- [ ] 0. Branch `multiplayer` (from `refactor`, which is not merged to main yet). Baseline: `task check`,
      golden value, fast-suite time in the log.
- [ ] 1. Commands as data (Sim):
      a) `Sim/Commands.cs`: `PlayerCommand` (Kind Place/Remove/Configure, Player, Type, X, Y, Facing),
         `World.Apply(command)` = today's TryPlace/TryRemove/TryConfigure. Commands queued for tick T run at
         the start of tick T in a fixed order (player, then arrival order).
      b) Binary codec (`CommandCodec`: fixed little-endian layout, no reflection, no strings).
      c) BuildController and BotPlayer go through an `ICommandSink` instead of calling World directly. The
         drag-belt logic reads `World.CanPlace` (prediction) instead of the TryPlace result, and pending
         placements are drawn as ghosts until they land.
      Tests: codec round trip for every kind × type; Apply == the direct call; queued order; golden
      unchanged (local delay 0).
- [ ] 2. Determinism audit for different machines (the golden test only proves the same machine):
      a guard test that scans `Scripts/Sim/*.cs` text for `float`, `double`, `Math.Sqrt`, `System.Random`,
      `DateTime`, `GetHashCode`, `HashSet<`/`Dictionary<` that are *iterated* over keys whose hash
      changes between processes (reference types, tuples: `HashCode` is seeded per process). Known spot:
      `BotPlayer.cs` HashSet<(int,int)> (check iteration; only `Contains` is safe). Fix what turns up.
      Test: the same match in two fresh processes (`dotnet test` child run) gives the same checksum.
- [ ] 3. Lockstep core (pure, `Scripts/Net/`):
      a) Messages + codec: Hello(protocol version, game build hash, name), Welcome(player index, map seed,
         settings), Reject(reason), Start(start tick), Turn(tick, commands), Hash(tick, checksum),
         Ping/Pong, Bye.
      b) `ITransport` (Send(peer, bytes), Poll → Connected/Data/Disconnected) + `FakeNetwork` for tests
         (seeded latency, jitter, ordering kept like ENet's reliable channel).
      c) `HostSession` / `ClientSession`: handshake, version check, input delay (start 3 ticks = 150 ms,
         adjusted from measured ping, the same for both sides), turn buffer, `CanStep(tick)`, stall when
         input is missing (never guess).
      Tests: 2 players + bots via commands over the fake network at 0/150/400 ms with jitter, 2 simulated
      minutes → equal checksums every tick; the wrong version is rejected; a stall resumes when the late
      packet arrives; a tampered world → desync is reported with the tick.
- [ ] 4. Disconnect and desync handling (pure + tests): no packets for 10 s → "Motståndaren tappade
      anslutningen" (the remaining player wins, or goes back to the menu); "väntar på motståndaren…"
      after 1 s of stall; desync → stop + log file (`user://desync-<tick>.txt` with both checksums and the
      last commands).
- [ ] 5. Godot adapter (View): `View/Net/ENetTransport.cs` (CreateServer(7777, max 1 client),
      CreateClient(ip, port), Poll/PutPacket). Game.cs takes a `MatchSetup` (mode, seed, local player,
      session); the tick loop steps only when the session allows it. The client is player 1: the camera
      starts in the right-hand room, and LocalPlayer flows to every view (check the views that assume
      player 0). Command-line switches `--host`, `--join <ip>`, `--bot` (for the smoke test).
      `task build` + a screenshot as player 1.
- [ ] 6. Start menu (`Scenes/Menu.tscn` = new main scene, `View/MainMenu.cs`, UiTheme style). The logic is
      in `Scripts/UI/MenuModel.cs` (states, address parsing/validation, remembered address) with tests:
      - "Spela lokalt" (difficulty: armyDelayTicks)
      - "Hosta match" (shows LAN IPs + public IP/UPnP status, "Väntar på motståndare…", Start when
        connected)
      - "Anslut" (IP[:port] field, remembered in `user://settings.cfg`, "Ansluter…", errors in plain
        Swedish)
      - "Avsluta"
      In-game Esc menu: "Lämna match" back to the menu.
- [ ] 7. `task mp:smoke`: a script starts a headless host (`--host --bot`) and a client
      (`--join 127.0.0.1 --bot`), lets them play 60 s, each writes the final tick and checksum, and the
      script compares them → one line. Run before every multiplayer commit.
- [ ] 8. Internet: UPnP port mapping on hosting (status line in the host screen: "Port öppnad" /
      "Öppna UDP 7777 i routern eller använd Tailscale"), public IP via the UPnP gateway. Test against
      a real friend.
- [ ] 9. Cross-platform builds: install Godot 4.7 .NET export templates; `export_presets.cfg` for
      Windows x86_64, Linux x86_64, macOS (universal); `task export` → `builds/Leksakskrig-<os>.zip` (one
      file to send; .NET exports are a folder, the zip makes it one file). Notes for the friend: Windows
      firewall prompt (allow), macOS unsigned app (right-click → Öppna).
- [ ] 10. CLAUDE.md (network design, Net tests, mp:smoke, export), `task check`, merge.

## Risks
- Determinism between OS/CPU: the sim is integer-only and has a golden checksum; step 2 adds a guard
  test. .NET integer math is the same on x64/ARM.
- Feel: with lockstep your own building appears after the input delay (~150 ms); ghosts cover it
  (step 1c).
- The host's router: without UPnP/port forwarding a friend outside the LAN can't connect. A virtual LAN
  (Tailscale) is the fallback until a relay plan exists.
- The game must be the same build on both sides: the build hash in Hello rejects mismatches with a
  clear message.

## Log
