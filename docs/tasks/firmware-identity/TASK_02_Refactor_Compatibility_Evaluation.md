# TASK 02 — Refactor Firmware Compatibility Evaluation

## Goal
Make compatibility evaluation aware of identity source and install intent.

## Add operation mode
```csharp
public enum FirmwareInstallMode
{
    NormalUpgrade,
    Recovery
}
```

## Normal upgrade
- prefer authoritative bootloader/device identity when available;
- otherwise use running firmware identity;
- require compatible board target, vehicle type and variant;
- reject mismatched board id/target;
- reject ambiguous resolution.

## Recovery
For wrong-target recovery:
1. prefer bootloader identity;
2. if bootloader matches selected firmware, recovery may proceed even if running firmware differs;
3. if bootloader conflicts, block;
4. if bootloader is unavailable, return `IdentityInsufficient` or an explicit reduced-confidence recovery result;
5. never reinterpret running firmware identity as physical hardware identity.

## Structured result
Return evidence, not just a string code.

Example:

```csharp
FirmwareCompatibilityResult
  Status
  Mode
  Summary
  Evidence[]
  CanProceed
  RequiresExplicitConfirmation
```

Useful statuses:

```text
Compatible
CompatibleForRecovery
RunningFirmwareMismatch
BootloaderMismatch
PhysicalDeviceMismatch
TargetMismatch
VehicleTypeMismatch
IdentityInsufficient
Ambiguous
```

## Important
BoardId alone is not complete identity. Preserve exact target/platform and variant logic already used by Install Firmware.

## Tests
Cover:
- running 1002 / selected 1002 -> compatible;
- running 134 / selected 1002 -> normal upgrade blocked;
- running 134 / bootloader 1002 / selected 1002 -> recovery allowed;
- running 134 / bootloader 134 / selected 1002 -> recovery blocked;
- bootloader unknown -> explicit insufficient/reduced-confidence result.

## Acceptance
Normal protection remains strict and recovery uses authoritative identity sources.
