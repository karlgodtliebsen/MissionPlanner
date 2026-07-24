using System.Text.Json.Serialization;

namespace MissionPlanner.Core.ConfigTuning.Planner;

/// <summary>Identifies the preferred display unit system.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<UnitSystem>))]
public enum UnitSystem
{
    /// <summary>Metric units.</summary>
    Metric,

    /// <summary>Imperial units.</summary>
    Imperial,

    /// <summary>Aviation and nautical units.</summary>
    Aviation
}
