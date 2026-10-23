# Map Task 09 — Esri integration cleanup

## Objective

Keep all current Esri basemaps working while integrating them with the common catalog, policy, credential/cache and attribution model.


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


## Policy

For the current Mapsui path:

```text
interactive hosted use
approved client caching only
no MissionPlanner tile harvesting
no MissionPlanner MBTiles/PMTiles creation
no redistribution
dynamic/current attribution where the API exposes it
```

Do not implement Esri offline use by scraping. Official ArcGIS offline workflows are outside this adapter.

## Attribution

Fetch official service/style attribution metadata where practical, merge it into the standard attribution engine and retain a conservative fallback.

## Authentication

If current official endpoints require a key/token, move it into the secure credential service. Never log tokens or signed URLs.

## Tests/documentation

Test each current Esri style, attribution success/fallback, credential redaction, cache policy and offline-pack denial. Update `docs/MAPS.md` and `docs/FEATURES.md`.
