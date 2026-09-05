[CmdletBinding()]
param(
    [ValidateSet('Thorium', 'Comet', 'Edge', 'Chrome')]
    [string]$Browser = 'Thorium',
    [string]$AppUrl = 'https://localhost:7169/',
    [string]$BrowserPath
)

$ErrorActionPreference = 'Stop'
$applicationUri = [Uri]$AppUrl
if (-not $applicationUri.IsAbsoluteUri -or $applicationUri.Scheme -notin @('http', 'https')) {
    throw 'AppUrl must be an absolute HTTP or HTTPS application URL.'
}
$AppUrl = $applicationUri.AbsoluteUri
$debuggerUri = [Uri]::new($applicationUri, '_framework/debug')
$debuggerUrl = $debuggerUri.AbsoluteUri + '?url=' + [Uri]::EscapeDataString($AppUrl)
$relativePath = switch ($Browser) {
    'Thorium' { 'Thorium/Application/thorium.exe' }
    'Comet' { 'Perplexity/Comet/Application/comet.exe' }
    'Edge' { 'Microsoft/Edge/Application/msedge.exe' }
    'Chrome' { 'Google/Chrome/Application/chrome.exe' }
}
$browserExecutable = $BrowserPath
if (-not $browserExecutable) {
    $browserExecutable = @($env:ProgramFiles, ${env:ProgramFiles(x86)}, $env:LOCALAPPDATA) |
    Where-Object { $_ } |
    ForEach-Object { Join-Path $_ $relativePath } |
    Where-Object { Test-Path -LiteralPath $_ } |
    Select-Object -First 1
}
if (-not $browserExecutable -or -not (Test-Path -LiteralPath $browserExecutable -PathType Leaf)) {
    throw "$Browser was not found. Pass -BrowserPath with the browser executable's full path."
}

# A separate profile enables remote debugging without affecting normal browser sessions.
$profileDirectory = Join-Path $env:LOCALAPPDATA "MissionPlanner/BrowserDebug/$Browser"
Start-Process -FilePath $browserExecutable -ArgumentList @(
    '--remote-debugging-address=127.0.0.1',
    '--remote-debugging-port=9222',
    "--user-data-dir=`"$profileDirectory`"",
    '--no-first-run',
    "`"$AppUrl`""
)
Write-Output "Opened $Browser with local remote debugging on port 9222."
Write-Output "Keep the application tab at $AppUrl open."
Write-Output "Open a NEW tab (Ctrl+L then Alt+Enter, or Ctrl+T) and navigate to:"
Write-Output $debuggerUrl
Write-Output 'The url filter selects the application instead of the debugger itself. Do not replace the application tab with this URL.'
