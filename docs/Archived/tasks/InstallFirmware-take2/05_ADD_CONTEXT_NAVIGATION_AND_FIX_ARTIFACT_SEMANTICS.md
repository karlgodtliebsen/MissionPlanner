# Task 05 — Add Context Navigation and Preserve Artifact Semantics

**Repository:** `karlgodtliebsen/MissionPlanner`  
**Branch:** `feature/add-DFU-bootload`

## Goal

Add small explicit UI navigation state so MissionPlanner carries the user naturally through Betaflight -> STM32 DFU without hiding any safety decision.

This is presentation/application state. Do not make selected tabs a firmware-domain concept.

## Navigation state

Add simple enum/properties as appropriate, conceptually:

```csharp
public enum FirmwareSection
{
    Firmware,
    Stm32Dfu,
    Help
}

public enum Stm32DfuSection
{
    Device,
    Catalogue,
    Custom
}
```

Names may vary to match project conventions.

Bind nested `TabControl`s to this state.

## Required transition

After a **successful physically correlated** Betaflight -> STM32 ROM DFU reboot:

1. keep/select the correlated DFU endpoint;
2. switch top-level section to `STM32 DFU`;
3. switch nested section to `Catalogue`;
4. preserve established source-controller identity/evidence;
5. do **not** start flashing automatically.

The user must still review/select firmware and explicitly confirm installation.

If handoff fails or is ambiguous, remain on `Device / Enter DFU` and show the failure.

## Target resolver integration boundary

If an existing Betaflight -> ArduPilot target resolver exists when this task runs, use its result to pre-filter/preselect only when confidence and safety rules allow it.

If no resolver exists, do not invent unsafe UI string matching. Manual catalogue selection remains valid.

Specifically:

- Betaflight target name alone may map to multiple ArduPilot platforms;
- STM32 MCU ID alone is insufficient proof of exact PCB;
- do not hardcode `BETAFPVF405 -> BETAFPV-F405` as a general rule.

## Artifact/source state

Keep these concepts distinct:

- selected catalogue manifest/APJ identity;
- normal APJ validated package;
- DFU resolved combined HEX artifact.

The DFU path must not reuse an APJ `ValidatedPackageView` with misleading labels.

Also verify selected platform and source directory/vehicle variant agree.

## User-state examples

### Betaflight COM

```text
Firmware: Betaflight
Target:   <reported target>
MCU:      <reported MCU>
UID:      <UID>
Action:   Enter STM32 DFU
```

### Correlated DFU

```text
STM32 ROM DFU: Detected
Physical handoff: Matched
Source identity: preserved
Ready to choose combined ArduPilot image
```

### Anonymous DFU

```text
STM32 ROM DFU: Detected
Source identity: Unknown
Exact flight-controller target cannot be inferred from DFU alone.
```

## Acceptance criteria

1. Successful Betaflight -> DFU handoff navigates to STM32 DFU / Catalogue.
2. It does not automatically flash.
3. Failed handoff stays in Device / Enter DFU.
4. Correlated DFU selection survives navigation.
5. Anonymous DFU does not get an invented platform.
6. Existing target resolver is used if available; otherwise manual selection remains.
7. No hardcoded Pavo/BETAFPV special case is introduced.
8. Manifest/APJ and DFU HEX state are semantically distinct.
9. Relevant tests pass and affected projects compile.
