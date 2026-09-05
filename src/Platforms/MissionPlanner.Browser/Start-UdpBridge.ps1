[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$browserProject = Join-Path $PSScriptRoot 'MissionPlanner.Browser.csproj'
$bridgeProject = Join-Path $PSScriptRoot '../MissionPlanner.BrowserBridge/MissionPlanner.BrowserBridge.csproj'
dotnet build $browserProject --configuration Debug
if ($LASTEXITCODE -ne 0) { throw 'Browser build failed.' }
$manifest = Join-Path $PSScriptRoot 'bin/Debug/net10.0-browser/MissionPlanner.Browser.staticwebassets.runtime.json'
Write-Host 'Open http://127.0.0.1:7170/ and select UDP port 14550.'
Write-Host 'Configure the simulator to send MAVLink to 127.0.0.1:14550. Leave this shell running.'
dotnet run --project $bridgeProject --configuration Debug -- --BrowserAssets $manifest
if ($LASTEXITCODE -ne 0) { throw 'Browser bridge exited with an error.' }
