using System.Text.Json.Serialization;

namespace MissionPlanner.Core.ConfigTuning.Planner;

/// <summary>Identifies the requested map presentation style.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PlannerMapStyle>))]
public enum PlannerMapStyle
{
    /// <summary>Standard street-map styling.</summary>
    Standard,

    /// <summary>Satellite imagery styling.</summary>
    Topographic,

    /// <summary>Terrain-focused styling.</summary>
    Physical,

    /// <summary>Shaded-relief styling.</summary>
    ShadedRelief,

    /// <summary>Dark-gray styling.</summary>
    DarkGray
}
