# Map platform verification matrix

This checklist records manual verification separately from deterministic automated tests. Do not mark a cell passed without running the current commit on the named platform.

| Scenario | Windows | Android | Mac Catalyst |
|---|---|---|---|
| OpenStreetMap Standard | Pending interactive run | Pending device run | Pending macOS-host run |
| Esri Topographic, Physical, Shaded Relief, Dark Gray | Pending interactive run | Pending device run | Pending macOS-host run |
| Blank / No Map | Pending interactive run | Pending device run | Pending macOS-host run |
| Custom XYZ | Pending interactive run | Pending device run | Pending macOS-host run |
| Raster MBTiles with network disabled | Pending interactive run | Pending device run | Pending macOS-host run |
| Plan mission editing and context actions | Pending interactive run | Pending touch run | Pending mouse/touchpad run |
| Flight Data map and follow vehicle | Pending interactive run | Pending touch run | Pending mouse/touchpad run |
| Pan, zoom, pointer/touch gestures | Pending interactive run | Pending touch run | Pending mouse/touchpad run |
| Light and dark theme attribution/settings | Pending interactive run | Pending device run | Pending macOS-host run |
| Network loss and source fallback | Pending interactive run | Pending device run | Pending macOS-host run |

For each run, record the commit, OS/device version, Mapsui version, source ID, result, and sanitized diagnostic snapshot. Confirm that mission, vehicle, track, fence, ADS-B, POI, guided, and camera overlays remain present when switching basemaps. Confirm attribution remains visible and no credential or signed URL appears in logs or copied diagnostics.

Vector/PMTiles is intentionally absent because ADR-0006 deferred it and conditional Task 06 was not authorized.
