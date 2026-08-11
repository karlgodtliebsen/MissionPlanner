using MissionPlanner.Maps.Attribution;

namespace MissionPlanner.Maps.Diagnostics;

/// <summary>Provides required attribution to future screenshot, static-image, and PDF exporters.</summary>
public static class MapExportAttribution
{
    /// <summary>Builds a footer from entries that require attribution in exported output.</summary>
    public static string CreateFooter(MapAttributionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return string.Join(" · ", snapshot.OnExport.Select(value => value.Text).Distinct(StringComparer.Ordinal));
    }
}
