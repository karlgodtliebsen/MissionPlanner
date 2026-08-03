# Firmware automated test matrix

Verified on 2026-08-04 on branch `feature/firmware`.

## Automated coverage

- Domain/unit: manifest normalization and corruption, catalogue filtering/cache fallback, APJ/PX4 bounds and decompression, compatibility decisions, operation transitions/exclusivity/cancellation, download length/hash/atomic commit, page-mode command policy, and every connected command ACK.
- Protocol: identify, erase, chunked program, verify, reboot, fragmented replies, bounded timeouts, invalid synchronization/status, wrong checksum, disconnect during erase, and disconnect during program.
- Orchestration: successful simulated install, connected rejection, download and malformed-package failures, missing device, board/capacity mismatch, erase/program/verification failures, application rediscovery and timeout, duplicate lease, cancellation before erase, and cancellation raised during erase.
- Presentation contracts: connected/disconnected/operation/unsupported modes, navigation policy, command gates, Stable/Beta/Latest channel model, all-options filtering, custom package parsing, confirmation adapter, and bootloader interaction codes.
- Recovery/diagnostics: USB identity across port changes, missing returning application as non-fatal, and bounded copyable reports.

`MissionPlanner.Firmware.Tests`: 101 passed, 1 skipped manual-hardware theory, 0 failed. The test project is hardware-independent and CI-safe. The added audit cases cover the current official manifest schema and bounded compressed retrieval, artifacts without encoded length, isolated temporary MAVLink reboot ACK outcomes, same-port application-to-bootloader replacement, stronger returning-device identity matching, and non-cancellable destructive protocol tokens after confirmation.

## Manual hardware category

`FirmwareHardwareTests` is marked `Category=ManualHardware` and skipped unless an operator deliberately converts/runs the scenario under a hardware safety procedure. It records F4, H7, port-change, repeated-upload, mismatch, unplug/replug, and embedded-bootloader-update checks. No CI job requires a physical controller.

## Repository regression results

- `MissionPlanner.App` builds successfully for its configured `net10.0` target.
- `MissionPlanner.Firmware` references only `MissionPlanner.Transport`; it has no MAUI/UI reference.
- The full Core suite reports 461 passed, 11 skipped, and the same 11 pre-existing failures recorded before firmware implementation. Ten focused firmware presentation and temporary-MAVLink gateway cases pass.
- The full solution restore succeeds. The managed build reaches all projects; the same two pre-existing Android packaging targets fail because `java.exe` exits with code 2 (`UraniumUI.Material.Extensions.Samples` and `MissionPlanner.Droid`).

The baseline failures and Android toolchain issue are not suppressed or reclassified as successful firmware tests.
