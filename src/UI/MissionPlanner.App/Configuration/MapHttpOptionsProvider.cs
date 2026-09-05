using MissionPlanner.Maps.Http;

namespace MissionPlanner.App.Configuration;

/// <summary>
///  Provides the HTTP options for map requests.
/// </summary>
public class MapHttpOptionsProvider
{
    /// <inheritdoc />
    public MapHttpOptions GetOptions()
    {
        return new MapHttpOptions(
            $"MissionPlanner/{typeof(App).Assembly.GetName().Version?.ToString(3) ?? "unknown"} (+https://ardupilot.org/planner/)",
            TimeSpan.FromSeconds(20));
    }
}
