using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.MavLink.Services;

/// <summary>
/// Provides CRC extra bytes for common MAVLink and ArduPilotMega messages.
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

            case MessageIds.ParamValue:
                crcExtra = 220;
                return true;

            case MessageIds.ParamRequestRead:
                crcExtra = 214;
                return true;

            case MessageIds.ParamRequestList:
                crcExtra = 159;
                return true;
            case MessageIds.ParamSet:
                crcExtra = 168;
                return true;

            case MessageIds.GpsRawInt:
                crcExtra = 24;
                return true;

            case MessageIds.RawImu:
                crcExtra = 144;
                return true;

            case MessageIds.ScaledPressure:
                crcExtra = 115;
                return true;

            case MessageIds.Attitude:
                crcExtra = 39;
                return true;

            case MessageIds.LocalPositionNed:
                crcExtra = 185;
                return true;

            case MessageIds.GlobalPositionInt:
                crcExtra = 104;
                return true;

            case MessageIds.ServoOutputRaw:
                crcExtra = 222;
                return true;

            case MessageIds.MissionCurrent:
                crcExtra = 28;
                return true;

            case MessageIds.NavControllerOutput:
                crcExtra = 183;
                return true;

            case MessageIds.RcChannels:
                crcExtra = 118;
                return true;

            case MessageIds.VfrHud:
                crcExtra = 20;
                return true;

            case MessageIds.CommandLong:
                crcExtra = 152;
                return true;

            case MessageIds.CommandAck:
                crcExtra = 143;
                return true;

            case MessageIds.TimeSync:
                crcExtra = 34;
                return true;

            case MessageIds.PowerStatus:
                crcExtra = 203;
                return true;

            case MessageIds.BatteryStatus:
                crcExtra = 154;
                return true;

            case MessageIds.MemInfo:
                crcExtra = 208;
                return true;

            case MessageIds.Ahrs2:
                crcExtra = 47;
                return true;

            case MessageIds.EkfStatusReport:
                crcExtra = 71;
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
