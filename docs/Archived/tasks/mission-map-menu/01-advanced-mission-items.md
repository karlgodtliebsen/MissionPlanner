# Mission Map Task 01 — Spline waypoint, DO_JUMP and ROI mission items

## Objective

Implement the four advanced mission-item MenuFlyout actions:

```text
Insert Spline WP
Jump to Start
Jump to WP #
DO_SET_ROI
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


## Existing limitations

Current typed mission items/protocol mapper support only the basic mission command family.

Extend the typed mission domain rather than inserting raw numeric mission rows.

## 1. Spline waypoint

Add a typed mission item for:

```text
MavCmd.NavSplineWaypoint
```

Model the fields supported by MissionPlanner's current mission editor:

```text
lat
lon
altitude
frame/altitude reference
hold/delay if supported by existing mission editor semantics
```

Requirements:

- insert at clicked map position using existing insertion sequencing;
- use default/current mission altitude consistently;
- preserve spline item through:
  - `Mission.WithSequence`;
  - protocol mapping;
  - mission upload/download;
  - mission file save/load;
  - map rendering/labels.
- gate the creation UI by supported vehicle family/capability where appropriate.
- do not silently convert spline to normal waypoint on round trip.

## 2. DO_JUMP

Add a typed `JumpMissionItem` using:

```text
MavCmd.DoJump
```

Fields:

```text
TargetSequence
RepeatCount
```

Implement:

```text
Jump to Start
Jump to WP #
```

Validation:

- target exists;
- target is not the DO_JUMP item itself where invalid;
- repeat count follows MAVLink/ArduPilot semantics;
- support `-1` infinite only with explicit warning/confirmation;
- enforce/document ArduPilot's practical mission limit for DO_JUMP commands;
- preserve user-facing numbering versus zero-based MAVLink sequence correctly.

`Jump to Start` should derive the first executable mission item rather than blindly hard-coding a UI row index.

Mission reorder/delete must keep Jump target semantics coherent.

Choose and document one policy:

```text
A. Jump target sequence automatically tracks item identity when mission rows move;
or
B. target remains explicit numeric sequence and revalidation warns after reordering.
```

Prefer stable mission-item identity + sequence recalculation if the current domain can support it without excessive change.

## 3. ROI

For **new** location ROI items prefer:

```text
MavCmd.DoSetRoiLocation
```

because the generic:

```text
MavCmd.DoSetRoi
```

is legacy/superseded.

Add typed ROI location mission item:

```text
lat
lon
altitude
frame
```

Requirements:

- context-menu action uses clicked map location;
- make label/menu wording clearer if practical, e.g. `Set ROI Here`, while preserving user intent;
- protocol mapper writes modern ROI Location;
- downloader/file parser should preserve and understand legacy `DoSetRoi` where possible rather than dropping it;
- do not silently rewrite unsupported legacy ROI variants into location ROI unless semantics are equivalent.

## 4. Raw/unsupported preservation

Review `MissionFileCodec` and download mapper behavior.

Do not keep the current pattern where newly supported commands are skipped simply because no typed mapper existed.

If a generic unknown mission-item preservation model already exists, reuse it. Otherwise limit this task to the commands above and document remaining unsupported-command behavior.

## UI

Replace these four `NotImplementedCommand` bindings with real commands.

Use prompt abstractions from task 00 for:

```text
Jump target
Repeat count
infinite-repeat warning
```

Map item labels/icons should distinguish:

```text
WP
Spline
Jump
ROI
```

## Tests

Add tests for:

- spline wire round trip;
- spline file round trip;
- insert-at-location;
- Jump Start;
- Jump specific target;
- repeat `0`, positive, `-1`;
- invalid target;
- jump-count limit;
- reorder/delete behavior;
- ROI modern encoder;
- legacy ROI decode compatibility;
- sequence recalculation;
- upload/download round trip.

## Documentation

Update:

```text
docs/MISSIONS.md
docs/FEATURES.md
```

Document the new typed mission items and legacy ROI compatibility.

## Acceptance criteria

- All four menu items are functional.
- Mission upload/download/file round-trip preserves them.
- Modern ROI Location is used for newly created location ROI commands.
- No duplicate MAVLink numeric constants are introduced.
