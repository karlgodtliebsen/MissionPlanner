using System.Text.Json.Serialization;

namespace MissionPlanner.Core.ConfigTuning.Planner;

/// <summary>Identifies how cached parameter data is reused.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ParameterCachePolicy>))]
public enum ParameterCachePolicy
{
    /// <summary>Prefer a recent cache and refresh stale data.</summary>
    PreferRecentCache,

    /// <summary>Refresh parameters whenever a connection is established.</summary>
    RefreshOnConnect,

    /// <summary>Always request a complete parameter list.</summary>
    AlwaysRefresh
}
