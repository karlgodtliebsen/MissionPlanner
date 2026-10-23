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
