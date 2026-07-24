using System.Text.Json.Serialization;

namespace MissionPlanner.Core.ConfigTuning.Planner;

/// <summary>Identifies a Planner settings section.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PlannerSettingsSection>))]
public enum PlannerSettingsSection
{
    /// <summary>Display units.</summary>
    Units,

    /// <summary>Map defaults.</summary>
    Map,

    /// <summary>Telemetry presentation.</summary>
    Telemetry,

    /// <summary>Application appearance.</summary>
    Appearance,

    /// <summary>Logging behavior.</summary>
    Logging,

    /// <summary>Connection defaults.</summary>
    Connection,

    /// <summary>Parameter-cache behavior.</summary>
    ParameterCache,

    /// <summary>Safety confirmations.</summary>
    Confirmations,

    /// <summary>Application updates.</summary>
    Updates,

    /// <summary>Accessibility behavior.</summary>
    Accessibility
}
