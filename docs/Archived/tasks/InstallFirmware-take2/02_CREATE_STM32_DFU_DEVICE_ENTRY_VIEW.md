# Task 02 — Create STM32 DFU Device / Enter DFU View

**Repository:** `karlgodtliebsen/MissionPlanner`  
**Branch:** `feature/add-DFU-bootload`

## Goal

Create a focused `STM32 DFU -> Device / Enter DFU` view that owns the user's device-state transition into STM32 ROM DFU.

The existing `LandingView` already contains much of the UI, and `InstallFirmwareViewModel.DfuReboot.cs` already contains the safe Betaflight/MSP reboot + physical-device correlation flow. Recompose that functionality; do not replace it.

## Primary files

Existing:

- `src/UI/MissionPlanner.App/Views/InitSetup/InstallFirmware/LandingView.axaml`
- `src/UI/MissionPlanner.App/Views/InitSetup/InstallFirmware/FirmwareLandingViewModel.cs`
- `src/UI/MissionPlanner.App/Views/InitSetup/InstallFirmware/InstallFirmwareViewModel.DfuReboot.cs`
- `src/UI/MissionPlanner.App/Views/InitSetup/InstallFirmware/STM32BootloaderViewModel.cs`
- `src/UI/MissionPlanner.App/Views/InitSetup/InstallFirmware/STM32BootloaderViewModel.Discovery.cs`

Suggested new view:

- `STM32DfuDeviceView.axaml`
- `STM32DfuDeviceView.axaml.cs`

Prefer reusing existing ViewModels/properties rather than introducing parallel state.

## Required device states

### A. Betaflight controller available on COM port

Show, where available:

- COM/device selection;
- detected runtime firmware/protocol family;
- Betaflight identity summary;
- Betaflight board/target identity from MSP;
- MCU/UID information.

Expose the existing **Reboot to DFU** action.

The command must continue to:

1. force-reprobe the selected controller;
2. verify Betaflight `BTFL`;
3. require a usable MCU UID;
4. ask for explicit user confirmation;
5. use the existing reboot/bootloader-entry service;
6. accept only the newly appearing STM32 ROM DFU endpoint correlated to the same physical USB source;
7. refresh DFU discovery and select the correlated endpoint.

Do not weaken any of these checks.

### B. STM32 ROM DFU already detected

Show:

- DFU device selection;
- VID/PID where available (`0483:DF11`);
- descriptor/physical identity information already exposed by the model;
- STM32CubeProgrammer/tool readiness;
- clear state such as `Ready for STM32 DFU installation`.

Do not require a COM device when a valid DFU endpoint already exists.

### C. No usable device

Show concise fallback guidance:

- hold BOOT/DFU while reconnecting USB;
- some boards may require BOOT + RESET;
- STM32 ROM DFU is a USB endpoint, normally not a COM port;
- refresh after changing device mode.

### D. Anonymous/pre-existing DFU endpoint

If MissionPlanner did not observe a proven serial -> DFU handoff, do not imply that the exact FC board is known merely because `0483:DF11` exists.

Use wording equivalent to:

> STM32 ROM DFU device detected. No preceding controller identity is available; the exact flight-controller target cannot be inferred from STM32 DFU alone.

## Naming

Use **Device / Enter DFU**, not `Set Boot Mode`, because the endpoint may already be in DFU.

## LandingView cleanup

After the new subview exists, remove or reduce `LandingView` if it is no longer needed. Do not leave two separate screens offering the same Reboot-to-DFU operation.

## Acceptance criteria

1. A detected Betaflight COM device can be selected from `STM32 DFU -> Device / Enter DFU`.
2. Reboot-to-DFU is enabled only under the same safe prerequisites as before.
3. Successful correlated reboot selects the resulting DFU endpoint.
4. Existing physical correlation checks remain intact.
5. A pre-existing DFU endpoint can be selected without a COM port.
6. Anonymous DFU does not claim an exact ArduPilot platform.
7. No second serial scanner or DFU monitor is created.
8. The old Information/Landing workflow is not duplicated.
9. Relevant tests pass and affected projects compile.
