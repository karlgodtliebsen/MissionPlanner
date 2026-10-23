# Task 04 — Create STM32 DFU Custom HEX Workflow

**Repository:** `karlgodtliebsen/MissionPlanner`  
**Branch:** `feature/add-DFU-bootload`

## Goal

Move the current local combined HEX workflow out of the overloaded `STM32BootloaderView` and into:

```text
STM32 DFU -> Custom HEX
```

Normal `Firmware -> Custom Firmware` remains APJ/PX4-oriented. The two workflows should look consistent while retaining different validation rules.

## Existing behavior to preserve

`STM32BootloaderViewModel` already contains:

- `LoadCustomBlWithFirmwareCommand`
- `ClearLocalDfuFirmwareCommand`
- `LocalDfuFirmwarePath`
- `LocalDfuFirmwareName`
- `LocalDfuPlatform`
- `.hex` extension validation
- `*_with_bl.hex` filename requirement
- local-path requirement for STM32CubeProgrammer

Reuse these semantics.

## Suggested new view

- `STM32DfuCustomHexView.axaml`
- `STM32DfuCustomHexView.axaml.cs`

It may continue using `STM32BootloaderViewModel` or a carefully renamed DFU ViewModel if that is already the correct state owner.

Do not create a second file-picker service.

## UI content

Display:

- Load local/custom combined HEX;
- Clear selection;
- selected filename/path summary;
- exact ArduPilot platform input when required;
- selected STM32 DFU endpoint;
- CubeProgrammer readiness;
- clear warning that Intel HEX lacks APJ board metadata;
- install action using the existing DFU command/service.

Keep a prominent warning equivalent to:

> This combined application-and-bootloader image replaces the current firmware.

Use the existing confirmation/safety path from the parent installer.

## Validation rules

Preserve at least:

1. file must be `.hex`;
2. modern workflow accepts combined `*_with_bl.hex`;
3. file must expose a local path usable by STM32CubeProgrammer;
4. exact ArduPilot platform must be supplied when catalogue metadata is unavailable;
5. a selected DFU endpoint is required;
6. existing target-safety and provider/tool validation still execute before programming.

Do not reuse `CustomFirmwareViewModel` APJ board-ID override semantics for HEX unless there is a genuine shared abstraction. APJ and Intel HEX carry different metadata.

## Clean up old STM32 view

After Tasks 02-04, `STM32BootloaderView.axaml` must no longer be a single overloaded screen containing catalogue + custom HEX + DFU instructions + device selection + flash warnings.

Remove it if obsolete, or reduce/rename it to a clean composition matching the new hierarchy. Avoid duplicate dead views.

## Acceptance criteria

1. `Firmware -> Custom Firmware` remains unchanged for normal custom packages.
2. `STM32 DFU -> Custom HEX` accepts only the intended combined HEX workflow.
3. Load and Clear commands work.
4. Exact platform input appears/is required where necessary.
5. DFU installation uses the existing service/command path.
6. Custom HEX does not inherit APJ-specific board metadata UI.
7. Existing warnings and confirmations remain.
8. No duplicate picker/service is created.
9. Relevant tests pass and affected projects compile.
