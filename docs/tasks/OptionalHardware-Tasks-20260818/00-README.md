# MissionPlanner NextGen — Optional Hardware Codex Task Set

Prepared from the uploaded `MissionPlanner-202600818-v1` source snapshot and the two classic Mission Planner Optional Hardware screenshots.

The current NextGen route exists but is still a placeholder:

```text
src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/
    OptionalHardwareView.xaml
    OptionalHardwareView.xaml.cs
    OptionalHardwareViewModel.cs
```

There is already useful Setup-domain infrastructure that must be reused rather than replaced:

```text
src/Core/MissionPlanner.Core/Setup/
    OptionalHardwareCatalog.cs
    OptionalHardwareService.cs
    SerialPortsModule.cs
    GpsModule.cs
    RangefinderModule.cs
    AirspeedModule.cs
    CanBusModule.cs
    BatteryConfigurationService.cs
    ActuatorTestService.cs
```

There are also existing higher-level subsystems to reuse:

```text
ConfigTuning/Osd/*
FlightData/Payload/*
FlightData/Tabs/DataFlashLogs*
```

## Execution order

Recommended order:

1. `01-optional-hardware-shell-and-capability-catalog.md`
2. `02-parameter-backed-optional-hardware.md`
3. `03-frame-aware-motor-test.md`
4. `04-compassmot-and-optical-flow-tools.md`
5. `05-osd-camera-gimbal-and-existing-feature-bridges.md`
6. `06-external-serial-tools-sik-bluetooth.md`
7. `07-rtk-gps-injection.md`
8. `08-dronecan-cubeid-esp8266.md`
9. `09-joystick.md`
10. `10-antenna-tracker-and-fft.md`
11. `11-optional-hardware-parity-audit-and-docs.md`

Tasks 1–5 provide the main vehicle-connected Optional Hardware workspace.  
Tasks 6–10 add the larger specialty utilities.  
Task 11 is the final parity/quality gate.

## General repository rules

Before editing, Codex must read the repository guidance that exists on the active branch, especially:

```text
docs/AGENTS.md
docs/AI.md
docs/CODEX.md
docs/DESIGN_CONCEPTS.md
docs/ARCHITECTURE_DECISION_RECORDS.md
```

Also inspect relevant current tests and documentation.

`src-v.1.38` is **behavioral/reference source only**. Do not edit it and do not mechanically port WinForms architecture into NextGen.

Use the current NextGen principles:

- domain/service code outside UI;
- immutable snapshots/results where useful;
- CommunityToolkit.Mvvm observable patterns;
- shared active-vehicle boundary;
- connection-scoped cancellation;
- operation gates for mutually exclusive or safety-sensitive operations;
- parameter writes confirmed by readback;
- lifecycle-aware TabView content;
- adaptive MAUI/UraniumUI presentation.

## TabView requirement

The Optional Hardware page must use the same `ExtendedTabView` family and left-side tab presentation used by Mandatory Hardware:

```text
TabPlacement="Start"
HeaderItemsSource
SelectedHeaderItem
TabViewLifecycleContent
```

Do not recreate the classic WinForms backstage menu.

## Screenshots

Included:

```text
references/classic-optional-connected.png
references/classic-optional-disconnected.png
```

Additional screenshots of individual classic views are **not required to begin**, because `src-v.1.38` contains the implementations. They would, however, be useful later for visual/behavioral review of the large specialty pages such as RTK/GPS Inject, DroneCAN, SiK Radio, Compass/Motor Calibration, Motor Test, Joystick and FFT.

## Verification

From `src/`, use the normal solution build/test workflow. At completion of each task report:

- files changed;
- behavior added/changed;
- protocol/parameter assumptions;
- tests added/changed;
- commands executed and results;
- any hardware-only verification still required.
