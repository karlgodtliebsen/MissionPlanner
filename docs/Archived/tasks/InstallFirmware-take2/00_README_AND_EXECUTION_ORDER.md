# MissionPlanner DFU/Firmware UI Restructure — Codex Task Set

**Repository:** `karlgodtliebsen/MissionPlanner`  
**Branch:** `feature/add-DFU-bootload`

These tasks restructure `InstallFirmwarePage` so the UI reflects the actual installation mechanisms:

```text
Install Firmware
├── Firmware
│   ├── Catalogue
│   └── Custom Firmware
├── STM32 DFU
│   ├── Device / Enter DFU
│   ├── Catalogue
│   └── Custom HEX
└── Help & Support
```

The intent is to separate:

- normal ArduPilot firmware installation through an ArduPilot serial bootloader using `.apj`/`.px4`;
- initial/conversion installation through the STM32 ROM DFU endpoint using a combined `*_with_bl.hex`;
- firmware source selection (catalogue vs custom) from installation mechanism (serial bootloader vs STM32 DFU).

## Execute in this order

1. `01_RESTRUCTURE_INSTALL_FIRMWARE_NAVIGATION.md`
2. `02_CREATE_STM32_DFU_DEVICE_ENTRY_VIEW.md`
3. `03_CREATE_STM32_DFU_CATALOGUE_WORKFLOW.md`
4. `04_CREATE_STM32_DFU_CUSTOM_HEX_WORKFLOW.md`
5. `05_ADD_CONTEXT_NAVIGATION_AND_FIX_ARTIFACT_SEMANTICS.md`
6. `06_TESTS_REGRESSION_AND_DOCUMENTATION.md`

Each task should leave the branch buildable. Do not combine all tasks into one large rewrite.

## Rules for every task

- Work only on `feature/add-DFU-bootload`.
- Read `ai.md`, `docs/FIRMWARE.md`, `docs/INSTALL_FIRMWARE_VIEWMODELS.md`, and `docs/BETAFLIGHT_DFU_AND_ARDUPILOT_CONVERSION.md` before changing code.
- Reuse the existing firmware catalogue, serial discovery, Betaflight/MSP identity, DFU discovery/correlation, `BootloaderEntryService`, `IDfuInstallationService`, and STM32CubeProgrammer integration.
- Do not create a second DFU implementation, a second device scanner, or a second firmware catalogue.
- Betaflight firmware installation remains out of scope.
- Preserve safe failure behavior: an anonymous STM32 ROM DFU device does not prove an exact flight-controller PCB.
- Keep Windows/Desktop working and do not break Browser/WASM compilation.
- Keep presentation state in the UI/application layer.
- Run relevant tests and build the affected projects after each task.
- Do not weaken confirmations, board/platform checks, power-critical handling, cancellation boundaries, or physical DFU correlation.

## Important semantic constraint

The existing `FirmwareCatalogueView` combines device selection, catalogue selection, selected firmware and APJ validated-package presentation. The STM32 DFU workflow may reuse the selector and selected-firmware components, but it must not present the APJ package as the artifact being programmed by STM32CubeProgrammer. The DFU workflow ultimately programs the sibling `*_with_bl.hex`.

Likewise, normal Custom Firmware and STM32 Custom HEX should look similar while retaining different validation semantics.
