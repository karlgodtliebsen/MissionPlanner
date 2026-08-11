# Map Task 02 — Policy engine, attribution, credentials and HTTP cache

## Objective

Centralize the rules that every hosted map request and visible layer must follow.


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


## Policy

1. Add `IMapPolicyEvaluator` and typed effective decisions:
   - interactive use;
   - client disk cache;
   - offline area download;
   - bulk prefetch;
   - proxy;
   - redistributed pack;
   - static export.
2. Return denial reasons and policy IDs.
3. Encode OSM Standard conservatively: interactive yes, visible attribution, honest User-Agent, HTTP cache yes, prefetch/offline-pack no.

## Attribution

1. Add `IMapAttributionContributor`, `IMapAttributionService` and stable attribution entries.
2. Aggregate from all visible sources/layers and deduplicate.
3. Add one standard map overlay with compact and expanded modes.
4. Expose current attribution to screenshot/export code.
5. Add dynamic attribution resolver support for services such as Esri.

## Credentials

1. Declare required credential type in source metadata.
2. Store real credentials in secure storage only.
3. Expose configured/not-configured state, set/remove/test.
4. Redact diagnostics and URLs.

## HTTP/cache

1. Add a central map HTTP client/factory.
2. Use an honest MissionPlanner User-Agent where permitted/required.
3. Honor Cache-Control/Expires/ETag/Last-Modified.
4. Namespace disk cache by source/style/product identity.
5. Add disk budget and clear-by-source/all.
6. Never use this cache as an offline-pack repository.

## Tests

Cover policy intersection, OSM restrictions, attribution aggregation, secret redaction, HTTP cache lifecycle, namespace isolation and cancellation.

## Documentation

Update `docs/MAPS.md`, `docs/PLANNER_SETTINGS.md` and `docs/FEATURES.md`.
