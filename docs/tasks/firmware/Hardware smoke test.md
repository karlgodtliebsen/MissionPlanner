# Firmware hardware smoke-test record

Status: **F4 serial upload protocol completed, but operational acceptance remains pending with the corrected OmnibusF4 target; H7 coverage is also pending.**

This record is intentionally separate from CI. Complete it on Windows with expendable or recoverable test hardware, stable USB power, a known-good data cable, and access to the board's documented recovery method. Do not test on an installed or armed vehicle. Remove propellers and disconnect actuators before starting.

## Evidence required for each controller

Record all of the following rather than marking a scenario only as pass/fail:

- Date, operator, Mission Planner commit, and Windows version.
- Controller manufacturer/model, processor family (F4 or H7), hardware revision, and bootloader revision.
- USB VID/PID, USB serial number when exposed, application COM port, and bootloader COM port.
- Selected catalogue channel, vehicle type, platform, version, firmware board ID, and detected bootloader board ID.
- Firmware operation ID and the complete copyable diagnostic report.
- Whether application-to-bootloader and bootloader-to-application transitions changed COM ports.
- Verification result and the returned application port.
- Recovery method tested or available.

Never paste firmware images, private signing material, or unrelated device identifiers into this record.

## F4 controller

| Field | Result |
|---|---|
| Controller and revision | OmnibusF4 / STM32F405; previously identified by Betaflight as `OMNIBUSF4SD`; STM32 device ID `0x413`, 1 MB internal flash |
| Test date / operator | 2026-08-04 / Karl Godtliebsen with Codex harness |
| Mission Planner commit | `a2b8ea37d` |
| USB/application identity | During board-ID 134 test: `1209:5741`, serial `5B002E000951353332343134`, `ArduPilot (COM11)`; corrected OmnibusF4 application identity to be recorded on its matching upload |
| STM32 ROM DFU identity | VID:PID `0483:DF11`, serial `345F348D3335` |
| Stable catalogue upload | Pending with the corrected `omnibusf4` board-ID 1002 bootloader/application target |
| Custom APJ upload | **Protocol passed but operational result failed.** The uploader erased, programmed and verified the board-ID 134 `speedybeef4` package, but that hardware definition could not initialize the physical IMU (`Config Error: INS: unable to initialize driver`) |
| Application → bootloader transition | **Protocol passed.** Temporary MAVLink reboot returned `Accepted`; the device disappeared and returned on COM11; the then-installed bootloader identified board 134 before confirmation/erase |
| Bootloader → application rediscovery | **Transport passed, operational acceptance failed.** The application returned on COM11, but produced no attitude because the mismatched SpeedyBee hardware definition could not initialize INS |
| Repeated upload | Pending |
| Wrong-board package blocked before erase | Pre-erase board-ID guards were exercised with the board-ID 1002 catalogue candidate; destructive protocol was not entered |
| Manual unplug/replug fallback | Prompt/resume and live PnP removal/arrival detection exercised; successful upload used automatic MAVLink entry |
| Connected embedded-bootloader update | Pending |
| Diagnostic report / operation IDs | **Protocol completed:** `5e431412-621b-42db-b904-c49729f29422`; detected board 134; bootloader revision 5; 848,336 bytes programmed; verification succeeded; elapsed 15.753 s. This is not an operational target pass. |
| Recovery method | **Passed.** Operator entered STM32 ROM DFU with BOOT/BOOT0 and used STM32CubeProgrammer to install a custom-built OmnibusF4 image. INS, the original Mission Planner HUD and the new Mission Planner HUD all work afterward. |

## H7 controller

| Field | Result |
|---|---|
| Controller and revision | Pending |
| Test date / operator | Pending |
| Mission Planner commit | Pending |
| Stable catalogue upload | Pending |
| Custom APJ upload | Pending |
| Application → bootloader transition | Pending |
| Bootloader → application rediscovery | Pending |
| Repeated upload | Pending |
| Wrong-board package blocked before erase | Pending |
| Manual unplug/replug fallback | Pending |
| Connected embedded-bootloader update | Pending |
| Diagnostic report / operation IDs | Pending |

## Procedure

1. Start Mission Planner at the recorded commit with no active vehicle connection. Confirm the firmware page detects the application-mode controller and reports its USB identity.
2. Select the matching Stable catalogue entry. Confirm the displayed platform and firmware board ID before continuing.
3. Start installation and capture the final confirmation, detected bootloader identity, both COM ports, operation ID, and diagnostic report.
4. Keep power connected through erase, program, verify, and reboot. A test passes only if protocol verification succeeds; rediscovery is recorded separately.
5. Repeat with a locally selected matching `.apj` package.
6. Repeat the successful catalogue upload once to exercise serial-resource cleanup and a second complete transition.
7. Select a known wrong-board APJ and verify it is rejected after bootloader identification but before erase. Confirm no destructive protocol call occurred.
8. Exercise the manual unplug/replug prompt and record whether bootloader discovery resumes on the same or a different COM port.
9. Reconnect normally, keep the vehicle disarmed, run the separately confirmed embedded Bootloader Update action on a supported controller, and record the exact ACK outcome. Reboot only according to the controller documentation.
10. If any destructive-stage failure occurs, stop normal testing, preserve logs and the diagnostic report, and follow the manufacturer's documented recovery procedure.

## Completion gate

The firmware task's first-release hardware criterion requires at least one real supported controller to complete a documented Windows erase/program/verify/reboot cycle with an operational matching target. The board-ID 134 run proves the serial protocol path but does not satisfy that gate because the target was physically incompatible. Re-run the new uploader with the corrected OmnibusF4 board-ID 1002 bootloader and matching APJ before marking the F4 result complete. The extended F4 scenarios and full H7 table remain pending.
