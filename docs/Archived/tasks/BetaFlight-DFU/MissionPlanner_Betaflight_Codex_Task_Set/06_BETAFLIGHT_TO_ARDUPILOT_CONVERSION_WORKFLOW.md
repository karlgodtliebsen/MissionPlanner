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
