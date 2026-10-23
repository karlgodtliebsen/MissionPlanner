# Codex Task — Windows DFU and Driver Diagnostics

## Objective

Provide user-facing diagnostics that explain whether an STM32 flight controller is:

- Not in DFU mode.
- Present in DFU mode with a usable driver.
- Present with the wrong driver.
- Present with a Windows device problem.
- Busy/in use by another process.
- Ready, but STM32CubeProgrammer is missing or misconfigured.

Do not install or replace USB drivers automatically.

---

# Task 1 — Define diagnostic model

Create platform-neutral result types:

```csharp
public enum DfuEnvironmentState
{
    NoDevice,
    DevicePresentReady,
    DevicePresentWrongDriver,
    DevicePresentProblem,
    DeviceBusy,
    ProgrammerNotInstalled,
    ProgrammerUnsupported,
    Ready,
    Unknown
}

public sealed record DfuEnvironmentDiagnostic(
    DfuEnvironmentState State,
    DfuToolStatus Tool,
    IReadOnlyList<DfuDeviceDiagnostic> Devices,
    IReadOnlyList<FirmwareSupportAction> RecommendedActions,
    string Summary,
    string TechnicalDetail);
```

Use typed recommendation codes rather than embedding UI text in core services.

---

# Task 2 — Collect Windows PnP/driver evidence

For matching USB devices, capture where available:

- Friendly name.
- PnP instance ID.
- VID/PID.
- Device path.
- Device status/problem code.
- Driver service.
- Driver provider.
- Driver version/date.
- Class/class GUID.
- Container ID.
- Parent/composite relationship.

Recognize the common STM32 ROM DFU identity:

```text
VID 0483
PID DF11
Friendly name commonly STM32 BOOTLOADER
```

Do not require exact friendly-name text.

Avoid requesting administrator elevation merely to inspect device state.

---

# Task 3 — Determine readiness through layered evidence

Use layered checks:

1. Windows PnP device exists.
2. Device has no blocking problem code.
3. Expected driver service/provider is present.
4. STM32CubeProgrammer CLI can enumerate/connect to the selected USB device.

CubeProgrammer connection is the definitive provider-readiness test. Do not assume a driver is correct solely from its display name.

Map likely states with explicit uncertainty.

---

# Task 4 — Add user actions

Windows-only host actions:

```csharp
IWindowsDeviceManagerLauncher.OpenAsync()
IDfuToolInstallationLauncher.OpenOfficialDownloadAsync()
IExternalLinkLauncher.OpenAsync(Uri)
```

UI buttons:

- Refresh USB devices.
- Open Windows Device Manager.
- Open STM32CubeProgrammer.
- Download STM32CubeProgrammer.
- Browse to CLI executable.
- Copy diagnostics.
- Show driver recovery instructions.

Launch Device Manager using a safe platform service. Keep process details out of the firmware core library.

---

# Task 5 — Driver guidance policy

## Primary recommendation

STM32CubeProgrammer is the primary supported route because its installer provides the required DFU support and the tool itself can confirm USB DFU enumeration.

Embedded instructions:

1. Install/update STM32CubeProgrammer from ST.
2. During installation, include its USB/DFU driver components.
3. Put the board into DFU mode.
4. Open Device Manager.
5. Look under Universal Serial Bus devices for `STM32 BOOTLOADER` or equivalent VID/PID.
6. Refresh USB in STM32CubeProgrammer.
7. Confirm a USB port such as USB1 appears.

## Zadig fallback

Only show after the primary method fails.

Instructions must require:

- Disconnect unrelated USB devices where practical.
- Enable List All Devices only when necessary.
- Select the exact STM32 bootloader device.
- Verify VID/PID before replacement.
- Prefer the driver required by the selected provider; for libusb-based tools this is often WinUSB.

Warning:

> Replacing the driver for the wrong USB device may prevent that device from working with Windows or its normal software.

## ImpulseRC Driver Fixer

May be mentioned as an optional third-party Betaflight-community recovery tool.

Requirements:

- Label it third-party and unsupported by MissionPlanner/ST/ArduPilot.
- Verify its current official download source before embedding a link.
- Do not download or execute it from MissionPlanner.
- Do not present it as the first recommendation.

---

# Task 6 — Add context-sensitive messages

Examples:

## No device

```text
No STM32 DFU device was detected. Hold the flight controller's BOOT/DFU button or bridge its BOOT pads while connecting USB, then refresh. Check the board manual because the sequence differs by board.
```

## Device present with Windows problem

```text
Windows can see the STM32 bootloader, but the device reports a driver or enumeration problem. Open Device Manager and inspect the highlighted device.
```

## Device present but CubeProgrammer cannot connect

```text
The DFU device is visible to Windows, but STM32CubeProgrammer cannot open it. Close other flashing tools, reconnect the board and check the installed DFU driver.
```

## Tool missing

```text
STM32CubeProgrammer CLI was not found. Install it from STMicroelectronics or select the existing installation directory.
```

## More than one DFU device

Require explicit selection by provider ID/device path. Never automatically flash the first USB device.

---

# Task 7 — Add diagnostics export

Copyable report:

```text
Timestamp
OS version
MissionPlanner version
CubeProgrammer status/path/version
Detected USB DFU count
Each device friendly name
PnP instance ID
VID/PID
Driver provider/service/version
Problem code
CubeProgrammer enumeration/connect result
Recommended next action
```

Redact user-specific paths where appropriate.

---

# Task 8 — Tests

Automated tests:

- No device.
- Correct VID/PID and ready provider.
- Wrong driver.
- Windows problem code.
- Tool missing.
- Unsupported tool version.
- CLI sees no device despite PnP presence.
- Multiple devices.
- Busy provider.
- Link/action availability by platform.
- Diagnostic report formatting.

No automated test should change a real driver.

---

# Acceptance criteria

1. MissionPlanner distinguishes no device from wrong driver.
2. Device Manager action is available on Windows.
3. CubeProgrammer status is shown separately from device status.
4. The primary recommendation is ST’s bundled driver/tool.
5. Zadig and ImpulseRC are fallback guidance only.
6. MissionPlanner never installs/replaces drivers automatically.
7. Multiple DFU devices require explicit selection.
8. User can copy a complete diagnostic report.
9. Tests run without administrator privileges or physical hardware.
