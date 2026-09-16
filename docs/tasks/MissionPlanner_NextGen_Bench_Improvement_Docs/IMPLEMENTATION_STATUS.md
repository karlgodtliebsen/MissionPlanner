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
## Final cross-task verification

- Final complete solution build: passed, 28 warnings, zero errors; no CS1591/CS1587.
- Final Core suite: 663 passed, 6 skipped.
- Full repository script: BrowserBridge 1 passed; Firmware 292 passed/1 skipped;
  simulator smoke 8 passed/22 skipped; miscellaneous tests 54 passed/1 skipped;
  browser JavaScript 7 passed.
- Avalonia UI suite: 72 passed, 46 failed. All failures are in DfuWorkflowTests,
  FirmwarePanelLoadingTests, FirmwarePanelViewModelTests and FirmwarePlanViewModelTests.
  They dereference an unconfigured mock activeVehicle.Current at InstallFirmwareViewModel.cs:875.
  The implicated production code and firmware-test setup are unchanged from the starting revision.
  This unrelated test-fixture issue is not part of the bench changes.
- Full script logs: TestResults/all-tests/20260916-130645-169 (local, ignored artifacts).
- Final review additionally invalidated motor assistant display/evidence on disconnect, vehicle replacement
  or cancelled connection lifetime, with a focused regression test.
- No physical flight-controller, motor, sensor or interactive UI verification was performed.
- Concurrent edits to docs/tasks/ComingTasks/incomplete-tasks.txt and Views/Common/StatusBarView.axaml
  remain uncommitted. The latter includes the small unsupported-property correction described above.

## Task commits and changed files

Paths below are relative to the repository. This index lists each task's committed changes.
The final validation commit adds the motor session-boundary correction and this verification record.

### 1. Arming status and HUD — 86b57d97d

- docs/FEATURES.md
- docs/VEHICLE_CONNECTION.md
- docs/tasks/MissionPlanner_NextGen_Bench_Improvement_Docs/IMPLEMENTATION_STATUS.md
- src/Core/MissionPlanner.Core/Vehicles/Handlers/StatusTextHandler.cs
- src/Core/MissionPlanner.Core/Vehicles/Models/VehicleArmingStatus.cs
- src/Core/MissionPlanner.Core/Vehicles/Models/VehicleHudData.cs
- src/Core/MissionPlanner.Core/Vehicles/Models/VehicleState.cs
- src/Core/MissionPlanner.Core/Vehicles/VehicleHudDataService.cs
- src/Core/MissionPlanner.Core/Vehicles/VehicleRegistry.cs
- src/Core/MissionPlanner.Core/Vehicles/VehicleSession.cs
- src/Tests/MissionPlanner.Core.Tests/VehicleArmingStatusTests.cs
- src/UI/MissionPlanner.App/Views/FlightData/Hud/HudView.axaml
- src/UI/MissionPlanner.App/Views/FlightData/Hud/HudViewModel.cs

### 2. RC used-channel calibration — b4cb8070e

- docs/FEATURES.md
- docs/PARAMETERS.md
- docs/tasks/MissionPlanner_NextGen_Bench_Improvement_Docs/IMPLEMENTATION_STATUS.md
- src/Core/MissionPlanner.Core/Setup/MandatoryHardware/RadioCalibrationService.cs
- src/Core/MissionPlanner.Core/Setup/MandatoryHardware/RadioChannelCapture.cs
- src/Tests/MissionPlanner.Core.Tests/RadioSetupTests.cs
- src/UI/MissionPlanner.App/Views/InitSetup/MandatoryHardware/Sections/RadioSetupView.axaml
- src/UI/MissionPlanner.App/Views/InitSetup/MandatoryHardware/Sections/RadioSetupViewModel.cs

### 3. RC neutral diagnostics — 4043a80b5

- docs/FEATURES.md
- docs/PARAMETERS.md
- docs/tasks/MissionPlanner_NextGen_Bench_Improvement_Docs/IMPLEMENTATION_STATUS.md
- src/Core/MissionPlanner.Core/Setup/MandatoryHardware/RadioCalibrationService.cs
- src/Core/MissionPlanner.Core/Setup/MandatoryHardware/RadioChannelInfo.cs
- src/Core/MissionPlanner.Core/Setup/MandatoryHardware/RadioNeutralDiagnostic.cs
- src/Tests/MissionPlanner.Core.Tests/RadioSetupTests.cs
- src/UI/MissionPlanner.App/Views/InitSetup/MandatoryHardware/Sections/RadioSetupView.axaml
- src/UI/MissionPlanner.App/Views/InitSetup/MandatoryHardware/Sections/RadioSetupViewModel.cs

### 4. PC telemetry recording — 5cf60d6fd

- docs/FEATURES.md
- docs/TELEMETRY_RECORDING.md
- docs/tasks/MissionPlanner_NextGen_Bench_Improvement_Docs/IMPLEMENTATION_STATUS.md
- src/Core/MissionPlanner.Core/Configuration/DomainConfigurator.cs
- src/Core/MissionPlanner.Core/Replay/TelemetryRecordingService.cs
- src/Core/MissionPlanner.Core/Replay/TelemetryRecordingStatus.cs
- src/Core/MissionPlanner.MavLink/Services/Abstractions/IMavLinkTrafficRecording.cs
- src/Core/MissionPlanner.MavLink/Services/MavLinkConnection.cs
- src/Core/MissionPlanner.MavLink/Services/MavLinkInspectionTap.cs
- src/Tests/MissionPlanner.Core.Tests/TelemetryRecordingTests.cs
- src/UI/MissionPlanner.App/Views/FlightData/Tabs/TelemetryLogsTabView.axaml
- src/UI/MissionPlanner.App/Views/FlightData/Tabs/TelemetryLogsTabViewModel.cs

### 5. Onboard logger diagnostics — 3642b1b75

- docs/FEATURES.md
- docs/TELEMETRY_RECORDING.md
- docs/tasks/MissionPlanner_NextGen_Bench_Improvement_Docs/IMPLEMENTATION_STATUS.md
- src/Core/MissionPlanner.Core/Vehicles/Models/VehicleOnboardLoggingStatus.cs
- src/Core/MissionPlanner.Core/Vehicles/Models/VehicleState.cs
- src/Core/MissionPlanner.Core/Vehicles/VehicleRegistry.cs
- src/Core/MissionPlanner.Core/Vehicles/VehicleSession.cs
- src/Tests/MissionPlanner.Core.Tests/VehicleArmingStatusTests.cs
- src/Tests/MissionPlanner.Core.Tests/VehicleTests.cs
- src/UI/MissionPlanner.App/Views/FlightData/Tabs/TelemetryLogsTabView.axaml
- src/UI/MissionPlanner.App/Views/FlightData/Tabs/TelemetryLogsTabViewModel.cs

### 6. Accelerometer calibration — 89977770e

- docs/ACCELEROMETER_CALIBRATION.md
- docs/FEATURES.md
- docs/tasks/MissionPlanner_NextGen_Bench_Improvement_Docs/IMPLEMENTATION_STATUS.md
- src/Core/MissionPlanner.Core/Setup/MandatoryHardware/ArduPilotCalibrationService.cs
- src/Core/MissionPlanner.Core/Setup/MandatoryHardware/CalibrationOptions.cs
- src/Tests/MissionPlanner.Core.Tests/AccelerometerWorkflowTests.cs
- src/UI/MissionPlanner.App/Views/InitSetup/MandatoryHardware/Sections/AccelerometerSetupViewModel.cs

### 7. Compass diagnostics — ae6a718ec

- docs/COMPASS_DIAGNOSTICS.md
- docs/FEATURES.md
- docs/tasks/MissionPlanner_NextGen_Bench_Improvement_Docs/IMPLEMENTATION_STATUS.md
- src/Core/MissionPlanner.Core/Setup/MandatoryHardware/CompassConfigurationService.cs
- src/Core/MissionPlanner.Core/Setup/MandatoryHardware/CompassDiagnostics.cs
- src/Core/MissionPlanner.Core/Setup/MandatoryHardware/CompassInventory.cs
- src/Tests/MissionPlanner.Core.Tests/CompassDiagnosticsTests.cs
- src/UI/MissionPlanner.App/Views/InitSetup/MandatoryHardware/Sections/CompassSetupView.axaml
- src/UI/MissionPlanner.App/Views/InitSetup/MandatoryHardware/Sections/CompassSetupViewModel.cs

### 8. Motor threshold assistant — 499d8ce7d

- docs/FEATURES.md
- docs/MOTOR_START_THRESHOLDS.md
- docs/tasks/MissionPlanner_NextGen_Bench_Improvement_Docs/IMPLEMENTATION_STATUS.md
- src/Core/MissionPlanner.Core/Configuration/DomainConfigurator.cs
- src/Core/MissionPlanner.Core/Setup/OptionalHardware/Motor/MotorSpinParameterService.cs
- src/Core/MissionPlanner.Core/Setup/OptionalHardware/Motor/MotorStartThresholdService.cs
- src/Tests/MissionPlanner.Core.Tests/MotorSpinParameterServiceTests.cs
- src/Tests/MissionPlanner.Core.Tests/MotorStartThresholdTests.cs
- src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/MotorTestView.axaml
- src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/MotorTestViewModel.cs

### 9. Motor output diagnostics — ec465358d

- docs/FEATURES.md
- docs/MOTOR_OUTPUT_DIAGNOSTICS.md
- docs/tasks/MissionPlanner_NextGen_Bench_Improvement_Docs/IMPLEMENTATION_STATUS.md
- src/Core/MissionPlanner.Core/Setup/OptionalHardware/Motor/MotorOutputDiagnostics.cs
- src/Tests/MissionPlanner.Core.Tests/MotorOutputDiagnosticsTests.cs
- src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/MotorTestView.axaml
- src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/Sections/MotorTestViewModel.cs

### 10. Replay regressions — e235c208a

- docs/BENCH_TELEMETRY_REPLAY.md
- docs/FEATURES.md
- docs/tasks/MissionPlanner_NextGen_Bench_Improvement_Docs/IMPLEMENTATION_STATUS.md
- src/Core/MissionPlanner.Core/Replay/ImmediateReplayDelay.cs
- src/Core/MissionPlanner.Core/Replay/ReplayTelemetryPipeline.cs
- src/Core/MissionPlanner.Core/Vehicles/Handlers/StatusTextHandler.cs
- src/Tests/MissionPlanner.Core.Tests/BenchTelemetryReplayTests.cs
- src/Tests/MissionPlanner.Core.Tests/Fixtures/BenchReplayFixtures.cs
