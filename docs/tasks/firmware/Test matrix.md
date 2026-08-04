# Firmware automated test matrix

Verified on 2026-08-04 on branch `feature/firmware-tasks-step-2`.

## Automated coverage

- Domain/unit: manifest normalization and per-entry isolation, persistent catalogue fallback, APJ/PX4 bounds and decompression, compatibility decisions, operation transitions/exclusivity/safe-boundary cancellation, download length/hash/atomic cache publication and cleanup, page-mode command policy, and every connected command ACK.
- Protocol: identify, erase, chunked program, verify, reboot, fragmented replies, bounded timeouts, invalid synchronization/status, wrong checksum, disconnect during erase, and disconnect during program.
- Orchestration: successful simulated install, connected rejection, download and malformed-package failures, missing device, board/capacity mismatch, erase/program/verification failures, application rediscovery and timeout, duplicate lease, cancellation before erase, and cancellation raised during erase.
- Presentation contracts: connected/disconnected/operation/unsupported modes, navigation policy, command gates, latest-request-wins Stable/Beta/Latest refresh, all-options filtering, explicit selection retention, Download & Validate cancellation, custom package parsing, confirmation adapter, and bootloader interaction codes.
- Recovery/diagnostics: USB identity across port changes, missing returning application as non-fatal, and bounded copyable reports.

`MissionPlanner.Firmware.Tests`: 133 passed, 1 skipped manual-hardware test, 0 failed. The test project is hardware-independent and CI-safe. Coverage includes the official manifest schema and bounded compressed retrieval, artifacts without encoded length, persistent cache restart/corruption/quota behavior, isolated temporary MAVLink reboot ACK outcomes, same-port application-to-bootloader replacement, stronger returning-device identity matching, non-cancellable destructive protocol tokens after confirmation, deferred cancellation through verified reboot and port disposal, and revision-five identification without an unsafe optional external-flash probe.

The operator-facing no-hardware acceptance procedure is [Firmware Download — User Test Protocol](Step-2/02-Firmware-Download-User-Test-Protocol.md).

## Manual hardware category

`FirmwareHardwareTests` is marked `Category=ManualHardware` and skipped unless an operator deliberately converts/runs the scenario under a hardware safety procedure. It records F4, H7, port-change, repeated-upload, mismatch, unplug/replug, and embedded-bootloader-update checks. No CI job requires a physical controller.

The operator checklist and evidence fields are in [Hardware smoke test](Hardware%20smoke%20test.md). A real F4 board-ID 134 erase/program/verify/reboot protocol cycle completed on Windows, but the SpeedyBee target was physically incompatible with the OmnibusF4 controller and failed INS initialization. Operational acceptance therefore remains pending with a matching OmnibusF4 board-ID 1002 APJ; extended F4 scenarios and H7 coverage also remain explicit. No physical result is inferred from simulated coverage.

## Repository regression results

- `MissionPlanner.App` builds successfully for its configured `net10.0` target.
- `MissionPlanner.Firmware` references only `MissionPlanner.Transport`; it has no MAUI/UI reference.
- Eight focused firmware presentation cases pass in the current Step-2 verification, including refresh ordering, explicit selection retention, custom-package handling, and Download & Validate cancellation. Broader repository baselines must be recorded from the actual run rather than copied from an earlier snapshot.
- The full solution restore succeeds. The managed build reaches all projects; the same two pre-existing Android packaging targets fail because `java.exe` exits with code 2 (`UraniumUI.Material.Extensions.Samples` and `MissionPlanner.Droid`).

The baseline failures and Android toolchain issue are not suppressed or reclassified as successful firmware tests.
