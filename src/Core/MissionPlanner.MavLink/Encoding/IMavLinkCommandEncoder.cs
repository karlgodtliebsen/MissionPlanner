namespace MissionPlanner.MavLink.Encoding;

/// <summary>
/// 
/// </summary>
public interface IMavLinkCommandEncoder
{
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