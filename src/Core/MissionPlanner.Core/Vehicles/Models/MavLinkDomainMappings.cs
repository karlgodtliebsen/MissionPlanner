using ProtocolAutopilot = MissionPlanner.MavLink.Generated.MavAutopilot;
using ProtocolCapability = MissionPlanner.MavLink.Generated.MavProtocolCapability;
using ProtocolVehicleType = MissionPlanner.MavLink.Generated.MavType;

namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>
/// Maps generated MAVLink protocol enums into deliberately smaller domain concepts.
/// </summary>
public static class MavLinkDomainMappings
{
    /// <summary>
    /// Maps a protocol vehicle type and autopilot to a Mission Planner firmware family.
    /// </summary>
    /// <param name="mavType">The generated MAV_TYPE value.</param>
    /// <param name="autopilot">The generated MAV_AUTOPILOT value.</param>
    /// <returns>The domain firmware family, or <see cref="FirmwareFamily.Unknown"/>.</returns>
    public static FirmwareFamily ToFirmwareFamily(
        this ProtocolVehicleType mavType,
        ProtocolAutopilot autopilot)
    {
        if (autopilot != ProtocolAutopilot.ArduPilotMega)
        {
            return FirmwareFamily.Unknown;
        }

        return mavType switch
        {
            ProtocolVehicleType.FixedWing => FirmwareFamily.ArduPlane,
            ProtocolVehicleType.Quadrotor
                or ProtocolVehicleType.Coaxial
                or ProtocolVehicleType.Helicopter
                or ProtocolVehicleType.Hexarotor
                or ProtocolVehicleType.Octorotor
                or ProtocolVehicleType.Tricopter
                or ProtocolVehicleType.Dodecarotor
                or ProtocolVehicleType.Decarotor
                or ProtocolVehicleType.GenericMultirotor => FirmwareFamily.ArduCopter,
            ProtocolVehicleType.GroundRover or ProtocolVehicleType.SurfaceBoat => FirmwareFamily.Rover,
            ProtocolVehicleType.Submarine => FirmwareFamily.ArduSub,
            ProtocolVehicleType.AntennaTracker => FirmwareFamily.AntennaTracker,
            ProtocolVehicleType.Airship => FirmwareFamily.Blimp,
            _ => FirmwareFamily.Unknown
        };
    }

    /// <summary>
    /// Projects protocol capability bits into the subset used by the vehicle domain.
    /// </summary>
    /// <param name="capabilities">The generated MAV_PROTOCOL_CAPABILITY flags.</param>
    /// <returns>The supported domain capability flags.</returns>
    public static MavProtocolCapability ToDomainCapabilities(this ProtocolCapability capabilities)
    {
        var result = MavProtocolCapability.None;
        if ((capabilities & ProtocolCapability.ParamFloat) != 0)
        {
            result |= MavProtocolCapability.ParamFloat;
        }

        if ((capabilities & ProtocolCapability.ParamEncodeBytewise) != 0)
        {
            result |= MavProtocolCapability.ParamUnion;
        }

        if ((capabilities & ProtocolCapability.Ftp) != 0)
        {
            result |= MavProtocolCapability.Ftp;
        }

        return result;
    }
}
