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
