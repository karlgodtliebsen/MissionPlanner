using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Firmware;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Vehicles;

/// <summary>
/// Provides the stable ArduPilot custom-mode catalogs used by command presentation and encoding.
/// </summary>
public sealed class ArduPilotModeCatalog : IArduPilotModeCatalog
{
    private static readonly IReadOnlyDictionary<FirmwareFamily, IReadOnlyList<VehicleModeOption>> modes =
        new Dictionary<FirmwareFamily, IReadOnlyList<VehicleModeOption>>
        {
            [FirmwareFamily.ArduCopter] =
            [
                new VehicleModeOption("Stabilize", 0, VehicleMode.Stabilize), new VehicleModeOption("Alt Hold", 2, VehicleMode.AltHold),
                new VehicleModeOption("Auto", 3), new VehicleModeOption("Guided", 4, VehicleMode.Guided), new VehicleModeOption("Loiter", 5, VehicleMode.Loiter),
                new VehicleModeOption("RTL", 6, VehicleMode.Rtl), new VehicleModeOption("Land", 9, VehicleMode.Land), new VehicleModeOption("Pos Hold", 16),
                new VehicleModeOption("Brake", 17), new VehicleModeOption("Smart RTL", 21)
            ],
            [FirmwareFamily.ArduPlane] =
            [
                new VehicleModeOption("Manual", 0), new VehicleModeOption("Circle", 1), new VehicleModeOption("Stabilize", 2, VehicleMode.Stabilize),
                new VehicleModeOption("Training", 3), new VehicleModeOption("Acro", 4), new VehicleModeOption("FBWA", 5), new VehicleModeOption("FBWB", 6), new VehicleModeOption("Cruise", 7),
                new VehicleModeOption("Auto", 10), new VehicleModeOption("RTL", 11, VehicleMode.Rtl), new VehicleModeOption("Loiter", 12, VehicleMode.Loiter),
                new VehicleModeOption("Takeoff", 13), new VehicleModeOption("Guided", 15, VehicleMode.Guided), new VehicleModeOption("QStabilize", 17),
                new VehicleModeOption("QHover", 18), new VehicleModeOption("QLoiter", 19), new VehicleModeOption("QLand", 20, VehicleMode.Land), new VehicleModeOption("QRTL", 21)
            ],
            [FirmwareFamily.Rover] =
            [
                new VehicleModeOption("Manual", 0), new VehicleModeOption("Acro", 1), new VehicleModeOption("Steering", 3), new VehicleModeOption("Hold", 4, VehicleMode.Loiter),
                new VehicleModeOption("Loiter", 5), new VehicleModeOption("Follow", 6), new VehicleModeOption("Simple", 7), new VehicleModeOption("Auto", 10),
                new VehicleModeOption("RTL", 11, VehicleMode.Rtl), new VehicleModeOption("Smart RTL", 12), new VehicleModeOption("Guided", 15, VehicleMode.Guided)
            ],
            [FirmwareFamily.ArduSub] =
            [
                new VehicleModeOption("Stabilize", 0, VehicleMode.Stabilize), new VehicleModeOption("Acro", 1),
                new VehicleModeOption("Alt Hold", 2, VehicleMode.AltHold), new VehicleModeOption("Auto", 3),
                new VehicleModeOption("Guided", 4, VehicleMode.Guided), new VehicleModeOption("Circle", 7),
                new VehicleModeOption("Surface", 9, VehicleMode.Rtl), new VehicleModeOption("Pos Hold", 16, VehicleMode.Loiter), new VehicleModeOption("Manual", 19)
            ]
        };

    /// <inheritdoc />
    public IReadOnlyList<VehicleModeOption> GetModes(FirmwareFamily family)
    {
        return modes.TryGetValue(family, out var result) ? result : [];
    }

    /// <inheritdoc />
    public VehicleModeOption? Find(FirmwareFamily family, VehicleMode mode)
    {
        return GetModes(family).FirstOrDefault(candidate => candidate.SemanticMode == mode);
    }
}
