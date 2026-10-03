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
  - On the same LAN: connect to the host's IP (the menu shows it), the chosen port (default 7777).
  - Over the internet: the host tries UPnP (Godot's `Upnp` class) to open the port automatically. If
    the router refuses: forward the chosen UDP port by hand, or both players join a virtual LAN (Tailscale/ZeroTier,
    free) and connect as on a LAN.
  - NOT in this plan: a relay server or NAT hole punching. That would need a server on the internet,
    which conflicts with "host = server"; it can be added later as its own plan.
- **Port and password, chosen by the host:**
  - Port: the host picks it when starting a match (default 7777, allowed 1024–65535, remembered).
    The friend types `ip:port`.
  - Password: required, minimum 4 characters. The host types it and tells the friend.
  - The password never travels over the network, not even hashed and replayable. The host sends a
    random challenge (32 bytes, `RandomNumberGenerator`). The client replies with
    HMAC-SHA256(password, challenge + its own nonce), and the host checks it with
    `CryptographicOperations.FixedTimeEquals`.
  - Until the reply is approved the host sends no game data and ignores everything except the reply.
  - Wrong password → "Fel lösenord" and the connection is closed. After 3 failures from the same IP that
    IP is blocked for 30 s, so the password can't be guessed quickly.
  - The match traffic itself is not encrypted (ENet has no encryption). That's fine, because it only
    contains build commands, but it is stated in the plan.
- **Bot stays local only** (local mode). An online match is 2 humans.

## Test loop (token budget)
- `task test -- Net` = the pure network/lockstep tests (fake network with latency and jitter, seeded).
- The golden checksum must stay unchanged throughout: local play goes through the same command path
  with delay 0, so it must play exactly as now.
- `task mp:smoke` (step 7) = two headless Godot instances over localhost with bots on both sides. It
  prints one line: `MP OK 1200 ticks, checksums equal` or `MP FAIL desync at tick N`.

## Steps
- [x] 0. Branch `multiplayer` (from `refactor`, which is not merged to main yet). Baseline: `task check`,
      golden value, fast-suite time in the log.
- [x] 1. Commands as data (Sim):
      a) `Sim/Commands.cs`: `PlayerCommand` (Kind Place/Remove/Configure, Player, Type, X, Y, Facing),
         `World.Apply(command)` = today's TryPlace/TryRemove/TryConfigure. Commands queued for tick T run at
         the start of tick T in a fixed order (player, then arrival order).
      b) Binary codec (`CommandCodec`: fixed little-endian layout, no reflection, no strings).
      c) BuildController and BotPlayer go through an `ICommandSink` instead of calling World directly. The
         drag-belt logic reads `World.CanPlace` (prediction) instead of the TryPlace result, and pending
         placements are drawn as ghosts until they land.
      Tests: codec round trip for every kind × type; Apply == the direct call; queued order; golden
      unchanged (local delay 0).
- [x] 2. Determinism audit for different machines (the golden test only proves the same machine):
      a guard test that scans `Scripts/Sim/*.cs` text for `float`, `double`, `Math.Sqrt`, `System.Random`,
      `DateTime`, `GetHashCode`, `HashSet<`/`Dictionary<` that are *iterated* over keys whose hash
      changes between processes (reference types, tuples: `HashCode` is seeded per process). Known spot:
      `BotPlayer.cs` HashSet<(int,int)> (check iteration; only `Contains` is safe). Fix what turns up.
      Test: the same match in two fresh processes (`dotnet test` child run) gives the same checksum.
- [x] 3. Lockstep core (pure, `Scripts/Net/`):
      a) Messages + codec: Hello(protocol version, game build hash, name, client nonce), Challenge(32 random
         bytes), Proof(HMAC), Welcome(player index, map seed, settings), Reject(reason: version / wrong
         password / blocked / full), Start(start tick), Turn(tick, commands), Hash(tick, checksum),
         Ping/Pong, Bye.
      b) `ITransport` (Send(peer, bytes), Poll → Connected/Data/Disconnected) + `FakeNetwork` for tests
         (seeded latency, jitter, ordering kept like ENet's reliable channel).
      c) `HostSession` / `ClientSession`: handshake (version → challenge → password proof), version check, input delay (start 3 ticks = 150 ms,
         adjusted from measured ping, the same for both sides), turn buffer, `CanStep(tick)`, stall when
         input is missing (never guess).
      Tests: 2 players + bots via commands over the fake network at 0/150/400 ms with jitter, 2 simulated
      minutes → equal checksums every tick; the wrong version is rejected; the right password gets in, a wrong
      one gets "Fel lösenord" and no game data, the 4th attempt within 30 s is blocked, and a replayed old
      Proof doesn't work (new challenge every time); the password bytes never appear in any sent packet
      (the test searches the fake network's traffic); a stall resumes when the late
      packet arrives; a tampered world → desync is reported with the tick.
- [x] 4. Disconnect and desync handling (pure + tests): no packets for 10 s → "Motståndaren tappade
      anslutningen" (the remaining player wins, or goes back to the menu); "väntar på motståndaren…"
      after 1 s of stall; desync → stop + log file (`user://desync-<tick>.txt` with both checksums and the
      last commands).
- [x] 5. Godot adapter (View): `View/Net/ENetTransport.cs` (CreateServer(port, max 1 client),
      CreateClient(ip, port), Poll/PutPacket). Game.cs takes a `MatchSetup` (mode, seed, local player,
      session); the tick loop steps only when the session allows it. The client is player 1: the camera
      starts in the right-hand room, and LocalPlayer flows to every view (check the views that assume
      player 0). Command-line switches `--host [--port N]`, `--join <ip:port>`, `--password X`, `--bot` (for the smoke test).
      `task build` + a screenshot as player 1.
- [x] 6. Start menu (`Scenes/Menu.tscn` = new main scene, `View/MainMenu.cs`, UiTheme style). The logic is
      in `Scripts/UI/MenuModel.cs` (states, address parsing/validation, remembered address) with tests:
      - "Spela lokalt" (difficulty: armyDelayTicks)
      - "Hosta match": a port field (default 7777, validated) and a password field (required, at least 4
        characters, a "visa" eye toggle), then shows LAN IPs:port + public IP/UPnP status, "Väntar på motståndare…", Start when
        connected)
      - "Anslut": an IP[:port] field (remembered in `user://settings.cfg`) and a password field (never
        saved), "Ansluter…", errors in plain Swedish ("Fel lösenord", "Fel version av spelet", "Ingen
        svarar på adressen", "Spärrad en stund efter för många försök")
      MenuModel tests: port validation (text, 0, 80, 70000 → error), address parsing (`ip`, `ip:port`,
      bad ones), a password under 4 characters → error, the password not in the settings file.
      - "Avsluta"
      In-game Esc menu: "Lämna match" back to the menu.
- [x] 7. `task mp:smoke`: a script starts a headless host (`--host --port 7790 --password test --bot`) and a
      client (`--join 127.0.0.1:7790 --password test --bot`), lets them play 60 s, each writes the final tick and checksum, and the
      script compares them → one line. Run before every multiplayer commit.
- [x] 8. Internet: UPnP mapping of the chosen port on hosting, removed when the match ends (status line in
      the host screen: "Port öppnad" / "Öppna UDP <port> i routern eller använd Tailscale"), public IP via the UPnP gateway. Test against
      a real friend.
- [x] 9. Cross-platform builds: install Godot 4.7 .NET export templates; `export_presets.cfg` for
      Windows x86_64, Linux x86_64, macOS (universal); `task export` → `builds/Leksakskrig-<os>.zip` (one
      file to send; .NET exports are a folder, the zip makes it one file). Notes for the friend: Windows
      firewall prompt (allow), macOS unsigned app (right-click → Öppna).
- [x] 10. CLAUDE.md (network design, Net tests, mp:smoke, export), `task check`, merge.

## Risks
- Password: the host chooses it, so a weak password can be guessed, but the IP blocking makes that slow.
  The match traffic is unencrypted (build commands only).
- Determinism between OS/CPU: the sim is integer-only and has a golden checksum; step 2 adds a guard
  test. .NET integer math is the same on x64/ARM.
- Feel: with lockstep your own building appears after the input delay (~150 ms); ghosts cover it
  (step 1c).
- The host's router: without UPnP/port forwarding a friend outside the LAN can't connect. A virtual LAN
  (Tailscale) is the fallback until a relay plan exists.
- The game must be the same build on both sides: the build hash in Hello rejects mismatches with a
  clear message.

## Log
- Step 0: baseline `task check` green, 300 tests, golden 0x299E99DC031BB7E0, all tests 17.9 s.
- Step 1: Sim/Commands.cs (PlayerCommand 8 bytes LE, Read rejects unknown kind/type/facing; ICommandSink, DirectCommands); World.Apply (ignores unknown player / off-map). BotPlayer.Tick(world, sink): with a delay it waits until its last order landed (no double configure). BuildController sends commands (drag judges with CanPlace). Pending ghosts moved to step 5 (needs the online loop to see). Golden unchanged. CommandTests 7.
- Step 2: DeterminismGuardTests scans Scripts/Sim for float/double, Math.Sqrt & co, Random/Guid, clocks, GetHashCode/HashCode, threads (+ self-tests that the rules catch/leave alone). Sim was already clean. Dictionary/HashSet are NOT a risk: they enumerate their entry array in insertion order (hash only picks buckets), so the BotPlayer tuple sets are fine. The child-process test was dropped: the golden constant was recorded in an earlier process, so every run already compares across processes. Both players run the same exported build (bundled .NET runtime), so the same library code.
- Step 3+4: Scripts/Net (pure, linked into the tests): Transport (ITransport, NetEvent), Protocol (messages, PacketWriter/Reader that never throw, HMAC proof, BuildId = assembly MVID), MatchSession (lockstep: turn per step, input for step+delay made final when stepping, stall without turn, hash every 20 steps, ping/RTT, 10 s timeout, IsWaiting after 1 s, DesyncReport), HostSession (version/build check, challenge/proof, 3 wrong in 60 s -> address blocked 30 s, Full, Reject then hang up after 0.5 s, client may only command player 1, DelayFor(rtt) 3..12), ClientSession. Tests/Support/FakeNetwork (seeded latency/jitter, ordered, freeze/drop, records all packets). NetTests 26 (~3 s): 2 bots at 0/150/400 ms play 1 min with identical checksums every step; stall + resume; desync caught within 20 steps on both sides; replayed proof rejected; password bytes in no packet; wrong build; full; leave/silence/no host; junk packets ignored. Fix found by tests: an empty packet read as Bye. The desync log file and "you win when the opponent leaves" are UI (step 5). test:changed: Scripts/Net -> Net|Command.
- Step 5: View/Net/ENetTransport (ENetMultiplayerPeer as a raw reliable pipe; a client that never gets through = Disconnected; Close flushes first), View/Net/MatchSetup (static: session/transport, bot delay, --bot, --steps/--out). Game: world/local player/commands from the session; steps only via TryStep (waiting keeps at most one tick in hand); camera mirrored for the right-hand player; desync -> user://desync-TICK.txt; debug keys off online. MatchOverlay: "Väntar på X…", end panel (MenuModel.EndText), Esc menu (local play pauses, online not), leave -> Menu. BuildController draws sent placements faintly until they land (the 1c ghosts).
- Step 6: Scenes/Menu.tscn is the main scene (View/MainMenu.cs in UiTheme style): local (Lätt = armies wait 3 min / Normal), host (name, port, password + show, LAN addresses:port, Starta matchen when someone is in), join (name, ip:port, password). Remembered in user://settings.cfg (name, address, port; never the password). UI/MenuModel (port/address/password rules, LAN address filter, status and end texts, MenuSettings, LaunchArgs) + MenuModelTests 23.
- Step 7: tests/mp_smoke.ps1 + `task mp:smoke`: headless host (--host --port 7790 --password, --bot, --steps, --out) and client (--join 127.0.0.1:7790) through the real menu path and ENet; compares the "tick checksum" lines. 1200 steps: MP OK in 66 s; default 600 steps. Screenshot of the client: player 1 in the right room, its bot's belts land through lockstep.
- Step 8: View/Net/PortOpener (UPnP in the background: discover, map UDP port with a 2 h lease, falling back to a permanent one, public address; removed on leave/quit/window close, also if the host gave up mid-discovery). Host page shows MenuModel.InternetStatus (port opened + public ip:port / open UDP port yourself or use Tailscale). Tried against the real router here: port opened, public address found, mapping removed again. LAN addresses ordered home network first (WSL/VirtualBox adapters later). Not done here: a real friend outside the network (needs a person); the plan's checklist leaves that to the first real match.
- Step 9: Godot 4.7.2 .NET export templates installed (~/AppData/Roaming/Godot/export_templates/4.7.2.stable.mono, from the official release). factory-td/export_presets.cfg (now versioned: no secrets) for Windows x86_64 (embedded pck + .NET outputs = one .exe), Linux x86_64, macOS universal (ad-hoc signed, not notarised); project renamed to Leksakskrig; ETC2/ASTC import on (required for Apple Silicon). tests/export.ps1 + `task export` -> builds/Leksakskrig-{windows 69 MB, linux 59 MB, macos 121 MB}.zip, each with docs/LAS-MIG.txt (Swedish how-to for the friend); tools/build/zip_build.py keeps the Linux/macOS programs executable (755). Checked: the export MVID (BuildId) is the same on every platform/arch, so a Windows host accepts a Mac/Linux friend; the editor build has another id (both must use the exported zip). Two exported Windows exes played 200 steps over localhost: same checksum as the editor build (Debug = Release).
- Step 10: CLAUDE.md (design: multiplayer; structure: Scripts/Net, View/Net, menu; workflow: Net tests, mp:smoke, export; plan listed as done). Final `task check` + mp:smoke green. Not merged into main by me: merging is the user's call (PR link in the reply); the branch builds on `refactor`.
- After step 10 (user): no ports opened in the router, it felt unsafe. PortOpener/UPnP removed; the host page says to use Tailscale (MenuModel.InternetHint), Tailscale addresses (100.64-127.x) are listed and marked; LAS-MIG.txt and CLAUDE.md updated. The step 8 notes above are history.
