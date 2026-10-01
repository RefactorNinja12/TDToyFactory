<#
  Copy-paste check: jscpd (pinned, via npx; config in .jscpd.json) over the game and test code.

    tests/dupes.ps1

  Any block of 50+ tokens that appears twice fails. Prints one line per clone (max 10), then one summary:
    DUPES OK (73 files) in 3.1s   /   DUPES FAIL 2 clones (23 lines) in 3.4s
  Fix by extracting a shared helper or base class, not by raising minTokens.
#>
$ErrorActionPreference = "Continue"
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$root = Split-Path $PSScriptRoot
$report = Join-Path $PSScriptRoot "FactoryTD.Sim.Tests/TestResults/dupes"
if (-not (Get-Command npx.cmd -ErrorAction SilentlyContinue)) { Write-Output "DUPES SKIPPED (npx not found: install Node.js)"; exit 0 }
if (Test-Path $report) { Remove-Item $report -Recurse -Force }

$watch = [Diagnostics.Stopwatch]::StartNew()
Push-Location $root
# npx.cmd with a quoted package: through the npx.ps1 shim npm could not find jscpd@version to run.
$output = & npx.cmd --yes "jscpd@5.4.0" --config .jscpd.json 2>&1 | Out-String
Pop-Location
$seconds = [math]::Round($watch.Elapsed.TotalSeconds, 1)
$file = Join-Path $report "jscpd-report.json"
if (-not (Test-Path $file)) {
	Write-Output (($output.Trim() -split "`r?`n") | Select-Object -Last 5)
	Write-Output "DUPES ERROR (jscpd wrote no report) in ${seconds}s"
	exit 1
}
$data = Get-Content $file -Raw | ConvertFrom-Json
$total = $data.statistics.total
$clones = @($data.duplicates)
if ($clones.Count -eq 0) { Write-Output "DUPES OK ($($total.sources) files) in ${seconds}s"; exit 0 }

function At($f) { "$(Split-Path $f.name -Leaf):$($f.start)-$($f.end)" }
$clones | Sort-Object { - $_.lines } | Select-Object -First 10 | ForEach-Object {
	Write-Output "DUPE $($_.lines) lines  $(At $_.firstFile)  ~  $(At $_.secondFile)"
}
Write-Output "DUPES FAIL $($clones.Count) clones ($($total.duplicatedLines) lines) in ${seconds}s (extract a shared helper)"
exit 1
