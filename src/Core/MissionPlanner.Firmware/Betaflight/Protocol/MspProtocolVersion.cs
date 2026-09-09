namespace MissionPlanner.Firmware.Betaflight.Protocol;

/// <summary>Supported native wire formats; jumbo and v2-over-v1 frames are not supported.</summary>
public enum MspProtocolVersion
{
    /// <summary>One-byte command and XOR checksum.</summary>
    V1,
    /// <summary>Native two-byte command with CRC-8 DVB-S2.</summary>
    V2
}