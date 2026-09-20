# TASK 04 — Install Firmware UI and Diagnostics

## Goal
Make identity provenance obvious in the UI.

## Terminology
Do not display:

```text
Detected board ID: 134
```

when the source is the running ArduPilot firmware.

Display:

```text
Running firmware board ID: 134
```

Distinguish:

```text
Physical device
Bootloader
Running firmware
Selected firmware
```

## Suggested panel
```text
Physical device
  MCU: STM32F405
  UID: ...

Bootloader
  Target: omnibusf4
  Board ID: 1002

Running firmware
  Target: speedybeef4
  Board ID: 134
  Version: ArduCopter 4.7.1

Selected firmware
  Target: omnibusf4
  Board ID: 1002
  Source: Local / Official
```

Hide truly unknown fields rather than showing misleading zeroes.

## Error presentation
Replace raw-only:

```text
compatibility.board-id-mismatch
```

with:

```text
Firmware target mismatch

The running firmware reports speedybeef4 / 134.
The selected firmware reports omnibusf4 / 1002.

This may mean the wrong firmware target is currently installed.
```

Retain diagnostic codes internally.

## Progress log
Log source explicitly:

```text
Running firmware target: speedybeef4
Running firmware board ID: 134
Selected firmware target: omnibusf4
Selected firmware board ID: 1002
```

## Local firmware
Show embedded APJ identity after validation. If filename/folder disagrees with APJ metadata, warn and trust embedded metadata.

## Recovery action
Only show recovery when compatibility evaluation says it is appropriate.

## Tests
Cover terminology, unknown fields, recovery-button visibility, APJ filename-vs-embedded mismatch, and Browser/Desktop composition.

## Acceptance
The UI never conflates running firmware identity with physical-board detection.
