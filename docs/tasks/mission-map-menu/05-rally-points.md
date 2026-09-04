# Mission Map Task 05 — Rally point domain, protocol and MenuFlyout

## Objective

Implement:

```text
Set Rally Point
Rally Points Download
Rally Points Upload
Clear Rally Points
Save Rally to File
Load Rally from File
```


## Repository constraints

- Work only in the new MissionPlanner implementation under `src/`, `docs/`, `scripts/`, and test-data folders.
- Treat `src-v.1.38/` as read-only reference material. Never modify, format, move, or include legacy files in a commit.
- `src-v.1.38/GCSViews/FlightPlanner.cs` and legacy utilities/plugins may be read to understand historic Mission Planner behavior, but new code must be cross-platform .NET 10 / Avalonia architecture, not a WinForms port.
- Do not add the missing features as another 40+ methods inside the already-large `MissionMapViewModel`.
- Keep MAVLink wire/protocol concerns in `MissionPlanner.MavLink`, domain/application workflows in `MissionPlanner.Core`, map/source infrastructure in `MissionPlanner.Maps`, and Mapsui/Avalonia presentation in `MissionPlanner.AvaloniaUI.App`.
- Views and code-behind may handle native pointer/file/dialog boundaries but must not send MAVLink commands directly.
- Reuse generated `MavCmd`, `MavMissionType`, frames, messages and the current mission transport/ACK infrastructure. Do not create duplicate numeric MAVLink constants.
- Outbound vehicle operations must be active-vehicle scoped, connection-aware, cancellation-aware, operation-gated where appropriate, and disabled during telemetry-log replay.
- Do not mutate UI-bound observable collections from `Dispose()`.
- Commands that modify the mission or vehicle must support explicit validation, user-visible results and undo/cancel/preview where appropriate.
- Preserve all existing working MissionMapView behavior.
- Add deterministic tests with every task.
- Update `docs/FEATURES.md` and mission-planning documentation with every implemented slice.


## Existing protocol support

Reuse the current generic mission-transfer infrastructure where appropriate:

```text
MissionPlanType.RallyPoints
MavMissionType.Rally
MissionTransferService
generated MavCmd.NavRallyPoint
```

There is currently no dedicated Rally domain/application service, so create one.

## Domain

Add:

```text
RallyPoint
RallyPointId
RallyPlan
RallyPlanRevision
RallyPlanSnapshot
IRallyConfigurationService
IRallyWorkspace
```

A rally point should represent:

```text
latitude
longitude
altitude
altitude/frame semantics
optional stable local identity
```

Keep MAVLink sequence separate from stable UI identity where practical.

## Protocol mapper

Add a dedicated mapper between:

```text
RallyPoint
MAV_CMD_NAV_RALLY_POINT
MAV_MISSION_TYPE_RALLY
```

Support the Global/Relative/Terrain altitude frames accepted by current ArduPilot rally-point handling, using existing MissionPlanner altitude/frame concepts where possible.

Do not reuse the normal flight mission `Mission` class if that would blur plan-type semantics.

## Workspace/revisions

Follow the good Fence pattern:

```text
vehicle revision
local revision
dirty state
last download
```

Avoid direct mutation of a bound list from disposal.

## Set Rally Point

Use task-00 map interaction:

```text
SetRallyPoint
```

At map click:

1. prompt/default altitude and altitude reference;
2. validate;
3. add to local rally plan;
4. render immediately;
5. do not upload automatically.

## Download/upload

Download using mission protocol with `MAV_MISSION_TYPE_RALLY`.

Upload only after:

```text
active vehicle
connected
not replay
valid plan
confirmation
operation gate
```

Handle firmware unsupported responses explicitly.

## Clear

Distinguish:

```text
clear local rally plan
clear vehicle rally points
```

Vehicle clear is destructive and requires confirmation.

## File format

Add versioned JSON:

```text
schemaVersion
vehicle/firmware provenance optional
points
altitude reference
createdAt
```

Atomic save/load.

Loading changes only local rally plan until user uploads.

## Overlay

Render:

```text
rally marker index/name
altitude
selected marker
```

Use stable separate layer.

## Tests

Add:

- rally command mapper;
- each supported frame;
- download/upload round trip;
- unsupported firmware;
- set interaction;
- local/vehicle revision semantics;
- clear local vs vehicle;
- file round trip;
- malformed file;
- reconnect/cancellation/replay;
- overlay ordering.

## Documentation

Update:

```text
docs/MISSIONS.md
docs/FEATURES.md
```

Document that Rally is a separate MAVLink mission plan type from the flight mission.

## Acceptance criteria

- All Rally menu commands work end-to-end.
- Rally points never get mixed into the normal flight mission upload.
- Local edits are explicit before upload.
