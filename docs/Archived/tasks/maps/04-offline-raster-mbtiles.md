# Map Task 04 — Offline raster MBTiles packs

## Objective

Implement the first production offline map path using Mapsui/BruTile's mature MBTiles support.


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


## Scope

Support:

- user-imported raster MBTiles;
- MissionPlanner-managed downloaded raster MBTiles only from explicitly approved pack feeds.

Do not scrape hosted provider tiles into MBTiles.
Do not claim vector MBTiles support in this task.

## Pack infrastructure

Add:

```text
OfflineMapPackManifest
InstalledOfflineMapPack
IOfflineMapPackRepository
IOfflineMapPackInstaller
IOfflineMapPackValidator
```

Store under `Maps/Packs/<id>/<version>/` using staging and atomic rename.

Validate manifest, SHA-256, file sizes, MBTiles SQLite schema/metadata, bounds, zooms, projection and declared raster payload. Open archives read-only.

## Mapsui

Add a `MapsuiMbTilesSourceFactory` and expose installed packs through the same basemap controller/catalog.

## UI/API

Support import, install, list, select, verify and remove. Show size, version, coverage, attribution and license notices.

## Tests

Valid/corrupt DB, hash mismatch, path traversal, duplicate version, atomic install, uninstall active source, offline use with network disabled.

## Documentation

Update `docs/MAPS.md` and `docs/FEATURES.md`.
