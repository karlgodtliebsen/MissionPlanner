# Map Task 03 — Mapsui basemap slot and migration

## Objective

Move existing source construction behind a focused Mapsui basemap adapter and guarantee that provider switching never destroys MissionPlanner operational layers.


## Common repository rules

- Modify only the new implementation under `src/`, `docs/`, `scripts/` and test-data folders.
- Treat `src-v.1.38/` as read-only reference material.
- Preserve the existing Mapsui/BruTile mission-map behavior unless the task explicitly changes it.
- Keep MissionPlanner operational overlays (mission, vehicle, track, fence, ADS-B, POI, guided/camera overlays) independent from the basemap provider.
- Do not put Mapsui/BruTile/Avalonia types into `MissionPlanner.Core` domain models.
- Secrets must use the existing secure secret-storage abstraction; never persist them in provider JSON, planner settings, logs or diagnostics.
- All HTTP work must be cancellable, bounded by timeout and provider-policy aware.
- Never implement bulk prefetch, proxying, offline-pack creation or redistribution for a hosted provider unless the reviewed policy explicitly permits that exact operation.
- Provider policy metadata is a conservative application guardrail, not legal advice and not a runtime terms-of-service parser.
- Add deterministic tests and update `docs/MAPS.md` plus `docs/FEATURES.md` as capabilities change.


## Work

1. Add UI-layer abstractions/classes such as:
   - `IMapsuiBasemapFactory`;
   - `MapsuiBasemapFactory`;
   - `MapBasemapController`.
2. Create a stable basemap layer/slot identity.
3. Basemap switch sequence:
   - resolve source;
   - evaluate policy/credentials;
   - create new layer;
   - replace only basemap slot;
   - preserve viewport;
   - preserve mission/vehicle/other overlays;
   - refresh attribution;
   - dispose previous source safely.
4. Migrate OSM and all current Esri sources.
5. Add `NoMap`/blank background.
6. If new layer creation fails, retain the previous working basemap.

## Tests

Verify viewport, waypoint selection, mission route, vehicle marker, follow state, context/tap handling and operational-layer identity survive repeated basemap changes.

## Documentation

Update provider implementation details in `docs/MAPS.md` and `docs/FEATURES.md`.
