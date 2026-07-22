namespace MissionPlanner.MavLink.Encoding;

/// <summary>
/// 
/// </summary>
public interface IMavLinkCommandEncoder
{
    /// <summary>
    /// Encodes a MAVLink 2 COMMAND_LONG packet.
    /// </summary>
    /// <param name="targetSystemId">The target system ID.</param>
    /// <param name="targetComponentId">The target component ID.</param>
    /// <param name="commandId">The MAV_CMD identifier.</param>
    /// <param name="parameters">Up to seven command parameters.</param>
    /// <returns>The encoded MAVLink packet.</returns>
    byte[] EncodeCommandLong(byte targetSystemId, byte targetComponentId, ushort commandId, IReadOnlyList<float> parameters);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="targetSystemId"></param>
    /// <param name="targetComponentId"></param>
    /// <param name="arm"></param>
    /// <returns></returns>
    byte[] EncodeArmDisarm(byte targetSystemId, byte targetComponentId, bool arm);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="vehicleIdSystemId"></param>
    /// <param name="vehicleIdComponentId"></param>
    /// <param name="customMode"></param>
    /// <returns></returns>
    byte[] EncodeSetMode(byte vehicleIdSystemId, byte vehicleIdComponentId, uint customMode);
}
