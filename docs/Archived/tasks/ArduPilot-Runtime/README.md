# MissionPlanner Codex Task Set
## ArduPilot Runtime Detection and Firmware Bootloader Handoff

### Purpose

Improve `InstallFirmware` so a serial device running ArduPilot can be identified as an **ArduPilot application-runtime device** even when the user has not created an active MissionPlanner vehicle connection.

Verified motivating case:

- Windows Device Manager shows `ArduPilot (COM10)`.
- Old MissionPlanner connects to it successfully.
- MissionPlanner Next Gen connects to it successfully.
- Therefore `InstallFirmware` should be capable of proving that the controller is running ArduPilot.
- USB friendly name and VID/PID are **not** sufficient to prove the exact flight-controller board.

Keep these concepts separate:

1. Physical device / serial endpoint.
2. Application runtime — e.g. ArduPilot or Betaflight.
3. Operating/boot mode — application, ArduPilot bootloader, STM32 ROM DFU, unknown.
4. Exact hardware flashing target — established from authoritative bootloader identity, not guessed from USB metadata.

A valid state is:

```text
Port:                 COM10
Runtime:              ArduPilot
Runtime verification: Verified by MAVLink
Operating mode:       Application
Exact board:          Not yet verified
```

### Execution order

Run these tasks **one at a time and in order**:

1. `01-runtime-identity-model.md`
2. `02-ardupilot-runtime-probe.md`
3. `03-installfirmware-runtime-integration.md`
4. `04-ardupilot-install-bootloader-handoff.md`
5. `05-tests-and-regression-guards.md`

Each task must leave the solution buildable and tests passing before the next task begins.

## Global constraints for every task

### Protect the ongoing UI work

The firmware UI has other work in progress. Treat its visual structure as protected.

**Do not modify any existing button that uses an icon.**

This prohibition includes:

- button declarations;
- icons, image resources, glyphs, paths, icon keys or icon templates;
- button styles or templates;
- button dimensions, margin, padding or spacing;
- toolbar ordering or composition;
- button visibility or tooltips;
- button text associated with an icon button;
- command bindings declared on those buttons;
- replacing, adding, removing or renaming icon buttons.

Prefer to make **no view/XAML changes at all** for this task set. If a small status-text binding or label correction is genuinely required, make the smallest possible change and do not reformat surrounding UI markup. It must not touch any icon button.

If a required functional change conflicts with ongoing icon-button work, **stop and report the conflict instead of modifying the UI**.

It is acceptable for an existing button's enabled/disabled state to change naturally because corrected ViewModel state or existing `CanExecute` logic now receives better information. Do not achieve that by changing icon-button declarations or bindings.

### Preserve architecture and existing behavior

- Inspect the current source before implementing.
- Reuse existing firmware, transport, bootloader-entry, compatibility and vehicle-session abstractions.
- Extend existing types where appropriate rather than creating a parallel architecture.
- Do not duplicate existing APJ/bootloader board-ID compatibility logic.
- Preserve existing Betaflight/MSP support and STM32 DFU recovery behavior.
- Preserve current online/local firmware selection unless an explicit integration point is required.
- Do not perform unrelated refactoring, formatting, renaming or cleanup.
- Keep each task narrowly scoped.

### Identity safety rules

- Windows friendly name `ArduPilot` is useful discovery evidence but is **not authoritative proof of the exact board**.
- USB VID/PID is discovery evidence, not exact-board proof where identities are shared.
- A MAVLink response can prove an ArduPilot runtime when its autopilot identity is ArduPilot.
- Exact flashing compatibility must use authoritative ArduPilot bootloader board identity and selected firmware package identity.
- A USB-derived board guess must never override a bootloader board-ID mismatch.
- Never erase or write firmware before strict compatibility checks have succeeded.

### Completion report required from Codex

At the end of each task report:

1. files changed;
2. architecture or behavior changed;
3. tests added/changed;
4. commands/tests run and results;
5. remaining limitations;
6. this explicit statement:

```text
Icon-button definitions/resources/styles were not modified.
```

If that statement cannot truthfully be made, the task is not complete.
