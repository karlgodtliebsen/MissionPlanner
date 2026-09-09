# MissionPlanner Next Generation — Betaflight/MSP/DFU Codex Task Set

## Goal

Implement first-class Betaflight flight-controller discovery over MSP, show useful board/firmware/MCU identity, allow a proven Betaflight FC to reboot into the STM32 factory ROM DFU bootloader, and then reuse MissionPlanner's existing DFU and ArduPilot firmware infrastructure to convert supported boards to ArduPilot.

Target operator flow:

```text
USB serial device
  -> MSP probe
  -> Betaflight (`BTFL`) identified
  -> detailed board/firmware/MCU identity
  -> Reboot to STM32 ROM DFU
  -> correlate the same physical DFU device
  -> validate/select compatible ArduPilot target
  -> existing MissionPlanner DFU installer
  -> reconnect
  -> verify ArduPilot
```

## Architectural constraint

MissionPlanner already has substantial firmware functionality under `src/Core/MissionPlanner.Firmware`.

At task-set creation time, current `main` includes/references:

```text
src/Core/MissionPlanner.Firmware/
    Configuration/FirmwareConfigurator.cs
    FirmwareFamily.cs
    Entry/
    Discovery/
    Devices/
    Connected/
    Dfu/
    Installation/
    Recovery/
    Safety/
    Tooling/
    Transport/
```

The current DI configuration already registers firmware-device discovery, device matching, bootloader-entry strategies, STM32 DFU enumeration/monitoring, DFU transport backends, DFU programming, ArduPilot firmware catalog/selection, installation, and recovery services.

**Do not implement a second DFU stack.**

The missing bridge is primarily MSP + Betaflight identity + Betaflight bootloader entry + safe orchestration into the existing ArduPilot installer.

## Current firmware UI anchors

Verify against `main` before editing. Current documentation in:

```text
docs/INSTALL_FIRMWARE_VIEWMODELS.md
```

describes these anchors:

```text
src/UI/MissionPlanner.App/Features/Firmware/FirmwareShellViewModel
src/UI/MissionPlanner.App/Features/Firmware/FirmwareLandingViewModel
src/UI/MissionPlanner.App/Features/Firmware/FirmwareDetailsViewModel
src/UI/MissionPlanner.App/Features/Firmware/FirmwareProgressViewModel
```

If current paths differ when Codex executes a task, adapt to the current architecture. Do not recreate obsolete folders.

## Scope

### In scope

- minimal robust MSP framing/client;
- `BTFL` Betaflight proof;
- firmware/API/build identity;
- board/target/manufacturer identity;
- MCU information and UID where supported;
- integration with current serial/firmware discovery;
- MSP reboot into STM32 ROM DFU;
- serial-to-DFU physical-device correlation;
- Betaflight-to-ArduPilot conversion orchestration;
- compatibility validation;
- reuse of current ArduPilot catalog and DFU installer;
- firmware UI integration;
- automated and physical acceptance tests;
- documentation.

### Explicitly out of scope

- full Betaflight Configurator functionality;
- PID/rates/modes configuration;
- Betaflight CLI emulation;
- OSD configuration;
- MSP DisplayPort;
- DJI Air Unit/VTX configuration;
- Betaflight firmware catalog/download/installation.

Betaflight firmware installation is captured only as deferred work in Task 09.

## Safety invariants

The implementation must never:

- classify a device as Betaflight merely because Windows calls it `STM Device`;
- infer ArduPilot compatibility solely from `STM32F4`, `F405`, etc.;
- map every F4 board to `omnibusf4`;
- flash the first DFU device that happens to be present;
- continue after ambiguous device correlation;
- silently replace an operator-selected physical device with another device;
- claim conversion success without post-flash ArduPilot verification.

## Hardware acceptance context

### Three 5-inch drones

The operator has three sets of 5-inch drones being converted from Betaflight to ArduPilot. For the compatible FCs currently being tested, the ArduPilot firmware used is `omnibusf4`.

These provide an excellent complete-conversion test bed for:

- Parameters;
- Radio;
- Motors;
- external buzzer;
- external GPS.

`omnibusf4` is a known hardware test case, **not a universal mapping**.

### BetaFPV Pavo 20

A BetaFPV Pavo 20 running Betaflight with a DJI Air Unit is available for:

- Betaflight/MSP identity validation;
- board/MCU parsing;
- software reboot to ROM DFU;
- DFU correlation;
- later OSD testing.

Do not flash ArduPilot to the Pavo 20 unless its exact board identity is independently proven compatible with an ArduPilot target.

## Task order

Execute one at a time:

1. `01_MINIMAL_MSP_TRANSPORT_AND_PROTOCOL.md`
2. `02_BETAFLIGHT_DEVICE_IDENTITY_PROBE.md`
3. `03_SERIAL_DISCOVERY_AND_CONNECTED_IDENTITY_INTEGRATION.md`
4. `04_BETAFLIGHT_MSP_REBOOT_TO_DFU.md`
5. `05_DFU_HANDOFF_AND_DEVICE_CORRELATION.md`
6. `06_BETAFLIGHT_TO_ARDUPILOT_CONVERSION_WORKFLOW.md`
7. `07_FIRMWARE_UI_AND_OPERATOR_SAFETY.md`
8. `08_TESTS_HARDWARE_ACCEPTANCE_AND_DOCS.md`

`09_DEFERRED_BETAFLIGHT_FIRMWARE_FLASHING.md` is a future design note and is not part of initial implementation.

## Definition of complete

A supported physical Betaflight FC can complete:

```text
Betaflight serial identified by MSP
-> identity displayed
-> software reboot to STM32 ROM DFU
-> same physical DFU device correlated
-> compatible ArduPilot target validated
-> existing MissionPlanner firmware/DFU path used
-> FC returns on USB/serial
-> ArduPilot identity verified
```

Unsupported or ambiguous boards stop before destructive programming.

---
## Codex execution rules

1. Work from the current `main` branch and locate every referenced symbol before editing. MissionPlanner is evolving quickly; do not rely on stale paths or duplicate an abstraction that already exists.
2. Keep this task cohesive and limited to its stated scope.
3. Prefer typed protocol/domain models over unstructured metadata.
4. Reuse current MissionPlanner firmware, DFU, device-matching, installation, progress, and recovery abstractions wherever they already solve the problem.
5. Add or update automated tests for every behavioral change.
6. Run the affected project tests plus the relevant `MissionPlanner.Firmware` tests. Run a broader build/test when practical.
7. Fail closed on uncertain identity, compatibility, or physical-device correlation.
8. In the completion report include:
   - files changed;
   - architectural decisions;
   - tests run and results;
   - hardware/manual validation still required;
   - deviations from this task and why.
