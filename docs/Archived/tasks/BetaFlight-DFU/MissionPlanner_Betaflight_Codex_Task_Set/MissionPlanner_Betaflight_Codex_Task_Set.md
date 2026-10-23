# MissionPlanner Next Generation — Betaflight/MSP/DFU Codex Task Set

Generated: 2026-09-08

This file contains the complete task set. The ZIP also contains each task as a separate Markdown file.


---

<!-- BEGIN 00_README.md -->

# MissionPlanner Next Generation — Betaflight/MSP/DFU Codex Task Set

## Goal

Implement first-class Betaflight flight-controller discovery over MSP, show useful board/firmware/MCU identity, allow a proven Betaflight FC to reboot into the STM32 factory ROM DFU bootloader, and then reuse MissionPlanner's existing DFU and ArduPilot firmware infrastructure to convert supported boards to ArduPilot.

Target operator flow:

```text
USB serial device
  -> MSP probe
  -> Betaflight (`BTFL`) identified
  -> detailed board/firmware/MCU identity
  -> Reboot to STM32 ROM DFU
  -> correlate the same physical DFU device
  -> validate/select compatible ArduPilot target
  -> existing MissionPlanner DFU installer
  -> reconnect
  -> verify ArduPilot
```

## Architectural constraint

MissionPlanner already has substantial firmware functionality under `src/Core/MissionPlanner.Firmware`.

At task-set creation time, current `main` includes/references:

```text
src/Core/MissionPlanner.Firmware/
    Configuration/FirmwareConfigurator.cs
    FirmwareFamily.cs
    Entry/
    Discovery/
    Devices/
    Connected/
    Dfu/
    Installation/
    Recovery/
    Safety/
    Tooling/
    Transport/
```

The current DI configuration already registers firmware-device discovery, device matching, bootloader-entry strategies, STM32 DFU enumeration/monitoring, DFU transport backends, DFU programming, ArduPilot firmware catalog/selection, installation, and recovery services.

**Do not implement a second DFU stack.**

The missing bridge is primarily MSP + Betaflight identity + Betaflight bootloader entry + safe orchestration into the existing ArduPilot installer.

## Current firmware UI anchors

Verify against `main` before editing. Current documentation in:

```text
docs/INSTALL_FIRMWARE_VIEWMODELS.md
```

describes these anchors:

```text
src/UI/MissionPlanner.App/Features/Firmware/FirmwareShellViewModel
src/UI/MissionPlanner.App/Features/Firmware/FirmwareLandingViewModel
src/UI/MissionPlanner.App/Features/Firmware/FirmwareDetailsViewModel
src/UI/MissionPlanner.App/Features/Firmware/FirmwareProgressViewModel
```

If current paths differ when Codex executes a task, adapt to the current architecture. Do not recreate obsolete folders.

## Scope

### In scope

- minimal robust MSP framing/client;
- `BTFL` Betaflight proof;
- firmware/API/build identity;
- board/target/manufacturer identity;
- MCU information and UID where supported;
- integration with current serial/firmware discovery;
- MSP reboot into STM32 ROM DFU;
- serial-to-DFU physical-device correlation;
- Betaflight-to-ArduPilot conversion orchestration;
- compatibility validation;
- reuse of current ArduPilot catalog and DFU installer;
- firmware UI integration;
- automated and physical acceptance tests;
- documentation.

### Explicitly out of scope

- full Betaflight Configurator functionality;
- PID/rates/modes configuration;
- Betaflight CLI emulation;
- OSD configuration;
- MSP DisplayPort;
- DJI Air Unit/VTX configuration;
- Betaflight firmware catalog/download/installation.

Betaflight firmware installation is captured only as deferred work in Task 09.

## Safety invariants

The implementation must never:

- classify a device as Betaflight merely because Windows calls it `STM Device`;
- infer ArduPilot compatibility solely from `STM32F4`, `F405`, etc.;
- map every F4 board to `omnibusf4`;
- flash the first DFU device that happens to be present;
- continue after ambiguous device correlation;
- silently replace an operator-selected physical device with another device;
- claim conversion success without post-flash ArduPilot verification.

## Hardware acceptance context

### Three 5-inch drones

The operator has three sets of 5-inch drones being converted from Betaflight to ArduPilot. For the compatible FCs currently being tested, the ArduPilot firmware used is `omnibusf4`.

These provide an excellent complete-conversion test bed for:

- Parameters;
- Radio;
- Motors;
- external buzzer;
- external GPS.

`omnibusf4` is a known hardware test case, **not a universal mapping**.

### BetaFPV Pavo 20

A BetaFPV Pavo 20 running Betaflight with a DJI Air Unit is available for:

- Betaflight/MSP identity validation;
- board/MCU parsing;
- software reboot to ROM DFU;
- DFU correlation;
- later OSD testing.

Do not flash ArduPilot to the Pavo 20 unless its exact board identity is independently proven compatible with an ArduPilot target.

## Task order

Execute one at a time:

1. `01_MINIMAL_MSP_TRANSPORT_AND_PROTOCOL.md`
2. `02_BETAFLIGHT_DEVICE_IDENTITY_PROBE.md`
3. `03_SERIAL_DISCOVERY_AND_CONNECTED_IDENTITY_INTEGRATION.md`
4. `04_BETAFLIGHT_MSP_REBOOT_TO_DFU.md`
5. `05_DFU_HANDOFF_AND_DEVICE_CORRELATION.md`
6. `06_BETAFLIGHT_TO_ARDUPILOT_CONVERSION_WORKFLOW.md`
7. `07_FIRMWARE_UI_AND_OPERATOR_SAFETY.md`
8. `08_TESTS_HARDWARE_ACCEPTANCE_AND_DOCS.md`

`09_DEFERRED_BETAFLIGHT_FIRMWARE_FLASHING.md` is a future design note and is not part of initial implementation.

## Definition of complete

A supported physical Betaflight FC can complete:

```text
Betaflight serial identified by MSP
-> identity displayed
-> software reboot to STM32 ROM DFU
-> same physical DFU device correlated
-> compatible ArduPilot target validated
-> existing MissionPlanner firmware/DFU path used
-> FC returns on USB/serial
-> ArduPilot identity verified
```

Unsupported or ambiguous boards stop before destructive programming.

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

<!-- END 00_README.md -->


---

<!-- BEGIN 01_MINIMAL_MSP_TRANSPORT_AND_PROTOCOL.md -->

# Task 01 — Minimal Robust MSP Transport and Protocol

## Objective

Implement the smallest reusable MSP layer required for Betaflight discovery, identity requests, and reboot-to-bootloader commands.

This is not a general Betaflight configuration library.

## Placement

Inspect current `main` first. Prefer a focused location under the current firmware subsystem, for example:

```text
src/Core/MissionPlanner.Firmware/Betaflight/Protocol/
```

or the equivalent location that best fits current dependency boundaries.

## Required abstractions

Use MissionPlanner naming conventions, but provide concepts equivalent to:

```csharp
public enum MspProtocolVersion
{
    V1,
    V2
}

public static class MspCommand
{
    // named command identifiers
}

public readonly record struct MspFrame(...);

public interface IBetaflightMspClient
{
    Task<MspResponse> RequestAsync(...);
}
```

Do not expose raw serial/parser details to ViewModels.

## Commands needed by the initial feature

Verify every identifier against current upstream Betaflight protocol source before implementing.

Required named definitions:

```text
MSP_API_VERSION
MSP_FC_VARIANT
MSP_FC_VERSION
MSP_BOARD_INFO
MSP_BUILD_INFO
MSP_NAME
MSP_REBOOT
MSP_UID
MSP2_MCU_INFO (if actually needed/supported)
```

Current known values at task-set creation:

```text
MSP_API_VERSION = 1
MSP_FC_VARIANT  = 2
MSP_FC_VERSION  = 3
MSP_BOARD_INFO  = 4
MSP_BUILD_INFO  = 5
MSP_NAME        = 10
MSP_REBOOT      = 68
MSP_UID         = 160
MSP2_MCU_INFO   = 0x300C
```

Current upstream source wins if these ever change.

## MSP v1

Implement framing and checksum validation sufficient for request/response communication.

The receive parser must correctly handle:

- frame fragmentation across reads;
- multiple frames in one read;
- unrelated bytes/noise before a header;
- bad checksum followed by later valid data;
- zero-length payload;
- MSP error response;
- bounded maximum payload length;
- cancellation;
- timeout;
- serial disconnect.

## MSP v2

Implement only what the required identity commands genuinely need.

If `MSP2_MCU_INFO` is used, add the necessary v2 framing support and tests. If equivalent MCU identity is reliably available without it for the supported API range, it may be deferred with a documented decision.

Do not build unused MSP v2 surface area.

## Serial transport

Reuse an existing MissionPlanner serial byte-stream abstraction if suitable.

Otherwise introduce a narrow async transport boundary rather than coupling the parser directly to `System.IO.Ports.SerialPort`.

Requirements:

- explicit exclusive ownership;
- deterministic disposal;
- async read/write;
- cancellation;
- finite timeout;
- typed port-busy/unavailable result;
- no UI dependency.

## Failure model

Distinguish at least:

```text
Timeout
Cancelled
PortUnavailableOrBusy
Disconnected
MalformedFrame
ChecksumFailure
MspErrorResponse
Unsupported
```

Later probing must be able to represent “not an MSP endpoint” without treating it as an application crash.

## Tests

Use deterministic fake transports and hand-authored byte vectors.

Required:

1. exact MSP v1 request bytes;
2. parse a known successful response;
3. bad checksum rejected;
4. fragmented frame;
5. multiple frames/read;
6. noise then valid frame;
7. corrupt frame then resynchronization;
8. zero payload;
9. MSP error response;
10. timeout;
11. cancellation;
12. disconnect;
13. payload-size guard;
14. v2 exact vector/round-trip if v2 is implemented.

Do not generate all parser test inputs with the same encoder under test.

## Acceptance criteria

- A fake Betaflight endpoint can answer `MSP_API_VERSION`.
- Protocol framing survives fragmentation/noise/corruption.
- Command IDs exist in one authoritative code location.
- No application code contains scattered `68` / `1` magic values for reboot.
- MAVLink framing/transport is not modified to understand MSP.
- All affected tests pass.

## Out of scope

Identity interpretation is Task 02. Discovery integration is Task 03. Reboot semantics are Task 04.

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

<!-- END 01_MINIMAL_MSP_TRANSPORT_AND_PROTOCOL.md -->


---

<!-- BEGIN 02_BETAFLIGHT_DEVICE_IDENTITY_PROBE.md -->

# Task 02 — Betaflight Device Identity Probe

## Objective

Use MSP to prove that a serial endpoint is actually Betaflight and retrieve a typed flight-controller identity.

## Positive identification rule

First establish MSP communication and request:

```text
MSP_FC_VARIANT
```

Only classify the endpoint as Betaflight if the returned ASCII firmware variant is exactly:

```text
BTFL
```

Do not classify INAV, Cleanflight, another MSP implementation, or an unresponsive STM32 serial device as Betaflight.

## Typed identity

Introduce a typed model equivalent to:

```csharp
public sealed record BetaflightDeviceInfo(
    string PortName,
    Version? MspApiVersion,
    Version? FirmwareVersion,
    string FirmwareVariant,
    string? BoardIdentifier,
    string? TargetName,
    string? BoardName,
    string? ManufacturerId,
    string? BuildInformation,
    string? SourceRevision,
    string? CraftName,
    string? McuType,
    string? McuUniqueId,
    BetaflightTargetCapabilities Capabilities);
```

Adapt fields to current Betaflight payload definitions and MissionPlanner conventions.

Rules:

- optional/version-dependent values are nullable;
- preserve raw unknown identifiers;
- do not use `Dictionary<string,string>` as the primary model;
- retain full UID internally;
- presentation can abbreviate UID later.

## Probe outcome

Represent at least:

```text
Success
NotMsp
MspButNotBetaflight
PortBusy
Timeout
Disconnected
ProtocolError
UnsupportedApi
```

These should be suitable for both discovery decisions and diagnostics.

## Query sequence

Reject non-Betaflight endpoints early, then retrieve supported details from:

```text
MSP_API_VERSION
MSP_FC_VARIANT
MSP_FC_VERSION
MSP_BOARD_INFO
MSP_BUILD_INFO
MSP_UID
MSP2_MCU_INFO (where implemented/supported)
MSP_NAME (optional)
```

Failure of an optional identity request must not invalidate an otherwise proven `BTFL` identity.

## BOARD_INFO parsing

`MSP_BOARD_INFO` has evolved across API versions.

Parse defensively:

- consume mandatory prefix fields first;
- check remaining payload length before every optional read;
- account for API/version differences when required;
- allow older valid payloads;
- tolerate unknown trailing bytes;
- never read beyond payload bounds.

Where upstream provides them, expose:

- board identifier;
- target name;
- board name;
- manufacturer ID;
- target capability bits;
- board signature/revision material;
- MCU/configuration information.

Only create capability flags after confirming current upstream bit assignments.

## Manufacturer information

The manufacturer ID returned by Betaflight is canonical.

A presentation helper may map known IDs to friendly names such as BetaFPV, but unknown IDs must remain visible and must not be discarded.

## No ArduPilot mapping in this task

Do not infer ArduPilot target from:

```text
STM32F405
F4
manufacturer
USB product string
```

Compatibility is Task 06.

## Tests

Required fixtures:

1. valid `BTFL` endpoint;
2. MSP endpoint with non-Betaflight FC variant;
3. no MSP/timeout;
4. API version;
5. Betaflight firmware version;
6. current-style BOARD_INFO;
7. older/truncated-but-valid BOARD_INFO;
8. BOARD_INFO with additional unknown tail;
9. malformed BOARD_INFO;
10. UID;
11. optional MCU-info unsupported;
12. optional query error without losing core identity;
13. port busy.

## Acceptance criteria

A fake or physical Betaflight FC produces a typed identity containing the available firmware, board, manufacturer, MCU and UID details.

MAVLink-only, INAV, random serial, and unavailable ports are not falsely reported as Betaflight.

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

<!-- END 02_BETAFLIGHT_DEVICE_IDENTITY_PROBE.md -->


---

<!-- BEGIN 03_SERIAL_DISCOVERY_AND_CONNECTED_IDENTITY_INTEGRATION.md -->

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

<!-- END 03_SERIAL_DISCOVERY_AND_CONNECTED_IDENTITY_INTEGRATION.md -->


---

<!-- BEGIN 04_BETAFLIGHT_MSP_REBOOT_TO_DFU.md -->

# Task 04 — Betaflight MSP Reboot to STM32 ROM DFU

## Objective

Add a bootloader-entry strategy that tells a positively identified Betaflight STM32 FC to reboot into the MCU's factory ROM DFU bootloader.

No physical BOOT button should be required during the normal successful path.

## Reuse existing strategy architecture

Inspect:

```text
IBootloaderEntryStrategy
BootloaderEntryService
BootloaderEntryContext
BootloaderEntryResult
ArduPilotRebootToBootloaderStrategy
MavlinkBootloaderEntryStrategy
UsbPortCycleBootloaderEntryStrategy
```

Implement an equivalent of:

```csharp
public sealed class BetaflightMspBootloaderEntryStrategy
    : IBootloaderEntryStrategy
```

## `CanHandle`

Return true only when current, positive MSP Betaflight identity has been established.

Never infer Betaflight capability from:

- `STM Device`;
- ST VID/PID;
- COM number;
- MCU family alone;
- operator-selected target.

## Reboot command

Current upstream Betaflight uses `MSP_REBOOT` with ROM-bootloader reboot mode `1`.

Represent that with a named protocol enum/constant, for example:

```csharp
public enum MspRebootMode : byte
{
    Firmware = 0,
    RomBootloader = 1
}
```

Verify current upstream source at implementation time.

Call sites must use semantic APIs such as:

```csharp
await client.RebootAsync(MspRebootMode.RomBootloader, cancellationToken);
```

Never scatter:

```text
command 68
payload byte 1
```

through application code.

## Armed-state safety

Current Betaflight protects reboot processing while armed, but MissionPlanner must also fail safely.

If reliable armed state can be read with a small existing/added read-only MSP query:

- reject when known armed.

Do not introduce a broad Betaflight telemetry subsystem solely for this.

UI safety acknowledgement is Task 07.

## ACK/disconnect race

The USB serial endpoint may disappear before MissionPlanner consumes a final response.

Treat reboot as successfully **initiated** when either:

1. the command returns an expected MSP success response; or
2. the reboot request was definitely written and the selected serial device disappears within the bounded transition window.

Do not convert arbitrary serial I/O exceptions into success.

A disconnect before the command write is failure.

## Resource lifetime

After sending the reboot request, promptly close/dispose the MSP serial transport so the OS can re-enumerate the device.

No lingering reader may retain the COM handle.

## Strategy ordering

Integrate deliberately with `BootloaderEntryService`.

Conceptual preference:

```text
already DFU -> existing fast path
proven Betaflight -> MSP ROM-DFU strategy
proven ArduPilot -> existing ArduPilot/MAVLink strategy
existing recovery/USB-cycle fallback
```

Use actual current strategy ordering conventions.

## Diagnostics

Differentiate:

```text
RebootAccepted
SerialDisappearedAfterRequest
Armed
PortBusy
TimeoutBeforeWrite
MspRejected
SerialDidNotDisappear
Cancelled
IdentityStale
```

or current equivalent.

## Tests

Required:

1. `CanHandle` only for proven Betaflight;
2. exact encoded reboot request uses ROM mode;
3. ACK success;
4. request written then disconnect-before-ACK success;
5. disconnect-before-write failure;
6. no ACK and serial remains -> timeout/failure;
7. MSP error -> failure;
8. armed -> no reboot write;
9. cancellation;
10. correct strategy ordering;
11. transport disposed.

## Physical acceptance

On a Betaflight STM32 FC:

```text
normal COM port
-> MissionPlanner proves BTFL
-> Reboot to DFU
-> COM disappears
-> STM32 ROM DFU enumerates
```

without pressing BOOT.

Physical BOOT remains the recovery fallback.

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

<!-- END 04_BETAFLIGHT_MSP_REBOOT_TO_DFU.md -->


---

<!-- BEGIN 05_DFU_HANDOFF_AND_DEVICE_CORRELATION.md -->

# Task 05 — DFU Handoff and Physical-Device Correlation

## Objective

After MSP reboot, identify the **same physical FC** when it re-enumerates as STM32 ROM DFU.

Never choose “the first DFU device.”

## Reuse current MissionPlanner DFU infrastructure

Inspect and reuse current equivalents of:

```text
IDfuDeviceEnumerator
PlatformDfuDeviceEnumerator
IDfuDeviceMonitor
DfuDeviceMonitor
SerialDfuDeviceResolver
FirmwareDeviceMatcher
FirmwareDeviceMatchKey
IDfuFirmwareInstaller
DfuFirmwareInstaller
IDfuTransportBackendRouter
LibUsbDotNetDfuTransportBackend
DfuUtilDfuTransportBackend
CubeProgrammerDfuTransportBackend
DfuStageValidator
```

Do not add another USB polling/programming stack.

## Expected STM32 ROM DFU candidate

Normal ST ROM DFU is typically:

```text
VID 0483
PID DF11
```

This identifies an STM32 DFU candidate, not necessarily the selected FC.

## Transition states

Use or extend current work-item/session state concepts to represent:

```text
BetaflightSerialIdentified
RebootRequested
WaitingForSerialRemoval
WaitingForDfuEnumeration
DfuCandidateFound
DfuCorrelated
ReadyForFirmware
Failed
Cancelled
```

Avoid an isolated duplicate state machine if current firmware work-item abstractions already cover this.

## Correlation evidence

Use the strongest reliable evidence available:

- current stable device-match key;
- USB topology/location/parent path;
- physical-port identity;
- before/after VID/PID transition at same location;
- serial/UID evidence only where genuinely comparable.

The MSP MCU UID is useful source identity but must not be assumed to equal a DFU serial string on every platform.

## Ambiguity rules

### No DFU appears

Timeout with diagnostics. Higher layer can advise physical BOOT recovery.

### DFU already existed before reboot

Record the pre-existing set and do not automatically associate it with the selected FC.

### Multiple new DFU candidates

Proceed only with unambiguous topology/identity evidence.

### Wrong topology

Do not substitute.

### Selected COM remains present

Do not proceed to flashing.

### Delayed enumeration

Use existing configurable monitor timeout/cancellation behavior.

## Multi-device requirement

Model cases such as:

```text
COM11 -> FC A -> selected
COM12 -> FC B -> untouched
DFU X -> unrelated/pre-existing
```

Only FC A's correlated DFU endpoint may become the firmware target.

## Retain evidence

Keep enough source/transition evidence for later conversion logs:

```text
Source Betaflight:
  port
  board identity
  MCU UID
  USB topology

DFU:
  VID/PID
  topology
  correlation evidence/confidence
```

## Tests

Required:

1. selected serial disappears + matching DFU appears;
2. matching DFU delayed;
3. unrelated pre-existing DFU ignored;
4. matching + unrelated DFU -> correct one chosen;
5. two indistinguishable new DFUs -> ambiguity failure;
6. serial never disappears;
7. no DFU;
8. cancellation;
9. complete device removal;
10. topology mismatch;
11. all monitor resources disposed.

## Acceptance criteria

With multiple USB flight controllers/DFU-capable devices attached, MissionPlanner either:

- correlates the selected Betaflight FC to one DFU endpoint with explicit evidence; or
- refuses to flash because correlation is ambiguous.

It never guesses.

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

<!-- END 05_DFU_HANDOFF_AND_DEVICE_CORRELATION.md -->


---

<!-- BEGIN 06_BETAFLIGHT_TO_ARDUPILOT_CONVERSION_WORKFLOW.md -->

# Task 06 — Betaflight-to-ArduPilot Conversion Workflow

## Objective

Compose Tasks 01–05 with MissionPlanner's existing ArduPilot firmware catalog/selection and DFU installer into one safe conversion workflow.

This task orchestrates existing components; it must not duplicate MSP framing, USB DFU programming, artifact download, or firmware catalog code.

## Service boundary

Use current firmware work-item/orchestration patterns. If a new focused service is appropriate, introduce an equivalent of:

```csharp
public interface IBetaflightToArduPilotConversionService
{
    Task<BetaflightConversionResult> ConvertAsync(
        BetaflightConversionRequest request,
        IProgress<...>? progress,
        CancellationToken cancellationToken);
}
```

## Required phases

### 1. Revalidate Betaflight source

- selected serial descriptor still exists;
- positive `BTFL` identity still holds;
- capture immutable source identity snapshot;
- stop if runtime changed.

### 2. Resolve/select ArduPilot target

Reuse current:

```text
IArduPilotFirmwareCatalog
IArduPilotFirmwareSelectionService
```

and current firmware artifact model.

### 3. Validate compatibility

Require either:

- explicit operator target plus compatibility validation; or
- an exact, high-confidence data-driven mapping.

### 4. Enter ROM DFU

Use existing `BootloaderEntryService` plus Task 04's strategy.

### 5. Correlate DFU

Use Task 05/current matching infrastructure.

### 6. Install ArduPilot

Use existing MissionPlanner DFU/firmware installer.

Do not invoke `dfu-util`, CubeProgrammer, or libusb directly from this orchestration service.

### 7. Wait for normal USB/serial return

Use existing device-monitoring mechanisms.

### 8. Verify ArduPilot

Use MissionPlanner's current connected-firmware identity mechanisms to verify the returned device is running ArduPilot.

A completed DFU write without runtime verification is not full conversion success.

## Compatibility provider

Create a narrow typed policy boundary, e.g.:

```csharp
public interface IBetaflightArduPilotCompatibilityProvider
{
    BetaflightArduPilotCompatibility Resolve(BetaflightDeviceInfo device);
}
```

Exact names may follow current architecture.

A mapping should use sufficiently specific identity, potentially including:

- Betaflight board identifier;
- target/board name;
- manufacturer ID;
- board signature/revision;
- MCU type as supporting evidence.

It resolves to one ArduPilot target.

## Forbidden mapping

Never implement:

```text
STM32F405 -> omnibusf4
F4 -> omnibusf4
Betaflight -> omnibusf4
```

MCU family does not define pin mapping/peripherals/flash layout.

## Known `omnibusf4` test hardware

The operator already has compatible 5-inch FCs converted using ArduPilot `omnibusf4`.

Treat this as a manual acceptance case.

Before adding an automatic mapping:

1. capture actual MSP board identity from one such FC;
2. prove that exact board identity corresponds to the known compatible ArduPilot target;
3. add a narrow reviewed mapping;
4. test another physical unit of the same board type.

Do not generalize beyond that identity.

## Pavo 20 rule

For the BetaFPV Pavo 20:

- identity discovery is allowed;
- DFU reboot/correlation is allowed;
- ArduPilot flash is blocked unless exact target compatibility is independently established.

Do not infer compatibility from “Pavo 20”, BetaFPV, or MCU alone.

## Mapping provenance

Keep mapping entries reviewable.

Retain:

```text
source Betaflight identity key
ArduPilot target
board/revision constraints
reason/provenance
verification note/date where useful
```

Do not hide mapping in ViewModel conditionals.

## Configuration backup

Before destructive conversion, require a clear backup step/acknowledgement.

Initial implementation may require confirmation that Betaflight config was backed up externally if MissionPlanner has no suitable export function.

Do not expand scope into a full Betaflight configuration backup/restore subsystem.

## Fail-closed boundaries

Stop before programming when:

- Betaflight identity uncertain;
- compatibility unsupported/ambiguous;
- selected artifact doesn't match target;
- DFU correlation ambiguous;
- source physical device changes;
- cancellation requested;
- work-item ownership conflicts.

## Progress model

Expose current-equivalent phases:

```text
IdentifyingBetaflight
ResolvingTarget
ValidatingCompatibility
EnteringDfu
WaitingForDfu
DownloadingFirmware
Programming
VerifyingFlash
WaitingForSerial
VerifyingArduPilot
Completed
Failed
Cancelled
```

Reuse current progress abstractions.

## Conversion receipt/log

Record:

### Source
- COM/USB identity;
- Betaflight/API version;
- board/target/manufacturer;
- MCU/UID.

### Target
- ArduPilot target;
- firmware version/channel;
- artifact identifier;
- existing checksum/integrity data.

### Transition
- bootloader strategy;
- correlated DFU evidence;
- DFU backend.

### Result
- programming result;
- returned serial device;
- verified ArduPilot identity/version;
- failure phase if any.

## Tests

Required:

1. successful fake Betaflight -> DFU -> ArduPilot;
2. unsupported board stops before reboot;
3. incompatible manually selected target stops;
4. ambiguous mapping stops;
5. reboot failure -> no installer;
6. DFU ambiguity -> no installer;
7. artifact/download failure;
8. DFU programming failure;
9. no post-flash serial;
10. serial returns but not ArduPilot;
11. cancellation at destructive boundaries;
12. receipt contains source/target/correlation evidence;
13. exact board mapping never matches a merely similar MCU.

## Acceptance criteria

A known compatible physical FC can complete:

```text
BTFL identity
-> compatible target
-> ROM DFU
-> correlated DFU
-> existing ArduPilot firmware selection
-> existing DFU installer
-> serial return
-> ArduPilot verified
```

Unsupported/ambiguous boards stop before destructive flashing.

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

<!-- END 06_BETAFLIGHT_TO_ARDUPILOT_CONVERSION_WORKFLOW.md -->


---

<!-- BEGIN 07_FIRMWARE_UI_AND_OPERATOR_SAFETY.md -->

# Task 07 — Firmware UI Integration and Operator Safety

## Objective

Expose Betaflight identity, `Reboot to DFU`, and safe ArduPilot conversion through the existing Install Firmware experience.

Do not create a separate mini Betaflight Configurator.

## Current UI anchors

Read current:

```text
docs/INSTALL_FIRMWARE_VIEWMODELS.md
```

Current documented anchors include:

```text
FirmwareShellViewModel
FirmwareLandingViewModel
FirmwareDetailsViewModel
FirmwareProgressViewModel
```

under the MissionPlanner firmware feature.

Use their current locations and navigation flow.

## Betaflight identity card

When positive Betaflight identity exists, display available fields such as:

```text
Port
Running firmware: Betaflight
Betaflight version
MSP API
Manufacturer ID / friendly manufacturer
Board identifier
Board name
Target name
MCU
MCU UID (abbreviated)
Build/revision
```

Keep OS USB product, e.g. `STM Device`, as secondary diagnostic information.

## Diagnostics area

Provide a collapsible/secondary diagnostics presentation for:

- full UID;
- VID/PID;
- USB topology;
- raw Betaflight identifiers;
- capability flags;
- probe errors/status;
- compatibility-mapping evidence.

## Actions

### Reboot to DFU

Enable only when:

- positive/current Betaflight identity;
- serial port not owned elsewhere;
- bootloader strategy can handle it;
- safety conditions satisfied.

This is useful independently of conversion.

### Install ArduPilot...

Enable only when the conversion workflow can enter target selection/validation.

Continue through the existing firmware landing/details/progress flow.

Do not duplicate the ArduPilot catalog.

## Destructive confirmation

Before conversion begins, show:

- exact detected Betaflight board;
- selected ArduPilot target;
- firmware version/channel;
- warning that Betaflight firmware/configuration will be replaced;
- **propellers must be removed**;
- Betaflight configuration backup reminder/requirement;
- physical BOOT/STM32 ROM DFU recovery note.

Do not use only a generic `Are you sure?`.

## Target presentation

For exact high-confidence mapping:

- show the selected target;
- show why it matched.

For manual selection:

- show source board identity alongside candidate;
- do not preselect based on MCU family;
- require explicit confirmation.

For unsupported board:

- show unsupported;
- do not offer an unsafe generic flash.

## Progress

Use current `FirmwareProgressViewModel`/equivalent and show meaningful stages:

```text
Identifying Betaflight
Validating ArduPilot target
Requesting STM32 ROM DFU
Waiting for selected FC
DFU device matched
Downloading
Programming
Verifying flash
Waiting for flight controller
Verifying ArduPilot
Complete
```

Failures retain phase + actionable diagnostic.

## Already-in-DFU behavior

Preserve current DFU-only startup/selection.

If MissionPlanner starts while a board is already in ROM DFU, do not claim it is a particular Betaflight board unless reliable cached/work-item correlation proves that identity.

## Multiple FCs

The UI must make the selected device clear throughout transition.

If COM11 and COM12 exist and COM11 is selected, all reboot/progress/correlation state must remain associated with COM11's physical identity.

If correlation becomes ambiguous, stop and show that ambiguity.

## Explicit scope exclusion

Do not add:

- Betaflight OSD;
- MSP DisplayPort;
- DJI Air Unit settings;
- VTX tables;
- PID/rates/configurator features.

The Pavo 20 is later useful for OSD work, but this task set ends at identity/DFU/conversion.

## Tests

Required ViewModel/UI-level tests:

1. Betaflight identity card visible;
2. non-Betaflight does not show Betaflight actions;
3. reboot enablement state;
4. unsupported conversion blocked;
5. exact mapped target shown;
6. confirmation includes source + target + props warning;
7. progress phase updates;
8. cancellation/error;
9. ambiguous DFU state;
10. existing ArduPilot firmware install flow still works.

## Acceptance criteria

A selected Betaflight FC is presented by its actual MSP-derived identity, not merely `STM Device`.

The operator can reboot it to DFU and, if exact compatibility is established, enter the existing ArduPilot firmware selection/install workflow safely.

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

<!-- END 07_FIRMWARE_UI_AND_OPERATOR_SAFETY.md -->


---

<!-- BEGIN 08_TESTS_HARDWARE_ACCEPTANCE_AND_DOCS.md -->

# Task 08 — Automated Regression, Physical Hardware Acceptance, and Documentation

## Objective

Validate Tasks 01–07 together on real hardware, close remaining automated-test gaps, and document the architecture and recovery procedure.

Unit tests alone are not sufficient for this task.

## A. Automated coverage gate

Ensure coverage for all of the following.

### MSP
- exact v1 vectors;
- framing/checksum;
- fragmented and multiple frames;
- noise/resync;
- malformed input;
- MSP error;
- timeout/cancellation/disconnect;
- v2 where implemented.

### Betaflight identity
- exact `BTFL`;
- other MSP firmware variant;
- non-MSP serial;
- API/version;
- BOARD_INFO current/older/extended;
- UID;
- optional commands.

### Discovery
- enrichment;
- active-port avoidance;
- cache/revalidation;
- COM reuse;
- no ArduPilot/PX4/DFU regression.

### Bootloader entry
- exact ROM reboot mode;
- ACK;
- disconnect-after-write race;
- armed rejection;
- strategy ordering;
- disposal.

### DFU correlation
- matching endpoint;
- unrelated pre-existing DFU;
- multiple candidates;
- ambiguity;
- topology mismatch;
- delay;
- no DFU;
- cancellation.

### Conversion
- exact compatible mapping;
- unsupported board;
- wrong target;
- reboot failure;
- DFU ambiguity;
- download/program failure;
- post-flash timeout;
- post-flash non-ArduPilot;
- successful verification;
- conversion receipt.

### UI
- identity/actions;
- safety/target confirmation;
- progress;
- error;
- existing ArduPilot flow regression.

## B. Physical acceptance — first 5-inch FC

Use a physical 5-inch Betaflight FC already known through prior manual work to accept ArduPilot `omnibusf4`.

### Preparation

- remove propellers;
- back up Betaflight configuration through a trusted method;
- record connected USB port;
- ensure correct board is selected.

### Sequence

1. Boot normal Betaflight.
2. Select FC in MissionPlanner.
3. Capture:
   - `BTFL`;
   - Betaflight version;
   - MSP API;
   - manufacturer ID;
   - board identifier;
   - target name;
   - board name;
   - MCU;
   - UID;
   - build/revision.
4. Verify MissionPlanner shows Betaflight identity instead of only `STM Device`.
5. Create/verify any `omnibusf4` compatibility mapping only from this exact observed board identity.
6. Choose `Reboot to DFU`.
7. Verify COM disappears without pressing BOOT.
8. Verify STM32 ROM DFU enumerates (`0483:DF11` where platform exposes it).
9. Verify MissionPlanner correlates that DFU endpoint with the selected FC.
10. Confirm the correct ArduPilot target `omnibusf4`.
11. Flash through MissionPlanner's existing DFU installer.
12. Verify normal USB/serial returns.
13. Verify MissionPlanner identifies ArduPilot.
14. Connect as a vehicle.
15. Exercise:
    - Parameters read/write;
    - Radio;
    - Motors with props removed;
    - external buzzer;
    - external GPS.
16. Record board-specific observations.

## C. Second 5-inch FC

Repeat identity, reboot, correlation, and conversion on a second physical unit.

Purpose:

- catch hard-coded COM assumptions;
- catch hard-coded USB-path assumptions;
- confirm otherwise identical boards remain distinct physical devices;
- validate different MCU UID/device identities.

## D. BetaFPV Pavo 20 acceptance

Use the Pavo 20 with DJI Air Unit.

1. Start in Betaflight.
2. Capture full MSP identity.
3. Verify board/manufacturer/MCU parsing.
4. Software `Reboot to DFU`.
5. Verify STM32 ROM DFU enumeration.
6. Verify selected-device correlation.
7. **Do not flash ArduPilot unless exact compatibility is independently established.**
8. Return/recover the Pavo to its correct Betaflight firmware through a trusted recovery method if needed.
9. Confirm DJI Air Unit wiring is irrelevant to the identity/DFU logic.

This Pavo then becomes a useful later fixture for OSD work.

## E. Multi-device safety

Where practical:

```text
FC A -> COM11 -> selected
FC B -> COM12 -> untouched
optional DFU X -> pre-existing
```

Verify:

- FC A remains selected across reboot;
- FC B is untouched;
- pre-existing DFU is ignored;
- ambiguity stops the flash.

## F. Documentation

Update:

```text
docs/INSTALL_FIRMWARE_VIEWMODELS.md
```

Add a focused document, e.g.:

```text
docs/BETAFLIGHT_DFU_AND_ARDUPILOT_CONVERSION.md
```

Document:

1. MSP architecture;
2. commands used;
3. identity model;
4. serial probing policy;
5. ROM-DFU reboot;
6. serial-to-DFU correlation;
7. compatibility mapping policy;
8. conversion phases;
9. recovery via physical BOOT;
10. how to add a reviewed board mapping;
11. verified physical hardware identities;
12. limitations and deferred Betaflight flashing.

Do not document an unverified Pavo-to-ArduPilot mapping.

## G. Build/test report

Run current appropriate project/solution build and tests.

Completion report must list exact commands and results.

## Final acceptance criteria

Accepted when:

- real Betaflight identity is reliable;
- one known-compatible 5-inch FC completes Betaflight -> DFU -> ArduPilot;
- a second unit validates physical-device isolation;
- Pavo 20 validates Betaflight identity + software DFU without unsafe target assumptions;
- multiple-device ambiguity is handled safely;
- current ArduPilot firmware installation has no regression;
- architecture/recovery is documented.

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

<!-- END 08_TESTS_HARDWARE_ACCEPTANCE_AND_DOCS.md -->


---

<!-- BEGIN 09_DEFERRED_BETAFLIGHT_FIRMWARE_FLASHING.md -->

# Deferred Task — Installing/Updating Betaflight Firmware from MissionPlanner

## Status

**Do not implement in the initial task set.**

Tasks 01–08 solve the current need:

```text
identify Betaflight
-> enter STM32 ROM DFU
-> safely convert compatible FC to ArduPilot
```

## Why Betaflight installation is separate

A reliable Betaflight installer needs policy beyond raw DFU programming:

- authoritative firmware/artifact source;
- current Betaflight target/config system;
- Cloud Build/API behavior where applicable;
- target/manufacturer/board revision matching;
- firmware channels/versions;
- artifact integrity;
- flash layout/address;
- configuration backup/migration;
- custom defines/options;
- deprecated targets;
- recovery;
- post-flash MSP verification.

MissionPlanner should not accidentally become an incomplete Betaflight Configurator.

## Reusable work from Tasks 01–08

Future Betaflight flashing can reuse:

- MSP parser/client;
- Betaflight identity;
- board/MCU/UID data;
- MSP ROM-DFU reboot;
- serial-to-DFU correlation;
- existing MissionPlanner DFU backends;
- progress UI;
- post-flash MSP verification concepts.

## Proposed later task set

1. Betaflight artifact/catalog investigation.
2. Exact board-target resolver.
3. Integrity/flash-address validation.
4. configuration backup/export strategy.
5. installer orchestration using existing DFU stack.
6. post-flash `BTFL` verification.
7. UI/version/channel selection.
8. physical hardware/recovery matrix.

## Separate future OSD work

Betaflight OSD/MSP DisplayPort/DJI Air Unit support is also a separate feature.

The Pavo 20 is useful test hardware for both, but firmware identity/DFU and OSD configuration should remain separate subsystems.

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

<!-- END 09_DEFERRED_BETAFLIGHT_FIRMWARE_FLASHING.md -->


---

<!-- BEGIN REFERENCES.md -->

# References

Verified while preparing this task set on **2026-09-08**.

## MissionPlanner

Repository:

```text
karlgodtliebsen/MissionPlanner
branch: main
```

Current anchors to re-check before implementation:

```text
src/Core/MissionPlanner.Firmware/Configuration/FirmwareConfigurator.cs
src/Core/MissionPlanner.Firmware/FirmwareFamily.cs
src/Core/MissionPlanner.Firmware/Entry/
src/Core/MissionPlanner.Firmware/Discovery/
src/Core/MissionPlanner.Firmware/Devices/
src/Core/MissionPlanner.Firmware/Connected/
src/Core/MissionPlanner.Firmware/Dfu/
src/Core/MissionPlanner.Firmware/Installation/
docs/INSTALL_FIRMWARE_VIEWMODELS.md
```

The current firmware subsystem already contains STM32 DFU discovery/monitoring/backends, bootloader-entry strategies, ArduPilot firmware selection, installation and recovery. Reuse them.

## Betaflight upstream

MSP implementation:

```text
https://github.com/betaflight/betaflight/blob/master/src/main/msp/msp.c
```

MSP protocol definitions:

```text
https://github.com/betaflight/betaflight/blob/master/src/main/msp/msp_protocol.h
```

MSP documentation:

```text
https://betaflight.com/docs/development/API/MSP-Extensions
```

USB/DFU flashing:

```text
https://betaflight.com/docs/wiki/guides/current/USB-Flashing
```

Current implementation facts to verify again when coding:

- Betaflight identifies itself via `MSP_FC_VARIANT` as `BTFL`.
- `MSP_REBOOT` supports reboot to the MCU ROM bootloader.
- current ROM-bootloader mode is `1`;
- Betaflight's current implementation calls `systemResetToBootloader(BOOTLOADER_REQUEST_ROM)`;
- reboot processing is protected against the armed state;
- STM32 ROM DFU normally enumerates as ST `0483:DF11`.

If current upstream source disagrees with a copied numeric value in these tasks, **current upstream source wins**.

Use named constants/enums and exact protocol tests so such changes remain localized.

<!-- END REFERENCES.md -->
