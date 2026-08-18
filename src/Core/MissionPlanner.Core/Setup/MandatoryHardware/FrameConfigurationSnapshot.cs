using MissionPlanner.Firmware;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Contains the frame configuration supported by the connected firmware.</summary>
/// <param name="VehicleId">The vehicle from which the values were read.</param>
/// <param name="Family">The reported firmware family.</param>
/// <param name="Settings">Metadata-backed frame settings.</param>
/// <param name="Recommendations">Optional user-reviewed initial changes.</param>
public sealed record FrameConfigurationSnapshot(
    VehicleId VehicleId,
    FirmwareFamily Family,
    IReadOnlyList<FrameParameterSetting> Settings,
    IReadOnlyList<FrameInitialParameterRecommendation> Recommendations);
