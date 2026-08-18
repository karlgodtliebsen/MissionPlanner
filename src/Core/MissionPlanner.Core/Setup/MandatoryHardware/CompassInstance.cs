using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Describes one discovered compass instance built from parameters and device IDs.</summary>
/// <param name="Index">The one-based compass slot index.</param>
/// <param name="DeviceId">The bus device identifier from COMPASS_DEV_IDn.</param>
/// <param name="Use">Whether the compass is enabled for navigation.</param>
/// <param name="External">Whether the compass is marked external, when the parameter exists.</param>
/// <param name="Orientation">The configured board-rotation enumeration value.</param>
/// <param name="OrientationName">The human-readable orientation name.</param>
/// <param name="Priority">The one-based priority position, or zero when unranked.</param>
/// <param name="MotorCompensationConfigured">Whether motor interference compensation is enabled globally.</param>
/// <param name="OffsetX">The stored X offset.</param>
/// <param name="OffsetY">The stored Y offset.</param>
/// <param name="OffsetZ">The stored Z offset.</param>
/// <param name="Healthy">Aggregate 3D-magnetometer health when reported by the vehicle.</param>
public sealed record CompassInstance(
    int Index,
    uint DeviceId,
    bool Use,
    bool? External,
    int Orientation,
    string OrientationName,
    int Priority,
    bool MotorCompensationConfigured,
    double OffsetX,
    double OffsetY,
    double OffsetZ,
    bool? Healthy)
{
    /// <summary>Gets whether this compass is the highest-priority (primary) instance.</summary>
    public bool IsPrimary => Priority == 1;
}
