# Mission Map Task 10 — Persistent Points of Interest

## Objective

Implement:

```text
POI Add
POI Delete
POI Edit
```

with a typed persistent POI repository and map overlay.


## Repository constraints

- Work only in the new MissionPlanner implementation under `src/`, `docs/`, `scripts/`, and test-data folders.
- Treat `src-v.1.38/` as read-only reference material. Never modify, format, move, or include legacy files in a commit.
- `src-v.1.38/GCSViews/FlightPlanner.cs` and legacy utilities/plugins may be read to understand historic Mission Planner behavior, but new code must be cross-platform .NET 10 / MAUI architecture, not a WinForms port.
- Do not add the missing features as another 40+ methods inside the already-large `MissionMapViewModel`.
- Keep MAVLink wire/protocol concerns in `MissionPlanner.MavLink`, domain/application workflows in `MissionPlanner.Core`, map/source infrastructure in `MissionPlanner.Maps`, and Mapsui/MAUI presentation in `MissionPlanner.App`.
- Views and code-behind may handle native pointer/file/dialog boundaries but must not send MAVLink commands directly.
- Reuse generated `MavCmd`, `MavMissionType`, frames, messages and the current mission transport/ACK infrastructure. Do not create duplicate numeric MAVLink constants.
- Outbound vehicle operations must be active-vehicle scoped, connection-aware, cancellation-aware, operation-gated where appropriate, and disabled during telemetry-log replay.
- Do not mutate UI-bound observable collections from `Dispose()`.
- Commands that modify the mission or vehicle must support explicit validation, user-visible results and undo/cancel/preview where appropriate.
- Preserve all existing working MissionMapView behavior.
- Add deterministic tests with every task.
- Update `docs/FEATURES.md` and mission-planning documentation with every implemented slice.


## Domain/application model

Add:

```text
PointOfInterest
PointOfInterestId
IPoiRepository
IPoiService
PoiSnapshot
```

Suggested fields:

```text
Id
Name
Latitude
Longitude
Altitude optional
Description optional
Category optional
CreatedAt
UpdatedAt
```

POIs are local MissionPlanner planning data, not MAVLink mission items.

Do not use a static/global mutable collection.

## Persistence

Use versioned JSON in app data.

Requirements:

```text
atomic save
schema version
bounded file size/count
valid finite coordinates
stable IDs
migration path
corrupt file isolation/backup
```

If the application already has a suitable local-data repository abstraction, reuse it.

## Add

Use context-click location or task-00 `AddPoi` interaction.

Prompt:

```text
name
optional altitude/description/category
```

Provide sensible default name.

## Edit

The menu command needs a POI target.

Support one of:

```text
nearest POI to clicked location within screen tolerance
selected POI
explicit list chooser
```

Prefer explicit selected/nearest + confirmation rather than editing an arbitrary first POI.

## Delete

Delete selected/nearest POI with confirmation.

No vehicle interaction.

## Overlay

Render persistent POIs on their own stable Mapsui layer.

Requirements:

- labels;
- selected state;
- click hit-testing;
- basemap changes do not remove them;
- reload across application restart.

## Import interaction

Task 03 KML point imports may offer:

```text
Import as POIs
```

through `IPoiService`.

Do not duplicate KML parsing here.

## Tests

Add:

- repository round trip;
- atomic save failure;
- corrupt file;
- add/edit/delete;
- coordinate validation;
- duplicate names;
- selection/hit target behavior;
- KML import handoff;
- restart persistence;
- overlay updates;
- disposal does not clear bound collection.

## Documentation

Update:

```text
docs/MISSIONS.md
docs/FEATURES.md
```

Document that POIs are local and not uploaded to the aircraft.

## Acceptance criteria

- Add/Edit/Delete work and persist.
- POIs survive basemap switching and application restart.
- POIs are not mixed into flight mission upload.
