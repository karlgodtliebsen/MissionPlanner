# Codex Task — Implement Setup / Optional Hardware / Serial Ports

## Goal

Add a **Serial Ports** configuration page to MissionPlanner Next Gen.

The page must provide the ArduPilot serial-port configuration functionality that is currently missing from Next Gen, while using the existing Next Gen architecture, parameter services, metadata, UI conventions, and test patterns.

This task is not a literal port of the old Mission Planner UI. Reuse the useful behavior, but implement it using the current Avalonia/MVVM/DDD structure.

---

## Repository / preparation

Work on the current `main` branch of the MissionPlanner repository.

Before changing code:

1. Read `ai.md`.
2. Read the relevant design documentation under `docs/`.
3. Inspect the current Setup implementation and especially the existing **Mandatory Hardware** and **Optional Hardware** pages/tabs.
4. Inspect the existing parameter read/write infrastructure, parameter metadata parsing, bitmask editing, validation, busy/error handling, connection-state handling, and reboot-required handling.
5. Inspect the original Mission Planner source (`src-v.1.38`, if present in the repository) for the existing Serial Ports implementation and use it as a behavioral reference only.
6. Do not introduce a second parameter-management mechanism if the current solution already provides the required abstractions.

---

## Placement

Add a new item/page:

**Setup → Optional Hardware → Serial Ports**

Use the same tab/navigation mechanism and visual conventions already used by the other Optional Hardware pages.

The page must work in the desktop Avalonia application and must not introduce platform-specific dependencies that prevent Browser/WASM builds.

---

## Functional requirements

### 1. Discover serial ports from vehicle parameters

Do not hard-code a fixed number of serial ports.

Build the displayed collection from the serial parameter groups that actually exist on the connected vehicle:

- `SERIAL0_PROTOCOL`, `SERIAL0_BAUD`, `SERIAL0_OPTIONS`
- `SERIAL1_PROTOCOL`, `SERIAL1_BAUD`, `SERIAL1_OPTIONS`
- ...
- through the highest `SERIALn_*` group supplied by the vehicle.

A board may not expose every number. Missing groups must not cause an error.

Each displayed row should retain the ArduPilot serial index (`n`) explicitly.

If the existing metadata/infrastructure provides a better board/port display name, use it. Otherwise use a safe fallback such as:

- `SERIAL PORT 1`
- `UART1`

Do not claim a physical UART mapping that cannot actually be established.

### 2. Display columns / controls

For each available serial port display at least:

| Field | Backing parameter |
|---|---|
| Port | `SERIALn_*` group/index |
| Speed | `SERIALn_BAUD` |
| Protocol | `SERIALn_PROTOCOL` |
| Options | `SERIALn_OPTIONS` |

The resulting page should provide the same core capability as the classic Mission Planner Serial Ports page:

- readable port identity;
- baud-rate selector;
- protocol selector;
- options bitmask editor;
- clear indication that applicable changes require a reboot.

### 3. Protocol selector

The protocol selector must be metadata-driven.

Use the existing ArduPilot parameter metadata for `SERIALn_PROTOCOL` and its value descriptions. Do not create a separate hard-coded copy of the ArduPilot protocol enum unless an existing domain enum is already the canonical implementation.

Expected examples include values such as:

- None
- MAVLink
- GPS
- RCIN
- DisplayPort
- other protocols present in the current ArduPilot metadata

Unknown/future protocol values must remain representable and must not crash the view.

### 4. Baud-rate selector

Bind the selector to `SERIALn_BAUD`.

The UI must show human-readable baud rates such as:

- 9600
- 57600
- 115200
- 230400
- 460800
- 921600

while writing the parameter value in the form expected by ArduPilot.

Prefer existing parameter metadata/value conversion infrastructure. Do not scatter baud encoding/decoding logic through the ViewModel.

Unknown/current values must still be displayed rather than silently replaced.

Important: for some RC protocols ArduPilot may control the effective runtime baud rate automatically. The page is configuring the ArduPilot parameter; do not claim that the displayed configured baud is necessarily the live electrical baud rate.

### 5. Options bitmask

Provide an **Options** control for `SERIALn_OPTIONS`.

Reuse the existing generic bitmask editor/dialog if one already exists.

Requirements:

- options and labels come from parameter metadata;
- current bits are shown correctly;
- multiple bits can be selected;
- unknown bits are preserved;
- applying the dialog writes the combined numeric value through the normal parameter-write path;
- no protocol-specific bitmask values are hard-coded in the UI.

A button/icon with a tooltip such as **Serial options** is acceptable if that matches current Next Gen UI conventions.

### 6. Read/write behavior

Use the existing parameter service/store.

The page must:

- populate from the current connected vehicle parameter set;
- refresh correctly after reconnect / vehicle change;
- show parameter values actually read from the vehicle;
- write only values the user changed;
- surface write errors;
- not optimistically claim success when the FC rejected a write;
- respect read-only parameters if encountered;
- retain current values if a write fails.

Do not write parameters merely because the page was opened.

### 7. Reboot-required behavior

Serial settings commonly require a reboot before taking effect.

Reuse the existing Next Gen reboot-required mechanism if available.

At minimum the page must clearly state:

> Changes to serial port settings may not take effect until the flight controller is rebooted.

If the application already tracks pending reboot-required parameter writes, integrate with that mechanism rather than creating page-local state.

### 8. Connection state

When no vehicle is connected:

- the Serial Ports page must remain stable;
- configuration controls must be disabled or show the established disconnected-state UI;
- no parameter-write command may execute.

After connection/reconnection, reconstruct/refresh the displayed port collection from the newly connected vehicle rather than assuming the previous board has the same serial layout.

---

## Configuration diagnostics

Add lightweight diagnostics to make this page more useful than the classic implementation.

### Duplicate RCIN warning

If more than one serial port is configured with:

`SERIALn_PROTOCOL = RCIN`

show a non-blocking warning such as:

> Multiple serial ports are configured for RC input.

If `RC_OPTIONS` is available, inspect the ArduPilot metadata/value for multiple-receiver support and make the warning more specific when possible:

> Multiple RCIN ports are configured, but multiple-receiver support is not enabled.

Do **not** automatically disable or modify either port.

The user must remain in control of configuration changes.

This directly covers cases such as:

- UART1 = RCIN
- UART3 = RCIN

where only one receiver is actually intended.

### Other duplicate protocols

Do not invent broad rules that label duplicate protocols as errors. Multiple MAVLink, GPS, telemetry, etc. ports can be legitimate.

---

## UI / UX

Follow current Next Gen styling rather than copying WinForms styling.

Recommended structure:

```text
Serial Ports

┌────────────────────────────────────────────────────────────┐
│ Port          Speed       Protocol       Options           │
│ SERIAL1       115200      RCIN           [options button]  │
│ UART1                                                      │
├────────────────────────────────────────────────────────────┤
│ SERIAL3       230400      RCIN           [options button]  │
│ UART3                                                      │
└────────────────────────────────────────────────────────────┘

⚠ Multiple serial ports are configured for RC input.

Changes to serial port settings may require a flight-controller reboot.
```

Use existing SectionCard/divider/style resources where appropriate.

The UI must remain usable with a larger number of serial ports and at reduced window sizes.

Avoid unnecessary modal dialogs except for the bitmask editor if that is already the application convention.

---

## Suggested model

Do not treat this exact type as mandatory; adapt it to existing architecture.

```csharp
public sealed class SerialPortConfigurationItem
{
    public int Index { get; }
    public string DisplayName { get; }

    public ParameterItem? Protocol { get; }
    public ParameterItem? Baud { get; }
    public ParameterItem? Options { get; }
}
```

The important point is to group `SERIALn_*` parameters into one serial-port concept instead of scattering string manipulation throughout the AXAML.

If a suitable domain/application model already exists, extend/reuse it.

---

## Parameter grouping

Use one well-tested grouping/parser function.

Conceptually:

```text
SERIAL3_PROTOCOL
SERIAL3_BAUD
SERIAL3_OPTIONS
        ↓
SerialPortConfiguration(index: 3)
```

Requirements:

- parse multi-digit indexes (`SERIAL10_*`);
- do not confuse unrelated parameters beginning with `SERIAL`;
- tolerate missing BAUD or OPTIONS parameters;
- stable ordering by serial index;
- no exception when a future `SERIALn_*` parameter unknown to Next Gen appears.

---

## Tests

Add automated tests at the appropriate existing test layers.

### Unit tests — grouping

Cover at minimum:

1. `SERIAL1_PROTOCOL/BAUD/OPTIONS` produces port 1.
2. Multiple ports are ordered numerically.
3. `SERIAL10_*` parses as port 10, not port 1.
4. Missing `OPTIONS` does not discard the port.
5. Missing `BAUD` does not discard the port.
6. Unrelated parameters are ignored.
7. Empty parameter collection returns no serial ports.

### Unit tests — metadata/value handling

Cover:

1. protocol selected value resolves from metadata;
2. an unknown protocol numeric value remains visible;
3. baud display/write conversion round-trips;
4. an unknown baud value is preserved;
5. bitmask editing preserves unknown bits.

### Unit tests — diagnostics

Cover:

1. one RCIN port → no duplicate warning;
2. two RCIN ports → duplicate RCIN warning;
3. two RCIN ports + multiple-receiver support enabled → no incorrect "support disabled" assertion;
4. duplicate MAVLink ports do not trigger an RCIN warning.

### ViewModel tests

Cover:

1. disconnected state;
2. connection loads port rows;
3. reconnect to a different vehicle rebuilds rows;
4. changed protocol writes the correct `SERIALn_PROTOCOL`;
5. changed speed writes the correct `SERIALn_BAUD`;
6. changed options writes the correct `SERIALn_OPTIONS`;
7. failed parameter write is surfaced and displayed value is not falsely committed;
8. unchanged values are not written.

---

## Manual acceptance test

Use the Pavo20 Pro that exposed this missing feature.

The vehicle currently presents a configuration similar to:

```text
UART1  115200  RCIN
UART3  230400  RCIN
UART4  115200  DisplayPort
UART5   57600  None
UART6   57600  None
```

Acceptance procedure:

1. Connect the Pavo20 Pro.
2. Open **Setup → Optional Hardware → Serial Ports**.
3. Confirm all available serial ports are displayed.
4. Confirm both RCIN ports are visible.
5. Confirm the duplicate-RCIN diagnostic appears.
6. Change UART1 / `SERIAL1_PROTOCOL` from `RCIN` to `None`.
7. Write/apply the change using the standard Next Gen mechanism.
8. Verify the application indicates reboot-required state/information.
9. Reboot the FC.
10. Reconnect.
11. Confirm UART1 shows `None`.
12. Confirm UART3 still shows `RCIN`.
13. Confirm the receiver still operates through UART3.
14. Confirm the duplicate-RCIN warning is gone.
15. Open the UART3 options bitmask and verify current bits can be viewed without altering them.
16. Restart Next Gen and reconnect; verify values are still read correctly from the FC.

---

## Browser/WASM requirement

The feature must compile for all currently supported targets, including the Browser/WASM target.

Do not introduce:

- direct `System.IO.Ports` use in the UI feature;
- Windows-only APIs;
- platform-specific serial-port enumeration.

This page configures **ArduPilot SERIAL parameters on the connected vehicle**. It is not a host-PC COM-port configuration page.

---

## Documentation

Update the relevant Setup/Optional Hardware documentation and, if appropriate, `FEATURES.md`.

Document briefly:

- what `SERIALn_PROTOCOL`, `SERIALn_BAUD`, and `SERIALn_OPTIONS` represent;
- that changing serial configuration commonly requires a reboot;
- that RCIN means ArduPilot serial RC input;
- that multiple RCIN assignments can be intentional but should be reviewed.

Do not duplicate the entire ArduPilot serial protocol reference in project documentation.

---

## Build / quality gate

Before completing the task:

1. Build the full solution.
2. Build the Browser/WASM target.
3. Run all affected unit tests.
4. Run existing tests related to parameters, Setup, and Optional Hardware.
5. Resolve warnings/errors introduced by this change.
6. Do not leave TODO placeholders for core functionality.
7. Do not disable tests to make the build pass.

---

## Deliverables

Return:

1. implementation source changes;
2. tests;
3. relevant documentation update;
4. a concise summary of files changed;
5. build/test results;
6. any discovered limitation in identifying physical UART names from ArduPilot parameters.

---

## Acceptance criteria

The task is complete when:

- **Serial Ports** exists under Setup → Optional Hardware;
- the page dynamically displays the connected vehicle's `SERIALn_*` groups;
- protocol values are metadata-driven;
- baud values are shown in human-readable form and write correctly;
- `SERIALn_OPTIONS` is editable through a bitmask UI;
- parameter writes use existing Next Gen infrastructure;
- reboot-required behavior is communicated/integrated;
- connection/reconnection works safely;
- duplicate RCIN configuration is detected without automatically changing it;
- the Pavo20 Pro can be changed from two RCIN assignments to the intended single RCIN assignment from this page;
- desktop and Browser/WASM builds pass;
- automated tests cover grouping, writing, bitmask preservation, diagnostics, and reconnect behavior.
