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

    public static MissionAltitudeReference ToAltitudeReference(this MissionFrame frame)
    {
        return frame switch
        {
            MissionFrame.GlobalRelativeAltitude => MissionAltitudeReference.Home,
            MissionFrame.Global => MissionAltitudeReference.MeanSeaLevel,
            MissionFrame.GlobalTerrainAltitude => MissionAltitudeReference.Terrain,
            var _ => throw new ArgumentOutOfRangeException(nameof(frame))
        };
    }
}
