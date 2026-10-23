# MissionPlanner maps — architecture and Codex tasks

The current application already uses Mapsui + BruTile and has working online basemaps. The purpose of this task set is to add a maintainable provider/policy architecture, attribution, secure credentials, online caching, offline packs and self-hosted sources without destabilizing the mission editor.

Important corrections to the original proposal:

1. PMTiles and MBTiles are archive/container formats, not tile payload formats.
2. OpenMapTiles is primarily a schema/toolchain; it is not automatically a hosted provider or a redistributable data product.
3. Provider identity, data-product identity, concrete source and reviewed usage policy must be separate.
4. Attribution must aggregate all visible layer requirements, not just the basemap.
5. Mapsui/BruTile should remain the production renderer initially.
6. Raster MBTiles is the first stable offline path.
7. PMTiles/vector support is a decision-gated spike because Mapsui vector-tile support is experimental.
8. A loopback HTTP server is a last-resort adapter, not the default architecture.
9. Esri should remain online-only in the current Mapsui integration; official Esri offline workflows are a separate SDK/service architecture.
10. Basemap switching must replace one basemap layer only and preserve viewport plus operational overlays.

Recommended order:

```text
01 architecture/catalog
02 attribution/policy/credentials/cache
03 Mapsui basemap adapter and migration
04 offline raster MBTiles packs
05 PMTiles/vector compatibility spike
06 production vector packs (conditional)
07 custom/self-hosted sources
08 hosted Stadia/Thunderforest/MapTiler
09 Esri cleanup
10 settings/provider/pack UI
11 pack feed and update infrastructure
12 integration tests, diagnostics and documentation
```

Commit after each task. Task 05 is a decision gate; do not execute task 06 unless the resulting ADR approves it.
