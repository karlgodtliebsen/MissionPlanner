# Task 07 — Firmware UI Integration and Operator Safety

## Objective

Expose Betaflight identity, `Reboot to DFU`, and safe ArduPilot conversion through the existing Install Firmware experience.

Do not create a separate mini Betaflight Configurator.

## Current UI anchors

Read current:

```text
docs/INSTALL_FIRMWARE_VIEWMODELS.md
```

Current documented anchors include:

```text
FirmwareShellViewModel
FirmwareLandingViewModel
FirmwareDetailsViewModel
FirmwareProgressViewModel
```

under the MissionPlanner firmware feature.

Use their current locations and navigation flow.

## Betaflight identity card

When positive Betaflight identity exists, display available fields such as:

```text
Port
Running firmware: Betaflight
Betaflight version
MSP API
Manufacturer ID / friendly manufacturer
Board identifier
Board name
Target name
MCU
MCU UID (abbreviated)
Build/revision
```

Keep OS USB product, e.g. `STM Device`, as secondary diagnostic information.

## Diagnostics area

Provide a collapsible/secondary diagnostics presentation for:

- full UID;
- VID/PID;
- USB topology;
- raw Betaflight identifiers;
- capability flags;
- probe errors/status;
- compatibility-mapping evidence.

## Actions

### Reboot to DFU

Enable only when:

- positive/current Betaflight identity;
- serial port not owned elsewhere;
- bootloader strategy can handle it;
- safety conditions satisfied.

This is useful independently of conversion.

### Install ArduPilot...

Enable only when the conversion workflow can enter target selection/validation.

Continue through the existing firmware landing/details/progress flow.

Do not duplicate the ArduPilot catalog.

## Destructive confirmation

Before conversion begins, show:

- exact detected Betaflight board;
- selected ArduPilot target;
- firmware version/channel;
- warning that Betaflight firmware/configuration will be replaced;
- **propellers must be removed**;
- Betaflight configuration backup reminder/requirement;
- physical BOOT/STM32 ROM DFU recovery note.

Do not use only a generic `Are you sure?`.

## Target presentation

For exact high-confidence mapping:

- show the selected target;
- show why it matched.

For manual selection:

- show source board identity alongside candidate;
- do not preselect based on MCU family;
- require explicit confirmation.

For unsupported board:

- show unsupported;
- do not offer an unsafe generic flash.

## Progress

Use current `FirmwareProgressViewModel`/equivalent and show meaningful stages:

```text
Identifying Betaflight
Validating ArduPilot target
Requesting STM32 ROM DFU
Waiting for selected FC
DFU device matched
Downloading
Programming
Verifying flash
Waiting for flight controller
Verifying ArduPilot
Complete
```

Failures retain phase + actionable diagnostic.

## Already-in-DFU behavior

Preserve current DFU-only startup/selection.

If MissionPlanner starts while a board is already in ROM DFU, do not claim it is a particular Betaflight board unless reliable cached/work-item correlation proves that identity.

## Multiple FCs

The UI must make the selected device clear throughout transition.

If COM11 and COM12 exist and COM11 is selected, all reboot/progress/correlation state must remain associated with COM11's physical identity.

If correlation becomes ambiguous, stop and show that ambiguity.

## Explicit scope exclusion

Do not add:

- Betaflight OSD;
- MSP DisplayPort;
- DJI Air Unit settings;
- VTX tables;
- PID/rates/configurator features.

The Pavo 20 is later useful for OSD work, but this task set ends at identity/DFU/conversion.

## Tests

Required ViewModel/UI-level tests:

1. Betaflight identity card visible;
2. non-Betaflight does not show Betaflight actions;
3. reboot enablement state;
4. unsupported conversion blocked;
5. exact mapped target shown;
6. confirmation includes source + target + props warning;
7. progress phase updates;
8. cancellation/error;
9. ambiguous DFU state;
10. existing ArduPilot firmware install flow still works.

## Acceptance criteria

A selected Betaflight FC is presented by its actual MSP-derived identity, not merely `STM Device`.

The operator can reboot it to DFU and, if exact compatibility is established, enter the existing ArduPilot firmware selection/install workflow safely.

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
