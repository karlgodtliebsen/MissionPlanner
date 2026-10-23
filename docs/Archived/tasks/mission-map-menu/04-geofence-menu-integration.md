# Mission Map Task 04 — Integrate existing GeoFence subsystem into MissionMap

## Objective

Implement all Geo-Fence MenuFlyout commands by reusing the already-complete fence subsystem:

```text
Geo-Fence Upload
Geo-Fence Download
Geo-Fence Set Return Location
Geo-Fence Load from File
Geo-Fence Save to File
Geo-Fence Clear
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


## Critical constraint

Do **not** create another MAVLink fence implementation.

Reuse:

```text
IFenceConfigurationService
FenceConfigurationService
FencePlan
FenceArea
FenceAreaKind
FenceGeometryValidator
FenceProtocolMapper
existing operation gate
MAV_MISSION_TYPE_FENCE support
```

MissionMap and Config/Tuning GeoFence must operate on the same conceptual fence plan/state.

## Shared fence workspace

Inspect current ownership in `GeoFenceTabViewModel`.

If the config tab currently owns local edit state privately, extract the minimum shared application workspace needed so:

```text
MissionMap menu
Config/Tuning GeoFence
```

can see/modify the same local fence plan without duplicating protocol state.

Do not make a static global singleton detached from active vehicle.

## Download

Call the existing fence service download flow.

Requirements:

- current active vehicle;
- supported typed geometry;
- replay disabled for vehicle operation;
- cancellation;
- show progress/result;
- update shared local plan;
- render fence overlay in MissionMap.

If local edits exist, require explicit conflict choice or use existing revision/backup semantics.

## Upload

Upload/apply the shared local fence plan through `IFenceConfigurationService.ApplyAsync`.

Requirements:

- validate geometry first;
- operation gate;
- connection/replay checks;
- confirmation summarizing inclusion/exclusion areas and return point;
- preserve/read back result through existing service behavior.

## Set Return Location

Use task-00 map interaction:

```text
SetFenceReturnLocation
```

Next accepted map click updates only the local `FencePlan` return point.

Do not immediately upload.

## Load/save file

Prefer one versioned MissionPlanner fence JSON format capable of representing:

```text
return point
polygon inclusion
polygon exclusion
circle inclusion
circle exclusion
```

If existing fence file serialization exists, reuse it.

Do not use a lossy polygon-only format.

Load should update local plan only, with validation and confirmation if local changes exist.

Save is local-only and must work offline.

## Clear

Distinguish:

```text
Clear local fence plan
Clear fence on vehicle
```

The existing MenuFlyout wording `Geo-Fence Clear` is ambiguous.

Improve UX so the command cannot unexpectedly erase vehicle state.

Recommended flow:

1. prompt:
   - Clear local plan only
   - Clear vehicle fence
   - Cancel
2. vehicle clear uses `IFenceConfigurationService.ClearAsync` and strong confirmation.

## Overlay

Render current/local fence plan through a stable fence planning overlay.

Use different visual treatment for:

```text
inclusion
exclusion
return point
dirty/local state when useful
```

## Tests

Add tests for:

- shared state between MissionMap and GeoFence Config view-models;
- download;
- upload;
- set return point interaction;
- load/save round trip;
- inclusion/exclusion circles/polygons;
- invalid geometry;
- dirty-local conflict;
- replay denial;
- disconnect/cancel;
- clear-local versus clear-vehicle distinction;
- overlay update.

## Documentation

Update:

```text
docs/MISSIONS.md
docs/FEATURES.md
```

Cross-reference existing fence documentation.

## Acceptance criteria

- All six MissionMap GeoFence commands use the existing fence protocol implementation.
- Config/Tuning and MissionMap do not maintain competing fence states.
- Vehicle-destructive clear/upload operations are explicit and confirmed.
