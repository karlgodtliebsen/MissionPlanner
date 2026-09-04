# Codex Tasks — Complete Mandatory Hardware

## Objective

Complete the remaining **Initial Setup → Mandatory Hardware** workflows in MissionPlanner Next Gen.

The following Mandatory Hardware modules are already implemented and must be used as architectural/UI references:

- Frame
- Accelerometer
- Compass
- Radio
- Servo Output
- ESC
- Flight Modes

The missing workflows are:

1. Failsafe
2. Initial Tune Parameters
3. HW ID
4. ADSB

The original MissionPlanner implementation in `src-v.1.38` is the behavioral reference. The existing Next Gen Mandatory Hardware implementations are the architectural, MVVM, UI, DI, navigation, and styling reference.

---

# Mandatory Naming

Use these canonical names consistently.

| UI Caption | Canonical code name |
|---|---|
| Failsafe | `FailSafe` |
| Initial Tune Parameters | `InitTuneParameters` |
| HW ID | `HwId` |
| ADSB | `Adsb` |

For example, where the existing architecture uses these kinds of types, use:

- `IFailSafeService`
- `FailSafeService`
- `FailSafeViewModel`
- `FailSafeView`

- `IInitTuneParametersService`
- `InitTuneParametersService`
- `InitTuneParametersViewModel`
- `InitTuneParametersView`

- `IHwIdService`
- `HwIdService`
- `HwIdViewModel`
- `HwIdView`

- `IAdsbService`
- `AdsbService`
- `AdsbViewModel`
- `AdsbView`

Do **not** introduce alternative names such as:

- `SafetySetup`
- `FailsafeSetup`
- `InitialTune`
- `HardwareId`
- `HardwareIdentification`
- `ADSBSetup`

The canonical stems above must be used throughout services, ViewModels, Views, DI registrations, navigation definitions, workflow definitions, and tests.

---

# Task 1 — Analyse Existing Mandatory Hardware Architecture

Before implementing the four modules, inspect the existing Mandatory Hardware implementation.

Use several existing workflows as references, particularly:

- Frame
- Accelerometer
- Compass
- Radio
- Servo Output
- ESC
- Flight Modes

Determine and document internally:

- View/ViewModel structure
- service interfaces and implementations
- DI registration
- workflow registration
- TabView integration
- connection-state handling
- parameter loading
- parameter modification
- parameter write-back
- command execution
- busy/loading state
- error handling
- cancellation
- validation
- local completion tracking
- automatic re-evaluation
- connected/disconnected presentation
- navigation from the Mandatory Hardware overview
- styling and Ursa controls already in use

Do not establish a second architecture for the new workflows.

Reuse the existing abstractions wherever appropriate.

Also inspect the corresponding functionality in original MissionPlanner under `src-v.1.38`.

The original application is the source of truth for:

- which parameters are relevant
- parameter value interpretation
- available options
- enable/disable conditions
- vehicle-type restrictions
- command semantics
- warnings
- safety requirements
- user workflow

Do not simply port WinForms controls or code-behind.

Translate the behavior into the existing Next Gen MVVM/domain/service architecture.

### Acceptance criteria

- No duplicate generic parameter-access infrastructure is introduced.
- No second navigation mechanism is introduced.
- No second completion-state mechanism is introduced.
- New modules follow the same lifetime and disposal conventions as existing Mandatory Hardware modules.
- Existing Mandatory Hardware functionality remains unchanged.

---

# Task 2 — Implement `FailSafe`

Implement the complete Mandatory Hardware **Failsafe** workflow using the canonical code name:

`FailSafe`

Create the corresponding service, ViewModel, View, registrations, workflow definition, and tests following the existing architecture.

## Functional analysis

Inspect original MissionPlanner's Failsafe configuration and identify the functionality relevant to the connected vehicle type.

Do not assume that every vehicle exposes the same failsafe settings.

The implementation must derive its available configuration from:

- connected vehicle type
- firmware/autopilot type
- available parameters
- parameter metadata where appropriate

The UI must not present unsupported settings merely because original MissionPlanner contains controls for them.

## Expected behavior

The workflow must:

- require an appropriate connected vehicle before editable configuration is presented
- load current failsafe-related parameters
- display current values
- expose meaningful descriptions/options rather than raw numeric values where mappings are known
- allow modification of supported settings
- validate values before writing
- write changes through the existing parameter service/infrastructure
- update the UI after successful writes
- handle rejected/failed writes
- handle disconnect while the page is active
- re-evaluate when another vehicle is connected

Where a failsafe setting has potentially dangerous consequences, preserve the safety guidance represented by original MissionPlanner.

Do not automatically change safety parameters merely by opening the page.

## UI

The page must visually belong to the existing Mandatory Hardware TabView system.

Follow the same layout, typography, sections, spacing, controls, status presentation, and responsive behavior as existing pages.

Do not reproduce the old MissionPlanner WinForms appearance.

## Completion

Determine completion using the same mechanism as the existing Mandatory Hardware workflows.

A page merely having been opened must not count as successfully configured unless that is already the semantic convention used by the surrounding implementation.

### Tests

At minimum cover:

- disconnected state
- supported vehicle
- unsupported/missing parameter
- loading current configuration
- modifying configuration
- successful parameter write
- failed parameter write
- disconnect during operation
- reconnection/re-evaluation
- completion-state evaluation

---

# Task 3 — Implement `InitTuneParameters`

Implement **Initial Tune Parameters** using the canonical code name:

`InitTuneParameters`

Create:

- service
- service interface where consistent with the existing design
- ViewModel
- View
- DI registration
- Mandatory Hardware workflow registration
- navigation integration
- tests

## Functional reference

Inspect original MissionPlanner's **Initial Tune Parameters** page.

Determine exactly:

- which vehicle types support the workflow
- which parameters are used
- which controls are derived from parameters
- ranges and units
- calculated/recommended values
- dependencies between controls
- whether any values depend on frame configuration, weight, propeller size, motor configuration, battery, or other vehicle properties

Do not approximate formulas if the original implementation contains explicit calculations.

Where original MissionPlanner performs calculations, isolate those calculations from the View.

Prefer a dedicated domain/calculation component if the logic is significant enough to warrant one.

The ViewModel should orchestrate rather than contain substantial tuning mathematics.

## Parameter semantics

Use parameter metadata and strongly typed presentation where available.

The user should see understandable engineering values and units rather than MAVLink parameter encoding details.

Preserve conversion and rounding behavior where relevant.

## Safety

Initial tune values can materially change vehicle behavior.

Therefore:

- never write values merely because the page is opened
- show proposed/current values clearly
- require an explicit user action to apply modifications
- validate against valid ranges
- reject obviously invalid or incomplete input
- surface parameter-write failures

## Completion

Integrate with Mandatory Hardware completion tracking.

Completion criteria should reflect whether required initial-tuning information is valid/configured for the connected vehicle, not simply whether the View has been visited.

### Tests

Cover:

- disconnected state
- unsupported vehicle
- missing parameters
- loading existing values
- calculation logic
- boundary values
- unit/conversion behavior
- changed versus unchanged state
- successful writes
- partial write failure
- full write failure
- reconnect/reload behavior
- completion evaluation

---

# Task 4 — Implement `HwId`

Implement **HW ID** using the canonical code name:

`HwId`

Unlike the configuration-oriented pages, this is primarily an inspection/diagnostic workflow.

Create:

- `HwIdService` and interface if required by the existing conventions
- `HwIdViewModel`
- `HwIdView`
- DI registration
- workflow/navigation registration
- tests

## Functional reference

Inspect original MissionPlanner's **HW ID** page and determine which hardware/device information it obtains and how.

Use existing Next Gen MAVLink/domain capabilities where they already provide the information.

Do not create duplicate MAVLink decoding or parameter retrieval implementations inside `HwIdService`.

Identify any missing MAVLink messages or domain observations separately if needed.

## Presentation

Present useful hardware identification information in a structured form.

Depending on what is actually supplied by the vehicle/original implementation, this may include categories such as:

- autopilot/flight-controller hardware
- board identification
- firmware information
- sensors
- IMUs
- compasses
- barometers
- CAN devices
- peripheral IDs
- vendor/product identifiers

This list is illustrative only.

Implement the actual information exposed by the original MissionPlanner and supported by the current vehicle.

Do not manufacture unavailable values.

Unknown/unreported information should be presented explicitly as unavailable rather than given guessed defaults.

## Refresh behavior

HW ID should:

- populate when a vehicle connects
- clear or become unavailable on disconnect
- reload when the active vehicle changes
- provide explicit refresh if the original workflow or underlying protocol makes that useful

Because this page is mostly diagnostic, avoid forcing it into configuration semantics that do not apply.

## Completion

Investigate how the Mandatory Hardware overview should treat an informational page.

If existing completion infrastructure requires a state, choose a semantically meaningful state rather than pretending hardware IDs require configuration.

Keep the solution consistent with the surrounding workflow model.

### Tests

Cover:

- disconnected state
- hardware information retrieval
- partially available hardware information
- unknown values
- reconnect
- changing active vehicle
- refresh
- cancellation/disposal

---

# Task 5 — Implement `Adsb`

Implement the Mandatory Hardware **ADSB** workflow using the canonical code name:

`Adsb`

Create the complete service/ViewModel/View integration and tests.

## Functional reference

Inspect original MissionPlanner's ADSB setup implementation.

Determine:

- required parameters
- supported vehicle/firmware types
- ADS-B enable/disable semantics
- peripheral/serial dependencies
- ICAO/address configuration if applicable
- callsign configuration if applicable
- emitter/category configuration if applicable
- avoidance configuration if applicable
- parameter value mappings
- validation rules
- units and ranges

Only implement functionality actually supported by ArduPilot/original MissionPlanner and the current MissionPlanner Next Gen parameter infrastructure.

## Dynamic availability

ADSB configuration must react to actual vehicle capabilities.

Handle cases where:

- ADSB parameters do not exist
- only a subset exists
- ADSB is disabled
- ADSB hardware is not configured
- firmware does not expose the feature

Unsupported options should not generate exceptions or bogus configuration.

Prefer hiding or disabling unsupported sections with an explanatory status consistent with the rest of the application.

## Parameter writes

Use the common parameter-write infrastructure.

Support:

- dirty/change tracking
- validation
- explicit apply/write
- successful confirmation
- failed writes
- partial failures
- reload after writes where appropriate

Do not silently swallow failed parameter updates.

### Tests

Cover:

- disconnected state
- vehicle without ADSB support
- full ADSB parameter set
- partial ADSB parameter set
- value mappings
- validation
- enable/disable transitions
- successful write
- rejected write
- reconnect
- completion evaluation

---

# Task 6 — Add the Four Workflows to Mandatory Hardware Navigation

Extend **Initial Setup → Mandatory Hardware** so the complete ordered workflow matches the intended Mandatory Hardware set.

The final order should be:

1. Frame
2. Accelerometer
3. Compass
4. Radio
5. Servo Output
6. ESC
7. Flight Modes
8. FailSafe
9. Init Tune Parameters
10. HW ID
11. ADSB

User-visible captions remain:

- `Failsafe`
- `Initial Tune Parameters`
- `HW ID`
- `ADSB`

Internal identifiers use:

- `FailSafe`
- `InitTuneParameters`
- `HwId`
- `Adsb`

Ensure that:

- cards appear in the Mandatory Hardware overview
- cards navigate to the correct TabView/page
- direct navigation works
- workflow status is displayed consistently
- connected/disconnected availability is correct
- re-evaluation updates the cards
- selected TabView state remains coherent
- no duplicate page instances or stale ViewModels are created accidentally

---

# Task 7 — Reconcile Mandatory Hardware Completion Logic

The current screenshot indicates:

> `0 of 7 relevant workflows completed`

The number of relevant workflows must be recalculated after introducing the four missing modules.

Do not simply change `7` to `11`.

The count must remain capability/vehicle aware.

For example, if ADSB or another workflow is genuinely not applicable to a particular firmware or vehicle type, it should follow the existing distinction between:

- applicable/incomplete
- applicable/complete
- unavailable because disconnected
- not applicable

Inspect the existing workflow/completion model and extend it rather than hard-coding page counts.

### Acceptance criteria

For a connected vehicle:

`CompletedCount <= RelevantWorkflowCount`

and `RelevantWorkflowCount` is determined from workflow applicability.

For a disconnected vehicle, retain the existing presentation semantics.

---

# Task 8 — DI, Lifecycle, Connection and Vehicle-Change Audit

After all four implementations are complete, perform an integration audit.

Verify each service and ViewModel correctly handles:

- initial construction
- navigation onto page
- vehicle already connected
- vehicle connecting after page construction
- vehicle disconnect
- reconnect to same vehicle
- connection to different vehicle
- parameter refresh
- cancellation
- navigation away
- disposal

Pay particular attention to event subscriptions.

There must be no duplicate subscriptions after repeated connect/disconnect cycles.

There must be no stale state left from the previously connected vehicle.

Do not use static state to work around lifecycle problems.

---

# Task 9 — UI Consistency Audit

Compare all eleven Mandatory Hardware workflows.

The four new pages should look and behave as members of the same feature.

Check:

- TabView usage
- headers
- explanatory text
- section layout
- cards/panels
- buttons
- disabled controls
- validation display
- busy indicators
- success/error feedback
- connected/disconnected states
- widths and spacing
- scrolling
- desktop sizing
- tablet/mobile responsiveness
- dark/light theme compatibility

Prefer shared styles/components already present in MissionPlanner Next Gen.

If obvious duplication exists across Mandatory Hardware pages, minor safe extraction to shared components is acceptable.

Do not undertake an unrelated UI-framework refactoring.

---

# Task 10 — Build and Regression Tests

Run the relevant solution build and automated tests after implementation.

At minimum verify:

- existing Mandatory Hardware tests still pass
- new tests pass
- MissionPlanner Next Gen builds
- no new compiler warnings caused by these changes
- DI can resolve all four new workflows
- navigation routes resolve
- no XAML binding errors are introduced
- repeated connect/disconnect does not produce duplicate event handling

Also manually/simulator-test where practical with an ArduCopter vehicle because the supplied comparison screenshot is based on a Quadrotor/Copter configuration.

The implementation must not become Copter-specific where the underlying workflow supports other ArduPilot vehicle types.

---

# Implementation Constraints

1. **Do not port WinForms architecture from original MissionPlanner.**  
   Use it only as a functional reference.

2. **Reuse the existing Next Gen architecture.**  
   Follow the patterns established by the seven completed Mandatory Hardware modules.

3. **Do not bypass domain/services from the ViewModel.**

4. **Do not access MAVLink connections directly from Views.**

5. **Do not duplicate parameter storage or parameter-write logic.**

6. **Do not hard-code parameter metadata that is already available from the MissionPlanner parameter metadata subsystem.**

7. **Gracefully handle missing parameters.**  
   ArduPilot parameter availability varies by firmware, version, hardware and vehicle type.

8. **Preserve cancellation and async behavior.**  
   Do not use `.Result`, `.Wait()` or fire-and-forget operations for vehicle communication.

9. **Keep canonical names exact:**
   - `FailSafe`
   - `InitTuneParameters`
   - `HwId`
   - `Adsb`

10. **Keep user-visible labels consistent with MissionPlanner:**
    - Failsafe
    - Initial Tune Parameters
    - HW ID
    - ADSB

---

# Definition of Done

Mandatory Hardware is considered complete when the Next Gen implementation contains all of:

- Frame
- Accelerometer
- Compass
- Radio
- Servo Output
- ESC
- Flight Modes
- Failsafe
- Initial Tune Parameters
- HW ID
- ADSB

and each applicable workflow:

- is represented in the Mandatory Hardware overview
- has a functional View
- has a ViewModel
- uses the appropriate service/domain layer
- reacts correctly to connection state
- supports its original MissionPlanner functionality where relevant
- uses the existing parameter/command infrastructure
- participates correctly in workflow applicability/completion
- has automated tests
- does not regress already implemented Mandatory Hardware functionality

When uncertain about behavior, inspect the corresponding implementation in `src-v.1.38` before designing new semantics.