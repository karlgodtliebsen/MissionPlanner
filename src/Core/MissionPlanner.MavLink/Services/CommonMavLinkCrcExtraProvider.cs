using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.MavLink.Services;

/// <summary>
/// Provides CRC extra bytes for common MAVLink messages.
/// </summary>
public sealed class CommonMavLinkCrcExtraProvider : IMavLinkCrcExtraProvider
{
    /// <inheritdoc/>
    public bool TryGetCrcExtra(uint messageId, out byte crcExtra)
    {
        switch (messageId)
        {
            case MessageIds.Heartbeat:
                crcExtra = 50;
                return true;

            case MessageIds.SysStatus:
                crcExtra = 124;
                return true;

            case MessageIds.Attitude:
                crcExtra = 39;
                return true;

            case MessageIds.GlobalPositionInt:
                crcExtra = 104;
                return true;

            case MessageIds.ParamRequestRead:
                crcExtra = 214;
                return true;

            case MessageIds.ParamRequestList:
                crcExtra = 159;
                return true;

            case MessageIds.ParamValue:
                crcExtra = 220;
                return true;

            case MessageIds.ParamSet:
                crcExtra = 168;
                return true;

            case MessageIds.CommandLong:
                crcExtra = 152;
                return true;

            case MessageIds.CommandAck:
                crcExtra = 143;
                return true;

            case MessageIds.StatusText:
                crcExtra = 83;
                return true;


            default:
                crcExtra = 0;
                return false;
        }
    }
}