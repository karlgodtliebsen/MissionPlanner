# MissionPlanner Firmware — Codex Work Package Index

## Source snapshot

This work package is based on the uploaded source archive:

```text
MissionPlanner-202600804-v1.zip
```

The audit covers the current implementation in:

```text
src/Core/MissionPlanner.Firmware
src/Tests/MissionPlanner.Firmware.Tests
src/UI/AvaloniaUI/MissionPlanner.AvaloniaUI.App/Views/InitSetup/InstallFirmware
```

It also reviews the existing firmware planning documents under:

```text
docs/tasks/firmware
```

## Verification limitation

The source was inspected statically. The current environment did not contain a .NET SDK, so the recorded repository build and test results could not be independently rerun. Any pass counts quoted in the audit are clearly marked as repository-reported rather than independently verified.

## Executive direction

The existing serial/APJ firmware implementation is substantial and well advanced. Do not replace it. Harden and complete it.

The next work should proceed in this order:

1. Correct current firmware workflow defects and add an explicit **Download & Validate** operation.
2. Replace the weak vehicle-picture/first-item selection model with an explicit hardware-target selection model.
3. Add embedded firmware documentation, support and recovery guidance.
4. Introduce DFU as a separate advanced/recovery workflow.
5. Use STM32CubeProgrammer CLI as the first DFU execution provider rather than immediately implementing USB DFU from scratch.
6. Add Windows DFU and driver diagnostics.
7. Preserve native DFU, ST-LINK and secure-programming work as later phases.

## Files in this package

### 01 — Current implementation audit

`01-Firmware-Current-State-Audit.md`

Static assessment of the existing tasks, source structure, implemented functionality, defects and priority gaps.

### 02 — User download test protocol

`02-Firmware-Download-User-Test-Protocol.md`

A safe protocol for testing the current firmware catalogue/download implementation, followed by the preferred protocol after adding Download & Validate.

### 03 — Download and target-selection tasks

`03-Firmware-Download-And-Selection-Improvements-Codex-Tasks.md`

Codex tasks for target search, explicit board selection, download-only validation, typed device selection, persistent caching and current workflow corrections.

### 04 — Documentation and support UI

`04-Firmware-Documentation-And-Support-UX-Codex-Task.md`

Codex task for an embedded documentation/support area with official ArduPilot/ST links, file-type explanations, platform limitations and recovery instructions.

### 05 — STM32CubeProgrammer and DFU architecture

`05-STM32CubeProgrammer-And-DFU-Architecture.md`

Analysis of STM32CubeProgrammer functionality, what MissionPlanner should adopt, what should remain external, and the proposed provider architecture.

### 06 — DFU implementation tasks

`06-DFU-Implementation-Codex-Tasks.md`

A staged Codex plan for Intel HEX inspection, Windows DFU detection, STM32CubeProgrammer CLI integration, target safety, orchestration, UI, tests and hardware validation.

### 07 — Windows DFU driver diagnostics

`07-Windows-DFU-Driver-Diagnostics-Codex-Task.md`

Codex task for Device Manager guidance, DFU presence/driver state, STM32CubeProgrammer detection and carefully scoped Zadig/ImpulseRC fallback guidance.

### 08 — Updated roadmap

`08-Firmware-Roadmap-Update.md`

Current scope, immediate hardening, DFU phase one, later native providers and explicitly deferred capabilities.

## Codex execution rules

For every task:

1. Read `ai.md`, `docs/DesignConcepts.md`, `docs/FIRMWARE.md` and the existing firmware task documents before editing.
2. Inspect current implementations before introducing new abstractions.
3. Do not duplicate existing domain, transport, logging, time or device abstractions without a documented reason.
4. Keep `MissionPlanner.Firmware` free of Avalonia, Ursa and WinUI dependencies.
5. Keep serial bootloader installation and USB DFU installation as separate workflows.
6. Build the affected projects and run focused tests after every coherent change.
7. Run the complete firmware test project before completing each task.
8. Record any baseline failure without suppressing or reclassifying it.
9. Never weaken board-ID, image-size or verification checks to make a test pass.
10. Never perform an erase before the selected hardware target and firmware artifact have been explicitly confirmed.
11. Do not implement arbitrary delays as protocol synchronization unless the delay is defined by the vendor protocol and represented by named options.
12. Keep one task or tightly related task group per branch/PR.

## User-experience principle

The original Mission Planner’s frame pictures are not required for safe firmware selection. Frame geometry such as Quad-X, Plus, Hexa or Octa is generally configured after firmware installation and does not identify the flight-controller hardware target.

The new firmware UI should prioritize:

```text
Vehicle family
Hardware platform/board target
Manufacturer/brand
Release channel
Version and Git identity
Firmware file type
Detected USB/bootloader evidence
```

Vehicle-family icons may be retained as optional visual aids, but they must never replace explicit platform selection and compatibility evidence.
