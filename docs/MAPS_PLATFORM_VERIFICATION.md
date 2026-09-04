# Maps platform verification

The current application project targets `net10.0-windows10.0.19041.0`. Windows is therefore
the required map release target. Linux and macOS remain future Avalonia targets and must not
be reported as supported until dedicated heads, packaging, and interactive verification exist.

| Scenario | Windows |
|---|---|
| OSM and each supported Esri source | Pending interactive verification |
| Blank map and source switching | Pending interactive verification |
| Installed raster MBTiles | Pending interactive verification |
| Custom XYZ/TMS | Pending interactive verification |
| Mission and vehicle overlays | Pending interactive verification |
| Follow vehicle/current-position zoom | Pending interactive verification |
| Mouse, wheel, keyboard, and context actions | Pending interactive verification |
| Light/dark theme and attribution | Pending interactive verification |
| Offline startup and network loss | Pending interactive verification |

Automated builds and map tests verify composition, policy, source resolution, cancellation,
and layer behavior. They do not replace an interactive renderer check. Record the application
commit, Mapsui version, source IDs, test data, and observed result when completing this matrix.
