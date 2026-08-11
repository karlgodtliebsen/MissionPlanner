# Map Task 10 — Provider, cache and offline-pack settings UI

## Objective

Expose the new subsystem through a clear cross-platform UX.


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


## Groups

Display sources as:

```text
Offline packs
Self-hosted/custom
Online providers
Blank map
```

Do not group by underlying protocol.

## Source details

Show:

```text
display name
provider/data product
online/offline
raster/vector
credential requirement/configured state
attribution preview
cache behavior
offline-pack availability
policy review date
terms/source details
```

## Pack manager

List/import/install/select/remove/verify packs; show coverage, zoom, size, version, source, attribution and license notices.

## Cache manager

Show cache size/disk limit and clear selected/all. Make it visually distinct from offline packs.

## Credentials

Set/remove/test; never reveal stored secret after save.

## Persistence/tests

Persist selected source and non-secret preferences with schema migration tests. Test deleted-source fallback, offline operation and missing credentials.

## Documentation

Update `docs/MAPS.md`, `docs/PLANNER_SETTINGS.md`, `docs/FEATURES.md`.
