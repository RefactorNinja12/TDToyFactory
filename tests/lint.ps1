<#
  Low-noise lint: dotnet format (whitespace per .editorconfig, code style, analyzers) in verify mode.

    tests/lint.ps1          check both projects
    tests/lint.ps1 -Fix     apply the automatic fixes instead

  Prints one line per file and rule (capped), then one summary line:
    LINT OK in 9.1s   /   LINT FAIL 1735 issues in 1 file in 9.3s (task fmt fixes whitespace/style)
  One badly saved file can produce thousands of raw diagnostics, so never print dotnet format's own output.
#>
param([switch]$Fix)

$ErrorActionPreference = "Continue"
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$root = Split-Path $PSScriptRoot
$projects = @("factory-td/FactoryTD.csproj", "tests/FactoryTD.Sim.Tests/FactoryTD.Sim.Tests.csproj")
$reports = Join-Path $PSScriptRoot "FactoryTD.Sim.Tests/TestResults/lint"
if (Test-Path $reports) { Remove-Item $reports -Recurse -Force }
New-Item -ItemType Directory -Force $reports | Out-Null

$watch = [Diagnostics.Stopwatch]::StartNew()
# Formatting code that doesn't parse gives hundreds of bogus whitespace fixes: show the compile errors instead.
$build = & (Join-Path $PSScriptRoot "build.ps1")
if ($LASTEXITCODE -ne 0) {
	$build | Select-Object -SkipLast 1 | ForEach-Object { Write-Output $_ }
	Write-Output "LINT SKIPPED: the game doesn't compile"
	exit 1
}
$failedToRun = @()
foreach ($project in $projects) {
	$name = [IO.Path]::GetFileNameWithoutExtension($project)
	$arguments = @("format", (Join-Path $root $project), "--severity", "warn", "--report", (Join-Path $reports "$name.json"))
	if (-not $Fix) { $arguments += "--verify-no-changes" }
	$output = & dotnet @arguments 2>&1 | Out-String
	# Exit 2 = files need formatting (reported below); anything else non-zero = dotnet format itself failed.
	if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne 2) {
		$failedToRun += $name
		$errors = [regex]::Matches($output, "[^\r\n]*error CS\d+[^\r\n]*") | ForEach-Object { $_.Value -replace "\s*\[[^\]]*\]$", "" } | Select-Object -Unique -First 5
		if ($errors) { $errors | ForEach-Object { Write-Output ($_ -replace [regex]::Escape($root), "") } }
		else { Write-Output (($output.Trim() -split "`r?`n") | Select-Object -Last 5) }
	}
}
$seconds = [math]::Round($watch.Elapsed.TotalSeconds, 1)
if ($failedToRun) { Write-Output "LINT ERROR (could not run on $($failedToRun -join ', ')) in ${seconds}s"; exit 1 }

# The same linked Sim file shows up in both projects' reports: count each file/line/rule once.
$issues = @{}
foreach ($report in Get-ChildItem $reports -Filter *.json) {
	foreach ($file in (Get-Content $report.FullName -Raw | ConvertFrom-Json)) {
		$path = $file.FilePath.Replace($root, "").TrimStart("\", "/")
		foreach ($change in $file.FileChanges) {
			$issues["$path|$($change.LineNumber)|$($change.DiagnosticId)"] = [pscustomobject]@{
				File = $path; Line = $change.LineNumber; Rule = $change.DiagnosticId; Text = $change.FormatDescription
			}
		}
	}
}

if ($issues.Count -eq 0) { Write-Output "LINT OK in ${seconds}s"; exit 0 }
if ($Fix) { Write-Output "FIXED $($issues.Count) issues in ${seconds}s (rerun to see what is left)"; exit 0 }

$groups = @($issues.Values | Group-Object File, Rule | Sort-Object Count -Descending)
foreach ($group in $groups | Select-Object -First 10) {
	$first = $group.Group | Sort-Object Line | Select-Object -First 1
	$text = $first.Text -replace "\s*\[.*$", ""
	if ($text -match "\\r\\n") { $text = "line endings (CRLF), " + $text.Substring(0, [math]::Min(40, $text.Length)) }
	elseif ($text.Length -gt 110) { $text = $text.Substring(0, 110) + "..." }
	Write-Output ("{0} {1}x {2} (first line {3}): {4}" -f $first.File, $group.Count, $first.Rule, $first.Line, $text)
}
if ($groups.Count -gt 10) { Write-Output "... and $($groups.Count - 10) more file/rule groups" }
$files = @($issues.Values | Select-Object -ExpandProperty File -Unique).Count
Write-Output "LINT FAIL $($issues.Count) issues in $files file(s) in ${seconds}s (task fmt fixes whitespace/style)"
exit 1
