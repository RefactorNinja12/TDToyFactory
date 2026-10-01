<#
  Runs only the tests for what changed since the last commit (staged, unstaged and new files):

    tests/changed.ps1

  Changed game files map to test areas (table below), changed test files run their own classes, and the
  golden bot match always comes along when simulation code changed (it catches behaviour changes the area
  tests miss). Core files (World, units, map, grid search...) run every fast test. View files only need
  a build (tests don't cover Godot code). Nothing changed: says so and stops.
  First line: what it decided. Then the usual one-line PASS/FAIL from run.ps1.
#>
$ErrorActionPreference = "Continue"
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$root = Split-Path $PSScriptRoot
Push-Location $root

$changed = @(git diff --name-only HEAD) + @(git ls-files --others --exclude-standard) |
	Where-Object { $_ -match "\.cs$" } | Sort-Object -Unique
Pop-Location
if (-not $changed) { Write-Output "NOTHING CHANGED (no .cs files since the last commit)"; exit 0 }

# Game file name (without .cs) -> test name filters. Anything in Sim not listed runs all fast tests.
$areas = @{
	"Power" = "Power|Charge"; "PowerGrid" = "Power|Charge"
	"Vision" = "Vision|Fog|Knowledge|Lamp|Minimap|LightSources"; "Scouting" = "Scout|Vision"
	"Farming" = "Farm|Kitchen|Upkeep|Cheese"; "Warehouse" = "Storage"
	"Logistics" = "Splitter|Sorter|Junction|Belt|EveryType"; "Conveyor" = "Belt|ConveyorLook"
	"Extractor" = "Extractor|Cheese"; "Assembler" = "Crafting|Assembler"
	"Tower" = "Tower|Targeting|Combat|Balance_Tower"; "Treadmill" = "Treadmill|CheeseHunter|LastBuilder"
	"UnitFactory" = "UnitFactory|Tent|Treadmill|Upkeep"; "BotPlayer" = "Bot"
	"MapObstacles" = "Obstacle|Map"; "Core" = "Storage|Smoke"
	"Item" = "EveryType|Texts"; "Checksum" = "Determinism"
}
$core = $false; $build = $false; $filters = New-Object System.Collections.Generic.List[string]
foreach ($file in $changed) {
	$name = [IO.Path]::GetFileNameWithoutExtension($file)
	if ($file -match "^factory-td/Scripts/Sim/") {
		if ($areas.ContainsKey($name)) { $filters.Add($areas[$name]); $filters.Add("Golden") } else { $core = $true }
	}
	elseif ($file -match "^factory-td/Scripts/UI/") { $filters.Add("Ui|Hud|Hotkey|EveryType|InfoRows|Fog|Power|ConveyorLook|DragPath|FoodMeter|Texts") }
	elseif ($file -match "^factory-td/Scripts/Net/") { $filters.Add("Net|Command") }
	elseif ($file -match "^factory-td/Scripts/") { $build = $true }
	elseif ($file -match "^tests/FactoryTD\.Sim\.Tests/Support/") { $core = $true }
	elseif ($file -match "^tests/FactoryTD\.Sim\.Tests/.+\.cs$") {
		$classes = Select-String -Path (Join-Path $root $file) -Pattern "public class (\w+)" | ForEach-Object { $_.Matches[0].Groups[1].Value }
		foreach ($c in $classes) { $filters.Add($c) }
	}
}

$names = ($changed | ForEach-Object { [IO.Path]::GetFileName($_) }) -join ", "
if ($build) {
	Write-Output "changed: $names -> View: build"
	& (Join-Path $PSScriptRoot "build.ps1")
	if ($LASTEXITCODE -ne 0) { exit 1 }
}
if ($core) {
	Write-Output "changed: $names -> core: all fast tests"
	& (Join-Path $PSScriptRoot "run.ps1")
	exit $LASTEXITCODE
}
if ($filters.Count -eq 0) { if (-not $build) { Write-Output "changed: $names -> no tests for these" }; exit 0 }
$filter = (($filters -join "|") -split "\|" | Sort-Object -Unique) -join "|"
Write-Output "changed: $names -> $filter"
& (Join-Path $PSScriptRoot "run.ps1") $filter
exit $LASTEXITCODE
