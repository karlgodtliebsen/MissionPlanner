# Flight Data 06 — ArduPilot auxiliary-function execution

## Objective

Implement `AuxFunctionTabView` as a capability-aware interface for generated `MavCmd.DoAuxFunction`, without duplicating richer typed workflows from Actions, Servo/Relay or Payload Control.

Apply all constraints from `00-README.md`.

## Existing protocol support

```text
MavCmd.DoAuxFunction = 218
MavCmdDoAuxFunctionSwitchLevel
IVehicleParameterRegistry and parameter metadata
```

## Catalog and service

Add:

```text
IAuxiliaryFunctionCatalog
AuxiliaryFunctionDescriptor
AuxiliaryFunctionRequest
AuxiliaryFunctionResult
IAuxiliaryFunctionService
IAuxiliaryFunctionPolicy
```

A descriptor contains numeric function ID, name, description, category, switch behavior, hazard level, required state/capability and preferred typed workflow when another feature owns the action.

Catalog sources:

1. official generated enums where available;
2. active parameter metadata for `RCx_OPTION` and related parameters;
3. a reviewed application catalog only for missing descriptions/safety classification.

Preserve unknown IDs. Do not assume the catalog is identical across ArduPilot versions.

## Ownership rules

- Arm/disarm, mode, RTL, takeoff and land remain in Actions.
- Servo/relay remain in Servo/Relay.
- Camera/gimbal functions redirect to Payload Control when supported there.
- Emergency/hazardous functions such as parachute release or motor emergency stop must not appear as generic one-click actions; show them as unavailable with explanation or route to a dedicated confirmed workflow.

## Command workflow

- Encode typed `MavCmd.DoAuxFunction` with generated switch levels.
- Gate through active vehicle, replay, operation gate and policy.
- Require explicit confirmation for warning/high-risk functions.
- Support low/middle/high and momentary behavior as defined by the descriptor.
- For momentary actions, send release/neutral only when required and cancel safely on disconnect.
- Return ACK, timeout, unsupported, denied and disconnected results.
- Never imply observed state from ACK alone.

## UI

Provide search, category/configured-only filters, function description, configured RC channel/source when known, switch/momentary controls, safety badge and last result. Use accessible confirmation UX.

## Lifecycle

Build the catalog on activation from firmware identity and parameters; rebuild on active-vehicle/relevant-parameter changes. Do not clear the bound function collection from `Dispose()`.

## Tests

Cover catalog merging, unknown IDs, version/family filtering, switch encoding, typed-workflow redirect, hazard denial/confirmation, replay, ACK/timeout/disconnect, momentary release and duplicate-operation prevention.

## Documentation

- Add Aux Function semantics and safety categories to `docs/FLIGHT_DATA.md`.
- Update `docs/FEATURES.md`, `docs/PARAMETERS.md`, and `docs/MAVLINK.md` when encoding contracts change.
- Document intentionally unsupported hazardous functions.

## Acceptance criteria

- Supported aux functions execute through a typed, acknowledged service.
- Unknown/hazardous functions are explicit.
- The tab does not duplicate better typed workflows.
