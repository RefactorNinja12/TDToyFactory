<#
  Low-noise build of the Godot C# project: each error/warning once, then one summary line.
    BUILD OK in 1.2s   /   BUILD FAIL (1 errors, 0 warnings) in 0.9s
#>
$ErrorActionPreference = "Continue"
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$root = Split-Path $PSScriptRoot

$watch = [Diagnostics.Stopwatch]::StartNew()
$output = & dotnet build (Join-Path $root "factory-td/FactoryTD.csproj") -nologo -v q 2>&1 | Out-String
$ok = $LASTEXITCODE -eq 0
$seconds = [math]::Round($watch.Elapsed.TotalSeconds, 1)

$lines = [regex]::Matches($output, "[^\r\n]*: (error|warning) [A-Z]+\d+[^\r\n]*") | ForEach-Object {
	($_.Value -replace "\s*\[[^\]]*\]$", "") -replace [regex]::Escape($root), ""
} | Select-Object -Unique
$errors = @($lines | Where-Object { $_ -match ": error " }).Count
$warnings = @($lines | Where-Object { $_ -match ": warning " }).Count
$lines | Select-Object -First 15 | ForEach-Object { Write-Output $_ }

if ($ok -and $warnings -eq 0) { Write-Output "BUILD OK in ${seconds}s"; exit 0 }
if ($ok) { Write-Output "BUILD OK with $warnings warnings in ${seconds}s"; exit 0 }
if ($errors -eq 0) { Write-Output (($output.Trim() -split "`r?`n") | Select-Object -Last 8) }
Write-Output "BUILD FAIL ($errors errors, $warnings warnings) in ${seconds}s"
exit 1
