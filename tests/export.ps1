<#
  Builds the game for Windows, Linux and macOS (export_presets.cfg) and packs each into one zip to send:
  builds/Leksakskrig-windows.zip, -linux.zip, -macos.zip.

    tests/export.ps1 [-Only windows]

  One line per platform:  EXPORT windows OK 68 MB   /   EXPORT macos FAIL: <first error>
  Needs the Godot .NET export templates for the editor's version (Editor > Manage Export Templates).
  Godot: $env:GODOT, else the newest Godot*mono*console.exe under ~/Downloads.
#>
param([string]$Only = "")

$ErrorActionPreference = "Continue"
$root = Split-Path $PSScriptRoot
$project = Join-Path $root "factory-td"
$builds = Join-Path $root "builds"
$godot = $env:GODOT
if (-not $godot) {
	$godot = Get-ChildItem (Join-Path $HOME "Downloads") -Recurse -Filter "Godot*mono*console.exe" -ErrorAction SilentlyContinue |
		Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName
}
if (-not $godot) { Write-Output "EXPORT SKIPPED (no Godot found: set `$env:GODOT)"; exit 0 }

$platforms = @(
	@{ Key = "windows"; Preset = "Windows"; File = "windows/Leksakskrig.exe" },
	@{ Key = "linux"; Preset = "Linux"; File = "linux/Leksakskrig.x86_64" },
	@{ Key = "macos"; Preset = "macOS"; File = "macos/Leksakskrig.zip" }
) | Where-Object { -not $Only -or $_.Key -eq $Only }

& $godot --headless --path $project --import 2>&1 | Out-Null
$failed = 0
foreach ($p in $platforms) {
	$folder = Join-Path $builds $p.Key
	if (Test-Path $folder) { Remove-Item $folder -Recurse -Force }
	New-Item -ItemType Directory -Force $folder | Out-Null
	$target = Join-Path $builds $p.File
	$log = & $godot --headless --path $project --export-release $p.Preset $target 2>&1 | Out-String
	$zip = Join-Path $builds "Leksakskrig-$($p.Key).zip"
	if (Test-Path $zip) { Remove-Item $zip -Force }
	if (-not (Test-Path $target)) {
		$failed++
		$first = ($log -split "`r?`n" | Where-Object { $_ -match "ERROR|error" } | Select-Object -First 2) -join " | "
		Write-Output "EXPORT $($p.Key) FAIL: $first"
		continue
	}
	# macOS already comes as a zip with the .app inside; the others get their folder zipped. All get the
	# Swedish read-me for the friend (how to start it, firewall/Gatekeeper, how to connect). Python keeps the
	# Linux program runnable (Unix mode 755); without Python, PowerShell zips it (then: chmod +x).
	$readme = Join-Path $root "docs/LAS-MIG.txt"
	if ($p.Key -eq "macos") { Move-Item $target $zip }
	if (Get-Command python -ErrorAction SilentlyContinue) {
		& python (Join-Path $root "tools/build/zip_build.py") $folder $zip --exec (Split-Path $p.File -Leaf) --add $readme
	}
	elseif ($p.Key -eq "macos") { Compress-Archive -Path $readme -DestinationPath $zip -Update }
	else { Copy-Item $readme $folder; Compress-Archive -Path (Join-Path $folder "*") -DestinationPath $zip }
	$mb = [math]::Round((Get-Item $zip).Length / 1MB)
	Write-Output "EXPORT $($p.Key) OK $mb MB -> builds/Leksakskrig-$($p.Key).zip"
}
exit $failed
