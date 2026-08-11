# Map Task 11 — Approved pack feeds and updates

## Objective

Provide a safe way for MissionPlanner to publish/install known offline packs without turning arbitrary hosted map services into download sources.


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


## Feed

Define a versioned signed/HTTPS manifest feed containing only MissionPlanner-reviewed pack artifacts.

Each entry includes:

```text
pack ID/version
source/data-product IDs
coverage
archive/payload format
zoom range
size
SHA-256
download URI
license/notice references
minimum MissionPlanner/renderer compatibility
```

## Installer

- bounded download;
- cancellation/progress;
- staging;
- checksum validation;
- pack validation;
- atomic activation;
- old-version cleanup policy;
- rollback on failure.

Do not derive an offline pack URI from a hosted provider tile URL.

## Protomaps

Only add an approved Protomaps-derived feed if task 05/06 approved vector support and the pack includes all offline rendering assets and required notices.

## Tests/documentation

Test malformed feed, hash mismatch, downgrade/upgrade, partial download, disk full, incompatible renderer and rollback. Document provenance and update behavior in `docs/MAPS.md`.
