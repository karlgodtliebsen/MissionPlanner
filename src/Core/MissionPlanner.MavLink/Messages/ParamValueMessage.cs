using MissionPlanner.MavLink.Parameters;
using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Messages;

/// <summary>
/// Represents a MAVLink PARAM_VALUE message.
/// This message is sent by the vehicle to report parameter values.
/// </summary>
/// <param name="SystemId">The ID of the system that sent the message.</param>
/// <param name="ComponentId">The ID of the component that sent the message.</param>
/// <param name="EndPoint">The endpoint from which the message was received.</param>
/// <param name="ParamId">The parameter name (max 16 characters).</param>
/// <param name="ParamValue">The parameter value as a float (wire format).</param>
/// <param name="ParamType">The parameter type.</param>
/// <param name="ParamCount">The total number of parameters on the vehicle.</param>
/// <param name="ParamIndex">The index of this parameter in the list.</param>
/// <param name="ReceivedAt">The timestamp when the message was received.</param>
public sealed record ParamValueMessage(
    byte SystemId,
    byte ComponentId,
    TransportEndPoint EndPoint,
    string ParamId,
    float ParamValue,
    MavParamType ParamType,
    ushort ParamCount,
    ushort ParamIndex,
    DateTimeOffset ReceivedAt)
    : MavLinkMessage(SystemId, ComponentId, MessageIds.ParamValue, EndPoint, ReceivedAt);
