using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Catalog;

/// <summary>Defines firmware catalogue target filters.</summary>
public sealed record FirmwareTargetQuery(
    FirmwareVehicleType? VehicleFamily = null,
    FirmwareReleaseChannel? ReleaseChannel = null,
    string? Platform = null,
    string? Manufacturer = null,
    int? BoardId = null,
    string? Bootloader = null,
    UsbIdentifier? UsbIdentifier = null,
    string? Version = null,
    string? GitSha = null,
    string? SearchText = null);
