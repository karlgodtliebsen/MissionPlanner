# Codex Task 07 — Final Actions Parity Hardening, Tests, SITL Matrix, and Documentation

## Goal

Complete the FlightData Actions parity work after Tasks 01–06 by rerunning the portions of the previous hardening task that could not be completed while the backend/UI features were deferred.

This is primarily an audit, regression, integration-test, and documentation task. Do not use it to introduce unrelated features.

## 1. Update the existing parity document

Update, do not replace blindly:

`docs/flightdata-actions-parity.md`

The document must reflect the final deliberate NextGen semantics rather than describing Tasks 02–06 as unresolved.

At minimum classify and explain:

- Arm
- Disarm
- Set Flight Mode
- Auto shortcut
- Loiter/Hold
- RTL
- Land
- Takeoff
- Set Home Here
- legacy Set Home Alt -> **Zero Altitude / Reset Altitude** replacement
- Set Current WP
- Restart Mission
- Resume Mission
- Abort Landing
- Change Speed
- Change Altitude
- Set Loiter Radius
- Set Mount / gimbal
- Joystick
- Raw Sensor View
- Clear Track
- Message
- legacy Do Action
- NextGen Expert MAV CMD
- Reboot Autopilot

## 2. Document intentional semantic divergences

The parity document must explicitly record these deliberate decisions so future maintainers do not “restore” legacy bugs:

### Legacy Set Home Alt

Not replicated. Replaced with GCS-local Zero/Reset Altitude based on vehicle relative altitude. Does not change HOME.

### Restart Mission

Uses `MAV_CMD_DO_SET_MISSION_CURRENT` sequence 0 with reset flag true, therefore resets jump/completion state. Does not arm/change mode/start mission.

### Resume Mission

Does not rewrite/upload/truncate the onboard mission. Uses `MAV_CMD_DO_PAUSE_CONTINUE` continue=true and never arms/takes off/changes mode implicitly.

### Change Speed

Does not reproduce the legacy speed-type ambiguity. Copter/Rover use Ground speed; Plane exposes Airspeed/Ground speed explicitly. No hidden throttle behavior.

### Change Altitude

Uses typed modern Guided movement behavior with an absolute altitude above HOME. Does not use legacy mission-item `current=3` and does not auto-enter Guided mode.

### Set Loiter Radius

Is a persistent typed parameter write with direction-sign preservation, not a transient command.

## 3. Audit every Actions policy/capability

Verify semantic independence for at least:

- Arm
- Disarm
- Set Mode
- Takeoff
- Land
- Hold
- RTL
- Set Home Here
- Zero/Reset Altitude
- Reboot
- Set Current WP
- Restart Mission
- Resume Mission
- Abort Landing
- Change Speed
- Change Altitude
- Set Loiter Radius

No control may borrow a semantically unrelated policy merely because the current allow/deny rules happen to match.

Document any intentionally shared lower-level predicate while keeping semantic action capabilities distinct at the UI boundary.

## 4. Mission-state/snapshot regression audit

Verify Task 01 infrastructure against real combined usage:

- `MISSION_CURRENT` extension fields survive through decoder -> session -> policy/ViewModel state;
- verified snapshot requires matching non-zero mission IDs;
- mission ID change invalidates Abort Landing eligibility immediately;
- unverified ID=0 snapshot is never treated as verified for Abort Landing;
- current sequence resolves safely to the canonical mission item command.

Add missing regression tests if any path is not covered.

## 5. Multi-vehicle isolation tests

Create explicit two-vehicle automated scenarios with independent fake transports/sessions.

Cover at least:

1. existing flight action: Set Mode or RTL;
2. local presentation state: Zero Altitude;
3. mission action: Set Current WP or Resume;
4. command adjustment: Change Speed;
5. telemetry-confirmed adjustment: Change Altitude;
6. parameter operation: Set Loiter Radius.

Demonstrate that:

- ACK from A cannot complete B;
- mission telemetry from A cannot confirm B;
- position/altitude telemetry from A cannot confirm B;
- parameter value from A cannot confirm B;
- local zero reference from A never affects B;
- per-vehicle operation leases do not block unrelated vehicle B.

Do not use physical COM port names in automated tests.

## 6. Status model regression tests

The shared Actions status presentation must correctly distinguish operation families.

Cover:

- pending;
- command ACK accepted;
- command rejected/denied/unsupported;
- command timeout;
- cancellation;
- command accepted + telemetry confirmed;
- command accepted but telemetry confirmation unavailable/timed out;
- no-ACK setpoint sent + telemetry confirmed;
- no-ACK setpoint sent but telemetry timeout;
- mission legacy fallback confirmed only by post-request MISSION_CURRENT;
- parameter write confirmed;
- local GCS action applied without MAVLink ACK;
- sequential actions replacing/clearing stale status according to intended UX.

No status string/model may call a parameter confirmation or local Zero Altitude operation a MAVLink command ACK.

## 7. Binding/layout regression audit

Review final `ActionsTabView.xaml` and ViewModel bindings.

Verify specifically:

- Expert Command ID -> `ExpertCommandId` only;
- Takeoff altitude -> `TakeoffAltitudeMeters` only;
- Zero Altitude has no accidental command binding;
- mission sequence input -> mission sequence property only;
- speed input/type are independent;
- altitude input is independent from Takeoff altitude;
- loiter radius input is independent;
- every `Can...` maps to its matching semantic action;
- no raw command IDs/frames/type masks/parameter sign logic appear outside Expert MAV CMD / appropriate backend code;
- core controls remain visually primary;
- Mission intervention and In-flight adjustments remain compact/responsive.

Add static XAML/binding tests if the repository's testing style supports them.

## 8. SITL acceptance matrix

Update the parity document with a concise reproducible manual/SITL matrix.

### ArduCopter SITL

Verify where applicable:

- connect/disconnect;
- Arm/Disarm;
- mode setting;
- Takeoff/Land/Hold/RTL;
- Set Home Here vs Zero/Reset Altitude distinction;
- mission Set Current WP;
- Restart Mission;
- pause + Resume Mission;
- Ground speed change;
- Guided HOME-relative altitude target;
- Expert MAV CMD command-ID entry and normal status behavior.

### ArduPlane SITL

Verify where applicable:

- Set Current WP / Restart / Resume;
- Abort Landing with `LAND_ABORT_THR=1` and a mission actively executing NAV_LAND;
- Abort Landing denied outside its prerequisites;
- Plane Airspeed vs Ground speed;
- Guided HOME-relative altitude target using the implemented Plane adapter;
- `WP_LOITER_RAD` persistent change;
- negative-radius direction sign remains negative after magnitude change.

### Rover SITL

At minimum verify:

- Ground speed adjustment;
- unsupported Airspeed and Change Altitude are unavailable rather than sent.

For each test record:

- vehicle/firmware target;
- precondition/mode;
- operator action;
- protocol operation expected;
- ACK/telemetry/parameter confirmation expected;
- observed result.

If a specific SITL scenario cannot be automated in CI, retain a deterministic manual procedure rather than deleting the acceptance criterion.

## 9. Real-flight-controller bench checklist

Keep a short hardware verification checklist that emphasizes:

- props removed / propulsion made safe;
- selected vehicle identity checked before every command;
- two connected vehicles used only for safe isolation checks;
- no assumption about specific COM port names;
- destructive/in-flight-only operations tested in SITL unless a controlled test explicitly requires hardware;
- Zero Altitude verified as display-only;
- persistent Loiter Radius parameter changes restored after bench testing if appropriate.

## 10. Preserve deliberate exclusions

Do not re-add these merely for function-count parity:

- Joystick setup in Actions
- Raw Sensor View in Actions
- Clear Track in Actions
- Message utility in Actions
- Mount/Gimbal controls in Actions
- legacy generic Do Action menu

Record their correct NextGen location or deferred feature candidate in the parity document.

Expert MAV CMD remains the advanced generic escape hatch.

## 11. Full build/test gate

Before completion:

1. Build the full supported solution/configuration used by the repository's normal validation workflow.
2. Run all relevant test projects, not only new Actions tests.
3. Run any available mission/SITL integration suite practical for the repository.
4. Resolve failures introduced by Wave 2.
5. Do not introduce new warnings attributable to this work.

## Final acceptance criteria

This final task is complete only when:

- Tasks 01–06 behavior is actually implemented, not documented as deferred;
- the parity document reflects the final semantics above;
- independent capability gating is audited;
- mission snapshot freshness is covered;
- multi-vehicle isolation is covered across command, telemetry, parameter, and local-state operations;
- status semantics cannot overclaim confirmation;
- binding regressions are covered/audited;
- SITL acceptance procedures/results are documented;
- solution builds;
- relevant automated tests pass;
- intentionally excluded legacy utilities remain outside Actions.

The final Codex report must list:

- files changed;
- tests added/changed;
- exact build/test commands and results;
- SITL runs performed and results;
- any firmware-specific limitation still present;
- any follow-up feature candidate that is truly outside this Actions parity scope.
