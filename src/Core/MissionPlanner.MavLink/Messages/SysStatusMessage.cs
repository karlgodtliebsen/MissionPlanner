using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Represents the system status of the drone, including battery information.
/// </summary>
/// <param name="SystemId">The ID of the system sending the message.</param>
/// <param name="ComponentId">The ID of the component sending the message.</param>
/// <param name="EndPoint">The endpoint from which the message was received.</param>
/// <param name="BatteryRemaining">The remaining battery percentage.</param>
/// <param name="BatteryVoltage">The battery voltage in volts.</param>
/// <param name="ReceivedAt">The timestamp when the message was received.</param>
/// <param name="BatteryCurrent">The battery current in amperes, or <see langword="null"/> when unknown.</param>
/// <param name="SensorsPresent">The available onboard sensor bitmap.</param>
/// <param name="SensorsEnabled">The enabled onboard sensor bitmap.</param>
/// <param name="SensorsHealthy">The healthy onboard sensor bitmap.</param>
/// <param name="ControllerLoadPercent">The controller load percentage.</param>
/// <param name="CommunicationDropRatePercent">The communication drop rate percentage.</param>
/// <param name="CommunicationErrors">The communication error count.</param>
public sealed record SysStatusMessage(
    byte SystemId,
    byte ComponentId,
    TransportEndPoint EndPoint,
    int? BatteryRemaining,
    float? BatteryVoltage,
    DateTimeOffset ReceivedAt,
    double? BatteryCurrent = null,
    uint? SensorsPresent = null,
    uint? SensorsEnabled = null,
    uint? SensorsHealthy = null,
    double? ControllerLoadPercent = null,
    double? CommunicationDropRatePercent = null,
    ushort? CommunicationErrors = null) : MavLinkMessage(
    SystemId, ComponentId, MessageIds.SysStatus, EndPoint, ReceivedAt);
