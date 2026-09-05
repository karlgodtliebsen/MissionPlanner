using MissionPlanner.Core.Missions.Planning;

namespace MissionPlanner.AvaloniaUI.App.Configuration;

/// <summary>
/// Provides the file path for storing JSON points of interest.
/// </summary>
public class JsonPoiFilePathProvider : IJsonPoiFilePathProvider
{
    /// <inheritdoc />
    public string GetPath()
    {
        var path = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "Planning", "points-of-interest.json");
        return path;
    }
}
