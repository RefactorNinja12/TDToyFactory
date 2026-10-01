<#
  Line coverage of the game logic, as a short summary (never the raw report):

    tests/coverage.ps1            fast tests
    tests/coverage.ps1 -All       all tests (slow scenario tests too)

  Prints one line for Sim and one for UI, then the 10 least covered files:
    Sim 84.2% (3120/3705)   UI 91.0% (610/670)
      Sim/BotPlayer.cs        61.3%  (231 lines not run)
#>
param([switch]$All, [int]$Top = 10)

$ErrorActionPreference = "Continue"
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$root = Split-Path $PSScriptRoot
$project = Join-Path $PSScriptRoot "FactoryTD.Sim.Tests"
$results = Join-Path $project "TestResults/coverage"
if (Test-Path $results) { Remove-Item $results -Recurse -Force }

$arguments = @("test", $project, "--nologo", "-v", "q", "--collect", "XPlat Code Coverage", "--results-directory", $results)
if (-not $All) { $arguments += @("--filter", "Speed!=Slow") }
# The game code is compiled into the test assembly (linked files), which coverlet skips unless told.
$arguments += @("--", "DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.IncludeTestAssembly=true")
$watch = [Diagnostics.Stopwatch]::StartNew()
$output = & dotnet @arguments 2>&1 | Out-String
$seconds = [math]::Round($watch.Elapsed.TotalSeconds, 1)

$report = Get-ChildItem $results -Recurse -Filter coverage.cobertura.xml | Select-Object -First 1
if (-not $report) {
	Write-Output (($output.Trim() -split "`r?`n") | Select-Object -Last 8)
	Write-Output "NO COVERAGE in ${seconds}s (did the tests build?)"
	exit 1
}

[xml]$xml = Get-Content $report.FullName
# Per file: lines (by number) and whether any test ran them. A file shows up once per class in it.
$files = @{}
foreach ($class in $xml.SelectNodes("//class")) {
	$name = ($class.filename -replace "\\", "/")
	if ($name -notmatch "Scripts/(Sim|UI)/(.+)$") { continue }
	$key = "$($Matches[1])/$($Matches[2])"
	if (-not $files.ContainsKey($key)) { $files[$key] = @{} }
	foreach ($line in $class.lines.line) {
		$hit = [int]$line.hits -gt 0
		$files[$key][[int]$line.number] = $files[$key][[int]$line.number] -or $hit
	}
}

function Sum($prefix) {
	$total = 0; $covered = 0
	foreach ($key in $files.Keys | Where-Object { $_.StartsWith($prefix) }) {
		$total += $files[$key].Count
		$covered += @($files[$key].Values | Where-Object { $_ }).Count
	}
	$pct = if ($total) { [math]::Round(100 * $covered / $total, 1) } else { 0 }
	return "$($prefix.TrimEnd('/')) $pct% ($covered/$total)"
}

Write-Output "$(Sum 'Sim/')   $(Sum 'UI/')   in ${seconds}s"
$rows = foreach ($key in $files.Keys) {
	$total = $files[$key].Count
	$missed = @($files[$key].Values | Where-Object { -not $_ }).Count
	[pscustomobject]@{ File = $key; Pct = if ($total) { 100 * ($total - $missed) / $total } else { 100 }; Missed = $missed }
}
foreach ($row in $rows | Where-Object { $_.Missed -gt 0 } | Sort-Object Missed -Descending | Select-Object -First $Top) {
	Write-Output ("  {0,-28} {1,5:N1}%  ({2} lines not run)" -f $row.File, $row.Pct, $row.Missed)
}
