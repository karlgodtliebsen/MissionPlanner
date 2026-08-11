# Mission Map Task 00 — Planning interaction and overlay foundation

## Objective

Create the shared interaction, dialog/file boundary and map-overlay infrastructure needed by the remaining MenuFlyout features without changing existing working mission commands.


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


## Current source state

Inspect before editing:

```text
MissionMapView.xaml
MissionMapView.xaml.cs
MissionMapViewModel.cs
MissionMapPresenter.cs
MissionMapSnapshot
```

The current `MissionMapViewModel` is already large and contains direct MAUI calls such as:

```text
FilePicker
Application.Current...Page
DisplayPromptAsync
DisplayActionSheetAsync
Toast
```

Do not expand that pattern for 43 new commands.

## 1. Interaction state

Introduce a small UI-neutral interaction model, for example:

```csharp
public enum MissionMapInteractionMode
{
    None,
    DrawPolygon,
    MeasureDistance,
    SetFenceReturnLocation,
    SetRallyPoint,
    AddPoi,
    SetTrackerHome
}
```

Only add modes actually needed by later tasks.

Add a controller/state service:

```text
IMissionMapInteractionService
MissionMapInteractionState
```

Responsibilities:

- enter one interaction mode;
- cancel current interaction;
- accept map click/move/end events;
- expose prompt/status text;
- reject mutually exclusive interactions;
- clear temporary interaction state on vehicle/page lifecycle change.

The service must not reference Mapsui.

## 2. Planning overlay snapshot

Add an immutable/UI-neutral overlay model separate from the normal mission snapshot:

```text
MissionPlanningOverlaySnapshot
    DrawnPolygon
    TemporaryMeasurement
    FencePreview
    RallyPoints
    PoiItems
    ImportedOverlays
    SurveyPreview
    TrackerHome
```

Use small typed records rather than one giant bag of nullable objects where practical.

`MissionMapPresenter` becomes responsible for mapping these snapshots to Mapsui layers/features.

Do not put Mapsui geometry into Core/ViewModels.

## 3. Presenter layers

Create stable presenter-owned layers for categories such as:

```text
planning polygon
measurement
fence
rally
POI
imported KML/SHP visual overlays
survey preview
tracker home
```

Update features in place or replace layer contents; do not recreate the whole map.

All temporary layers must survive basemap changes.

## 4. Dialog/file abstractions

Introduce reusable application abstractions for the new feature set, for example:

```text
IUserPromptService
IUserChoiceService
IFileOpenService
IFileSaveService
IUserNotificationService
```

Reuse existing abstractions where equivalent ones already exist.

Do not create duplicate dialog services.

As later tasks touch existing direct `DisplayPromptAsync`/`FilePicker` paths, migrate those touched paths to the abstraction. Do not perform a huge unrelated UI rewrite.

## 5. Command availability

Add a typed command-availability model:

```text
MissionMapCommandAvailability
    IsEnabled
    Reason
```

Availability should eventually account for:

```text
current map interaction
mission state
active vehicle
vehicle family/capabilities
online/replay state
polygon present
fence/rally state
selected POI
map provider policy
terrain availability
```

Do not put provider-policy logic directly in XAML converters.

## 6. Lifecycle

- cancel temporary interaction when the MissionMap view is truly deactivated;
- do not clear stable UI-bound data during `Dispose()`;
- subscriptions must be deterministic;
- presenter layers/resources must be disposed without disposing shared map/cache services.

## Tests

Add deterministic tests for:

- one interaction at a time;
- cancel;
- mode transition;
- map click routing;
- temporary overlay state;
- overlay state survives basemap change;
- lifecycle cancellation;
- presenter layer identity preserved;
- dialog/file abstractions injectable in view-model tests;
- command availability reason propagation.

## Documentation

Create or extend `docs/MISSIONS.md` with a section:

```text
Mission map interaction architecture
Planning overlays
Menu command ownership
```

Update `docs/FEATURES.md` only for the new infrastructure; do not mark menu features complete yet.

## Acceptance criteria

- Later tasks can add map interactions without adding Mapsui types to Core.
- `MissionMapViewModel` delegates new functionality to focused services.
- Temporary overlay rendering is independent of basemap switching.
- Existing mission editing still works.
