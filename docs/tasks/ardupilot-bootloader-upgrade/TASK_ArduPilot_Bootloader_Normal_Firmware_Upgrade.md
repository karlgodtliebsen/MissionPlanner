# TASK — Support Normal ArduPilot Firmware Upgrade Through the ArduPilot Bootloader

## Goal

Change MissionPlanner Next Gen firmware installation so an already-running ArduPilot vehicle can perform a normal firmware upgrade through its installed **ArduPilot bootloader**, without requiring the user to manually enter STM32 ROM DFU mode.

STM32 DFU should remain available for:

- first-time ArduPilot installation;
- bootloader repair/replacement;
- recovery when the ArduPilot bootloader is missing or unusable.

This task must preserve the firmware identity/recovery architecture already being implemented.

---

## Real test case

There are currently **three physical OmnibusF4 drones** available for testing.

All three:

- run ArduPilot;
- are connectable normally by MissionPlanner Next Gen;
- have an older stable ArduPilot firmware, e.g. 4.7.0;
- are correctly detected by the new background firmware-version check as having 4.7.1 Stable available;
- currently get routed by Install Firmware into an STM32 DFU requirement;
- behave similarly when manually put into STM32 DFU;
- should instead be good candidates for normal ArduPilot bootloader firmware upload.

Use all three drones as repeatability/regression test hardware after the implementation is complete.

Props must remain removed during all firmware and motor-related bench testing.

---

## Current problematic workflow

Today the application behaves approximately like:

```text
Connected ArduPilot vehicle
        │
        ▼
New stable firmware available
        │
        ▼
Install Firmware
        │
        ▼
Require STM32 DFU mode
```

That is not the preferred path for a normal ArduPilot-to-ArduPilot upgrade.

---

## Required normal upgrade workflow

For an already-running compatible ArduPilot vehicle, implement:

```text
Connected ArduPilot vehicle
        │
        ▼
Select compatible .apj firmware
        │
        ▼
Validate firmware identity/compatibility
        │
        ▼
Request reboot to ArduPilot bootloader
        │
        ▼
Transport temporarily disappears/re-enumerates
        │
        ▼
Discover ArduPilot bootloader endpoint
        │
        ▼
Connect with ArduPilot/PX4-compatible uploader protocol
        │
        ▼
Erase / Program / Verify
        │
        ▼
Reboot into application firmware
        │
        ▼
Reconnect
        │
        ▼
Verify installed firmware identity/version
```

The user should not normally need to hold a BOOT button or manually enter STM32 ROM DFU for a routine ArduPilot firmware upgrade.

---

## Important distinction

Model these as different installation mechanisms:

```text
ArduPilotBootloader
Stm32Dfu
```

They are not aliases.

### ArduPilot bootloader

Used for:

- normal ArduPilot firmware upgrades;
- `.apj` upload;
- compatible ArduPilot/PX4 bootloader protocol;
- existing installed bootloader.

### STM32 ROM DFU

Used for:

- initial installation where no compatible bootloader exists;
- bootloader recovery;
- deeply broken firmware/bootloader situations;
- explicit recovery workflow.

Do not route every STM32F4 firmware installation through DFU.

---

## Inspect and reuse existing implementation

Before adding new code, inspect the current solution for:

- firmware install orchestration;
- bootloader/uploader services;
- PX4/ArduPilot uploader implementation;
- reboot command support;
- `MAV_CMD_PREFLIGHT_REBOOT_SHUTDOWN`;
- serial-device discovery/re-enumeration;
- STM32 DFU services;
- firmware compatibility evaluation;
- firmware identity models;
- Install Firmware ViewModel;
- progress/status reporting;
- connection-monitor services.

Also inspect the original MissionPlanner source in the repository for the equivalent ArduPilot/PX4 bootloader upload workflow.

Do not introduce a second uploader implementation if a suitable one already exists.

---

## Installation strategy selection

Introduce an explicit installation strategy decision.

Suggested model:

```csharp
public enum FirmwareInstallTransport
{
    ArduPilotBootloader,
    Stm32Dfu
}
```

or equivalent existing terminology.

Add a strategy resolver similar in responsibility to:

```csharp
public interface IFirmwareInstallStrategyResolver
{
    FirmwareInstallStrategy Resolve(
        ControllerFirmwareIdentity controller,
        SelectedFirmwareIdentity firmware,
        FirmwareInstallMode mode);
}
```

For a normal connected ArduPilot vehicle with a valid compatible `.apj`, prefer:

```text
ArduPilotBootloader
```

Do not prefer DFU simply because the MCU is STM32.

---

## Reboot-to-bootloader

For normal upgrade, use the existing MAVLink reboot command infrastructure.

The workflow should:

1. confirm the target vehicle is the intended controller;
2. stop/cancel active operations tied to the live connection;
3. flush/close telemetry recording appropriately;
4. request reboot to bootloader;
5. intentionally tolerate the serial device disappearing;
6. transition connection state into an expected bootloader/reboot state rather than reporting an unexpected connection failure;
7. discover the re-enumerated bootloader device.

Do not depend on a fixed COM port remaining unchanged.

---

## Device rediscovery

The bootloader may:

- reappear on the same COM port;
- reappear on a different COM port;
- temporarily disappear for a short interval.

Reuse the existing device-identification work.

Match the returning device using the strongest available evidence:

```text
USB VID/PID
hardware UID/serial
bootloader identity
board id
known transition from the selected controller
```

Do not simply pick the first new serial port.

Use a bounded timeout and cancellation.

---

## Bootloader identity

Once connected to the ArduPilot bootloader, retrieve whatever identity the uploader protocol exposes.

Where possible collect:

```text
board id
board revision
bootloader revision
flash size
device identity
```

Pass this into the firmware compatibility/recovery architecture.

For normal upgrade, the bootloader identity should agree with the selected `.apj`.

If it does not, stop before erase/program.

---

## Upload

Use the existing ArduPilot/PX4 uploader protocol.

The user-facing progress should expose meaningful stages:

```text
Rebooting controller to bootloader...
Waiting for bootloader...
Bootloader detected on COMx.
Bootloader board ID: ...
Erasing firmware...
Programming...
Verifying...
Rebooting controller...
Waiting for ArduPilot...
Firmware upgrade complete.
```

Expose percentage where the uploader supports it.

Cancellation should be honored before destructive stages where safe.

Do not pretend cancellation is safe during an erase/program operation if the underlying protocol cannot guarantee it.

---

## Post-flash reconnect and verification

After successful upload:

1. request/allow reboot from bootloader;
2. rediscover the normal ArduPilot application connection;
3. reconnect;
4. wait for heartbeat;
5. obtain `AUTOPILOT_VERSION`;
6. verify:
   - firmware family;
   - target/board identity where available;
   - installed semantic version.

Example expected result:

```text
Upgrade complete

Target:  omnibusf4
Version: ArduCopter 4.7.1 Official
```

Do not mark success solely because the uploader finished writing.

---

## Fallback to STM32 DFU

If the ArduPilot bootloader cannot be entered or discovered, do not immediately hide the reason and demand DFU.

Show a structured fallback such as:

```text
ArduPilot bootloader could not be reached.

Possible causes:
- bootloader missing;
- bootloader damaged;
- USB re-enumeration failed;
- controller did not enter bootloader.

Recovery option:
[Use STM32 DFU / Repair Bootloader]
```

DFU must be an explicit fallback/recovery path.

---

## Interaction with firmware identity recovery work

Integrate with the new identity model:

```text
PhysicalDeviceIdentity
BootloaderIdentity
RunningFirmwareIdentity
SelectedFirmwareIdentity
```

Normal upgrade:

```text
running firmware compatible
+
bootloader compatible
+
selected firmware compatible
```

Recovery mode may still use STM32 DFU when the running target or bootloader is wrong.

Do not merge normal ArduPilot bootloader upgrade with wrong-target recovery into one generic path.

---

## Connection-monitor integration

The normal connection monitor must distinguish:

```text
unexpected connection loss
```

from:

```text
expected disconnect because firmware workflow requested reboot to bootloader
```

Introduce/use an explicit transition/reason such as:

```text
FirmwareUpgradeReboot
BootloaderTransition
```

Pending application-level operations should be cancelled cleanly before reboot.

Avoid warning/toast storms while waiting for the bootloader.

---

## Telemetry/application logging

Add structured application logging for:

```text
FirmwareInstallStrategy
VehicleId
OriginalPort
DetectedBootloaderPort
RunningFirmwareIdentity
BootloaderIdentity
SelectedFirmwareIdentity
RebootCommandResult
BootloaderDiscoveryDuration
UploadResult
VerifyResult
ReconnectResult
PostFlashFirmwareIdentity
```

The new Live Telemetry Inspector / diagnostic journal may record high-level state transitions, but do not flood it with every uploader packet.

---

## UI changes

### Before install

For a normal ArduPilot upgrade show something like:

```text
Install method
  ArduPilot Bootloader

Current
  ArduCopter 4.7.0

Selected
  ArduCopter 4.7.1 Stable
```

Do not instruct the user to enter STM32 DFU.

### Recovery case

Only show DFU-specific instructions when the selected strategy is actually:

```text
STM32 DFU / recovery
```

---

## Automated tests

Add tests using fakes for:

### Strategy selection

```text
Connected ArduPilot + compatible APJ + bootloader expected
-> ArduPilotBootloader
```

```text
Betaflight/no ArduPilot bootloader
-> Stm32Dfu/recovery path
```

```text
Wrong firmware target recovery
-> use recovery strategy rules
```

### Re-enumeration

- same COM port returns;
- different COM port returns;
- unrelated COM port appears;
- timeout;
- cancellation.

### Bootloader compatibility

- matching board id -> continue;
- mismatch -> stop before erase;
- missing identity -> follow explicit policy.

### Upload stages

- erase failure;
- program failure;
- verify failure;
- reboot failure.

### Post-flash verification

```text
4.7.0 -> upload 4.7.1 -> reconnect reports 4.7.1
-> success
```

Wrong post-flash version/target must not be reported as success.

---

## Physical acceptance test — three OmnibusF4 drones

Run the same workflow against all three available OmnibusF4 drones.

For each drone record:

```text
Drone identifier
Original COM port
Original firmware version
Selected firmware version
Install strategy
Bootloader port
Bootloader board id
Upload result
Reconnect port
Post-flash firmware version
```

Expected for all three:

```text
Original firmware: 4.7.0
Available stable:  4.7.1
Install strategy:  ArduPilot Bootloader
Manual DFU:        Not required
Upload:            Successful
Reconnect:         Successful
Installed:         4.7.1
```

If one board behaves differently, do not hide the difference; capture its device/bootloader identity and diagnose it separately.

---

## Acceptance criteria

- A normal ArduPilot firmware upgrade no longer requires manual STM32 DFU.
- ArduPilot bootloader is the preferred path for compatible running ArduPilot vehicles.
- Serial/USB re-enumeration is handled automatically.
- Bootloader identity is checked before destructive upload.
- `.apj` is uploaded using the existing ArduPilot/PX4 bootloader protocol.
- Normal application reconnect occurs automatically after flash.
- Installed firmware identity/version is verified after reconnect.
- STM32 DFU remains available as explicit recovery/fallback.
- Expected bootloader transitions do not trigger false connection-loss warnings.
- All automated tests pass.
- The workflow is successfully exercised against the three OmnibusF4 test drones.
