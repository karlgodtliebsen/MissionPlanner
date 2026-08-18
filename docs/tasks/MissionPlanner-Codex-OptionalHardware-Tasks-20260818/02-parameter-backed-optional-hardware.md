# Codex Task 2 — Parameter-backed Optional Hardware Tabs

## Goal

Implement the Optional Hardware features that are fundamentally parameter configuration and can reuse the current metadata/readback infrastructure.

Do not create one generic giant data grid as the final user experience. Use dedicated, readable tab views while sharing services/models underneath.

This task covers:

```text
Battery Monitors
CAN GPS Order
Rangefinder
Airspeed
Optical Flow configuration
Parachute
```

OSD, Camera/Gimbal, CompassMot and Motor Test are separate tasks.

---

## Reuse current infrastructure

Existing domain:

```text
OptionalHardwareService
OptionalHardwareCatalog
PeripheralSetting
PeripheralSettingFactory
RangefinderModule
AirspeedModule
GpsModule
CanBusModule
BatteryConfigurationService
BatteryConfiguration
BatteryMonitorInstance
```

Existing battery UI:

```text
MandatoryHardware/Sections/BatterySetupView.xaml
MandatoryHardware/Sections/BatterySetupViewModel.cs
```

The existing battery service already discovers multiple battery instances. Use that instead of cloning classic `Battery Monitor` and `Battery Monitor 2`.

---

## Shared requirements for parameter-backed tabs

Every parameter-backed view must:

- require the active vehicle it was loaded for;
- cancel on active-vehicle change/disconnect;
- use the live parameter registry as the current source of truth;
- use parameter metadata for labels/options/min/max/increments/reboot flags when available;
- write through the existing parameter service/domain abstraction;
- confirm writes by readback;
- show pending/reboot state clearly;
- never assume a parameter exists on every firmware;
- handle renamed/removed parameters by capability/presence detection;
- avoid sending `PARAM_REQUEST_READ` continuously.

Do not put raw parameter writes in XAML code-behind.

---

## A. Battery Monitors

### Direction

Create **one** tab called:

```text
Battery Monitors
```

Use/host/refactor the existing `BatterySetupView` and `BatteryConfigurationService`.

It should discover and show all available instances:

```text
Battery 1
Battery 2
...
```

Do not reproduce two separate classic pages.

Keep:

- monitor backend/type;
- capacity;
- voltage/current live readings;
- voltage/current calibration;
- low/critical failsafe thresholds where supported;
- stale telemetry indication;
- metadata/readback validation.

Move/refactor the view from Mandatory Hardware only if necessary to avoid ownership confusion. Do not duplicate the service/ViewModel.

---

## B. CAN GPS Order

Classic reference:

```text
src-v.1.38/GCSViews/ConfigurationView/ConfigGPSOrder.cs
```

Historically relevant parameters include:

```text
GPS1_CAN_OVRIDE
GPS2_CAN_OVRIDE
GPS_CAN_NODEID1
GPS_CAN_NODEID2
```

Do not assume those exact names exist on every current firmware.

Create a `CanGpsOrder` projection that discovers current equivalent parameters by exact current parameter presence/metadata.

UI should explain:

- detected GPS/CAN node IDs;
- current ordering/override;
- effect of changing an override;
- reboot requirement where metadata says so.

No write should occur merely by selection; use explicit Apply.

---

## C. Rangefinder

Current module:

```text
RangefinderModule
```

Improve it into an instance-oriented view.

Discover sparse instances:

```text
RNGFND1_*
RNGFND3_*
```

without assuming contiguous numbering.

At minimum expose when present:

```text
TYPE
ORIENT
MIN_CM
MAX_CM
```

Use metadata to add other high-value settings only when they are clearly part of the rangefinder instance.

Show live distance when a suitable vehicle telemetry projection already exists; otherwise keep this task parameter-only rather than adding ad hoc MAVLink plumbing.

---

## D. Airspeed

Current module:

```text
AirspeedModule
```

Support available first/second sensors and current firmware parameter naming.

Historically relevant examples:

```text
ARSPD_TYPE
ARSPD_USE
ARSPD_PIN
ARSPD_RATIO
ARSPD2_TYPE
ARSPD2_USE
```

Use metadata and current presence rather than a fixed classic list only.

Show live airspeed/health from existing vehicle telemetry if it can be mapped unambiguously.

---

## E. Optical Flow configuration

Add a dedicated `OpticalFlowModule`.

Classic reference:

```text
src-v.1.38/GCSViews/ConfigurationView/ConfigHWOptFlow.cs
```

Potential parameter signatures include:

```text
FLOW_ENABLE          legacy
FLOW_TYPE
FLOW_ORIENT_YAW
FLOW_FXSCALER
FLOW_FYSCALER
FLOW_POS_X
FLOW_POS_Y
FLOW_POS_Z
FLOW_HGT_OVR         vehicle-dependent
```

Requirements:

- distinguish legacy `FLOW_ENABLE` from current `FLOW_TYPE` style;
- only show supported parameters;
- preserve Rover-specific behavior for height override only when appropriate;
- do not hard-code old min/max ranges where metadata is available;
- parameter write/readback confirmation is mandatory.

PX4Flow focus/image calibration belongs in Task 4.

---

## F. Parachute

Classic reference:

```text
src-v.1.38/GCSViews/ConfigurationView/ConfigHWParachute.cs
```

Potential parameters:

```text
CHUTE_ENABLED
CHUTE_TYPE
CHUTE_ALT_MIN
CHUTE_SERVO_ON
CHUTE_SERVO_OFF
```

Use current metadata/presence.

Safety:

- make it very clear that configuration can command real deployment hardware if later adding a test function;
- this task is configuration only;
- do not add a parachute deployment/test command unless separately designed and safety-reviewed.

---

## UI pattern

Each tab should use a consistent card/section pattern:

```text
Title
Short explanation/status
Detected instance(s)
Current value
Editable pending value
Apply
Issues / reboot status
Refresh
```

Prefer the same controls/styles already used in Mandatory Hardware.

---

## Tests

Add focused tests for:

1. Battery service/UI discovers multiple instances without duplicate tabs.
2. Sparse rangefinder instances remain supported.
3. Airspeed tab hides unsupported second-sensor settings.
4. Optical Flow legacy/current parameter signatures are both handled.
5. Rover-only optical-flow height override visibility is correct.
6. Parachute tab appears only with relevant parameters.
7. CAN GPS Order tab appears only when actual CAN/GPS ordering parameters exist.
8. Metadata read-only settings cannot be written.
9. write/readback mismatch is reported.
10. reboot-required metadata propagates to the UI.
11. disconnect/vehicle switch cancels edits and reloads the new target.

---

## Acceptance criteria

Complete when these six feature areas have real dedicated Optional Hardware views, reuse current domain infrastructure, perform confirmed writes, and do not depend on classic fixed parameter assumptions.
