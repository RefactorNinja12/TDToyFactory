<#
  Low-noise test runner for the simulation tests.

    tests/run.ps1                 fast tests (everything not marked Speed=Slow)
    tests/run.ps1 Farming         fast tests whose name contains "Farming"
    tests/run.ps1 -Slow           only the slow scenario tests
    tests/run.ps1 -All            everything

  Prints compile errors, or each failing test with its message and the line in the test file,
  then one summary line:  PASS 142/142 (fast) in 3.1s   /   FAIL 2/142 ...
#>
param(
	[string]$Name = "",
	[switch]$Slow,
	[switch]$All
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

foreach ($f in $failed) {
	Write-Output "FAIL $($f.testName)"
	$message = $f.Output.ErrorInfo.Message
	if ($message) { ($message.Trim() -split "`r?`n") | Select-Object -First 6 | ForEach-Object { Write-Output "     $_" } }
	# Only the stack frame(s) inside the test project, e.g. "at ...Tests.Foo() in ...\FarmingTests.cs:line 42".
	$stack = $f.Output.ErrorInfo.StackTrace
	if ($stack) {
		($stack -split "`r?`n") | Where-Object { $_ -match "FactoryTD\.Sim\.Tests\\[^\\]+\.cs:line" } | Select-Object -First 2 |
			ForEach-Object { Write-Output ("     " + ($_.Trim() -replace ".*\\(\w+\.cs:line \d+)", '@ $1')) }
	}
}

# Slow runs: the scenario tests' own measurements (balance numbers) and how long each took.
if ($Slow -or $All) {
	foreach ($r in $runs | Where-Object { $_.outcome -eq "Passed" -and $_.Output.StdOut }) {
		$duration = [math]::Round(([TimeSpan]::Parse($r.duration)).TotalSeconds, 1)
		Write-Output "  $($r.testName -replace '^FactoryTD\.Sim\.Tests\.', '') (${duration}s)"
		($r.Output.StdOut.Trim() -split "`r?`n") | ForEach-Object { Write-Output "     $_" }
	}
}

if ($total -eq 0) { Write-Output "NO TESTS MATCHED ($scope) in ${seconds}s"; exit 1 }
if ($failed.Count -eq 0) { Write-Output "PASS $total/$total ($scope) in ${seconds}s"; exit 0 }
Write-Output "FAIL $($failed.Count)/$total ($scope) in ${seconds}s"
exit 1
