# Codex Task 04 — Mission Intervention UI

## Goal

Expose the typed mission intervention backend from Task 03 in the FlightData > Actions UI without recreating the legacy button grid.

Required operations:

- Set Current WP
- Restart Mission
- Resume Mission
- Abort Landing

## Preconditions

Tasks 01 and 03 must be complete and green.

The UI must bind only to typed application/domain operations from Task 03.

## 1. Add a compact Mission intervention section

Add a secondary section named **Mission intervention**.

Use the application's existing collapsible/expander pattern if one exists. Keep it visually subordinate to Arm/Disarm/RTL/Land/Takeoff and normal mode controls.

Do not expose MAVLink command IDs, reset bits, ACK enums, or raw mission protocol fields.

## 2. Set Current WP UI

Provide a canonical mission sequence input/selector and a **Set Current WP** button.

Preferred presentation when a verified/current mission snapshot is available:

```text
0 — NAV_WAYPOINT
1 — DO_CHANGE_SPEED
2 — NAV_WAYPOINT
...
```

Use a concise user-readable command name if the repository already has command metadata. The number shown/sent must remain the canonical MAVLink sequence, not a one-based UI row number.

If only valid mission bounds are available but item metadata is not, a bounded numeric sequence input is acceptable.

If mission bounds are unknown, disable the operation rather than accepting an unvalidated arbitrary index.

Show current sequence where practical.

## 3. Restart Mission UI

Add **Restart Mission** with help text equivalent to:

> Resets the mission to its first item and resets mission execution state/jump counters. Does not arm the vehicle or change flight mode.

Use the application's established confirmation dialog pattern because this materially changes mission execution state.

At minimum require confirmation when the vehicle is armed or the mission is active. It is acceptable to confirm unconditionally if that is the existing UX convention for disruptive mission actions.

Do not present Restart as “Start Mission”.

## 4. Resume Mission UI

Add **Resume Mission** with help text equivalent to:

> Continues a paused onboard mission from the autopilot's current mission position. Does not rewrite the mission, arm, take off, or change flight mode.

The control is independently gated by `CanResumeMission` or the equivalent policy from Task 03.

Do not add a resume-waypoint picker to this control. If the operator wants another waypoint, the intended composition is:

```text
Set Current WP -> Resume Mission
```

## 5. Abort Landing UI

Expose **Abort Landing** only in the Plane/fixed-wing context.

Recommended behavior:

- hidden for vehicle families where the action is not applicable;
- visible but disabled for Plane when current prerequisites are not met;
- if the policy framework provides denial reasons, surface a concise reason in tooltip/help text, e.g.:
  - AUTO mode required;
  - current landing item not verified;
  - landing abort disabled by `LAND_ABORT_THR`.

The label must remain explicit: **Abort Landing**.

Do not substitute RTL or a mode switch.

## 6. ViewModel requirements

Extend `ActionsTabViewModel` or the current equivalent with semantically named properties/commands such as:

- selected/current mission sequence;
- `CanSetCurrentMissionItem`;
- `CanRestartMission`;
- `CanResumeMission`;
- `CanAbortLanding`;
- async commands for the four typed operations.

Requirements:

- no raw MAVLink construction;
- no direct parameter lookup for LAND_ABORT_THR in the ViewModel if policy/service already owns it;
- selected vehicle/session targeting through existing architecture;
- refresh availability from observable mission execution/snapshot/policy state, not polling;
- pending state prevents duplicate submissions according to Task 03 operation serialization.

## 7. Status presentation

Map typed backend results to the existing Actions status area accurately.

Examples of distinct states that must not be collapsed into one misleading “Success”:

- Pending
- Command accepted
- Telemetry confirmed
- Accepted, telemetry confirmation unavailable/timed out
- Unsupported
- Rejected/Denied
- Timeout
- Cancelled
- Legacy set-current fallback confirmed by mission telemetry

For Abort Landing, “Command accepted” must not be phrased as “Landing aborted successfully/completed”.

## 8. Responsive layout

Ensure the section remains usable on narrow/mobile layouts:

- inputs and buttons must wrap/stack using existing responsive conventions;
- no fixed desktop-only width assumptions;
- do not push Expert MAV CMD or core flight controls into an unusable layout.

## Acceptance tests

Automated ViewModel/UI tests must demonstrate at least:

1. Set Current WP sends the selected canonical sequence to the typed backend.
2. Displayed mission row numbering cannot introduce a +1/-1 sequence conversion bug.
3. Invalid/unknown mission bounds prevent backend invocation.
4. Restart calls only `RestartMissionAsync` after required confirmation.
5. Restart cancellation from the confirmation dialog sends nothing.
6. Resume calls only `ResumeMissionAsync`.
7. Abort Landing calls only `AbortLandingAsync`.
8. Four mission actions have independent CanExecute/policy state.
9. Abort Landing is not offered as an executable action for non-Plane vehicles.
10. Mission snapshot/current-sequence changes refresh the relevant controls.
11. Pending operation blocks accidental duplicate submission as intended.
12. Accepted-but-not-telemetry-confirmed is not rendered as fully telemetry confirmed.
13. Existing Arm/Disarm/Mode/Takeoff/Land/Hold/RTL/Set Home Here/Zero Altitude controls retain correct bindings.
14. Expert MAV CMD remains completely separate.

## Manual SITL verification

### Copter SITL

Upload a small mission and verify:

1. Set Current WP changes the canonical mission sequence.
2. Restart resets to sequence 0 without changing mode or arming.
3. Pause then Resume uses pause/continue semantics and does not rewrite the mission.

### Plane SITL

In addition to the above where supported:

1. Configure `LAND_ABORT_THR=1`.
2. Use a mission with a NAV_LAND item.
3. Verify Abort Landing remains disabled until AUTO is executing the verified NAV_LAND item.
4. Send Abort Landing and verify command acceptance without the UI claiming immediate completion.

Record observed command status and mission telemetry transitions.

## Build/test gate

Build the UI/app projects and run Task 03 backend tests plus Actions/ViewModel/UI tests.
