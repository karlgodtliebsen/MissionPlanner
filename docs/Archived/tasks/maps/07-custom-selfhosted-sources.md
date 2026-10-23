# Map Task 07 — Custom and self-hosted sources

## Objective

Allow users to add their own map services without code changes and without MissionPlanner pretending to know their redistribution rights.


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


## Source types

Implement reviewed support for:

```text
custom raster XYZ/TMS
custom WMS
custom WMTS
self-hosted raster tiles
local raster MBTiles
vector custom source only if task 05 approved it
```

## Configuration

Store non-secret source settings in planner settings. Keep credentials in secure storage.

Validate URL/templates, placeholders, min/max zoom, WMS/WMTS metadata and attribution.
Prefer HTTPS and warn on plain HTTP.
Redact query secrets from diagnostics.

## Policy

Use a conservative `UserControlled` policy. MissionPlanner may support technical caching but must not automatically claim offline-pack or redistribution rights.

## UI

Add/edit/test/delete with source type, endpoint, credential state, attribution preview, cache mode and last connection status.

## Tests/documentation

Cover validation, metadata parsing, redaction, deletion/fallback and provider switching. Update `docs/MAPS.md`, `docs/PLANNER_SETTINGS.md`, `docs/FEATURES.md`.
