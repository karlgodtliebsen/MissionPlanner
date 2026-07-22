using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;

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
                new("Stabilize", 0, VehicleMode.Stabilize), new("Alt Hold", 2, VehicleMode.AltHold),
                new("Auto", 3), new("Guided", 4, VehicleMode.Guided), new("Loiter", 5, VehicleMode.Loiter),
                new("RTL", 6, VehicleMode.Rtl), new("Land", 9, VehicleMode.Land), new("Pos Hold", 16),
                new("Brake", 17), new("Smart RTL", 21)
            ],
            [FirmwareFamily.ArduPlane] =
            [
                new("Manual", 0), new("Circle", 1), new("Stabilize", 2, VehicleMode.Stabilize),
                new("Training", 3), new("Acro", 4), new("FBWA", 5), new("FBWB", 6), new("Cruise", 7),
                new("Auto", 10), new("RTL", 11, VehicleMode.Rtl), new("Loiter", 12, VehicleMode.Loiter),
                new("Takeoff", 13), new("Guided", 15, VehicleMode.Guided), new("QStabilize", 17),
                new("QHover", 18), new("QLoiter", 19), new("QLand", 20, VehicleMode.Land), new("QRTL", 21)
            ],
            [FirmwareFamily.Rover] =
            [
                new("Manual", 0), new("Acro", 1), new("Steering", 3), new("Hold", 4, VehicleMode.Loiter),
                new("Loiter", 5), new("Follow", 6), new("Simple", 7), new("Auto", 10),
                new("RTL", 11, VehicleMode.Rtl), new("Smart RTL", 12), new("Guided", 15, VehicleMode.Guided)
            ],
            [FirmwareFamily.ArduSub] =
            [
                new("Stabilize", 0, VehicleMode.Stabilize), new("Acro", 1),
                new("Alt Hold", 2, VehicleMode.AltHold), new("Auto", 3),
                new("Guided", 4, VehicleMode.Guided), new("Circle", 7),
                new("Surface", 9, VehicleMode.Rtl), new("Pos Hold", 16, VehicleMode.Loiter), new("Manual", 19)
            ]
        };

    /// <inheritdoc />
    public IReadOnlyList<VehicleModeOption> GetModes(FirmwareFamily family) =>
        modes.TryGetValue(family, out var result) ? result : [];

    /// <inheritdoc />
    public VehicleModeOption? Find(FirmwareFamily family, VehicleMode mode) =>
        GetModes(family).FirstOrDefault(candidate => candidate.SemanticMode == mode);
}
