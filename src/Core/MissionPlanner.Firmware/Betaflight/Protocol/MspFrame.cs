namespace MissionPlanner.Firmware.Betaflight.Protocol;

/// <summary>A checksum-validated response with an owned, bounded payload.</summary>
public sealed record MspFrame(MspProtocolVersion Version, ushort Command, byte[] Payload, bool IsError);