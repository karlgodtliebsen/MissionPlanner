# Mission Map Task 11 — Tracker Home and UTM coordinate entry

## Objective

Implement:

```text
Tracker Home
Enter UTM Coordinate
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


## 1. Tracker Home

The legacy feature stored a local tracker/antenna home location.

The current new application has no confirmed antenna-tracker transport subsystem.

Therefore implement a truthful local planning state first:

```text
TrackerHome
ITrackerHomeService
TrackerHomeSnapshot
```

Fields:

```text
latitude
longitude
altitude optional
updatedAt
source
```

The MenuFlyout action:

1. uses context-click map position;
2. optionally prompts altitude;
3. stores tracker-home state;
4. renders a distinctive tracker-home marker.

Do **not** claim that this commands physical tracker hardware.

If Codex discovers an actual current tracker service during implementation:

- integrate through that existing typed service;
- keep local tracker-home state separate from hardware ACK/state;
- update documentation accordingly.

Persistence may be added to Planner settings or a small local state file if useful; document whether it is persisted.

## 2. UTM inverse conversion

Current `MapCoordinateFormatter` supports WGS84 -> UTM/MGRS display.

Add proper inverse:

```text
UTM -> WGS84
```

through a dedicated typed geodesy service, for example:

```text
IGeodeticCoordinateConverter
UtmCoordinate
GeographicCoordinate
```

Do not put parsing/calculation in the view model.

## UTM parser

Accept explicit:

```text
zone number 1..60
hemisphere N/S
easting
northing
```

Optionally support common compact input:

```text
32N 500000 6170000
```

Do not conflate UTM zone letters/bands with hemisphere without validation.

Requirements:

```text
finite values
valid zone
valid easting/northing ranges
round-trip accuracy tests
```

Use a reviewed geodesy/projection implementation.

If adding ProjNet or another package, perform dependency/license/security review.

## Enter UTM Coordinate workflow

1. prompt or dialog for zone/hemisphere/easting/northing;
2. convert to WGS84;
3. show conversion preview:
   - lat/lon;
   - map marker;
4. user chooses:
   - Add waypoint here
   - Center map here
   - Cancel

If legacy behavior always added a waypoint, preserve that as the default action but make the result explicit.

Altitude uses current/default mission altitude when adding waypoint.

## Tests

Add reference fixtures from known UTM coordinates covering:

```text
Denmark
northern hemisphere
southern hemisphere
zone boundaries
invalid zone
invalid easting/northing
round trip WGS84 -> UTM -> WGS84
```

Also test:

- tracker home set/update;
- marker state;
- no hardware command when no tracker subsystem exists;
- UTM waypoint insertion sequence.

## Documentation

Update:

```text
docs/MISSIONS.md
docs/FEATURES.md
```

Document tracker-home semantics honestly and UTM input conventions.

## Acceptance criteria

- Tracker Home stores/renders a real local state rather than a placeholder.
- UTM input converts accurately and can create a waypoint.
- No fake antenna-tracker command path is introduced.
