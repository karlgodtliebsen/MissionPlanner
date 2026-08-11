# Map Task 06 — Production vector offline packs (conditional)

## Gate

Execute only if task 05 approves a production vector renderer path.


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

1. Reuse the existing pack manifest/repository/policy/attribution architecture.
2. Add PMTiles/MVT validation and renderer compatibility metadata.
3. Package all style resources required for true offline use: style JSON, sprites, glyph/font ranges and any other assets required by the approved renderer.
4. Add Protomaps as a built-in candidate only after complete offline rendering succeeds.
5. Include required OSM attribution and data-license notices.
6. If an OpenMapTiles-derived product/style is added, include required OpenMapTiles attribution as applicable.
7. Do not hotlink style/glyph/sprite resources in an offline pack.
8. Add pack version/renderer/style compatibility checks.

## Tests

Corrupt PMTiles, unsupported version, missing assets, hash mismatch, complete offline startup, overlay preservation and cross-platform smoke tests.

## Documentation

Update `docs/MAPS.md`, `docs/FEATURES.md` and the task-05 ADR result.
