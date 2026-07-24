using System.Text.Json.Serialization;

namespace MissionPlanner.Core.ConfigTuning.Planner;

/// <summary>Identifies a supported base-map provider.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PlannerMapProvider>))]
public enum PlannerMapProvider
{
    /// <summary>OpenStreetMap raster tiles.</summary>
    OpenStreetMap,

    /// <summary>Esri-hosted map tiles.</summary>
    Esri
}
