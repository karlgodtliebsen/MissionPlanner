# Codex Task 1 — Safe Board-ID Override for Local / Custom Firmware

## Goal

Add an explicit **advanced override** that permits a deliberately selected local/custom ArduPilot APJ/PX4 application firmware package to be installed through the ArduPilot serial bootloader even when the firmware package board ID does not exactly match the bootloader-reported board ID.

The normal behavior must remain **strict and fail-closed**.

This is intended for expert use with known-compatible custom builds, for example a deliberately built `omnibusf4` image used on compatible hardware that MissionPlanner would otherwise reject solely because of board identity.

The feature is **not** permission to bypass other compatibility or safety checks.

## Current source snapshot

The current UI class is:

```text
src/UI/AvaloniaUI/MissionPlanner.AvaloniaUI.App/Views/InitSetup/InstallFirmware/InstallFirmwareViewModel.cs
```

The user may refer to this area as `FirmwareInstallation/FirmwareInstallationModel`; follow the current names in the branch being edited.

Local application firmware is selected by:

```text
LoadCustomFirmwareAsync
```

which accepts `.apj` and `.px4`, parses the file, and stores it in:

```text
CustomPackage
```

The installation request is created in:

```text
InstallAsync
```

Board identity is currently enforced in at least two independent places:

```text
src/Core/MissionPlanner.Firmware/Compatibility/FirmwareCompatibilityService.cs
src/Core/MissionPlanner.Firmware/Protocol/ArduPilotBootloaderClient.cs
```

Specifically:

- `FirmwareCompatibilityService.Check(...)` blocks `compatibility.board-id-mismatch`.
- `ArduPilotBootloaderClient.ValidatePackage(...)` repeats the board-ID check before both program and verify.
- Both currently retain the historical board `33` / firmware `9` compatibility exception.
- Therefore changing only the ViewModel or only `FirmwareCompatibilityService` is insufficient.

Also inspect:

```text
src/Core/MissionPlanner.Firmware/Installation/FirmwareInstallationRequest.cs
src/Core/MissionPlanner.Firmware/Installation/FirmwareInstallationService.cs
src/Core/MissionPlanner.Firmware/Installation/FirmwareInstallationConfirmation.cs
src/UI/AvaloniaUI/MissionPlanner.AvaloniaUI.App/Views/InitSetup/InstallFirmware/FirmwareInteractionService.cs
src/UI/AvaloniaUI/MissionPlanner.AvaloniaUI.App/Views/InitSetup/InstallFirmware/InstallFirmwareView.axaml
src/Core/MissionPlanner.Firmware/Preparation/FirmwarePreparationService.cs
src/Tests/MissionPlanner.Firmware.Tests/FirmwareCompatibilityServiceTests.cs
src/Tests/MissionPlanner.Firmware.Tests/FirmwareInstallationServiceTests.cs
src/Tests/MissionPlanner.Firmware.Tests/ArduPilotBootloaderClientTests.cs
```

`src-v.1.38` may be inspected for historical behavior but must not be edited.

---

## Required behavior

### 1. Strict is the default

Add a ViewModel/UI option with wording similar to:

```text
Require exact board ID match
```

Default:

```text
true
```

The option should be visible only in the **Local / custom firmware** application-firmware workflow.

Do not expose this override for normal firmware selected from the official catalogue.

Do not persist the unsafe state as an application preference.

When a new local/custom APJ/PX4 is selected, reset the option to strict.

When the custom package is cleared, reset the option to strict.

A user must therefore deliberately disable the check for the specific custom firmware selection.

### 2. Make the unsafe state visually obvious

When strict board-ID matching is disabled, show a prominent warning beside the local/custom package information.

Suggested text:

```text
Advanced override: board-ID mismatch will be allowed for this local firmware only.
Use this only when you know the firmware target is electrically and bootloader compatible
with this controller. Flash-size, revision, bootloader and secure-boot checks still apply.
```

Do not call this simply "ignore compatibility".

### 3. Override only the exact board-ID mismatch rule

When the override is active for a local/custom package, allow:

```text
firmware.BoardId != bootloader.BoardId
```

but continue to enforce all other existing checks, including:

- minimum board revision;
- maximum board revision;
- internal application image size versus flash capacity;
- external image size versus external flash capacity;
- minimum bootloader revision;
- secure-boot requirements;
- signed-image requirements;
- all parsing/package integrity checks;
- verification after programming.

The existing board `33` / firmware `9` compatibility behavior must remain intact unless existing tests/documentation prove it should be changed.

### 4. Preserve defense in depth

Do not simply remove the check from `ArduPilotBootloaderClient.ValidatePackage`.

The serial bootloader client currently performs its own validation before `ProgramAsync` and `VerifyAsync`; retain that protection.

Introduce one explicit typed compatibility/programming policy or options object that can be passed through the installation workflow and used by both the higher-level compatibility decision and the low-level bootloader validation.

Avoid ambiguous signatures such as:

```csharp
ProgramAsync(package, true, ...)
```

Prefer a named immutable policy/options type.

Example direction only:

```csharp
public sealed record FirmwareCompatibilityPolicy(
    bool AllowBoardIdMismatch = false);
```

Choose the final namespace/name according to the existing architecture.

### 5. Track source/provenance explicitly

Do not infer "custom" solely from `FirmwareInstallationRequest.Artifact is null`.

In the current ViewModel, a previously downloaded/validated catalogue package may also be supplied directly as `Package`, so `Package != null` does not necessarily mean local/custom firmware.

Add or preserve explicit installation provenance sufficient to distinguish at least:

```text
Official/catalogue firmware
Local/custom firmware
```

The board-ID override must be honored only for the local/custom source.

If an official/catalogue request somehow asks to allow a board-ID mismatch, fail closed or ignore the override and remain strict.

This is also a good opportunity to ensure the diagnostic source is not incorrectly reported as `"custom"` for a prepared official catalogue package.

### 6. Strong confirmation when an actual mismatch is being overridden

Normal matching-board installs may continue to use the existing installation confirmation.

When all of these are true:

```text
local/custom package
AND board IDs differ
AND strict board-ID matching was deliberately disabled
```

require a stronger confirmation before erase.

The confirmation must clearly display:

- selected local firmware filename/source;
- firmware package board ID;
- detected bootloader board ID;
- image size;
- detected bootloader revision;
- a warning that the target identity does not match.

Use a typed confirmation phrase if the existing dialog infrastructure supports it cleanly. A suitable phrase is:

```text
FLASH <firmware-board-id> ON <detected-board-id>
```

For example:

```text
FLASH 9 ON 50
```

Keep the interaction behind the existing firmware interaction abstraction; do not move domain/install orchestration into the Avalonia ViewModel.

Erase must not start unless this confirmation succeeds.

### 7. Diagnostics

Record whether a board-ID mismatch override was requested and whether it was actually used.

The diagnostic report should retain both IDs.

If a flash fails later, the copied diagnostic must make it obvious that a board-ID mismatch was deliberately overridden.

Prefer a structured field such as:

```text
Board ID override: Requested / Used / Not used
```

rather than relying only on free-form log text.

### 8. Do not change STM32 DFU local HEX semantics in this task

The existing local STM32 DFU `*_with_bl.hex` workflow is separate.

Intel HEX does not carry the APJ board metadata used by the serial bootloader workflow, and the current UI asks for the exact ArduPilot platform.

Do not apply this APJ/PX4 board-ID override to the DFU path unless a separate, explicit design decision is made.

### 9. Keep catalogue package validation strict

`FirmwarePreparationService` checks that the manifest board ID matches the downloaded APJ package board ID.

Keep this strict for official catalogue firmware.

The new override is about the **local/custom application package versus the detected bootloader board**, not about allowing a catalogue manifest to disagree with its package.

---

## Tests

Add/adjust tests covering at least:

### Compatibility service

1. Default policy rejects a board-ID mismatch.
2. Explicit local/custom policy permits only the board-ID mismatch.
3. With board mismatch allowed, too-old/new board revision is still rejected.
4. With board mismatch allowed, oversized internal image is still rejected.
5. With board mismatch allowed, insufficient external flash is still rejected.
6. With board mismatch allowed, bootloader revision is still enforced.
7. Secure/signed-image rules remain enforced.
8. Existing `33` / `9` exception remains covered.

### Bootloader client

1. Default `ProgramAsync` rejects wrong board before program command.
2. Explicit approved policy permits program/verify with wrong board.
3. Flash-capacity validation still blocks programming when board mismatch is allowed.
4. Verify uses the same policy as program; it must not fail only because the approved mismatch remains present.

### Installation service

1. Strict custom request fails at compatibility and never confirms/erases.
2. Custom request with override reaches the stronger mismatch confirmation.
3. Declining/mistyping mismatch confirmation never erases.
4. Approved mismatch proceeds through erase/program/verify/reboot.
5. Official/catalogue request cannot use the override.
6. Diagnostics show firmware ID, detected ID, source/provenance, and override state.

### ViewModel/UI state

Where practical with the existing test infrastructure:

1. Local custom file selection starts with strict matching enabled.
2. Clearing/replacing the local custom package restores strict matching.
3. The override control is not presented for ordinary catalogue-only installation.

Do not add a large UI-test framework merely for this task.

---

## Documentation

Update:

```text
docs/FIRMWARE.md
```

and any firmware help text that currently states that board-ID compatibility "cannot be overridden".

The documentation must say:

- strict exact board identity is the normal/default safety rule;
- only a deliberately selected local/custom application package can opt out;
- other compatibility checks cannot be bypassed by this option;
- using the override is an expert operation and may make the controller unbootable if the build is not genuinely compatible.

Do not weaken the general firmware safety guidance.

---

## Acceptance criteria

The task is complete when:

- ordinary catalogue firmware installation remains strict with no extra action required;
- local/custom APJ/PX4 installation defaults to strict;
- a user can deliberately disable only the board-ID equality rule for that custom package;
- both high-level compatibility and low-level bootloader validation honor the same explicit policy;
- all non-board-ID safety checks still fail closed;
- an actual overridden mismatch requires strong pre-erase confirmation;
- diagnostics record the override;
- existing serial and DFU workflows continue to pass their tests;
- documentation is consistent with the new expert override.
