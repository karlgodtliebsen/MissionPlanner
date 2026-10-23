# COM12 onboard logging investigation

## Findings (26 September 2026, local time)

The user clarified that COM12 is a 5-inch drone, not the Pavo 20 Pro, and that
there is no SD card installed. The user identifies three Omnibus controllers:

| Label | Port | Intended MAV_SYSID | Intended BRD_SERIAL_NUM |
| --- | --- | --- | --- |
| Yellow | COM14 | 10 | 2 |
| Black | COM11 | 11 | 3 |
| Blue | COM12 | 12 | 4 |

Read-only inspection of COM12 instead observed system/component **1:1**, the
parameter values below, USB serial `52003D001651353232323938`, and the banner
`omnibusf4 003D0052 32355116 38393232`. Firmware was ArduCopter 4.7.1,
git `dbe79216`. Heartbeat remained disarmed. COM11 and COM14 were not contacted.
Resolve this identity discrepancy before applying any configuration changes.

After this capture, the user corrected Blue to `MAV_SYSID=12` and
`BRD_SERIAL_NUM=4`. This is a user-confirmed update; the values below deliberately
preserve what the earlier read-only diagnostic observed.

```text
MAV_SYSID = 1
BRD_SERIAL_NUM = 0
LOG_BACKEND_TYPE = 1 // File backend
LOG_DISARMED = 0
LOG_BITMASK = 180222
LOG_FILE_BUFSIZE = 16
LOG_FILE_DSRMROT = 0
LOG_FILE_MB_FREE = 500
LOG_FILE_RATEMAX = 0
BRD_SD_SLOWDOWN = 0
ARMING_SKIPCHK = 4 // Compass check skipped; logging check is not skipped
ARMING_OPTIONS = 0
RC5_OPTION = 153
FLTMODE_CH = 8
```

Requests for ARMING_CHECK did not produce that parameter. This build replaces
ARMING_CHECK with ARMING_SKIPCHK, with the opposite mask meaning. That explains
the old page's unknown configuration label. It is separate from SYS_STATUS
live readiness and from FC rejection messages.

MAVFTP listing of `/` and `/APM/LOGS` returned FC `FailureErrno` responses.
Listing `@SYS` succeeded, including `threads.txt`, `memory.txt`, `storage.bin`
and `flash.bin`. These are virtual diagnostics; their presence does not establish
a mounted log volume. In particular, `storage.bin` (15360 bytes) is not proof of
usable flight-log storage. No log directory or free-capacity reading was obtained.
The reported ENOSPC/PreArm messages were not repeated during this short passive
inspection; no arm attempt or reboot was issued to provoke them.

Raw local probe output: `artifacts/logging-com12.txt`. The temporary probe uses
the existing connection/parameter services and connection-owned MAVFTP client.
It requested parameter reads and directory listings only, and disconnected in
`finally`. No parameter write, erase, format, firmware installation, reboot or
arming command was performed. An initial identity guard stopped on SysID 1;
subsequent inspection was deliberately read-only despite that discrepancy.

## Why ENOSPC does not mean “delete logs” here

The exact running revision's [omnibusf4 hardware definition](https://github.com/ArduPilot/ardupilot/blob/dbe79216/libraries/AP_HAL_ChibiOS/hwdef/omnibusf4/hwdef.dat)
inherits [omnibusf4pro](https://github.com/ArduPilot/ardupilot/blob/dbe79216/libraries/AP_HAL_ChibiOS/hwdef/omnibusf4pro/hwdef.dat).
It expects an **SPI2 SD card**, chip select **PB12**, with FATFS enabled. The MCU's
internal parameter-storage flash is not an alternate log filesystem. This target
does not establish that a physically different cheap F405 board has an SD socket,
the expected wiring, or a supported logging flash chip.

In the pinned [FATFS adapter](https://github.com/ArduPilot/ardupilot/blob/dbe79216/libraries/AP_Filesystem/AP_Filesystem_FATFS.cpp),
`FR_NOT_ENABLED` (volume has no registered work area) maps to `ENOSPC`.
Directory-full/access-denied instead maps to `EACCES`; low-level disk errors map
to `EIO`. [SD initialization](https://github.com/ArduPilot/ardupilot/blob/dbe79216/libraries/AP_HAL_ChibiOS/sdcard.cpp)
connects the card before mounting FATFS. A missing/unresponsive card can therefore
leave no mounted volume. The [file logger](https://github.com/ArduPilot/ardupilot/blob/dbe79216/libraries/AP_Logger/AP_Logger_File.cpp)
reports the translated error when directory creation fails.

The missing card, unusable real filesystem, and this error mapping support
**unavailable/unmounted SD storage**, not a demonstrated full filesystem.
Physical board markings, presence of an SD socket and its wiring remain
unverified remotely. A known-good card cannot fix a board with no supported SD
interface. Flashing an arbitrary F4 target is not a justified repair either.

## Ordered recovery

1. Keep the vehicle disarmed, remove propellers for bench work, and identify the
   actual COM12 controller against the serial/UID above. In NextGen connect COM12,
   open **Arming → Open Full Parameters**, use **Refresh parameters**, and wait
   for completion. Use **Save to CSV or Json file → Save to json file** (or CSV)
   to preserve the parameter snapshot. Check MAV_SYSID and BRD_SERIAL_NUM against
   the intended Blue controller before any writes. Save the Messages evidence too.

2. Preserve any media/logs before attempting repair. In **Config/Tuning → Mav Ftp**,
   use **Refresh**, select `APM`, **Open selected remote directory**, then `LOGS`;
   select each log and use **Download selected file from vehicle**. Verify the
   downloaded files before any removal. NextGen's **DataFlash Logs** flight-data
   tab is currently a TODO and cannot perform a log backup. If the FC cannot mount
   an existing card, power off, remove the card and copy/image it using a reader
   before filesystem repair or formatting. Do not interpret a failed directory
   listing as “no logs to preserve.”

3. Inspect the physical board with power disconnected.
   - If it has the SD socket/interface specified for OmnibusF4, install a suitable
     known-good FAT32 microSD card. Back up an existing card first; prefer a spare
     for diagnosis. Reconnect and verify the filesystem becomes accessible.
   - If there is no supported SD interface, there is **no parameter-only repair**
     for this target's onboard file logger. Identify the exact board/revision and
     any flash chip. Use a firmware target that explicitly supports that hardware
     and logging medium, or replace the FC with one providing supported storage.
     Do not select block logging merely because the MCU has flash.
   - Only if media mounts and measured free space confirms exhaustion should
     backed-up logs be removed to free space. If a spare works but the original
     does not, investigate card/filesystem failure after preserving it. If neither
     works, check target, wiring, socket and power before formatting anything.

4. With working supported SD storage, retain the observed file-backend value:

   ```text
   LOG_BACKEND_TYPE = 1 // Already set; no rewrite needed
   ```

   Do not change LOG_FILE_MB_FREE or buffer size to fix an unmounted volume.
   LOG_FILE_MB_FREE controls automatic deletion of old logs once a usable backend
   exists, so preserve any old card before putting it back into service.
   LOG_BACKEND_TYPE=0 disables logging; 2 selects MAVLink logging; 4 selects a
   compiled supported block backend. None creates the missing SD storage, and PC
   telemetry recording is not a substitute for onboard logging repair.

5. Restore all optional arming checks for flight after resolving the applicable
   hardware/setup issues. On this **confirmed 4.7.1 firmware**, the relevant value
   is:

   ```text
   ARMING_SKIPCHK = 0 // No checks skipped; current value 4 skips compass
   ```

   In **Full Parameters**, edit that value, review it, use **Apply modified
   Parameters**, then **Refresh parameters** to verify readback. Enabling the
   compass check may reveal a separate configuration problem; fix it. Do not set
   skip-mask bit 10 to hide logging failure. On older firmware that actually
   exposes ARMING_CHECK, the equivalent all-checks value is ARMING_CHECK=1, not 0.
   Do not paste legacy values into a 4.7 skip mask.

6. After backup and hardware repair, reboot normally and inspect fresh FC
   messages. A controlled, propeller-free logging validation can temporarily use
   `LOG_DISARMED = 1` through Full Parameters, followed by readback, to generate an
   onboard log without arming. Confirm a new non-empty BIN log is created, grows,
   downloads and can be parsed; then restore the recorded `LOG_DISARMED = 0`.
   Only do this with working media. Confirm logging errors do not recur and all
   enabled pre-arm checks pass. A switch transition or accepted command does not
   prove arming: only an armed heartbeat does. No actual arming test was run here.

The firmware's [logger parameter definitions](https://github.com/ArduPilot/ardupilot/blob/dbe79216/libraries/AP_Logger/AP_Logger.cpp)
and [arming definitions](https://github.com/ArduPilot/ardupilot/blob/dbe79216/libraries/AP_Arming/AP_Arming.cpp)
are the basis for these conditional changes. The hardware repair remains pending;
the investigation did not alter the vehicle.

## NextGen changes

- Logger messages and retained storage detail keep original reception timestamps.
- Retained logger state no longer re-adds expired pre-arm blockers. The existing
  30-second diagnostic lifetime applies; expiry is explicitly not proof of repair.
- Arming documents attribute logging reports to FC log storage, display timestamps
  and recent/stale status, and separate historical evidence from current blockers.
- Latest heartbeat time/state is independent of request stage and readiness.
- ARMING_SKIPCHK is reported from confirmed loaded parameters without applying
  the opposite legacy ARMING_CHECK editor semantics. Skip-mask editing remains in
  Full Parameters; no new automatic configuration writes were introduced.

Regression coverage includes retained logger expiry after state republication,
fresh repeated rejection, unknown readiness with logging blockers, RC requests
remaining disarmed, Markdown evidence timestamps, and skip-mask polarity.

### Changed files

- `src/Core/MissionPlanner.Core/Vehicles/Models/VehicleOnboardLoggingStatus.cs`
- `src/Core/MissionPlanner.Core/Vehicles/VehicleSession.cs`
- `src/Core/MissionPlanner.Core/Diagnostics/VehicleArmingDiagnostic.cs`
- `src/Core/MissionPlanner.Core/Diagnostics/VehicleLiveDiagnostics.Arming.cs`
- `src/Core/MissionPlanner.Core/Setup/Arming/ArmingSetupState.cs`
- `src/UI/MissionPlanner.App/Presentation/Documents/ArmingSetupDocumentFactory.cs`
- `src/Tests/MissionPlanner.Core.Tests/VehicleArmingDiagnosticTests.cs`
- `src/Tests/MissionPlanner.Core.Tests/ArmingRequestEvidenceTests.cs`
- `src/Tests/MissionPlanner.AvaloniaUI.Tests/ArmingDocumentTests.cs`
- This recovery guide.

### Verification

- Core tests: `dotnet test src/Tests/MissionPlanner.Core.Tests/MissionPlanner.Core.Tests.csproj --no-restore --filter "FullyQualifiedName~VehicleArmingDiagnosticTests|FullyQualifiedName~ArmingRequestEvidenceTests|FullyQualifiedName~BenchTelemetryReplayTests"` — **16 passed**.
- UI tests: `dotnet test src/Tests/MissionPlanner.AvaloniaUI.Tests/MissionPlanner.AvaloniaUI.Tests.csproj --no-restore --filter "FullyQualifiedName~ArmingDocumentTests"` — **14 passed**.
- Desktop: `dotnet build src/Platforms/MissionPlanner.Desktop/MissionPlanner.Desktop.csproj --no-restore -p:OutputPath=C:\Projects\MissionPlanner\artifacts\logging-validation\` — **0 errors, 26 existing warnings**. No CS1591/CS1587 warning was reported.
- Output logs: `artifacts/logging-core-tests.txt`, `artifacts/logging-ui-tests.txt`, `artifacts/logging-build.txt`.
- No physical storage repair, write/readback logging validation or motor/arming test was performed; those require usable logging hardware and the ordered procedure above. The desktop view was compiled and its document rendering tested, not manually inspected in a running GUI.
