namespace MissionPlanner.Firmware.Dfu;

/// <summary>Identifies one USB DFU device without pretending it is a serial port.</summary>
public sealed record DfuDeviceDescriptor(
    string ProviderId,
    ushort VendorId,
    ushort ProductId,
    DfuDriverState DriverState,
    string? ProductName = null,
    string? Manufacturer = null,
    string? SerialNumber = null,
    string? DevicePath = null,
    string? PnpInstanceId = null,
    string? DriverProvider = null,
    string? DriverVersion = null,
    int? ProblemCode = null,
    DateTimeOffset? ObservedAt = null,
    DateTimeOffset? ArrivedAt = null,
    DateTimeOffset? RemovedAt = null,
    int? ProviderUsbIndex = null);
