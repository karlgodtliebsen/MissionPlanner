# MissionPlanner upstream comparison — 27 September 2026

## Scope and baseline

Source review of Servo Output, Hardware ID and ADS-B in Next Gen, plus all upstream commits since development diverged. No application behavior was changed and no hardware testing was performed.

- Next Gen checkout reviewed: `7425d0cb3`.
- First migration commit: `614da6ce8`, 11 June 2026, whose parent is upstream `3673a1777c02f8fa92d3088ecbb1ae8c2498f663` (PSC_POSZ_P rename fix).
- Current upstream master checked through GitHub's live API: `8cdd00fe2390841fc0d80b4c1d1462d4ce8bb0e5`, committed 25 September 2026.
- [Exact upstream comparison: 28 commits](https://github.com/ArduPilot/MissionPlanner/compare/3673a1777c02f8fa92d3088ecbb1ae8c2498f663...8cdd00fe2390841fc0d80b4c1d1462d4ce8bb0e5).

Dates below use **committer dates**, because several changes were authored months before they entered upstream history. This avoids incorrectly excluding those changes. The three configuration-page gaps below are older parity gaps, not newly added features in these 28 commits.

## A. Configuration-page findings

### Servo Output

Core editing is already present: function assignment, reversal, minimum, trim, maximum, live PWM, and explicit Write with vehicle readback confirmation. The page is not merely a function selector.

| Capability | Original MissionPlanner | Next Gen finding |
|---|---|---|
| Output count | 16, or 32 when `SERVO_32_ENABLE > 0` | Service hard-codes a maximum of 16. Outputs 17–32 cannot be configured here. |
| Live output display | Horizontal PWM bars with values | Text only in the Output PWM column. Graphical feedback is missing. |
| Function, reverse, min/trim/max | Individual parameter-bound controls | All are implemented, with an explicit Write workflow. This is a deliberate interaction difference, not a missing feature. |
| Incomplete parameter sets | Controls bind individually to available parameters | Next Gen excludes an entire row unless all five parameters exist. A partially supported output may disappear. |
| Function metadata | Loaded for each output's function parameter | Next Gen uses `SERVO1_FUNCTION` options for all rows. Usually equivalent, but less capability-specific. |
| Visible layout | Compact controls | Columns total about 1,035 pixels inside a maximum 900-pixel container. Min/trim/max can require horizontal scrolling and appear absent. |

Additional correctness finding: `OnVehicleStateUpdated` indexes telemetry using the position in `Outputs`, rather than `ChannelNumber - 1`. If an earlier output is omitted because of missing parameters, subsequent rows can display another channel's PWM. This should be corrected alongside output-count work.

Suggested work: correct channel indexing; discover supported output rows through 32; make individual unavailable fields explicit; add compact live PWM bars and reduce column widths. Preserve confirmed writes.

Sources:

- [Upstream ConfigRadioOutput.cs](https://github.com/ArduPilot/MissionPlanner/blob/8cdd00fe2390841fc0d80b4c1d1462d4ce8bb0e5/GCSViews/ConfigurationView/ConfigRadioOutput.cs)
- Next Gen: `src/UI/MissionPlanner.App/Views/InitSetup/MandatoryHardware/Sections/ServoOutputSetupView.axaml`, `ServoOutputSetupViewModel.cs`, `ServoOutputItemViewModel.cs`.
- Domain: `src/Core/MissionPlanner.Core/Setup/MandatoryHardware/ServoOutputConfigurationService.cs`.

### Hardware ID

The current checkout has **only an Information tab**, not a Configuration tab. It displays the shared Markdown setup report. `HardwareIdViewModel.Items`, `Board`, and `Firmware` are populated separately, but there is no dedicated item table bound to them in the view.

| Capability | Original MissionPlanner | Next Gen finding |
|---|---|---|
| Parameter name and raw ID | Table | Available in the report; IDs also receive hexadecimal formatting. |
| Bus type | Decoded I2C/SPI/UAVCAN/etc. | Missing. |
| Bus number and address | Separate decoded fields | Missing. |
| Device type | Sensor names for compass/barometer/airspeed/IMU; sensor ID for UAVCAN | Missing; current service only formats the number. |
| Editing IDs | Read-only diagnostics | Read-only is appropriate; this does not require write functionality. |

Suggested work: add a dedicated read-only identification table with decoded fields and raw ID retained. Unknown device types should remain identifiable by their numeric code. A Diagnostics label is more suitable than Configuration if a separate tab is retained.

Sources:

- [Upstream ConfigHWIDs.cs](https://github.com/ArduPilot/MissionPlanner/blob/8cdd00fe2390841fc0d80b4c1d1462d4ce8bb0e5/GCSViews/ConfigurationView/ConfigHWIDs.cs)
- [Upstream decoded DeviceInfo](https://github.com/ArduPilot/MissionPlanner/blob/8cdd00fe2390841fc0d80b4c1d1462d4ce8bb0e5/GCSViews/ConfigurationView/DeviceInfo.cs)
- Next Gen: `src/UI/MissionPlanner.App/Views/InitSetup/MandatoryHardware/Sections/HardwareIdView.axaml`, `HardwareIdViewModel.cs`.
- Domain: `src/Core/MissionPlanner.Core/Setup/MandatoryHardware/HwIdService.cs`, `src/Core/MissionPlanner.Core/Setup/Reporting/SetupReportService.cs`.

### ADS-B

Both implementations select reported `ADSB_*` and `AVD_*` parameters. Next Gen has working numeric/enum editing and per-setting Apply; it is not an empty placeholder. The main gaps are the editing controls and workflow.

| Capability | Original MissionPlanner | Next Gen finding |
|---|---|---|
| Enumerated values | Named choices | Implemented. |
| Bitmask values | Named checkbox bits | Missing; no bitmask projection/control, leaving numeric entry for these parameters. |
| Numeric metadata | Range, increment, units, descriptions; range-based controls | Generic numeric text entry. These metadata fields are not carried by `PeripheralSettingFactory`. |
| Search | Searches names/descriptions | No search in this page. |
| Favorites | Existing `fav_adsb` entries sort first | No equivalent page ordering. |
| Batch writing | Accumulates changed values, Write/Ctrl+S; sorts enable parameters last | Per-row Apply only. No batch ordering or Ctrl+S workflow here. |
| Refresh from vehicle | Explicit parameter-list download | Refresh rebuilds the page from the shared parameter registry; it does not itself request a new parameter list. |

Additional workflow issue: after a successful Apply, `MandatoryParameterViewModel` reloads and replaces **all** settings. Unapplied edits in other rows are discarded. This is a shared-viewmodel issue, so other pages using it should be considered when fixing it.

The upstream flight-ID/aircraft-registration packet handlers are commented out. They should **not** be counted as working upstream features missing in Next Gen.

Suggested work: expand the shared setting model with descriptions, units, range/increment and bit definitions; preserve dirty values; add batch Apply and a clear reload-from-vehicle action; add filtering. This can benefit several hardware pages without introducing another parameter-writing implementation.

Sources:

- [Upstream ConfigADSB.cs](https://github.com/ArduPilot/MissionPlanner/blob/8cdd00fe2390841fc0d80b4c1d1462d4ce8bb0e5/GCSViews/ConfigurationView/ConfigADSB.cs)
- Next Gen: `src/UI/MissionPlanner.App/Views/InitSetup/MandatoryHardware/Sections/AdsbView.axaml`, `AdsbViewModel.cs`, `MandatoryParameterViewModel.cs`, `PeripheralSettingViewModel.cs`.
- Domain: `src/Core/MissionPlanner.Core/Setup/MandatoryHardware/AdsbService.cs`, `MandatoryParameterServiceBase.cs`, `PeripheralSettingFactory.cs`.

## B. Upstream additions and fixes since the baseline

Each link points to the actual upstream commit. These are upstream changes, **not a claim that every item is absent or defective in Next Gen**. Next Gen has different implementations and needs targeted applicability checks before adopting them.

### New functionality and functional extensions

| Committed | Change | Upstream source |
|---|---|---|
| 16 June | Mount the vehicle's MAVFTP filesystem as a Windows drive through Dokan, with mount/unmount UI. Requires the Dokan driver. Also delays camera discovery while the communication port is reserved. | [0dc6408](https://github.com/ArduPilot/MissionPlanner/commit/0dc6408998190f20335b7bf9fcba72cd86e8769b) |
| 2 July | Plugin hook for custom HUD painting. The commit title says “HUB”, but the modified control is HUD. | [36c9c67](https://github.com/ArduPilot/MissionPlanner/commit/36c9c673cc8db3b1c5960222214c5c5132163c4b) |
| 2 July | Plugins can register/unregister Flight Data actions, including their placement in the action list. See September fix below before reproducing the original implementation. | [b6f3c25](https://github.com/ArduPilot/MissionPlanner/commit/b6f3c25d3c9c1a071c092e3a61b51ca39b762d9b) |
| 9 July | Log sorting recognizes `HIL_CONTROLS` as simulation evidence, in addition to `SIMSTATE`. | [c57b7e0](https://github.com/ArduPilot/MissionPlanner/commit/c57b7e0c8fb1c1d6b787a4d7986ddc3c9ec18dbe) |
| 11 July | Expands BIN/LOG sorting, filename-based loading and vehicle/system/board categorization; adds RLOG to the sort action. Also guards malformed vehicle-identification messages in LogBrowse. The source changes were reviewed, but classification accuracy was not runtime-tested. | [40309b4](https://github.com/ArduPilot/MissionPlanner/commit/40309b448a7c8bddec0d206e3f763eb880fbff75) |
| 17 September | MAVFTP supports `ListDirectoryWithTime` (opcode 16), with fallback to ordinary directory listing when unsupported; records optional modification timestamps. | [b90cae4](https://github.com/ArduPilot/MissionPlanner/commit/b90cae4b1641358225070400a3c09c2be2b0cfdc) |
| 17 September | Mounted MAVFTP drive reports vehicle modification times where available. | [ab2ee7c](https://github.com/ArduPilot/MissionPlanner/commit/ab2ee7c979815dc4856f351602fdfbb7dab1e8dd) |
| 17 September | MAVFTP browser adds modification dates, displayed in local time; unknown dates are blank. | [d03151d](https://github.com/ArduPilot/MissionPlanner/commit/d03151d1d118bda4412db6e8e2d5d4e703cd0f77) |
| 23 September | Flight Data adds reboot into USB mass-storage mode using `PREFLIGHT_REBOOT_SHUTDOWN`, parameter 1 = 5. Vehicle firmware/hardware must support it. | [1bb11f5](https://github.com/ArduPilot/MissionPlanner/commit/1bb11f578173bce0b3e3db6ace751590418a5c42) |

### Fixes and reliability improvements

| Committed | Change | Upstream source |
|---|---|---|
| 12 June | Restricts HTTP guided commands to loopback, constrains web-file paths and log embedded-file export paths, and changes connection handling to address resource exhaustion. July follows up on HTTP handling. | [ec2b6fc](https://github.com/ArduPilot/MissionPlanner/commit/ec2b6fcda70db8adc9fbec68273f35ac73c7dd81) |
| 12 June | Corrects ZedGraph axis-update/redraw ordering. | [4576278](https://github.com/ArduPilot/MissionPlanner/commit/4576278d37e05ae1e613717766a458bd080c350d) |
| 12 June | Adds missing French localization resources. | [b509df7](https://github.com/ArduPilot/MissionPlanner/commit/b509df75944ad7a2b9dbf77190b67cd07ad1fb03) |
| 2 July | HTTP follow-up: bounds concurrent/per-IP connections and rejects browser cross-site guided-command requests using Origin/Referer checks. | [980d2b8](https://github.com/ArduPilot/MissionPlanner/commit/980d2b8aba4414a7c0035c7e9cb3f4cc8701b40c) |
| 9 July | Corrupt DataFlash cache deserialization falls back to reading the log rather than failing there. | [0edac19](https://github.com/ArduPilot/MissionPlanner/commit/0edac1929813992d11e99a66439775023419dc21) |
| 9 July | Parses `TimeMS`/`T` using 64-bit integers to handle timestamps beyond the signed 32-bit range. | [f596729](https://github.com/ArduPilot/MissionPlanner/commit/f596729e202ebbdca4d7d32446a8a8e4a650bf96) |
| 9 July | Skips cache load/save without a filename; includes the full file path in cache identity, reducing same-name collisions; updates log map/index callers to filename-based loading. | [a4fd8cd](https://github.com/ArduPilot/MissionPlanner/commit/a4fd8cd98208a2a6e68ce3c38adeba37b2456c68) |
| 11 July | Null guards in gimbal/gimbal-manager discovery; unknown mission-command metadata guard; reduces state-update work during log indexing; adds explicit ignored message cases in CurrentState. | [a2fcd74](https://github.com/ArduPilot/MissionPlanner/commit/a2fcd74d64513d0d8fb43fbdd72020fa75aaa974) |
| 19 August | Fixes DroneCAN parameter enumeration skipping entries: semaphore starts unsignalled and reply-source/destination filtering is corrected. | [67a3c4f](https://github.com/ArduPilot/MissionPlanner/commit/67a3c4f22bd1b38ac499f9756902e04fa4ed8444) |
| 17 September | MAVFTP browser and mounted drive ignore nameless placeholders representing skipped directory entries. | [3324689](https://github.com/ArduPilot/MissionPlanner/commit/33246890795b9c8b5f0672623a3cd6749e20da02) |
| 17 September | Fixes built-in Flight Data actions throwing after plugin actions were introduced; uses dictionary `TryGetValue`. | [efb0801](https://github.com/ArduPilot/MissionPlanner/commit/efb080190de0bf091f9aab982c8848a124de7588) |
| 25 September | Only calls `Debugger.Break()` when a debugger is attached, avoiding Windows Error Reporting/UI hangs when the communication-port reservation is set twice. | [8cdd00f](https://github.com/ArduPilot/MissionPlanner/commit/8cdd00fe2390841fc0d80b4c1d1462d4ce8bb0e5) |

### Changes that should not be advertised as new end-user functionality

| Committed | Change | Upstream source |
|---|---|---|
| 16 / 19 June | SIMSTATE was initially wired into primary position/attitude/IMU state, then those assignments were commented out. Do not describe the first commit alone as active new simulation telemetry support. | [7f045a8](https://github.com/ArduPilot/MissionPlanner/commit/7f045a8fdd3a512436577b06a57b13dbb510accc), [4832720](https://github.com/ArduPilot/MissionPlanner/commit/48327200c67bd1cf265ece8a4b94fc14c0289a15) |
| 19 June | Git/SSH tracing in the beta release script. | [14840eb](https://github.com/ArduPilot/MissionPlanner/commit/14840eb0cd56b6ad824e05475383484d3213678f) |
| 29 August | Pins Android CI builds to Windows 2022. | [2b5589f](https://github.com/ArduPilot/MissionPlanner/commit/2b5589f4012f2f2b2d2d695b1233ee42943654aa) |
| 3 September | Adds codebase/workflow documentation for tooling. | [9515c88](https://github.com/ArduPilot/MissionPlanner/commit/9515c880441aa3467809d4af32329e8be1155d93) |
| 3 September | Removes Azure Pipelines and AppVeyor configurations. | [27d7dab](https://github.com/ArduPilot/MissionPlanner/commit/27d7dab8161a01e1c076d57a2eb56e9adb5c5dd3) |
| 7 September | Repairs Mac CI dependency installation for Xamarin/Mono. | [0cdb163](https://github.com/ArduPilot/MissionPlanner/commit/0cdb16308e751bedc649aa2ede3567ad96cd2ae8) |

## Suggested follow-up priorities

1. Fix Servo Output channel mapping and ADS-B loss of pending edits.
2. Complete the three page-level gaps above: 32 servo outputs/bars, decoded hardware table, richer shared parameter controls.
3. Assess MAVFTP timestamps, skipped entries and mass-storage reboot against Next Gen's existing services. These are concrete upstream additions with visible user benefit.
4. Review equivalent log timestamp/cache/export handling and DroneCAN enumeration. Adopt behavior and regression cases where applicable, not legacy implementation dependencies.
5. Treat Windows drive mounting and plugin extension hooks as separate product decisions; do not introduce platform-specific dependencies automatically.

Validation: local source inspection, Git ancestry verification, live GitHub compare/API queries, and upstream source/diff inspection. No application build or test run was needed for this documentation-only investigation. Runtime behavior and a complete Next Gen applicability audit for all 28 commits remain outside this review.

## Implementation follow-up

The three page-level gaps were implemented following this review:

- Servo Output discovers reported parameters through channel 32, keeps partial rows, uses per-output function options, maps live PWM by physical channel number, and displays compact PWM bars. Unsupported fields cannot be edited or written. Outputs without telemetry show an unavailable marker; adding configuration for outputs 17–32 does not fabricate live telemetry for them.
- Hardware ID now has a read-only Diagnostics table for raw identifiers, bus type, bus instance, address and sensor type. Unknown codes remain visible, and unrelated identifiers are not decoded as sensors.
- ADS-B now has metadata descriptions, units, numeric ranges/increments, named bitmask checkboxes, search, persisted favorites, Apply modified/Ctrl+S, and a full parameter download action. Batch writes put enable flags last and stop on the first unconfirmed change. Dirty edits survive per-row Apply and refresh on the same vehicle; they are not carried to a different vehicle.
- The shared mandatory-hardware parameter writer now waits for matching vehicle readback before reporting success. Failures leave pending edits intact. Pages without the rich bitmask editor retain their numeric fallback.

Validation: four core tests and four Avalonia/headless tests passed, including the existing hardware-refresh regression. The Avalonia test build compiled the changed XAML. Existing unrelated compiler warnings remain. No live controller writes or interactive visual verification were performed.
