# Improved map architecture

## Current context

MissionPlanner already has a shared Mapsui/BruTile mission map used by Plan/FlightData. The existing implementation supports OpenStreetMap plus several Esri basemaps. This architecture should be extended rather than replaced.

## Separate the identities

Use four related concepts:

```text
MapProviderDefinition
    Organization/service operator.

MapDataProductDefinition
    Actual map product/dataset/style family.

MapSourceDefinition
    One concrete hosted endpoint, self-hosted endpoint or local archive.

MapUsagePolicy
    MissionPlanner's reviewed rules for that source/product/provider combination.
```

This avoids treating OpenMapTiles, PMTiles, a user endpoint and a commercial hosted service as equivalent concepts.

## Separate access, archive and payload

```csharp
public enum MapAccessKind
{
    HttpXyz,
    HttpTms,
    Wmts,
    Wms,
    LocalArchive,
    LocalDirectory,
    Blank
}

public enum MapArchiveFormat
{
    None,
    MbTiles,
    PmTiles
}

public enum MapTileContentFormat
{
    RasterPng,
    RasterJpeg,
    RasterWebp,
    VectorMvt
}
```

Examples:

```text
LocalArchive + MbTiles + RasterPng
LocalArchive + PmTiles + VectorMvt
HttpXyz + None + RasterPng
```

## Keep one basemap slot

The Mapsui map composition should be:

```text
Basemap slot
    exactly one currently selected basemap

Operational layers
    mission route
    waypoints/home
    vehicle
    flight track
    fence/rally
    ADS-B
    POI
    guided/camera overlays
```

Changing a provider swaps only the basemap slot and attribution. It must not rebuild the whole map or destroy mission editor state.

## Renderer boundary

Do not create a broad `IMapRenderer` yet. There is only one production renderer.

Use renderer-neutral catalog/policy/pack models and a focused UI adapter:

```text
MissionPlanner.Maps
    catalog
    usage policy
    attribution aggregation
    pack manifests/repository
    cache abstractions

MissionPlanner.AvaloniaUI.App/Maps/Mapsui
    IMapsuiBasemapFactory
    MapsuiBasemapFactory
    MapsuiMbTilesSourceFactory
    MapsuiOnlineRasterSourceFactory
```

Only introduce a renderer abstraction when a second renderer is genuinely adopted.

## Capability versus policy

Technical capability and MissionPlanner's reviewed permission are not the same thing.

```csharp
public sealed record MapSourceCapabilities(
    bool SupportsInteractiveUse,
    bool SupportsClientCache,
    bool SupportsOfflineAreaDownload,
    bool SupportsBulkPrefetch,
    bool SupportsProxy,
    bool SupportsRedistribution,
    bool SupportsStaticExport);

public sealed record MapUsagePolicy(
    string PolicyId,
    DateOnly ReviewedAt,
    Uri TermsUri,
    string ReviewSummary,
    bool AllowInteractiveUse,
    bool AllowClientDiskCache,
    bool AllowOfflineAreaDownload,
    bool AllowBulkPrefetch,
    bool AllowProxy,
    bool AllowRedistributedPack,
    bool AllowStaticExport,
    bool RespectHttpCacheHeaders,
    TimeSpan? FallbackCacheRetention);
```

Effective behavior is the intersection of capability and reviewed policy.

Do not scrape legal pages and dynamically reinterpret contracts at runtime. Store a review date, policy ID and official terms URL so policy can be audited and updated in code/catalog releases.

## Attribution aggregation

Every visible source/layer can contribute attribution. Add:

```csharp
public interface IMapAttributionContributor
{
    IReadOnlyList<MapAttributionEntry> GetAttribution();
}
```

Aggregate and deduplicate entries for the standard on-map attribution control. Dynamic metadata can be fetched where a service exposes it, such as Esri attribution metadata.

## Packs versus cache

Use separate roots and services:

```text
Maps/Packs/
Maps/Cache/
```

Pack:

```text
explicit install/import
versioned
checksum verified
durable
user manageable
usable offline
```

Cache:

```text
provider/source scoped
HTTP-header driven
evictable
not exported as a pack
not described as offline map content
```

## Offline manifest

A pack manifest should include:

```text
schema version
pack ID/version
display name
source/data-product IDs
archive format
tile content format
projection
bounds
zoom range
file size/hash
attribution IDs
license/notice files
source provenance
retrieved/created timestamp
```

Open imported MBTiles read-only and validate schema/metadata before use.

## Provider-specific policy baseline

### OSM Standard community tiles

Use only for normal interactive viewing. Configure an honest MissionPlanner User-Agent, show OSM attribution, honor HTTP caching headers, and disable bulk/offline-area download or pack creation.

### Protomaps

The Protomaps basemap is a downloadable ODbL Produced Work and needs visible OpenStreetMap attribution. Current licensing guidance permits free unmodified redistribution. However, it is vector PMTiles, so MissionPlanner should not advertise it as production offline support until the vector/PMTiles spike passes.

### OpenMapTiles

Treat OpenMapTiles primarily as schema/generation technology. Do not infer rights for arbitrary OpenMapTiles endpoints or packs. Maps derived from the schema also require OpenMapTiles attribution in addition to applicable data attribution.

### MapTiler Cloud

Use a conservative policy: interactive hosted use, temporary single-user client cache, HTTP-header compliance, no bulk download, no MissionPlanner offline pack creation and no proxy without an agreement.

### Thunderforest

Current terms permit on-device caching/offline retention and prohibit caching-proxy redistribution. Do not automatically interpret this as permission for a generic MissionPlanner region-prefetch feature.

### Stadia Maps

Current terms permit limited local mobile offline cache but not unrestricted bulk download/proxy/redistributable packs. Treat this as cache behavior, not a durable MissionPlanner map pack.

### Esri

For the current Mapsui integration, keep Esri basemaps online-only. Esri supports official offline workflows through its own APIs/SDKs, but MissionPlanner should not create an equivalent by harvesting hosted tiles.

## PMTiles/vector decision gate

Mapsui/BruTile has stable raster/MBTiles support. Current Mapsui vector-tile support is experimental and PMTiles is not a first-class production path. Therefore:

```text
Phase 1 production:
    existing raster online sources
    raster offline MBTiles
    custom/self-hosted raster XYZ/TMS/WMS/WMTS

Phase 2 spike:
    MVT rendering
    PMTiles reader
    Protomaps styles/glyphs/sprites
    mission overlays
    Windows/Android/macOS performance

Phase 3 conditional:
    production Protomaps/vector offline packs
```

Do not add a loopback server unless a documented spike proves direct source integration impossible and the added security/lifecycle complexity is justified.
