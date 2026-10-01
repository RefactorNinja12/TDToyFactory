<#
  Low-noise test runner for the simulation tests.

    tests/run.ps1                 fast tests (everything not marked Speed=Slow)
    tests/run.ps1 Farming         fast tests whose name contains "Farming"
    tests/run.ps1 -Slow           only the slow scenario tests
    tests/run.ps1 -All            everything
    tests/run.ps1 -Slow -Report   also print what each passing test measured (balance numbers) and its time
    tests/run.ps1 -All -Timing    also list the 10 slowest tests (one line each)

  Prints compile errors, or each failing test with its message and the line in the test file,
  then one summary line:  PASS 142/142 (fast) in 3.1s   /   FAIL 2/142 ...
#>
param(
	[string]$Name = "",
	[switch]$Slow,
	[switch]$All,
	[switch]$Report,
	[switch]$Timing
)

# Continue: dotnet writes test failures to stderr, which Windows PowerShell would otherwise treat as fatal.
$ErrorActionPreference = "Continue"
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$project = Join-Path $PSScriptRoot "FactoryTD.Sim.Tests"
$results = Join-Path $project "TestResults"
$trx = Join-Path $results "run.trx"
if (Test-Path $results) { Remove-Item $results -Recurse -Force }

$filters = @()
if ($Slow) { $filters += "Speed=Slow"; $scope = "slow" }
elseif (-not $All) { $filters += "Speed!=Slow"; $scope = "fast" }
else { $scope = "all" }
if ($Name) { $filters += "FullyQualifiedName~$Name"; $scope += ", ~$Name" }

$arguments = @("test", $project, "--nologo", "-v", "q", "--logger", "trx;LogFileName=run.trx", "--results-directory", $results)
if ($filters.Count -gt 0) { $arguments += @("--filter", ($filters -join "&")) }

$watch = [Diagnostics.Stopwatch]::StartNew()
$output = & dotnet @arguments 2>&1 | Out-String
$seconds = [math]::Round($watch.Elapsed.TotalSeconds, 1)

# Compile errors: show each once, without the project path noise.
$errors = [regex]::Matches($output, "[^\r\n]*error CS\d+[^\r\n]*") | ForEach-Object {
	($_.Value -replace "\s*\[[^\]]*\.csproj\]$", "") -replace [regex]::Escape((Split-Path $PSScriptRoot)), ""
} | Select-Object -Unique
if ($errors) {
	$errors | ForEach-Object { Write-Output $_ }
	Write-Output "BUILD FAILED ($(@($errors).Count) errors) in ${seconds}s"
	exit 1
}

if (-not (Test-Path $trx)) {
	Write-Output ($output.Trim() -split "`r?`n" | Select-Object -Last 15)
	Write-Output "NO RESULTS ($scope) in ${seconds}s"
	exit 1
}

[xml]$xml = Get-Content $trx
$ns = @{ t = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010" }
$runs = Select-Xml -Xml $xml -XPath "//t:UnitTestResult" -Namespace $ns | ForEach-Object { $_.Node }
$total = @($runs).Count
$failed = @($runs | Where-Object { $_.outcome -ne "Passed" })

function Short($name) { $name -replace '^FactoryTD\.Sim\.Tests\.', '' }

# Failures with the exact same message almost always share one cause: one block per message.
# At most $shown blocks in full; the rest only by name.
$shown = 6
$groups = @($failed | Group-Object { "$($_.Output.ErrorInfo.Message)".Trim() })
foreach ($group in $groups | Select-Object -First $shown) {
	$f = $group.Group[0]
	$others = if ($group.Count -gt 1) { "  (+$($group.Count - 1) more with the same message)" } else { "" }
	Write-Output "FAIL $(Short $f.testName)$others"
	$message = $f.Output.ErrorInfo.Message
	if ($message) { ($message.Trim() -split "`r?`n") | Select-Object -First 6 | ForEach-Object { Write-Output "     $_" } }
	# Only frames inside the test project, e.g. "at ...Tests.Foo() in ...\FarmingTests.cs:line 42".
	$stack = $f.Output.ErrorInfo.StackTrace
	if ($stack) {
		($stack -split "`r?`n") | Where-Object { $_ -match "FactoryTD\.Sim\.Tests\\(Support\\)?[^\\]+\.cs:line" } | Select-Object -First 2 |
			ForEach-Object { Write-Output ("     " + ($_.Trim() -replace ".*\\(\w+\.cs:line \d+)", '@ $1')) }
	}
	if ($group.Count -gt 1) {
		$names = $group.Group | Select-Object -Skip 1 -First 5 | ForEach-Object { (Short $_.testName) -replace '\(.*$', '' }
		Write-Output "     also: $($names -join ', ')$(if ($group.Count -gt 6) { ', ...' })"
	}
}
if ($groups.Count -gt $shown) {
	$rest = $groups | Select-Object -Skip $shown | ForEach-Object { $_.Group } | Select-Object -First 10 | ForEach-Object { (Short $_.testName) -replace '\(.*$', '' }
	Write-Output "also failing (other messages): $($rest -join ', ')$(if (($groups | Select-Object -Skip $shown | Measure-Object -Property Count -Sum).Sum -gt 10) { ', ...' })"
}

# -Report: the scenario tests' own measurements (balance numbers) and how long each took.
if ($Report) {
	foreach ($r in $runs | Where-Object { $_.outcome -eq "Passed" -and $_.Output.StdOut }) {
		$duration = [math]::Round(([TimeSpan]::Parse($r.duration)).TotalSeconds, 1)
		Write-Output "  $(Short $r.testName) (${duration}s)"
		($r.Output.StdOut.Trim() -split "`r?`n") | ForEach-Object { Write-Output "     $_" }
	}
}

# -Timing: the slowest tests, to see where the feedback loop spends its time.
if ($Timing) {
	$runs | Sort-Object { [TimeSpan]::Parse($_.duration) } -Descending | Select-Object -First 10 | ForEach-Object {
		Write-Output ("  {0,5:N1}s  {1}" -f ([TimeSpan]::Parse($_.duration)).TotalSeconds, (Short $_.testName))
	}
}

if ($total -eq 0) { Write-Output "NO TESTS MATCHED ($scope) in ${seconds}s"; exit 1 }
if ($failed.Count -eq 0) { Write-Output "PASS $total/$total ($scope) in ${seconds}s"; exit 0 }
Write-Output "FAIL $($failed.Count)/$total ($scope) in ${seconds}s"
exit 1
