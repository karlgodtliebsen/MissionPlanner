# Firmware identity tasks — execution

Tasks 01–05 implemented in order. No commits or staging performed.

## Delivered

1. Independent PhysicalDeviceIdentity, BootloaderIdentity, RunningFirmwareIdentity and
   SelectedFirmwareIdentity with source attribution. AUTOPILOT_VERSION board_version
   decodes only into the running-firmware identity. Local metadata comes from APJ content.
2. Structured, intent-aware compatibility; strict normal upgrades and explicit recovery.
   Conflicting bootloader identity, MCU, target and variant evidence blocks installation.
3. Existing installer/connection/uploader reused for recovery, with typed target confirmation,
   bounded discovery/reconnect and fresh running identity verification. Local packages keep
   embedded provenance and require sufficient vehicle/variant metadata.
4. Existing InstallFirmwarePage/ViewModel now displays separate source-labelled identity
   sections and a policy-gated Recovery / Change Firmware Target action. Intent is invalidated
   on selection changes; source-specific diagnostics replace ambiguous board terminology.
5. Regression tests and docs/FirmwareIdentityAndRecovery.md added. Desktop and Browser/WASM
   composition built; full automated test results recorded below.

## COM12 hardware evidence

The user retained COM12 for this task and separately authorized a temporary bootloader
identity check only. The initial automatic approval review rejected the transition while
the earlier preserve-unchanged instruction was still applicable; no hardware action ran
until the user explicitly approved the identity check.

| Observation | Value |
| --- | --- |
| Physical board | OmnibusF4, identified by the operator |
| USB serial | 52003D001651353232323938 |
| Running firmware | speedybeef4, application-reported board ID 134 |
| Running version/Git | ArduCopter 4.7.1 Official / dbe79216 |
| Selected artifact | Official omnibusf4 Copter 4.7.1 APJ, embedded board ID 1002 |
| Embedded MCU requirement | STM32F405xx |
| Queried bootloader | Board ID 134, revision 5, flash capacity 983040 |
| Bootloader chip description | STM32F40x,? (family-level evidence) |
| Erase/program | Not called |
| After inspection | Existing application rebooted; same USB UID, speedybeef4 / 134, 4.7.1, disarmed |

The captured first decision reported PhysicalDeviceMismatch because a broad chip string
was compared literally. This diagnostic defect was corrected: observed MCU families are
handled conservatively, and the actual bootloader 134 versus selected 1002 conflict takes
precedence as BootloaderMismatch. Regression tests use this exact captured evidence.

COM12 cannot be repaired through this bootloader workflow because its bootloader also
conflicts. It remains unchanged for a separately reviewed bootloader/DFU recovery operation.
No successful physical target-replacement flash is claimed. Matching-bootloader recovery
is covered by automated service tests.

Hardware evidence is in [HARDWARE_COM12.md](HARDWARE_COM12.md).
The temporary console harness composed the actual production services. It queried the
bootloader and rebooted the existing application; its inspection branch returns before
the installer and contains no erase/program calls. Visual desktop acceptance was not performed.

## Validation

- Full repository run: 1,315 .NET tests passed, 30 skipped; 7 browser JavaScript tests passed. No failures.
- Results: TestResults/all-tests/20260921-024259-543.
- Final affected-suite checks after the last fail-closed local-APJ adjustment: 346 firmware tests and 160 UI tests passed.
- Final solution build (including Desktop and Browser/WASM): zero errors, six existing nullable warnings. No CS1591, CS1587 or CS1573 warnings.
- Dedicated Browser/WASM build also passed with zero errors and warnings.
- git diff --check passed. No changes staged or committed.
