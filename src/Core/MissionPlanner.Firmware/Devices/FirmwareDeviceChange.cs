namespace MissionPlanner.Firmware.Devices;

/// <summary>Describes a serial device arrival or removal.</summary>
public sealed record FirmwareDeviceChange(FirmwareDeviceChangeKind Kind, Model.SerialDeviceDescriptor Device, DateTimeOffset Timestamp);
