# Codex Task — Improve Install Firmware Bootloader Reconnect Flow

## Objective

Improve the **Install Firmware / Custom Firmware (.apj)** flashing flow so that MissionPlanner Next Gen does **not immediately require the user to unplug/reconnect the flight controller** when the device is already available through a serial port such as `COM11`.

Manual reset or USB reconnect must become a **fallback** used only when MissionPlanner cannot automatically reboot the running ArduPilot firmware into the **ArduPilot bootloader**.

This task is specifically about the normal ArduPilot `.apj` flashing path. It must **not** conflate the ArduPilot bootloader with STM32 DFU mode.

---

## Repository

Work from the latest `main` branch of the MissionPlanner Next Gen repository.

Before making changes:

1. Inspect the current Install Firmware implementation.
2. Locate the code handling:
   - Custom `.apj` firmware loading.
   - Serial-device discovery.
   - ArduPilot bootloader detection.
   - Firmware upload orchestration.
   - `FirmwareInteractionCodes.ManualBootloaderReconnect`.
3. Preserve the current architecture and service abstractions.
4. Do not port legacy WinForms implementation patterns into the Next Gen UI.

---

## Current behavior

A flight controller is connected through USB and appears as a serial device, for example:

```text
COM11
```

No normal MissionPlanner vehicle connection has been established.

The user chooses **Custom Firmware** and loads an `.apj` file.

MissionPlanner currently reaches:

```csharp
FirmwareInteractionCodes.ManualBootloaderReconnect =>
    "Click Continue, then immediately unplug and reconnect the flight controller or press its hardware reset button. Mission Planner will watch for the ArduPilot bootloader.",
```

This makes manual reset/reconnection effectively part of the normal flashing flow.

That should be changed.

---

## Required behavior

When MissionPlanner already knows which serial device represents the flight controller, the expected flow is:

```text
Running ArduPilot firmware
        |
        | temporary serial access
        v
Attempt MAVLink reboot-to-bootloader
        |
        v
Watch USB/serial enumeration
        |
        +--> ArduPilot bootloader detected
        |        |
        |        v
        |   Validate APJ board ID
        |        |
        |        v
        |   Erase / upload / verify
        |
        +--> Automatic reboot failed
                 |
                 v
          Manual reset/reconnect prompt
                 |
                 v
          Continue watching for
          ArduPilot bootloader
```

A normal MissionPlanner `VehicleConnection` must **not** be required merely to send the reboot request.

The firmware subsystem may temporarily open/use the selected serial device itself, following the architecture already used by the Install Firmware subsystem.

---

## 1. Add automatic reboot-to-ArduPilot-bootloader

When all of the following are true:

- A flight controller is present as a serial device.
- MissionPlanner knows the relevant serial port/device.
- The board is not already detected in ArduPilot bootloader mode.
- The selected firmware is an `.apj`.

MissionPlanner must first attempt to reboot the currently running ArduPilot firmware into its bootloader automatically.

Use the standard MAVLink reboot command:

```text
MAV_CMD_PREFLIGHT_REBOOT_SHUTDOWN
```

with the appropriate ArduPilot reboot-to-bootloader semantics.

Do not require the global MissionPlanner vehicle connection to be active.

Reuse existing MAVLink serialization, transport, serial, device-discovery, and firmware abstractions wherever possible.

Do not introduce a second competing MAVLink implementation.

---

## 2. Serial-port ownership

The firmware uploader must safely obtain temporary ownership of the selected serial device.

The implementation must:

- Detect whether the port is already in use by MissionPlanner.
- Avoid opening the same serial port concurrently from two independent owners.
- Correctly dispose/close temporary serial access before waiting for USB re-enumeration.
- Handle the expected disappearance of the application COM port during reboot.
- Handle the possibility that the bootloader enumerates under a different COM port.
- Continue matching the physical device/board rather than assuming that `COM11` remains `COM11`.

Reuse the project's existing device identity/discovery abstractions where available.

Do not solve this by inserting arbitrary fixed delays.

---

## 3. Detect an already-running bootloader

Before requesting a MAVLink reboot, check whether the selected device is already exposing the ArduPilot bootloader.

If it is already in bootloader mode:

- Skip the MAVLink reboot attempt.
- Continue directly with board identification and firmware upload.

---

## 4. Manual reconnect becomes fallback only

`FirmwareInteractionCodes.ManualBootloaderReconnect` must only be raised after the automatic reboot attempt fails to produce a detectable ArduPilot bootloader within the normal bounded detection/retry policy.

Change its user-facing text to make that clear.

Use wording equivalent to:

```csharp
FirmwareInteractionCodes.ManualBootloaderReconnect =>
    "The flight controller did not enter the ArduPilot bootloader automatically. " +
    "Click Continue, then press the flight controller's RESET button or unplug and reconnect its USB cable. " +
    "Mission Planner will watch for the ArduPilot bootloader.";
```

Keep wording centralized in the existing interaction/message mechanism.

Do not hard-code UI strings into the firmware protocol/service layer.

---

## 5. Keep ArduPilot bootloader and STM32 DFU separate

Do not treat manual USB reconnection as evidence that the device is entering DFU mode.

Normal custom `.apj` flashing is:

```text
ArduPilot application
    -> ArduPilot bootloader
    -> APJ upload
```

STM32 DFU is a separate recovery/provisioning path, typically involving BOOT/BOOT0 and reset.

The implementation must preserve this distinction in:

- State names.
- Interaction codes.
- Logging.
- Device detection.
- Error messages.

Do not introduce DFU terminology into the normal `.apj` reboot/reconnect interaction.

---

## 6. Firmware flashing state machine

Review the current firmware installation orchestration and make the transition explicit enough that the following states can be distinguished:

```text
DeviceSelected
FirmwareLoaded
CheckingForBootloader
RequestingBootloaderReboot
WaitingForBootloader
ManualBootloaderReconnectRequired
BootloaderDetected
ValidatingBoard
Uploading
Verifying
Completed
Failed
Cancelled
```

The exact enum/type names may follow the existing codebase conventions.

Do not create a second state machine if the current implementation already has an equivalent representation; extend/refine it instead.

---

## 7. Cancellation and timeout behavior

All waiting must remain bounded and cancellable.

Required:

- CancellationToken propagated through reboot and detection operations.
- No `.Wait()`, `.Result`, or synchronous blocking of asynchronous operations.
- No unobserved fire-and-forget tasks.
- Automatic reboot timeout must transition to the manual-reconnect interaction rather than immediately failing the installation.
- Manual reconnect waiting must still honor cancellation and the existing timeout/retry policy.

---

## 8. Browser/WASM behavior

MissionPlanner Next Gen now supports Browser/WASM.

This feature is inherently dependent on available device/transport capabilities.

Do not introduce desktop-only APIs directly into shared ViewModels or shared UI code.

The implementation must use the existing platform/device capability abstractions.

For platforms where direct serial firmware flashing is unavailable:

- The feature must compile.
- The unsupported operation must be disabled or rejected through the existing capability mechanism.
- The UI must provide the existing clear unsupported-platform feedback.
- Browser/WASM must not fail because of references to desktop-only serial/device APIs.

---

## 9. Logging

Add useful structured/debug logging around the sequence.

At minimum log:

```text
Selected firmware device
Current application serial endpoint
Checking for existing ArduPilot bootloader
Attempting MAVLink reboot to bootloader
Reboot command sent
Application serial device disappeared
Waiting for bootloader enumeration
Bootloader candidate discovered
Bootloader matched to selected board/device
Automatic bootloader entry timed out
Requesting manual reset/reconnect
ArduPilot bootloader detected
Starting firmware upload
```

Do not log excessively inside tight polling loops.

---

## 10. Failure handling

Handle at least these cases explicitly:

1. Port disappears while sending reboot command.
2. MAVLink reboot command receives no response.
3. Device resets before an ACK is observed.
4. Bootloader appears on a different COM port.
5. Unrelated USB serial devices appear during detection.
6. Device is already in ArduPilot bootloader mode.
7. Automatic reboot fails but manual reconnect succeeds.
8. Both automatic and manual bootloader detection fail.
9. User cancels while waiting.
10. Selected `.apj` board ID does not match the detected bootloader board.

A lost ACK is **not necessarily a reboot failure** if the device disappears and the expected bootloader subsequently appears.

---

# Tests

Add or update automated tests around the firmware orchestration.

Use mocks/fakes for serial-device enumeration, MAVLink transport, clock/timing, interactions, and bootloader discovery where the project architecture allows it.

## Test 1 — automatic reboot succeeds

Given:

```text
COM11 contains running ArduPilot firmware
```

When a custom `.apj` is selected:

- MissionPlanner sends the MAVLink reboot-to-bootloader command.
- Application serial device disappears.
- ArduPilot bootloader appears.
- Firmware flashing continues.
- `ManualBootloaderReconnect` is never raised.

---

## Test 2 — bootloader already active

Given the selected device is already an ArduPilot bootloader:

- No MAVLink reboot command is sent.
- No manual reconnect interaction occurs.
- Upload proceeds directly.

---

## Test 3 — reboot ACK missing but bootloader appears

Simulate:

- MAVLink reboot command sent.
- No command ACK received.
- COM11 disappears.
- ArduPilot bootloader appears.

Expected:

- Operation is considered successful.
- Manual reconnect is not shown.
- Upload proceeds.

---

## Test 4 — automatic reboot fails

Simulate:

- Running ArduPilot detected.
- Reboot command attempted.
- No matching ArduPilot bootloader appears before timeout.

Expected:

```text
FirmwareInteractionCodes.ManualBootloaderReconnect
```

is raised.

The operation must not immediately terminate as failed.

---

## Test 5 — manual reconnect succeeds

After the manual reconnect interaction:

- User resets/reconnects FC.
- Matching ArduPilot bootloader appears.
- Firmware upload proceeds normally.

---

## Test 6 — COM port changes

Simulate:

```text
Application: COM11
Bootloader:  COM14
```

Expected:

- The bootloader is matched to the same physical FC.
- Upload proceeds on COM14.
- Code does not assume bootloader remains on COM11.

---

## Test 7 — unrelated serial device appears

While waiting for the target board bootloader, another USB serial device appears.

Expected:

- It is ignored.
- MissionPlanner continues waiting for the target board.

---

## Test 8 — cancellation

Cancel while:

```text
WaitingForBootloader
```

Expected:

- Operation terminates promptly.
- Serial resources are released.
- No further interaction prompts appear.
- No background polling remains active.

---

## Test 9 — board mismatch

Bootloader is detected but its board identity is incompatible with the selected `.apj`.

Expected:

- Upload is refused.
- Existing board-mismatch safety handling is preserved.
- No erase/program operation starts.

---

## Test 10 — Browser/WASM build

The Browser/WASM target must build after the changes.

No unsupported desktop serial API may leak into the Browser/WASM shared code path.

---

# Acceptance criteria

The task is complete when all of the following are true:

- [ ] A connected FC does not require an established MissionPlanner Vehicle Connection in order to attempt reboot-to-bootloader.
- [ ] A custom `.apj` flash first attempts automatic ArduPilot bootloader entry.
- [ ] Manual reset/unplug/reconnect is a fallback rather than the default path.
- [ ] An already-active ArduPilot bootloader is recognized without attempting MAVLink reboot.
- [ ] A reboot can succeed even if no MAVLink ACK is received, provided the expected bootloader subsequently appears.
- [ ] COM-port renumbering during bootloader entry is supported.
- [ ] Unrelated USB serial devices are not mistaken for the target FC.
- [ ] ArduPilot bootloader and STM32 DFU remain clearly separate concepts.
- [ ] APJ board-ID validation remains in place before erase/program.
- [ ] All serial resources are disposed correctly.
- [ ] All waits are bounded and cancellable.
- [ ] No blocking async calls are introduced.
- [ ] Browser/WASM continues to compile and handles unsupported serial flashing through capability gating.
- [ ] Automated tests cover automatic success, fallback, changed COM port, cancellation, and board mismatch.
- [ ] Existing Install Firmware tests continue to pass.
- [ ] Existing working firmware-install behavior is not regressed.

---

# Scope constraints

Do **not**:

- Implement a new STM32 DFU flashing subsystem as part of this task.
- Require a normal MissionPlanner Vehicle Connection before custom firmware flashing.
- Replace existing firmware/device-discovery abstractions without a strong architectural reason.
- Use fixed sleeps as the primary device-detection mechanism.
- Assume the bootloader retains the same COM port.
- Bypass APJ board compatibility checks.
- Add WinForms-era implementation dependencies.
- Introduce direct platform-specific serial APIs into shared Browser/WASM code.

---

# Completion report

When finished, provide:

1. Files changed.
2. Short description of the revised firmware state flow.
3. How the temporary MAVLink reboot is performed without a Vehicle Connection.
4. How physical-device identity is retained across COM-port re-enumeration.
5. How automatic failure transitions to `ManualBootloaderReconnect`.
6. Tests added/changed and their results.
7. Desktop build result.
8. Browser/WASM build result.
9. Any remaining limitations or hardware-specific behavior observed.
