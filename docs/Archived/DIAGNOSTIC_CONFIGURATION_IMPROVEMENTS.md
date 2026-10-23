# Diagnostic and configuration improvements

## 1. Flight modes

The dedicated Flight Modes page and Parameters Editor used different sources: the
former used the shared family catalogue (which omitted Copter Acro), while the
latter used parameter metadata. The preceding flight-mode fix added Acro, numeric
slot values, per-slot unknown placeholders and explicit Apply. This change verifies
Acro/unknown values in both editors. Catalogue membership does not establish support
in an individual firmware build. Unloaded values remain distinct from mode zero.

Files: `ArduPilotModeCatalog`, `FlightModeConfigurationService`,
`FlightModesSetupViewModel`, `ParameterItemViewModel`; tests
`FlightModeSetupTests`, `FlightModesViewModelTests`, `DiagnosticConfigurationTests`.
Opening, refreshing and selection changes remain read-only; slot Apply writes only
the deliberately changed slot.

## 2. HUD heading reference

The requested model is the **2D HUD**, as clarified by the user. Its cockpit horizon
already displays roll/pitch independently of yaw; there is no 3D camera transform to
reset. `HudHeadingReference` and `HudViewModel` provide a separate heading arrow:
Reset captures current yaw as zero (nose-away/up on screen), Restore removes the
offset. Compass, roll and pitch remain unchanged. `VehicleSession` timestamps actual
attitude observations; reset requires attitude no older than two seconds. Vehicle,
connection-token changes and offline state clear the offset. This UI has no command
or parameter-service dependency. Tests cover wraparound, multiple reset headings,
combined roll/pitch preservation, stale input and vehicle changes.

## 3. RC evidence

The compact bars show receiver PWM before FC reversal. New
`RadioInputInterpretation` applies piecewise calibrated dead-zone normalization and
reversal for Copter centered axes only. Channel details show actual MIN/TRIM/MAX,
DZ, REVERSED and explicitly received RCMAP evidence. Missing/ambiguous mapping or
calibration and stale input produce unavailable interpretation, not invented zeros.
The existing calibration workflow and its legacy fallback map remain unchanged;
fallback map labels are not evidence for the new direction calculation.

This is a **local interpretation**, not an FC-reported command or inferred physical
stick position. Other firmware families and throttle interpretation remain unavailable.
The disarmed verification instructions explain forward/backward pitch and left/right
roll. Tests cover reversal, observed 988/2011 endpoints, asymmetric ranges, dead zone,
missing versus zero parameters, remapped pitch and freshness.

Files: `RadioCalibrationService`, `RadioChannelInfo`, `RadioInputInterpretation`,
`RadioSetupView`, `RadioSetupViewModel`, `RadioChannelDetailsView`.

## 4. Inspector/export selection

The Inspector retained an independent selection without making the mismatch clear.
It now defaults to the active vehicle when opened unless the user pins its selection.
Retained historical vehicles remain selectable. The export banner shows name, IDs,
endpoint, connection state, last update and active-vehicle mismatch, even when sample
display is frozen. Exports always capture the latest collected snapshot, not the
frozen visible rows.

`CopyEvents` captures the selected ID before asynchronous work. The diagnostics
collector copies state, parameters, events and raw data for that ID and serializes
outside its ingestion lock. JSON includes capture time, data status and an identity-
and-time-based suggested filename. Tests cover reopening/pinning, retained data,
selection changes during copy and export identity. Last diagnostic update and recent
heartbeat are not a guarantee that every individual field is fresh.

Files: `LiveTelemetryInspectorView`, `LiveTelemetryInspectorViewModel`,
`VehicleLiveDiagnostics.Export`; tests `LiveTelemetryInspectorTests`,
`VehicleLiveDiagnosticsTests`.

## 5. Parameter baseline provenance

The old Default column was bound to `ParameterEditField.OriginalValue`: the first
received value in an editing session, not a firmware default. It is now **Baseline**.
Its tooltip distinguishes baseline, live FC value, explicit metadata default, and
unknown board/firmware factory default. `ParameterFieldMetadata.DefaultValue` carries
only a supplied metadata default. No reset action uses the baseline as a default.

Baseline survives refresh and readback in the same valid vehicle/firmware session.
Disconnect, vehicle or firmware changes invalidate that session. Creating the next
session captures new baseline values; pending edits from the old session must first
be discarded. The factory already enforces this scope. Calibration readback tests
show that old values remain baselines, while absent defaults remain Unknown.

Files: `ParameterFieldMetadata`, `ParameterEditSession`, `ParameterItemViewModel`,
`ParametersEditorTabView`, and existing baseline comparison labels.

## 6. Motor identities and diagram

Existing command construction was correct: parameter one is test sequence order,
not logical motor number or SERVO output. It is unchanged. `MotorLayoutDiagram`
uses the existing frame catalogue and output resolver, showing expected top-view
geometry, nose, rotation, test letter, logical motor and SERVO assignments. Missing
frame type no longer silently becomes Plus. Unsupported layouts and missing/duplicate
outputs remain explicit. No outputs are automatically remapped.

BetaFlightX ordering is A–2 front right, B–1 rear right, C–3 rear left, D–4 front
left. Quad X retains A–1, B–4, C–2, D–3. Tests verify both geometries and sequence
arguments at a mock encoder boundary, plus remapped and ambiguous output assignments.
The encoder test deliberately aborts before packet transmission. Dense/coaxial layouts
may need further diagram layout refinement; expected geometry does not prove wiring.

Files: `MotorLayoutResolver`, `MotorTestView`, `MotorTestViewModel`,
`MotorLayoutDiagram`; tests `MotorLayoutResolverTests`, `MotorTestCommandTests`,
`MotorOutputResolverTests`, `DiagnosticConfigurationTests`.

## 7. Flight-mode switch evidence

The page previously exposed slot highlighting without the full chain of evidence.
It now displays configured RC channel/raw PWM, selected slot, stored assignment and
heartbeat-reported active mode separately. Freshness is refreshed while the page is
active; missing/stale RC clears selection. A different active mode is not labelled a
successful switch: the UI directs users to vehicle messages for rejection or overrides.

The existing outer PWM limits were too broad. The shared ArduPilot six-position bands
are now 801–1230, 1231–1360, 1361–1490, 1491–1620, 1621–1749 and 1750–2199 µs.
800 and 2200 are invalid. Every transition and invalid outer value has regression
coverage. This is instantaneous local band selection; firmware debounce and mode
acceptance are not simulated. Three-position switches commonly select 1, 4 and 6,
depending on actual PWM. Existing firmware-family slot/channel routing remains intact.

## Sources and verification

- [ArduPilot RC normalization and six-position switch logic](https://github.com/ArduPilot/ardupilot/blob/master/libraries/RC_Channel/RC_Channel.cpp)
- [RC validity limits](https://github.com/ArduPilot/ardupilot/blob/master/libraries/RC_Channel/RC_Channel.h)
- [Frame layout and output-test sequence](https://github.com/ArduPilot/ardupilot/blob/master/libraries/AP_Motors/AP_MotorsMatrix.cpp)
- [Copter motor-test command handling](https://github.com/ArduPilot/ardupilot/blob/master/ArduCopter/motor_test.cpp)

Validation: `dotnet build src/UI/MissionPlanner.App/MissionPlanner.App.csproj --no-restore`
passed with 0 errors and 16 existing warnings. Focused `dotnet test --no-restore`
runs passed 91 Core tests and 25 Avalonia UI tests (including help). `git diff --check`
passed. The full solution/test suite was not run. No hardware, SITL,
physical stick direction, actual firmware mode support, or physical motor operation
was validated. No controller settings, transmitter settings, arming configuration or
ACRO_TRAINER were changed by this task.
