namespace MissionPlanner.Core.Missions.Planning;

/// <summary>
/// Provides the file path for the JSON Point of Interest repository.
/// </summary>
public interface IJsonPoiFilePathProvider
{
    /// <summary>
    /// Gets the file path for the JSON Point of Interest repository.
    /// </summary>
    /// <returns>The file path as a string.</returns>
    string GetPath();
}
