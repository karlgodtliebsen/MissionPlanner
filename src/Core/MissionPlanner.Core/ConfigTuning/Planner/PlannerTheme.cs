using System.Text.Json.Serialization;

namespace MissionPlanner.Core.ConfigTuning.Planner;

/// <summary>Identifies the application color theme.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PlannerTheme>))]
public enum PlannerTheme
{
    /// <summary>Follow the operating-system theme.</summary>
    System,

    /// <summary>Use the light theme.</summary>
    Light,

    /// <summary>Use the dark theme.</summary>
    Dark
}
