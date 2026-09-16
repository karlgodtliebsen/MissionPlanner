# Bench improvement task progress

Source: [Task set](MissionPlanner_NextGen_Codex_Tasks.md).

Each task is implemented, verified, documented, and committed separately.

| Task | Status |
| --- | --- |
| 1. Arming status and HUD | Complete |
| 2. RC calibration used channels | Complete |
| 3. RC neutral diagnostics | Complete |
| 4. PC telemetry recording | Complete |
| 5. Onboard logging diagnostics | Pending |
| 6. Accelerometer calibration | Pending |
| 7. Compass diagnostics | Pending |
| 8. Motor start threshold assistant | Pending |
| 9. Motor output diagnostics | Pending |
| 10. Telemetry replay regression harness | Pending |

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
