namespace MissionPlanner.Core.Missions.Planning;

/// <summary>
/// Central safety limits for local mission-planning inputs and generated output.
/// </summary>
public static class MissionPlanningLimits
{
    /// <summary>Maximum accepted geospatial source-file size.</summary>
    public const int MaximumImportedFileBytes = 16 * 1024 * 1024;
    /// <summary>Maximum expanded KML payload size.</summary>
    public const int MaximumExpandedGeospatialBytes = 64 * 1024 * 1024;
    /// <summary>Maximum number of features in one geospatial import.</summary>
    public const int MaximumGeospatialFeatures = 10_000;
    /// <summary>Maximum total vertices in one geospatial import.</summary>
    public const int MaximumGeospatialVertices = 500_000;
    /// <summary>Maximum vertices in the active planning polygon.</summary>
    public const int MaximumPolygonVertices = 20_000;
    /// <summary>Maximum generated automatic-waypoint items.</summary>
    public const int MaximumGeneratedMissionItems = 1_000;
    /// <summary>Maximum generated survey points.</summary>
    public const int MaximumSurveyPoints = 4_000;
    /// <summary>Maximum points produced by the text waypoint generator.</summary>
    public const int MaximumTextGeneratorPoints = 1_000;
    /// <summary>Maximum samples in an elevation profile.</summary>
    public const int MaximumTerrainSamples = 10_000;
}
