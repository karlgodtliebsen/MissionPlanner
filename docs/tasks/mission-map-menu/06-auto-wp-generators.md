# Mission Map Task 06 — Auto-WP circles, spline circles, area and text

## Objective

Implement:

```text
Create WP Circle
Create Spline Circle
Auto WP Area
Auto WP Text
```

`Create Circle Survey` and `Survey (Grid)` are task 07.


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


## Shared architecture

Add a platform-neutral generation service:

```text
IAutoWaypointGenerator
AutoWaypointGenerationRequest
AutoWaypointGenerationResult
GeneratedMissionPreview
```

Generators return candidate typed mission items.

They must **not** mutate the mission until preview validation and explicit Apply/Append/Replace choice.

## Create WP Circle

Input:

```text
center = context map location
radius metres
number of points
direction clockwise/counter-clockwise
start angle
altitude
altitude reference
```

Validation:

```text
radius > 0
bounded point count
finite values
valid latitude
```

Generate normal waypoint items around the circle using geodesic destination calculations.

Do not approximate longitude scaling with fixed degree arithmetic.

Preview circle + generated points before applying.

## Create Spline Circle

Use typed `NavSplineWaypoint` support from task 01.

Preserve useful legacy behavior while making it explicit:

```text
center
radius
point count or angular spacing
direction
start angle
minimum altitude
maximum altitude
altitude step / climb profile
```

If a helical climb is selected, produce deterministic altitude progression.

If preserving the legacy center-ROI behavior:

- use modern `DoSetRoiLocation`;
- make `Point camera/ROI at center` an explicit option;
- do not insert legacy generic `DoSetRoi`.

## Auto WP Area

Do not duplicate area math.

Delegate to task-02 polygon area calculation.

If no polygon exists:

```text
disabled with reason
or prompt user to draw/create one
```

## Auto WP Text

Do not port the legacy Windows/System.Drawing + external `1CamBam_Stick_3` font dependency.

Implement a deterministic, cross-platform stroke font.

Recommended:

```text
small embedded Hershey-like/single-line vector font data
```

Inputs:

```text
text
origin
height/scale in metres
rotation
letter spacing
line spacing if multiline
altitude
```

Generate waypoint paths representing strokes.

Handle pen-up transitions deliberately and document the resulting travel path.

Avoid absurd missions:

```text
maximum characters
maximum generated points
minimum segment spacing
mission item limit check
```

Preview before applying.

## Mission merge

All generators support explicit:

```text
Append
Replace
Cancel
```

unless a generator clearly only makes sense as append.

Do not silently clear the mission.

## Tests

Add:

- circle known bearings/distances;
- clockwise/counter-clockwise;
- start angle;
- high latitude fixtures;
- spline altitude progression;
- optional ROI center;
- area delegation;
- stroke-font deterministic glyph fixtures;
- rotation/scale;
- max-point limit;
- preview/apply semantics;
- mission sequence correctness.

## Documentation

Update:

```text
docs/MISSIONS.md
docs/FEATURES.md
```

Document the cross-platform text generator and mission-size limits.

## Acceptance criteria

- Four menu actions are implemented without platform-specific font/rendering dependencies.
- Generated missions are previewed before mutation.
- Geodesic calculations are used.
