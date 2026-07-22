using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Vehicles.Abstractions;

/// <summary>
/// Provides firmware-family-specific ArduPilot mode definitions.
/// </summary>
public interface IArduPilotModeCatalog
{
    /// <summary>Gets the modes supported by a firmware family.</summary>
    /// <param name="family">The firmware family.</param>
    /// <returns>The ordered supported modes, or an empty list for unsupported families.</returns>
    IReadOnlyList<VehicleModeOption> GetModes(FirmwareFamily family);

    /// <summary>Finds a common semantic mode for a firmware family.</summary>
    /// <param name="family">The firmware family.</param>
    /// <param name="mode">The semantic mode.</param>
    /// <returns>The matching mode option, or <see langword="null"/>.</returns>
    VehicleModeOption? Find(FirmwareFamily family, VehicleMode mode);
}
