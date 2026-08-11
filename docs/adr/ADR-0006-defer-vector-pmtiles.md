# ADR-0006: Defer vector/PMTiles and remain raster/MBTiles

- Status: Accepted
- Date: 2026-08-11
- Decision: Defer vector/PMTiles and remain raster/MBTiles

## Context

Mission Planner needs a dependable offline basemap on Windows, Android, and Mac Catalyst without destabilizing its Mapsui mission editor. PMTiles v3 can contain MVT vector tiles and supports local random access, but archive access is only one part of a complete offline vector product. Production also requires a renderer, styles, sprites, glyph ranges/fonts, attribution, predictable gestures, and operational overlay compatibility on every target.

## Evidence

Approach A, Mapsui plus direct PMTiles, is not production-ready. Mapsui 5.1 is the current application renderer, while its vector-tile implementation is explicitly published as `Mapsui.Experimental.VectorTiles`; Mapsui states experimental packages may contain more bugs and breaking changes in patch releases. The current Mission Planner dependency contains no stable PMTiles reader. A PMTiles v3 reader could be built, but that would not make the experimental MVT/style renderer production-grade. See [Mapsui experimental packages](https://mapsui.com/v5/experimental-packages/) and the [PMTiles v3 specification](https://github.com/protomaps/PMTiles/blob/master/spec/v3/spec.md).

Approach B, conversion to vector MBTiles, does not remove the renderer/style problem. It also creates provenance, storage expansion, conversion integrity, and redistribution questions. Conversion is therefore not justified when archive access is not the primary blocker.

Approach C, a separate renderer, is a future architecture option rather than a safe incremental change. MapLibre Native has mature Android and iOS implementations, but its official project does not provide a complete supported .NET MAUI binding across Windows, Android, and Mac Catalyst; the MAUI integration discussion remains open and platform wrappers are external experiments. A WebView renderer would introduce another event, gesture, offline asset, lifecycle, and overlay-composition boundary. See [MapLibre Native](https://github.com/maplibre/maplibre-native) and its [.NET MAUI bindings discussion](https://github.com/maplibre/maplibre-native/issues/3146).

## Functional matrix

| Area | Windows | Android | Mac Catalyst | Result |
| --- | --- | --- | --- | --- |
| Direct PMTiles v3 random access | Not integrated | Not integrated | Not integrated | Archive reader absent |
| MVT labels and complete styles | Experimental renderer only | Experimental renderer only | Experimental renderer only | Production gate fails |
| Light/dark plus offline sprites/glyphs | Not demonstrated | Not demonstrated | Not demonstrated | Production gate fails |
| Mission route/waypoint and vehicle/follow overlays | Existing raster path passes | Existing raster path baseline | Existing raster path baseline | Vector path unverified |
| Gestures, context actions, source switching | Existing raster path passes | Existing raster path baseline | Existing raster path baseline | Vector path unverified |
| Fully offline regional archive | Raster MBTiles passes | Raster MBTiles implementation shared | Raster MBTiles implementation shared | Use raster MBTiles |
| Memory/CPU/startup responsiveness | No representative vector result | No representative vector result | No representative vector result | Production gate fails |

No real regional Protomaps archive was promoted into product code because the renderer prerequisites fail before cross-platform acceptance testing. This avoids presenting an incomplete archive-only prototype as renderer evidence.

## Decision

Defer vector/PMTiles and remain on the stable raster/MBTiles production path. Keep the disabled PMTiles catalog candidate so the architecture can be revisited without persisting renderer-specific state.

Task 06 is not authorized by this decision and must not execute. Reconsider only when a renderer path is supported on all three targets and a spike demonstrates complete offline styles/assets, overlays, gestures, source switching, and acceptable performance with a real regional archive.

## Consequences

- Raster MBTiles remains the supported offline format.
- No PMTiles reader, vector conversion pipeline, experimental Mapsui package, or separate renderer enters production.
- Operational map behavior and dependency risk remain bounded.
- The functional matrix is a required acceptance checklist for any future superseding ADR.
