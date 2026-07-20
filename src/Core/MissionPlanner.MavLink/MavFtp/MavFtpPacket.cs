using System.Text;

namespace MissionPlanner.MavLink.MavFtp;

/// <summary>
/// Provides the public API for MavFtpPacket.
/// </summary>
/// <param name="Sequence">The Sequence value.</param>
/// <param name="Session">The Session value.</param>
/// <param name="Opcode">The Opcode value.</param>
/// <param name="RequestedOpcode">The RequestedOpcode value.</param>
/// <param name="BurstComplete">The BurstComplete value.</param>
/// <param name="Offset">The Offset value.</param>
/// <param name="Data">The Data value.</param>
public sealed record MavFtpPacket(
    ushort Sequence,
    byte Session,
    MavFtpOpcode Opcode,
    MavFtpOpcode RequestedOpcode,
    bool BurstComplete,
    uint Offset,
    ReadOnlyMemory<byte> Data);
