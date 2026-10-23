# TASK 01 — Arming Semantic Model and Configuration Service

## Goal
Create a semantic Arming boundary analogous to Compass. The ViewModel must not directly
encode arbitrary `ARMING_*` and `RCx_OPTION` parameter rules.

## Reuse
Inspect/reuse:
- `VehicleArmingDiagnostic`
- `VehicleLiveDiagnostics.Arming`
- `VehicleArmingStatus`
- `RadioArmingConfiguration`
- `SafetyAssessmentService`
- `IVehicleParameterRegistry`
- parameter metadata and `IParameterEditSession`

Do not duplicate armed/readiness truth or create a second parameter cache.

## Add `IArmingConfigurationService`
Suggested responsibilities:
```csharp
ArmingSetupState Read(VehicleId id);
ArmingChangeSet EvaluateChanges(VehicleId id, ArmingConfiguration desired);
Task<ArmingApplyResult> ApplyAsync(VehicleId id, ArmingChangeSet changes, CancellationToken ct);
```

Use the existing loaded registry/metadata. Opening the page must not start another full
parameter download.

## Semantic configuration
Model concepts, not raw numbers:
- Pre-arm checks: Unknown / Disabled / All / Custom bitmask
- Rudder/stick arming: Disabled / Arm only / Arm and disarm
- Require location: Off / On / Unsupported / Unknown
- Arming requirement: metadata-derived option when the firmware exposes it
- RC Arm/Disarm assignment: none / one / multiple / unknown

First-class parameters when present:
```text
ARMING_CHECK
ARMING_RUDDER
ARMING_NEED_LOC
ARMING_REQUIRE
RC1_OPTION ... RC16_OPTION
FLTMODE_CH
RCMAP_ROLL/PITCH/THROTTLE/YAW
```

Keep `ARMING_OPTIONS`, `ARMING_MIS_ITEMS`, thresholds and other expert parameters as
Advanced evidence initially unless a concrete semantic editor is needed.

## ARMING_CHECK
Friendly choices:
```text
All checks (recommended)
Disabled
Custom
```
For Custom, use metadata bitmask entries. Preserve ArduPilot's special `All` semantics;
do not combine the All bit with custom bits accidentally. If metadata is unavailable,
do not invent the custom bit list.

## Capability
Parameter presence and metadata determine capability. Missing does not mean false/zero.
Plane/Rover/Copter differences must be represented honestly.

## State
`ArmingSetupState` should contain current semantic configuration, setting definitions,
capabilities, current RC assignments, validation, defaults and reboot requirements.

Do not duplicate live arming state in this model; compose it with `VehicleArmingDiagnostic`.

## Tests
Cover disabled/all/custom checks, ARMING_RUDDER 0/1/2, supported/unsupported
`ARMING_NEED_LOC` and `ARMING_REQUIRE`, zero/one/multiple RC=153 assignments,
metadata missing, and firmware-family differences.

## Acceptance
UI can consume semantic Arming state without knowing raw parameter numeric meanings.
