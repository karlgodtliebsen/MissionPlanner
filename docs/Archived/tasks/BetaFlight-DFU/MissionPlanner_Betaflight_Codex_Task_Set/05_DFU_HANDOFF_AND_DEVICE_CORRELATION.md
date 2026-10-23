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
