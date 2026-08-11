using System.Net;
using MissionPlanner.Maps.Policy;

namespace MissionPlanner.Maps.Http;

/// <summary>Configures bounded map HTTP requests.</summary>
/// <param name="UserAgent">Honest application User-Agent.</param>
/// <param name="Timeout">Per-request timeout.</param>
public sealed record MapHttpOptions(string UserAgent, TimeSpan Timeout)
{
    /// <summary>Gets safe default map HTTP options.</summary>
    public static MapHttpOptions Default { get; } = new(
        $"MissionPlanner/{typeof(MapHttpOptions).Assembly.GetName().Version?.ToString(3) ?? "unknown"} (+https://ardupilot.org/planner/)",
        TimeSpan.FromSeconds(20));
}
