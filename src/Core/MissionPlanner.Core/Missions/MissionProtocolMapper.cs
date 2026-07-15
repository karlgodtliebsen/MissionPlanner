using MissionPlanner.MavLink.Missions;

namespace MissionPlanner.Core.Missions;

/// <inheritdoc />
public sealed class MissionProtocolMapper : IMissionProtocolMapper
{
    /// <inheritdoc/>
    public MavLinkMissionItem ToProtocol(MissionItem item, MissionPlanType missionType)
    {
        return item switch
        {
            WaypointMissionItem x => New(x, (float)x.HoldTime.TotalSeconds,
                (float)(x.AcceptanceRadiusMeters ?? 0),
                (float)(x.PassRadiusMeters ?? 0), (float)(x.DesiredYawDegrees ?? float.NaN),
                x.Position, (float)x.Altitude.Meters, missionType),
            TakeoffMissionItem x => New(x, (float)(x.PitchDegrees ?? 0), 0, 0,
                (float)(x.HeadingDegrees ?? float.NaN), x.Position ?? new GeoPosition(0, 0),
                (float)x.Altitude.Meters, missionType),
            LandMissionItem x => New(x, (float)(x.AbortAltitudeMeters ?? 0), 0, 0,
                (float)(x.DesiredYawDegrees ?? float.NaN), x.Position, (float)x.Altitude.Meters, missionType),
            ReturnToLaunchMissionItem x => New(x, 0, 0, 0, 0,
                new GeoPosition(0, 0), 0, missionType),
            ChangeSpeedMissionItem x => New(x, (float)x.SpeedType, (float)x.SpeedMetersPerSecond,
                (float)(x.ThrottlePercent ?? -1), 0,
                new GeoPosition(0, 0), 0, missionType),
            var _ => throw new NotSupportedException(item.GetType().Name)
        };
    }

    private static MavLinkMissionItem New(MissionItem i, float p1, float p2, float p3, float p4, GeoPosition pos, float z, MissionPlanType type)
    {
        return new MavLinkMissionItem(i.Sequence, (byte)i.Frame,
            (ushort)i.Command, i.Sequence == 0,
            i.AutoContinue, p1, p2, p3, p4,
            (int)Math.Round(pos.LatitudeDegrees * 1e7),
            (int)Math.Round(pos.LongitudeDegrees * 1e7), z,
            (MavMissionType)(byte)type);
    }
}
