# Maps

Mission Planner separates map product identity, access, policy, attribution, and renderer integration. The platform-neutral `MissionPlanner.Maps` project owns the versioned catalog and its validation. UI projects adapt approved sources to Mapsui without embedding provider rules in views.

## Catalog model

The embedded catalog is stored at `src/Core/MissionPlanner.Maps/Resources/Maps/builtin-map-catalog.json`. It distinguishes provider, product, concrete source, technical capability, reviewed policy, attribution, and credential requirement. Stable string identifiers are persisted instead of display names or renderer types. `schemaVersion` governs file compatibility; `catalogVersion` identifies catalog content. Serialization is deterministic so changes remain reviewable.

## Built-in sources

The initial catalog represents the existing OpenStreetMap, Esri World Topographic, World Physical, World Shaded Relief, World Dark Gray, and No Map choices. Their current UI behavior is unchanged in this architecture step.

Custom raster sources, raster MBTiles, credentialed hosted providers, and vector PMTiles are present only as disabled future candidates. Presence in the catalog is not approval to enable a provider. A source becomes selectable only after its adapter, policy, attribution, credential, and test work is complete.

## Validation and boundaries

Catalog loading fails closed when identifiers are duplicated, references are missing, zoom ranges are invalid, network endpoints are not HTTP(S), or archive metadata conflicts with the access mechanism. `MissionPlanner.Maps` remains independent of MAUI and Mapsui. Later layers depend inward on the catalog and policy abstractions: platform-neutral services, renderer adapters, then MAUI views and settings.
