using System.Globalization;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Builds hardware diagnostics from the shared vehicle state and parameter registry.</summary>
public sealed class HwIdService : IHwIdService
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IVehicleParameterRegistry parameterRegistry;

    /// <summary>Initializes the HW ID service.</summary>
    public HwIdService(IActiveVehicleContext activeVehicle, IVehicleParameterRegistry parameterRegistry)
    {
        this.activeVehicle = activeVehicle;
        this.parameterRegistry = parameterRegistry;
    }

    /// <inheritdoc />
    public Task<HwIdSnapshot> GetAsync(VehicleId vehicleId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!activeVehicle.IsOnline || activeVehicle.VehicleId != vehicleId || activeVehicle.State is not { } state)
        {
            throw new InvalidOperationException("The target vehicle is no longer the active online vehicle.");
        }

        var firmware = state.Identity.Firmware;
        var items = parameterRegistry.GetAllParameters(vehicleId)
            .Where(pair => IsHardwareIdentifier(pair.Key))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new HwIdItem(pair.Key, pair.Value.Value, FormatIdentifier(pair.Value.Value)))
            .ToArray();
        var board = firmware.BoardVersion == 0
            ? "Board identification unavailable"
            : $"Board {firmware.BoardVersion} · vendor 0x{firmware.VendorId:X4} · product 0x{firmware.ProductId:X4}";
        var firmwareDescription = $"{firmware.Family} {firmware.FlightVersion}";
        return Task.FromResult(new HwIdSnapshot(vehicleId, board, firmwareDescription, items));
    }

    private static bool IsHardwareIdentifier(string name)
    {
        return (name.Contains("_ID", StringComparison.Ordinal) || name.Contains("_DEVID", StringComparison.Ordinal)) &&
            !name.Contains("_IDX", StringComparison.Ordinal) &&
            !name.Contains("FRSKY", StringComparison.Ordinal);
    }

    private static string FormatIdentifier(double value)
    {
        if (!double.IsFinite(value) || value < 0 || value > uint.MaxValue)
        {
            return "Unavailable or invalid identifier";
        }

        var identifier = Convert.ToUInt32(Math.Round(value));
        return $"0x{identifier.ToString("X8", CultureInfo.InvariantCulture)} ({identifier.ToString(CultureInfo.InvariantCulture)})";
    }
}
