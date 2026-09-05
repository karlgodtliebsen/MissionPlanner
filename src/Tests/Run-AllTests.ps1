#requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
Get-Command dotnet, node -ErrorAction Stop | Out-Null
$repositoryRoot = Split-Path (Split-Path $PSScriptRoot)
$resultsRoot = Join-Path $repositoryRoot ('TestResults/all-tests/' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $resultsRoot -Force | Out-Null
$failures = [System.Collections.Generic.List[string]]::new()

# Build sequentially because the projects share intermediate output directories.
$projects = Get-ChildItem -Path "$PSScriptRoot/*/*.csproj" |
    Where-Object BaseName -ne 'MissionPlanner.Test.Support' | Sort-Object FullName
foreach ($project in $projects) {
    Write-Host "Running $($project.BaseName)..."
    $projectResults = Join-Path $resultsRoot $project.BaseName
    $log = Join-Path $resultsRoot "$($project.BaseName).log"
    & dotnet test $project.FullName --configuration $Configuration '-p:UsedAvaloniaProducts=' `
        --logger trx --results-directory $projectResults --blame-hang-timeout 2m --verbosity minimal *> $log
    if ($LASTEXITCODE -ne 0) { $failures.Add($project.BaseName) }
    Get-Content -LiteralPath $log -Tail 5
}

Write-Host 'Running browser JavaScript tests...'
$browserLog = Join-Path $resultsRoot 'browser-platform.log'
& node --test (Join-Path $PSScriptRoot 'browser-platform.test.mjs') *> $browserLog
if ($LASTEXITCODE -ne 0) { $failures.Add('browser-platform') }
Get-Content -LiteralPath $browserLog -Tail 9
Write-Host "Logs and TRX results: $resultsRoot"
if ($failures.Count -gt 0) {
    throw "Test suites failed: $($failures -join ', '). See the logs above."
}
