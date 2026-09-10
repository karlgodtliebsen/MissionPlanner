# MissionPlanner Next Gen — Install Firmware

Repository: `karlgodtliebsen/MissionPlanner`  
Working branch: `feature/add-DFU-bootload`

## Architectural constraints

Preserve the existing top-level UI organization:

1. `Information` — informational `LandingView`; no operational buttons.
2. `Firmware` — the single operational firmware page with toolbar, device/target information, selected firmware, validation/compatibility status, and install actions. Online selection remains an overlay dialog; local selection remains a file picker/overlay interaction.
3. `Help & Support`.

Do not reintroduce nested firmware/DFU tabs.

Treat these concepts as separate:
- physical controller presence,
- active MissionPlanner vehicle/telemetry connection,
- runtime firmware (`ArduPilot`, `Betaflight`, `Unknown`, none),
- boot environment (`ArduPilot serial bootloader`, `STM32 ROM DFU`, none),
- hardware target identity and confidence,
- selected firmware artifact and provenance,
- artifact validity,
- target compatibility,
- executable installation plan.

Do not infer an exact flight-controller board from a generic USB VID/PID.

The intended installation paths are:
- ArduPilot serial bootloader → `.apj`
- STM32 ROM DFU → `*_with_bl.hex`

Do not replace or rewrite the already-working low-level ArduPilot bootloader programmer or STM32CubeProgrammer implementation unless a task explicitly requires a narrowly scoped correction.

# Codex execution order

Run these tasks **one at a time**, in this order:

1. `01-fix-usb-target-identification.md` — immediate safety correction.
2. `02-introduce-firmware-workflow-state.md` — define the state/capability contract.
3. `03-scope-device-discovery-and-port-ownership.md` — stop treating any Vehicle connection as a global lock.
4. `04-add-installation-plan-resolver.md` — centralize transport/artifact/entry decisions.
5. `05-unify-local-online-artifact-pipeline.md` — normalize provenance, validation and cache.
6. `06-enforce-plan-specific-file-formats-and-compatibility.md` — APJ vs WITH_BL and remove source-specific board-ID policy.
7. `07-complete-runtime-probe-and-boot-mode-actions.md` — finish AP bootloader / STM32 DFU transitions.
8. `08-refactor-install-firmware-ui-around-plan.md` — make the existing single-page UI reflect the state model.
9. `09-fix-progress-cancel-and-diagnostics-ux.md` — correct modal/progress/result behavior.
10. `10-end-to-end-workflow-tests-and-cleanup.md` — lock behavior down and delete obsolete glue.

## Important instruction for every Codex task

Before editing:
1. inspect the current branch implementation and tests;
2. reuse existing firmware/DFU services where possible;
3. keep changes narrowly scoped to the task;
4. run the affected tests before finishing;
5. do not opportunistically redesign unrelated MissionPlanner subsystems.

The highest-priority invariant is:

> A USB VID/PID, COM-port name, STM32 MCU type, or generic USB product string is not sufficient proof of an exact ArduPilot flight-controller board target.


# MissionPlanner Next Gen — Install Firmware

Repository: `karlgodtliebsen/MissionPlanner`  
Working branch: `feature/add-DFU-bootload`

## Architectural constraints

Preserve the existing top-level UI organization:

1. `Information` — informational `LandingView`; no operational buttons.
2. `Firmware` — the single operational firmware page with toolbar, device/target information, selected firmware, validation/compatibility status, and install actions. Online selection remains an overlay dialog; local selection remains a file picker/overlay interaction.
3. `Help & Support`.

Do not reintroduce nested firmware/DFU tabs.

Treat these concepts as separate:
- physical controller presence,
- active MissionPlanner vehicle/telemetry connection,
- runtime firmware (`ArduPilot`, `Betaflight`, `Unknown`, none),
- boot environment (`ArduPilot serial bootloader`, `STM32 ROM DFU`, none),
- hardware target identity and confidence,
- selected firmware artifact and provenance,
- artifact validity,
- target compatibility,
- executable installation plan.

Do not infer an exact flight-controller board from a generic USB VID/PID.

The intended installation paths are:
- ArduPilot serial bootloader → `.apj`
- STM32 ROM DFU → `*_with_bl.hex`

Do not replace or rewrite the already-working low-level ArduPilot bootloader programmer or STM32CubeProgrammer implementation unless a task explicitly requires a narrowly scoped correction.

# Task 01 — Fix unsafe USB-based board identification

## Problem

The current catalogue can identify and auto-select the wrong ArduPilot board solely because the connected controller's runtime USB VID/PID appears in a manifest entry.

The current test hardware is a BETAFPV/Pavo controller, but the UI selected `ACNS-CM4Pilot`, board ID `1115`, as an `Exact USB match`. The USB identity is shared and is not board proof.

Current relevant code includes:

- `src/Core/MissionPlanner.Firmware/Catalog/FirmwareTargetSelector.cs`
- `src/Core/MissionPlanner.Firmware/Catalog/FirmwareTargetConfidence.cs`
- `src/Core/MissionPlanner.Firmware/Catalog/FirmwareTargetMatchReason.cs`
- `src/UI/MissionPlanner.App/Views/InitSetup/InstallFirmware/SubViews/DetectedDeviceViewModel.cs`
- `src/UI/MissionPlanner.App/Views/InitSetup/InstallFirmware/FirmwareCatalogueViewModel.cs`
- `src/Tests/MissionPlanner.Firmware.Tests/FirmwareTargetSelectorTests.cs`

`FirmwareTargetSelector.Recommend` currently promotes a USB match to `High` confidence and `ExactUsbMatch`. `FirmwareCatalogueViewModel` then calls `UnambiguousHighConfidence`, allowing the wrong target to be auto-selected.

## Required change

Make the target-ranking model distinguish **hardware hints** from **board-proven evidence**.

A runtime USB VID/PID match:
- may rank/filter possible firmware targets;
- must not be called `Exact`;
- must not be `High`/verified confidence;
- must never by itself cause automatic board selection.

A textual USB product/board hint has the same restriction unless the value is protocol-proven to identify the exact ArduPilot board target.

High/verified target confidence must come only from evidence such as:
- ArduPilot bootloader protocol board ID, when available;
- a reviewed exact Betaflight-target → ArduPilot-target mapping;
- another explicitly documented protocol-level exact board identifier.

Do not couple physical-device discovery to catalogue matches. `DetectedDeviceViewModel` may say that a device is a firmware-capable serial candidate, but must not claim that a matching VID/PID identifies its exact firmware board.

## UI wording

Replace wording such as:

`Exact catalogue USB match`

with wording such as:

`USB compatibility hint`

or another equally unambiguous phrase.

The catalogue may show a match reason, but the reason must not imply exact hardware identification.

## Required regression test

Create a test containing at least two different firmware targets that share the same USB identifier. Include the observed identity if practical:

- VID `4617`
- PID `22337`

Use targets representing, for example:
- `ACNS-CM4Pilot`
- `BETAFPV-F405`

Assert:
1. both may be ranked as USB-compatible candidates;
2. neither becomes a high-confidence exact board identification solely from VID/PID;
3. `UnambiguousHighConfidence` returns `null`;
4. the catalogue does not auto-select `ACNS-CM4Pilot` merely because it appears first.

Also retain tests showing that genuinely protocol-proven exact evidence can still become high confidence.

## Acceptance criteria

- Plugging a generic ArduPilot USB serial controller cannot cause an unrelated board target to be labelled an exact match.
- Shared VID/PID never automatically selects a firmware platform.
- Existing manual search/filtering remains usable.
- Reviewed Betaflight target mapping remains able to select the exact mapped target.
- All `MissionPlanner.Firmware.Tests` pass.


---

# MissionPlanner Next Gen — Install Firmware

Repository: `karlgodtliebsen/MissionPlanner`  
Working branch: `feature/add-DFU-bootload`

## Architectural constraints

Preserve the existing top-level UI organization:

1. `Information` — informational `LandingView`; no operational buttons.
2. `Firmware` — the single operational firmware page with toolbar, device/target information, selected firmware, validation/compatibility status, and install actions. Online selection remains an overlay dialog; local selection remains a file picker/overlay interaction.
3. `Help & Support`.

Do not reintroduce nested firmware/DFU tabs.

Treat these concepts as separate:
- physical controller presence,
- active MissionPlanner vehicle/telemetry connection,
- runtime firmware (`ArduPilot`, `Betaflight`, `Unknown`, none),
- boot environment (`ArduPilot serial bootloader`, `STM32 ROM DFU`, none),
- hardware target identity and confidence,
- selected firmware artifact and provenance,
- artifact validity,
- target compatibility,
- executable installation plan.

Do not infer an exact flight-controller board from a generic USB VID/PID.

The intended installation paths are:
- ArduPilot serial bootloader → `.apj`
- STM32 ROM DFU → `*_with_bl.hex`

Do not replace or rewrite the already-working low-level ArduPilot bootloader programmer or STM32CubeProgrammer implementation unless a task explicitly requires a narrowly scoped correction.

# Task 02 — Replace Connected/Disconnected page mode with a firmware workflow capability model

## Problem

`FirmwarePageModeResolver` and `FirmwarePageContext` currently reduce firmware UI state primarily to:
- vehicle connected,
- vehicle disconnected,
- unsupported,
- operation in progress.

This is too coarse. An unrelated UDP/TCP vehicle connection is not the physical controller being flashed, and it should not turn the entire firmware page into a disabled connected-mode screen.

Relevant code:
- `src/Core/MissionPlanner.Firmware/Presentation/FirmwarePageContext.cs`
- `src/Core/MissionPlanner.Firmware/Presentation/FirmwarePageMode.cs`
- `src/Core/MissionPlanner.Firmware/Presentation/FirmwarePageState.cs`
- `src/Core/MissionPlanner.Firmware/Presentation/FirmwarePageModeResolver.cs`
- `src/UI/MissionPlanner.App/Views/InitSetup/InstallFirmware/InstallFirmwareViewModel.cs`
- `src/Tests/MissionPlanner.Firmware.Tests/FirmwarePageModeResolverTests.cs`

## Required state model

Introduce a presentation-neutral firmware workflow state/capability model with enough information to describe these independent axes:

- physical target:
  - none,
  - serial controller,
  - STM32 DFU controller;
- runtime:
  - none,
  - ArduPilot,
  - Betaflight,
  - unknown;
- boot environment:
  - none,
  - ArduPilot bootloader,
  - STM32 ROM DFU;
- target identity confidence:
  - unknown/hint/candidate/verified;
- active vehicle connection:
  - none,
  - serial,
  - UDP,
  - TCP,
  - other as already represented by the application;
- firmware operation in progress;
- selected artifact/target state where needed.

The resolver should return capabilities, not merely one mutually-exclusive page mode. At minimum expose decisions equivalent to:

- `CanBrowseOnlineFirmware`
- `CanSelectLocalFirmware`
- `CanRefreshPhysicalDevices`
- `CanProbeRuntime`
- `CanEnterArduPilotBootloader`
- `CanEnterStm32Dfu`
- `CanInstall`
- allowed/required artifact format
- installation block reason

Names may follow existing conventions.

## Required behavior matrix

Implement tests for:

### A — no physical device, no vehicle
- Browse online: yes.
- Select/import local firmware: yes.
- Install: no.
- Device discovery: yes.

### B — no physical device, UDP/TCP vehicle connected
- Browse online: yes.
- Select/import local firmware: yes.
- Install: no physical target, therefore no.
- Device discovery: yes.
- Do not replace the whole firmware page with a connected-mode warning.

### C — serial Betaflight controller
- Runtime = Betaflight.
- APJ serial install is not executable.
- STM32 DFU entry is the relevant transition.
- Final DFU artifact requirement = `WithBootloaderHex`.

### D — serial unknown controller
- Runtime = Unknown.
- Probe/identify is available.
- No destructive install is executable until a safe target/transport is resolved.
- Manual STM32 DFU recovery guidance may be offered.

### E — serial ArduPilot controller
- Required application artifact = APJ.
- ArduPilot bootloader entry/direct bootloader discovery is available.
- `*_with_bl.hex` is not the serial application-install artifact.

### F — STM32 DFU controller
- Required artifact = `WithBootloaderHex`.
- `.apj` install is unavailable.
- Exact board cannot be inferred merely from STM32 ROM DFU USB identity.

### G — controller already in ArduPilot bootloader
- Required artifact = APJ.
- No MAVLink vehicle/runtime connection is required.
- Install/reinstall is executable after compatibility succeeds.

## Acceptance criteria

- Capabilities for A–G are explicit unit-tested behavior, not scattered UI booleans.
- `activeVehicle.IsOnline` alone no longer decides whether firmware browsing/discovery is enabled.
- Existing operation-in-progress and safety lockouts are retained.
- The new model remains UI-framework independent.


---

# MissionPlanner Next Gen — Install Firmware

Repository: `karlgodtliebsen/MissionPlanner`  
Working branch: `feature/add-DFU-bootload`

## Architectural constraints

Preserve the existing top-level UI organization:

1. `Information` — informational `LandingView`; no operational buttons.
2. `Firmware` — the single operational firmware page with toolbar, device/target information, selected firmware, validation/compatibility status, and install actions. Online selection remains an overlay dialog; local selection remains a file picker/overlay interaction.
3. `Help & Support`.

Do not reintroduce nested firmware/DFU tabs.

Treat these concepts as separate:
- physical controller presence,
- active MissionPlanner vehicle/telemetry connection,
- runtime firmware (`ArduPilot`, `Betaflight`, `Unknown`, none),
- boot environment (`ArduPilot serial bootloader`, `STM32 ROM DFU`, none),
- hardware target identity and confidence,
- selected firmware artifact and provenance,
- artifact validity,
- target compatibility,
- executable installation plan.

Do not infer an exact flight-controller board from a generic USB VID/PID.

The intended installation paths are:
- ArduPilot serial bootloader → `.apj`
- STM32 ROM DFU → `*_with_bl.hex`

Do not replace or rewrite the already-working low-level ArduPilot bootloader programmer or STM32CubeProgrammer implementation unless a task explicitly requires a narrowly scoped correction.

# Task 03 — Decouple physical device/runtime discovery from unrelated Vehicle connections

## Problem

Several components currently stop all firmware discovery whenever `activeVehicle.IsOnline` or `IFirmwareConnectionGateway.IsVehicleConnected` is true.

Relevant examples:
- `DetectedDeviceViewModel.Lifecycle.RefreshAsync`
- `FirmwareCatalogueViewModel.Lifecycle.RefreshAsync`
- `FirmwareDeviceIdentityService.EnrichAsync`
- `BetaflightMspBootloaderEntryStrategy`
- `FirmwareLandingViewModel`

This incorrectly blocks firmware work when a vehicle is connected through UDP/TCP and a separate USB controller is being prepared or discovered.

## Required change

Replace global vehicle-online interlocks with **resource-scoped ownership/conflict checks**.

The firmware subsystem must be able to answer whether the specific target serial port is currently owned by an active MissionPlanner serial session.

Use or extend existing connection abstractions rather than referencing UI state from core services. A suitable contract could expose semantics such as:

- active connection transport kind;
- active serial port/stable device identity if applicable;
- whether a requested serial target can be acquired safely.

Do not invent a second connection manager if an existing abstraction can be extended cleanly.

## Required behavior

- UDP vehicle connected + COM10 controller plugged in:
  - COM10 must still be enumerated.
  - Betaflight probing on COM10 may run.
  - Catalogue loading may run.
  - DFU discovery may run.
- TCP vehicle connected + local USB controller: same behavior.
- MissionPlanner serial vehicle session already owns COM10:
  - do not open COM10 concurrently for MSP or bootloader probing;
  - expose a precise block reason.
- An active serial vehicle on COM7 must not automatically block a separate COM10 controller if existing application architecture safely permits separate serial resources.
- A firmware operation owning a target still prevents concurrent probing of that same target.
- Do not weaken the armed-state and live UID verification in `BetaflightMspBootloaderEntryStrategy`.

## LandingView correction

`FirmwareLandingViewModel` is informational only. Remove its operational `RebootToDfuCommand`/operation dispatch.

Update its text:
- do not say all discovery is paused simply because a network vehicle is online;
- show the active vehicle connection separately from local serial/DFU devices;
- explain a real same-port conflict when one exists.

## Catalogue correction

`FirmwareCatalogueViewModel.Lifecycle` must not refuse to load the firmware catalogue just because a vehicle is online. Catalogue browsing/preparation is independent of hardware ownership.

## Tests

Add tests for:
1. UDP connection does not suppress serial enumeration.
2. TCP connection does not suppress catalogue loading.
3. UDP connection does not suppress Betaflight MSP probe of an unowned COM port.
4. same serial port ownership blocks an MSP probe.
5. firmware-operation ownership prevents concurrent target probing.

## Acceptance criteria

- Scenario B behaves as preparation/browse mode, not global-disabled mode.
- Physical device discovery remains useful while network telemetry is active.
- Same-resource conflicts are still fail-closed.


---

# MissionPlanner Next Gen — Install Firmware

Repository: `karlgodtliebsen/MissionPlanner`  
Working branch: `feature/add-DFU-bootload`

## Architectural constraints

Preserve the existing top-level UI organization:

1. `Information` — informational `LandingView`; no operational buttons.
2. `Firmware` — the single operational firmware page with toolbar, device/target information, selected firmware, validation/compatibility status, and install actions. Online selection remains an overlay dialog; local selection remains a file picker/overlay interaction.
3. `Help & Support`.

Do not reintroduce nested firmware/DFU tabs.

Treat these concepts as separate:
- physical controller presence,
- active MissionPlanner vehicle/telemetry connection,
- runtime firmware (`ArduPilot`, `Betaflight`, `Unknown`, none),
- boot environment (`ArduPilot serial bootloader`, `STM32 ROM DFU`, none),
- hardware target identity and confidence,
- selected firmware artifact and provenance,
- artifact validity,
- target compatibility,
- executable installation plan.

Do not infer an exact flight-controller board from a generic USB VID/PID.

The intended installation paths are:
- ArduPilot serial bootloader → `.apj`
- STM32 ROM DFU → `*_with_bl.hex`

Do not replace or rewrite the already-working low-level ArduPilot bootloader programmer or STM32CubeProgrammer implementation unless a task explicitly requires a narrowly scoped correction.

# Task 04 — Add a single FirmwareInstallationPlan resolver above the existing AP and DFU services

## Goal

Make one explicit object answer:

> Given the selected physical controller, what we know about its runtime/boot state, and the selected firmware target/artifact, what operation would MissionPlanner perform?

Do this above the existing:
- `FirmwareInstallationService` for ArduPilot serial bootloader `.apj`;
- `DfuInstallationService` for STM32 ROM DFU `*_with_bl.hex`.

Do not merge these mature services into one implementation.

## Suggested location

Add a focused `Workflow` or `Installation/Planning` area in `MissionPlanner.Firmware`, following the existing project structure.

## Plan contents

Create an immutable plan/result model containing enough information for the UI and execution layer to display/decide:

- physical target identity;
- detected runtime kind;
- detected boot environment;
- selected ArduPilot platform/board ID, if known;
- target identity confidence/evidence;
- selected firmware target/release;
- installation transport:
  - `ArduPilotSerialBootloader`
  - `Stm32RomDfu`
  - none/unresolved;
- boot-entry requirement/strategy:
  - already in AP bootloader;
  - MAVLink reboot to AP bootloader;
  - Betaflight MSP reboot to STM32 DFU;
  - manual reset/reconnect for AP bootloader;
  - manual BOOT/RESET to STM32 DFU;
  - none;
- required artifact format:
  - `Apj`
  - `WithBootloaderHex`;
- artifact validation state;
- target compatibility/safety state;
- `CanExecute`;
- stable block/warning code and human-readable reason;
- expected post-flash runtime (`ArduPilot`).

Use the existing `BootloaderEntryTarget.ArduPilotSerial` and `BootloaderEntryTarget.Stm32RomDfu` concepts rather than creating conflicting terminology.

## Planning rules

- Betaflight runtime → STM32 ROM DFU → `*_with_bl.hex`.
- ArduPilot runtime → ArduPilot serial bootloader → `.apj`.
- Already AP bootloader → `.apj`.
- Already STM32 DFU → `*_with_bl.hex`.
- Unknown serial runtime → no guessed destructive plan. Permit probe/manual recovery guidance.
- No target device → preparation only; no executable install.
- DFU MCU/VID/PID is not exact board proof.
- Same firmware version remains installable; model it as reinstall/repair rather than blocking it.

## Execution

A thin application coordinator may consume the plan and delegate to the appropriate existing service. Do not duplicate flashing logic in the ViewModel.

## Tests

Unit-test plans for all A–G scenarios defined in Task 02.

Additional assertions:
- APJ + DFU target cannot produce an executable plan.
- WITH_BL.HEX + AP serial target cannot produce an executable plan.
- same-version APJ remains executable.
- ambiguous hardware target cannot produce an executable destructive plan.

## Acceptance criteria

`InstallFirmwareViewModel` can ultimately bind to one current installation plan instead of independently deriving transport, file type and install capability from many booleans.


---

# MissionPlanner Next Gen — Install Firmware

Repository: `karlgodtliebsen/MissionPlanner`  
Working branch: `feature/add-DFU-bootload`

## Architectural constraints

Preserve the existing top-level UI organization:

1. `Information` — informational `LandingView`; no operational buttons.
2. `Firmware` — the single operational firmware page with toolbar, device/target information, selected firmware, validation/compatibility status, and install actions. Online selection remains an overlay dialog; local selection remains a file picker/overlay interaction.
3. `Help & Support`.

Do not reintroduce nested firmware/DFU tabs.

Treat these concepts as separate:
- physical controller presence,
- active MissionPlanner vehicle/telemetry connection,
- runtime firmware (`ArduPilot`, `Betaflight`, `Unknown`, none),
- boot environment (`ArduPilot serial bootloader`, `STM32 ROM DFU`, none),
- hardware target identity and confidence,
- selected firmware artifact and provenance,
- artifact validity,
- target compatibility,
- executable installation plan.

Do not infer an exact flight-controller board from a generic USB VID/PID.

The intended installation paths are:
- ArduPilot serial bootloader → `.apj`
- STM32 ROM DFU → `*_with_bl.hex`

Do not replace or rewrite the already-working low-level ArduPilot bootloader programmer or STM32CubeProgrammer implementation unless a task explicitly requires a narrowly scoped correction.

# Task 05 — Unify online and local firmware selection, validation and cache semantics

## Problem

Online firmware currently uses `FirmwarePreparationService` and `ValidatedPackageViewModel`.

Local APJ selection is handled separately by `CustomFirmwareViewModel`, and `InstallFirmwareViewModel.LoadAndValidateAsync` currently has the real preparation code commented out and simply sets `IsFirmwareValidated = true`.

This makes the Local and Online paths semantically different even though both ultimately install the same APJ package type.

## Required design

Make **source/provenance** different while keeping **artifact semantics** common.

Introduce or adapt a normalized selected-artifact presentation/application model containing:

- firmware format;
- platform/summary;
- board ID;
- vehicle type when known;
- firmware/build version;
- image sizes;
- SHA-256;
- source/provenance:
  - official catalogue URL/channel/git SHA, or
  - local file name/original path/imported timestamp;
- cache identity/cache hit;
- structural validation state;
- compatibility state against the currently identified target;
- warnings/errors.

Underlying domain types may remain APJ-specific and DFU-specific where appropriate; the UI-level selected artifact should be common.

## Local APJ pipeline

For a local `.apj`:
1. read/parse with the existing APJ package reader;
2. compute/retain content hash;
3. validate structure and payload;
4. import/copy it into the existing firmware artifact store/cache or extend that store cleanly for local provenance;
5. expose a prepared/stored artifact equivalent to the online-prepared artifact;
6. do not mark it compatible until a target/bootloader identity is available.

Remove the placeholder/commented implementation in `LoadAndValidateAsync`.

## Online pipeline

Keep existing official download integrity/cache handling, but feed the same selected-artifact presentation state.

## DFU HEX

For local `*_with_bl.hex`:
- use the existing `IntelHexInspector`/`DfuArtifactResolver`;
- retain address ranges, SHA-256 and warnings;
- project its result into the same common selected-artifact/status presentation model.
Do not force APJ and Intel HEX into the same binary package class if that would weaken type safety.

## Important distinction

Expose separately:
- `ArtifactValid`
- `TargetCompatible`

A file can be a perfectly valid APJ yet incompatible with the detected bootloader board ID.

## Clear behavior

`Clear Selected Firmware` must clear firmware/artifact state only. It must not reset discovered physical devices or DFU devices.

## Tests

Add tests proving:
- local and online APJ produce equivalent parsed board ID/image metadata;
- local artifact receives a stable content hash/cache identity;
- invalid local APJ never becomes validated;
- selecting a valid local APJ does not imply target compatibility;
- clearing firmware does not clear device discovery state.

## Acceptance criteria

The UI no longer needs separate Local-vs-Online selected-firmware panels simply to represent the same APJ metadata.


---

# MissionPlanner Next Gen — Install Firmware

Repository: `karlgodtliebsen/MissionPlanner`  
Working branch: `feature/add-DFU-bootload`

## Architectural constraints

Preserve the existing top-level UI organization:

1. `Information` — informational `LandingView`; no operational buttons.
2. `Firmware` — the single operational firmware page with toolbar, device/target information, selected firmware, validation/compatibility status, and install actions. Online selection remains an overlay dialog; local selection remains a file picker/overlay interaction.
3. `Help & Support`.

Do not reintroduce nested firmware/DFU tabs.

Treat these concepts as separate:
- physical controller presence,
- active MissionPlanner vehicle/telemetry connection,
- runtime firmware (`ArduPilot`, `Betaflight`, `Unknown`, none),
- boot environment (`ArduPilot serial bootloader`, `STM32 ROM DFU`, none),
- hardware target identity and confidence,
- selected firmware artifact and provenance,
- artifact validity,
- target compatibility,
- executable installation plan.

Do not infer an exact flight-controller board from a generic USB VID/PID.

The intended installation paths are:
- ArduPilot serial bootloader → `.apj`
- STM32 ROM DFU → `*_with_bl.hex`

Do not replace or rewrite the already-working low-level ArduPilot bootloader programmer or STM32CubeProgrammer implementation unless a task explicitly requires a narrowly scoped correction.

# Task 06 — Enforce artifact format and one common compatibility policy

## Required behavior

The current file selection is too broad for a plan-driven workflow. The file picker shown in the UI allows `.apj`, `.px4` and `*_with_bl.hex` together.

For the Install Firmware workflow implement:

### ArduPilot serial/application path
- selectable/installable local artifact: `.apj`;
- serial install validates package board ID against the **protocol-reported ArduPilot bootloader board ID**;
- `*_with_bl.hex` is not accepted as an application-install artifact.

### STM32 ROM DFU path
- selectable/installable local artifact: `*_with_bl.hex` by default;
- `.apj` is not accepted;
- use existing `DfuArtifactResolver`, `IntelHexInspector` and `DfuTargetSafetyService`.

If PX4 support is a separate intentional workflow, do not silently remove it globally. For this ArduPilot Install Firmware workflow, however, do not advertise `.px4` as equivalent to APJ unless the current installation service genuinely supports it end-to-end.

## Remove the local-only board-ID checkbox

Remove `RequireExactBoardIdMatch` from normal Local Firmware UI.

Do **not** add the same checkbox to Online Firmware.

Instead:
- strict board-ID compatibility is the default for all APJ sources;
- online and local packages are checked by the same `FirmwareCompatibilityService` after the ArduPilot bootloader is identified;
- source provenance does not change hardware compatibility rules.

The current logic:
`new FirmwareCompatibilityPolicy(!LocalFirmwareModel.RequireExactBoardIdMatch)`
means unchecking a convenience checkbox enables a board mismatch override. Remove this routine path.

If an advanced mismatch override is intentionally retained for expert recovery:
- put it behind a clearly separate advanced action;
- require explicit typed confirmation;
- record it in diagnostics;
- never enable it by default;
- do not make it Local-only.

## DFU target safety

Do not attempt to compare a ROM-DFU MCU identity as though it were an ArduPilot board ID.

Continue to require:
- selected exact platform;
- reviewed Betaflight mapping when available;
- otherwise the strong confirmation mechanism already implemented by `DfuTargetSafetyService`.

## Tests

- AP serial plan accepts APJ and rejects WITH_BL.HEX.
- DFU plan accepts WITH_BL.HEX and rejects APJ.
- local APJ board mismatch blocks by default.
- online APJ board mismatch blocks by default.
- same board ID succeeds for both.
- generic STM32 MCU/DFU USB identity is not treated as ArduPilot board proof.

## Acceptance criteria

The user cannot accidentally choose the wrong artifact family for the active boot environment, and hardware compatibility policy is source-independent.


---

# MissionPlanner Next Gen — Install Firmware

Repository: `karlgodtliebsen/MissionPlanner`  
Working branch: `feature/add-DFU-bootload`

## Architectural constraints

Preserve the existing top-level UI organization:

1. `Information` — informational `LandingView`; no operational buttons.
2. `Firmware` — the single operational firmware page with toolbar, device/target information, selected firmware, validation/compatibility status, and install actions. Online selection remains an overlay dialog; local selection remains a file picker/overlay interaction.
3. `Help & Support`.

Do not reintroduce nested firmware/DFU tabs.

Treat these concepts as separate:
- physical controller presence,
- active MissionPlanner vehicle/telemetry connection,
- runtime firmware (`ArduPilot`, `Betaflight`, `Unknown`, none),
- boot environment (`ArduPilot serial bootloader`, `STM32 ROM DFU`, none),
- hardware target identity and confidence,
- selected firmware artifact and provenance,
- artifact validity,
- target compatibility,
- executable installation plan.

Do not infer an exact flight-controller board from a generic USB VID/PID.

The intended installation paths are:
- ArduPilot serial bootloader → `.apj`
- STM32 ROM DFU → `*_with_bl.hex`

Do not replace or rewrite the already-working low-level ArduPilot bootloader programmer or STM32CubeProgrammer implementation unless a task explicitly requires a narrowly scoped correction.

# Task 07 — Complete runtime probing and make boot-mode toolbar actions explicit

## Goal

The Firmware toolbar must expose boot transitions that match the selected target's proven state.

There are three different concepts; do not conflate them:

1. **Enter ArduPilot Bootloader** — transition from ArduPilot application runtime to the ArduPilot serial bootloader, or watch for it during manual reset/reconnect.
2. **Enter STM32 DFU** — transition a proven Betaflight controller using MSP when supported, or guide the user through BOOT/RESET.
3. **Update Embedded Bootloader** — an ArduPilot maintenance operation on a connected supported/disarmed vehicle. This is not the same as entering a bootloader.

## Required toolbar behavior

Replace ambiguous icon-only semantics such as a generic `B` with precise tooltip/accessible text and distinct commands/capabilities.

### Betaflight serial runtime
- show/enable `Enter STM32 DFU`;
- perform the existing live MSP re-verification, UID verification and armed-state safety checks;
- use existing `BetaflightMspBootloaderEntryStrategy`/handoff;
- follow device disappearance/re-enumeration;
- preserve the Betaflight identity and reviewed target mapping through the handoff.

### ArduPilot application runtime
- show/enable `Enter ArduPilot Bootloader`;
- use existing MAVLink/serial bootloader entry mechanisms;
- if software entry cannot be performed, show the existing reset/reconnect guidance and watch for the AP bootloader.

### ArduPilot bootloader already present
- do not ask the user to enter it again;
- identify board ID and enable APJ validation/install.

### STM32 DFU already present
- do not ask the user to enter DFU again;
- show DFU target/artifact readiness.

### Unknown serial runtime
- show runtime as Unknown;
- provide `Probe Runtime`/refresh behavior;
- do not label it ArduPilot merely because of VID/PID;
- if no safe software transition is known, offer manual BOOT/RESET DFU guidance rather than sending arbitrary reboot commands.

## LandingView

Keep operational commands out of `LandingView`. It displays the current state only.

Move any remaining `RebootToDfuCommand` ownership to the operational firmware page/workflow coordinator.

## Runtime state

Add explicit runtime probe result/state instead of storing only `BetaflightIdentity` on a serial OS descriptor. Reuse the existing Betaflight probe, and expose an ArduPilot/application/bootloader result as appropriate.

## Tests

- proven Betaflight → Enter STM32 DFU capability true;
- unknown serial device → automatic MSP DFU reboot false;
- ArduPilot runtime → Enter AP bootloader capability true;
- already AP bootloader → entry unnecessary;
- already DFU → entry unnecessary;
- Betaflight handoff preserves target identity evidence;
- runtime VID/PID alone never sets runtime to ArduPilot.

## Acceptance criteria

The toolbar reflects what transition is actually possible for the selected physical controller and cannot direct the user down the wrong flashing path.


---

# MissionPlanner Next Gen — Install Firmware

Repository: `karlgodtliebsen/MissionPlanner`  
Working branch: `feature/add-DFU-bootload`

## Architectural constraints

Preserve the existing top-level UI organization:

1. `Information` — informational `LandingView`; no operational buttons.
2. `Firmware` — the single operational firmware page with toolbar, device/target information, selected firmware, validation/compatibility status, and install actions. Online selection remains an overlay dialog; local selection remains a file picker/overlay interaction.
3. `Help & Support`.

Do not reintroduce nested firmware/DFU tabs.

Treat these concepts as separate:
- physical controller presence,
- active MissionPlanner vehicle/telemetry connection,
- runtime firmware (`ArduPilot`, `Betaflight`, `Unknown`, none),
- boot environment (`ArduPilot serial bootloader`, `STM32 ROM DFU`, none),
- hardware target identity and confidence,
- selected firmware artifact and provenance,
- artifact validity,
- target compatibility,
- executable installation plan.

Do not infer an exact flight-controller board from a generic USB VID/PID.

The intended installation paths are:
- ArduPilot serial bootloader → `.apj`
- STM32 ROM DFU → `*_with_bl.hex`

Do not replace or rewrite the already-working low-level ArduPilot bootloader programmer or STM32CubeProgrammer implementation unless a task explicitly requires a narrowly scoped correction.

# Task 08 — Refactor InstallFirmwarePage around the resolved installation plan

## Preserve the chosen UI organization

Keep exactly:
- Information
- Firmware
- Help & Support

Keep online catalogue selection as an overlay dialog and local firmware selection as a picker.

Do not restore the old nested `Online Firmware / Local Firmware / STM32 DFU` tab layout.

## Firmware page layout

Keep the existing top toolbar, then organize the body around four concepts:

### 1. Workflow/status banner
Replace hard-coded `Standard serial installation` with plan-driven text, e.g.:
- `Prepare firmware — no controller selected`
- `ArduPilot firmware update / reinstall`
- `Betaflight → ArduPilot conversion`
- `STM32 DFU recovery`
- `Controller runtime not identified`
- `ArduPilot bootloader detected`

Include the most important next step/block reason.

### 2. Physical controller card
Show:
- physical endpoint / COM port or DFU USB endpoint;
- VID/PID and stable identity when useful;
- runtime: ArduPilot / Betaflight / Unknown / none;
- boot mode: Application / ArduPilot Bootloader / STM32 DFU;
- exact target/platform if proven;
- identity evidence/confidence;
- Betaflight target/version while live;
- previously detected Betaflight identity only when explicitly marked historical after a handoff.

Do not display `Exact USB match` as board identity.

### 3. One Selected Firmware card
Replace the separate online `SelectedFirmwareView` and local `CustomFirmwareView` presentation with one common selected-artifact view.

Common rows:
- source/provenance;
- format;
- platform;
- board ID;
- vehicle family;
- version/build;
- size;
- hash/cache state.

Conditional provenance rows:
- online: channel, URL, Git SHA;
- local: file/original source/import status.

### 4. Validation/compatibility card
Show distinct status:
- file/artifact validation;
- target compatibility;
- required boot transport/artifact format;
- warnings;
- install/reinstall readiness.

Fix leaked implementation text such as:
- `ValidatedPackageModel package`
- `ValidatedPackageModel cached firmware package`
- `Firmware downloaded and ValidatedModel`

Use user-facing terms.

## Vehicle connection behavior

Remove the current whole-page `IsConnectedMode` replacement.

A UDP/TCP vehicle connection must not hide the firmware page or catalogue preparation.

Show a connection/resource conflict only when it materially conflicts with the selected target.

## Toolbar capabilities

Bind each action to the resolved workflow capabilities:
- Refresh/probe devices
- Enter ArduPilot Bootloader
- Enter STM32 DFU
- Online Firmware
- Local Firmware
- Clear Selected Firmware
- Install/Reinstall/Repair
- Update Embedded Bootloader, only if retained as a distinct supported maintenance operation
- copy URL only when the selected artifact actually has an online URL

## Cleanup

Remove obsolete state left over from the previous nested-tab design when no longer used:
- `SelectedSectionIndex`
- `SelectedDfuTabIndex`
- `CanUseSerialFirmware`/`CanUseDfuFirmware` if superseded by plan capabilities
- large blocks of commented old XAML
- stale section enums only if truly unused.

Do not remove core DFU functionality.

## Acceptance criteria

- A–G scenarios all render a coherent single Firmware page.
- Local and online selected firmware look like the same concept with different provenance.
- No device is required merely to browse/select/validate a firmware artifact.
- Install is enabled only when the current installation plan is executable.
- Clear firmware leaves physical device discovery intact.


---

# MissionPlanner Next Gen — Install Firmware

Repository: `karlgodtliebsen/MissionPlanner`  
Working branch: `feature/add-DFU-bootload`

## Architectural constraints

Preserve the existing top-level UI organization:

1. `Information` — informational `LandingView`; no operational buttons.
2. `Firmware` — the single operational firmware page with toolbar, device/target information, selected firmware, validation/compatibility status, and install actions. Online selection remains an overlay dialog; local selection remains a file picker/overlay interaction.
3. `Help & Support`.

Do not reintroduce nested firmware/DFU tabs.

Treat these concepts as separate:
- physical controller presence,
- active MissionPlanner vehicle/telemetry connection,
- runtime firmware (`ArduPilot`, `Betaflight`, `Unknown`, none),
- boot environment (`ArduPilot serial bootloader`, `STM32 ROM DFU`, none),
- hardware target identity and confidence,
- selected firmware artifact and provenance,
- artifact validity,
- target compatibility,
- executable installation plan.

Do not infer an exact flight-controller board from a generic USB VID/PID.

The intended installation paths are:
- ArduPilot serial bootloader → `.apj`
- STM32 ROM DFU → `*_with_bl.hex`

Do not replace or rewrite the already-working low-level ArduPilot bootloader programmer or STM32CubeProgrammer implementation unless a task explicitly requires a narrowly scoped correction.

# Task 09 — Fix firmware progress, manual-action, cancellation and diagnostic UX

## Problems visible in the current implementation/screenshots

- A cancelled installation is displayed in a dialog titled `Firmware installation failed`.
- The progress window can appear as a small detached/clipped popup.
- Manual bootloader reconnect and generic progress UI can overlap/compete.
- Expected bootloader-probe timeouts generate noisy first-chance exceptions.
- User-facing diagnostic text sometimes exposes internal model/property names.

## Required behavior

### Result presentation

Map terminal states explicitly:
- `Completed` → success dialog/status
- `Cancelled` → cancellation dialog/status, not failure
- `Failed` → failure dialog/status

Do this for both AP serial and DFU flows.

### Progress presentation

Use one correctly sized overlay/progress surface owned by the firmware page/dialog coordinator.

It must:
- remain readable at normal desktop sizes;
- update stage and detail in place;
- not spawn a second detached progress window while an operator action overlay is active;
- preserve page state/selection behind it.

### Manual actions

For manual AP bootloader reset/reconnect:
- show the instruction overlay;
- after Continue, transition that same logical operation into `Waiting for ArduPilot bootloader`;
- closing/cancelling returns `Cancelled` when the operator cancels;
- timeout returns a precise failure only after the discovery deadline.

For manual STM32 BOOT/RESET:
- same principle, but watch DFU enumeration.

### Cancellation safety

Preserve the existing good safety semantics:
- cancellation is immediate before destructive erase/program;
- after destructive AP serial stages begin, defer cancellation until the safe verify/reboot boundary;
- preserve equivalent DFU provider safety behavior.

Make the UI explain when cancellation is deferred.

### Exception/logging behavior

Expected negative probes/timeouts should be represented as normal probe/discovery outcomes where feasible.

Do not log routine `not this protocol` / discovery misses as errors.

Use Debug/Trace/Information for expected fallback flow. Preserve exceptions/logging for actual programmer, verification, corruption and unexpected I/O faults.

### User-facing text cleanup

Remove strings exposing internal class/property names such as `ValidatedPackageModel`.

## Tests

- operator rejects manual reconnect → terminal Cancelled, not Failed;
- manual reconnect timeout → Failed with specific bootloader discovery code;
- successful reconnect → proceeds to compatibility;
- progress stages arrive in correct order;
- cancellation after destructive AP stage is deferred;
- expected probe miss does not surface as installation failure until all applicable strategies are exhausted.

## Acceptance criteria

The screenshots corresponding to current cancellation and clipped-progress behavior can no longer occur.


---

# MissionPlanner Next Gen — Install Firmware

Repository: `karlgodtliebsen/MissionPlanner`  
Working branch: `feature/add-DFU-bootload`

## Architectural constraints

Preserve the existing top-level UI organization:

1. `Information` — informational `LandingView`; no operational buttons.
2. `Firmware` — the single operational firmware page with toolbar, device/target information, selected firmware, validation/compatibility status, and install actions. Online selection remains an overlay dialog; local selection remains a file picker/overlay interaction.
3. `Help & Support`.

Do not reintroduce nested firmware/DFU tabs.

Treat these concepts as separate:
- physical controller presence,
- active MissionPlanner vehicle/telemetry connection,
- runtime firmware (`ArduPilot`, `Betaflight`, `Unknown`, none),
- boot environment (`ArduPilot serial bootloader`, `STM32 ROM DFU`, none),
- hardware target identity and confidence,
- selected firmware artifact and provenance,
- artifact validity,
- target compatibility,
- executable installation plan.

Do not infer an exact flight-controller board from a generic USB VID/PID.

The intended installation paths are:
- ArduPilot serial bootloader → `.apj`
- STM32 ROM DFU → `*_with_bl.hex`

Do not replace or rewrite the already-working low-level ArduPilot bootloader programmer or STM32CubeProgrammer implementation unless a task explicitly requires a narrowly scoped correction.

# Task 10 — Add end-to-end workflow coverage and finish subsystem cleanup

## Goal

After Tasks 01–09, lock the subsystem down with scenario-level tests and remove obsolete glue without changing the chosen UI architecture.

## Required scenario tests

Cover at least:

### A
No physical device, no vehicle:
- catalogue/local preparation available;
- install disabled.

### B
No physical device, UDP/TCP vehicle:
- catalogue/local preparation available;
- physical discovery remains active;
- install disabled because no target exists.

Also test a separate local USB controller while UDP/TCP vehicle is connected.

### C
Betaflight serial device:
- MSP identity is captured;
- target mapping is reviewed;
- APJ serial install is unavailable;
- STM32 DFU transition is selected;
- `*_with_bl.hex` is required;
- after mocked flash/reboot ArduPilot application rediscovery occurs.

### D
Unknown serial device:
- no exact board inferred from USB;
- no destructive install until target/transport safety requirements are satisfied.

### E
ArduPilot application serial device:
- AP bootloader path;
- APJ required;
- same-version reinstall/repair allowed.

### F
Controller already in STM32 DFU:
- `*_with_bl.hex` path;
- APJ unavailable;
- DFU identity alone does not infer exact board.

### G
Controller already in ArduPilot bootloader:
- APJ direct path;
- board ID protocol identity drives compatibility;
- no live MAVLink vehicle is required.

## Negative/safety regression tests

- two targets share VID/PID → no exact-board auto-selection;
- local APJ board mismatch → blocked;
- online APJ board mismatch → blocked;
- serial session owns selected COM port → operation blocked;
- network vehicle on unrelated transport → operation not globally blocked;
- invalid/truncated local APJ → validation failure;
- generic `.hex` in normal DFU picker → rejected/default-hidden;
- cancellation never becomes `Failed` solely because operator cancelled;
- verification failure remains a real `Failed` result.

## UI/ViewModel tests

Use the existing `MissionPlanner.AvaloniaUI.Tests` infrastructure where practical.

Assert:
- exactly three top-level tabs remain;
- Landing has no operational boot/flash buttons;
- Firmware page remains present with network vehicle connected;
- selected firmware presentation is common for Local/Online;
- plan controls allowed file/action capabilities;
- clear firmware does not clear device discovery;
- install command follows `CurrentPlan.CanExecute`.

## Cleanup

After tests are green:
- remove superseded commented XAML;
- remove obsolete nested-section UI state/enums only when no references remain;
- remove dead duplicate selected/validated models if the common artifact presentation replaces them;
- retain low-level AP bootloader and DFU services and tests;
- update XML documentation for the new workflow/state types.

## Verification

Run:
- `MissionPlanner.Firmware.Tests`
- `MissionPlanner.AvaloniaUI.Tests`
- solution/project build(s) normally used by the repository CI for the affected projects.

Do not suppress warnings/errors to make tests pass.

## Completion criteria

The Install Firmware subsystem has one deterministic state/plan model, two explicit flashing transports, common artifact semantics, safe target identification, and tested A–G behavior.
