# Mission Map Task 12 — Final MenuFlyout integration, safety, tests and documentation

## Objective

Complete the MissionMap MenuFlyout implementation after tasks 00–11, remove all placeholder command bindings and verify cross-feature behavior.


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


## 1. Zero placeholder bindings

Search:

```text
MissionMapView.axaml
all MissionMap-related XAML
MissionMapViewModel
```

Acceptance condition:

```text
0 MenuFlyout items bound to NotImplementedCommand
```

If `NotImplementedCommand` has no legitimate remaining consumer, remove it and `NotImplemented(string feature)`.

Do not replace it with a differently named generic TODO command.

## 2. Command grouping and naming

Review MenuFlyout organization after real implementation.

Use clear groups:

```text
Mission Items
Polygon
Geo-Fence
Rally Points
Auto WP / Survey
Map Tools
Import / Overlay
POI
Location
```

Clarify ambiguous labels where safe:

```text
DO_SET_ROI -> Set ROI Here
Geo-Fence Clear -> explicit local/vehicle choice
Load KML File -> Import Mission from KML
KML Overlay -> Add KML Overlay
```

Preserve discoverability for users familiar with legacy Mission Planner.

## 3. Dynamic availability

Every command must have meaningful enablement.

Examples:

```text
Spline:
    supported vehicle family

Jump:
    mission has suitable target(s)

Polygon Area/Offset/Save:
    polygon exists

GeoFence Upload:
    connected, not replay, valid local fence

Rally Upload:
    connected, not replay, valid local rally plan

Survey:
    valid polygon exists

Prefetch:
    effective map policy allows bulk prefetch

Elevation Graph:
    mission has geographic navigation legs

POI Edit/Delete:
    POI selected/near context click
```

Disabled actions should expose a reason where practical.

## 4. Context location semantics

Audit all actions that use:

```text
last right-click/context point
current vehicle
current map center
```

Use one explicit `MissionMapContext` snapshot.

Avoid accidentally using a stale click from another view/tab.

On touch platforms where right-click does not exist, ensure commands have a usable alternate interaction.

## 5. Dirty state / undo

Review mission/polygon/fence/rally modifications.

At minimum provide consistent dirty-state feedback.

Where existing mission undo support exists, integrate generated/imported modifications.

Do not build a giant new undo framework if none exists; document current limitations.

## 6. Vehicle/replay safety audit

Local-only commands may work during replay:

```text
polygon
measure
rotate
local POI
KML/SHP overlay
UTM conversion
elevation profile from loaded mission
```

Vehicle-changing commands must be disabled appropriately:

```text
GeoFence upload/download/clear
Rally upload/download/clear
```

Mission local editing may remain available during replay but mission upload must stay prohibited through existing upload safeguards.

## 7. Cancellation

Long operations must expose cancellation/progress:

```text
SHP/KML import
survey generation when large
terrain profile
prefetch
fence/rally transfer
```

Navigating away cancels transient work without clearing bound result collections in `Dispose()`.

## 8. Central limits

Add centralized limits/configuration for:

```text
max imported file size
max geospatial feature count
max polygon vertices
max generated mission items
max survey lines/points
max text-generator points
max terrain samples
max prefetch tiles
```

Limits should fail with actionable messages rather than freezing the UI.

## 9. DI composition

Extend production configurator and deterministic DI tests to resolve all new services:

```text
interaction service
polygon service
geospatial import
fence workspace integration
rally service/workspace
auto-WP generator
survey generator
map tools/prefetch
elevation profile
POI repository/service
tracker home
coordinate converter
dialog/file abstractions
```

Choose lifetimes deliberately.

Vehicle-specific mutable state must not leak between vehicles.

## 10. Cross-feature tests

Create end-to-end deterministic scenarios:

### Mission planning

```text
draw polygon
generate grid survey
append mission
add spline
add jump
save mission
reload mission
all commands preserved
```

### Fence

```text
set return
upload fake protocol
download
state remains shared with Config/Tuning
```

### Rally

```text
set local point
save
reload
upload/download fake protocol
```

### Geospatial

```text
SHP polygon -> planning polygon -> survey
KML line -> mission preview -> append
KML points -> POIs
```

### Map/local tools

```text
measure
rotate
UTM -> waypoint
terrain profile
POI persistence
tracker home
```

### Map policy

```text
OSM prefetch denied
provider allowing bulk prefetch -> cache warming succeeds
no offline-pack writes
```

## 11. Manual cross-platform verification

Update a mission-map verification matrix for:

```text
Windows
Android
macOS
```

Verify MenuFlyout/context interaction equivalents for mouse and touch.

Do not mark unrun checks passed.

## 12. Documentation

Update at minimum:

```text
docs/MISSIONS.md
docs/FEATURES.md
docs/README.md
docs/MAPS.md
```

`docs/MISSIONS.md` should contain:

```text
mission item support matrix
context-menu feature guide
polygon workspace
GeoFence integration
Rally plan type
Auto-WP/surveys
KML/SHP support
map measurement/prefetch
elevation profile
POIs
Tracker Home
UTM entry
file formats
safety/replay rules
known limitations
```

Clearly distinguish:

```text
Implemented
Runtime integrated
Manually verified
```

## 13. Legacy parity audit

Compare current MenuFlyout against legacy FlightPlanner only to ensure no existing XAML item remains stubbed.

Do not add unrelated legacy menu features that are not present in the current new MissionMap menu.

Create a final table in `docs/FEATURES.md`:

```text
Menu item
Implementation owner/service
Status
Tests
Known limitations
```

## Acceptance criteria

- `NotImplementedCommand` is absent from every MissionMap MenuFlyout item.
- All 43 previously missing actions have real behavior.
- No feature is implemented solely in code-behind with direct MAVLink calls.
- Existing basic mission editing and new map-provider architecture still work.
- Deterministic tests pass.
- Documentation reflects actual implementation and manual verification status.
