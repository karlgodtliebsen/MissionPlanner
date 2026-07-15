using MissionPlanner.Core.Missions.Abstractions;
using MissionPlanner.Core.Missions.Models;
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
            LoiterMissionItem x => New(x,
                x.Time is not null ? (float)x.Time.Value.TotalSeconds : (float)(x.Turns ?? 0), 0,
                (float)(x.RadiusMeters ?? 0), (float)(x.DesiredYawDegrees ?? float.NaN),
                x.Position, (float)x.Altitude.Meters, missionType),
            var _ => throw new NotSupportedException(item.GetType().Name)
        };
    }

    /// <inheritdoc />
    public MissionItem FromProtocol(MavLinkMissionItem item)
    {
        var id = MissionItemId.New();
        var position = new GeoPosition(item.X / 1e7, item.Y / 1e7);
        var altitude = new MissionAltitude(item.Z, Frame(item).ToAltitudeReference());

        return (MissionCommand)item.Command switch
        {
            MissionCommand.NavigateWaypoint => new WaypointMissionItem(id, item.Sequence, position, altitude,
                TimeSpan.FromSeconds(item.Param1), NullIfZero(item.Param2), NullIfZero(item.Param3),
                NullIfNaN(item.Param4), item.AutoContinue),
            MissionCommand.LoiterUnlimited => new LoiterMissionItem(id, item.Sequence, position, altitude,
                null, null, NullIfZero(item.Param3), NullIfNaN(item.Param4), item.AutoContinue),
            MissionCommand.LoiterTurns => new LoiterMissionItem(id, item.Sequence, position, altitude,
                null, item.Param1, NullIfZero(item.Param3), NullIfNaN(item.Param4), item.AutoContinue),
            MissionCommand.LoiterTime => new LoiterMissionItem(id, item.Sequence, position, altitude,
                TimeSpan.FromSeconds(item.Param1), null, NullIfZero(item.Param3),
                NullIfNaN(item.Param4), item.AutoContinue),
            MissionCommand.ReturnToLaunch => new ReturnToLaunchMissionItem(id, item.Sequence, item.AutoContinue),
            MissionCommand.Land => new LandMissionItem(id, item.Sequence, position, altitude,
                NullIfZero(item.Param1), NullIfNaN(item.Param4), item.AutoContinue),
            MissionCommand.Takeoff => new TakeoffMissionItem(id, item.Sequence,
                item is { X: 0, Y: 0 } ? null : position, altitude,
                NullIfZero(item.Param1), NullIfNaN(item.Param4), item.AutoContinue),
            MissionCommand.ChangeSpeed => new ChangeSpeedMissionItem(id, item.Sequence,
                (MissionSpeedType)(byte)item.Param1, item.Param2,
                item.Param3 < 0 ? null : item.Param3, item.AutoContinue),
            var _ => throw new NotSupportedException($"Mission command {item.Command} is not supported.")
        };
    }

    private static MissionFrame Frame(MavLinkMissionItem item)
    {
        return Enum.IsDefined(typeof(MissionFrame), item.Frame)
            ? (MissionFrame)item.Frame
            : throw new NotSupportedException($"Mission frame {item.Frame} is not supported.");
    }

    private static double? NullIfZero(float value)
    {
        return value == 0 ? null : value;
    }

    private static double? NullIfNaN(float value)
    {
        return float.IsNaN(value) ? null : value;
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
