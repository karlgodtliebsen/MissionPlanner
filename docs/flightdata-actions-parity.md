# FlightData Actions parity

This document records deliberate parity decisions between legacy MissionPlanner FlightData
Actions and the NextGen Actions tab. Parity means preserving useful, proven operations—not
copying the legacy button matrix.

## Capability decisions

| Capability | Classification | NextGen status and rationale |
|---|---|---|
| Arm | Keep / Already present | Typed, policy-gated acknowledged action in FlightData > Actions. |
| Disarm | Keep / Already present | Typed action with confirmation when airborne. |
| Set Flight Mode | Keep / Already present | Uses the firmware-family mode catalog. |
| Auto shortcut | Keep / Already present | AUTO is selected through Set Flight Mode; a duplicate shortcut is unnecessary. |
| Loiter / Hold | Keep / Already present | Typed family-aware mode action with independent `CanHoldPosition`. |
| RTL | Keep / Already present | Typed family-aware mode action with independent `CanReturnToLaunch`. |
| Land | Keep / Already present | Typed mode action with independent `CanLand`. |
| Takeoff | Keep / Already present | Typed relative-altitude action with confirmation. |
| Set Home Here | Keep / Already present | Acknowledged `DO_SET_HOME` operation that changes vehicle HOME. |
| Zero / Reset Altitude | Keep / Implemented with clarified semantics | Per-session GCS-local relative-altitude display reference. It never modifies vehicle HOME or sends MAVLink. |
| Set Current WP | Implemented | Compact mission section sends the displayed canonical sequence through acknowledged `DO_SET_MISSION_CURRENT`, with post-request telemetry and explicit-unsupported fallback only. |
| Restart Mission | Implemented | Confirmed UI uses sequence 0 plus reset; help text makes clear that it does not arm or change mode. |
| Resume Mission | Implemented | Independently enabled only for positively identified paused/suspended execution and uses pause/continue semantics. |
| Abort Landing | Implemented | Plane-only control; enabled only for AUTO, active execution, ID-verified current `NAV_LAND`, and enabled `LAND_ABORT_THR`. |
| Change Speed | Implemented | Plane exposes Airspeed/Ground speed; Copter/Rover expose Ground speed only. Throttle is fixed at no-change and ACK acceptance is not mislabeled telemetry confirmation. |
| Change Altitude | Implemented | UI explicitly requests an absolute target above HOME and is enabled only in supported Guided mode with position; it never changes mode. |
| Set Loiter Radius | Implemented | Positive-magnitude UI identifies the persistent write; backend chooses the available parameter, preserves direction sign, and requires value confirmation. |
| Set Mount / gimbal | Moved / Belongs elsewhere | Not present in NextGen; candidate for a dedicated payload/gimbal workspace, not Actions. |
| Joystick | Moved / Belongs elsewhere | Not present in NextGen; candidate for dedicated input-device setup. |
| Raw Sensor View | Moved / Belongs elsewhere | Not present in NextGen; candidate for a diagnostics workspace. |
| Clear Track | Intentionally not replicated | Map-display utility does not belong among vehicle commands. |
| Message | Moved / Belongs elsewhere | FlightData has a dedicated Messages tab; a future send-message utility belongs there if required. |
| Legacy Do Action | Intentionally not replicated | An untyped generic command menu conflicts with the typed action model. |
| NextGen Expert MAV CMD | Expert-only generic command | Retained as an advanced confirmed escape hatch; Command ID binds only to `ExpertCommandId`. |
| Reboot Autopilot | Keep / Already present | Typed, ground-only, confirmed and acknowledged action. |

## Policy and binding audit

Every implemented operator action evaluates its semantically named `VehicleAction`. Land,
Hold, and RTL now publish distinct UI capabilities even though the current policy shares a
common in-flight validation primitive. Arm, Disarm, Set Mode, Takeoff, Reboot, Set Home Here,
Expert Command, and the four mission interventions retain independent policy entries. Deferred actions have no control
and no misleading capability property.

The takeoff input binds only to `TakeoffAltitudeMeters`. Expert Command ID binds only to
`ExpertCommandId`; its seven parameters remain separate. No ordinary control constructs raw
MAVLink parameters.

## Command status and vehicle isolation

Existing actions capture the active `VehicleId`, run through `AsyncOperationRunner`, and
call typed services with that identity. The command service resolves the corresponding
vehicle session and simulation channel, uses a per-vehicle operation lease, and correlates
ACKs by vehicle and command. The shared status panel distinguishes pending, accepted,
rejected, timeout, cancellation, and accepted-without-telemetry-confirmation outcomes.

Mission interventions use the same per-vehicle operation gate while retaining independent
operation across vehicles. ACKs and post-request `MISSION_CURRENT` observations are correlated
to the exact vehicle; pre-request state cannot satisfy confirmation.

## Deliberate divergences from legacy Actions

- **Set Home Alt is not reproduced.** Zero/Reset Altitude is a per-session GCS display reference
  based only on relative altitude. It never changes HOME and is never reported as a MAVLink ACK.
- **Restart Mission resets execution state.** It sends `DO_SET_MISSION_CURRENT` with canonical
  sequence 0 and reset=true, resetting jump/completion state without arming, changing mode, or
  issuing mission start. Unsupported firmware is not degraded to the legacy message.
- **Resume Mission is non-destructive.** It sends `DO_PAUSE_CONTINUE` continue=true only when
  pause/suspend telemetry is positive. It never downloads, truncates, rewrites, uploads, arms,
  takes off, or changes mode.
- **Change Speed is semantic.** Copter/Rover use Ground speed; Plane explicitly selects Airspeed
  or Ground speed. Throttle remains no-change and is not an operator input.
- **Change Altitude is a Guided setpoint.** The value is absolute above HOME. The implementation
  uses a typed `GLOBAL_RELATIVE_ALT_INT` position target at current lat/lon, never the legacy
  mission-item `current=3` form, and never enters Guided automatically.
- **Set Loiter Radius is persistent.** It writes the one available typed parameter, preferring
  `WP_LOITER_RAD`, and preserves the existing direction sign while replacing magnitude.

## Capability audit

Every UI control owns a matching semantic capability: `CanArm`, `CanDisarm`, `CanTakeoff`,
`CanLand`, `CanHoldPosition`, `CanReturnToLaunch`, `CanSetHome`, `CanToggleAltitudeZero`,
`CanRestartMission`, `CanResumeMission`, `CanAbortLanding`, `CanSetCurrentMissionItem`,
`CanChangeSpeed`, `CanChangeAltitude`, and `CanSetLoiterRadius`. Land/Hold/RTL intentionally
share a lower-level fresh-in-flight predicate, while remaining independent policy entries and
bindings. Mission and adjustment services defensively repeat family/mode/state checks before
transmission. Expert command input, takeoff altitude, mission sequence, speed, guided altitude,
and radius are separate properties.

Status is confirmation-family aware: speed reports command ACK; mission operations distinguish
ACK, post-request telemetry, and no-ACK legacy fallback; Guided altitude reports target sent or
telemetry reached without inventing an ACK; radius reports parameter confirmation; Zero/Reset
reports a local display operation.

## SITL manual verification matrix

| Check | Preconditions and action | Expected result |
|---|---|---|
| Connect/disconnect | Connect one SITL vehicle, then disconnect. | Controls follow policy; pending work cancels without targeting another session. |
| Arm/Disarm | On ground with fresh telemetry. | Real ACK shown; armed state confirmed by telemetry. |
| Set Mode | Choose a family-supported mode. | ACK followed by matching mode telemetry. |
| Takeoff/Land/Hold/RTL | Use appropriate armed/airborne states. | Independent enablement; ACK and meaningful telemetry confirmation. |
| Set Home Here | Confirm with fresh 3D position. | HOME command ACK; vehicle HOME changes. |
| Zero / Reset Altitude | Requires a finite relative altitude to create; reset remains available while active. | Local presentation state only; HUD displays `relative altitude - local reference`, while MSL fallback remains unmodified. |
| Mission intervention | Use the subordinate Actions section after downloading/verifying the onboard mission. | Modern current/reset/pause commands retain distinct ACK and telemetry-confirmation results; abort remains strongly gated and Plane-only. |
| In-flight adjustments | Use the subordinate Actions section in a supported mode. | Speed reports command ACK, altitude reports setpoint/telemetry state without a fake ACK, and radius reports persistent parameter confirmation. |
| Expert MAV CMD | Enter a safe test command ID and seven parameters. | ID remains independent of takeoff altitude; confirmation and real ACK are shown. |
| Sequential status | Run two safe commands in sequence. | New pending/result state replaces stale command presentation. |

### Firmware-specific Wave 2 matrix

| Target | Precondition / mode | Action | Expected protocol and evidence |
|---|---|---|---|
| Copter SITL | Connected, normal safe ground/flight states | Arm, Disarm, Set Mode, Takeoff, Land, Hold, RTL | Existing typed command; real ACK and applicable telemetry. |
| Copter SITL | Relative altitude available | Zero, climb, Reset Altitude | No packet; HUD becomes 0, tracks the local delta, then returns to normal relative altitude. HOME is unchanged. |
| Copter SITL | Small uploaded mission; AUTO/paused as applicable | Set Current WP, Restart, Resume | Command 224 with reset false/true or pause/continue; matching post-request mission telemetry. No arm/mode/upload side effects. |
| Copter SITL | Guided with valid position | Ground speed; target altitude above HOME | `DO_CHANGE_SPEED` ground type with ACK; global HOME-relative setpoint with no ACK and later relative-altitude confirmation. |
| Plane SITL | Uploaded mission and paused state | Set Current WP, Restart, Resume | Same typed mission semantics and confirmation boundaries as Copter. |
| Plane SITL | AUTO actively executing ID-verified `NAV_LAND`; `LAND_ABORT_THR=1` | Abort Landing | `DO_GO_AROUND` ACK means accepted, not maneuver complete. Repeat outside every prerequisite and verify denial/no send. |
| Plane SITL | Supported navigation mode | Select Airspeed, then Ground speed | Distinct semantic `DO_CHANGE_SPEED` types; no throttle mutation. |
| Plane SITL | Guided with valid position | Target altitude above HOME | Typed `GLOBAL_RELATIVE_ALT_INT` target; no mode change and no fabricated ACK. |
| Plane SITL | `WP_LOITER_RAD` loaded, first positive then negative | Set radius magnitude | One parameter write and exact value confirmation; negative direction remains negative. Restore the original value afterward. |
| Rover SITL | AUTO or Guided | Ground speed | Ground-speed command ACK. Airspeed selector is absent. |
| Rover SITL | Any mode | Change Altitude | Control remains unavailable and no target is sent. |

Record firmware version, vehicle identity, starting mode/state, selected action, expected packet,
and observed ACK/telemetry/parameter result for each run. These procedures are deterministic
manual acceptance tests; no Wave 2 SITL instance was available during the unit-test run recorded
with this change.

## Real-controller bench verification

Remove propellers and otherwise make motors safe. Use separate mocked/SITL validation before
connecting multiple real vehicles. Confirm the selected vehicle identity before every
command and never assume fixed COM-port names. Limit bench testing to ground-safe operations;
exercise takeoff, landing, mission intervention, and in-flight adjustments in SITL unless a
controlled flight-test plan explicitly authorizes hardware execution. Confirm actual ACKs
and telemetry rather than inferring success from button activation.
Verify Zero Altitude is display-only. Restore any persistent loiter-radius parameter changed
during bench testing. Two connected controllers may be used only for propulsion-safe isolation
checks, and the selected vehicle identity must be rechecked before every operation.

## Follow-up feature candidates

- Dedicated joystick/input setup.
- Sensor diagnostics workspace.
- Payload/gimbal workspace.
- Optional message-sending tool in the Messages area.
- Map-track display controls outside Actions.
