# Map Task 12 — Integration, exports, diagnostics and documentation

## Objective

Finish the map subsystem with policy tests, cross-platform verification, export attribution and diagnostics.


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


## Export attribution

Find map screenshot/static-image/PDF export paths. When provider content is present, include the currently required aggregate attribution in the image/footer where policy requires it. If no export exists yet, expose the attribution API and document the future integration point.

## Sanitized diagnostics

Include:

```text
selected source/provider/data product
source kind/archive/payload format
online/offline
credential configured yes/no
policy ID/review date
attribution IDs
cache size
active pack/version
Mapsui version
platform
last source error
```

Never include tokens or signed URLs.

## Automated tests

Cover:

```text
catalog validation
policy intersection
attribution aggregation
provider switching
overlay/viewport preservation
credential redaction
cache isolation
raster MBTiles offline
custom-source validation
hosted-provider offline denial
Esri attribution fallback
pack-manifest validation
```

## Manual platform matrix

Verify Windows, Android and Mac Catalyst with OSM, all Esri basemaps, NoMap, custom XYZ and raster MBTiles. Verify Plan and FlightData mission interactions, follow vehicle, touch/mouse gestures, light/dark theme and network loss. Add vector/PMTiles only if task 06 exists.

## Documentation

Finalize `docs/MAPS.md`, `docs/FEATURES.md`, `docs/README.md`, `docs/PLANNER_SETTINGS.md` and ADR index. Document known limitations rather than hiding unsupported offline/vector cases.
