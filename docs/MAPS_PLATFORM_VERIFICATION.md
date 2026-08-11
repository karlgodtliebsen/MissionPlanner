# Map platform verification matrix

This checklist records manual verification separately from deterministic automated tests. Do not mark a cell passed without running the current commit on the named platform.

| Scenario | Windows | Android | Mac Catalyst |
|---|---|---|---|
| OpenStreetMap Standard | Not run | Not run | Not run |
| Esri Topographic, Physical, Shaded Relief, Dark Gray | Not run | Not run | Not run |
| Blank / No Map | Not run | Not run | Not run |
| Custom XYZ | Not run | Not run | Not run |
| Raster MBTiles with network disabled | Not run | Not run | Not run |
| Plan mission editing and context actions | Not run | Not run | Not run |
| Flight Data map and follow vehicle | Not run | Not run | Not run |
| Pan, zoom, pointer/touch gestures | Not run | Not run | Not run |
| Light and dark theme attribution/settings | Not run | Not run | Not run |
| Network loss and source fallback | Not run | Not run | Not run |

For each run, record the commit, OS/device version, Mapsui version, source ID, result, and sanitized diagnostic snapshot. Confirm that mission, vehicle, track, fence, ADS-B, POI, guided, and camera overlays remain present when switching basemaps. Confirm attribution remains visible and no credential or signed URL appears in logs or copied diagnostics.

Automated restore, compilation, and deterministic tests are evidence for implementation and runtime composition, not manual UI verification. Vector/PMTiles is intentionally absent because ADR-0006 deferred it and the ADR's conditional vector task was not authorized.

## Automated verification record

Run on 2026-08-11 at commit worktree state preceding the Task 07 commit:

- `dotnet restore src/MissionPlanner.slnx`: Passed; all 23 projects restored/up to date and no NuGet vulnerability warning was reported.
- Map deterministic tests (`FullyQualifiedName~Maps`): Passed, 112 of 112.
- Full solution build: Not passed as an all-platform validation; Windows compilation reached the application and test assemblies, while Android packaging failed in the local toolchain because `java.exe` exited with code 2.
- Full repository test sweep: Not passed. Existing non-map Core assertions (including vehicle display-name expectations) failed, and simulator smoke tests failed without their required live simulator/network environment. These failures do not count as manual map verification and were not relabeled as passed.
