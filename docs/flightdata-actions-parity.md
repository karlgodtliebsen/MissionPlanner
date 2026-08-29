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
| Set Home Alt | Deferred because semantics/support remain unresolved | Legacy is a local display offset, but its arithmetic contradicts its tooltip. See the Task 02 investigation. |
| Set Current WP | Deferred because semantics/support remain unresolved | Protocol is known; selected-vehicle confirmation and complete mission-operation backend remain prerequisites. |
| Restart Mission | Deferred because semantics/support remain unresolved | Legacy sets sequence 0 only; whether to adopt the newer reset flag needs a product decision. |
| Resume Mission | Deferred because semantics/support remain unresolved | Legacy rewrites the mission and may arm/take off; a transactional, recoverable workflow is required. |
| Abort Landing | Deferred because semantics/support remain unresolved | Requires current-item command state to prove Plane AUTO `NAV_LAND` applicability. |
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
and Expert Command retain their independent policy entries. Deferred actions have no control
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

Explicit two-vehicle regression coverage for a new mission intervention and adjustment
cannot be added until those typed operations exist. This remains a required acceptance gate
for their future implementation.

## SITL manual verification matrix

| Check | Preconditions and action | Expected result |
|---|---|---|
| Connect/disconnect | Connect one SITL vehicle, then disconnect. | Controls follow policy; pending work cancels without targeting another session. |
| Arm/Disarm | On ground with fresh telemetry. | Real ACK shown; armed state confirmed by telemetry. |
| Set Mode | Choose a family-supported mode. | ACK followed by matching mode telemetry. |
| Takeoff/Land/Hold/RTL | Use appropriate armed/airborne states. | Independent enablement; ACK and meaningful telemetry confirmation. |
| Set Home Here | Confirm with fresh 3D position. | HOME command ACK; vehicle HOME changes. |
| Set Home Alt | Not available. | Resolve Task 02 semantic decision before testing. |
| Mission intervention | Not available. | Complete Task 03 prerequisites and test exact current item/state transitions. |
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
