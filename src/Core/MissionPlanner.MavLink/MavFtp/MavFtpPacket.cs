using System.Text;

namespace MissionPlanner.MavLink.MavFtp;

public sealed record MavFtpPacket(
    ushort Sequence,
    byte Session,
    MavFtpOpcode Opcode,
    MavFtpOpcode RequestedOpcode,
    bool BurstComplete,
    uint Offset,
    ReadOnlyMemory<byte> Data);
