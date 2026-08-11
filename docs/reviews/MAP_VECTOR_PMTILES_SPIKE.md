# PMTiles and vector renderer spike

This review supplies the evidence behind [ADR-0006](../adr/ADR-0006-defer-vector-pmtiles.md). It is intentionally a decision artifact, not shipped product code.

## Ordered evaluation

1. **Mapsui experimental vector renderer plus direct PMTiles:** rejected for production. PMTiles v3 random access is implementable with bounded reads and validation, but the current Mapsui MVT renderer is explicitly experimental. Complete style, label, sprite, glyph, performance, and three-platform acceptance evidence is absent.
2. **PMTiles-to-vector-MBTiles conversion:** rejected as a workaround. Both containers deliver MVT; changing the container does not solve rendering. It adds conversion integrity and rights/redistribution concerns.
3. **Separate vector renderer:** retained as future research. MapLibre Native is capable on its native platforms, but Mission Planner lacks one supported MAUI integration spanning Windows, Android, and Mac Catalyst. A WebView would also require a new overlay/event/lifecycle architecture.

## Re-entry criteria

A future spike must use a licensed regional archive and run the ADR matrix on physical or representative Windows, Android, and Mac Catalyst targets. It must package style JSON, sprites, glyph/font ranges, and attribution without network access; preserve mission and vehicle overlays plus gestures; validate malformed archives with bounded allocation and cancellation; and publish startup, pan/zoom CPU, and memory measurements. Until all criteria pass, the catalog candidate stays disabled.
