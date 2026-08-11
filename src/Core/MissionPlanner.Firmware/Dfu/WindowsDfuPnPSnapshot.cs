namespace MissionPlanner.Firmware.Dfu;

/// <summary>Contains raw Windows Plug and Play evidence used by the DFU catalogue.</summary>
public sealed record WindowsDfuPnPSnapshot(
    string InstanceId,
    ushort VendorId,
    ushort ProductId,
    bool IsPresent,
    string? DevicePath = null,
    string? FriendlyName = null,
    string? Manufacturer = null,
    string? UsbSerialNumber = null,
    string? DriverService = null,
    string? DriverProvider = null,
    string? DriverVersion = null,
    int? ProblemCode = null,
    bool IsBusy = false);
