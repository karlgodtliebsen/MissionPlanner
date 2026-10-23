# MissionPlanner Firmware — Current State Audit

## Audit basis

Source snapshot:

```text
MissionPlanner-202600804-v1
```

Primary implementation locations:

```text
src/Core/MissionPlanner.Firmware
src/Tests/MissionPlanner.Firmware.Tests
src/UI/AvaloniaUI/MissionPlanner.AvaloniaUI.App/Views/InitSetup/InstallFirmware
```

The current implementation is not an early prototype. It already contains a coherent modern ArduPilot/PX4 serial-bootloader subsystem with domain models, bounded protocol operations, catalogue handling, package parsing, installation orchestration, connected bootloader update, UI modes and automated tests.

The correct strategy is to preserve it, correct the remaining defects, improve target selection and add DFU as a separate workflow.

## Build/test verification limitation

The repository contains a test report dated 2026-08-04 stating:

```text
MissionPlanner.Firmware.Tests: 106 passed, 1 skipped, 0 failed
```

The test source currently contains approximately:

```text
77 [Fact] attributes
10 [Theory] attributes
36 [InlineData] attributes
```

The uploaded environment did not contain `dotnet`, so these results were not independently rerun. Treat them as repository-reported until Karl runs the commands in the user test protocol.

## Existing architecture

### Firmware class library

`MissionPlanner.Firmware.csproj` is correctly separated as a `net10.0` class library with nullable enabled and warnings treated as errors. It references only Microsoft abstraction packages and `MissionPlanner.Transport`; it has no Avalonia/Ursa dependency.

### Existing functional areas

The project already includes:

- Catalogue and manifest parsing.
- Conditional HTTP manifest retrieval.
- In-memory catalogue caching.
- APJ/PX4 package parsing.
- ArduPilot-compatible firmware checksum logic.
- Serial-device catalogues and polling monitor.
- Modern ArduPilot/PX4 serial bootloader protocol.
- Bootloader discovery.
- Bootloader-entry strategies.
- Firmware compatibility validation.
- Artifact downloading and local storage.
- Firmware operation coordination and state management.
- Firmware installation orchestration.
- Connected embedded-bootloader update.
- Returning application-device discovery.
- Firmware page-mode resolution.
- Structured diagnostic report support.
- Avalonia/Ursa firmware view and view model.

## Existing task verification

| Original task | Status | Audit result |
|---|---:|---|
| Task 0 — Repository and architecture discovery | Complete | Architecture and task documents exist and the project boundary is coherent. |
| Task 1 — Create projects | Complete | `MissionPlanner.Firmware` and `MissionPlanner.Firmware.Tests` exist. |
| Task 2 — Domain model | Complete | Firmware, board, USB, package, operation, progress and result models exist. |
| Task 3 — Operation state machine | Complete/mostly | Operation coordination, exclusivity and progress states exist; cancellation UX still needs completion. |
| Task 4 — Manifest support | Mostly complete | HTTP retrieval, parser, filtering and memory cache exist. Persistent manifest cache is missing. |
| Task 5 — APJ package parsing | Complete for normal APJ/PX4 | Bounds, Base64/zlib and checksum work exist. External-flash support is conservatively limited. |
| Task 6 — Device/serial abstractions | Mostly complete | Serial abstractions and Windows enumeration exist. Explicit typed UI device selection is not wired. DFU devices are outside this model. |
| Task 7 — Bootloader protocol | Mostly complete | Identify, erase, program, verify and reboot exist. Optional external-flash capacity probing is deliberately disabled. |
| Task 8 — Bootloader discovery | Complete/mostly | Bounded discovery exists. It should prioritize an explicitly selected physical device more strongly. |
| Task 9 — Bootloader entry | Partial integration | Strategies exist, but the UI does not normally provide `ApplicationDevice`, making temporary MAVLink reboot unavailable in normal use. |
| Task 10 — Compatibility | Complete for initial scope | Board and capacity checks exist. Force mode correctly remains deferred. |
| Task 11 — Download/storage | Mostly complete | Streaming, validation and local artifact cache exist. No user-facing download-only workflow; persistence/atomicity need hardening. |
| Task 12 — Installation orchestrator | Complete/mostly | Full workflow exists. User interaction cancellation semantics need correction. |
| Task 13 — Connected Bootloader Update | Complete | Connected/disarmed mode and ACK handling exist. |
| Task 14 — Page mode | Complete | Connected/disconnected/operation/unsupported modes exist. |
| Task 15 — Avalonia/Ursa UI | Partial/usable | Functional UI exists, but platform selection, documentation, cancellation and detail presentation are weak. |
| Task 16 — Custom firmware | Complete for APJ/PX4 | Local `.apj`/`.px4` parsing exists; `.hex` is correctly deferred to DFU. |
| Task 17 — Recovery/reconnect | Complete/mostly | Returning-device discovery exists and port changes are modeled. |
| Task 18 — Diagnostics/logging | Complete/mostly | Diagnostic reports and structured logs exist; download/user evidence can be improved. |
| Task 19 — Documentation | Partial | Strong repository documentation exists, but embedded user documentation/support is missing. |
| Task 20 — Test matrix | Strong, not independently verified | Extensive tests and hardware checklist exist. |
| Task 21 — Deferred backlog | Present | DFU, legacy, secure and additional-platform work is documented but not implemented. |

## High-priority current defects

### P0. Interaction code mismatch

`ManualReconnectBootloaderEntryStrategy` uses:

```text
entry.manual-unplug-replug
```

The UI interaction service recognizes:

```text
bootloader.manual-reconnect
```

The result is that the raw technical code can be presented instead of the intended user instruction.

Required correction:

- Define interaction codes in one shared constants/type location.
- Remove string duplication across domain and UI.
- Add a test proving every emitted code has a UI message.

### P0. User cancellation is discarded

`FirmwareInteractionService.RequestAsync` and `AcknowledgeManualActionAsync` await confirmation dialogs but do not use the returned Boolean result.

Consequences:

- Selecting Cancel may not stop the workflow.
- Manual-action acknowledgement is semantically indistinguishable from rejection.

Required correction:

- Change the interaction contract to return a typed result, or throw `OperationCanceledException` on rejection.
- Add orchestrator tests for accepted, rejected and externally cancelled interactions.

### P0. Temporary MAVLink reboot strategy is not normally wired

The temporary MAVLink bootloader-entry strategy requires an `ApplicationDevice` in `BootloaderEntryContext`.

The current view model creates the installation request using expected USB IDs and bootloader hints but does not pass a selected/application serial device.

Consequences:

- The strategy is usually not applicable.
- The user is pushed toward manual unplug/replug even when the application device is known.

Required correction:

- Keep discovered devices as typed `SerialDeviceDescriptor` view models, not strings.
- Add `SelectedDevice`.
- Pass that device as `ApplicationDevice` and/or preferred discovery candidate.

### P0. No safe user-facing Download & Validate operation

The current UI combines download and installation under the Install command.

Consequences:

- A user cannot verify catalogue selection, HTTP download, package parsing, board metadata and cache behavior without beginning the install workflow.
- Download problems are harder to distinguish from bootloader/discovery problems.

Required correction:

Add an explicit non-destructive workflow:

```text
Select target → Download & Validate → inspect metadata/cache → Install
```

This is the most important immediate UX addition.

### P0. Catalogue refresh races

`OnSelectedChannelChanged` starts `RefreshAsync` fire-and-forget. Rapid channel changes can create overlapping HTTP/cache/device operations and concurrent mutation of `ObservableCollection`.

Required correction:

- Cancel the previous refresh or version requests.
- Serialize collection updates.
- Ignore stale responses.
- Add rapid channel-change tests.

## Target-selection weaknesses

### Automatic first-item selection

The current refresh implementation:

- Groups by vehicle type in normal mode.
- Selects a USB-matching entry if available; otherwise chooses the first item in each group.
- Automatically selects `choices.FirstOrDefault()`.

This is unsafe UX because vehicle family does not uniquely identify the flight-controller platform.

A user selecting “Copter” still needs to identify whether the target is, for example, `CubeOrange`, `MatekH743`, `OmnibusF4`, `SpeedyBeeF405V4`, or another platform.

Compatibility checks protect the erase stage once a bootloader is identified, but the UI should prevent an obviously wrong selection much earlier.

### Recommended selection model

Use:

```text
Vehicle family
Release channel
Manufacturer/brand
Platform/board target
Version
Artifact type
```

Add search and filters for:

- Platform name.
- Brand/manufacturer.
- Board ID.
- Bootloader string.
- USB VID/PID.
- Vehicle family.
- Version/Git SHA.

Never automatically install the first item in a vehicle group.

## Catalogue/download gaps

### Persistent manifest cache missing

DI registers:

```csharp
IFirmwareCatalogCache → MemoryFirmwareCatalogCache
```

A valid catalogue is lost at application restart.

Required correction:

- Add a persistent JSON/gzip cache behind `IFirmwareCatalogCache`.
- Store ETag, Last-Modified, fetch time and source URI.
- Retain stale valid data when the network fails.
- Write atomically.

### Generic HttpClient registration

Firmware DI currently registers a singleton `HttpClient` without a named client, explicit User-Agent, bounded request timeout or firmware-specific policy.

Required correction:

- Add a named/typed firmware HTTP client.
- Set a MissionPlanner User-Agent.
- Use explicit connect/request timeout policy.
- Preserve cancellation.
- Avoid retrying unsafe/large transfers blindly.

### Manifest parser robustness

A malformed item can fail the complete manifest parse depending on where the exception is raised.

Recommended behavior:

- Parse entries individually.
- Skip unusable entries with structured diagnostics.
- Fail only when no usable entries remain or the document itself is invalid.
- Surface skipped-entry count to diagnostics.

### Artifact cache location and atomicity

The artifact store uses:

```text
%TEMP%\MissionPlanner\FirmwareArtifacts
```

and moves the data file before writing metadata.

Risks:

- OS cleanup can remove the cache unexpectedly.
- A metadata write failure can leave an orphan data file.

Required correction:

- Use the application cache-data abstraction/path.
- Commit data and metadata through a transactional directory or atomic rename sequence.
- Add cleanup for orphan/partial artifacts.
- Add cache inspection and clear-cache operations.

## Protocol limitation

### External flash capacity deliberately unavailable

`ArduPilotBootloaderClient.IdentifyAsync` currently sets external flash size to zero because some revision-five bootloaders accept the optional info command but never reply cleanly on Windows.

This is a reasonable safety choice for the initial scope: packages requiring an external image are blocked before erase.

Do not remove this protection casually.

Future work should:

- Identify exact bootloader revisions/boards where external-flash information is safe.
- Add a capability table or bounded probe strategy.
- Keep unsupported external-flash packages blocked.

## UI gaps

The current UI does not yet provide:

- Explicit hardware-target search and confirmation.
- Manufacturer/brand.
- Artifact URL.
- Git SHA.
- USB identifiers and bootloader strings.
- Cache source/freshness.
- Download-only progress/result.
- Save As or Copy Download URL.
- Open cache location.
- Safe cancel button for pre-destructive states.
- Embedded ArduPilot/ST documentation.
- DFU/driver diagnostics.
- Platform limitations and recovery instructions.

## Frame-picture conclusion

There is no technical requirement to duplicate the original Mission Planner’s many airframe pictures.

The firmware image is selected primarily by:

```text
Vehicle family + flight-controller hardware platform
```

Frame geometry such as X, Plus, Hexa or Octa is normally configured later through vehicle parameters. The UI may use a small vehicle-family icon for recognition, but the firmware workflow should emphasize platform identity and compatibility evidence.

Keep explicit distinctions where the firmware manifest itself distinguishes a vehicle family or specialized build, such as Helicopter or another dedicated target.

## Recommended implementation order

### Immediate hardening

1. Fix interaction codes.
2. Correct interaction rejection/cancellation.
3. Introduce typed device selection and wire `ApplicationDevice`.
4. Serialize/cancel refresh operations.
5. Add Download & Validate.

### Selection and download UX

6. Add platform/brand/board search and filters.
7. Stop selecting the first catalogue item automatically.
8. Add detailed selected-artifact panel.
9. Add persistent manifest cache.
10. Add typed firmware HTTP client.
11. Harden artifact-cache atomicity and location.

### Documentation and support

12. Add embedded firmware help and official links.
13. Add standard APJ versus DFU recovery explanation.
14. Add Windows Device Manager and driver guidance.

### DFU

15. Add STM32CubeProgrammer CLI provider.
16. Add Windows DFU-device diagnostics.
17. Add Intel HEX inspection and explicit board-target confirmation.
18. Add advanced/recovery DFU UI and manual hardware protocol.

## Source files requiring early attention

```text
src/Core/MissionPlanner.Firmware/Configuration/FirmwareConfigurator.cs
src/Core/MissionPlanner.Firmware/Catalog/FirmwareCatalogService.cs
src/Core/MissionPlanner.Firmware/Catalog/ArduPilotFirmwareManifestParser.cs
src/Core/MissionPlanner.Firmware/Downloads/FileSystemFirmwareArtifactStore.cs
src/Core/MissionPlanner.Firmware/Downloads/FirmwareArtifactDownloader.cs
src/Core/MissionPlanner.Firmware/Entry/BootloaderEntryStrategies.cs
src/Core/MissionPlanner.Firmware/Installation/FirmwareInstallationService.cs
src/Core/MissionPlanner.Firmware/Protocol/ArduPilotBootloaderClient.cs
src/UI/AvaloniaUI/MissionPlanner.AvaloniaUI.App/Views/InitSetup/InstallFirmware/InstallFirmwareViewModel.cs
src/UI/AvaloniaUI/MissionPlanner.AvaloniaUI.App/Views/InitSetup/InstallFirmware/InstallFirmwareView.axaml
src/UI/AvaloniaUI/MissionPlanner.AvaloniaUI.App/Views/InitSetup/InstallFirmware/FirmwareInteractionService.cs
src/UI/AvaloniaUI/MissionPlanner.AvaloniaUI.App/Views/InitSetup/InstallFirmware/FirmwareHostGateways.cs
```
