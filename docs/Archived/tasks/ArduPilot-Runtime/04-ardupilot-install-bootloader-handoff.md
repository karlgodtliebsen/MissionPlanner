# Task 04 — Automate ArduPilot Application → Bootloader Handoff During Install

## Objective

Complete the normal ArduPilot `.apj` installation path so the user does not have to manually invoke `Enter ArduPilot Bootloader` before an ordinary firmware install. The explicit/manual bootloader-entry command remains available for diagnostics, but **do not modify that icon button or any other icon button**.

The Install workflow should use the known ArduPilot runtime to perform the transition internally.

## Required investigation

Locate and reuse:

1. current install coordinator/workflow;
2. existing ArduPilot bootloader-entry service/strategy;
3. serial re-enumeration/discovery after bootloader entry;
4. ArduPilot bootloader protocol and board-ID read;
5. APJ package identity extraction;
6. existing strict compatibility service;
7. existing flash/erase/write entry point;
8. DFU and Betaflight paths.

Do not implement a second bootloader protocol or compatibility algorithm.

## Required install sequence

### A. Preflight

1. Validate the selected package using existing validation.
2. Determine current device state.
3. If already in ArduPilot bootloader, continue to bootloader identification.
4. Otherwise, when runtime is verified ArduPilot application firmware, request reboot/entry into the ArduPilot bootloader through the existing bootloader-entry abstraction. Do not require the user to press the manual toolbar button first.

### B. Bootloader discovery

1. Tolerate expected serial disappearance/re-enumeration.
2. Discover the matching ArduPilot bootloader using existing mechanisms.
3. Use bounded timeout/cancellation.
4. Do not match an arbitrary newly appearing serial device merely by timing.
5. Preserve existing physical-device correlation safeguards.

### C. Authoritative hardware identification

1. Read the bootloader's authoritative board identity/board ID.
2. Update hardware-target state from that source.
3. Do not retain a conflicting USB-derived exact-board guess.

### D. Strict compatibility gate

Before any erase/write:

1. compare APJ target identity with bootloader board identity through the existing compatibility service;
2. proceed only on authoritative match;
3. block mismatch;
4. block if required authoritative identity cannot be established;
5. never allow friendly name, VID/PID or ArduPilot runtime to override this gate.

### E. Flash

Only after compatibility succeeds, invoke the existing programming path and preserve existing verification/progress/cancellation behavior.

## Failure handling

Provide actionable failures for:

- bootloader reboot/entry request failed;
- bootloader discovery timeout;
- bootloader handshake failure;
- board ID unavailable;
- APJ/bootloader board mismatch;
- ambiguous re-enumerated device;
- cancellation.

Do not silently fall back to STM32 ROM DFU for an ordinary APJ installation unless existing architecture explicitly defines a safe, supported, user-confirmed recovery path.

## Preservation requirements

Do not regress manual ArduPilot bootloader entry, Betaflight, STM32 DFU recovery, online/local firmware selection, progress/status reporting or existing board-ID compatibility checks. Do not redesign the toolbar.

## UI protection — hard constraint

Do not modify any icon button, including the manual bootloader-entry button: no icon/glyph/image, declaration, style/template, layout/order, visibility, tooltip, command binding, size or spacing changes. The automatic handoff belongs in workflow/services.

## Tests

At minimum cover:

1. already in AP bootloader => no redundant reboot; board ID read; match proceeds;
2. verified AP application => Install requests bootloader entry automatically, discovers, reads board ID, checks compatibility, proceeds on match;
3. board mismatch => erase/write never called;
4. board ID unavailable => blocked before erase/write;
5. discovery timeout => no flash attempt;
6. cancellation during handoff => clean exit, no later flash;
7. USB hint conflicts with bootloader => bootloader identity wins;
8. Betaflight/DFU regression paths remain unaffected.

## Acceptance criteria

- [ ] Normal Install can transition verified ArduPilot application firmware into bootloader automatically.
- [ ] Existing bootloader-entry architecture is reused.
- [ ] Bootloader board ID is read before flashing.
- [ ] Existing strict APJ/bootloader compatibility logic is reused.
- [ ] Mismatch blocks before erase/write.
- [ ] USB metadata cannot override authoritative compatibility.
- [ ] Manual bootloader-entry remains intact.
- [ ] Betaflight and DFU behavior remain intact.
- [ ] Cancellation/timeouts are bounded and safe.
- [ ] Relevant tests pass.
- [ ] No icon-button definitions/resources/styles/bindings were modified.
- [ ] No toolbar/UI redesign was performed.
