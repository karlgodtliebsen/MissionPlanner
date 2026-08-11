using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Catalog;

/// <summary>Defines deterministic catalogue filtering and refresh behavior.</summary>
public sealed record FirmwareCatalogRequest(
    FirmwareVehicleType? VehicleType = null,
    FirmwareReleaseChannel? Channel = null,
    int? BoardId = null,
    UsbIdentifier? UsbIdentifier = null,
    bool ForceRefresh = false);
