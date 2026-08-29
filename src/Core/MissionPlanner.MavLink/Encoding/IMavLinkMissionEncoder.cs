using MissionPlanner.MavLink.Missions;
namespace MissionPlanner.MavLink.Encoding;
/// <summary>
/// Provides the public API for IMavLinkMissionEncoder.
/// </summary>
public interface IMavLinkMissionEncoder
{
 /// <summary>
 /// Provides the public API for EncodeMissionCount.
 /// </summary>
 byte[] EncodeMissionCount(byte targetSystem, byte targetComponent, ushort count, MavMissionType missionType);
 /// <summary>
 /// Provides the public API for EncodeMissionItemInt.
 /// </summary>
 byte[] EncodeMissionItemInt(byte targetSystem, byte targetComponent, MavLinkMissionItem item);
 /// <summary>
 /// Provides the public API for EncodeMissionRequestList.
 /// </summary>
 byte[] EncodeMissionRequestList(byte targetSystem, byte targetComponent, MavMissionType missionType);
 /// <summary>
 /// Provides the public API for EncodeMissionRequestInt.
 /// </summary>
 byte[] EncodeMissionRequestInt(byte targetSystem, byte targetComponent, ushort sequence, MavMissionType missionType);
 /// <summary>
 /// Provides the public API for EncodeMissionAck.
 /// </summary>
 byte[] EncodeMissionAck(byte targetSystem, byte targetComponent, byte result, MavMissionType missionType);
 /// <summary>
 /// Provides the public API for EncodeMissionClearAll.
 /// </summary>
 byte[] EncodeMissionClearAll(byte targetSystem, byte targetComponent, MavMissionType missionType);
 /// <summary>Encodes the superseded MISSION_SET_CURRENT compatibility message.</summary>
 byte[] EncodeMissionSetCurrent(byte targetSystem, byte targetComponent, ushort sequence);
}
