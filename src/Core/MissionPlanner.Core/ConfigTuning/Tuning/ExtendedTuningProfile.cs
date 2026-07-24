using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.ConfigTuning.Tuning;

/// <summary>Defines an advanced tuning catalog for one firmware family.</summary>
/// <param name="Family">The firmware family.</param>
/// <param name="Descriptors">The ordered reusable descriptors.</param>
public sealed record ExtendedTuningProfile(
    FirmwareFamily Family,
    IReadOnlyList<AdvancedTuningDescriptor> Descriptors);
