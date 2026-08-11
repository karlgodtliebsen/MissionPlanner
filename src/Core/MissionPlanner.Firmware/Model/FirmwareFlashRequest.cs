using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Firmware.Model;

/// <summary>Contains the verified inputs passed to a platform flashing adapter.</summary>
/// <param name="VehicleId">The target vehicle identity before disconnect.</param>
/// <param name="Identity">The protocol-reported firmware and board identity.</param>
/// <param name="Package">The verified local firmware package.</param>
public sealed record FirmwareFlashRequest(VehicleId VehicleId, VehicleFirmwareIdentity Identity, FirmwarePackage Package);
