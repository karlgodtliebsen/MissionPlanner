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
| Set Current WP | Backend implemented; UI pending | Uses acknowledged `DO_SET_MISSION_CURRENT`, post-request `MISSION_CURRENT`, and only an explicit unsupported-result legacy fallback. |
| Restart Mission | Backend implemented; UI pending | Uses `DO_SET_MISSION_CURRENT` sequence 0 with reset flag; no legacy fallback, mode change, arm, or mission start. |
| Resume Mission | Backend implemented; UI pending | Uses `DO_PAUSE_CONTINUE` only when telemetry positively identifies paused/suspended execution. |
| Abort Landing | Backend implemented; UI pending | Plane AUTO only, with active execution, ID-verified current `NAV_LAND`, and enabled `LAND_ABORT_THR`. |
| Change Speed | Deferred because semantics/support remain unresolved | Legacy mixes airspeed, ground-speed, and throttle sources while always encoding speed type 0. |
| Change Altitude | Deferred because semantics/support remain unresolved | Proven as absolute HOME-relative guided mission-item behavior; typed protocol support is missing. |
| Set Loiter Radius | Deferred because semantics/support remain unresolved | Proven persistent parameter write; needs explicit typed parameter UX, not a temporary-command label. |
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

## SITL manual verification matrix

| Check | Preconditions and action | Expected result |
|---|---|---|
| Connect/disconnect | Connect one SITL vehicle, then disconnect. | Controls follow policy; pending work cancels without targeting another session. |
| Arm/Disarm | On ground with fresh telemetry. | Real ACK shown; armed state confirmed by telemetry. |
| Set Mode | Choose a family-supported mode. | ACK followed by matching mode telemetry. |
| Takeoff/Land/Hold/RTL | Use appropriate armed/airborne states. | Independent enablement; ACK and meaningful telemetry confirmation. |
| Set Home Here | Confirm with fresh 3D position. | HOME command ACK; vehicle HOME changes. |
| Zero / Reset Altitude | Requires a finite relative altitude to create; reset remains available while active. | Local presentation state only; HUD displays `relative altitude - local reference`, while MSL fallback remains unmodified. |
| Mission intervention | Backend available; Actions UI pending. | Modern current/reset/pause commands retain distinct ACK and telemetry-confirmation results; abort remains strongly gated. |
| In-flight adjustments | Not available. | Complete Task 05 prerequisites; distinguish ACK from parameter readback. |
| Expert MAV CMD | Enter a safe test command ID and seven parameters. | ID remains independent of takeoff altitude; confirmation and real ACK are shown. |
| Sequential status | Run two safe commands in sequence. | New pending/result state replaces stale command presentation. |

## Real-controller bench verification

Remove propellers and otherwise make motors safe. Use separate mocked/SITL validation before
connecting multiple real vehicles. Confirm the selected vehicle identity before every
command and never assume fixed COM-port names. Limit bench testing to ground-safe operations;
exercise takeoff, landing, mission intervention, and in-flight adjustments in SITL unless a
controlled flight-test plan explicitly authorizes hardware execution. Confirm actual ACKs
and telemetry rather than inferring success from button activation.

## Follow-up feature candidates

- Dedicated joystick/input setup.
- Sensor diagnostics workspace.
- Payload/gimbal workspace.
- Optional message-sending tool in the Messages area.
- Map-track display controls outside Actions.
