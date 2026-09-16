# Bench improvement task progress

Source: [Task set](MissionPlanner_NextGen_Codex_Tasks.md).

Each task is implemented, verified, documented, and committed separately.

| Task | Status |
| --- | --- |
| 1. Arming status and HUD | Complete |
| 2. RC calibration used channels | Pending |
| 3. RC neutral diagnostics | Pending |
| 4. PC telemetry recording | Pending |
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
