# Task 01 — Restructure Install Firmware Navigation

**Repository:** `karlgodtliebsen/MissionPlanner`  
**Branch:** `feature/add-DFU-bootload`

## Goal

Restructure only the navigation/composition of the Install Firmware page.

The current horizontal tabs mix firmware-source choices with installation mechanisms:

- Information
- Select Firmware from Catalogue
- Custom Firmware
- STM32 Bootloader
- Help & Support

Replace them with three top-level contexts:

```text
Firmware
STM32 DFU
Help & Support
```

Inside **Firmware**, add a left-sided `TabControl`:

```text
Catalogue
Custom Firmware
```

Inside **STM32 DFU**, add a left-sided `TabControl`:

```text
Device / Enter DFU
Catalogue
Custom HEX
```

Subsequent tasks will refine the DFU subviews.

## Primary files

- `src/UI/MissionPlanner.App/Views/InitSetup/InstallFirmware/InstallFirmwarePage.axaml`
- `src/UI/MissionPlanner.App/Views/InitSetup/InstallFirmware/InstallFirmwarePage.axaml.cs`
- `src/UI/MissionPlanner.App/Views/InitSetup/InstallFirmware/InstallFirmwareViewModel.cs`

Create small presentation enums/properties only if needed for selected-tab state.

## Required behavior

### Top level

The top-level control must contain exactly:

1. `Firmware`
2. `STM32 DFU`
3. `Help & Support`

Use `STM32 DFU`, not `STM32 Bootloader`, because the latter is ambiguous with the ArduPilot serial bootloader.

### Firmware

Retain existing normal behavior:

```text
Firmware
├── Catalogue
│   └── existing FirmwareCatalogueView with normal serial-device presentation
└── Custom Firmware
    └── existing CustomFirmwareView
```

Normal firmware installation continues to use APJ/PX4 and the ArduPilot serial bootloader path.

### STM32 DFU

Create the nested left-tab structure now. It is acceptable initially to host/recompose existing STM32 content while later tasks extract focused views.

### Information tab

Remove the top-level `Information` tab.

Do not remove useful status information. The persistent context/status card above the main tabs must remain. DFU-specific content currently in `LandingView` will move into `STM32 DFU -> Device / Enter DFU` in Task 02.

### Help

Keep the existing `HelpView` as the top-level Help & Support content.

## UI constraints

- Use a left-sided tab strip for nested workflow tabs.
- Preserve stretch and scrolling behavior.
- Avoid unnecessary nested `ScrollViewer`s.
- Preserve the existing `SectionCard` visual language.
- Do not redesign the catalogue grid.
- Do not change firmware-operation behavior in this task.

## Acceptance criteria

1. The page has only three top-level tabs: Firmware, STM32 DFU, Help & Support.
2. Firmware has left-side tabs Catalogue and Custom Firmware.
3. STM32 DFU has left-side tabs Device / Enter DFU, Catalogue, Custom HEX.
4. Existing normal Catalogue UI still renders and selects firmware.
5. Existing normal Custom Firmware UI still renders.
6. Existing Help view still renders.
7. Persistent device/context status and Refresh remain above the tabs.
8. No flashing/downloading/cancellation/safety logic is removed.
9. Affected projects compile.
10. Browser/WASM compilation is not broken.

## Out of scope

- Betaflight target -> ArduPilot target mapping.
- New DFU services.
- Betaflight firmware flashing.
- Manifest-format changes.
