<#
  Multiplayer smoke test: two headless games on this machine, a host and a client over localhost (real ENet,
  real menu code path, password), a bot playing each side. Each writes "tick checksum" at the last step.

    tests/mp_smoke.ps1 [-Steps 600] [-Port 7790] [-Map nursery|garden]   (the host picks the map; the client gets it)

  One line:  MP OK 600 ticks on Nursery, checksums equal (2E1F...) in 36s
         /   MP FAIL host: ended Desync 340 / client: 1200 9A3B...
  Godot: $env:GODOT, else the newest Godot*mono*console.exe under ~/Downloads.
#>
param([int]$Steps = 600, [int]$Port = 7790, [string]$Map = "nursery")

$ErrorActionPreference = "Continue"
$root = Split-Path $PSScriptRoot
$project = Join-Path $root "factory-td"
$godot = $env:GODOT
if (-not $godot) {
	$godot = Get-ChildItem (Join-Path $HOME "Downloads") -Recurse -Filter "Godot*mono*console.exe" -ErrorAction SilentlyContinue |
		Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName
}
if (-not $godot) { Write-Output "MP SKIPPED (no Godot found: set `$env:GODOT)"; exit 0 }

$out = Join-Path $PSScriptRoot "FactoryTD.Sim.Tests/TestResults/mp"
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Force $out | Out-Null
$hostFile = Join-Path $out "host.txt"
$clientFile = Join-Path $out "client.txt"

$watch = [Diagnostics.Stopwatch]::StartNew()
$build = & (Join-Path $PSScriptRoot "build.ps1")
if ($LASTEXITCODE -ne 0) { $build | ForEach-Object { Write-Output $_ }; exit 1 }

function Start-Game($name, $userArgs) {
	$all = @("--headless", "--path", "`"$project`"", "--") + $userArgs
	Start-Process -FilePath $godot -ArgumentList $all -PassThru -WindowStyle Hidden `
		-RedirectStandardOutput (Join-Path $out "$name.log") -RedirectStandardError (Join-Path $out "$name.err")
}
$common = @("--password", "smoke-test", "--bot", "--steps", $Steps)
$hostGame = Start-Game "host" (@("--host", "--port", $Port, "--seed", 42, "--map", $Map, "--out", "`"$hostFile`"") + $common)
Start-Sleep -Milliseconds 1500
$clientGame = Start-Game "client" (@("--join", "127.0.0.1:$Port", "--out", "`"$clientFile`"") + $common)

# Real time: Steps / 20 seconds of play plus start-up; give up well after that.
$limit = [int]($Steps / 20 * 2 + 60)
$null = Wait-Process -Id $hostGame.Id, $clientGame.Id -Timeout $limit -ErrorAction SilentlyContinue
foreach ($game in @($hostGame, $clientGame)) { if (-not $game.HasExited) { Stop-Process -Id $game.Id -Force } }
$seconds = [math]::Round($watch.Elapsed.TotalSeconds)

$h = if (Test-Path $hostFile) { (Get-Content $hostFile -Raw).Trim() } else { "no result" }
$c = if (Test-Path $clientFile) { (Get-Content $clientFile -Raw).Trim() } else { "no result" }
if ($h -eq $c -and $h -match "^$Steps ([0-9A-F]{16}) (\w+)$") {
	Write-Output "MP OK $Steps ticks on $($Matches[2]), checksums equal ($($Matches[1])) in ${seconds}s"
	exit 0
}
foreach ($name in "host", "client") {
	$errors = Get-Content (Join-Path $out "$name.log"), (Join-Path $out "$name.err") -ErrorAction SilentlyContinue |
		Where-Object { $_ -match "ERROR|Exception|desync" } | Select-Object -First 3
	$errors | ForEach-Object { Write-Output "  ${name}: $_" }
}
Write-Output "MP FAIL host: $h / client: $c in ${seconds}s (logs in tests/FactoryTD.Sim.Tests/TestResults/mp)"
exit 1
