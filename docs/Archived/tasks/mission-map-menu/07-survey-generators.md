# Mission Map Task 07 — Circle Survey and Grid Survey

## Objective

Implement:

```text
Create Circle Survey
Survey (Grid)
```

using platform-neutral geometry/planning services.


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


## Legacy references

Read-only reference material includes:

```text
src-v.1.38/Utilities/CircleSurveyMission.cs
src-v.1.38/ExtLibs/MissionPlanner.Gridv2/GridUIv2.cs
src-v.1.38/ExtLibs/Utilities/Grid.cs
```

Understand legacy planning behavior but do not port WinForms/plugin UI code.

## Survey domain

Add:

```text
ISurveyMissionGenerator
SurveyArea
GridSurveyRequest
CircleSurveyRequest
SurveyMissionResult
SurveyLeg
SurveyStatistics
```

Reuse task-02 planning polygon for area/grid survey.

## Grid Survey

At minimum support:

```text
planning polygon
flight-line angle
line spacing
overshoot/lead-in
altitude
altitude reference
start corner/optimization option
cross-grid optional
```

If camera metadata/calculations already exist elsewhere, reuse them.

If not, keep v1 scope based on explicit line spacing/altitude rather than implementing an entire camera-calibration subsystem in this task.

Requirements:

- clip flight lines to polygon;
- order legs deterministically;
- avoid zero-length legs;
- support concave polygon where chosen geometry engine permits;
- preview path;
- calculate:
  - total distance;
  - estimated number of mission points;
  - area;
  - line count.

## Circle Survey

Implement a concentric/orbit style survey centered at context location or polygon-derived center.

Define explicit inputs after inspecting the legacy algorithm:

```text
center
radius / radial spacing
altitude
point spacing
direction
number of rings or inner/outer radius
```

If legacy circle survey is camera-footprint driven, preserve useful calculations only when their required inputs can be represented cleanly.

Do not invent opaque magic defaults without documenting them.

## Mission commands

Generated navigation items must use typed mission items/protocol mapper.

If camera trigger mission commands are added:

- use generated MAVLink enums;
- add typed mission items and round-trip tests;
- only include them when the user explicitly enables triggering.

## Preview and apply

Use `MissionPlanningOverlaySnapshot.SurveyPreview`.

Show:

```text
area
flight path
direction arrows when practical
start/end
estimated distance
point count
```

Apply only after confirmation.

Support:

```text
Append
Replace
Cancel
```

## Limits

Validate against:

```text
maximum mission items
minimum line spacing
finite geometry
too-small polygon
self-intersecting polygon
extreme latitude/projection limitations
```

## Tests

Add fixture polygons:

```text
rectangle
rotated rectangle
concave L shape
small polygon
invalid polygon
```

Verify:

- clipping;
- line spacing;
- angle;
- ordering;
- cross-grid;
- overshoot;
- deterministic output;
- point count;
- circle survey geometry;
- preview/apply;
- cancellation.

## Documentation

Update:

```text
docs/MISSIONS.md
docs/FEATURES.md
```

Document the supported v1 survey parameters and known limitations versus legacy Mission Planner.

## Acceptance criteria

- Both survey menu entries generate usable previewed missions.
- Geometry code is platform-neutral and unit tested.
- No WinForms/plugin dependencies are introduced.
