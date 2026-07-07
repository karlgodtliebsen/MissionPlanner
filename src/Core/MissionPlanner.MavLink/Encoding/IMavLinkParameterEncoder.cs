using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.MavLink.Encoding;

/// <summary>
/// Encoder for MAVLink parameter-related messages.
/// </summary>
public interface IMavLinkParameterEncoder
{
    /// <summary>
    /// Encodes a PARAM_REQUEST_LIST message to request all parameters from a vehicle.
    /// </summary>
    /// <param name="targetSystemId">The system ID of the target vehicle.</param>
    /// <param name="targetComponentId">The component ID of the target vehicle.</param>
    /// <returns>The encoded MAVLink packet as a byte array.</returns>
    byte[] EncodeParamRequestList(byte targetSystemId, byte targetComponentId);

    /// <summary>
    /// Encodes a PARAM_REQUEST_READ message to request a specific parameter by name.
    /// </summary>
    /// <param name="targetSystemId">The system ID of the target vehicle.</param>
    /// <param name="targetComponentId">The component ID of the target vehicle.</param>
    /// <param name="paramId">The parameter name (max 16 characters).</param>
    /// <param name="paramIndex">The parameter index (-1 to use paramId instead).</param>
    /// <returns>The encoded MAVLink packet as a byte array.</returns>
    byte[] EncodeParamRequestRead(byte targetSystemId, byte targetComponentId, string paramId, short paramIndex = -1);

    /// <summary>
    /// Encodes a PARAM_SET message to set a parameter value on the vehicle.
    /// </summary>
    /// <param name="targetSystemId">The system ID of the target vehicle.</param>
    /// <param name="targetComponentId">The component ID of the target vehicle.</param>
    /// <param name="paramId">The parameter name (max 16 characters).</param>
    /// <param name="paramValue">The parameter value to set.</param>
    /// <param name="paramType">The parameter type.</param>
    /// <returns>The encoded MAVLink packet as a byte array.</returns>
    byte[] EncodeParamSet(byte targetSystemId, byte targetComponentId, string paramId, float paramValue, MavParamType paramType);
}
