using MissionPlanner.MavLink.Missions;
namespace MissionPlanner.MavLink.Encoding;
public interface IMavLinkMissionEncoder
{
 byte[] EncodeMissionCount(byte targetSystem, byte targetComponent, ushort count, MavMissionType missionType);
 byte[] EncodeMissionItemInt(byte targetSystem, byte targetComponent, MavLinkMissionItem item);
 byte[] EncodeMissionRequestList(byte targetSystem, byte targetComponent, MavMissionType missionType);
 byte[] EncodeMissionRequestInt(byte targetSystem, byte targetComponent, ushort sequence, MavMissionType missionType);
 byte[] EncodeMissionAck(byte targetSystem, byte targetComponent, byte result, MavMissionType missionType);
 byte[] EncodeMissionClearAll(byte targetSystem, byte targetComponent, MavMissionType missionType);
}
