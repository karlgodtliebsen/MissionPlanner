using MissionPlanner.Core.Missions.Models;
using MissionPlanner.MavLink.Generated;
using MissionPlanner.MavLink.Missions;
using ProtocolMissionType = MissionPlanner.MavLink.Missions.MavMissionType;

namespace MissionPlanner.Core.Missions.Rally;

/// <summary>Default rally-point MAVLink mapper.</summary>
public sealed class RallyProtocolMapper : IRallyProtocolMapper
{
    /// <inheritdoc />
    public IReadOnlyList<MavLinkMissionItem> ToProtocol(RallyPlan plan) => plan.Points.Select((point, index) => new MavLinkMissionItem(
        checked((ushort)index), (byte)ToFrame(point.Altitude.Reference), (ushort)MavCmd.NavRallyPoint, false, true,
        0, 0, 0, 0, checked((int)Math.Round(point.Position.LatitudeDegrees * 1e7)),
        checked((int)Math.Round(point.Position.LongitudeDegrees * 1e7)), (float)point.Altitude.Meters, ProtocolMissionType.Rally)).ToArray();

    /// <inheritdoc />
    public RallyPlan FromProtocol(IReadOnlyList<MavLinkMissionItem> items)
    {
        var points = new List<RallyPoint>();
        foreach (var item in items.OrderBy(item => item.Sequence))
        {
            if (item.MissionType != ProtocolMissionType.Rally || item.Command != (ushort)MavCmd.NavRallyPoint)
                throw new InvalidDataException("The vehicle returned a non-rally mission item.");
            var position = new GeoPosition(item.X / 1e7d, item.Y / 1e7d);
            if (!position.IsValid) throw new InvalidDataException("The vehicle returned an invalid rally coordinate.");
            points.Add(new(RallyPointId.New(), position, new MissionAltitude(item.Z, FromFrame((MissionFrame)item.Frame))));
        }
        return new(points);
    }

    private static MissionFrame ToFrame(MissionAltitudeReference reference) => reference switch
    { MissionAltitudeReference.MeanSeaLevel => MissionFrame.Global, MissionAltitudeReference.Terrain => MissionFrame.GlobalTerrainAltitude, _ => MissionFrame.GlobalRelativeAltitude };
    private static MissionAltitudeReference FromFrame(MissionFrame frame) => frame switch
    { MissionFrame.Global => MissionAltitudeReference.MeanSeaLevel, MissionFrame.GlobalTerrainAltitude => MissionAltitudeReference.Terrain,
        MissionFrame.GlobalRelativeAltitude => MissionAltitudeReference.Home, _ => throw new InvalidDataException($"Rally frame {frame} is unsupported.") };
}
