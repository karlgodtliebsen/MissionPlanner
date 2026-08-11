# Map Task 08 — Stadia, Thunderforest and MapTiler

## Objective

Add optional hosted sources through the central policy, credential, attribution and cache services.


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


## Stadia

Use reviewed current terms conservatively: normal hosted use, standard local HTTP cache, only currently permitted limited mobile offline cache, no MissionPlanner redistributable packs and no general proxy. Do not expose a region downloader unless current terms/account limits are explicitly modeled.

## Thunderforest

Use current reviewed terms: on-device caching/offline retention is allowed, caching-proxy redistribution is not. Do not infer generic bulk-prefetch permission without a separate current review. Use honest identification headers.

## MapTiler Cloud

Use interactive hosted use, temporary per-user cache, HTTP-header compliance, no bulk tile download, no MissionPlanner offline-pack export and no proxy unless contract permits it.

## Work

- Add reviewed official endpoints/styles.
- Keep providers disabled until credentials are configured.
- Show provider policy/attribution/cache summary in settings.
- Route all HTTP through the common map HTTP/cache service.
- Handle 401/403/429/network errors distinctly.

## Tests/documentation

Test credentials, redaction, attribution, denied pack/prefetch/proxy operations and switching. Record policy review dates and official terms sources in `docs/MAPS.md`.
