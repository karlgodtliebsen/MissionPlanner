# TASK 03 — Explicit Firmware Recovery Workflow

## Goal
Support repair when the wrong firmware target is already running.

## Trigger
When normal compatibility fails because running and selected firmware differ, expose:

```text
Recovery / Change Firmware Target
```

only when policy says recovery is a valid next step.

Do not automatically switch modes.

## Flow
```text
1 Detect controller
2 Read running firmware identity
3 Enter/query bootloader where supported
4 Read bootloader/device identity
5 Evaluate recovery compatibility
6 Show evidence
7 Require explicit confirmation
8 Flash
9 Reconnect
10 Validate new running identity
```

## Reuse
Use existing DFU/bootloader/device-identification and reconnect services. Do not introduce a second USB stack.

## Confirmation
Show clear evidence, e.g.:

```text
Currently running:
  speedybeef4 / 134

Selected:
  omnibusf4 / 1002

Bootloader:
  omnibusf4 / 1002

Recovery will replace the current firmware target.
```

## Validation
Recovery must still validate:
- APJ structure;
- embedded target and board id;
- vehicle type;
- MCU compatibility where available;
- firmware size;
- existing hash/signature rules.

No generic force-flash button.

## Post-flash
After reconnect, re-read:
- AUTOPILOT_VERSION;
- startup target text;
- firmware version.

Only report success if the new running identity matches expected target sufficiently.

## Tests
Cover:
- matching bootloader recovery;
- conflicting bootloader rejection;
- explicit confirmation;
- cancellation during bootloader transition;
- COM/USB re-enumeration;
- post-flash identity verification.

## Acceptance
Wrong-target firmware can be repaired without weakening ordinary upgrade safety.
