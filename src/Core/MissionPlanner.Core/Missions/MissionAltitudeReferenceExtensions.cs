namespace MissionPlanner.Core.Missions;

internal static class MissionAltitudeReferenceExtensions
{
    public static MissionFrame ToFrame(this MissionAltitudeReference reference)
    {
        return reference switch
        {
            MissionAltitudeReference.Home => MissionFrame.GlobalRelativeAltitude,
            MissionAltitudeReference.MeanSeaLevel => MissionFrame.Global,
            MissionAltitudeReference.Terrain => MissionFrame.GlobalTerrainAltitude,
            var _ => throw new ArgumentOutOfRangeException(nameof(reference))
        };
    }
}
