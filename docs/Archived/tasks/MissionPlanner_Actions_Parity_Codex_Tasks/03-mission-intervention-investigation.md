# Mission intervention backend investigation

## Outcome

The four-operation backend is deferred as a unit. Set Current Waypoint and Restart Mission
have implementable protocol mappings, but Resume Mission and Abort Landing lack the state
and transactional guarantees required by the task. Publishing a partial service would make
unsafe operations appear available to the UI.

## Proven legacy behavior

### Set Current Waypoint

`BUT_setwp_Click` calls `setWPCurrent` with the selected mission sequence.
`setWPCurrentAsync` sends `MISSION_SET_CURRENT` and retries until a matching
`MISSION_CURRENT` arrives from the target system/component. The future implementation may
use the superseding `MAV_CMD_DO_SET_MISSION_CURRENT`, but must retain selected-vehicle
targeting, range validation from `VehicleNavigationState.MissionItemCount`, and confirmation
from a real ACK and/or matching `MISSION_CURRENT`.

### Restart Mission

`BUTrestartmission_Click` only calls `setWPCurrent(..., 0)`. It does not switch to AUTO and
does not issue `MAV_CMD_MISSION_START`. Current MAVLink additionally supports resetting jump
counters through `MAV_CMD_DO_SET_MISSION_CURRENT` parameter 2; adopting that newer semantic
needs an explicit product decision because it is not what the traced legacy button did.

### Resume Mission

Legacy Resume Mission is not a mode shortcut. It:

1. asks for a resume waypoint;
2. downloads the target and complete onboard mission;
3. removes earlier navigation items while retaining HOME and applicable command items;
4. uploads the rewritten mission;
5. sets current mission sequence to 1;
6. for Copter, repeatedly enters GUIDED, arms, and commands takeoff to the selected item's
   altitude; and
7. repeatedly enters AUTO.

NextGen has mission download/upload primitives, but no transactional operation that can
restore the original onboard mission if rewriting, arming, takeoff, or mode change fails.
It also does not retain enough typed onboard-item context in `VehicleState` to validate this
workflow through `IVehicleCommandPolicy`. Implementing Resume as unconditional AUTO or as a
single set-current command would not be legacy compatible.

### Abort Landing

Legacy calls `doAbortLand`, which sends `MAV_CMD_DO_GO_AROUND` with zero parameters. Current
ArduPilot Plane documentation constrains this to AUTO while executing a `NAV_LAND` item.
NextGen retains current mission sequence and count but not the currently executing mission
command, so policy cannot prove applicability. Sending it based only on armed/airborne state
would violate the task's independent state gating requirement.

## Required prerequisites

- Add a selected-vehicle mission-operation service with per-vehicle operation leases.
- Retain the active onboard mission snapshot (including item command types) with clear
  freshness and vehicle identity.
- Define whether Restart preserves legacy sequence-0 behavior or uses the newer reset flag.
- Design Resume as a transactional workflow with explicit confirmation, rollback/recovery,
  and a typed result for each failed step.
- Expose current mission item command/state so Abort Landing policy can require Plane AUTO
  plus an active landing item.
- Add protocol encoding/tracking for set-current confirmation without treating streamed,
  stale `MISSION_CURRENT` as confirmation of a new request.

No production command API or policy entry is added until these prerequisites are met.
