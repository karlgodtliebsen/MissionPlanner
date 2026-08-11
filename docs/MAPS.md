# Maps

Mission Planner separates map product identity, access, policy, attribution, and renderer integration. The platform-neutral `MissionPlanner.Maps` project owns the versioned catalog and its validation. UI projects adapt approved sources to Mapsui without embedding provider rules in views.

## Catalog model

The embedded catalog is stored at `src/Core/MissionPlanner.Maps/Resources/Maps/builtin-map-catalog.json`. It distinguishes provider, product, concrete source, technical capability, reviewed policy, attribution, and credential requirement. Stable string identifiers are persisted instead of display names or renderer types. `schemaVersion` governs file compatibility; `catalogVersion` identifies catalog content. Serialization is deterministic so changes remain reviewable.

## Built-in sources

The initial catalog represents the existing OpenStreetMap, Esri World Topographic, World Physical, World Shaded Relief, World Dark Gray, and No Map choices. Their current UI behavior is unchanged in this architecture step.

Custom raster sources, raster MBTiles, credentialed hosted providers, and vector PMTiles are present only as disabled future candidates. Presence in the catalog is not approval to enable a provider. A source becomes selectable only after its adapter, policy, attribution, credential, and test work is complete.

## Validation and boundaries

Catalog loading fails closed when identifiers are duplicated, references are missing, zoom ranges are invalid, network endpoints are not HTTP(S), or archive metadata conflicts with the access mechanism. `MissionPlanner.Maps` remains independent of MAUI and Mapsui. Later layers depend inward on the catalog and policy abstractions: platform-neutral services, renderer adapters, then MAUI views and settings.

## Policy guardrails

Every operation is evaluated as the intersection of a source's technical capability and its reviewed policy. Decisions are typed, carry the policy identifier, and explain denials. Interactive use, client cache, offline area download, bulk prefetch, proxying, redistribution, and static export are separate decisions. Unknown operations, proxying, and redistribution fail closed.

OpenStreetMap Standard is configured conservatively: interactive use, visible attribution, an honest MissionPlanner User-Agent, and an HTTP-compliant bounded client cache are allowed. Bulk prefetch and offline pack creation are denied. Policy metadata is an application guardrail, not legal advice or a live terms parser.

## Attribution, credentials, and HTTP

Visible layers contribute stable attribution entries to one deduplicated snapshot. The snapshot supplies compact and expanded overlay text and a separate export list. A dynamic resolver can add response- or viewport-specific attribution, including Esri service attribution.

Catalog entries declare only a credential type. Real values use the existing Planner secure-storage abstraction through a map adapter and never enter the catalog, ordinary settings, export, URL diagnostics, or logs. The credential service exposes configured state plus set, remove, and test operations without returning the secret to presentation code.

Map HTTP clients use bounded timeouts, cancellation, and an honest User-Agent. Their disk cache stores HTTP responses by source, product, and style namespace, retains standard expiry and validator metadata, enforces a disk budget, and supports clearing one namespace or all namespaces. It is not an offline-pack repository.

## Mapsui basemap adapter

`MapsuiBasemapFactory` is the only built-in source-to-Mapsui construction boundary for the mission map. It resolves stable catalog IDs, requires an enabled source and an affirmative interactive-use policy decision, and creates the existing OpenStreetMap and Esri BruTile layers or a blank `No Map` layer.

`MapBasemapController` owns one layer named `MissionPlanner.Basemap`. Switching creates the replacement before changing the map, inserts only that slot, preserves the navigator viewport and every operational layer, then disposes the previous source. Creation or policy failure leaves the prior working layer installed. A successful change event is the hook for refreshing the standard attribution snapshot.

## Offline raster MBTiles

Raster MBTiles is the first production offline format. Users may import archives for which they hold the necessary rights; managed downloads are limited to explicitly approved pack feeds. Mission Planner never scrapes hosted tile services into MBTiles, and vector MBTiles is not claimed as supported.

Each pack has a manifest containing stable ID, version, display name, size, SHA-256, WGS84 bounds, zoom range, `EPSG:3857` projection, raster payload format, attribution, and license notice. Installation writes to an isolated staging directory, validates the manifest/hash/size and SQLite `metadata`/`tiles` schema plus a representative raster payload, then atomically renames to `Maps/Packs/<id>/<version>/`. Installed archives are opened read-only. A duplicate version is rejected, partial staging is removed on failure, and an active pack must be deselected before removal.

`IOfflineMapPackRepository`, `IOfflineMapPackInstaller`, and `IOfflineMapPackValidator` provide list, find, install/import, verify, and remove APIs. `MapsuiMbTilesSourceFactory` exposes a validated installed pack as the same stable basemap layer used by the controller, so operation overlays remain independent and no network client is involved during tile reads.

## Vector and PMTiles decision

[ADR-0006](adr/ADR-0006-defer-vector-pmtiles.md) defers production vector/PMTiles support. PMTiles v3 archive access is feasible, but Mapsui's MVT renderer remains explicitly experimental and converting to vector MBTiles does not solve style/rendering compatibility. A separate MapLibre or WebView renderer would be a future migration with its own cross-platform lifecycle and overlay architecture. The disabled catalog candidate remains a placeholder only; conditional map Task 06 is not authorized.

## Custom and self-hosted sources

Users can configure raster XYZ/TMS, WMS, and WMTS services without code changes; local raster MBTiles continues through the pack API. Non-secret settings include stable ID, display name, endpoint/template, zooms, WMS/WMTS layer/style/matrix values, credential type, attribution, and technical cache preference. The JSON store uses staged atomic replacement. Credentials are stored separately through secure storage and source URLs containing secret query values are rejected.

Validation requires absolute HTTP(S) endpoints, XYZ/TMS `{z}/{x}/{y}` placeholders, valid zooms, service-specific identifiers, and attribution. Plain HTTP produces a prominent warning. WMS/WMTS test-connection calls are bounded and cancellable, parse capabilities metadata, confirm the configured layer, redact failures, and record a presentation-ready status. Deleting the selected custom source returns the safe `osm-standard` fallback before renderer switching.

The `UserControlled` policy permits interactive rendering and an optional protocol-aware HTTP cache. It does not assert offline-pack, export, proxy, or redistribution rights; the operator remains responsible for source terms and attribution. Custom vector sources remain unavailable because ADR-0006 did not approve a vector renderer.

## Optional hosted providers

Stadia Outdoors, Thunderforest Outdoors, and MapTiler Streets are catalogued raster sources but remain unselectable until their API key is present in secure storage. Keys are injected only while constructing an HTTP request: Stadia uses its authorization header; Thunderforest and MapTiler use their documented query parameter. Catalog JSON and diagnostics never contain the values. All rendering requests use the common bounded HTTP client, and the standard attribution service shows provider and underlying OpenStreetMap/OpenMapTiles credits.

Policies were reviewed on 2026-08-11 against official sources:

| Provider | Enabled operations | Explicitly unavailable | Review source |
| --- | --- | --- | --- |
| Stadia Maps | Interactive hosted use; HTTP-compliant local cache | General region downloader, proxy, redistributable pack, export. Limited mobile offline caching is not exposed because its subscription/device/100 MB conditions are not modeled. | [Stadia raster/authentication](https://docs.stadiamaps.com/raster/), [limited mobile offline guidance](https://docs.stadiamaps.com/tutorials/offline-maps-with-flutter-maplibre-gl/) |
| Thunderforest | Interactive hosted use; on-device cache/retention | Generic bulk prefetch, caching proxy, redistribution, pack export | [Thunderforest terms](https://www.thunderforest.com/terms/), [tile API](https://www.thunderforest.com/docs/tile-numbering/) |
| MapTiler Cloud | Interactive hosted use; temporary single-user cache following HTTP headers | Bulk tile download, export, proxy, redistribution without a custom agreement | [MapTiler Cloud terms](https://www.maptiler.com/terms/cloud/), [cache-header guidance](https://docs.maptiler.com/guides/maps-apis/maps-platform/how-are-the-tile-requests-cached-in-web-browser/) |

Settings can present each source's credential state, attribution, and effective policy summary. Provider failures are categorized as missing credentials, authorization (401/403), rate/quota limit (429), network, or unexpected provider response, with secret-bearing transport details excluded.

## Esri integration

The existing World Topographic, World Physical, World Shaded Relief, and World Dark Gray sources retain their established Mapsui/BruTile rendering. All four resolve through catalog IDs and the common Esri policy reviewed on 2026-08-11. That policy permits interactive hosted use and approved HTTP caching, and denies tile harvesting, MBTiles/PMTiles creation, bulk prefetch, proxying, and redistribution. Official ArcGIS offline workflows and packages are separate from this adapter; Mission Planner does not emulate them by scraping tiles. See [ArcGIS static basemap tiles](https://developers.arcgis.com/rest/static-basemap-tiles/) and [ArcGIS attribution guidance](https://developers.arcgis.com/documentation/glossary/data-attribution/).

`EsriAttributionResolver` requests the current MapServer JSON metadata through the bounded HTTP client and merges `copyrightText` with the conservative “Powered by Esri” fallback. Network, parse, or empty-metadata failures retain the fallback. Public current endpoints require no credential; the optional Esri token helper exists for future secured services and appends a token only at the request boundary, while diagnostic rendering always redacts it.

## Map settings

Config > Planner presents sources by operator purpose: Offline packs, Self-hosted/custom, Online providers, and Blank map. It deliberately does not group by XYZ, WMS, WMTS, or renderer implementation. Selecting a source displays provider/product, connectivity, raster/vector format, credential state, attribution, cache and pack policy, review date, and terms metadata from the validated catalog.

The selected source is persisted by stable `SelectedSourceId`. A deleted source falls back to `osm-standard`; offline startup prefers an installed pack and otherwise uses the blank map; a credential-gated source without a configured secret is not selected. HTTP cache enablement and its bounded disk limit are non-secret settings. The cache remains visually and operationally separate from installed offline packs.

Credential entry is transient: Set writes directly to secure storage and clears the field, Remove deletes it, and Test reports only state/provider validation. Stored values are never read back into a bindable property. The pack APIs expose list/import/install/select/remove/verify operations and manifest coverage, zoom, size, version, attribution, and license details for the settings presentation.
