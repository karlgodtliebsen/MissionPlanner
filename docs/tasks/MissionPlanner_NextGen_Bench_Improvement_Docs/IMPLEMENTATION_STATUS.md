# Bench improvement task progress

Source: [Task set](MissionPlanner_NextGen_Codex_Tasks.md).

Each task is implemented, verified, documented, and committed separately.

| Task | Status |
| --- | --- |
| 1. Arming status and HUD | Complete |
| 2. RC calibration used channels | Complete |
| 3. RC neutral diagnostics | Complete |
| 4. PC telemetry recording | Complete |
| 5. Onboard logging diagnostics | Complete |
| 6. Accelerometer calibration | Complete |
| 7. Compass diagnostics | Complete |
| 8. Motor start threshold assistant | Complete |
| 9. Motor output diagnostics | Complete |
| 10. Telemetry replay regression harness | Complete |

## Task 1 verification

- Complete solution build: passed (18 existing warnings, zero errors; no CS1591/CS1587).
- Focused arming tests: 9 passed.
- Core regression suite: 621 passed, 6 skipped.
- Hardware/interactive HUD verification: not run.
- Readiness is unknown when pre-arm support/enabling is absent. Arm rejection is retained until reset; readiness or arming clears the pre-arm reason.

## Task 2 verification

- Radio tests: 27 passed; Core suite: 625 passed, 6 skipped.
- Complete solution build passed (18 existing warnings, zero errors; no XML documentation warnings).
- Known mapping parameters drive strict validation and MIN/MAX/TRIM writes; assignments are revalidated at write time.
- Interactive/hardware calibration not run.

## Task 3 verification

- Radio tests: 32 passed; Core suite: 630 passed, 6 skipped.
- Complete solution build passed (18 existing warnings, zero errors).
- Observed center is sampled only during neutral review. Live neutral assessment requires fresh telemetry and downloaded trim/dead-zone.
- Interactive/hardware verification not run; no parameter writes are triggered by diagnostics.

## Task 4 verification

- Recording tests: 4 passed; Core suite: 634 passed, 6 skipped.
- Complete solution build passed (18 existing warnings, zero errors).
- Exact tlog framing, direction limitations, overflow/error behavior, configurable directory, and disconnect flushing documented in TELEMETRY_RECORDING.md.
- Physical bench connection and interactive UI verification not run.

## Task 5 verification

- Arming/logger tests: 12 passed; Core suite: 637 passed, 6 skipped.
- Complete solution build passed (18 existing warnings, zero errors).
- Simulator command-ack test now awaits connection startup; the previous fire-and-forget startup raced its first send after recording initialization.
- Free space remains explicitly unavailable rather than inferred from unrelated camera storage. Hardware/UI verification not run.

## Task 6 verification

- Five fake-MAVLink workflow tests passed; Core suite: 642 passed, 6 skipped.
- Complete solution build passed (19 warnings, zero errors; no XML documentation warnings).
- Extended the existing workflow and view; fixed connection ownership, synchronous-response transition ordering, and bounded full calibration/refresh lifetimes.
- Physical calibration remains unverified.

## Task 7 verification

- Compass diagnostics: 6 tests passed; Core suite: 648 passed, 6 skipped.
- Complete solution build passed (18 warnings, zero errors).
- Existing Compass view extended; unknown and per-instance health limitations remain explicit.
- Physical sensor verification not run. Source references are in COMPASS_DIAGNOSTICS.md.

## Task 8 verification

- Threshold/spin tests: 17 passed; Core suite: 652 passed, 6 skipped.
- Complete solution build passed (39 warnings, zero errors; no CS1591/CS1587).
- Existing motor/output/parameter services reused. Exactly 20% minimum spin is allowed; default recommendation matches 0.17/0.20.
- Physical motor tests not run. Non-atomic write behavior is documented.

## Task 9 verification

- Motor summary tests: 2 passed, existing mapping tests also passed; Core suite: 654 passed, 6 skipped.
- Complete solution build passed (39 warnings, zero errors).
- Existing view/viewmodel and output/layout resolvers reused; diagnostic section performs no writes.
- Board timer groups and effective protocol/reboot state remain explicitly unverified.
- Physical/UI verification not run.
## Task 10 verification

- Seven synthetic scenarios plus chunk-order/reset regression passed; Core suite: 662 passed, 6 skipped.
- Existing replay tests cover scaled timing, pause, seek, close and transmission isolation.
- Complete solution build passed (16 warnings, zero errors; no CS1591/CS1587).
- Replay uses the normal decoder and shared status-text handler; chunk expiry follows recorded time.
- During validation, a concurrent status-bar edit had an unsupported SelectableTextBlock property.
  The property was removed to restore the build; that unrelated file remains outside the task commit.
- Fixture sources and execution instructions are in BENCH_TELEMETRY_REPLAY.md.