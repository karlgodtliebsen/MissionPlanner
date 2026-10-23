# TASK 03 — Guard Embedded Bootloader Update

## Goal

Prevent `Update Embedded Bootloader` from being offered when the currently running firmware identity conflicts with the intended/selected target.

## Real case

```text
Running:
  speedybeef4
  board id 134

Selected/intended:
  omnibusf4
  board id 1002
```

In this state, updating the embedded bootloader from the currently running firmware is ambiguous and potentially unsafe.

## Required policy

Disable/hide `Update Embedded Bootloader` when running target/board identity and selected intended target disagree, unless a dedicated recovery policy explicitly proves the embedded bootloader is the intended one.

Use the existing firmware identity/recovery service.

Do not duplicate the logic in the ViewModel.

## User message

Example:

```text
Embedded bootloader update unavailable

The currently running firmware target does not match
the selected controller target.

Running:  speedybeef4 / 134
Selected: omnibusf4 / 1002

Use Firmware Recovery to restore the correct target
and bootloader.
```

## Recovery interaction

If recovery already uses:

```text
arducopter_with_bl.hex
```

through STM32 DFU, make it clear that bootloader replacement is part of recovery.

Do not offer a separate embedded-bootloader update simultaneously.

## Normal case

For:

```text
running omnibusf4 / 1002
selected omnibusf4 / 1002
```

retain current bootloader-update availability if supported.

## Tests

Cover:

```text
running == selected -> available
target mismatch -> unavailable
board id mismatch -> unavailable
identity unknown -> conservative/unavailable
with_bl recovery selected -> separate update unavailable
```

## Acceptance criteria

- Bootloader update cannot reinforce a known wrong target.
- Availability is driven by identity/recovery policy.
