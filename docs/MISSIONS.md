# Mission Planning and Transfer

This document describes how MissionPlanner represents, edits, validates, stores, transfers,
and monitors ArduPilot missions. It reflects the current implementation under
`MissionPlanner.Core/Missions`, `MissionPlanner.MavLink/Missions`, and
`MissionPlanner.App/Views/Missions`.

---

## Architectural boundary

Mission planning is modeled independently of the MAVLink wire protocol.

```text
Map and waypoint editor
        │
        ▼
Mission aggregate and typed mission items
        │
        ▼
Mission validation
        │
        ▼
Protocol mapper
        │
        ▼
MAVLink mission transfer service
        │
        ▼
ArduPilot mission storage and AUTO execution
```

The map is a projection and editor of the domain `Mission`; map pins and route polylines
are never the source of truth.

## Building blocks

| Concern | Main types | Location |
|---|---|---|
| Mission aggregate | `Mission`, `MissionId`, `MissionItemId` | `MissionPlanner.Core/Missions/Models` |
| Typed mission items | `WaypointMissionItem`, `TakeoffMissionItem`, `LandMissionItem`, `ReturnToLaunchMissionItem`, `LoiterMissionItem`, `ChangeSpeedMissionItem` | `MissionPlanner.Core/Missions/Models` |
| Coordinates and altitude | `GeoPosition`, `MissionAltitude`, `MissionAltitudeReference`, `MissionFrame` | `MissionPlanner.Core/Missions/Models` |
| Validation | `IMissionValidator`, `MissionValidator` | `MissionPlanner.Core/Missions/Validation` |
| Protocol mapping | `IMissionProtocolMapper`, `MissionProtocolMapper` | `MissionPlanner.Core/Missions` |
| File persistence | `IMissionFileCodec`, `MissionFileCodec` | `MissionPlanner.Core/Missions/Files` |
| Upload/download | `IMissionTransferService`, `MissionTransferService` | `MissionPlanner.Core/Missions/Transfer` |
| Wire DTOs | `MavLinkMissionItem`, `MavMissionType`, `MavMissionResult` | `MissionPlanner.MavLink/Missions` |
| Mission encoders/decoders | `IMavLinkMissionEncoder`, mission message decoders | `MissionPlanner.MavLink/Encoding`, `Decoding` |
| Shared UI editor | `MissionMapView(Model)`, `MissionItemListView` | `MissionPlanner.App/Views/Missions` |
| Plan screen | `FlightPlannerView(Model)` | `MissionPlanner.App/Views/FlightPlanner` |

---

## Mission aggregate

`Mission` owns the ordered executable item list and maintains sequence numbers.

Supported operations include:

- Add and insert an item.
- Replace an item while preserving its position in the sequence.
- Remove an item and resequence the remainder.
- Move an item and resequence the mission.
- Rename the mission.
- Track a revision number for changes.

Mission sequence numbers are zero-based in the domain. UI labels commonly display
`Sequence + 1`.

## Typed mission items

The domain uses strongly typed items instead of exposing generic MAVLink `Param1` through
`Param4` fields everywhere.

Currently supported commands are:

| Domain type | MAVLink command |
|---|---|
| `WaypointMissionItem` | `MAV_CMD_NAV_WAYPOINT` |
| `LoiterMissionItem` | `MAV_CMD_NAV_LOITER_UNLIM`, `_TURNS`, or `_TIME` |
| `ReturnToLaunchMissionItem` | `MAV_CMD_NAV_RETURN_TO_LAUNCH` |
| `LandMissionItem` | `MAV_CMD_NAV_LAND` |
| `TakeoffMissionItem` | `MAV_CMD_NAV_TAKEOFF` |
| `ChangeSpeedMissionItem` | `MAV_CMD_DO_CHANGE_SPEED` |

Typed items preserve command meaning in the domain. Generic MAVLink parameters are only
introduced when mapping to or from the wire representation.

## Coordinate frames and altitude

Altitude is always qualified by a reference:

- Relative to home.
- Mean sea level.
- Terrain.

Do not replace `MissionAltitude` with an unqualified numeric altitude. The same numeric
value has materially different flight meaning in each frame.

## Shared mission editor

`MissionMapViewModel` is registered as a singleton and is shared by the FlightData map and
the dedicated Plan screen. Both screens therefore edit the same in-memory mission.

The map currently supports:

- Mission pins and connecting route line.
- Home marker.
- Context-position commands.
- Optional click-to-add-waypoint mode.
- Delete nearest waypoint.
- Add waypoint at map or vehicle position.
- Takeoff, land, RTL, and loiter items.
- Reverse mission order.
- Modify mission altitude.
- Fit map to mission.
- Multiple tile providers.

New waypoints use the editor's default altitude and waypoint acceptance radius. New loiter
items use the editor's loiter radius.

## Complete waypoint editor

The complete editor projects each typed item through `IMissionProtocolMapper` to expose:

- Command and frame.
- P1 through P4.
- Latitude, longitude, and altitude.
- Per-leg distance, azimuth, and gradient.

When edited, the row is converted back through the protocol mapper into a typed domain
item and replaces the existing mission item.

This is a pragmatic compatibility surface for users familiar with classic Mission
Planner. New domain features should still prefer typed APIs over generic parameters.

---

## Validation

`MissionValidator` validates the local mission before the normal Write operation.

Current checks include:

- Mission is not empty.
- Sequence numbers are continuous.
- Geographic coordinates are valid.
- Takeoff altitude is above zero.
- Change-speed values are above zero.

Validation should grow as the planning domain grows. Future checks should include vehicle
capabilities, unsupported command/frame combinations, altitude warnings, geofence
conflicts, terrain availability, and mission-size limits.

`Write Fast` currently skips local validation. It does not bypass the MAVLink mission
handshake or acknowledgement.

---

## Protocol mapping

`MissionProtocolMapper` maps typed items to `MavLinkMissionItem` and reconstructs typed
items from downloaded protocol records.

```text
MissionItem
    → IMissionProtocolMapper.ToProtocol
    → MavLinkMissionItem

MavLinkMissionItem
    → IMissionProtocolMapper.FromProtocol
    → MissionItem
```

Unsupported downloaded commands are currently skipped by the UI and reported to the
user. Preserve the raw mission data in future work if round-tripping unsupported commands
becomes a requirement.

---

## Mission upload

The upload service follows the MAVLink mission protocol handshake:

```text
GCS                                      Vehicle
 │                                          │
 │──── MISSION_COUNT ──────────────────────>│
 │<─── MISSION_REQUEST_INT(sequence) ───────│
 │──── MISSION_ITEM_INT(sequence) ─────────>│
 │                  ...                     │
 │<────── MISSION_ACK ──────────────────────│
```

`MissionTransferService`:

- Subscribes before sending the first request.
- Correlates messages by vehicle system/component.
- Resends `MISSION_COUNT` on timeout.
- Handles repeated sequence requests.
- Reports upload progress.
- Requires the final mission acknowledgement.

Only one mission transfer should be active for a vehicle at a time. A transfer coordinator
or per-vehicle lock should be added before parallel transfer operations are exposed.

## Mission download

Download performs the inverse transaction:

```text
GCS                                      Vehicle
 │──── MISSION_REQUEST_LIST ───────────────>│
 │<──── MISSION_COUNT ──────────────────────│
 │──── MISSION_REQUEST_INT(0) ─────────────>│
 │<──── MISSION_ITEM_INT(0) ────────────────│
 │                  ...                     │
 │──── MISSION_ACK ────────────────────────>│
```

Each item is requested explicitly with retries. Sequence zero may represent the vehicle
home item and is handled separately by `FlightPlannerViewModel`.

## Mission files

`MissionFileCodec` supports:

- QGroundControl WPL 110 (`.waypoints` and `.txt`).
- Versioned MissionPlanner JSON (`.mission`).

The codec maps through the same protocol representation used by vehicle transfer, which
keeps file and wire semantics aligned.

---

## Execution and monitoring

Uploading does not execute a mission. Normal operation is:

1. Upload the mission.
2. Verify vehicle readiness and pre-arm checks.
3. Arm the vehicle.
4. Select `AUTO` mode.
5. ArduPilot executes the stored mission independently of the GCS.

Execution telemetry is represented in the vehicle domain by mission/navigation state.
Relevant incoming messages include:

- `MISSION_CURRENT` — active mission sequence.
- `MISSION_ITEM_REACHED` — completed sequence.
- `NAV_CONTROLLER_OUTPUT` — distance and navigation errors.
- `HEARTBEAT` — flight mode and armed state.
- Position and HUD telemetry — live map position and motion.

The next monitoring slice should highlight the active item on the map and show completed,
active, and upcoming items distinctly.

---

## Testing guidance

Mission tests should cover:

- Aggregate sequencing after add, insert, remove, move, and replace.
- Validation failures and warnings.
- Protocol mapping round trips for every supported item type.
- WPL and JSON file round trips.
- Upload retries, repeated requests, invalid sequence requests, and rejected ACKs.
- Download retries, out-of-order messages, duplicate items, and cancellation.
- UI-independent editing services once `MissionMapViewModel` is decomposed.

Use SITL or the simulator for end-to-end upload, download, and execution tests before
relying on physical flight hardware.

---

## Known gaps and next steps

See `FEATURES.md` for the complete inventory. High-value next steps are:

1. Live mission execution highlighting.
2. Draggable waypoint and home markers.
3. Spline waypoint domain support and curved rendering.
4. `DO_JUMP` and ROI commands.
5. Mission capability validation against vehicle/firmware type.
6. Terrain and elevation services.
7. Fence and rally aggregates separated from the flight mission.
8. Survey/grid generation retaining its source definition.
9. Transfer serialization and stronger mission-type correlation.
10. Decomposition of `MissionMapViewModel` into editing, file, interaction, and presentation services.
