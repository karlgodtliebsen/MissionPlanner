# Firmware hardware smoke-test record

Status: **Partially executed — F4 preflight/entry tested but blocked by the absence of an ArduPilot serial bootloader; H7 hardware is still required.**

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
| Controller and revision | OMNIBUSF4SD / STM32F405; application reports ArduCopter 4.8.0-dev (`3a98d087`) |
| Test date / operator | 2026-08-04 / Karl Godtliebsen with Codex harness |
| Mission Planner commit | `4e3e8db55` (entry/discovery fixes through this commit) |
| Stable catalogue upload | Blocked before erase: official `omnibusf4` Stable 4.7.0 APJ, board ID 1002, could not be used because no serial bootloader answered |
| Custom APJ upload | Pending |
| Application → bootloader transition | Not available through the supported serial workflow. BOOT/BOOT0 enters STM32 ROM DFU; DFU/HEX bootloader installation is future scope. |
| Bootloader → application rediscovery | Pending |
| Repeated upload | Pending |
| Wrong-board package blocked before erase | Pending |
| Manual unplug/replug fallback | Prompt and resumed discovery exercised; same COM11 application device remained, but no protocol-compatible bootloader was present |
| Connected embedded-bootloader update | Pending |
| Diagnostic report / operation IDs | Representative final pre-write result: `8de14620-9228-4ae6-af0c-8aa3dd3ae55e`, `installation.device-not-found`; all attempts stopped before erase |

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

The firmware first-release hardware criterion is met only when the F4 and H7 tables contain real successful evidence for the applicable scenarios. Pending entries, simulated transports, or the skipped `FirmwareHardwareTests` theory do not satisfy that criterion.
