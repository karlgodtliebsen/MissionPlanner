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
