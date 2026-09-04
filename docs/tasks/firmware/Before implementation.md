# Before implementation

Your screenshots confirm that the original Mission Planner has **two presentation modes for the same Install Firmware feature**:

* **Connected:** firmware installation is blocked; the page explains that the MAVLink session must be disconnected and exposes the separate **Bootloader Update** action.
* **Disconnected:** the firmware catalogue, release selection, custom firmware, board detection, and flashing actions become available.

This is deliberate. ArduPilot’s documentation states that the Install Firmware menu is visible in both states but normal firmware installation is functional only while Mission Planner is disconnected. ([ArduPilot][1])

The connected **Bootloader Update** action is a different operation. It sends `MAV_CMD_FLASH_BOOTLOADER` through the running ArduPilot firmware and does not upload a new vehicle firmware through the serial bootloader protocol. ArduPilot documents parameter 5 as the magic value `290876` and warns that interruption can make the board unresponsive. ([ArduPilot.org][2])

## Decisions to make now

### 1. Define the first supported scope

I recommend this first release:

* Windows desktop.
* Direct USB/serial flight controllers.
* Modern ArduPilot/PX4-compatible bootloader protocol.
* `.apj` and equivalent `.px4` packages.
* Stable, beta, latest, all-options, and custom firmware.
* Connected bootloader update through MAVLink.
* Automatic serial-device rediscovery after reboot.
* Manual unplug/replug fallback.

Explicitly postpone:

* AVR/Arduino legacy boards.
* VRBrain-specific upload.
* DFU flashing.
* DroneCAN node firmware.
* SD-card `.abin` updates.
* Secure/signing configuration.
* Mobile USB flashing.
* Network/BlueOS firmware upload.

The original UI’s **Install Firmware Legacy**, **Secure**, and **Force Bootloader** actions should initially appear as unsupported or remain hidden until their exact semantics are implemented.

### 2. Keep `MissionPlanner.Firmware` UI-independent

`MissionPlanner.Firmware` should be a normal `net10.0` class library with no references to:

* Avalonia UI;
* Ursa;
* CommunityToolkit popup controls;
* app pages or view models;
* platform-specific WinUI APIs.

The Avalonia firmware page belongs in the existing UI project. Windows device monitoring can either remain in the host project or later move to a separate `MissionPlanner.Firmware.Platforms.Windows` project.

### 3. Treat connected and disconnected operations separately

There are two use cases:

```text
Connected vehicle
    └── Update embedded bootloader through MAVLink

Disconnected GCS session
    └── Install ArduPilot application firmware through bootloader protocol
```

Do not try to make these one generic “flash” method. They have different protocols, safety rules, prerequisites, and results.

### 4. Preserve exclusive ownership of the serial device

Only one subsystem may own a serial device:

```text
Normal MAVLink subsystem
        OR
Firmware/bootloader subsystem
```

The firmware subsystem must refuse to start normal flashing while an active MissionPlanner vehicle connection exists.

Later, an explicit **Disconnect and Continue** workflow can coordinate the handover. For the initial implementation, matching the original Mission Planner’s manual-disconnect requirement is safer.

### 5. Decide how the board enters bootloader mode

When MissionPlanner itself is disconnected, the firmware service still needs one of these strategies:

1. The device is already running its bootloader.
2. Open the application serial port temporarily and send a minimal MAVLink reboot-to-bootloader command.
3. Ask the user to unplug and reconnect the controller.
4. Ask the user to press its reset/bootloader button.

Implement these as strategies rather than embedding all behavior in the uploader.

### 6. Licence copied code correctly

Mission Planner and ArduPilot are GPLv3. Porting the original `px4uploader` or ArduPilot `uploader.py` logic requires preserving applicable copyright notices and ensuring that your repository’s licensing remains compatible. ([ArduPilot.org][3])

### 7. Make board compatibility non-optional

An `.apj` file contains a board ID and compressed firmware image. ArduPilot generates APJ packages with `magic = APJFWv1`, `board_id`, image sizes, and Base64/zlib-compressed images. The bootloader reports the board ID used to prevent flashing firmware intended for different hardware. ([CoCalc][4])

Normal users must not be able to bypass that check accidentally. Any force option should be hidden behind an advanced warning and implemented separately.

### 8. Do not implement this as one Codex change

Use the tasks below sequentially. Each task must build and test before Codex proceeds to the next task.
