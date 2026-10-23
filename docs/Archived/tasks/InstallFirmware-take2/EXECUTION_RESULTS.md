# InstallFirmware-take2 execution results

Completed on 2026-09-09 on `feature/add-DFU-bootload`.

| Task | Commit | Result |
| --- | --- | --- |
| 01 Navigation | `5187b2f32` | Three contexts, nested left tabs, persistent status/Refresh retained |
| 02 Device / Enter DFU | `c7f816a0a` | Focused identity/device/tool view; existing safe reboot reused |
| 03 DFU Catalogue | `ed374b8d8` | Shared selector, distinct combined HEX preview, source-directory validation |
| 04 Custom HEX | `6b5c1887d` | Focused local HEX composition; explicit source context; obsolete view removed |
| 05 Navigation and evidence | `083322d62` | Correlated endpoint/source retained; Catalogue navigation without flashing |
| 06 Regression and documentation | This commit | Automated coverage, cleanup, source filename guard and final builds |

## Verification

Every task passed affected UI tests and the Browser compilation check before its commit.
Task 03 also passed the firmware suite. Final checks:

- `src/Tests/Run-AllTests.ps1`: all suites passed, 1000 .NET tests and 7 JavaScript tests;
  29 existing skips. TRX/logs: `TestResults/all-tests/20260909-072113-002`.
- After the final source-filename guard: Firmware tests passed 254, skipped 1;
  UI tests passed 83. These add two firmware regression cases beyond the full-suite run.
- Desktop and Browser projects built successfully with `--no-restore -p:UsedAvaloniaProducts= -v quiet`.
- Final incremental Desktop and Browser builds each reported zero warnings/errors.
- New tests use fakes; no hardware was rebooted or flashed for this task set.

Relevant logs at repository root are ignored build artifacts: `take2-01-*` through `take2-06-*`.

## Scope and limits

No second catalogue, scanner, DFU monitor, file picker, resolver or flashing stack was introduced.
Normal APJ preparation and serial installation remain separate from Intel HEX. The existing
installer re-resolves/revalidates the selected HEX and repeats device/tool/target checks before
programming; a preview is not an authorization to flash. A source filename/directory mismatch
is rejected rather than silently substituted with another vehicle variant.

Physical controller acceptance and exact Pavo 20/ArduPilot compatibility remain separate pending
work from the earlier Betaflight task set. This restructure does not install Betaflight firmware
or assert an exact PCB identity from 0483:DF11 or MCU information.
