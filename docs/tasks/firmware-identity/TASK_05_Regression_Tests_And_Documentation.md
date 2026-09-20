# TASK 05 — Regression Tests and Documentation

## Goal
Protect the new identity and recovery behavior.

## Real regression scenario
Add tests for:

```text
Running:
  speedybeef4 / 134

Selected:
  omnibusf4 / 1002
```

### Normal upgrade
Expected:
```text
blocked
RunningFirmwareMismatch
recovery may be offered
```

### Recovery with matching bootloader
```text
bootloader omnibusf4 / 1002
```

Expected:
```text
CompatibleForRecovery
RequiresExplicitConfirmation = true
```

### Recovery with conflicting bootloader
```text
bootloader speedybeef4 / 134
```

Expected: blocked.

## Additional tests
- local APJ filename says Omnibus but embedded metadata says 134;
- exact variants sharing a board id remain distinct;
- missing bootloader identity;
- MCU mismatch;
- vehicle-type mismatch;
- official online firmware;
- local/custom firmware;
- reconnect/post-flash validation.

## Documentation
Create:

```text
docs/FirmwareIdentityAndRecovery.md
```

Document:

```text
Physical device identity
Bootloader identity
Running firmware identity
Selected firmware identity
```

Document authority rules for NormalUpgrade vs Recovery.

State clearly that ArduPilot BoardId identifies firmware/bootloader targets and must not automatically be treated as immutable physical-hardware identity.

Document that embedded APJ metadata is authoritative over filename/folder names.

Document that there is no generic force-flash path.

## Build validation
Run firmware installer/device identification tests plus desktop and Browser/WASM builds where applicable.

## Acceptance
The real 134-vs-1002 case is covered by regression tests and documented.
