# Task 05 — End-to-End Regression Tests and Firmware Identification Guardrails

## Objective

Consolidate regression coverage for Tasks 01–04. This task is validation/hardening only: no UI redesign and no opportunistic refactoring.

## Required test matrix

### Runtime identification

**R1 — Existing MissionPlanner session, ArduPilot**

Same selected endpoint is already owned by a vehicle session whose heartbeat reports `MAV_AUTOPILOT_ARDUPILOTMEGA`. Verify ArduPilot/verified/existing-session evidence, no serial reopen, and exact board still unresolved unless separately authoritative.

**R2 — Free COM port, ArduPilot**

Bounded MAVLink probe receives valid ArduPilot heartbeat. Verify runtime ArduPilot, operating mode Application, exact board unresolved, resources released.

**R3 — USB says ArduPilot, protocol does not prove it**

Friendly name contains `ArduPilot` but no valid ArduPilot protocol identity is obtained. Verify friendly name alone does not create verified runtime or exact-board identity.

**R4 — Non-ArduPilot MAVLink endpoint**

Valid MAVLink heartbeat reports another autopilot. Verify no false ArduPilot classification and typed result is accurate.

**R5 — Betaflight fallback**

ArduPilot not proven; MSP reports `FC_VARIANT == BTFL`. Verify Betaflight and existing behavior.

**R6 — Unknown endpoint**

Neither protocol established. Verify Unknown, meaningful typed outcome, no exact-board claim.

**R7 — Port ownership**

Verify matching MissionPlanner session is reused and unrelated/busy port is not forcibly taken.

**R8 — Probe serialization**

Verify MAVLink and MSP probes cannot execute concurrently on one serial endpoint.

**R9 — Cancellation/timeout**

Verify finite timeout, cancellation, serial cleanup, and stale result cannot apply to a newer device selection.

### Hardware-target safety

**H1 — Runtime is not exact hardware**

Verified ArduPilot runtime with no bootloader identity must permit:

```text
Runtime = ArduPilot
Exact board = unresolved
```

**H2 — Shared/generic USB identity**

USB VID/PID/friendly name that can represent multiple boards must not independently authorize flashing.

**H3 — Bootloader identity overrides hints**

USB suggests board A but bootloader reports board B. Verify B is authoritative and A cannot override it.

### Install workflow

**I1 — Application → bootloader → matching APJ**

Verify ordered sequence: bootloader entry request, discovery, board ID read, compatibility check, then erase/write only after successful compatibility.

**I2 — Existing bootloader**

No unnecessary application reboot; identity/compatibility checked; matching firmware may continue.

**I3 — Board mismatch**

Verify installation fails and neither erase nor write/program is called.

**I4 — Missing authoritative board identity**

Verify blocked before erase/write.

**I5 — Bootloader discovery timeout**

Verify bounded failure, no flash, cleanup.

**I6 — Cancel during handoff**

Verify stop/cleanup and no delayed flash after cancellation.

## Regression coverage

Run/add tests ensuring no regression to:

- Betaflight/MSP identification and covered firmware workflow;
- STM32 ROM DFU identification/recovery;
- existing manual ArduPilot bootloader-entry service;
- online firmware catalog/filtering;
- local firmware selection;
- strict APJ board compatibility;
- InstallFirmware refresh/device-selection behavior.

Do not weaken existing tests to make the new implementation pass.

## Build and validation

Run relevant narrow tests during development, then the normal repository validation for affected projects. Report actual `dotnet build ...` and `dotnet test ...` commands and results. If platform-specific projects cannot build in the current environment, say so precisely and run all applicable platform-independent checks.

## Protected UI regression rule

Before finishing:

1. inspect diffs for firmware view files;
2. verify no icon-button definition/resource/style/template/binding changed;
3. verify no broad XAML formatting churn;
4. revert any protected icon-button change before completion.

Do not add snapshot/golden tests that freeze in-progress toolbar appearance unless already required by repository conventions.

## Final acceptance criteria

- [ ] ArduPilot runtime detection is covered.
- [ ] Existing-session reuse is covered.
- [ ] Betaflight fallback is covered.
- [ ] Unknown/busy outcomes are covered.
- [ ] MAVLink/MSP serialization is covered.
- [ ] USB hints cannot become exact-board authorization.
- [ ] Automatic bootloader handoff is covered.
- [ ] Matching board proceeds only after compatibility validation.
- [ ] Board mismatch blocks erase/write.
- [ ] Missing board identity blocks erase/write.
- [ ] Timeout/cancellation/resource cleanup is covered.
- [ ] Existing Betaflight and DFU paths remain covered/passing.
- [ ] Relevant builds/tests pass.
- [ ] No icon-button definitions/resources/styles/bindings were modified.
- [ ] No unrelated UI changes were introduced.
