namespace MissionPlanner.Firmware.Protocol;

internal static class ArduPilotBootloaderProtocol
{
    public const byte InSync = 0x12;
    public const byte EndOfCommand = 0x20;
    public const byte Ok = 0x10;
    public const byte Failed = 0x11;
    public const byte Invalid = 0x13;
    public const byte BadSiliconRevision = 0x14;
    public const byte GetSync = 0x21;
    public const byte GetDevice = 0x22;
    public const byte ChipErase = 0x23;
    public const byte ProgramMulti = 0x27;
    public const byte GetCrc = 0x29;
    public const byte GetChipDescription = 0x2e;
    public const byte Reboot = 0x30;
    public const byte ExternalErase = 0x34;
    public const byte ExternalProgramMulti = 0x35;
    public const byte ExternalGetCrc = 0x37;
    public const byte InfoBootloaderRevision = 0x01;
    public const byte InfoBoardId = 0x02;
    public const byte InfoBoardRevision = 0x03;
    public const byte InfoFlashSize = 0x04;
    public const byte InfoExternalFlashSize = 0x06;
    public const int MaximumProgramChunk = 252;
}
