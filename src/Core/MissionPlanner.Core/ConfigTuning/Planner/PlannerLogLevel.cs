using System.Text.Json.Serialization;

namespace MissionPlanner.Core.ConfigTuning.Planner;

/// <summary>Identifies the configured application logging threshold.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PlannerLogLevel>))]
public enum PlannerLogLevel
{
    /// <summary>Diagnostic trace logging.</summary>
    Verbose,

    /// <summary>Debug logging.</summary>
    Debug,

    /// <summary>Normal informational logging.</summary>
    Information,

    /// <summary>Warnings and errors only.</summary>
    Warning,

    /// <summary>Errors only.</summary>
    Error
}
