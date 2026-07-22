

# MissionPlanner Design Concepts



## Introduction



MissionPlanner is a modern, cross-platform Ground Control Station (GCS) for ArduPilot-based vehicles, written in .NET 10 using .NET MAUI.



Although inspired by the original Windows Mission Planner, this project is **not** a port. It is a complete redesign using modern architectural principles, Domain Driven Design (DDD), dependency injection, immutable state, asynchronous messaging, and transport-independent communication.



The primary goals are:



* Cross-platform operation

* Clean architecture

* Testability

* Extensibility

* Long-term maintainability



---



# Core Philosophy



Everything in the project follows one simple principle:



> **Separate transport, protocol, domain and presentation.**



Those four layers should never become tightly coupled.



```text

&#x20;          User Interface

&#x20;                │

&#x20;        Application Services

&#x20;                │

&#x20;       Domain (Business Logic)

&#x20;                │

&#x20;     MAVLink Protocol Library

&#x20;                │

&#x20;     USB / UDP / TCP / Radio

```



Each layer knows only about the layer immediately beneath it.



---



# Domain Driven Design



The Core project is the heart of the application.



It contains concepts such as



* Vehicle

* Mission

* Parameters

* Telemetry

* Geofence



—not MAVLink packets.



The domain should answer questions such as



> "What is the vehicle doing?"



rather than



> "Which MAVLink packet did we just receive?"



---



# Anti-Corruption Layer



The MAVLink library acts as an Anti-Corruption Layer (ACL).



It converts



```text

MAVLink packets



↓



Domain observations



↓



Vehicle state

```



The rest of the application should never depend directly on MAVLink packet formats.



---



# Transport Independence



The transport layer moves bytes.



Nothing more.



Supported transports may include



* USB Serial

* UDP

* TCP

* DroneBridge

* ELRS

* Bluetooth



The MAVLink library never knows which transport is being used.



---



# Pipeline Architecture



Incoming telemetry flows through a pipeline.



```text

USB



↓



Transport



↓



Channel<ReceivedBytes>



↓



Frame Parser



↓



Message Decoder



↓



Channel<MavLinkMessage>



↓



VehicleMessagePump



↓



Domain



↓



Domain Events



↓



UI

```



Every stage has a single responsibility.



---



# Channels



Channels are intentionally used only inside the communication pipeline.



They provide



* buffering

* back-pressure

* isolation between workers



They are **not** used as the application's event system.



---



# Event System



The project contains its own EventHub.



Domain events are published through EventHub.



Examples



* VehicleConnected

* VehicleDisconnected

* VehicleStateUpdated

* MissionUploaded



The EventHub is intentionally independent of MAVLink.



---



# Immutable Domain State



VehicleState is immutable.



Instead of modifying properties



```csharp

vehicle.State.Roll = ...

```



the domain creates a new state



```csharp

state = state with

{

&#x20;   Motion = ...

};

```



Advantages



* thread safety



* easier testing



* deterministic state changes



* simpler debugging



---



# Vehicle State



VehicleState is divided into logical capabilities rather than MAVLink messages.



```

VehicleState



&#x20;   Identity



&#x20;   Flight



&#x20;   Motion



&#x20;   Position



&#x20;   GPS



&#x20;   Power



&#x20;   Navigation



&#x20;   Radio



&#x20;   Health

```



This avoids creating one property for every MAVLink field.



---



# Observations



Handlers never update VehicleState directly.



Instead they create observations.



Example



```

ATTITUDE



↓



VehicleAttitudeObservation



↓



VehicleSession.ApplyAttitude()



↓



VehicleState

```



This keeps MAVLink details outside the domain.



---



# VehicleSession



VehicleSession owns the state of one connected vehicle.



It contains all domain logic for updating



* flight mode

* position

* attitude

* battery

* GPS

* navigation



No UI code modifies VehicleState directly.



---



# Capability-Oriented Handlers



Handlers represent capabilities instead of protocols.



Examples



* FlightTelemetryHandler

* NavigationTelemetryHandler

* PowerTelemetryHandler



rather than



```

AttitudeHandler



GPSRawHandler



VfrHudHandler



...

```



Multiple MAVLink messages often contribute to one capability.



---



# Decoder Architecture



Every MAVLink message has



* Message record

* Decoder



The decoder is responsible for



* MessageId

* CRC extra

* Payload decoding



A registry builds lookup tables automatically.



---



# Mission Planning



Mission planning is modeled independently of MAVLink.



```

Mission



↓



MissionItems



↓



Validation



↓



Protocol Mapper



↓



MISSION_ITEM_INT

```



The domain contains



Waypoint



Takeoff



RTL



Land



ChangeSpeed



rather than generic Param1–Param4 values.



---



# UI Philosophy



The UI observes the domain.



It does not own it.



ViewModels subscribe to



```

VehicleStateUpdated

```



and transform immutable domain state into display models.



---



# Testing



The project emphasizes automated testing.



Tests exist for



* protocol parsing

* decoders

* command encoding

* transports

* vehicle registration

* serial communication

* parameter download



Whenever practical, new functionality should include tests.



---



# Dependency Injection



All major services are resolved through dependency injection.



Examples



```

IMavLinkTransport



IMavLinkClient



IMavLinkConnection



IVehicleService



IMissionTransferService

```



Avoid using `new` directly inside domain code.



---



# Long-Term Vision



The goal is not merely to recreate Mission Planner.



The goal is to build a reusable Ground Control Station framework capable of supporting



* multiple vehicles

* multiple transports

* future MAVLink dialects

* autonomous mission planning

* AI-assisted planning

* simulation

* plugins



while maintaining a clean and testable architecture.



---



## I would also add one section that many projects miss



At the end I'd include **Architectural Principles**, because they become the "constitution" of the project.



Something like:



```markdown

## Architectural Principles



When adding new functionality:



- Keep MAVLink inside the MAVLink project.

- Keep business rules inside the Core project.

- Never expose MAVLink packets directly to the UI.

- Prefer immutable records over mutable objects.

- Prefer observations over direct state mutation.

- Prefer capability-oriented services over protocol-oriented services.

- Prefer composition over inheritance.

- Prefer asynchronous APIs.

- Keep transports interchangeable.

- Write tests for protocol and domain logic.

- Optimize only after measuring.

```

---

# UI Shell and Persistent Chrome

The MAUI app uses Shell (`AppShell`) as the navigation container, with persistent chrome
that survives page navigation:

- **Top bar** — `Shell.TitleView` in `AppShell.xaml` hosts `TopBarView`
  (`Views/Common`): app title, connection status, connect button.
- **Bottom status bar** — the reusable `StatusBarView` + singleton `StatusBarViewModel`
  (`Views/Common`): clock, connection dot, status messages. It reflects
  `ApplicationStateService` and domain events such as `VehicleConnected`; pages opt in by
  placing it in the bottom row of a two-row grid.
- Because the chrome ViewModels are singletons, every page shows the same live state.

# View construction pattern

Views instantiate their ViewModel in a parameterless constructor via
`ServiceHelper.GetRequiredService<TViewModel>()` and set `BindingContext`. Views are used
directly as XAML elements and are not registered in the container; only ViewModels are
registered (mostly singletons) in `ApplicationConfigurator`. This keeps XAML instantiation
(`<tabs:GaugesTabView />`) working while ViewModels still come from DI.

# Shared mission editor

The mission map editor (`MissionMapView` + singleton `MissionMapViewModel`,
`Views/Missions`) is a shared component hosted by both the FlightData map and the Plan
page. Both screens therefore edit the same mission plan; map pins and the route line are
projections of the domain `Mission` aggregate, never the source of truth.

# Shared Config parameter editor

Config parameter changes are owned by the singleton `IParameterEditSessionFactory` in
Core, not individual pages. Its session is scoped to both the active `VehicleId` and the
protocol-reported firmware identity. It maintains original, live, and pending values,
projects firmware metadata, validates edits, batches writes, and waits for parameter-
registry readback. Disconnects, vehicle switches, and firmware changes invalidate the
session and fail closed before any further write.

Config pages declare only the fields they present. Ordered cross-firmware aliases and
presence rules are explicit `ParameterFieldDefinition` data; pages do not probe guessed
names or access MAVLink transports. Full Parameters List uses the same session for the
complete live registry. Shell navigation preserves pending state between Config tabs and
asks for confirmation before leaving the Config workspace.

Basic Tuning is a profile projection over that session. `BasicTuningProfileCatalog` owns
firmware-family groups, explicit parameter names/aliases, safety text, and coupled rules;
the live registry removes absent and expert-only fields. `BasicTuningService` owns atomic
profile-scoped import/export and delegates group writes to the session. The view model never
writes parameters or invents firmware defaults, and telemetry-only active-vehicle changes do
not rebuild the page.

GeoFence extends this model with a singleton, vehicle-scoped geometry workspace. Its
`FencePlan` is independent from the flight mission and maps to typed fence mission items
only inside `IFenceProtocolMapper`. Local and last-confirmed vehicle revisions remain
separate. Download replacement and vehicle clear preserve a local backup, while incomplete
transfers never replace the editor's current plan. A connection-scoped operation gate
serializes the parameter and geometry phases, and an acknowledgement is required before a
revision is marked synchronized.

# Setup workflow shell

Initial Setup is an orchestration surface over existing domain and Config capabilities.
`SetupWorkflowCatalog` owns workflow ordering, firmware-family relevance, recommended
dependencies, and revalidation rules. `InitSetupViewModel` owns page lifecycle and creates
workflow hosts only when selected. Local completion evidence is fingerprinted with vehicle
identity, firmware, and the known parameter set; it is advisory and becomes a warning when
those inputs change. Later Setup workflows should plug into this shell and call domain
services rather than adding transport access or duplicate parameter editors to the UI.

Firmware management follows a fail-closed pipeline. Manifest selection matches the
protocol-reported firmware family and exact vendor/product/board identifiers; display or
marketing names are not selectors. `FirmwarePackageManager` accepts only HTTPS packages
with a valid manifest SHA-256 and writes them to the platform cache only after verification.
`FirmwareUpdateCoordinator` owns the download/verify/disconnect/flash/reconnect state
machine. Bootloader and serial details belong exclusively behind
`IFirmwareFlashingService`; the default adapter reports unsupported and never disconnects.

Frame setup follows the same fail-closed approach at the parameter boundary.
`FrameConfigurationService` maps each supported firmware family to candidate parameter
names, but a choice reaches the UI only when the connected vehicle has that live parameter
and the current firmware metadata advertises writable enum values. The service validates
reviewed changes again immediately before writing, targets the active `VehicleId`, confirms
each write through parameter-registry readback, and attempts rollback when a later write
fails. Setup completion is recorded only after all requested values have confirmed readback;
optional initial recommendations are separate opt-in changes.

Interactive calibration is modeled as a Core state machine, not a sequence of UI button
side effects. `ArduPilotCalibrationService` consumes decoded `COMMAND_ACK` and
`ACCELCAL_VEHICLE_POS` messages, uses status text only as supplemental evidence, and owns
timeouts, cancellation, disconnect recovery, parameter refresh, and terminal success. The
MAUI workflow is a projection of immutable `CalibrationSnapshot` values. A singleton
`IVehicleOperationGate` is also shared with `VehicleCommandService`, ensuring one
state-changing command or calibration owns a vehicle at a time.





