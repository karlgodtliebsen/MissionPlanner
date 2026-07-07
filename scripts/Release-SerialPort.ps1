<#
.SYNOPSIS
    Diagnoses and releases a stuck serial (COM) port, e.g. a USB flight-controller port.

.DESCRIPTION
    A COM port stays "in use" when a process still holds an open handle to it
    (crashed app, orphaned debugger session, another ground station, etc.).
    This script can:

      1. Check whether the port exists and whether it can currently be opened.
      2. Identify the process(es) holding the port (requires Sysinternals handle.exe).
      3. Kill the holding process(es)                      (-KillOwningProcess).
      4. Disable/enable the underlying PnP/USB device to force Windows to drop
         all handles and re-enumerate the port               (-RestartDevice, needs admin).

    With no action switches the script only diagnoses and reports.

.PARAMETER PortName
    The COM port to release, e.g. COM10. Default: COM10.

.PARAMETER KillOwningProcess
    Kill any process found holding an open handle to the port.
    Requires handle.exe (Sysinternals) on PATH or next to this script.

.PARAMETER RestartDevice
    Disable and re-enable the PnP device that provides the port.
    Requires an elevated (Administrator) PowerShell.

.EXAMPLE
    .\Release-SerialPort.ps1 COM10
    Diagnose only: is COM10 present, is it free, and who is holding it.

.EXAMPLE
    .\Release-SerialPort.ps1 COM10 -KillOwningProcess
    Kill whatever process has COM10 open.

.EXAMPLE
    .\Release-SerialPort.ps1 COM10 -RestartDevice
    Power-cycle the USB serial device (run as Administrator).

.NOTES
    handle.exe: https://learn.microsoft.com/sysinternals/downloads/handle
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Position = 0)]
    [ValidatePattern('^COM\d+$')]
    [string]$PortName = 'COM10',

    [switch]$KillOwningProcess,

    [switch]$RestartDevice
)

$ErrorActionPreference = 'Stop'

function Test-IsAdmin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    ([Security.Principal.WindowsPrincipal]$identity).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

# Resolve the NT device path behind the COM port (e.g. COM10 -> \Device\Silabser0).
# handle.exe reports handles by NT path, not by "COM10", so we need this to search.
function Get-NtDevicePath {
    param([string]$Name)
    if (-not ('Native.Kernel32' -as [type])) {
        Add-Type -Namespace Native -Name Kernel32 -MemberDefinition @'
[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
public static extern uint QueryDosDevice(string lpDeviceName, System.Text.StringBuilder lpTargetPath, int ucchMax);
'@
    }
    $sb = [System.Text.StringBuilder]::new(1024)
    if ([Native.Kernel32]::QueryDosDevice($Name, $sb, $sb.Capacity) -gt 0) { $sb.ToString() } else { $null }
}

function Test-PortIsFree {
    param([string]$Name)
    $sp = [System.IO.Ports.SerialPort]::new($Name)
    try {
        $sp.Open()
        $sp.Close()
        return $true
    }
    catch [System.UnauthorizedAccessException] {
        return $false   # held by another process
    }
    finally {
        $sp.Dispose()
    }
}

# Find processes holding a handle to the port via Sysinternals handle.exe.
function Get-PortOwningProcesses {
    param([string]$NtPath)

    $searchDirs = @($PSScriptRoot) + ($env:Path -split ';' | Where-Object { $_ })
    $handleExe = foreach ($dir in $searchDirs) {
        foreach ($exe in 'handle64.exe', 'handle.exe') {
            $candidate = Join-Path $dir $exe
            if (Test-Path $candidate) { $candidate; break }
        }
    }
    $handleExe = $handleExe | Select-Object -First 1

    if (-not $handleExe) {
        Write-Warning "handle.exe not found (PATH or script folder). Cannot identify the holding process."
        Write-Warning "Download: https://learn.microsoft.com/sysinternals/downloads/handle"
        return @()
    }

    Write-Verbose "Using $handleExe to search for handles to $NtPath"
    $output = & $handleExe -accepteula -nobanner -a $NtPath 2>$null

    # Lines look like: "MissionPlanner.exe   pid: 4242   type: File   1A4: \Device\Silabser0"
    $found = @{}
    foreach ($line in $output) {
        if ($line -match '^(?<name>.+?)\s+pid:\s*(?<pid>\d+)') {
            $found[[int]$Matches['pid']] = $Matches['name'].Trim()
        }
    }

    foreach ($processId in $found.Keys) {
        if ($processId -eq $PID) { continue }  # never report/kill ourselves
        $proc = Get-Process -Id $processId -ErrorAction SilentlyContinue
        if ($proc) { $proc }
    }
}

function Get-PortPnpDevice {
    param([string]$Name)
    # Friendly names look like "Silicon Labs CP210x USB to UART Bridge (COM10)"
    Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue |
        Where-Object { $_.FriendlyName -match "\($Name\)$" } |
        Select-Object -First 1
}

# --- 1. Does the port exist? -------------------------------------------------
$serialComm = Get-ItemProperty 'HKLM:\HARDWARE\DEVICEMAP\SERIALCOMM' -ErrorAction SilentlyContinue
$portExists = $serialComm.PSObject.Properties.Value -contains $PortName

if (-not $portExists) {
    Write-Warning "$PortName is not present on this system. Available ports: $([System.IO.Ports.SerialPort]::GetPortNames() -join ', ')"
    if (-not $RestartDevice) {
        Write-Host "If the device is plugged in but missing, unplug/replug it or check Device Manager." -ForegroundColor Yellow
        exit 1
    }
}

$device = Get-PortPnpDevice -Name $PortName
if ($device) {
    Write-Host "Device : $($device.FriendlyName)" -ForegroundColor Cyan
    Write-Host "Id     : $($device.InstanceId)" -ForegroundColor Cyan
    Write-Host "Status : $($device.Status)" -ForegroundColor Cyan
}

# --- 2. Is the port free? ----------------------------------------------------
$isFree = $false
if ($portExists) {
    try {
        $isFree = Test-PortIsFree -Name $PortName
    }
    catch {
        Write-Warning "Opening $PortName failed: $($_.Exception.Message)"
    }

    if ($isFree) {
        Write-Host "$PortName is FREE - it can be opened normally. Nothing to release." -ForegroundColor Green
        if (-not $RestartDevice) { exit 0 }
    }
    else {
        Write-Host "$PortName is IN USE by another process." -ForegroundColor Yellow
    }
}

# --- 3. Who is holding it? ---------------------------------------------------
$owners = @()
if ($portExists -and -not $isFree) {
    $ntPath = Get-NtDevicePath -Name $PortName
    if ($ntPath) {
        Write-Host "NT path: $ntPath" -ForegroundColor Cyan
        $owners = @(Get-PortOwningProcesses -NtPath $ntPath)
    }

    if ($owners.Count -gt 0) {
        Write-Host "`nProcesses holding ${PortName}:" -ForegroundColor Yellow
        $owners | Format-Table Id, ProcessName, Path -AutoSize | Out-String | Write-Host
    }
    elseif ($ntPath) {
        Write-Host "No user-mode holder found. The handle may be held by a service or the driver itself; -RestartDevice usually clears this." -ForegroundColor Yellow
    }
}

# --- 4. Kill the holders (optional) -------------------------------------------
if ($KillOwningProcess -and $owners.Count -gt 0) {
    foreach ($proc in $owners) {
        if ($PSCmdlet.ShouldProcess("$($proc.ProcessName) (PID $($proc.Id))", "Stop process holding $PortName")) {
            Stop-Process -Id $proc.Id -Force -Confirm:$false
            Write-Host "Killed $($proc.ProcessName) (PID $($proc.Id))." -ForegroundColor Green
        }
    }
    Start-Sleep -Milliseconds 500
}

# --- 5. Restart the PnP device (optional) -------------------------------------
if ($RestartDevice) {
    if (-not (Test-IsAdmin)) {
        Write-Error "-RestartDevice requires an elevated PowerShell. Re-run as Administrator."
        exit 1
    }
    if (-not $device) {
        Write-Error "Could not find a present PnP device for $PortName - cannot restart it."
        exit 1
    }
    if ($PSCmdlet.ShouldProcess($device.FriendlyName, 'Disable and re-enable device')) {
        Write-Host "Disabling $($device.FriendlyName)..." -ForegroundColor Yellow
        Disable-PnpDevice -InstanceId $device.InstanceId -Confirm:$false
        Start-Sleep -Seconds 2
        Write-Host "Enabling $($device.FriendlyName)..." -ForegroundColor Yellow
        Enable-PnpDevice -InstanceId $device.InstanceId -Confirm:$false
        Start-Sleep -Seconds 2
    }
}

# --- 6. Final check ------------------------------------------------------------
if ($KillOwningProcess -or $RestartDevice) {
    try {
        if (Test-PortIsFree -Name $PortName) {
            Write-Host "`n$PortName is now FREE." -ForegroundColor Green
            exit 0
        }
        Write-Warning "$PortName is still in use. Try -RestartDevice (elevated), or physically replug the USB device."
        exit 1
    }
    catch {
        Write-Warning "Could not verify ${PortName}: $($_.Exception.Message)"
        exit 1
    }
}
