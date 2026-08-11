# Map Task 01 — Architecture, ADR and source catalog

## Objective

Introduce the new map source model and versioned built-in catalog without changing visible map behavior.


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


## Work

1. Inventory current `MissionMapView`, map controllers/view models, Mapsui layer creation, current OSM/Esri providers, DI and settings.
2. Create `docs/MAPS.md` and an ADR recording:
   - Mapsui/BruTile remains the production renderer;
   - one basemap slot plus stable operational layers;
   - provider/data-product/source/policy separation;
   - cache versus pack separation;
   - PMTiles/vector decision gate.
3. Add pure models for provider, data product, source, capabilities, policy, attribution, access/archive/payload formats and credential requirement.
4. Place these in a platform-neutral map subsystem/namespace. Do not reference MAUI/Mapsui.
5. Add `Resources/Maps/builtin-map-catalog.json` with schema/version.
6. Seed current OSM and Esri sources plus `NoMap`.
7. Add disabled future entries for custom/self-hosted, hosted services, raster MBTiles and Protomaps candidate.
8. Validate duplicate IDs, invalid cross references and impossible source combinations.
9. Add deterministic catalog serialization/validation tests.

## Acceptance

- Existing maps render exactly as before.
- All current provider identity exists in the catalog rather than scattered constants.
- The architecture ADR and `docs/MAPS.md` are committed.
