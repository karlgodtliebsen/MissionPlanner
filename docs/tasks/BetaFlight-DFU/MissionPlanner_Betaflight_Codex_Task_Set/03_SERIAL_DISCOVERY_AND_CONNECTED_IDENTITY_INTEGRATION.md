# Task 03 — Integrate Betaflight with Current Serial/Firmware Discovery

## Objective

Integrate Task 02 into MissionPlanner's existing device-discovery and connected-firmware identity model.

Do not add a parallel Betaflight device scanner or second global device list.

## Inspect current architecture first

Locate current implementations and call sites for:

```text
ISerialDeviceDiscovery
PlatformSerialDeviceDiscovery
SerialDeviceRegistry
IFirmwareDeviceDiscovery
FirmwareDeviceDiscovery
IConnectedFirmwareIdentityResolver
ConnectedFirmwareIdentityResolver
IFirmwareDeviceMatcher
FirmwareDeviceMatcher
FirmwareDeviceMatchKey
SerialDeviceDescriptor
FirmwareFamily
FirmwareConfigurator
```

Choose integration points based on current `main`.

## Preserve identity layering

Keep these concepts distinct:

### USB/OS identity

```text
COM port
VID/PID
manufacturer/product strings
hardware ID
USB topology/location
```

### Protocol/runtime identity

```text
MSP endpoint
Betaflight / BTFL
Betaflight version/API
board/target/manufacturer
MCU
UID
```

Protocol identity may enrich the existing descriptor, but USB metadata must remain canonical USB metadata.

## Probe policy

MSP probing must be bounded and non-disruptive.

Requirements:

- do not probe a port owned by an active MissionPlanner connection;
- do not continuously reopen every port on every UI refresh;
- use a short configurable timeout;
- cancel when discovery is cancelled;
- dispose the port immediately after probing;
- provide a deliberate refresh/re-probe path.

## Cache/revalidation

Cache positive identity using the strongest current stable device key/topology evidence where useful.

Invalidate/revalidate when:

- the serial device disappears/reappears;
- physical USB identity changes;
- firmware installation occurs;
- operator forces refresh;
- cached identity disagrees with current runtime.

Do not cache “not Betaflight” indefinitely because firmware can change.

## Decide `FirmwareFamily` semantics before editing it

Current `FirmwareFamily` contains:

```text
Unknown
ArduPilot
PX4
Dfu
```

Inspect all uses.

If it means “firmware currently running on this device,” add `Betaflight`.

If it means a narrower install-target concept, do not overload it. Add a separate typed runtime/protocol-family concept and document the decision.

## Typed identity over metadata dumping

Make Betaflight information available to:

- firmware UI;
- bootloader strategy selection;
- conversion workflow;
- diagnostics.

Retain `BetaflightDeviceInfo` as typed authoritative data. It is acceptable to project a small number of fields into existing provider metadata, but do not flatten the whole model there.

## DI

Register the probe/client/integration services through current firmware configuration, currently centered around:

```text
src/Core/MissionPlanner.Firmware/Configuration/FirmwareConfigurator.cs
```

Use lifetimes consistent with neighboring services.

## No MAVLink regression

- A Betaflight serial FC should not be mistaken for a MAVLink vehicle.
- An ArduPilot serial FC must continue through the current path without false Betaflight classification.
- Do not modify the MAVLink parser for MSP.

## Tests

Required:

1. new Betaflight serial device becomes enriched;
2. ArduPilot remains non-Betaflight;
3. generic STM VCP remains unknown;
4. active/owned port is not probed;
5. positive result caching;
6. removal invalidates state;
7. same COM number reused by another physical device does not inherit stale identity;
8. forced refresh re-probes;
9. timeout cannot block overall discovery indefinitely;
10. DI resolves without cycles;
11. existing PX4/DFU/unknown discovery behavior remains valid.

## Acceptance criteria

The existing MissionPlanner firmware-device discovery pipeline reports both the normal serial descriptor and typed Betaflight runtime identity for a physical Betaflight FC.

No duplicate device-discovery subsystem is introduced.

---
## Codex execution rules

1. Work from the current `main` branch and locate every referenced symbol before editing. MissionPlanner is evolving quickly; do not rely on stale paths or duplicate an abstraction that already exists.
2. Keep this task cohesive and limited to its stated scope.
3. Prefer typed protocol/domain models over unstructured metadata.
4. Reuse current MissionPlanner firmware, DFU, device-matching, installation, progress, and recovery abstractions wherever they already solve the problem.
5. Add or update automated tests for every behavioral change.
6. Run the affected project tests plus the relevant `MissionPlanner.Firmware` tests. Run a broader build/test when practical.
7. Fail closed on uncertain identity, compatibility, or physical-device correlation.
8. In the completion report include:
   - files changed;
   - architectural decisions;
   - tests run and results;
   - hardware/manual validation still required;
   - deviations from this task and why.
