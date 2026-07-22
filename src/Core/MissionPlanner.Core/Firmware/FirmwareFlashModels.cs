using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Firmware;

/// <summary>Describes whether a platform adapter can flash a selected package.</summary>
/// <param name="IsSupported">Whether flashing is supported.</param>
/// <param name="Reason">A user-facing support or blocking explanation.</param>
public sealed record FirmwareFlashSupport(bool IsSupported, string Reason);

/// <summary>Contains the verified inputs passed to a platform flashing adapter.</summary>
/// <param name="VehicleId">The target vehicle identity before disconnect.</param>
/// <param name="Identity">The protocol-reported firmware and board identity.</param>
/// <param name="Package">The verified local firmware package.</param>
public sealed record FirmwareFlashRequest(VehicleId VehicleId, VehicleFirmwareIdentity Identity, FirmwarePackage Package);

/// <summary>Reports the result of a platform firmware flashing operation.</summary>
/// <param name="Succeeded">Whether flashing completed successfully.</param>
/// <param name="Message">The adapter result detail.</param>
public sealed record FirmwareFlashResult(bool Succeeded, string Message);
