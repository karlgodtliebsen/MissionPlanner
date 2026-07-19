# Project Review — 2026-07-17

This review focuses on the current mission-planning, map, waypoint, and transfer code.
Findings are ordered by likely impact rather than style preference.

## High priority

### 1. Mission transfers are not serialized

`MissionTransferService` creates a new event subscription and channel for each operation,
but does not prevent simultaneous upload/download/clear operations for the same vehicle.
Concurrent transfers can consume or react to the same mission messages.

**Recommendation:** introduce a per-vehicle transfer coordinator or `SemaphoreSlim`, and
correlate by mission type in addition to system/component.

### 2. Download message handling assumes the next message is the requested item

During download, a message for another sequence is read and effectively discarded. Real
links can duplicate, delay, or reorder mission items.

**Recommendation:** buffer received items by sequence and continue waiting until the
requested sequence arrives or the step timeout expires.

### 3. Unsupported downloaded mission commands are discarded

`FlightPlannerViewModel` and `MissionFileCodec` skip commands that the typed domain does
not yet understand. Saving or re-uploading the mission then loses those commands.

**Recommendation:** add an `UnsupportedMissionItem`/opaque protocol item for lossless
round trips, while excluding it from typed editing until supported.

### 4. `MissionMapViewModel` has too many responsibilities

The class owns:

- Mission editing.
- Map interaction state.
- File picker and file save UI.
- Toasts and prompts.
- Protocol row conversion.
- Command/frame lookup tables.
- Derived distance/azimuth/gradient calculations.
- Display row rebuilding.

This makes the mission domain difficult to test independently and increases the risk of
unrelated regressions.

**Recommendation:** gradually extract `IMissionEditingService`, `IMissionFileInteraction`,
`IMissionRowProjector`, and `IMissionMapInteractionState`. Keep the singleton ViewModel as
a coordinator during migration.

## Medium priority

### 5. `Write Fast` naming is misleading

The current implementation skips local validation but still uses the same MAVLink
handshake and acknowledgement. It is not materially a faster transfer implementation.

**Recommendation:** rename it to `Write Without Validation`, or implement and document a
real alternative transfer strategy before retaining the classic label.

### 6. Protocol knowledge is duplicated in the UI

`MissionMapViewModel` contains numeric command and frame tables while the domain already
has `MissionCommand` and `MissionFrame`.

**Recommendation:** expose editor descriptors from a mission command catalog so command
IDs, names, capabilities, and parameter metadata have one owner.

### 7. Mission item resequencing requires a type switch

`Mission.WithSequence` must be edited whenever a new `MissionItem` subtype is added.

**Recommendation:** add an abstract `WithSequence(ushort)` member to `MissionItem`, or use
an internal visitor. This makes new mission item types closed to aggregate modification.

### 8. Domain aggregate bounds were not guarded

`Mission.Insert` and `Mission.Move` delegated invalid indexes to list exceptions and could
partially obscure domain intent. This review adds explicit range validation.

### 9. UI commands use platform statics directly

`Application.Current`, `FilePicker.Default`, `Geolocation.Default`, and toast APIs are
called from ViewModels or code-behind. This is workable, but makes tests and platform
behavior harder to isolate.

**Recommendation:** wrap these APIs behind small UI application services when the planner
is next decomposed.

### 10. Silent geolocation failure

`CenterOnMyLocationAsync` catches every exception without diagnostics. Permission denial
is expected, but provider failures and programming errors become invisible.

**Recommendation:** catch expected permission/feature exceptions explicitly and log other
exceptions at debug or warning level.

## Low priority and maintainability

### 11. Obsolete methods remain active UI commands

`SaveWpFileAsync` and its format picker are marked `[Obsolete]` but are still bound by the
Plan toolbar. Either remove the attribute or migrate the binding to the intended service.

### 12. Status strings are mixed with behavior

User-facing English strings are embedded throughout the ViewModels. Localization is not
yet required by the project, but centralizing status codes/messages will help when the
Help/multi-language feature starts.

### 13. Full row rebuild on every edit

Every mission edit clears and recreates all `MissionItemRow` objects. This is acceptable
for small missions but will become expensive and may lose selection/focus for large survey
missions.

**Recommendation:** introduce incremental collection updates or a revision-based
projection once profiling shows a real problem.

## Changes included with this review

- Added `docs/MISSIONS.md`.
- Added repository-level `AGENTS.md` for Codex.
- Added optional click-to-add-waypoint mode to the Plan toolbar.
- Applied waypoint acceptance radius to newly created waypoints.
- Applied loiter radius to newly created loiter items.
- Removed an unnecessary async command with no await.
- Added explicit aggregate index validation.
- Updated documentation indexes and feature status.

## Verification limitation

The environment used for this review does not contain the .NET SDK, so the solution could
not be compiled here. Changes were made against the current source structure and checked
for XML well-formedness and internal symbol consistency. Run the commands in `AGENTS.md`
locally before merging.
