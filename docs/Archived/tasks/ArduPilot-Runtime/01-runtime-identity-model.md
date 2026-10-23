# Task 01 — Separate Runtime Identity from Hardware Target Identity

## Objective

Refine the firmware-installation state so `InstallFirmware` can represent independently:

- an identified application runtime such as ArduPilot or Betaflight;
- the evidence used to identify that runtime;
- the controller's current operating/boot mode;
- an exact hardware target that may still be unknown.

**Runtime identity and exact board identity must not be represented as the same fact.**

This is a model/state task. Do not implement the MAVLink serial probe yet.

## Required investigation

Before editing code, locate existing `InstallFirmware` state/view-model types for device identity, runtime/protocol identity, boot environment, hardware target/board identity, confidence/evidence and Betaflight detection. Reuse or extend existing abstractions rather than creating duplicates.

## Required behavior

The model must support at least:

### ArduPilot application state
```text
Runtime:              ArduPilot
Runtime verification: Verified
Runtime evidence:     MAVLink or existing vehicle session
Operating mode:       Application
Exact board:          Unknown / not yet verified
```

### Betaflight application state
```text
Runtime:              Betaflight
Runtime verification: Verified
Runtime evidence:     MSP
Operating mode:       Application
Exact board:          May be unknown
```

### ArduPilot bootloader state
```text
Runtime:              None/not-applicable
Operating mode:       ArduPilotBootloader
Exact board:          Available if bootloader board ID was read successfully
```

### STM32 ROM DFU state
```text
Runtime:              None/not-applicable
Operating mode:       Stm32RomDfu
Exact board:          Unknown unless separately established authoritatively
```

### Unknown state
```text
Runtime:              Unknown
Operating mode:       Unknown or best-known current mode
Exact board:          Unknown
Probe outcome:        Typed/diagnostic reason where available
```

Suggested concepts, adapted to repository naming conventions:

```csharp
enum FirmwareRuntimeKind { Unknown, ArduPilot, Betaflight, Other }
enum FirmwareOperatingMode { Unknown, Application, ArduPilotBootloader, Stm32RomDfu }
enum FirmwareRuntimeEvidence { None, UsbHint, ExistingVehicleSession, MavLinkProbe, MspProbe }
```

A runtime result should carry runtime kind, operating mode where known, evidence/source, verification strength, optional existing protocol/version detail, and typed probe outcome/failure detail. Do not force firmware-version retrieval into this task.

## Identity invariants

1. `Runtime == ArduPilot` does not imply an exact FC board.
2. USB product/friendly name `ArduPilot` does not imply exact board.
3. USB VID/PID does not imply exact board compatibility.
4. Runtime may be verified while exact hardware is unresolved.
5. Bootloader board identity is separate from application-runtime identity.
6. Existing firmware compatibility code remains authoritative for bootloader-board vs package comparison.

## UI restrictions

- Prefer zero XAML/view changes.
- Do not modify any icon button, icon-button binding/style/resource or toolbar composition.
- Preserve existing bindings where possible by mapping the refined state behind them.

## Tests

At minimum verify:

1. verified ArduPilot runtime can coexist with unknown exact board;
2. verified Betaflight runtime can coexist with unknown exact board;
3. a USB hint cannot become verified exact-board identity;
4. ArduPilot bootloader mode can carry exact board independently of runtime;
5. DFU mode does not inherit an application runtime accidentally;
6. unknown runtime can retain a typed diagnostic/probe outcome.

## Acceptance criteria

- [ ] Runtime identity is separate from exact hardware target identity.
- [ ] Operating/boot mode is independent.
- [ ] Evidence/verification level is explicit enough for later probing.
- [ ] USB metadata alone cannot produce authoritative exact-board identity.
- [ ] Existing Betaflight state remains representable.
- [ ] Existing bootloader compatibility behavior is preserved.
- [ ] Relevant tests pass and affected projects build.
- [ ] No icon-button declarations/resources/styles/bindings were modified.
- [ ] No unrelated UI formatting/refactoring was performed.
