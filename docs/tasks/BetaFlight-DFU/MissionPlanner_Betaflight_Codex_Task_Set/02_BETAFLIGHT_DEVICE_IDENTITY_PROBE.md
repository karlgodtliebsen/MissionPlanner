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
