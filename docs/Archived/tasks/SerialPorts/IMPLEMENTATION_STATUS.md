# Serial Ports implementation status

Task: [Serial Ports page](TASK_Serial_Ports_Page.md).

Implemented on main in C:/Projects/MissionPlanner-SerialPorts, an isolated worktree at the requested branch.
The original MissionPlanner checkout and its concurrent edits are preserved.

## Delivered

- Serial Ports view, viewmodel and grouped rows under the existing Optional Hardware navigation.
- Dynamic grouping with no fixed port count, numeric ordering, partial groups and unknown suffix handling.
- Existing metadata selectors, multi-select options control and shared confirmed parameter-write session.
- Unknown current values and option bits retained; metadata read-only validation respected.
- Sparse reconnect reconstruction, cancellation, and protection against stale async metadata responses.
- Metadata-driven RCIN warning with optional multiple-receiver support evidence; no broad exclusivity rule.
- Reboot information and separate configured/pending/readback values.
- Documentation in SERIAL_PORTS.md, PARAMETERS.md and FEATURES.md.

The reference src-v.1.38 directory is absent from this repository checkout. Existing Next Gen pages,
parameter controls, edit sessions, tab catalog and lifecycle/test patterns were inspected and reused.

## Verification

- Full solution build: passed.
- Explicit Browser/WASM project build: passed, zero warnings/errors.
- Core regression suite: 620 passed, 6 skipped.
- Affected UI suite: 16 passed (SerialPortsViewModelTests plus ParameterNotificationThreadingTests).
- No new compiler or XML documentation warnings introduced.
- Initial Core run required the locally available ignored MAVLink dialect XML inputs to be copied
  into the new worktree. No vendored source or generated protocol output was changed.
- Physical Pavo20 Pro acceptance, flight-controller reboot persistence and live receiver behavior
  were not verified. No hardware parameters were written.

## Changed source areas

- Core/Setup/OptionalHardware: SerialPortConfiguration, SerialPortsModule, tab catalog/key.
- Core/ConfigTuning/ParameterEditSession: preserve existing unknown bitmask flags during validation.
- App/Models/ParameterItemViewModel: unknown enum display, numeric enum labels and bitmask preservation.
- App/Views/InitSetup/OptionalHardware: SerialPortsView, SerialPortsViewModel, SerialPortRowViewModel,
  existing page navigation.
- App/Configuration/ApplicationConfigurator: standard viewmodel registration.
- Core.Tests: serial grouping/diagnostic tests and updated Optional Hardware contract tests.
- AvaloniaUI.Tests: serial viewmodel tests using real parameter sessions and fake vehicle readbacks.

Build and test logs are local ignored files under artifacts/serial-*.log.
