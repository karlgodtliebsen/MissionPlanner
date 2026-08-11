# Map Task 05 — PMTiles/vector renderer decision spike

## Objective

Determine whether Protomaps/OpenMapTiles-style vector maps can be integrated without destabilizing the existing Mapsui mission editor.

This is a decision task, not a shipping task.


## Common repository rules

- Modify only the new implementation under `src/`, `docs/`, `scripts/` and test-data folders.
- Treat `src-v.1.38/` as read-only reference material.
- Preserve the existing Mapsui/BruTile mission-map behavior unless the task explicitly changes it.
- Keep MissionPlanner operational overlays (mission, vehicle, track, fence, ADS-B, POI, guided/camera overlays) independent from the basemap provider.
- Do not put Mapsui/BruTile/MAUI types into `MissionPlanner.Core` domain models.
- Secrets must use the existing secure secret-storage abstraction; never persist them in provider JSON, planner settings, logs or diagnostics.
- All HTTP work must be cancellable, bounded by timeout and provider-policy aware.
- Never implement bulk prefetch, proxying, offline-pack creation or redistribution for a hosted provider unless the reviewed policy explicitly permits that exact operation.
- Provider policy metadata is a conservative application guardrail, not legal advice and not a runtime terms-of-service parser.
- Add deterministic tests and update `docs/MAPS.md` plus `docs/FEATURES.md` as capabilities change.


## Evaluate in order

### A. Mapsui experimental vector renderer + direct PMTiles source

Prototype PMTiles v3 random access feeding MVT tiles into the current Mapsui vector path.

### B. Alternative archive conversion

Evaluate PMTiles-to-vector-MBTiles only if legal/provenance constraints allow it and only if archive access, not rendering, is the blocker.

### C. Separate vector renderer

Only if A fails. Evaluate MapLibre-native/MAUI or WebView-based approaches as a future architecture; do not migrate production code during the spike.

## PMTiles reader requirements if prototyped

- v3 header/directory validation;
- local random access;
- bounded allocations/decompression;
- cancellation;
- malformed archive tests;
- HTTP Range only if a remote test is required.

## Functional matrix

Test Windows, Android and Mac Catalyst with a real regional Protomaps archive:

- pan/zoom;
- labels/styles;
- light/dark;
- mission route/waypoint overlay;
- vehicle marker/follow;
- map gestures and context actions;
- source switching;
- offline use;
- memory/CPU/startup responsiveness.

## Output

Create an ADR choosing one:

```text
Proceed with Mapsui + PMTiles/vector
Adopt another renderer in a future migration
Defer vector/PMTiles and remain raster/MBTiles
```

Task 06 may execute only when the ADR approves production vector support.
